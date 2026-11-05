using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace TroopTrainingRequirement.Patches
{
    /// <summary>
    /// Automatic upgrades of every party but the player's (daily tick and after each battle):
    /// PartyUpgraderCampaignBehavior.GetPossibleUpgradeTargets only asks the upgrade model's
    /// CanPartyUpgradeTroopToTarget for bandit troops, so this postfix drops every target the rule blocks from the
    /// list it returns. The troop keeps its XP; with no target left it simply isn't upgraded.
    /// </summary>
    internal static class AutoUpgradePatch
    {
        private static FieldInfo? _upgradeTarget;

        public static void Apply(Harmony harmony)
        {
            var original = AccessTools.Method(typeof(PartyUpgraderCampaignBehavior), "GetPossibleUpgradeTargets")
                ?? throw new MissingMethodException("PartyUpgraderCampaignBehavior.GetPossibleUpgradeTargets");
            var args = AccessTools.Inner(typeof(PartyUpgraderCampaignBehavior), "TroopUpgradeArgs")
                ?? throw new MissingMemberException("PartyUpgraderCampaignBehavior.TroopUpgradeArgs");
            _upgradeTarget = AccessTools.Field(args, "UpgradeTarget")
                ?? throw new MissingFieldException("TroopUpgradeArgs.UpgradeTarget");
            harmony.Patch(original, postfix: new HarmonyMethod(typeof(AutoUpgradePatch), nameof(Postfix)));
        }

        private static void Postfix(PartyBase party, TroopRosterElement element, object __result)
        {
            try
            {
                if (!(__result is IList list) || list.Count == 0 || _upgradeTarget == null) return;
                if (!Training.Settings.AppliesTo(Training.Classify(party))) return;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var target = _upgradeTarget.GetValue(list[i]) as CharacterObject;
                    if (!Training.Check(party, element.Character, target).Allowed) list.RemoveAt(i);
                }
            }
            catch (Exception e)
            {
                ModLog.ErrorOnce("autoUpgrade", e);
            }
        }
    }
}
