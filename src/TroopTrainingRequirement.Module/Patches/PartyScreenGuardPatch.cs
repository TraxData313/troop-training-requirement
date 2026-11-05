using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TroopTrainingRequirement.Patches
{
    /// <summary>
    /// The party screen's single gate for upgrades: PartyScreenLogic.UpgradeTroop runs only when ValidateCommand says
    /// yes, and every upgrade button (single, x5, whole stack, the troop manager pop-up) ends there. Vanilla never asks
    /// the upgrade model there, so this postfix refuses an upgrade the rule blocks and shows the reason as a quick
    /// message. Trainers come from the screen's pending roster. UpgradeButtonPatch disables the buttons up front, so
    /// this is the safety net (other mods' commands, a stale button) and the message shouldn't show in normal play.
    /// UpgradeGatePatch asks the same question earlier, at the entry of UpgradeTroop (see there for why).
    /// </summary>
    internal static class PartyScreenGuardPatch
    {
        public static void Apply(Harmony harmony)
        {
            var original = AccessTools.Method(typeof(PartyScreenLogic), nameof(PartyScreenLogic.ValidateCommand))
                ?? throw new MissingMethodException("PartyScreenLogic.ValidateCommand");
            harmony.Patch(original, postfix: new HarmonyMethod(typeof(PartyScreenGuardPatch), nameof(Postfix)));
        }

        private static void Postfix(PartyScreenLogic __instance, PartyScreenLogic.PartyCommand command, ref bool __result)
        {
            try
            {
                if (!__result) return;
                var reason = BlockReason(__instance, command);
                if (reason == null) return;
                __result = false;
                ShowReason(reason);
            }
            catch (Exception e)
            {
                ModLog.ErrorOnce("partyScreen.validate", e);
            }
        }

        /// <summary>The rule's reason when <paramref name="command"/> is an upgrade it blocks, else null.</summary>
        internal static string? BlockReason(PartyScreenLogic logic, PartyScreenLogic.PartyCommand command)
        {
            if (logic == null || command == null || command.Code != PartyScreenLogic.PartyCommandCode.UpgradeTroop) return null;
            var troop = command.Character;
            if (troop?.UpgradeTargets == null || command.UpgradeTarget < 0 || command.UpgradeTarget >= troop.UpgradeTargets.Length) return null;
            var side = command.RosterSide;
            if (side != PartyScreenLogic.PartyRosterSide.Left && side != PartyScreenLogic.PartyRosterSide.Right) return null;
            var party = side == PartyScreenLogic.PartyRosterSide.Right ? logic.RightOwnerParty : logic.LeftOwnerParty;
            var roster = logic.MemberRosters?[(int)side];
            return Training.BlockReason(party, roster, troop, troop.UpgradeTargets[command.UpgradeTarget]);
        }

        internal static void ShowReason(string reason)
        {
            var text = new TextObject("{=!}{REASON}");
            text.SetTextVariable("REASON", reason);
            MBInformationManager.AddQuickInformation(text);
        }
    }
}
