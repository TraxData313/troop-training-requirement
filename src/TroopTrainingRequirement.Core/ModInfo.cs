namespace TroopTrainingRequirement.Core
{
    /// <summary>Names shared by the module and the tests. The trainer rule joins this project in step 2.</summary>
    public static class ModInfo
    {
        /// <summary>The release module Id (tools\deploy.ps1 installs it as <c>TroopTrainingRequirement.Dev</c>).</summary>
        public const string ModuleId = "TroopTrainingRequirement";

        public const string DisplayName = "Troop Training Requirement";
    }
}
