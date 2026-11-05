using System;
using System.Collections.Generic;
using HarmonyLib;
using TroopTrainingRequirement.Core;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets.Party;

namespace TroopTrainingRequirement.Patches
{
    /// <summary>
    /// The party screen's upgrade buttons (the troop row and the "Upgrade Troops" pop-up, both <c>UpgradeTargetVM</c>).
    ///
    /// VM side: vanilla's <c>PartyCharacterVM.InitializeUpgrades</c> builds every target's availability and hint (XP, gold,
    /// horse items, the bandit perk) and never asks the upgrade model for regular troops. A postfix re-checks each target
    /// with <see cref="Training.Check(PartyBase, TaleWorlds.CampaignSystem.Roster.TroopRoster, TaleWorlds.CampaignSystem.CharacterObject, TaleWorlds.CampaignSystem.CharacterObject)"/>
    /// (the screen's pending roster, so a companion moved out stops teaching at once) and, when blocked, does what vanilla
    /// does for a bandit without the perk: IsInsufficient = true (ExecuteUpgrade refuses; the pop-up and Ctrl/Shift-click
    /// go through it too), AvailableUpgrades = 0 (the troop's upgrade count, the pop-up's open button), and appends our
    /// reason lines to the hint. InitializeUpgrades runs on screen open, after every upgrade, when the pop-up opens and
    /// for a moved troop; <c>PartyVM.Update</c> gets a postfix so moving a HERO refreshes every row.
    ///
    /// Widget side (orange, no UIExtenderEx, no prefab change): <c>PartyUpgradeButtonWidget.UpdateVisual</c> picks
    /// vanilla's default / unavailable / insufficient brush from IsAvailable + IsInsufficient. The widget knows nothing
    /// else, but its portrait child's <c>ImageId</c> is the target's character code - the VM records the codes of the
    /// targets only our rule stops, and a postfix swaps the brush for ours (<c>module\GUI\Brushes\TroopTrainingRequirement.xml</c>)
    /// and tints the portrait orange. The portrait's ImageId may be bound after the button's flags, so its setter re-runs
    /// UpdateVisual for upgrade buttons.
    /// </summary>
    internal static class UpgradeButtonPatch
    {
        private const string OrangeBrush = "TroopTrainingRequirement.UpgradeSlot.ButtonBlocked";
        private static readonly Color OrangePortrait = new Color(1f, 0.72f, 0.4f);

        private static readonly AccessTools.FieldRef<PartyCharacterVM, PartyScreenLogic> ScreenLogic =
            AccessTools.FieldRefAccess<PartyCharacterVM, PartyScreenLogic>("_partyScreenLogic");

        private static readonly AccessTools.FieldRef<UpgradeTargetVM, string> HintString =
            AccessTools.FieldRefAccess<UpgradeTargetVM, string>("_hintString");

        private static Action<PartyVM>? _refreshTroopsUpgradeable;
        private static Action<PartyVM>? _updatePopUpCounts;
        private static Action<PartyUpgradeButtonWidget>? _updateVisual;

        /// <summary>Character codes (portrait ImageIds) of the targets only our rule stops.</summary>
        private static readonly HashSet<string> OrangeIds = new HashSet<string>();

        /// <summary>Upgrade buttons seen on screen, to re-pick their brush when only our verdict changed.</summary>
        private static readonly List<WeakReference<PartyUpgradeButtonWidget>> Live = new List<WeakReference<PartyUpgradeButtonWidget>>();

        private static bool _brushMissingLogged;

        public static void Apply(Harmony harmony)
        {
            var initialize = AccessTools.Method(typeof(PartyCharacterVM), nameof(PartyCharacterVM.InitializeUpgrades))
                ?? throw new MissingMethodException("PartyCharacterVM.InitializeUpgrades");
            var update = AccessTools.Method(typeof(PartyVM), "Update", new[] { typeof(PartyScreenLogic.PartyCommand) })
                ?? throw new MissingMethodException("PartyVM.Update");
            var refresh = AccessTools.Method(typeof(PartyVM), "RefreshTroopsUpgradeable")
                ?? throw new MissingMethodException("PartyVM.RefreshTroopsUpgradeable");
            var counts = AccessTools.Method(typeof(PartyVM), "UpdateTroopManagerPopUpCounts")
                ?? throw new MissingMethodException("PartyVM.UpdateTroopManagerPopUpCounts");
            _refreshTroopsUpgradeable = AccessTools.MethodDelegate<Action<PartyVM>>(refresh);
            _updatePopUpCounts = AccessTools.MethodDelegate<Action<PartyVM>>(counts);

            harmony.Patch(initialize, postfix: new HarmonyMethod(typeof(UpgradeButtonPatch), nameof(InitializeUpgradesPostfix)));
            harmony.Patch(update, postfix: new HarmonyMethod(typeof(UpgradeButtonPatch), nameof(UpdatePostfix)));

            // The orange look is optional: without it the button still shows vanilla's red "insufficient" look.
            try
            {
                var visual = AccessTools.Method(typeof(PartyUpgradeButtonWidget), "UpdateVisual")
                    ?? throw new MissingMethodException("PartyUpgradeButtonWidget.UpdateVisual");
                var imageId = AccessTools.PropertySetter(typeof(ImageIdentifierWidget), nameof(ImageIdentifierWidget.ImageId))
                    ?? throw new MissingMethodException("ImageIdentifierWidget.ImageId setter");
                _updateVisual = AccessTools.MethodDelegate<Action<PartyUpgradeButtonWidget>>(visual);
                harmony.Patch(visual, postfix: new HarmonyMethod(typeof(UpgradeButtonPatch), nameof(UpdateVisualPostfix)));
                harmony.Patch(imageId, postfix: new HarmonyMethod(typeof(UpgradeButtonPatch), nameof(ImageIdPostfix)));
            }
            catch (Exception e)
            {
                ModLog.Error("patches", "orange upgrade button NOT applied (vanilla's red look is used)", e);
            }
        }

        // ---- VM: availability, count and hint

        private static void InitializeUpgradesPostfix(PartyCharacterVM __instance)
        {
            try
            {
                if (!__instance.IsUpgradableTroop || __instance.Upgrades == null) return;
                var logic = ScreenLogic(__instance);
                if (logic == null || logic.IsTroopUpgradesDisabled) return;
                var troop = __instance.Character;
                if (troop == null) return;

                var party = logic.RightOwnerParty;
                var roster = logic.MemberRosters?[(int)PartyScreenLogic.PartyRosterSide.Right];
                if (!Training.Settings.AppliesTo(Training.Classify(party))) { ForgetAll(__instance); return; }

                bool anyBlocked = false;
                var targets = troop.UpgradeTargets;
                for (int i = 0; i < __instance.Upgrades.Count && i < targets.Length; i++)
                {
                    var up = __instance.Upgrades[i];
                    if (up == null) continue;
                    var check = Training.Check(party, roster, troop, targets[i]);
                    var id = up.TroopImage?.Id;
                    if (check.Allowed)
                    {
                        SetOrange(id, false);
                        continue;
                    }

                    anyBlocked = true;
                    // Before IsInsufficient flips: the widget picks its brush in that setter.
                    SetOrange(id, UpgradeButtonLook.Look(true, up.IsAvailable, up.IsInsufficient) == UpgradeLook.BlockedByTraining);
                    HintString(up) = UpgradeButtonLook.Hint(HintString(up), TrainingRule.ReasonLines(check));
                    up.AvailableUpgrades = 0;
                    up.IsInsufficient = true;
                }

                if (!anyBlocked) return;
                int most = 0;
                foreach (var up in __instance.Upgrades)
                    if (up != null && up.AvailableUpgrades > most) most = up.AvailableUpgrades;
                __instance.NumOfUpgradeableTroops = most;
                __instance.IsTroopUpgradable = most > 0;
            }
            catch (Exception e)
            {
                ModLog.ErrorOnce("upgradeButton.initialize", e);
            }
        }

        private static void ForgetAll(PartyCharacterVM vm)
        {
            foreach (var up in vm.Upgrades) if (up != null) SetOrange(up.TroopImage?.Id, false);
        }

        /// <summary>A hero moved on the screen changes who can teach: re-check every row (vanilla re-checks only the moved
        /// troop) and the pop-up button's count. Vanilla already refreshes everything after an upgrade.</summary>
        private static void UpdatePostfix(PartyVM __instance, PartyScreenLogic.PartyCommand command)
        {
            try
            {
                if (command == null) return;
                var code = command.Code;
                bool heroMoved = command.Character != null && command.Character.IsHero &&
                    (code == PartyScreenLogic.PartyCommandCode.TransferTroop
                     || code == PartyScreenLogic.PartyCommandCode.TransferPartyLeaderTroop
                     || code == PartyScreenLogic.PartyCommandCode.TransferTroopToLeaderSlot);
                if (!heroMoved && code != PartyScreenLogic.PartyCommandCode.TransferAllTroops) return;
                _refreshTroopsUpgradeable?.Invoke(__instance);
                _updatePopUpCounts?.Invoke(__instance);
            }
            catch (Exception e)
            {
                ModLog.ErrorOnce("upgradeButton.update", e);
            }
        }

        private static void SetOrange(string? id, bool orange)
        {
            if (string.IsNullOrEmpty(id)) return;
            bool changed = orange ? OrangeIds.Add(id!) : OrangeIds.Remove(id!);
            if (changed) RefreshWidgets(id!);
        }

        // ---- widget: the orange brush

        private static void RefreshWidgets(string id)
        {
            if (_updateVisual == null) return;
            for (int i = Live.Count - 1; i >= 0; i--)
            {
                if (!Live[i].TryGetTarget(out var w) || w == null || w.ParentWidget == null) { Live.RemoveAt(i); continue; }
                if (w.ImageIdentifierWidget?.ImageId == id) _updateVisual(w);
            }
        }

        private static void Track(PartyUpgradeButtonWidget widget)
        {
            for (int i = Live.Count - 1; i >= 0; i--)
            {
                if (!Live[i].TryGetTarget(out var w) || w == null) { Live.RemoveAt(i); continue; }
                if (ReferenceEquals(w, widget)) return;
            }
            Live.Add(new WeakReference<PartyUpgradeButtonWidget>(widget));
        }

        private static void ImageIdPostfix(ImageIdentifierWidget __instance)
        {
            try
            {
                if (_updateVisual != null && __instance.ParentWidget is PartyUpgradeButtonWidget button
                    && ReferenceEquals(button.ImageIdentifierWidget, __instance))
                    _updateVisual(button);
            }
            catch (Exception e)
            {
                ModLog.ErrorOnce("upgradeButton.imageId", e);
            }
        }

        private static void UpdateVisualPostfix(PartyUpgradeButtonWidget __instance)
        {
            try
            {
                var image = __instance.ImageIdentifierWidget;
                if (image == null || __instance.UnavailableBrush == null || __instance.InsufficientBrush == null) return;
                Track(__instance);
                // Vanilla's insufficient branch: available but stopped. Ours only when the target is on our list.
                if (!__instance.IsAvailable || !__instance.IsInsufficient) return;
                var id = image.ImageId;
                if (string.IsNullOrEmpty(id) || !OrangeIds.Contains(id)) return;

                var brush = __instance.Context?.GetBrush(OrangeBrush);
                if (brush == null)
                {
                    if (!_brushMissingLogged)
                    {
                        _brushMissingLogged = true;
                        ModLog.Info("partyScreen", "orange upgrade brush not found (GUI\\Brushes\\TroopTrainingRequirement.xml missing?) - vanilla look");
                    }
                    return;
                }
                __instance.Brush = brush;
                image.Brush.GlobalColor = OrangePortrait;
            }
            catch (Exception e)
            {
                ModLog.ErrorOnce("upgradeButton.visual", e);
            }
        }
    }
}
