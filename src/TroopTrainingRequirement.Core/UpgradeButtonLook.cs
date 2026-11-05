using System.Collections.Generic;

namespace TroopTrainingRequirement.Core
{
    /// <summary>How the party screen's upgrade button looks when the rule blocks its target (pure, tested).</summary>
    public enum UpgradeLook
    {
        /// <summary>Vanilla's own look (not blocked by us, or vanilla already shows it unavailable / insufficient).</summary>
        Vanilla,

        /// <summary>Only the training rule stops it: orange #FFA028.</summary>
        BlockedByTraining,
    }

    /// <summary>The party screen's upgrade button: its look and its hint text.</summary>
    public static class UpgradeButtonLook
    {
        /// <summary>Orange only when vanilla would let the upgrade happen (enough XP, gold, items, perks) and only our
        /// rule stops it; vanilla's grey "not enough XP" / red "insufficient" looks win otherwise.</summary>
        public static UpgradeLook Look(bool blockedByTraining, bool vanillaAvailable, bool vanillaInsufficient)
            => blockedByTraining && vanillaAvailable && !vanillaInsufficient ? UpgradeLook.BlockedByTraining : UpgradeLook.Vanilla;

        /// <summary>Vanilla's hint ("Upgrade to X", XP, cost...) followed by our reason lines, one per line. Either may be
        /// missing (vanilla gives no hint when the target is not above the troop's level).</summary>
        public static string Hint(string? vanillaHint, IReadOnlyList<string>? reasonLines)
        {
            var reasons = reasonLines == null || reasonLines.Count == 0 ? "" : string.Join("\n", reasonLines);
            if (string.IsNullOrEmpty(vanillaHint)) return reasons;
            return reasons.Length == 0 ? vanillaHint! : vanillaHint + "\n" + reasons;
        }
    }
}
