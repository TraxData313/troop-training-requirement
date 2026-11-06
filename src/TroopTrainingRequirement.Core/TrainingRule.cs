using System;
using System.Collections.Generic;
using System.Linq;

namespace TroopTrainingRequirement.Core
{
    /// <summary>One possible teacher: a hero's name, his raw value in each <see cref="CombatSkill"/> (indexed by the
    /// enum) and his culture's StringId (for the same culture bonus; null = none).</summary>
    public sealed class Trainer
    {
        public Trainer(string name, IReadOnlyList<int> skills, string? cultureId = null)
        {
            Name = name ?? "";
            Skills = skills ?? throw new ArgumentNullException(nameof(skills));
            CultureId = cultureId;
        }

        public string Name { get; }

        /// <summary>The hero's culture StringId, or null.</summary>
        public string? CultureId { get; }
        public IReadOnlyList<int> Skills { get; }

        public int Skill(CombatSkill skill) => (int)skill < Skills.Count ? Skills[(int)skill] : 0;
    }

    /// <summary>One skill the party can't teach: the troop needs <see cref="Required"/> from a trainer
    /// (its own skill minus the allowance), the best trainer has <see cref="Best"/> (with his same culture bonus).</summary>
    public sealed class Shortfall
    {
        public Shortfall(CombatSkill skill, int troopSkill, int required, int best, string? bestTrainer,
            int bestRaw = -1, int bonus = 0, string? bonusCulture = null)
        {
            Skill = skill;
            TroopSkill = troopSkill;
            Required = required;
            Best = best;
            BestTrainer = bestTrainer;
            BestRaw = bestRaw < 0 ? best : bestRaw;
            Bonus = bonus;
            BonusCulture = bonusCulture;
        }

        /// <summary>The best trainer's own (raw) skill value.</summary>
        public int BestRaw { get; }

        /// <summary>His same culture bonus as shown (<see cref="Best"/> - <see cref="BestRaw"/>); 0 when none.</summary>
        public int Bonus { get; }

        /// <summary>The culture adjective of the bonus ("Battanian"), or null.</summary>
        public string? BonusCulture { get; }

        public CombatSkill Skill { get; }

        /// <summary>The target troop's own skill value.</summary>
        public int TroopSkill { get; }

        /// <summary>The skill a trainer needs: troop skill minus the allowance.</summary>
        public int Required { get; }

        /// <summary>The best trainer's value as it counts (with his same culture bonus), rounded down - so it is always
        /// below <see cref="Required"/> here (0 when there are no trainers).</summary>
        public int Best { get; }

        /// <summary>The best trainer's name; null when there are no trainers.</summary>
        public string? BestTrainer { get; }
    }

    /// <summary>The answer for one target troop.</summary>
    public sealed class TrainingCheck
    {
        public static readonly TrainingCheck Free = new TrainingCheck(true, false, Array.Empty<Shortfall>(), null);

        internal TrainingCheck(bool allowed, bool unlockedByClanTier, IReadOnlyList<Shortfall> shortfalls, int? clanTierNeeded)
        {
            Allowed = allowed;
            UnlockedByClanTier = unlockedByClanTier;
            Shortfalls = shortfalls;
            ClanTierNeeded = clanTierNeeded;
        }

        public bool Allowed { get; }

        /// <summary>True when the clan tier let it through (skills were not checked; nothing to show).</summary>
        public bool UnlockedByClanTier { get; }

        /// <summary>Every failing skill, in <see cref="CombatSkill"/> order; empty when allowed.</summary>
        public IReadOnlyList<Shortfall> Shortfalls { get; }

        /// <summary>When blocked and the clan tier bypass is on: the clan tier that would unlock the target
        /// (null if the bypass is off, the party has no clan or no clan tier can reach it).</summary>
        public int? ClanTierNeeded { get; }
    }

    /// <summary>
    /// The rule (pure): a troop may be upgraded into a target only if, for every counted combat skill, the target's
    /// skill is at most the best trainer's value plus the allowance - or the target's tier is at most the owner's
    /// clan tier (when that bypass is on).
    /// </summary>
    public static class TrainingRule
    {
        /// <summary>Bannerlord's highest clan tier; targets above it can't be unlocked by clan tier.</summary>
        public const int MaxClanTier = 6;

        /// <param name="targetSkills">The target troop's raw skill values, indexed by <see cref="CombatSkill"/>.</param>
        /// <param name="targetTier">The target troop's tier.</param>
        /// <param name="trainers">The heroes who can teach (may be empty).</param>
        /// <param name="clanTier">The party owner's clan tier (negative = no clan, the bypass never applies).</param>
        /// <param name="targetCultureId">The target troop's culture StringId: trainers of that culture count with the
        /// same culture bonus (<see cref="CultureBonus"/>). Null = no bonus for anyone.</param>
        /// <param name="targetCultureName">The culture's display name (reason line fallback when no adjective is known).</param>
        public static TrainingCheck Check(IReadOnlyList<int> targetSkills, int targetTier, IReadOnlyList<Trainer> trainers,
            int clanTier, TrainingSettings settings, string? targetCultureId = null, string? targetCultureName = null)
        {
            if (targetSkills == null) throw new ArgumentNullException(nameof(targetSkills));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            trainers ??= Array.Empty<Trainer>();
            if (!settings.Enabled) return TrainingCheck.Free;

            if (settings.ClanTierUnlocks && clanTier >= 0 && targetTier <= clanTier)
                return new TrainingCheck(true, true, Array.Empty<Shortfall>(), null);

            int allowance = Math.Max(0, settings.Allowance);
            var shortfalls = new List<Shortfall>();
            for (int i = 0; i < CombatSkills.Count; i++)
            {
                var skill = (CombatSkill)i;
                if (!settings.Counts(skill)) continue;
                int troop = i < targetSkills.Count ? targetSkills[i] : 0;
                int required = Math.Max(0, troop - allowance);
                if (required <= 0) continue;

                // The best trainer is the best AFTER each one's own same culture bonus (doubles, no rounding).
                Trainer? best = null;
                double bestValue = 0;
                foreach (var t in trainers)
                {
                    if (t == null) continue;
                    double v = CultureBonus.Effective(t.Skill(skill), CultureBonus.Matches(targetCultureId, t.CultureId), settings);
                    if (best == null || v > bestValue)
                    {
                        best = t;
                        bestValue = v;
                    }
                }
                if (best != null && bestValue >= required) continue;
                int raw = best?.Skill(skill) ?? 0;
                int bonus = best == null ? 0 : CultureBonus.ShownBonus(raw, bestValue);
                shortfalls.Add(new Shortfall(skill, troop, required, best == null ? 0 : CultureBonus.Shown(bestValue), best?.Name,
                    raw, bonus, bonus > 0 ? CultureBonus.Adjective(targetCultureId, targetCultureName) : null));
            }

            if (shortfalls.Count == 0) return new TrainingCheck(true, false, shortfalls, null);
            int? tierNeeded = settings.ClanTierUnlocks && clanTier >= 0 && targetTier <= MaxClanTier ?Math.Max(0, targetTier) : (int?)null;
            return new TrainingCheck(false, false, shortfalls, tierNeeded);
        }

        /// <summary>One line per failing skill, e.g. "Requires Athletics 120 - your best is 110 (Cadan)" or, with the
        /// same culture bonus, "Requires Bow 220 - your best is 219 (Cadan, +40 Battanian)", then "or reach clan tier 5"
        /// when that would unlock it. Empty when allowed.</summary>
        public static IReadOnlyList<string> ReasonLines(TrainingCheck check)
        {
            if (check == null || check.Allowed) return Array.Empty<string>();
            var lines = check.Shortfalls.Select(Line).ToList();
            if (check.ClanTierNeeded is int tier) lines.Add("or reach clan tier " + tier);
            return lines;
        }

        /// <summary><see cref="ReasonLines"/> joined by newlines ("" when allowed).</summary>
        public static string ReasonText(TrainingCheck check) => string.Join("\n", ReasonLines(check));

        private static string Line(Shortfall s)
        {
            string head = "Requires " + CombatSkills.DisplayName(s.Skill) + " " + s.Required;
            return s.BestTrainer == null
                ? head + " - no one in the party can teach it"
                : head + " - your best is " + s.Best + " (" + s.BestTrainer + BonusText(s) + ")";
        }

        private static string BonusText(Shortfall s)
        {
            if (s.Bonus <= 0) return "";
            return ", +" + s.Bonus + (string.IsNullOrWhiteSpace(s.BonusCulture) ? "" : " " + s.BonusCulture);
        }
    }
}
