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
            "刷新间隔",
            "刷新间隔",
            "功能格子 / 天气",
            "控制天气数据的刷新频率");

        Assert.NotEqual(SettingsSearchMatcher.NoMatch, score);
    }

    [Fact]
    public void MissingTermDoesNotMatch()
    {
        int score = SettingsSearchMatcher.GetScore(
            "天气 透明度",
            "刷新间隔",
            "功能格子 / 天气",
            "控制天气数据的刷新频率");

        Assert.Equal(SettingsSearchMatcher.NoMatch, score);
    }
}
