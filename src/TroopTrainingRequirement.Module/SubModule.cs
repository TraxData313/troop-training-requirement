using TroopTrainingRequirement.Core;
using TaleWorlds.MountAndBlade;

namespace TroopTrainingRequirement
{
    /// <summary>
    /// Step 1 scaffold: logs one line at load. The PartyTroopUpgradeModel decorator (registered in OnGameStart),
    /// the MCM page and the UI patches (applied at the first campaign start, never at module load) come in later steps.
    /// </summary>
    public class SubModule : MBSubModuleBase
    {
        internal const string HarmonyId = "trax.bannerlord.trooptrainingrequirement";

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            ModLog.Info("load", ModInfo.DisplayName + " loaded (" + typeof(SubModule).Assembly.GetName().Version + ")");
        }
    }
}
