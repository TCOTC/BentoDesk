// Copyright (c) BentoDesk. All rights reserved.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace BentoDesk.Helpers;

/// <summary>
/// 标题就地重命名时，按当前文字宽度自适应输入框。
/// </summary>
internal static class WidgetTitleRenameEditorHelper
{
    // 默认比当前标题多预留约两个汉字宽度，减少贴边滚动导致的抖动
    private const string ExtraWidthSample = "字字";
    private const double MinWidth = 24;
    private const double DefaultMaxWidth = 300;

    public static void ApplyAutoWidth(TextBox editor, double maxWidth)
    {
        ArgumentNullException.ThrowIfNull(editor);

        string text = editor.Text ?? string.Empty;
        if (string.IsNullOrEmpty(text))
        {
            text = string.IsNullOrEmpty(editor.PlaceholderText) ? "字" : editor.PlaceholderText;
        }

        double textWidth = MeasureWidth(text, editor);
        double extraWidth = MeasureWidth(ExtraWidthSample, editor);
        double resolvedMax = maxWidth > 0 && !double.IsInfinity(maxWidth)
            ? maxWidth
            : DefaultMaxWidth;
        double targetWidth = Math.Clamp(
            Math.Ceiling(textWidth + extraWidth),
            MinWidth,
            resolvedMax);

        // 忽略亚像素差异，避免每次按键因测量误差来回改 Width 导致抖动
        if (Math.Abs(editor.Width - targetWidth) < 1)
        {
            return;
        }

        editor.Width = targetWidth;
    }

    public static double ResolveMaxWidth(FrameworkElement titleText, double fallback = DefaultMaxWidth)
    {
        ArgumentNullException.ThrowIfNull(titleText);

        double maxWidth = titleText.MaxWidth;
        if (double.IsNaN(maxWidth) || double.IsInfinity(maxWidth) || maxWidth <= 0)
        {
            return fallback > 0 ? fallback : DefaultMaxWidth;
        }

        return maxWidth;
    }

    private static double MeasureWidth(string text, TextBox editor)
    {
        var measure = new TextBlock
        {
            Text = text,
            FontSize = editor.FontSize,
            FontFamily = editor.FontFamily,
            FontWeight = editor.FontWeight,
            FontStyle = editor.FontStyle,
            TextWrapping = TextWrapping.NoWrap
        };
        measure.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return measure.DesiredSize.Width;
    }
}
