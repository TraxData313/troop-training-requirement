namespace TroopTrainingRequirement.Core
{
    /// <summary>
    /// Ordering against other mods' Harmony patches on PartyScreenLogic.UpgradeTroop. The module's UpgradeGatePatch
    /// uses these values; tests\TroopTrainingRequirement.Compat.Tests proves with the game's own 0Harmony.dll that a
    /// prefix built from them runs before - and, when it blocks, skips - Troop Equipment Upgrade's prefix.
    /// </summary>
    public static class CompatOrder
    {
        /// <summary>
        /// The Harmony id of Troop Equipment Upgrade (Workshop 3674824323). Its prefix on PartyScreenLogic.UpgradeTroop
        /// reserves (and, in its town "market" mode, prices) the gear for the upgrade; the items and gold are taken
        /// when the party screen's Done is pressed.
        /// </summary>
        public const string TroopEquipmentUpgradeHarmonyId = "troop.equipment.upgrade";

        /// <summary>The gate prefix's priority: HarmonyLib.Priority.First (Core has no Harmony reference).</summary>
        public const int UpgradeGatePriority = 800;

        /// <summary>Harmony ids the gate prefix is ordered before (HarmonyBefore), on top of the priority.</summary>
        public static readonly string[] UpgradeGateBefore = { TroopEquipmentUpgradeHarmonyId };
    }
}
