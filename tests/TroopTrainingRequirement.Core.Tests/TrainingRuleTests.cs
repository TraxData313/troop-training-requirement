using TroopTrainingRequirement.Core;

namespace TroopTrainingRequirement.Core.Tests;

public class TrainingRuleTests
{
    // OneHanded, TwoHanded, Polearm, Bow, Crossbow, Throwing, Riding, Athletics
    private static int[] Skills(int oh = 0, int th = 0, int pole = 0, int bow = 0, int xbow = 0, int thr = 0, int ride = 0, int ath = 0)
        => new[] { oh, th, pole, bow, xbow, thr, ride, ath };

    private static readonly TrainingSettings Defaults = new();

    // Most tests check skills only: clan tier -1 = no clan, the bypass never applies.
    private static TrainingCheck Check(int[] target, TrainingSettings? s = null, params Trainer[] trainers)
        => TrainingRule.Check(target, 4, trainers, -1, s ?? Defaults);

    [Fact]
    public void AllowedWhenBestTrainerMeetsEverySkill()
    {
        var c = Check(Skills(oh: 100, ath: 80),
            null,
            new Trainer("Player", Skills(oh: 100, ath: 20)),
            new Trainer("Cadan", Skills(ath: 80)));
        Assert.True(c.Allowed);
        Assert.Empty(c.Shortfalls);
        Assert.Equal("", TrainingRule.ReasonText(c));
    }

    [Fact]
    public void BlockedListsBestTrainerPerSkill()
    {
        var c = Check(Skills(ath: 120), null,
            new Trainer("Player", Skills(ath: 60)),
            new Trainer("Cadan", Skills(ath: 110)));
        Assert.False(c.Allowed);
        var s = Assert.Single(c.Shortfalls);
        Assert.Equal(CombatSkill.Athletics, s.Skill);
        Assert.Equal(120, s.Required);
        Assert.Equal(110, s.Best);
        Assert.Equal("Cadan", s.BestTrainer);
        Assert.Equal("Requires Athletics 120 - your best is 110 (Cadan)", TrainingRule.ReasonText(c));
    }

    [Fact]
    public void SeveralFailuresGiveOneLineEachInSkillOrder()
    {
        var c = Check(Skills(oh: 90, ride: 100, ath: 50), null, new Trainer("Player", Skills(oh: 40, ride: 10, ath: 50)));
        Assert.Equal(new[]
        {
            "Requires One Handed 90 - your best is 40 (Player)",
            "Requires Riding 100 - your best is 10 (Player)",
        }, TrainingRule.ReasonLines(c));
    }

    [Fact]
    public void NoTrainersSaysNobodyCanTeach()
    {
        var c = Check(Skills(bow: 30));
        Assert.Equal("Requires Bow 30 - no one in the party can teach it", TrainingRule.ReasonText(c));
    }

    [Fact]
    public void ZeroSkillTargetNeedsNobody()
    {
        Assert.True(Check(Skills()).Allowed);
    }

    [Fact]
    public void AllowanceLowersTheRequirement()
    {
        var s = new TrainingSettings { Allowance = 10 };
        var t = new Trainer("Player", Skills(pole: 70));
        Assert.True(Check(Skills(pole: 80), s, t).Allowed);
        var c = Check(Skills(pole: 81), s, t);
        Assert.False(c.Allowed);
        Assert.Equal(71, c.Shortfalls[0].Required);
        Assert.Equal(81, c.Shortfalls[0].TroopSkill);
    }

    [Fact]
    public void DisabledSkillIsIgnored()
    {
        var s = new TrainingSettings { CountRiding = false };
        Assert.True(Check(Skills(ride: 200), s).Allowed);
        Assert.False(Check(Skills(ride: 200)).Allowed);
    }

    [Fact]
    public void DisabledModAllowsEverything()
    {
        Assert.True(Check(Skills(oh: 300), new TrainingSettings { Enabled = false }).Allowed);
    }

    [Fact]
    public void ClanTierUnlocksTargetsUpToThatTier()
    {
        var target = Skills(ath: 200);
        var c = TrainingRule.Check(target, 4, Array.Empty<Trainer>(), 4, Defaults);
        Assert.True(c.Allowed);
        Assert.True(c.UnlockedByClanTier);
        Assert.Equal("", TrainingRule.ReasonText(c));

        var c5 = TrainingRule.Check(target, 5, Array.Empty<Trainer>(), 4, Defaults);
        Assert.False(c5.Allowed);
        Assert.Equal(5, c5.ClanTierNeeded);
        Assert.Equal(new[]
        {
            "Requires Athletics 200 - no one in the party can teach it",
            "or reach clan tier 5",
        }, TrainingRule.ReasonLines(c5));
    }

    [Fact]
    public void ClanTierBypassCanBeTurnedOff()
    {
        var s = new TrainingSettings { ClanTierUnlocks = false };
        var c = TrainingRule.Check(Skills(ath: 50), 2, Array.Empty<Trainer>(), 6, s);
        Assert.False(c.Allowed);
        Assert.Null(c.ClanTierNeeded);
        Assert.DoesNotContain("clan tier", TrainingRule.ReasonText(c));
    }

    [Fact]
    public void NoClanTierLineAboveTheHighestClanTier()
    {
        var c = TrainingRule.Check(Skills(ath: 50), 7, Array.Empty<Trainer>(), 6, Defaults);
        Assert.False(c.Allowed);
        Assert.Null(c.ClanTierNeeded);
    }

    [Fact]
    public void SkillPassStillAllowedAboveClanTier()
    {
        var c = TrainingRule.Check(Skills(oh: 50), 6, new[] { new Trainer("Player", Skills(oh: 50)) }, 1, Defaults);
        Assert.True(c.Allowed);
        Assert.False(c.UnlockedByClanTier);
    }

    [Theory]
    [InlineData(PartyKind.MainParty, true)]
    [InlineData(PartyKind.ClanParty, true)]
    [InlineData(PartyKind.AiParty, false)]
    [InlineData(PartyKind.Other, false)]
    public void DefaultScope(PartyKind kind, bool applies)
    {
        Assert.Equal(applies, Defaults.AppliesTo(kind));
    }

    [Theory]
    //          main   mobile hero   player caravan bandit -> kind
    [InlineData(true,  true,  true,  true,  false,  false, PartyKind.MainParty)]
    [InlineData(false, true,  true,  true,  false,  false, PartyKind.ClanParty)]
    [InlineData(false, true,  true,  false, false,  false, PartyKind.AiParty)]
    [InlineData(false, true,  false, false, false,  false, PartyKind.Other)]  // garrison, militia, villagers, patrols
    [InlineData(false, false, true,  true,  false,  false, PartyKind.Other)]  // a settlement's party
    [InlineData(false, true,  true,  true,  true,   false, PartyKind.Other)]  // the player clan's companion-led caravan
    [InlineData(false, true,  true,  false, true,   false, PartyKind.Other)]  // an AI clan's caravan
    [InlineData(false, true,  true,  false, false,  true,  PartyKind.Other)]  // a hero-led bandit party
    public void PartyKindsCoverOnlyHeroLedNonCaravanNonBanditParties(bool main, bool mobile, bool hero, bool player,
        bool caravan, bool bandit, PartyKind expected)
    {
        Assert.Equal(expected, PartyKinds.Classify(main, mobile, hero, player, caravan, bandit));
    }

    [Fact]
    public void ScopeFollowsSettingsAndMasterSwitch()
    {
        Assert.True(new TrainingSettings { ApplyToAiParties = true }.AppliesTo(PartyKind.AiParty));
        Assert.False(new TrainingSettings { ApplyToClanParties = false }.AppliesTo(PartyKind.ClanParty));
        Assert.False(new TrainingSettings { Enabled = false }.AppliesTo(PartyKind.MainParty));
    }
}

public class SettingsMenuTests
{
    [Fact]
    public void EverySettingRoundTripsAndIdsAreUnique()
    {
        Assert.Equal(SettingsMenu.All.Count, SettingsMenu.All.Select(s => s.Id).Distinct().Count());
        foreach (var m in SettingsMenu.All)
        {
            Assert.NotNull(typeof(TrainingSettings).GetProperty(m.Id));
            var s = new TrainingSettings();
            double other = m.Kind == MenuKind.Bool ? 1 - m.Get(s) : m.Max;
            Assert.True(m.Set(s, other), m.Id);
            Assert.Equal(other, m.Get(s));
        }
    }

    [Fact]
    public void AllEightSkillsAndTheClanTierToggleAreOnTheMenu()
    {
        Assert.Equal(8, SettingsMenu.InGroup(SettingsMenu.Skills).Count());
        Assert.Equal(SettingsMenu.General, SettingsMenu.Find(nameof(TrainingSettings.ClanTierUnlocks))!.Group);
    }

    [Fact]
    public void AllowanceIsClamped()
    {
        var s = new TrainingSettings();
        SettingsMenu.Find(nameof(TrainingSettings.Allowance))!.Set(s, 500);
        Assert.Equal(100, s.Allowance);
    }
}
