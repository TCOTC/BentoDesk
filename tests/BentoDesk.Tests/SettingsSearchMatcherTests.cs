using BentoDesk.Services;

namespace BentoDesk.Tests;

public sealed class SettingsSearchMatcherTests
{
    [Fact]
    public void ExactTitleRanksAheadOfOtherMatches()
    {
        int exact = SettingsSearchMatcher.GetScore("透明度", "透明度", "外观 / 窗口材质", "调整背景透明程度");
        int prefix = SettingsSearchMatcher.GetScore("透明", "透明度", "外观 / 窗口材质", "调整背景透明程度");
        int breadcrumb = SettingsSearchMatcher.GetScore("外观", "透明度", "外观 / 窗口材质", "调整背景透明程度");

        Assert.True(exact < prefix);
        Assert.True(prefix < breadcrumb);
    }

    [Fact]
    public void DescriptionProvidesFallbackSearchText()
    {
        int score = SettingsSearchMatcher.GetScore(
            "封面悬停",
            "封面悬停动效",
            "功能盒子 / 音乐",
            "控制封面悬停时的动效");

        Assert.NotEqual(SettingsSearchMatcher.NoMatch, score);
    }

    [Fact]
    public void MissingTermDoesNotMatch()
    {
        int score = SettingsSearchMatcher.GetScore(
            "音乐 透明度",
            "封面悬停动效",
            "功能盒子 / 音乐",
            "控制封面悬停时的动效");

        Assert.Equal(SettingsSearchMatcher.NoMatch, score);
    }
}
