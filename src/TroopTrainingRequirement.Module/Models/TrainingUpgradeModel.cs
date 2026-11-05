using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace TroopTrainingRequirement.Models
{
    /// <summary>
    /// The DECORATOR over whatever PartyTroopUpgradeModel was registered before (vanilla's, War Sails', another mod's).
    /// Every member forwards to BaseModel; the one addition is the training rule in
    /// <see cref="CanPartyUpgradeTroopToTarget"/>.
    ///
    /// Vanilla only asks this method for bandit troops (PartyCharacterVM.InitializeUpgrades,
    /// PartyUpgraderCampaignBehavior.GetPossibleUpgradeTargets), so the rule is ALSO enforced by two patches:
    /// PartyScreenGuardPatch (the party screen's ValidateCommand) and AutoUpgradePatch (clan / AI daily upgrades).
    /// IsTroopUpgradeable is left alone: the party screen must still show the upgrade buttons (step 3 disables
    /// them with the reason).
    /// </summary>
    public sealed class TrainingUpgradeModel : PartyTroopUpgradeModel
    {
        internal string BaseModelName => BaseModel?.GetType().FullName ?? "(none)";

        public override bool CanPartyUpgradeTroopToTarget(PartyBase party, CharacterObject character, CharacterObject target)
        {
            bool vanilla = BaseModel.CanPartyUpgradeTroopToTarget(party, character, target);
            if (!vanilla) return false;
            try
            {
                return Training.Check(party, character, target).Allowed;
            }
            catch (Exception e)
            {
                ModLog.ErrorOnce("model.canUpgrade", e);
                return vanilla;
            }
        }

        // ------------------------------------------------------------------ forwarded (every abstract member)

        public override bool IsTroopUpgradeable(PartyBase party, CharacterObject character)
            => BaseModel.IsTroopUpgradeable(party, character);

        public override bool DoesPartyHaveRequiredItemsForUpgrade(PartyBase party, CharacterObject upgradeTarget)
            => BaseModel.DoesPartyHaveRequiredItemsForUpgrade(party, upgradeTarget);

        public override bool DoesPartyHaveRequiredPerksForUpgrade(PartyBase party, CharacterObject character, CharacterObject upgradeTarget, out PerkObject requiredPerk)
            => BaseModel.DoesPartyHaveRequiredPerksForUpgrade(party, character, upgradeTarget, out requiredPerk);

        public override ExplainedNumber GetGoldCostForUpgrade(PartyBase party, CharacterObject characterObject, CharacterObject upgradeTarget)
            => BaseModel.GetGoldCostForUpgrade(party, characterObject, upgradeTarget);

        public override int GetXpCostForUpgrade(PartyBase party, CharacterObject characterObject, CharacterObject upgradeTarget)
            => BaseModel.GetXpCostForUpgrade(party, characterObject, upgradeTarget);

        public override int GetSkillXpFromUpgradingTroops(PartyBase party, CharacterObject troop, int numberOfTroops)
            => BaseModel.GetSkillXpFromUpgradingTroops(party, troop, numberOfTroops);

        public override float GetUpgradeChanceForTroopUpgrade(PartyBase party, CharacterObject troop, int upgradeTargetIndex)
            => BaseModel.GetUpgradeChanceForTroopUpgrade(party, troop, upgradeTargetIndex);
    }
}
