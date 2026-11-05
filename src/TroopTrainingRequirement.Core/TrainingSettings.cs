namespace TroopTrainingRequirement.Core
{
    /// <summary>The 8 combat skills the rule can look at, in the order the reason lines list them.</summary>
    public enum CombatSkill
    {
        OneHanded,
        TwoHanded,
        Polearm,
        Bow,
        Crossbow,
        Throwing,
        Riding,
        Athletics,
    }

    public static class CombatSkills
    {
        public const int Count = 8;

        /// <summary>The skill's name as the game shows it.</summary>
        public static string DisplayName(CombatSkill skill) => skill switch
        {
            CombatSkill.OneHanded => "One Handed",
            CombatSkill.TwoHanded => "Two Handed",
            CombatSkill.Polearm => "Polearm",
            CombatSkill.Bow => "Bow",
            CombatSkill.Crossbow => "Crossbow",
            CombatSkill.Throwing => "Throwing",
            CombatSkill.Riding => "Riding",
            CombatSkill.Athletics => "Athletics",
            _ => skill.ToString(),
        };
    }

    /// <summary>
    /// Every setting of the mod. Plain properties on purpose: <see cref="SettingsMenu"/> binds them to MCM one by one
    /// (a new setting = a property here + a line in <see cref="SettingsMenu.All"/>). Nothing here is saved into the
    /// campaign; MCM keeps its own json.
    /// </summary>
    public sealed class TrainingSettings
    {
        // ---- General ----

        /// <summary>Master switch: off = vanilla upgrades everywhere.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>A target troop whose tier is at most the party owner's clan tier may always be upgraded to,
        /// whatever the trainers' skills (Toni, 2026-11-05: "I'm clan tier 4, I can upgrade to tier 4 troops").</summary>
        public bool ClanTierUnlocks { get; set; } = true;

        /// <summary>The target troop may exceed the best trainer by this many points in each skill.</summary>
        public int Allowance { get; set; }

        // ---- Skills that count (all on by default) ----

        public bool CountOneHanded { get; set; } = true;
        public bool CountTwoHanded { get; set; } = true;
        public bool CountPolearm { get; set; } = true;
        public bool CountBow { get; set; } = true;
        public bool CountCrossbow { get; set; } = true;
        public bool CountThrowing { get; set; } = true;
        public bool CountRiding { get; set; } = true;
        public bool CountAthletics { get; set; } = true;

        // ---- Whose parties ----

        /// <summary>The player's own party.</summary>
        public bool ApplyToMainParty { get; set; } = true;

        /// <summary>Hero-led parties of the player's clan (companions, family); they use their own heroes as trainers.</summary>
        public bool ApplyToClanParties { get; set; } = true;

        /// <summary>Hero-led parties of every other clan (AI lords).</summary>
        public bool ApplyToAiParties { get; set; }

        // ---- Trainers ----

        /// <summary>Off: trainers are the heroes in the party. On: every adult, free hero of the party's clan.</summary>
        public bool CountWholeClan { get; set; }

        public bool Counts(CombatSkill skill) => skill switch
        {
            CombatSkill.OneHanded => CountOneHanded,
            CombatSkill.TwoHanded => CountTwoHanded,
            CombatSkill.Polearm => CountPolearm,
            CombatSkill.Bow => CountBow,
            CombatSkill.Crossbow => CountCrossbow,
            CombatSkill.Throwing => CountThrowing,
            CombatSkill.Riding => CountRiding,
            CombatSkill.Athletics => CountAthletics,
            _ => false,
        };

        public void SetCounts(CombatSkill skill, bool value)
        {
            switch (skill)
            {
                case CombatSkill.OneHanded: CountOneHanded = value; break;
                case CombatSkill.TwoHanded: CountTwoHanded = value; break;
                case CombatSkill.Polearm: CountPolearm = value; break;
                case CombatSkill.Bow: CountBow = value; break;
                case CombatSkill.Crossbow: CountCrossbow = value; break;
                case CombatSkill.Throwing: CountThrowing = value; break;
                case CombatSkill.Riding: CountRiding = value; break;
                case CombatSkill.Athletics: CountAthletics = value; break;
            }
        }

        /// <summary>Whether the rule covers a party of this kind.</summary>
        public bool AppliesTo(PartyKind kind) => Enabled && kind switch
        {
            PartyKind.MainParty => ApplyToMainParty,
            PartyKind.ClanParty => ApplyToClanParties,
            PartyKind.AiParty => ApplyToAiParties,
            _ => false,
        };
    }

    /// <summary>Who a party belongs to, for the "whose parties" settings.</summary>
    public enum PartyKind
    {
        /// <summary>Not covered: garrisons, militia, villagers, bandits, patrols - anything without a leading hero.</summary>
        Other,

        /// <summary>The player's own party.</summary>
        MainParty,

        /// <summary>A hero-led party of the player's clan other than the main party.</summary>
        ClanParty,

        /// <summary>A hero-led party of any other clan.</summary>
        AiParty,
    }
}
