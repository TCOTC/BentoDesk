// Copyright (c) BentoDesk. All rights reserved.

using BentoDesk.Helpers;
using BentoDesk.Services;

namespace BentoDesk.Views;

/// <summary>
/// Desktop-layer facade: windows only call Pin / Front / Restore through these
/// entry points — never scatter <see cref="WidgetLayerService"/> Z-order calls.
/// </summary>
public abstract partial class WidgetWindowBase
{
    /// <summary>
    /// Generation from the last <see cref="LayerOnUserActivate"/> Front, used so
    /// Activated's <see cref="LayerScheduleFrontSettle"/> does not bump front again.
    /// </summary>
    private long _layerFrontGestureGeneration;

    /// <summary>
    /// User click / title activate / interaction begin: become front peer once.
    /// Does not steal XAML focus — callers that need RootGrid focus use
    /// <see cref="ElevateForInteraction"/> / <see cref="OnElevated"/>.
    /// </summary>
    protected void LayerOnUserActivate(string reason = "user-activate")
    {
        if (HWnd == IntPtr.Zero || !Win32Helper.IsWindow(HWnd))
        {
            return;
        }

        IsAtDesktopLayer = true;
        // Do not clear SuppressIdleRestore here — expand sets it before Front.
        RestoreDesktopLayerWhenIdle = false;
        _layerFrontGestureGeneration = WidgetLayerService.Front(HWnd, reason);
    }

    /// <summary>
    /// WinUI may lift Z-order asynchronously after Activate — settle only if this
    /// HWND is still the designated front from the same gesture.
    /// </summary>
    protected void LayerScheduleFrontSettle(string reason = "activated-settle")
    {
        if (HWnd == IntPtr.Zero || !Win32Helper.IsWindow(HWnd))
        {
            return;
        }

        IsAtDesktopLayer = true;
        WidgetLayerService.ScheduleFront(HWnd, reason);
    }

    /// <summary>
    /// Show path (anti-flash): idempotent quiet pin, never steals peer front.
    /// </summary>
    protected void LayerOnShow(string reason = "show")
    {
        if (HWnd == IntPtr.Zero || !Win32Helper.IsWindow(HWnd))
        {
            return;
        }

        IsAtDesktopLayer = true;
        WidgetLayerService.Pin(HWnd, reason);
    }

    /// <summary>
    /// Deactivate / interaction end / display restore: quiet pin only.
    /// </summary>
    protected void LayerOnRestore(bool force = false, string reason = "restore")
    {
        if (!force && !RestoreDesktopLayerWhenIdle && SuppressIdleRestore)
        {
            App.LogVerbose(
                $"[WidgetVis] LayerOnRestore skipped hwnd=0x{HWnd.ToInt64():X} " +
                $"force={force} idle={RestoreDesktopLayerWhenIdle} suppress={SuppressIdleRestore}");
            return;
        }

        if (!force && (IsDragging || IsResizing || HasBlockingFlyoutOpen()))
        {
            if (force || RestoreDesktopLayerWhenIdle)
            {
                RestoreDesktopLayerWhenIdle = true;
            }

            App.Log(
                $"[WidgetVis] LayerOnRestore deferred hwnd=0x{HWnd.ToInt64():X} " +
                $"force={force} drag={IsDragging} resize={IsResizing} flyout={HasBlockingFlyoutOpen()}");
            return;
        }

        TopMostSafetyTimer?.Stop();
        TopMostSafetyTimer = null;
        SuppressIdleRestore = false;
        RestoreDesktopLayerWhenIdle = false;
        IsAtDesktopLayer = true;
        App.Log($"[WidgetVis] LayerOnRestore hwnd=0x{HWnd.ToInt64():X} force={force} reason={reason}");
        WidgetLayerService.Pin(HWnd, reason);
        ApplyBackdropPreference();
        WidgetLayerService.LogPeersSnapshot($"Restore:{reason}", HWnd);
        WidgetLayerService.SchedulePeersSettleSnapshot($"Restore:{reason}", HWnd);
    }

    /// <summary>
    /// After <c>MoveAndResize</c>: re-pin only — must not steal sibling front.
    /// </summary>
    protected void LayerAfterBoundsChange(string reason = "bounds")
    {
        if (!IsAtDesktopLayer || HWnd == IntPtr.Zero || !Win32Helper.IsWindow(HWnd))
        {
            return;
        }

        WidgetLayerService.Pin(HWnd, reason);
    }

    /// <summary>
    /// Hide path: quiet pin before becoming invisible.
    /// </summary>
    protected void LayerOnHide(string reason = "hide")
    {
        if (HWnd == IntPtr.Zero || !Win32Helper.IsWindow(HWnd))
        {
            return;
        }

        WidgetLayerService.Pin(HWnd, reason);
    }

    /// <summary>
    /// Close path: detach DefView ownership.
    /// </summary>
    protected void LayerOnClose()
    {
        if (HWnd == IntPtr.Zero)
        {
            return;
        }

        WidgetLayerService.Release(HWnd);
    }
}
