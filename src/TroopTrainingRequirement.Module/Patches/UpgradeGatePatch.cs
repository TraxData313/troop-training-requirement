using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TroopTrainingRequirement.Core;
using TaleWorlds.CampaignSystem.Party;

namespace TroopTrainingRequirement.Patches
{
    /// <summary>
    /// The rule's check at the very ENTRY of PartyScreenLogic.UpgradeTroop, before any other mod's prefix.
    ///
    /// Why: vanilla's UpgradeTroop calls ValidateCommand (where PartyScreenGuardPatch refuses) inside its own body, so
    /// every Harmony prefix on UpgradeTroop runs before that check. Troop Equipment Upgrade (Harmony id
    /// "troop.equipment.upgrade") has exactly such a prefix: it reserves the gear for the upgrade (and in its town
    /// market mode adds the purchase price), and its DoneLogic prefix later takes those items and that gold. If the
    /// rule then refused the upgrade in ValidateCommand, the player would pay for gear of an upgrade that never
    /// happened. This prefix runs first (Priority.First + HarmonyBefore that id) and, when the rule blocks, returns
    /// false: Harmony 2 then skips the original AND every later prefix, so that mod never reserves anything
    /// (proved with the game's 0Harmony.dll in tests\TroopTrainingRequirement.Compat.Tests). The upgrade buttons
    /// are disabled up front (UpgradeButtonPatch), so this only fires for a stale button or another mod's command.
    ///
    /// Postfixes still run after a skipped original; that mod's gold-text postfix trusts its static
    /// UpgradeSession.LastUpgradeSucceeded, so it is cleared here (reflection, fails soft with one log line; without
    /// that mod nothing happens). No hard dependency: with or without it the prefix is the same.
    /// </summary>
    internal static class UpgradeGatePatch
    {
        private const string TeuSessionType = "TroopEquipmentUpgrade.Patches.UpgradeSession";

        private static FieldInfo? _teuLastUpgradeSucceeded;

        public static void Apply(Harmony harmony)
        {
            var original = AccessTools.Method(typeof(PartyScreenLogic), "UpgradeTroop")
                ?? throw new MissingMethodException("PartyScreenLogic.UpgradeTroop");
            var prefix = new HarmonyMethod(typeof(UpgradeGatePatch), nameof(Prefix))
            {
                priority = CompatOrder.UpgradeGatePriority,
                before = CompatOrder.UpgradeGateBefore,
            };
            harmony.Patch(original, prefix: prefix);
            FindTroopEquipmentUpgrade();
            var info = Harmony.GetPatchInfo(original);
            if (info != null)
                ModLog.Info("compat", "PartyScreenLogic.UpgradeTroop prefixes (owner:priority): "
                    + string.Join(", ", info.Prefixes.OrderByDescending(p => p.priority).Select(p => p.owner + ":" + p.priority)));
        }

        private static bool Prefix(PartyScreenLogic __instance, PartyScreenLogic.PartyCommand command)
        {
            try
            {
                var reason = PartyScreenGuardPatch.BlockReason(__instance, command);
                if (reason == null) return true;
                ClearTeuLastUpgradeSucceeded();
                PartyScreenGuardPatch.ShowReason(reason);
                return false;
            }
            catch (Exception e)
            {
                ModLog.ErrorOnce("partyScreen.upgradeGate", e);
                return true; // vanilla's ValidateCommand (with PartyScreenGuardPatch) still checks
            }
        }

        private static void FindTroopEquipmentUpgrade()
        {
            try
            {
                var type = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => a.GetName().Name == "TroopEquipmentUpgrade")
                    .Select(a => a.GetType(TeuSessionType, false))
                    .FirstOrDefault(t => t != null);
                if (type == null)
                {
                    ModLog.Info("compat", "Troop Equipment Upgrade not loaded - the upgrade gate runs alone");
                    return;
                }
                _teuLastUpgradeSucceeded = type.GetField("LastUpgradeSucceeded", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                ModLog.Info("compat", _teuLastUpgradeSucceeded != null && _teuLastUpgradeSucceeded.FieldType == typeof(bool)
                    ? "Troop Equipment Upgrade found - the upgrade gate runs before its UpgradeTroop prefix"
                    : "Troop Equipment Upgrade found but UpgradeSession.LastUpgradeSucceeded is missing - the gate still runs first; its gold text may be off after a blocked upgrade");
                if (_teuLastUpgradeSucceeded?.FieldType != typeof(bool)) _teuLastUpgradeSucceeded = null;
            }
            catch (Exception e)
            {
                _teuLastUpgradeSucceeded = null;
                ModLog.Info("compat", "Troop Equipment Upgrade lookup failed (the gate still runs first): " + e.Message);
            }
        }

        private static void ClearTeuLastUpgradeSucceeded()
        {
            if (_teuLastUpgradeSucceeded == null) return;
            try { _teuLastUpgradeSucceeded.SetValue(null, false); }
            catch (Exception e)
            {
                _teuLastUpgradeSucceeded = null;
                ModLog.Info("compat", "could not clear Troop Equipment Upgrade's LastUpgradeSucceeded (left alone from now on): " + e.Message);
            }
        }
    }
}
