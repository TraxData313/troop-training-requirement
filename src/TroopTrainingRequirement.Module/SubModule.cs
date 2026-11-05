using System;
using System.Linq;
using HarmonyLib;
using TroopTrainingRequirement.Core;
using TroopTrainingRequirement.Models;
using TroopTrainingRequirement.Patches;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TroopTrainingRequirement
{
    /// <summary>
    /// Registers the PartyTroopUpgradeModel decorator (every campaign start) and the two enforcement patches (once per
    /// process, at the first campaign start, never at module load), plus the optional MCM page.
    /// </summary>
    public class SubModule : MBSubModuleBase
    {
        internal const string HarmonyId = "trax.bannerlord.trooptrainingrequirement";

        private static bool _patched;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            ModLog.Info("load", ModInfo.DisplayName + " loaded (" + typeof(SubModule).Assembly.GetName().Version + ")");
        }

        // MCM's own module registers its services in OnSubModuleLoad; the main menu is the first safe point for a page.
        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            McmSettings.TryRegister("main menu");
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            McmSettings.TryRegister("game start"); // no-op once registered (normally at the main menu)
            Training.Reset();
            try
            {
                if (!(game?.GameType is Campaign)) return;
                ModLog.Info("load", "campaign start: " + game.GameType.GetType().Name + "; settings " + McmSettings.Describe(Training.Settings));
                RegisterModels(gameStarterObject);
                ApplyCampaignPatches();
            }
            catch (Exception e)
            {
                ModLog.Error("load", "OnGameStart", e);
            }
        }

        public override void OnGameEnd(Game game)
        {
            base.OnGameEnd(game);
            Training.Reset();
        }

        private static void ApplyCampaignPatches()
        {
            if (_patched) return;
            _patched = true;
            Patch("PartyScreenLogic.ValidateCommand postfix (party screen upgrades refused when the party can't train the target)", h => PartyScreenGuardPatch.Apply(h));
            Patch("PartyUpgraderCampaignBehavior.GetPossibleUpgradeTargets postfix (clan / AI automatic upgrades)", h => AutoUpgradePatch.Apply(h));
        }

        private static void Patch(string what, Action<Harmony> apply)
        {
            try
            {
                apply(new Harmony(HarmonyId));
                ModLog.Info("patches", "applied: " + what);
            }
            catch (Exception e)
            {
                ModLog.Error("patches", "NOT applied (carrying on without it): " + what, e);
            }
        }

        /// <summary>
        /// The decorator (trax_combat_enhancements' RegisterModels pattern): only when the game type already has the
        /// model, so BaseModel is never null. AddModel hands us the previously registered model as BaseModel.
        /// </summary>
        private static void RegisterModels(IGameStarter starter)
        {
            try
            {
                if (starter.Models.Any(m => m is PartyTroopUpgradeModel))
                {
                    var model = new TrainingUpgradeModel();
                    starter.AddModel<PartyTroopUpgradeModel>(model);
                    ModLog.Info("models", "upgrade decorator registered over " + model.BaseModelName);
                }
                else
                {
                    ModLog.Info("models", "this game type has no PartyTroopUpgradeModel - decorator not registered");
                }
            }
            catch (Exception e)
            {
                ModLog.Error("models", "upgrade decorator registration", e);
            }
        }
    }
}
