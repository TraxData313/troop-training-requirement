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
    /// message. Trainers come from the screen's pending roster. Step 3 disables the buttons up front; this stays as
    /// the safety net.
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
                if (!__result || command == null || command.Code != PartyScreenLogic.PartyCommandCode.UpgradeTroop) return;
                var troop = command.Character;
                if (troop == null || command.UpgradeTarget < 0 || command.UpgradeTarget >= troop.UpgradeTargets.Length) return;
                var side = command.RosterSide;
                if (side != PartyScreenLogic.PartyRosterSide.Left && side != PartyScreenLogic.PartyRosterSide.Right) return;
                var party = side == PartyScreenLogic.PartyRosterSide.Right ? __instance.RightOwnerParty : __instance.LeftOwnerParty;
                var roster = __instance.MemberRosters?[(int)side];
                var reason = Training.BlockReason(party, roster, troop, troop.UpgradeTargets[command.UpgradeTarget]);
                if (reason == null) return;
                __result = false;
                var text = new TextObject("{=!}{REASON}");
                text.SetTextVariable("REASON", reason);
                MBInformationManager.AddQuickInformation(text);
            }
            catch (Exception e)
            {
                ModLog.ErrorOnce("partyScreen.validate", e);
            }
        }
    }
}
