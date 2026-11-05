using TroopTrainingRequirement.Core;

namespace TroopTrainingRequirement.Core.Tests;

public class UpgradeButtonLookTests
{
    [Theory]
    [InlineData(true, true, false, UpgradeLook.BlockedByTraining)]
    [InlineData(false, true, false, UpgradeLook.Vanilla)] // not ours
    [InlineData(true, false, false, UpgradeLook.Vanilla)] // not enough XP: vanilla grey
    [InlineData(true, true, true, UpgradeLook.Vanilla)]   // gold / horse / perk: vanilla red
    [InlineData(true, false, true, UpgradeLook.Vanilla)]
    public void OrangeOnlyWhenOnlyTheRuleBlocks(bool blocked, bool available, bool insufficient, UpgradeLook expected)
        => Assert.Equal(expected, UpgradeButtonLook.Look(blocked, available, insufficient));

    [Fact]
    public void HintAppendsReasonLinesUnderVanilla()
        => Assert.Equal("Upgrade to Imperial Legionary\nCost: 100\nRequires Athletics 120 - your best is 110 (Cadan)\nor reach clan tier 5",
            UpgradeButtonLook.Hint("Upgrade to Imperial Legionary\nCost: 100",
                new[] { "Requires Athletics 120 - your best is 110 (Cadan)", "or reach clan tier 5" }));

    [Fact]
    public void HintWithoutVanillaIsJustTheReasons()
    {
        Assert.Equal("a\nb", UpgradeButtonLook.Hint(null, new[] { "a", "b" }));
        Assert.Equal("a", UpgradeButtonLook.Hint("", new[] { "a" }));
    }

    [Fact]
    public void HintWithoutReasonsIsVanilla()
    {
        Assert.Equal("v", UpgradeButtonLook.Hint("v", System.Array.Empty<string>()));
        Assert.Equal("v", UpgradeButtonLook.Hint("v", null));
        Assert.Equal("", UpgradeButtonLook.Hint(null, null));
    }

    [Fact]
    public void HintMatchesTheRuleReasonLines()
    {
        var s = new TrainingSettings();
        var c = TrainingRule.Check(new[] { 0, 0, 0, 0, 0, 0, 0, 120 }, 5,
            new[] { new Trainer("Cadan", new[] { 0, 0, 0, 0, 0, 0, 0, 110 }) }, 2, s);
        Assert.Equal("Upgrade to X\nRequires Athletics 120 - your best is 110 (Cadan)\nor reach clan tier 5",
            UpgradeButtonLook.Hint("Upgrade to X", TrainingRule.ReasonLines(c)));
    }
}
