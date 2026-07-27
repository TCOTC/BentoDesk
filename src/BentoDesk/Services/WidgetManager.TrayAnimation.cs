// Copyright (c) BentoDesk. All rights reserved.

using BentoDesk.Models;
using BentoDesk.Helpers;
using BentoDesk.Controls.WidgetContents;
using BentoDesk.ViewModels;
using BentoDesk.Views;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace BentoDesk.Services;

/// <summary>
/// Partial class containing TrayAnimation logic for WidgetManager.
/// </summary>
public sealed partial class WidgetManager
{

    private const double OffscreenAnimationPadding = 16.0;
    private long _trayRaiseBatchGeneration;

    // Single shared driver for batch tray animations: one clock and one
    // atomic DeferWindowPos commit per frame, so all windows slide in
    // lockstep instead of staggering per-window (the "wave" effect).
    private readonly WidgetTrayBatchAnimationDriver _trayBatchAnimationDriver = new(App.LogVerbose);

    /// <summary>
    /// Bring desktop widgets to the front of the normal Z-order from the tray.
    /// </summary>
    public async Task<bool?> RaiseWidgetsFromTrayAsync()
    {
        using var perfScope = PerformanceLogger.Measure("WidgetManager.RaiseWidgetsFromTray");
        // Desktop-fixed layer: tray/hotkey “raise” shows widgets at the desktop layer
        // (no temporary topmost session).
        App.LogVerbose("[TrayBatch] Raise redirected to desktop-pinned show");
        await SetAllWidgetsVisibleAsync(true);
        return false;
    }

    private async Task<IDesktopWidgetWindow?> PrepareWidgetForBatchShowAsync(
        WidgetConfig config,
        bool showRaisedWhileInitializing = false)
    {
        if (IsDeleted(config.Id))
        {
            App.LogVerbose($"[TrayBatch] Prepare skipped reason=deleted widget={FormatWidget(config)}");
            return null;
        }

        if (config.IsDisabled)
        {
            App.LogVerbose($"[TrayBatch] Prepare skipped reason=disabled widget={FormatWidget(config)}");
            return null;
        }

        if (config.WidgetKind != WidgetKind.File)
        {
            App.LogVerbose($"[TrayBatch] Prepare skipped reason=non-file widget={FormatWidget(config)}");
            if (IsDetachedContentHost(config.WidgetKind))
            {
                if (config.WidgetKind == WidgetKind.Music && !IsMusicWidgetEnabled())
                {
                    App.LogVerbose($"[TrayBatch] Prepare skipped reason=music-disabled widget={FormatWidget(config)}");
                    return null;
                }

                if (_contentWidgets.TryGetValue(config.Id, out var existingContent))
                {
                    App.LogVerbose($"[TrayBatch] Prepare useLoaded content widget={FormatWidget(config)} {FormatHostWindow(existingContent)}");
                    existingContent.RestoreBoundsForCurrentTopology();
                    if (!existingContent.Visible)
                    {
                        existingContent.PrepareTrayShowAnimation();
                    }

                    return existingContent;
                }

                App.LogVerbose($"[TrayBatch] Prepare createContent widget={FormatWidget(config)} raisedInit={showRaisedWhileInitializing}");
                return await CreateRegisteredWidgetFromConfigAsync(
                    config,
                    keepPreparedForAnimation: true,
                    showRaisedWhileInitializing: showRaisedWhileInitializing);
            }

            App.LogVerbose($"[TrayBatch] Prepare skipped reason=unsupported-kind widget={FormatWidget(config)}");
            return null;
        }

        if (_widgets.TryGetValue(config.Id, out var existing))
        {
            App.LogVerbose($"[TrayBatch] Prepare useLoaded widget={FormatWidget(config)} {FormatHostWindow(existing.Window)}");
            existing.Window.RestoreBoundsForCurrentTopology();
            if (!existing.Window.Visible)
            {
                existing.Window.PrepareTrayShowAnimation();
            }
            return existing.Window;
        }

        App.LogVerbose($"[TrayBatch] Prepare createFile widget={FormatWidget(config)} raisedInit={showRaisedWhileInitializing}");
        var window = await CreateRegisteredWidgetFromConfigAsync(
            config,
            keepPreparedForAnimation: true,
            showRaisedWhileInitializing: showRaisedWhileInitializing);
        return window;
    }

    private void PlayPreparedTrayShowAnimations(IReadOnlyList<IDesktopWidgetWindow> windows)
    {
        if (windows.Count == 0)
        {
            return;
        }

        App.LogVerbose($"[TrayBatch] Starting batch show for {windows.Count} widgets...");
        
        // ⭐ 统一驱动：同一时钟 + DeferWindowPos 原子批量提交，所有窗口锁步滑动
        var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        dispatcher.TryEnqueue(() =>
        {
            try
            {
                // Step 1: 在同一帧内完成所有偏移量设置
                ApplyTrayAnimationGroupOffset(windows);

                // Step 2: 收集所有窗口的共享动画条目（Scale 仍由各窗口
                // Composition 驱动；整窗透明度由 batch 的 Win32 alpha 驱动）
                var entries = new List<WidgetTrayBatchAnimationEntry>(windows.Count);
                foreach (var window in windows)
                {
                    try
                    {
                        var entry = window.BeginSharedTrayShowAnimation();
                        if (entry is not null)
                        {
                            entries.Add(entry);
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Log($"[WidgetManager] Failed to play widget show animation {window}: {ex}");
                    }
                }

                // Step 3: 单批启动；等待 1 帧让新显示的窗口先提交首帧表面
                var options = WidgetAnimationSettings.From(_settingsService.Settings);
                _trayBatchAnimationDriver.Start(
                    entries,
                    options.DurationMs,
                    _settingsService.Settings.WidgetAnimationEasingIntensity,
                    isShowing: true,
                    startDelayFrames: 1);
            }
            catch (Exception ex)
            {
                App.Log($"[TrayBatch] Error during batch animation: {ex}");
            }
        });
    }

    private void PrepareTrayShowAnimations(IReadOnlyList<IDesktopWidgetWindow> windows)
    {
        ApplyTrayAnimationGroupOffset(windows);
        foreach (var window in windows)
        {
            try
            {
                window.PrepareTrayShowAnimation();
            }
            catch (Exception ex)
            {
                App.Log($"[WidgetManager] Failed to prepare widget show animation {FormatHostWindow(window)}: {ex}");
            }
        }
    }

    private void PlayPreparedTrayHideAnimations(IReadOnlyList<IDesktopWidgetWindow> windows)
    {
        if (windows.Count == 0)
        {
            return;
        }

        App.LogVerbose($"[TrayBatch] Starting batch hide for {windows.Count} widgets...");
        
        // ⭐ 与批量显示相同：统一驱动 + DeferWindowPos 原子批量提交
        var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        dispatcher.TryEnqueue(() =>
        {
            try
            {
                // Step 1: 在同一帧内完成所有偏移量设置
                ApplyTrayAnimationGroupOffset(windows);

                // Step 2: 收集所有窗口的共享隐藏动画条目
                var entries = new List<WidgetTrayBatchAnimationEntry>(windows.Count);
                foreach (var window in windows)
                {
                    try
                    {
                        var entry = window.BeginSharedTrayHideAnimation();
                        if (entry is not null)
                        {
                            entries.Add(entry);
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Log($"[WidgetManager] Failed to play widget hide animation {FormatHostWindow(window)}: {ex}");
                    }
                }

                // Step 3: 单批启动，内容已渲染无需等待
                var options = WidgetAnimationSettings.From(_settingsService.Settings);
                _trayBatchAnimationDriver.Start(
                    entries,
                    options.DurationMs,
                    _settingsService.Settings.WidgetAnimationEasingIntensity,
                    isShowing: false,
                    startDelayFrames: 0);
            }
            catch (Exception ex)
            {
                App.Log($"[TrayBatch] Error during batch hide animation: {ex}");
            }
        });
    }

    private void ApplyTrayAnimationGroupOffset(IReadOnlyList<IDesktopWidgetWindow> windows)
    {
        if (windows.Count == 0)
        {
            return;
        }

        foreach (var window in windows)
        {
            window.SetTrayAnimationOffsetOverride(null, null);
        }

        var options = WidgetAnimationSettings.From(_settingsService.Settings);
        if (!options.UsesGroupOffset)
        {
            return;
        }

        string direction = options.Effect == SettingsService.WidgetAnimationEffectSlideFade
            ? options.SlideDirection
            : SettingsService.WidgetAnimationSlideDirectionNone;

        if (direction == SettingsService.WidgetAnimationSlideDirectionNone)
        {
            return;
        }

        foreach (var group in windows.GroupBy(GetAnimationWorkAreaKey))
        {
            var groupWindows = group.ToList();
            if (groupWindows.Count == 0)
            {
                continue;
            }

            var workArea = GetAnimationWorkArea(groupWindows[0]);
            // Use resting bounds: during prepare/play the HWNDs are physically
            // displaced offscreen, which would collapse the group offset to ~0
            // and leave windows parked at their final position when uncloaked.
            double groupLeft = groupWindows.Min(window => window.RestingAnimationBounds.Left);
            double groupTop = groupWindows.Min(window => window.RestingAnimationBounds.Top);
            double groupRight = groupWindows.Max(window => window.RestingAnimationBounds.Right);
            double groupBottom = groupWindows.Max(window => window.RestingAnimationBounds.Bottom);

            double offsetX = 0;
            double offsetY = 0;
            switch (direction)
            {
                case SettingsService.WidgetAnimationSlideDirectionLeft:
                    offsetX = -(groupRight - workArea.X + OffscreenAnimationPadding);
                    break;

                case SettingsService.WidgetAnimationSlideDirectionUp:
                    offsetY = -(groupBottom - workArea.Y + OffscreenAnimationPadding);
                    break;

                case SettingsService.WidgetAnimationSlideDirectionDown:
                    offsetY = workArea.Y + workArea.Height - groupTop + OffscreenAnimationPadding;
                    break;

                case SettingsService.WidgetAnimationSlideDirectionRight:
                default:
                    offsetX = workArea.X + workArea.Width - groupLeft + OffscreenAnimationPadding;
                    break;
            }

            foreach (var window in groupWindows)
            {
                window.SetTrayAnimationOffsetOverride(offsetX, offsetY);
            }
        }
    }

    private static string GetAnimationWorkAreaKey(IDesktopWidgetWindow window)
    {
        var workArea = GetAnimationWorkArea(window);
        return $"{workArea.X}:{workArea.Y}:{workArea.Width}:{workArea.Height}";
    }

    private static Windows.Graphics.RectInt32 GetAnimationWorkArea(IDesktopWidgetWindow window)
    {
        var point = new Windows.Graphics.PointInt32(
            (int)Math.Round(window.RestingAnimationBounds.Left),
            (int)Math.Round(window.RestingAnimationBounds.Top));
        var displayArea = DisplayArea.GetFromPoint(point, DisplayAreaFallback.Primary);
        return displayArea.WorkArea;
    }

    private void SaveBatchVisibilityState()
    {
        _settingsService.SaveDebounced(notifySubscribers: false);
    }

}
