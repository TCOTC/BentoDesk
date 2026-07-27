using System.Globalization;
using System.Collections.ObjectModel;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BentoDesk.Helpers;
using BentoDesk.Models;
using BentoDesk.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace BentoDesk.ViewModels;

public partial class SettingsViewModel
{
    public string GetThemeDisplayName(string theme)
    {
        return theme switch
        {
            ThemeLight => _localizationService.T("Settings.Theme.Light"),
            ThemeDark => _localizationService.T("Settings.Theme.Dark"),
            _ => _localizationService.T("Settings.Theme.System")
        };
    }

    public string GetTrayIconStyleDisplayName(string style)
    {
        return style switch
        {
            TrayIconStyleColorful => _localizationService.T("Settings.TrayIcon.Colorful"),
            TrayIconStyleBlack => _localizationService.T("Settings.TrayIcon.Black"),
            TrayIconStyleWhite => _localizationService.T("Settings.TrayIcon.White"),
            _ => _localizationService.T("Settings.TrayIcon.System")
        };
    }

    public string GetCornerDisplayName(string corner)
    {
        return corner switch
        {
            CornerDefault => _localizationService.T("Settings.Corner.Default"),
            CornerSquare => _localizationService.T("Settings.Corner.Square"),
            CornerRound => _localizationService.T("Settings.Corner.Round"),
            _ => _localizationService.T("Settings.Corner.Small")
        };
    }

    public string GetMaterialTypeDisplayName(string material)
    {
        return material switch
        {
            MaterialMica => _localizationService.T("Settings.Material.Mica"),
            MaterialMicaAlt => _localizationService.T("Settings.Material.MicaAlt"),
            MaterialAcrylicBase => _localizationService.T("Settings.Material.AcrylicBase"),
            MaterialSolid => _localizationService.T("Settings.Material.Solid"),
            _ => _localizationService.T("Settings.Material.Acrylic")
        };
    }

    public string GetBorderColorModeDisplayName(string mode)
    {
        return mode switch
        {
            BorderColorAccent => _localizationService.T("Settings.BorderColor.Accent"),
            BorderColorNone => _localizationService.T("Settings.BorderColor.None"),
            _ => _localizationService.T("Settings.BorderColor.Neutral")
        };
    }

    public string GetBorderStyleDisplayName(string style)
    {
        return style switch
        {
            BorderMedium => _localizationService.T("Settings.Border.Medium"),
            BorderThick => _localizationService.T("Settings.Border.Thick"),
            _ => _localizationService.T("Settings.Border.Thin")
        };
    }

    public string GetWidgetCollapseBehaviorDisplayName(string behavior)
    {
        return WidgetCollapseBehaviorNames.Normalize(behavior) switch
        {
            WidgetCollapseBehavior.Expanded => _localizationService.T("Settings.CollapseBehavior.Expanded"),
            WidgetCollapseBehavior.Smart => _localizationService.T("Settings.CollapseBehavior.Smart"),
            _ => _localizationService.T("Settings.CollapseBehavior.Click")
        };
    }

    public string GetWidgetCompactWidthModeDisplayName(string mode)
    {
        return SettingsService.NormalizeWidgetCompactWidthMode(mode) ==
            SettingsService.WidgetCompactWidthModeIndependent
                ? _localizationService.T("Settings.Capsule.WidthMode.Independent")
                : _localizationService.T("Settings.Capsule.WidthMode.Aligned");
    }

    public string GetLayoutDensityDisplayName(string density)
    {
        return density switch
        {
            SettingsService.LayoutDensityCompact => _localizationService.T("Settings.Density.Compact"),
            SettingsService.LayoutDensityRelaxed => _localizationService.T("Settings.Density.Relaxed"),
            SettingsService.LayoutDensityCustom => _localizationService.T("Settings.Density.Custom"),
            _ => _localizationService.T("Settings.Density.Standard")
        };
    }

    public string GetAnimationPresetDisplayName(string preset)
    {
        return preset == AnimationPresetNone
            ? _localizationService.T("Settings.Animation.Preset.None")
            : _localizationService.T("Settings.Animation.Effect.Fade");
    }

    public string GetWidgetChromeModeDisplayName(string mode)
    {
        return NormalizeWidgetChromeModeSetting(mode, WidgetChromeMode.Standard) switch
        {
            SettingsService.WidgetChromeModeCompact => _localizationService.T("Settings.WidgetChrome.Compact"),
            SettingsService.WidgetChromeModeOverlay => _localizationService.T("Settings.WidgetChrome.Overlay"),
            SettingsService.WidgetChromeModeHidden => _localizationService.T("Settings.WidgetChrome.Hidden"),
            _ => _localizationService.T("Settings.WidgetChrome.Standard")
        };
    }

    public string GetWidgetTitleIconModeDisplayName(string mode)
    {
        return NormalizeWidgetTitleIconModeSetting(mode) switch
        {
            SettingsService.WidgetTitleIconModeLineMono => _localizationService.T("Settings.WidgetTitleIcon.LineMono"),
            SettingsService.WidgetTitleIconModeColor => _localizationService.T("Settings.WidgetTitleIcon.Color"),
            SettingsService.WidgetTitleIconModeHidden => _localizationService.T("Settings.WidgetTitleIcon.Hidden"),
            SettingsService.WidgetTitleIconModeTextLabel => _localizationService.T("Settings.WidgetTitleIcon.TextLabel"),
            _ => _localizationService.T("Settings.WidgetTitleIcon.FilledMono")
        };
    }

    public string GetHoverButtonActionDisplayName(string action)
    {
        return action switch
        {
            SettingsService.WidgetHoverActionLockPosition => _localizationService.T("Settings.HoverButtonActions.LockPosition"),
            SettingsService.WidgetHoverActionLockSize => _localizationService.T("Settings.HoverButtonActions.LockSize"),
            SettingsService.WidgetHoverActionAdd => _localizationService.T("Settings.HoverButtonActions.Add"),
            SettingsService.WidgetHoverActionMore => _localizationService.T("Settings.HoverButtonActions.More"),
            SettingsService.WidgetHoverActionDelete => _localizationService.T("Settings.HoverButtonActions.Delete"),
            _ => action
        };
    }

    public string GetMusicDisplayModeDisplayName(string mode)
    {
        return SettingsService.NormalizeMusicDisplayMode(mode) switch
        {
            SettingsService.MusicDisplayModeCover => _localizationService.T("Settings.Music.DisplayMode.Cover"),
            SettingsService.MusicDisplayModeControls => _localizationService.T("Settings.Music.DisplayMode.Controls"),
            SettingsService.MusicDisplayModeRecordVertical => _localizationService.T("Settings.Music.DisplayMode.RecordVertical"),
            SettingsService.MusicDisplayModeRecordHorizontal => _localizationService.T("Settings.Music.DisplayMode.RecordHorizontal"),
            _ => _localizationService.T("Settings.Music.DisplayMode.Auto")
        };
    }
}
