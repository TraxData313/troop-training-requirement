using System;
using System.Collections.Generic;
using System.Linq;

namespace TroopTrainingRequirement.Core
{
    /// <summary>One possible teacher: a hero's name and his raw value in each <see cref="CombatSkill"/> (indexed by the enum).</summary>
    public sealed class Trainer
    {
        public Trainer(string name, IReadOnlyList<int> skills)
        {
            Name = name ?? "";
            Skills = skills ?? throw new ArgumentNullException(nameof(skills));
        }

        public string Name { get; }
        public IReadOnlyList<int> Skills { get; }

        public int Skill(CombatSkill skill) => (int)skill < Skills.Count ? Skills[(int)skill] : 0;
    }

    /// <summary>One skill the party can't teach: the troop needs <see cref="Required"/> from a trainer
    /// (its own skill minus the allowance), the best trainer has <see cref="Best"/>.</summary>
    public sealed class Shortfall
    {
        public Shortfall(CombatSkill skill, int troopSkill, int required, int best, string? bestTrainer)
        {
            Skill = skill;
            TroopSkill = troopSkill;
            Required = required;
            Best = best;
            BestTrainer = bestTrainer;
        }

        public CombatSkill Skill { get; }

        /// <summary>The target troop's own skill value.</summary>
        public int TroopSkill { get; }

        /// <summary>The skill a trainer needs: troop skill minus the allowance.</summary>
        public int Required { get; }

        /// <summary>The best trainer's value (0 when there are no trainers).</summary>
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
        public static TrainingCheck Check(IReadOnlyList<int> targetSkills, int targetTier, IReadOnlyList<Trainer> trainers,
            int clanTier, TrainingSettings settings)
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

                Trainer? best = null;
                foreach (var t in trainers)
                {
                    if (t == null) continue;
                    if (best == null || t.Skill(skill) > best.Skill(skill)) best = t;
                }
                int bestValue = best?.Skill(skill) ?? 0;
                if (bestValue >= required) continue;
                shortfalls.Add(new Shortfall(skill, troop, required, bestValue, best?.Name));
            }

            if (shortfalls.Count == 0) return new TrainingCheck(true, false, shortfalls, null);
            int? tierNeeded = settings.ClanTierUnlocks && clanTier >= 0 && targetTier <= MaxClanTier ?Math.Max(0, targetTier) : (int?)null;
            return new TrainingCheck(false, false, shortfalls, tierNeeded);
        }

        /// <summary>One line per failing skill, e.g. "Requires Athletics 120 - your best is 110 (Cadan)", then
        /// "or reach clan tier 5" when that would unlock it. Empty when allowed.</summary>
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
                : head + " - your best is " + s.Best + " (" + s.BestTrainer + ")";
        }
    }
}
