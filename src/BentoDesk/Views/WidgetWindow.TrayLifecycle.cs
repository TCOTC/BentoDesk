using System.ComponentModel;
using BentoDesk.Helpers;
using BentoDesk.Models;
using BentoDesk.Services;
using BentoDesk.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT;
using WinRT.Interop;

namespace BentoDesk.Views;

public sealed partial class WidgetWindow
{
    public void PushToBottom()
    {
        _isAtDesktopLayer = true;
        WidgetLayerService.MoveToDesktopBottom(_hWnd);
        App.LogVerbose($"[ZOrder] Widget PushToBottom hwnd=0x{_hWnd.ToInt64():X}");
    }

    public void ShowPreparedAtDesktopLayer(bool persistVisibility = true, bool revealWindow = true)
    {
        LogTrayWindow($"ShowPreparedAtDesktopLayer reveal={revealWindow}");
        // Essential anti-flash: Win32 alpha=0 hides the whole HWND (including Mica)
        // while AppWindow.Show may briefly sit above other apps. Attach first, then
        // clear alpha only after the next dispatcher tick.
        _trayAnimation.CloakWindowForTrayShow();
        Win32Helper.SetTemporaryWindowAlpha(_hWnd, 0);
        _trayAnimation.PrepareHiddenState();
        PushToBottom();
        // Show(false): default AppWindow.Show() activates and steals focus from other apps.
        _appWindow.Show(false);
        Win32Helper.ShowWindow(_hWnd, Win32Helper.SW_SHOWNOACTIVATE);
        PushToBottom();

        Visible = true;
        ViewModel.Config.IsVisible = true;
        if (persistVisibility)
        {
            _settingsService.SaveDebounced();
        }

        if (revealWindow)
        {
            FinishDesktopLayerShow();
        }
    }

    /// <summary>
    /// After Show+attach: stay alpha-hidden through one dispatcher tick plus a short
    /// settle (AppWindow reorders asynchronously), then restore visuals and clear alpha.
    /// </summary>
    private void FinishDesktopLayerShow(Action? beforeVisible = null)
    {
        if (!DispatcherQueue.TryEnqueue(async () =>
            {
                if (!Visible)
                {
                    return;
                }

                PushToBottom();
                _trayAnimation.RevealWindowForTrayShow();
                beforeVisible?.Invoke();
                PushToBottom();
                // AppWindow.Show side effects often land after the first tick.
                await Task.Delay(32);
                if (!Visible)
                {
                    return;
                }

                PushToBottom();
                // Fade / ScaleFade / Zoom keep Win32 alpha when a fade-in will
                // follow (Composition opacity cannot cover Mica). The
                // no-animation path passes beforeVisible and must clear here.
                if (beforeVisible is not null || !_trayAnimation.HasPreparedSoftOpacity)
                {
                    Win32Helper.ClearTemporaryWindowAlpha(_hWnd);
                    QueueBackdropRefresh();
                }
            }))
        {
            PushToBottom();
            _trayAnimation.RevealWindowForTrayShow();
            beforeVisible?.Invoke();
            if (beforeVisible is not null || !_trayAnimation.HasPreparedSoftOpacity)
            {
                Win32Helper.ClearTemporaryWindowAlpha(_hWnd);
                QueueBackdropRefresh();
            }
        }
    }

    public void SetTrayAnimationOffsetOverride(double? offsetX, double? offsetY)
    {
        _trayAnimation.SetOffsetOverride(offsetX, offsetY);
    }

    public void RaiseTemporarilyFromTray()
    {
        PrepareTrayShowAnimation();
        ShowPreparedRaisedFromTray();
        PlayTrayRaiseAnimationAfterFirstFrame();
    }

    public void ShowPreparedRaisedFromTray(bool persistVisibility = true)
    {
        // Desktop-fixed layer: "raise" is show-at-desktop-layer, not topmost.
        LogTrayWindow("ShowPreparedRaisedFromTray");
        ShowPreparedAtDesktopLayer(persistVisibility);
    }

    public void EnsureRaisedFromTrayTopMost()
    {
        if (!Visible)
        {
            App.LogVerbose($"[ZOrder] Widget EnsureRaisedFromTrayTopMost SKIPPED not-visible hwnd=0x{_hWnd.ToInt64():X}");
            return;
        }

        if (_isAtDesktopLayer)
        {
            App.LogVerbose($"[ZOrder] Widget EnsureRaisedFromTrayTopMost SKIPPED atDesktop hwnd=0x{_hWnd.ToInt64():X}");
            return;
        }

        if (App.Current.WidgetManager is not { WidgetsRaisedFromTray: true })
        {
            App.LogVerbose($"[ZOrder] Widget EnsureRaisedFromTrayTopMost SKIPPED not-raised hwnd=0x{_hWnd.ToInt64():X}");
            return;
        }

        App.LogVerbose($"[ZOrder] Widget EnsureRaisedFromTrayTopMost hwnd=0x{_hWnd.ToInt64():X} atDesktop={_isAtDesktopLayer}");
        _appWindow.Show();
        Win32Helper.ShowWindow(_hWnd, Win32Helper.SW_SHOWNORMAL);
        WidgetLayerService.BringToFront(_hWnd);
        HoldTemporaryTopMost();
    }

    public void ActivateRaisedFromTrayBatch()
    {
        if (!Visible)
        {
            return;
        }

        HoldTemporaryTopMost();
        base.Activate();
        Win32Helper.SetForegroundWindow(_hWnd);
        RootGrid.Focus(FocusState.Programmatic);
    }

    public void PlayTrayShowAnimation()
    {
        PlayTrayRaiseAnimationAfterFirstFrame();
    }

    public void PlayPreparedTrayHideAnimation()
    {
        if (!_isHideAnimationRunning)
        {
            return;
        }

        PlayTrayHideAnimation(CompleteTrayHideAnimation);
    }

    public void PrepareTrayShowAnimation()
    {
        _trayAnimation.NextGeneration();
        _trayAnimation.StopAndRestoreWindowPosition();
        _trayAnimation.CloakWindowForTrayShow();
        RestoreItemContainerTransitions();
        SuppressItemContainerTransitions();
        _isHideAnimationRunning = false;

        var animationProfile = GetTrayAnimationProfile();
        LogTrayWindow(
            $"PrepareShow gen={_trayAnimation.Generation} effect={_settingsService.Settings.WidgetAnimationEffect} " +
            $"speed={_settingsService.Settings.WidgetAnimationSpeed} enabled={animationProfile.IsEnabled} durationMs={animationProfile.DurationMs}");
        _trayAnimation.PrepareVisualState(
            animationProfile.ShowOffsetX,
            animationProfile.ShowOffsetY,
            animationProfile.ShowStartOpacity,
            animationProfile.ShowStartScale);
    }

    public void CompleteTrayShowWithoutAnimation()
    {
        var animationGeneration = _trayAnimation.NextGeneration();
        LogTrayWindow($"CompleteShowWithoutAnimation gen={animationGeneration}");
        _trayAnimation.Stop();
        SetTrayAnimationOffsetOverride(null, null);
        // Do not restore on-screen visuals before FinishDesktopLayerShow clears alpha.
        Win32Helper.SetTemporaryWindowAlpha(_hWnd, 0);
        PushToBottom();
        FinishDesktopLayerShow(() =>
        {
            _trayAnimation.RestoreVisualState();
            _trayAnimation.RestoreWindowPosition();
            QueueItemContainerTransitionRestore(animationGeneration);
        });
    }

    public WidgetTrayBatchAnimationEntry? BeginSharedTrayShowAnimation()
    {
        var animationGeneration = _trayAnimation.NextGeneration();
        _trayAnimation.Stop();
        RestoreItemContainerTransitions();
        SuppressItemContainerTransitions();
        _isHideAnimationRunning = false;

        var animationProfile = GetTrayAnimationProfile();
        if (!animationProfile.IsEnabled)
        {
            LogTrayWindow($"SharedShow skipped reason=animation-disabled gen={animationGeneration}");
            CompleteTrayShowWithoutAnimation();
            return null;
        }

        LogTrayWindow($"SharedShow gen={animationGeneration} durationMs={animationProfile.DurationMs}");
        return _trayAnimation.BeginSharedAnimate(
            animationProfile.ShowOffsetX,
            animationProfile.ShowOffsetY,
            0,
            0,
            animationProfile.ShowStartOpacity,
            WidgetTrayAnimationController.RestingOpacity,
            animationProfile.ShowStartScale,
            WidgetTrayAnimationController.RestingScale,
            animationProfile.DurationMs,
            true,
            animationGeneration,
            _settingsService.Settings.WidgetAnimationEasingIntensity,
            () =>
            {
                _trayAnimation.RestoreVisualState();
                _trayAnimation.RestoreWindowPosition();
                QueueBackdropRefresh();
                QueueItemContainerTransitionRestore(animationGeneration);
            });
    }

    public WidgetTrayBatchAnimationEntry? BeginSharedTrayHideAnimation()
    {
        if (!_isHideAnimationRunning)
        {
            return null;
        }

        var animationGeneration = _trayAnimation.Generation;
        var animationProfile = GetTrayAnimationProfile();
        if (!animationProfile.IsEnabled)
        {
            LogTrayWindow($"SharedHide skipped reason=animation-disabled gen={animationGeneration}");
            CompleteTrayHideAnimation();
            return null;
        }

        LogTrayWindow($"SharedHide gen={animationGeneration} durationMs={animationProfile.DurationMs}");
        return _trayAnimation.BeginSharedAnimate(
            0,
            0,
            animationProfile.HideOffsetX,
            animationProfile.HideOffsetY,
            WidgetTrayAnimationController.RestingOpacity,
            animationProfile.HideEndOpacity,
            WidgetTrayAnimationController.RestingScale,
            animationProfile.HideEndScale,
            animationProfile.DurationMs,
            false,
            animationGeneration,
            _settingsService.Settings.WidgetAnimationEasingIntensity,
            () =>
            {
                if (Visible)
                {
                    return;
                }

                CompleteTrayHideAnimation();
            });
    }

    private void PlayTrayRaiseAnimation()
    {
        var animationGeneration = _trayAnimation.NextGeneration();
        _trayAnimation.Stop();
        RestoreItemContainerTransitions();
        SuppressItemContainerTransitions();
        _isHideAnimationRunning = false;

        var animationProfile = GetTrayAnimationProfile();
        if (!animationProfile.IsEnabled)
        {
            LogTrayWindow($"PlayShow skipped reason=animation-disabled gen={animationGeneration}");
            CompleteTrayShowWithoutAnimation();
            return;
        }

        LogTrayWindow($"PlayShow gen={animationGeneration} durationMs={animationProfile.DurationMs}");
        _trayAnimation.Animate(
            animationProfile.ShowOffsetX,
            animationProfile.ShowOffsetY,
            0,
            0,
            animationProfile.ShowStartOpacity,
            WidgetTrayAnimationController.RestingOpacity,
            animationProfile.ShowStartScale,
            WidgetTrayAnimationController.RestingScale,
            animationProfile.DurationMs,
            true,
            animationGeneration,
            _settingsService.Settings.WidgetAnimationEasingIntensity,
            () =>
        {
            _trayAnimation.RestoreVisualState();
            _trayAnimation.RestoreWindowPosition();
            QueueBackdropRefresh();
            QueueItemContainerTransitionRestore(animationGeneration);
        });
    }

    private void PlayTrayRaiseAnimationAfterFirstFrame()
    {
        if (Visible)
        {
            _trayAnimation.PlayAfterContentReady(PlayTrayRaiseAnimation);
        }
    }

    public bool PrepareTrayHideAnimation(bool persistVisibility = true)
    {
        if (!Visible || _isHideAnimationRunning)
        {
            LogTrayWindow($"PrepareHide skipped visible={Visible} hideRunning={_isHideAnimationRunning}");
            return false;
        }

        _trayAnimation.NextGeneration();
        _trayAnimation.RevealWindowForTrayShow();
        // Stop any in-flight animation but do NOT snap the HWND back to
        // _targetPosition: when the widget is expanded via collapse mode the
        // current bounds differ from _targetPosition (stale compact bounds),
        // and restoring would cause a visible position jump before the hide
        // animation begins. PrepareVisualState below will set _targetPosition
        // from the actual current bounds.
        _trayAnimation.Stop();
        RestoreItemContainerTransitions();
        SuppressItemContainerTransitions();

        _isHideAnimationRunning = true;
        Visible = false;
        ViewModel.Config.IsVisible = false;
        if (persistVisibility)
        {
            _settingsService.SaveDebounced();
        }

        LogTrayWindow($"PrepareHide gen={_trayAnimation.Generation}");
        App.Log(
            $"[WidgetVis] PrepareHide hwnd=0x{_hWnd.ToInt64():X} persist={persistVisibility} " +
            $"gen={_trayAnimation.Generation}");
        WidgetLayerService.LogPeersSnapshot("PrepareHide", _hWnd);
        _trayAnimation.PrepareVisualState(
            0,
            0,
            WidgetTrayAnimationController.RestingOpacity,
            WidgetTrayAnimationController.RestingScale);
        return true;
    }

    private void PlayTrayHideAnimation(Action completed)
    {
        var animationGeneration = _trayAnimation.Generation;
        var animationProfile = GetTrayAnimationProfile();
        if (!animationProfile.IsEnabled)
        {
            LogTrayWindow($"PlayHide skipped reason=animation-disabled gen={animationGeneration}");
            completed();
            return;
        }

        LogTrayWindow($"PlayHide gen={animationGeneration} durationMs={animationProfile.DurationMs}");
        _trayAnimation.Animate(
            0,
            0,
            animationProfile.HideOffsetX,
            animationProfile.HideOffsetY,
            WidgetTrayAnimationController.RestingOpacity,
            animationProfile.HideEndOpacity,
            WidgetTrayAnimationController.RestingScale,
            animationProfile.HideEndScale,
            animationProfile.DurationMs,
            false,
            animationGeneration,
            _settingsService.Settings.WidgetAnimationEasingIntensity,
            () =>
        {
            if (Visible)
            {
                return;
            }
            completed();
        });
    }

    private void SuppressItemContainerTransitions()
    {
        if (_areItemTransitionsSuppressed)
        {
            return;
        }

        _savedGridItemTransitions = ItemsGridView.ItemContainerTransitions;
        _savedListItemTransitions = ItemsListView.ItemContainerTransitions;
        ItemsGridView.ItemContainerTransitions = new TransitionCollection();
        ItemsListView.ItemContainerTransitions = new TransitionCollection();
        _areItemTransitionsSuppressed = true;
    }

    private void RestoreItemContainerTransitions()
    {
        if (!_areItemTransitionsSuppressed)
        {
            return;
        }

        ItemsGridView.ItemContainerTransitions = _savedGridItemTransitions;
        ItemsListView.ItemContainerTransitions = _savedListItemTransitions;
        _savedGridItemTransitions = null;
        _savedListItemTransitions = null;
        _areItemTransitionsSuppressed = false;
    }

    private void QueueItemContainerTransitionRestore(long animationGeneration)
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            await Task.Delay(ItemTransitionRestoreDelayMs);
            if (animationGeneration == _trayAnimation.Generation)
            {
                RestoreItemContainerTransitions();
            }
        });
    }

    public void CompleteTrayHideAnimation()
    {
        if (Visible)
        {
            LogTrayWindow("CompleteHide skipped reason=visible-again");
            return;
        }

        _isHideAnimationRunning = false;
        _trayAnimation.Stop();
        WidgetLayerService.ClearTopMost(_hWnd);
        Win32Helper.ShowWindow(_hWnd, Win32Helper.SW_HIDE);
        _appWindow.Hide();
        _trayAnimation.RevealWindowForTrayShow();
        _trayAnimation.RestoreVisualState();
        QueueItemContainerTransitionRestore(_trayAnimation.Generation);
        _trayAnimation.RestoreWindowPosition();
        LogTrayWindow("CompleteHide");
    }

    public void RevealFromTray(bool autoRestore = true)
    {
        PrepareTrayShowAnimation();
        ShowPreparedAtDesktopLayer();
        PlayTrayRaiseAnimationAfterFirstFrame();

        if (!autoRestore)
        {
            return;
        }

        // Keep layer pinned; timer only re-asserts desktop attachment if needed.
        _autoRestoreTimer?.Stop();
        _autoRestoreTimer = DispatcherQueue.CreateTimer();
        _autoRestoreTimer.IsRepeating = false;
        _autoRestoreTimer.Interval = TimeSpan.FromMilliseconds(1200);
        _autoRestoreTimer.Tick += (_, _) =>
        {
            _autoRestoreTimer?.Stop();
            _autoRestoreTimer = null;
            if (!_isDragging && !_isResizing)
            {
                RestoreDesktopLayer(force: true);
            }
        };
        _autoRestoreTimer.Start();
    }

    public void HideWindow()
    {
        if (!PrepareTrayHideAnimation())
        {
            return;
        }

        PlayTrayHideAnimation(CompleteTrayHideAnimation);
    }

public void CloseWindow()
{
_trayAnimation.RevealWindowForTrayShow();
WidgetLayerService.ReleaseWindow(_hWnd);
Close();
}
}
