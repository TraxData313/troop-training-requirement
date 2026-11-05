using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TroopTrainingRequirement.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Items;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets.Encyclopedia;

namespace TroopTrainingRequirement.Patches
{
    /// <summary>
    /// The encyclopedia unit page's troop tree: units the MAIN party can't upgrade into right now (the training rule,
    /// with the player clan's tier bypass and the MCM settings) get an orange frame + portrait tint, and the reason lines
    /// at the bottom of their hover tooltip. Allowed units are untouched. No prefab change, no UIExtenderEx.
    ///
    /// VM side: a postfix on the <c>EncyclopediaUnitPageVM</c> constructor (= every time a unit page opens) walks the
    /// tree and checks every parent -> child edge with <see cref="Training.Check(PartyBase, CharacterObject, CharacterObject)"/>
    /// against <c>PartyBase.MainParty</c>'s real roster. The verdict is keyed by the node portrait's character code
    /// (what the widget binds as <c>ImageId</c>) and by the <c>CharacterObject</c> (for the tooltip).
    ///
    /// Widget side: <c>EncyclopediaUnitTreeNodeItemBrushWidget.OnLateUpdate</c> runs every frame for each node; a postfix
    /// looks up its portrait (<c>ButtonContainer</c> -> button -> <c>ElementImage</c>), and when its ImageId is on the
    /// list swaps the button's brush for ours (<c>module\GUI\Brushes\TroopTrainingRequirement.xml</c>) and tints the
    /// portrait. Done once per node and page (a generation counter), undone if the verdict changes.
    ///
    /// Tooltip: the tree's hover shows vanilla's CharacterObject tooltip (<c>TooltipRefresherCollection.RefreshCharacterTooltip</c>,
    /// shared by every screen). <c>EncyclopediaUnitVM.ExecuteBeginHint/EndHint</c> prefixes mark which blocked unit is
    /// hovered, and the refresher's postfix adds our lines only for that one.
    /// </summary>
    internal static class EncyclopediaTreePatch
    {
        private const string OrangeBrush = "TroopTrainingRequirement.Encyclopedia.ElementBlocked";
        private static readonly Color OrangePortrait = new Color(1f, 0.72f, 0.4f);
        private static readonly Color OrangeText = Color.ConvertStringToColor("#FFA028FF");

        private static readonly AccessTools.FieldRef<EncyclopediaUnitVM, CharacterObject> UnitCharacter =
            AccessTools.FieldRefAccess<EncyclopediaUnitVM, CharacterObject>("_character");

        /// <summary>Character code (portrait ImageId) -> blocked. Rebuilt on every unit page.</summary>
        private static readonly HashSet<string> BlockedIds = new HashSet<string>();

        /// <summary>Blocked unit -> its reason lines (the tooltip).</summary>
        private static readonly Dictionary<CharacterObject, IReadOnlyList<string>> BlockedReasons = new Dictionary<CharacterObject, IReadOnlyList<string>>();

        private static readonly ConditionalWeakTable<Widget, NodeState> States = new ConditionalWeakTable<Widget, NodeState>();

        private static int _generation;
        private static CharacterObject? _hovered;
        private static bool _brushMissingLogged;

        private sealed class NodeState
        {
            public int Generation = -1;
            public string? ImageId;
            public bool Orange;
            public Brush? VanillaBrush;
            public Color VanillaPortraitColor = Color.White;
        }

        public static void Apply(Harmony harmony)
        {
            var ctor = AccessTools.Constructor(typeof(EncyclopediaUnitPageVM), new[] { typeof(EncyclopediaPageArgs) })
                ?? throw new MissingMethodException("EncyclopediaUnitPageVM..ctor(EncyclopediaPageArgs)");
            harmony.Patch(ctor, postfix: new HarmonyMethod(typeof(EncyclopediaTreePatch), nameof(PagePostfix)));

            // The two looks are independent: either may fail to patch and the other still works.
            try
            {
                var late = AccessTools.Method(typeof(EncyclopediaUnitTreeNodeItemBrushWidget), "OnLateUpdate")
                    ?? throw new MissingMethodException("EncyclopediaUnitTreeNodeItemBrushWidget.OnLateUpdate");
                harmony.Patch(late, postfix: new HarmonyMethod(typeof(EncyclopediaTreePatch), nameof(NodeLateUpdatePostfix)));
            }
            catch (Exception e)
            {
                ModLog.Error("patches", "encyclopedia tree orange frame NOT applied", e);
            }

            try
            {
                var begin = AccessTools.Method(typeof(EncyclopediaUnitVM), nameof(EncyclopediaUnitVM.ExecuteBeginHint))
                    ?? throw new MissingMethodException("EncyclopediaUnitVM.ExecuteBeginHint");
                var end = AccessTools.Method(typeof(EncyclopediaUnitVM), nameof(EncyclopediaUnitVM.ExecuteEndHint))
                    ?? throw new MissingMethodException("EncyclopediaUnitVM.ExecuteEndHint");
                var refresher = AccessTools.Method(typeof(TooltipRefresherCollection), nameof(TooltipRefresherCollection.RefreshCharacterTooltip))
                    ?? throw new MissingMethodException("TooltipRefresherCollection.RefreshCharacterTooltip");
                harmony.Patch(begin, prefix: new HarmonyMethod(typeof(EncyclopediaTreePatch), nameof(BeginHintPrefix)));
                harmony.Patch(end, prefix: new HarmonyMethod(typeof(EncyclopediaTreePatch), nameof(EndHintPrefix)));
                harmony.Patch(refresher, postfix: new HarmonyMethod(typeof(EncyclopediaTreePatch), nameof(CharacterTooltipPostfix)));
            }
            catch (Exception e)
            {
                ModLog.Error("patches", "encyclopedia tree tooltip reasons NOT applied", e);
            }
        }

        // ---- VM: the verdicts, every time a unit page opens

        private static void PagePostfix(EncyclopediaUnitPageVM __instance)
        {
            try
            {
                BlockedIds.Clear();
                BlockedReasons.Clear();
                _hovered = null;
                _generation++;
                var tree = __instance.Tree;
                if (tree == null || !Training.Settings.AppliesTo(Training.Classify(PartyBase.MainParty))) return;
                Walk(tree, 0);
            }
            catch (Exception e)
            {
                ModLog.ErrorOnce("encyclopedia.page", e);
            }
        }

        private static void Walk(EncyclopediaTroopTreeNodeVM node, int depth)
        {
            if (depth > 16 || node.Branch == null || node.Unit == null) return; // trees are 4-6 deep; a guard against loops
            var troop = UnitCharacter(node.Unit);
            foreach (var child in node.Branch)
            {
                if (child?.Unit == null) continue;
                var target = UnitCharacter(child.Unit);
                if (troop != null && target != null)
                {
                    var check = Training.Check(PartyBase.MainParty, troop, target);
                    if (!check.Allowed)
                    {
                        var id = child.Unit.ImageIdentifier?.Id;
                        if (!string.IsNullOrEmpty(id)) BlockedIds.Add(id!);
                        BlockedReasons[target] = TrainingRule.ReasonLines(check);
                    }
                }
                Walk(child, depth + 1);
            }
        }

        // ---- widget: the orange frame and portrait

        private static void NodeLateUpdatePostfix(EncyclopediaUnitTreeNodeItemBrushWidget __instance)
        {
            try
            {
                var button = __instance.FindChild("ButtonContainer")?.GetChild(0) as ButtonWidget;
                if (button == null) return;
                var image = button.FindChild("ElementImage") as ImageIdentifierWidget;
                var id = image?.ImageId;
                if (image == null || string.IsNullOrEmpty(id)) return;

                var state = States.GetValue(__instance, _ => new NodeState());
                if (state.Generation == _generation && state.ImageId == id) return;
                state.Generation = _generation;
                state.ImageId = id;

                bool orange = BlockedIds.Contains(id!);
                if (orange == state.Orange) return;
                if (orange)
                {
                    var brush = button.Context?.GetBrush(OrangeBrush);
                    if (brush == null)
                    {
                        if (!_brushMissingLogged)
                        {
                            _brushMissingLogged = true;
                            ModLog.Info("encyclopedia", "orange tree brush not found (GUI\\Brushes\\TroopTrainingRequirement.xml missing?) - portrait tint only");
                        }
                    }
                    else
                    {
                        state.VanillaBrush = button.ReadOnlyBrush;
                        button.Brush = brush;
                    }
                    state.VanillaPortraitColor = image.ReadOnlyBrush.GlobalColor;
                    image.Brush.GlobalColor = OrangePortrait;
                }
                else
                {
                    if (state.VanillaBrush != null) button.Brush = state.VanillaBrush;
                    state.VanillaBrush = null;
                    image.Brush.GlobalColor = state.VanillaPortraitColor;
                }
                state.Orange = orange;
            }
            catch (Exception e)
            {
                ModLog.ErrorOnce("encyclopedia.node", e);
            }
        }

        // ---- tooltip: the reason lines for the hovered tree unit only

        private static void BeginHintPrefix(EncyclopediaUnitVM __instance)
        {
            try
            {
                var character = UnitCharacter(__instance);
                _hovered = character != null && BlockedReasons.ContainsKey(character) ? character : null;
            }
            catch (Exception e)
            {
                ModLog.ErrorOnce("encyclopedia.beginHint", e);
            }
        }

        private static void EndHintPrefix() => _hovered = null;

        private static void CharacterTooltipPostfix(PropertyBasedTooltipVM propertyBasedTooltipVM, object[] args)
        {
            try
            {
                if (_hovered == null || args == null || args.Length == 0 || !ReferenceEquals(args[0], _hovered)) return;
                if (!BlockedReasons.TryGetValue(_hovered, out var lines) || lines.Count == 0) return;
                propertyBasedTooltipVM.AddProperty("", "");
                foreach (var line in lines)
                    propertyBasedTooltipVM.AddColoredProperty("", line, OrangeText);
            }
            catch (Exception e)
            {
                ModLog.ErrorOnce("encyclopedia.tooltip", e);
            }
        }
    }
}
