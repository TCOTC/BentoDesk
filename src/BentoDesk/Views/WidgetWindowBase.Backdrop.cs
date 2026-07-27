// Copyright (c) BentoDesk. All rights reserved.

using BentoDesk.Helpers;
using BentoDesk.Models;
using BentoDesk.Services;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Runtime.CompilerServices;
using Windows.Graphics;
using WinRT;
using WinRT.Interop;

namespace BentoDesk.Views;

public abstract partial class WidgetWindowBase
{
    protected void ApplyBackdropPreference()
    {
        if (HWnd == IntPtr.Zero || IsClosing)
        {
            return;
        }

        bool isDark = RootElement.ActualTheme == ElementTheme.Dark;
        double surfaceOpacity = Math.Clamp(WidgetOpacity, 0.0, 1.0);
        var tintColor = BuildNativeBackdropTintColor(isDark);
        string materialType = SettingsService.Settings.WidgetMaterialType;

        try
        {
            Win32Helper.SetWindowTheme(HWnd, isDark);
            Win32Helper.ApplyFullWindowFrame(HWnd);
            ApplyDwmBorderStyle(isDark);

            // 整窗 layered alpha 会让图标/文字一起变淡，且破坏系统背板；透明度只改背景层。
            Win32Helper.ClearTemporaryWindowAlpha(HWnd);
            EnsureDesktopBackdropStaysActive();

            int backdropType = Win32Helper.DWMSBT_NONE;
            bool controllerApplied = false;
            double intensity = NormalizeMaterialIntensity(
                SettingsService.Settings.WidgetMaterialIntensity);

            if (SettingsService.IsMicaMaterial(materialType))
            {
                DisposeAcrylicController();
                controllerApplied = ApplyMicaController(
                    isDark,
                    tintColor,
                    materialType == SettingsService.WidgetMaterialTypeMicaAlt);
            }

            if (!controllerApplied && SettingsService.IsAcrylicMaterial(materialType))
            {
                // 桌面钉住窗口几乎从不激活；必须保持 IsInputActive，并用带 alpha 的 FallbackColor，
                // 否则会一直画不透明实心，Tint/Lum 再怎么调也看不到壁纸。
                controllerApplied = ApplyAcrylicController(
                    isDark,
                    tintColor,
                    surfaceOpacity,
                    materialType == SettingsService.WidgetMaterialTypeAcrylicBase);
            }

            if (controllerApplied)
            {
                backdropType = Win32Helper.DWMSBT_NONE;
                Win32Helper.DwmSetWindowAttribute(HWnd, Win32Helper.DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));
                Win32Helper.DisableAccentPolicy(HWnd);
            }
            else if (materialType is SettingsService.WidgetMaterialTypeSolid)
            {
                DisposeAcrylicController();
                DetachMicaControllerTarget();
                backdropType = Win32Helper.DWMSBT_NONE;
                Win32Helper.DwmSetWindowAttribute(HWnd, Win32Helper.DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));
                Win32Helper.DisableAccentPolicy(HWnd);
            }
            else
            {
                DisposeAcrylicController();
                backdropType = Win32Helper.DWMSBT_TRANSIENTWINDOW;
                Win32Helper.DwmSetWindowAttribute(HWnd, Win32Helper.DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));
                DetachMicaControllerTarget();
                Win32Helper.ApplyAccentBlur(HWnd, tintColor, Math.Min(surfaceOpacity, 0.52), true);
            }

            App.LogVerbose(
                $"[Backdrop] hwnd=0x{HWnd.ToInt64():X} material={materialType} isDark={isDark} " +
                $"opacity={surfaceOpacity:F3} intensity={intensity:F3} " +
                $"tint=#{tintColor.A:X2}{tintColor.R:X2}{tintColor.G:X2}{tintColor.B:X2} " +
                $"dwmBackdropType={backdropType} " +
                $"acrylicController={AcrylicController is not null} micaController={MicaController is not null} " +
                $"inputActive={BackdropConfiguration?.IsInputActive} " +
                $"transparencyFx={Win32Helper.IsTransparencyEffectsEnabled()} " +
                $"acrylicSupported={DesktopAcrylicController.IsSupported()}");

            ScheduleInactiveBackdropControllerCleanup(materialType);
        }
        catch (Exception ex)
        {
            App.Log($"ApplyBackdropPreference fallback: {ex}");
            DisposeAcrylicController();
            DisposeMicaController();
            Win32Helper.ApplyAccentBlur(HWnd, tintColor, Math.Min(surfaceOpacity, 0.52), true);
        }

        ApplySurfaceStyle();
    }

    protected static SolidColorBrush GetOrUpdateSolidColorBrush(Brush? current, Windows.UI.Color color)
    {
        if (current is SolidColorBrush brush && MutableBrushes.TryGetValue(brush, out _))
        {
            try
            {
                if (!brush.Color.Equals(color))
                {
                    brush.Color = color;
                }

                return brush;
            }
            catch (Exception)
            {
                MutableBrushes.Remove(brush);
            }
        }

        var replacement = new SolidColorBrush(color);
        MutableBrushes.Add(replacement, MutableBrushMarker);
        return replacement;
    }

    protected (double Thickness, Windows.UI.Color BorderColor, Windows.UI.Color DividerColor)
        GetWidgetBorderVisuals(bool isDark, Windows.UI.Color accentColor)
    {
        string borderStyle = SettingsService.Settings.WidgetBorderStyle;
        string colorMode = SettingsService.Settings.WidgetBorderColorMode;
        var (thickness, alpha) = borderStyle switch
        {
            SettingsService.WidgetBorderStyleMedium => (1.2d, (byte)0x30),
            SettingsService.WidgetBorderStyleThick => (1.6d, (byte)0x48),
            _ => (0.8d, (byte)0x18)
        };

        if (colorMode == SettingsService.WidgetBorderColorModeNone)
        {
            thickness = 0;
            alpha = 0;
        }

        bool useAccent = colorMode == SettingsService.WidgetBorderColorModeAccent;
        byte borderAlpha = useAccent
            ? (byte)Math.Clamp(Math.Round(alpha * 1.35), 0, 255)
            : alpha;
        byte red = useAccent ? accentColor.R : isDark ? (byte)0xFF : (byte)0x00;
        byte green = useAccent ? accentColor.G : isDark ? (byte)0xFF : (byte)0x00;
        byte blue = useAccent ? accentColor.B : isDark ? (byte)0xFF : (byte)0x00;
        var borderColor = ColorHelper.FromArgb(borderAlpha, red, green, blue);
        var dividerColor = ColorHelper.FromArgb(
            (byte)Math.Clamp(Math.Round(borderAlpha * (isDark ? 0.66 : 0.42)), 0, 255),
            red,
            green,
            blue);
        return (thickness, borderColor, dividerColor);
    }

    private void ApplyCompactBorderVisuals(bool? isDarkOverride = null)
    {
        bool isDark = isDarkOverride ?? RootElement.ActualTheme == ElementTheme.Dark;
        var accentColor = App.Current.ThemeService?.GetEffectiveAccentColor()
            ?? AccentColorHelper.DefaultAccentColor;
        var (borderThickness, borderColor, _) = GetWidgetBorderVisuals(isDark, accentColor);

        try
        {
            if (new Windows.UI.ViewManagement.AccessibilitySettings().HighContrast)
            {
                borderThickness = Math.Max(1, borderThickness);
                borderColor = new Windows.UI.ViewManagement.UISettings().GetColorValue(
                    Windows.UI.ViewManagement.UIColorType.Foreground);
            }
        }
        catch
        {
            // Keep the configured border when accessibility APIs are unavailable.
        }

        var surface = WidgetShellControl.BackgroundSurface;
        surface.BorderThickness = new Thickness(borderThickness);
        surface.BorderBrush = GetOrUpdateSolidColorBrush(surface.BorderBrush, borderColor);
    }

    protected void ScheduleInactiveBackdropControllerCleanup(string materialType)
    {
        bool hasInactiveController = materialType switch
        {
            SettingsService.WidgetMaterialTypeMica or SettingsService.WidgetMaterialTypeMicaAlt =>
                AcrylicController is not null,
            SettingsService.WidgetMaterialTypeAcrylic or SettingsService.WidgetMaterialTypeAcrylicBase =>
                MicaController is not null,
            _ => AcrylicController is not null || MicaController is not null
        };

        if (!hasInactiveController)
        {
            _inactiveBackdropCleanupTimer?.Stop();
            return;
        }

        if (_inactiveBackdropCleanupTimer is null)
        {
            _inactiveBackdropCleanupTimer = DispatcherQueue.CreateTimer();
            _inactiveBackdropCleanupTimer.IsRepeating = false;
            _inactiveBackdropCleanupTimer.Tick += InactiveBackdropCleanupTimer_Tick;
        }

        _inactiveBackdropCleanupTimer.Stop();
        _inactiveBackdropCleanupTimer.Interval = InactiveBackdropControllerRetention;
        _inactiveBackdropCleanupTimer.Start();
    }

    private void InactiveBackdropCleanupTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        string materialType = SettingsService.Settings.WidgetMaterialType;
        bool releasedController = false;

        if (!SettingsService.IsAcrylicMaterial(materialType) && AcrylicController is not null)
        {
            DisposeAcrylicController();
            releasedController = true;
        }

        if (!SettingsService.IsMicaMaterial(materialType) && MicaController is not null)
        {
            DisposeMicaController();
            releasedController = true;
        }

        if (releasedController)
        {
            App.ScheduleLightMemoryCleanup();
        }
    }

    private static double NormalizeMaterialIntensity(double value) =>
        double.IsFinite(value)
            ? Math.Clamp(
                value,
                SettingsService.MinWidgetMaterialIntensity,
                SettingsService.MaxWidgetMaterialIntensity)
            : SettingsService.DefaultWidgetMaterialIntensity;

    private static double LerpMaterialValue(double start, double end, double progress) =>
        start + ((end - start) * Math.Clamp(progress, 0.0, 1.0));

    protected bool ApplyMicaController(
        bool isDark,
        Windows.UI.Color tintColor,
        bool useAlt)
    {
        if (!MicaController.IsSupported())
        {
            DisposeMicaController();
            return false;
        }

        BackdropTarget ??= this.As<ICompositionSupportsSystemBackdrop>();
        BackdropConfiguration ??= new SystemBackdropConfiguration();
        BackdropConfiguration.IsInputActive = true;
        BackdropConfiguration.Theme = isDark ? SystemBackdropTheme.Dark : SystemBackdropTheme.Light;

        if (MicaController is not null && _micaControllerUsesAlt != useAlt)
        {
            DisposeMicaController();
        }

        if (MicaController is null)
        {
            DetachAcrylicControllerTarget();
            MicaController = new MicaController
            {
                Kind = useAlt ? MicaKind.BaseAlt : MicaKind.Base
            };
            _micaControllerUsesAlt = useAlt;
        }

        DetachAcrylicControllerTarget();
        if (!MicaControllerAttached)
        {
            if (!MicaController.AddSystemBackdropTarget(BackdropTarget))
            {
                DisposeMicaController();
                return false;
            }

            MicaControllerAttached = true;
            MicaController.SetSystemBackdropConfiguration(BackdropConfiguration);
        }

        double intensity = NormalizeMaterialIntensity(
            SettingsService.Settings.WidgetMaterialIntensity);
        var effectiveTint = BuildAcrylicAccentTintColor(isDark, tintColor, intensity);
        var fallback = useAlt
            ? isDark
                ? ColorHelper.FromArgb(0xFF, 0x16, 0x18, 0x1D)
                : ColorHelper.FromArgb(0xFF, 0xE8, 0xEA, 0xEF)
            : isDark
                ? ColorHelper.FromArgb(0xFF, 0x20, 0x22, 0x26)
                : ColorHelper.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);

        double tintOpacity = useAlt
            ? LerpMaterialValue(0.18, 0.90, intensity)
            : LerpMaterialValue(0.02, 0.62, intensity);
        double luminosityOpacity = useAlt
            ? LerpMaterialValue(isDark ? 0.28 : 0.36, isDark ? 0.82 : 0.88, intensity)
            : LerpMaterialValue(isDark ? 0.70 : 0.76, isDark ? 0.96 : 0.98, intensity);

        MicaController.Kind = useAlt ? MicaKind.BaseAlt : MicaKind.Base;
        MicaController.TintColor = effectiveTint;
        MicaController.FallbackColor = BlendBackdropColors(fallback, effectiveTint, intensity * 0.55);
        MicaController.TintOpacity = (float)tintOpacity;
        MicaController.LuminosityOpacity = (float)luminosityOpacity;
        MicaController.SetSystemBackdropConfiguration(BackdropConfiguration);
        return true;
    }

    protected void DisposeMicaController()
    {
        if (MicaController is null)
        {
            return;
        }

        try
        {
            MicaController.RemoveAllSystemBackdropTargets();
            MicaController.Dispose();
        }
        catch
        {
        }
        finally
        {
            MicaController = null;
            MicaControllerAttached = false;
            _micaControllerUsesAlt = null;
        }
    }

    protected void DetachMicaControllerTarget()
    {
        if (MicaController is null || !MicaControllerAttached)
        {
            return;
        }

        try
        {
            MicaController.RemoveAllSystemBackdropTargets();
        }
        catch
        {
        }
        finally
        {
            MicaControllerAttached = false;
        }
    }

    protected static Windows.UI.Color BuildAcrylicAccentTintColor(
        bool isDark,
        Windows.UI.Color baseTint,
        double intensity)
    {
        intensity = Math.Clamp(intensity, 0.0, 1.0);
        var accent = App.Current.ThemeService?.GetEffectiveAccentColor()
            ?? AccentColorHelper.DefaultAccentColor;
        double accentMix = LerpMaterialValue(isDark ? 0.04 : 0.06, isDark ? 0.42 : 0.36, intensity);
        return BlendBackdropColors(baseTint, accent, accentMix);
    }

    /// <summary>
    /// 桌面钉住窗口几乎从不成为前台；若 IsInputActive=false，亚克力会掉进不透明 Fallback。
    /// </summary>
    protected void EnsureDesktopBackdropStaysActive()
    {
        BackdropConfiguration ??= new SystemBackdropConfiguration();
        BackdropConfiguration.IsInputActive = true;

        try
        {
            AcrylicController?.SetSystemBackdropConfiguration(BackdropConfiguration);
            MicaController?.SetSystemBackdropConfiguration(BackdropConfiguration);
        }
        catch
        {
        }
    }

    private void WidgetWindowBase_Activated(object sender, WindowActivatedEventArgs args)
    {
        // 无论激活还是失活，桌面盒子都保持背板 Active，才能持续透出壁纸。
        EnsureDesktopBackdropStaysActive();
    }

    private static Windows.UI.Color BlendBackdropColors(
        Windows.UI.Color fromColor,
        Windows.UI.Color toColor,
        double amount)
    {
        amount = Math.Clamp(amount, 0.0, 1.0);
        static byte BlendChannel(byte from, byte to, double mix) =>
            (byte)Math.Clamp(Math.Round(from + ((to - from) * mix)), 0, 255);

        return ColorHelper.FromArgb(
            BlendChannel(fromColor.A, toColor.A, amount),
            BlendChannel(fromColor.R, toColor.R, amount),
            BlendChannel(fromColor.G, toColor.G, amount),
            BlendChannel(fromColor.B, toColor.B, amount));
    }

    protected bool ApplyAcrylicController(
        bool isDark,
        Windows.UI.Color tintColor,
        double surfaceOpacity,
        bool useBase)
    {
        if (!DesktopAcrylicController.IsSupported())
        {
            DisposeAcrylicController();
            return false;
        }

        double opacity = Math.Clamp(surfaceOpacity, 0.0, 1.0);
        double intensity = NormalizeMaterialIntensity(
            SettingsService.Settings.WidgetMaterialIntensity);

        BackdropTarget ??= this.As<ICompositionSupportsSystemBackdrop>();
        BackdropConfiguration ??= new SystemBackdropConfiguration();
        // 桌面盒子必须保持 Active，否则 Tint/Lum 无效，只剩不透明 Fallback。
        BackdropConfiguration.IsInputActive = true;
        BackdropConfiguration.Theme = isDark ? SystemBackdropTheme.Dark : SystemBackdropTheme.Light;
        BackdropConfiguration.HighContrastBackgroundColor = isDark
            ? ColorHelper.FromArgb(0xFF, 0x20, 0x20, 0x20)
            : ColorHelper.FromArgb(0xFF, 0xF3, 0xF3, 0xF3);

        // 透明度/浓度变化时重建，并在挂载前写入 Tint/Lum（事后改属性在桌面层常无效）。
        bool kindChanged = _acrylicControllerUsesBase != useBase;
        bool valuesChanged =
            !double.IsFinite(_lastAcrylicControllerOpacity) ||
            !double.IsFinite(_lastAcrylicControllerIntensity) ||
            Math.Abs(_lastAcrylicControllerOpacity - opacity) > 0.001 ||
            Math.Abs(_lastAcrylicControllerIntensity - intensity) > 0.001;
        if (AcrylicController is not null && (kindChanged || valuesChanged))
        {
            DisposeAcrylicController();
        }

        // 背景透视（不影响内容）：cover=0 尽量透出壁纸，cover=1 更实。
        // 系统透明开启时是真亚克力：必须把 Tint/Lum 拉满行程，否则滑块几乎无感、只剩固定磨砂。
        double cover = Math.Clamp(opacity, 0.0, 1.0);
        double peakTint = useBase
            ? LerpMaterialValue(isDark ? 0.62 : 0.55, isDark ? 0.92 : 0.88, intensity)
            : LerpMaterialValue(isDark ? 0.48 : 0.40, isDark ? 0.82 : 0.76, intensity);
        double peakLuminosity = useBase
            ? LerpMaterialValue(isDark ? 0.78 : 0.82, isDark ? 0.98 : 0.98, intensity)
            : LerpMaterialValue(isDark ? 0.62 : 0.68, isDark ? 0.95 : 0.96, intensity);
        // 低透明度端压到接近 0，高透明度端用满峰值，保证两端反差大。
        double tintOpacity = LerpMaterialValue(0.0, peakTint, cover);
        double luminosityOpacity = LerpMaterialValue(0.0, peakLuminosity, cover);
        var effectiveTint = BuildAcrylicAccentTintColor(isDark, tintColor, intensity);
        // Fallback 也必须跟透明度走：系统透明关闭时只画 Fallback，alpha 就是滑块行程。
        byte fallbackAlpha = (byte)Math.Clamp(Math.Round(255.0 * cover), 0, 255);
        var fallbackColor = ColorHelper.FromArgb(
            fallbackAlpha,
            effectiveTint.R,
            effectiveTint.G,
            effectiveTint.B);

        if (AcrylicController is null || AcrylicController.IsClosed)
        {
            DetachMicaControllerTarget();
            AcrylicController = new DesktopAcrylicController
            {
                Kind = useBase ? DesktopAcrylicKind.Base : DesktopAcrylicKind.Thin,
                TintColor = effectiveTint,
                FallbackColor = fallbackColor,
                TintOpacity = (float)tintOpacity,
                LuminosityOpacity = (float)luminosityOpacity
            };
            _acrylicControllerUsesBase = useBase;
            AcrylicControllerAttached = false;
        }

        DetachMicaControllerTarget();
        if (!AcrylicControllerAttached)
        {
            if (!AcrylicController.AddSystemBackdropTarget(BackdropTarget))
            {
                DisposeAcrylicController();
                return false;
            }

            AcrylicControllerAttached = true;
            AcrylicController.SetSystemBackdropConfiguration(BackdropConfiguration);
        }
        else
        {
            AcrylicController.Kind = useBase ? DesktopAcrylicKind.Base : DesktopAcrylicKind.Thin;
            AcrylicController.TintColor = effectiveTint;
            AcrylicController.FallbackColor = fallbackColor;
            AcrylicController.TintOpacity = (float)tintOpacity;
            AcrylicController.LuminosityOpacity = (float)luminosityOpacity;
            AcrylicController.SetSystemBackdropConfiguration(BackdropConfiguration);
        }

        // 挂载后再写一遍，避免 AddSystemBackdropTarget 重置属性。
        AcrylicController.Kind = useBase ? DesktopAcrylicKind.Base : DesktopAcrylicKind.Thin;
        AcrylicController.TintColor = effectiveTint;
        AcrylicController.FallbackColor = fallbackColor;
        AcrylicController.TintOpacity = (float)tintOpacity;
        AcrylicController.LuminosityOpacity = (float)luminosityOpacity;
        AcrylicController.SetSystemBackdropConfiguration(BackdropConfiguration);

        _lastAcrylicControllerOpacity = opacity;
        _lastAcrylicControllerIntensity = intensity;
        App.LogVerbose(
            $"[Backdrop] acrylic tintOp={tintOpacity:F3} lumOp={luminosityOpacity:F3} " +
            $"fallbackA={fallbackAlpha} cover={cover:F3} kind={(useBase ? "Base" : "Thin")}");
        return true;
    }

    /// <summary>
    /// 系统透明开启时，真亚克力本身几乎总是磨砂；用轻遮罩把「背景透明度」滑块补出可见行程。
    /// 系统透明关闭时不要用遮罩——那时 FallbackColor 的 alpha 已经承担透明度。
    /// </summary>
    protected static Windows.UI.Color BuildAcrylicPlateOverlayColor(
        bool isDark,
        Windows.UI.Color accentColor,
        double surfaceOpacity,
        double intensity)
    {
        double cover = Math.Clamp(surfaceOpacity, 0.0, 1.0);
        intensity = Math.Clamp(intensity, 0.0, 1.0);
        double plateOpacity = Math.Clamp(
            LerpMaterialValue(0.0, isDark ? 0.42 : 0.34, cover) *
            LerpMaterialValue(0.70, 1.20, intensity),
            0.0,
            isDark ? 0.50 : 0.42);

        var baseColor = isDark
            ? ColorHelper.FromArgb(0xFF, 0x20, 0x22, 0x26)
            : ColorHelper.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);
        var tinted = BlendBackdropColors(baseColor, accentColor, isDark ? 0.10 : 0.08);
        byte alpha = (byte)Math.Clamp(Math.Round(plateOpacity * 255.0), 0, 255);
        return ColorHelper.FromArgb(alpha, tinted.R, tinted.G, tinted.B);
    }

    protected bool ApplyTransparentAcrylicController(bool isDark)
    {
        if (!DesktopAcrylicController.IsSupported())
        {
            DisposeAcrylicController();
            return false;
        }

        BackdropTarget ??= this.As<ICompositionSupportsSystemBackdrop>();
        BackdropConfiguration ??= new SystemBackdropConfiguration();
        BackdropConfiguration.IsInputActive = true;
        BackdropConfiguration.Theme = isDark ? SystemBackdropTheme.Dark : SystemBackdropTheme.Light;

        DisposeAcrylicController();
        DetachMicaControllerTarget();
        AcrylicController = new DesktopAcrylicController
        {
            Kind = DesktopAcrylicKind.Thin,
            TintColor = Colors.Transparent,
            FallbackColor = Colors.Transparent,
            TintOpacity = 0.0f,
            LuminosityOpacity = 0.0f
        };
        _acrylicControllerUsesBase = false;
        AcrylicControllerAttached = false;

        if (!AcrylicController.AddSystemBackdropTarget(BackdropTarget))
        {
            DisposeAcrylicController();
            return false;
        }

        AcrylicControllerAttached = true;
        AcrylicController.SetSystemBackdropConfiguration(BackdropConfiguration);
        AcrylicController.TintColor = Colors.Transparent;
        AcrylicController.FallbackColor = Colors.Transparent;
        AcrylicController.TintOpacity = 0.0f;
        AcrylicController.LuminosityOpacity = 0.0f;
        return true;
    }

    protected void DisposeAcrylicController()
    {
        if (AcrylicController is null)
        {
            return;
        }

        try
        {
            AcrylicController.RemoveAllSystemBackdropTargets();
            AcrylicController.Dispose();
        }
        catch
        {
        }
        finally
        {
            AcrylicController = null;
            AcrylicControllerAttached = false;
            _acrylicControllerUsesBase = null;
        }
    }

    protected void DetachAcrylicControllerTarget()
    {
        if (AcrylicController is null || !AcrylicControllerAttached)
        {
            return;
        }

        try
        {
            AcrylicController.RemoveAllSystemBackdropTargets();
        }
        catch
        {
        }
        finally
        {
            AcrylicControllerAttached = false;
        }
    }

    // ── Backdrop refresh timer ─────────────────────────────────

    protected void QueueBackdropRefresh()
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(QueueBackdropRefresh);
            return;
        }

        ++BackdropRefreshGeneration;
        _backdropRefreshStage = 0;

        if (_backdropRefreshTimer is null)
        {
            _backdropRefreshTimer = DispatcherQueue.CreateTimer();
            _backdropRefreshTimer.Tick += (_, _) => OnBackdropRefreshTick(BackdropRefreshGeneration);
        }
        else
        {
            _backdropRefreshTimer.Stop();
        }

        _backdropRefreshTimer.Interval = TimeSpan.FromMilliseconds(BackdropRefreshDelays[0]);
        _backdropRefreshTimer.Start();
    }

    private void OnBackdropRefreshTick(long generation)
    {
        if (generation != BackdropRefreshGeneration)
        {
            _backdropRefreshTimer?.Stop();
            return;
        }

        RefreshBackdropIfCurrent(generation);

        int nextStage = _backdropRefreshStage + 1;
        _backdropRefreshStage = nextStage;

        if (nextStage < BackdropRefreshDelays.Length)
        {
            _backdropRefreshTimer!.Interval = TimeSpan.FromMilliseconds(BackdropRefreshDelays[nextStage]);
        }
        else
        {
            _backdropRefreshTimer!.Stop();
        }
    }

    private void RefreshBackdropIfCurrent(long generation)
    {
        if (generation != BackdropRefreshGeneration)
        {
            return;
        }

        if (!Visible || IsHideAnimationRunning)
        {
            return;
        }

        // Skip backdrop refresh during drag/resize — the window is moving
        // and the backdrop will be refreshed once when the operation ends.
        if (IsDragging || IsResizing)
        {
            return;
        }

        ApplyBackdropPreference();
    }

    protected void StopBackdropRefreshTimer()
    {
        _backdropRefreshTimer?.Stop();
        _backdropRefreshTimer = null;
    }

    // ── Layer / Z-order management ─────────────────────────────
}
