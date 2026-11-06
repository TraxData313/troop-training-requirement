using TroopTrainingRequirement.Core;

namespace TroopTrainingRequirement.Core.Tests;

public class CultureBonusTests
{
    // OneHanded, TwoHanded, Polearm, Bow, Crossbow, Throwing, Riding, Athletics
    private static int[] Skills(int oh = 0, int th = 0, int pole = 0, int bow = 0, int xbow = 0, int thr = 0, int ride = 0, int ath = 0)
        => new[] { oh, th, pole, bow, xbow, thr, ride, ath };

    private static readonly TrainingSettings Defaults = new();

    // Fian Champion (SandBoxCore spnpccharacters.xml, battanian_fian_champion, tier 6): the skills that matter here.
    private static readonly int[] FianChampion = Skills(oh: 90, th: 220, pole: 50, bow: 220, xbow: 25, thr: 40, ride: 60, ath: 170);

    private static TrainingSettings BowOnly(TrainingSettings? s = null)
    {
        s ??= new TrainingSettings();
        foreach (CombatSkill k in Enum.GetValues(typeof(CombatSkill))) s.SetCounts(k, k == CombatSkill.Bow);
        return s;
    }

    private static TrainingCheck Check(int[] target, string? culture, TrainingSettings s, params Trainer[] trainers)
        => TrainingRule.Check(target, 6, trainers, -1, s, culture, culture == "battania" ? "Battania" : null);

    [Fact]
    public void DefaultsAreOnPlus5Times1Point2()
    {
        Assert.True(Defaults.SameCultureBonusEnabled);
        Assert.Equal(5, Defaults.SameCultureBonusPoints);
        Assert.Equal(1.2f, Defaults.SameCultureBonusMultiplier);
    }

    [Theory]
    [InlineData("battania", "battania", true)]
    [InlineData("battania", "vlandia", false)]
    [InlineData("neutral_culture", "neutral_culture", false)]
    [InlineData(null, "battania", false)]
    [InlineData("battania", null, false)]
    public void OnlyARealSharedCultureMatches(string? troop, string? trainer, bool expected) =>
        Assert.Equal(expected, CultureBonus.Matches(troop, trainer));

    [Fact]
    public void FianChampionThresholdIsBow180()
    {
        // (220 - 5) / 1.2 = 179.17 -> 180; 179 x 1.2 + 5 = 219.8 is still short.
        Assert.Equal(180, CultureBonus.MinimumSkill(220, 5, 1.2f));
        var s = BowOnly();
        Assert.True(Check(FianChampion, "battania", s, new Trainer("Cadan", Skills(bow: 180), "battania")).Allowed);
        var c = Check(FianChampion, "battania", s, new Trainer("Cadan", Skills(bow: 179), "battania"));
        Assert.False(c.Allowed);
        var f = Assert.Single(c.Shortfalls);
        Assert.Equal(219, f.Best);   // rounded DOWN: never "your best is 220" next to "Requires 220"
        Assert.Equal(179, f.BestRaw);
        Assert.Equal(40, f.Bonus);
        Assert.Equal("Requires Bow 220 - your best is 219 (Cadan, +40 Battanian)", TrainingRule.ReasonText(c));
    }

    [Fact]
    public void OtherCultureTrainerGetsNothing()
    {
        var c = Check(FianChampion, "battania", BowOnly(), new Trainer("Bryn", Skills(bow: 210), "vlandia"));
        Assert.False(c.Allowed);
        Assert.Equal("Requires Bow 220 - your best is 210 (Bryn)", TrainingRule.ReasonText(c));
    }

    [Fact]
    public void BestTrainerIsTheBestAfterTheBonus()
    {
        // Vlandian 200 raw vs Battanian 178 raw (178 x 1.2 + 5 = 218.6): the Battanian is the better teacher.
        var c = Check(FianChampion, "battania", BowOnly(),
            new Trainer("Bryn", Skills(bow: 200), "vlandia"),
            new Trainer("Cadan", Skills(bow: 178), "battania"));
        var f = Assert.Single(c.Shortfalls);
        Assert.Equal("Cadan", f.BestTrainer);
        Assert.Equal(218, f.Best);
        Assert.Equal(40, f.Bonus);
    }

    [Fact]
    public void ToggleOffOrNoTargetCultureMeansRawSkills()
    {
        var off = BowOnly(new TrainingSettings { SameCultureBonusEnabled = false });
        Assert.False(Check(FianChampion, "battania", off, new Trainer("Cadan", Skills(bow: 200), "battania")).Allowed);
        Assert.False(Check(FianChampion, null, BowOnly(), new Trainer("Cadan", Skills(bow: 200), "battania")).Allowed);
        Assert.True(Check(FianChampion, "battania", BowOnly(), new Trainer("Cadan", Skills(bow: 200), "battania")).Allowed);
    }

    [Fact]
    public void AllowanceStillComesOffTheTroopSide()
    {
        // Allowance 10: required 210; 171 x 1.2 + 5 = 210.2 meets it, 170 -> 209 does not.
        var s = BowOnly(new TrainingSettings { Allowance = 10 });
        Assert.True(Check(FianChampion, "battania", s, new Trainer("Cadan", Skills(bow: 171), "battania")).Allowed);
        Assert.False(Check(FianChampion, "battania", s, new Trainer("Cadan", Skills(bow: 170), "battania")).Allowed);
    }

    [Fact]
    public void ClanTierBypassUnchanged()
    {
        var c = TrainingRule.Check(FianChampion, 6, Array.Empty<Trainer>(), 6, Defaults, "battania", "Battania");
        Assert.True(c.Allowed);
        Assert.True(c.UnlockedByClanTier);
    }

    [Fact]
    public void EveryCountedSkillGetsTheBonus()
    {
        // All 8 skills on: Fian Champion also needs Two Handed 220 and Athletics 170 (-> raw 138: 137 gives 169.4).
        Assert.Equal(138, CultureBonus.MinimumSkill(170, 5, 1.2f));
        Trainer Cadan(int ath) => new("Cadan", Skills(oh: 90, th: 180, pole: 50, bow: 180, xbow: 25, thr: 40, ride: 60, ath: ath), "battania");
        Assert.True(TrainingRule.Check(FianChampion, 6, new[] { Cadan(138) }, -1, Defaults, "battania", "Battania").Allowed);
        var c = TrainingRule.Check(FianChampion, 6, new[] { Cadan(137) }, -1, Defaults, "battania", "Battania");
        Assert.Equal(CombatSkill.Athletics, Assert.Single(c.Shortfalls).Skill);
    }

    [Fact]
    public void MultiplierSnapsTo005AndClamps()
    {
        var s = new TrainingSettings();
        var mult = SettingsMenu.Find(nameof(TrainingSettings.SameCultureBonusMultiplier))!;
        Assert.Equal(MenuKind.Float, mult.Kind);
        mult.Set(s, 1.33);
        Assert.Equal(1.35f, s.SameCultureBonusMultiplier, 4);
        mult.Set(s, 9);
        Assert.Equal(2f, s.SameCultureBonusMultiplier, 4);
        var add = SettingsMenu.Find(nameof(TrainingSettings.SameCultureBonusPoints))!;
        add.Set(s, 70);
        Assert.Equal(50, s.SameCultureBonusPoints);
        Assert.Equal(SettingsMenu.Culture, add.Group);
    }

    [Fact]
    public void FloatMultiplierDoesNotLeaveAHairUnder() =>
        Assert.Equal(230.0, CultureBonus.Effective(200, 0, 1.15f), 6);
}
