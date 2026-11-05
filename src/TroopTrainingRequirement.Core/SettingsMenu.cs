using System;
using System.Collections.Generic;
using System.Linq;

namespace TroopTrainingRequirement.Core
{
    /// <summary>How a setting shows in the Mod Configuration Menu.</summary>
    public enum MenuKind
    {
        /// <summary>A checkbox.</summary>
        Bool,

        /// <summary>A whole-number slider.</summary>
        Int,
    }

    /// <summary>One group (section) of the menu page, in page order.</summary>
    public sealed class MenuGroup
    {
        internal MenuGroup(string name, int order)
        {
            Name = name;
            Order = order;
        }

        public string Name { get; }
        public int Order { get; }
    }

    /// <summary>
    /// One setting of the menu page and how it maps onto <see cref="TrainingSettings"/>: <see cref="Get"/> gives the
    /// value in menu units (0/1 for a bool), <see cref="Set"/> takes one, clamps it and writes the settings object.
    /// The module only wraps Get/Set in MCM's ProxyRefs (the sister mod's pattern).
    /// </summary>
    public sealed class MenuSetting
    {
        private readonly Func<TrainingSettings, double> _get;
        private readonly Action<TrainingSettings, double> _set;

        internal MenuSetting(string id, string label, MenuGroup group, string hint, MenuKind kind, double min, double max,
            Func<TrainingSettings, double> get, Action<TrainingSettings, double> set)
        {
            Id = id;
            Label = label;
            Group = group;
            Hint = hint;
            Kind = kind;
            Min = min;
            Max = max;
            _get = get;
            _set = set;
        }

        /// <summary>The MCM property id (= the <see cref="TrainingSettings"/> property name).</summary>
        public string Id { get; }

        public string Label { get; }
        public MenuGroup Group { get; }
        public string Hint { get; }
        public MenuKind Kind { get; }
        public double Min { get; }
        public double Max { get; }

        public double Get(TrainingSettings settings) => _get(settings);

        /// <summary>Writes a menu-unit value, clamped to the range (NaN = left as it is). True when it changed.</summary>
        public bool Set(TrainingSettings settings, double value)
        {
            if (double.IsNaN(value)) return false;
            if (Kind == MenuKind.Bool) value = value != 0 ? 1 : 0;
            else value = Math.Round(Math.Max(Min, Math.Min(Max, value)), MidpointRounding.AwayFromZero);
            double before = _get(settings);
            _set(settings, value);
            return Math.Abs(_get(settings) - before) > 1e-6;
        }
    }

    /// <summary>
    /// Every setting as the in-game Mod Configuration Menu page shows it: groups, order, labels, hints and ranges.
    /// Game-free on purpose (unit-tested); the module builds the MCM page from this list.
    /// </summary>
    public static class SettingsMenu
    {
        public const string PageId = "TroopTrainingRequirement_v1";
        public const string PageName = ModInfo.DisplayName;
        public const string FolderName = "TroopTrainingRequirement";

        public static readonly MenuGroup General = new MenuGroup("General", 0);
        public static readonly MenuGroup Skills = new MenuGroup("Skills that count", 1);
        public static readonly MenuGroup Who = new MenuGroup("Whose parties", 2);
        public static readonly MenuGroup Trainers = new MenuGroup("Trainers", 3);

        public static IReadOnlyList<MenuGroup> Groups { get; } = new[] { General, Skills, Who, Trainers };

        public static IReadOnlyList<MenuSetting> All { get; } = Build();

        private static List<MenuSetting> Build()
        {
            var list = new List<MenuSetting>
            {
                Flag(General, nameof(TrainingSettings.Enabled), "Enabled",
                    "Troops can only be upgraded into troops whose combat skills someone in the party can teach. Off = vanilla upgrades. Recruiting is never affected. Default on.",
                    s => s.Enabled, (s, v) => s.Enabled = v),
                Flag(General, nameof(TrainingSettings.ClanTierUnlocks), "Clan tier unlocks troops up to that tier",
                    "A target troop whose tier is at most the party owner's clan tier can always be upgraded to, whatever the trainers' skills (clan tier 4 = any tier 4 troop). Default on.",
                    s => s.ClanTierUnlocks, (s, v) => s.ClanTierUnlocks = v),
                new MenuSetting(nameof(TrainingSettings.Allowance), "Allowance", General,
                    "The target troop may exceed the best trainer by this many points in each skill. Default 0.",
                    MenuKind.Int, 0, 100, s => s.Allowance, (s, v) => s.Allowance = (int)v),
            };

            for (int i = 0; i < CombatSkills.Count; i++)
            {
                var skill = (CombatSkill)i;
                string name = CombatSkills.DisplayName(skill);
                list.Add(Flag(Skills, "Count" + skill, name,
                    "Check " + name + ": the target troop's " + name + " must not exceed the best trainer's. Default on.",
                    s => s.Counts(skill), (s, v) => s.SetCounts(skill, v)));
            }

            list.Add(Flag(Who, nameof(TrainingSettings.ApplyToMainParty), "Your party",
                "The rule applies to your own party (party screen upgrades). Default on.",
                s => s.ApplyToMainParty, (s, v) => s.ApplyToMainParty = v));
            list.Add(Flag(Who, nameof(TrainingSettings.ApplyToClanParties), "Your clan's parties",
                "The rule applies to parties led by companions and family of your clan (their automatic upgrades); each uses its own heroes as trainers. Caravans stay vanilla. Default on.",
                s => s.ApplyToClanParties, (s, v) => s.ApplyToClanParties = v));
            list.Add(Flag(Who, nameof(TrainingSettings.ApplyToAiParties), "AI lords' parties",
                "The rule also applies to every other hero-led party (their automatic upgrades). Garrisons, militia, villagers, caravans and bandits always stay vanilla. Default off.",
                s => s.ApplyToAiParties, (s, v) => s.ApplyToAiParties = v));

            list.Add(Flag(Trainers, nameof(TrainingSettings.CountWholeClan), "Count the whole clan",
                "Off: trainers are the heroes in the party. On: every adult hero of the party's clan who is not a prisoner, wherever they are. Default off.",
                s => s.CountWholeClan, (s, v) => s.CountWholeClan = v));
            return list;
        }

        /// <summary>The settings of one group, in menu order.</summary>
        public static IEnumerable<MenuSetting> InGroup(MenuGroup group) => All.Where(s => s.Group == group);

        /// <summary>The setting with this id, or null.</summary>
        public static MenuSetting? Find(string id) => All.FirstOrDefault(s => s.Id == id);

        private static MenuSetting Flag(MenuGroup g, string id, string label, string hint,
            Func<TrainingSettings, bool> get, Action<TrainingSettings, bool> set) =>
            new MenuSetting(id, label, g, hint, MenuKind.Bool, 0, 1, s => get(s) ? 1 : 0, (s, v) => set(s, v != 0));
    }
}
