using System;
using System.Collections.Generic;
using TroopTrainingRequirement.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace TroopTrainingRequirement
{
    /// <summary>
    /// The game side of the rule: reads the target troop's skills, the trainers (heroes) and the clan tier, and feeds
    /// them to <see cref="TrainingRule"/>. Every entry point here is safe to call from game code: on any failure it
    /// logs once and answers "allowed" (vanilla behaviour).
    ///
    /// Step 3 (party screen) API: <see cref="Check(PartyBase, TroopRoster, CharacterObject, CharacterObject)"/> with the
    /// screen's pending roster, then <see cref="TrainingRule.ReasonText"/> / <see cref="TrainingRule.ReasonLines"/>.
    /// </summary>
    internal static class Training
    {
        /// <summary>The live settings (MCM writes into this object through its ProxyRefs).</summary>
        public static TrainingSettings Settings { get; } = new TrainingSettings();

        private static readonly SkillObject[] SkillObjects = new SkillObject[CombatSkills.Count];

        private static SkillObject Skill(CombatSkill s)
        {
            var cached = SkillObjects[(int)s];
            if (cached != null) return cached;
            var skill = s switch
            {
                CombatSkill.OneHanded => DefaultSkills.OneHanded,
                CombatSkill.TwoHanded => DefaultSkills.TwoHanded,
                CombatSkill.Polearm => DefaultSkills.Polearm,
                CombatSkill.Bow => DefaultSkills.Bow,
                CombatSkill.Crossbow => DefaultSkills.Crossbow,
                CombatSkill.Throwing => DefaultSkills.Throwing,
                CombatSkill.Riding => DefaultSkills.Riding,
                _ => DefaultSkills.Athletics,
            };
            SkillObjects[(int)s] = skill;
            return skill;
        }

        /// <summary>Forget cached game objects (a new game has new SkillObject instances).</summary>
        public static void Reset() => Array.Clear(SkillObjects, 0, SkillObjects.Length);

        /// <summary>Which "whose parties" setting covers this party.</summary>
        public static PartyKind Classify(PartyBase? party)
        {
            if (party == null) return PartyKind.Other;
            var mobile = party.MobileParty;
            var leader = party.LeaderHero;
            return PartyKinds.Classify(
                isMainParty: party == PartyBase.MainParty,
                isMobile: mobile != null,
                hasLeaderHero: leader != null,
                leaderInPlayerClan: leader?.Clan != null && leader.Clan == Clan.PlayerClan,
                isCaravan: mobile != null && mobile.IsCaravan,
                isBandit: mobile != null && mobile.IsBandit);
        }

        /// <summary>The upgrade check against the party's real member roster (campaign models, auto-upgrades).</summary>
        public static TrainingCheck Check(PartyBase? party, CharacterObject? troop, CharacterObject? target)
            => Check(party, party?.MemberRoster, troop, target);

        /// <summary>
        /// The upgrade check with the trainers taken from <paramref name="roster"/> (the party screen passes its pending
        /// roster, so a companion moved out on the screen stops teaching at once). <see cref="TrainingCheck.Free"/> when
        /// the rule doesn't cover the party, or on any error.
        /// </summary>
        public static TrainingCheck Check(PartyBase? party, TroopRoster? roster, CharacterObject? troop, CharacterObject? target)
        {
            try
            {
                if (target == null || target.IsHero || (troop != null && troop.IsHero)) return TrainingCheck.Free;
                if (!Settings.AppliesTo(Classify(party))) return TrainingCheck.Free;
                var clan = ClanOf(party!);
                // Same culture bonus: trainers of the TARGET troop's culture count boosted (Core CultureBonus).
                var culture = target.Culture;
                return TrainingRule.Check(SkillsOf(target), target.Tier, TrainersOf(roster, clan), clan?.Tier ?? -1, Settings,
                    culture?.StringId, culture?.Name?.ToString());
            }
            catch (Exception e)
            {
                ModLog.ErrorOnce("training.check", e);
                return TrainingCheck.Free;
            }
        }

        /// <summary>The blocking reason ("Requires Athletics 120 - your best is 110 (Cadan)", or with the same culture
        /// bonus "Requires Bow 220 - your best is 219 (Cadan, +40 Battanian)", one line per skill), or null when the
        /// upgrade is allowed.</summary>
        public static string? BlockReason(PartyBase? party, TroopRoster? roster, CharacterObject? troop, CharacterObject? target)
        {
            var check = Check(party, roster, troop, target);
            return check.Allowed ? null : TrainingRule.ReasonText(check);
        }

        private static Clan? ClanOf(PartyBase party)
        {
            if (party == PartyBase.MainParty) return Clan.PlayerClan;
            return party.MobileParty?.ActualClan ?? party.Owner?.Clan ?? party.LeaderHero?.Clan;
        }

        private static int[] SkillsOf(CharacterObject character)
        {
            var values = new int[CombatSkills.Count];
            for (int i = 0; i < values.Length; i++)
                values[i] = character.GetSkillValue(Skill((CombatSkill)i));
            return values;
        }

        private static List<Trainer> TrainersOf(TroopRoster? roster, Clan? clan)
        {
            var seen = new HashSet<Hero>();
            var trainers = new List<Trainer>();
            if (roster != null)
            {
                for (int i = 0; i < roster.Count; i++)
                {
                    var c = roster.GetCharacterAtIndex(i);
                    if (c != null && c.IsHero) Add(c.HeroObject, trainers, seen);
                }
            }
            if (Settings.CountWholeClan && clan != null)
            {
                foreach (var h in clan.Heroes) if (h != null && !h.IsChild) Add(h, trainers, seen);
                foreach (var h in clan.Companions) if (h != null && !h.IsChild) Add(h, trainers, seen);
            }
            return trainers;
        }

        private static void Add(Hero? hero, List<Trainer> trainers, HashSet<Hero> seen)
        {
            if (hero == null || !hero.IsAlive || hero.IsPrisoner || !seen.Add(hero)) return;
            var values = new int[CombatSkills.Count];
            for (int i = 0; i < values.Length; i++)
                values[i] = hero.GetSkillValue(Skill((CombatSkill)i));
            trainers.Add(new Trainer(hero.Name?.ToString() ?? "?", values, hero.Culture?.StringId));
        }
    }
}
