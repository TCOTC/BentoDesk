using System.ComponentModel;
using BentoDesk.Controls;
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

public sealed partial class WidgetWindow : WidgetWindowBase, IDesktopWidgetWindow
{
    private const int MinWidth = (int)SettingsService.MinWidgetWidth;
    private const int MinHeight = (int)SettingsService.MinWidgetHeight;
    private const int ItemTransitionRestoreDelayMs = 240;
    private static readonly QuickLookPreviewService s_quickLookPreviewService = new();

    // ── Backward-compatible aliases for base class fields ──────
    // These allow the existing WidgetWindow code to reference inherited
    // protected fields by their original private names without a
    // massive find-and-replace across 5000+ lines.
    private SettingsService _settingsService => SettingsService;
    private LocalizationService _localizationService => _localizationSvc;
    private IntPtr _hWnd => HWnd;
    private AppWindow _appWindow => AppWindow;
    private WidgetWindowDiagnostics _diagnostics => Diagnostics;
    private WidgetTrayAnimationController _trayAnimation => TrayAnimation;

    // Mutable field aliases for inherited protected state
    private DesktopAcrylicController? _acrylicController { get => AcrylicController; set => AcrylicController = value; }
    private MicaController? _micaController { get => MicaController; set => MicaController = value; }
    private SystemBackdropConfiguration? _backdropConfiguration { get => BackdropConfiguration; set => BackdropConfiguration = value; }
    private ICompositionSupportsSystemBackdrop? _backdropTarget { get => BackdropTarget; set => BackdropTarget = value; }
    private bool _isDragging { get => IsDragging; set => IsDragging = value; }
    private bool _hasMovedTitleBarDrag { get => HasMovedTitleBarDrag; set => HasMovedTitleBarDrag = value; }
    private bool _isResizing { get => IsResizing; set => IsResizing = value; }
    private bool _isApplyingBounds { get => IsApplyingBounds; set => IsApplyingBounds = value; }
    private string _resizeDirection { get => ResizeDirection; set => ResizeDirection = value; }
    private Win32Helper.POINT _initialCursorPt { get => InitialCursorPt; set => InitialCursorPt = value; }
    private Windows.Graphics.PointInt32 _initialWindowPos { get => InitialWindowPos; set => InitialWindowPos = value; }
    private Windows.Graphics.SizeInt32 _initialWindowSize { get => InitialWindowSize; set => InitialWindowSize = value; }
    private FrameworkElement? _dragCaptureElement { get => DragCaptureElement; set => DragCaptureElement = value; }

    private readonly LocalizationService _localizationSvc;
    private readonly WidgetContentDescriptor _chromeDescriptor;
    private readonly WidgetChromeModeResolver _chromeModeResolver;

    private Storyboard? _showButtonsStoryboard;
    private Storyboard? _hideButtonsStoryboard;
    private bool _isPointerOverRoot;
    private DispatcherQueueTimer? _statusToastTimer;
    private bool _emptyStateUpdateQueued;
    private bool _deletePending;
    private string[] _cutClipboardPaths = [];
    private readonly HashSet<Border> _interactiveSurfaces = [];
    private DateTime _lastTitleBarClickTimeUtc;
    private Win32Helper.POINT _lastTitleBarClickPoint;
    private bool _hasPendingTitleBarClick;
    private bool _isAtDesktopLayer { get => IsAtDesktopLayer; set => IsAtDesktopLayer = value; }
    private bool _keepRaisedUntilDeactivate { get => SuppressIdleRestore; set => SuppressIdleRestore = value; }
    private bool _restoreDesktopLayerWhenIdle { get => RestoreDesktopLayerWhenIdle; set => RestoreDesktopLayerWhenIdle = value; }
    private bool _isHideAnimationRunning { get => IsHideAnimationRunning; set => IsHideAnimationRunning = value; }
    private bool _isMigrationBusy;
    private long _backdropRefreshGeneration { get => BackdropRefreshGeneration; set => BackdropRefreshGeneration = value; }
    private bool _areItemTransitionsSuppressed;
    private DispatcherQueueTimer? _autoRestoreTimer;
    private DispatcherQueueTimer? _topMostSafetyTimer { get => TopMostSafetyTimer; set => TopMostSafetyTimer = value; }
    private WidgetDisplayChangeWatcher? _displayChangeWatcher { get => DisplayChangeWatcher; set => DisplayChangeWatcher = value; }
    private TransitionCollection? _savedGridItemTransitions;
    private TransitionCollection? _savedListItemTransitions;

    private Border BackgroundPlate => FileWidgetShell.BackgroundSurface;
    private Border HeaderDivider => FileWidgetShell.Divider;

    public WidgetViewModel ViewModel { get; }

    public IntPtr WindowHandle => HWnd;

    public WidgetWindowIdentity Identity => Diagnostics.Identity;

    public override WidgetConfig Config => ViewModel.Config;

    public Windows.Foundation.Rect AnimationBounds => GetCurrentAnimationBounds();
        public Windows.Foundation.Rect RestingAnimationBounds => _trayAnimation.GetRestingAnimationBounds();

    // ── WidgetWindowBase abstract overrides ────────────────────
    protected override double WidgetOpacity => ViewModel.WidgetOpacity;
    protected override FrameworkElement RootElement => RootGrid;
    protected override BentoDesk.Controls.WidgetShell WidgetShellControl => FileWidgetShell;
    protected override string LogPrefix => "Widget";
    protected override bool IsSizeLocked => ViewModel.IsSizeLocked;
    protected override bool IsPositionLocked => ViewModel.IsPositionLocked;
    protected override BentoDesk.Controls.WidgetCompactPresentation CreateCompactPresentation()
    {
        // Unused for file widgets (TitleBarOnly). Music builds its own presentation.
        return new BentoDesk.Controls.WidgetCompactPresentation(
            ViewModel.Name,
            string.Empty,
            ViewModel.IconGlyph,
            string.Empty);
    }

    protected override void OnElevated()
    {
        RootGrid.Focus(FocusState.Programmatic);
    }

    protected override bool HasBlockingFlyoutOpen()
    {
        return TitleEditBox.Visibility == Visibility.Visible ||
               _deletePending ||
               _isDeleteWidgetFlyoutOpen ||
               _isInlineFlyoutOpen;
    }

    protected override void ConfigureWindowExtra()
    {
        Win32Helper.AllowShellDragDropMessages(HWnd);
        InstallFileDropSubclass();
    }

    protected override void OnRootElementLoaded()
    {
        var parent = VisualTreeHelper.GetParent(RootGrid) as FrameworkElement;
        while (parent is not null)
        {
            if (parent is Control control)
            {
                control.Background = new SolidColorBrush(Colors.Transparent);
            }
            else if (parent is Panel panel)
            {
                panel.Background = new SolidColorBrush(Colors.Transparent);
            }
            else if (parent is Border border)
            {
                border.Background = new SolidColorBrush(Colors.Transparent);
            }
            else if (parent is ContentPresenter presenter)
            {
                presenter.Background = new SolidColorBrush(Colors.Transparent);
            }

            parent = VisualTreeHelper.GetParent(parent) as FrameworkElement;
        }
        RootGrid.Focus(FocusState.Programmatic);
    }

    private bool _isVisibleOnDesktop;
    private bool _isClosing { get => IsClosing; set => IsClosing = value; }
    private DateTime _lastElevateForInteractionUtc { get => LastElevateForInteractionUtc; set => LastElevateForInteractionUtc = value; }
    public new bool Visible
    {
        get => _isVisibleOnDesktop;
        private set => _isVisibleOnDesktop = value;
    }

    public new void Activate()
    {
        base.Activate();
        Win32Helper.ShowWindow(_hWnd, Win32Helper.SW_SHOWNOACTIVATE);
        Visible = true;
        ViewModel.Config.IsVisible = true;
        _settingsService.SaveDebounced();
        QueueBackdropRefresh();
    }

    public WidgetWindow(WidgetViewModel viewModel, SettingsService settingsService, LocalizationService? localizationService = null)
    {
        ViewModel = viewModel;
        SettingsService = settingsService;
        _localizationSvc = localizationService ?? new LocalizationService(settingsService);
        _fileDropSubclassProc = FileDropSubclassProc;
        _chromeDescriptor = new WidgetContentFactory(_localizationSvc).GetDescriptor(WidgetKind.File);
        _chromeModeResolver = new WidgetChromeModeResolver();
        InitializeComponent();
        
        // ✅ Set localized title
        this.Title = _localizationSvc.T("Window.Widget.Title");
        
        RootGrid.DataContext = ViewModel;

        ApplyLocalizedText();
        FileWidgetShell.SetDividerMargin(new Thickness(12, 0, 12, 0));
        FileWidgetShell.SetCollapseChromeMode(WidgetCollapseChromeMode.TitleBarOnly);
        FileWidgetShell.SetExternalCompactInteractionHosts(
            FileTitleMoveHandleHost,
            FileTitleExpansionHost);

        HWnd = WindowNative.GetWindowHandle(this);
        Diagnostics = new WidgetWindowDiagnostics("File", ViewModel.Config, () => HWnd);
        
        // ⭐ 使用智能适配器创建动画控制器
        var adapter = WidgetWindowBase.SmartAnimationAdapter;
        TrayAnimation = adapter?.CreateAnimationController(
            AppWindow,
            RootGrid,
            DispatcherQueue,
            HWnd,
            GetCurrentAnimationBounds,
            LogTrayWindow) ?? new WidgetTrayAnimationController(
                AppWindow,
                RootGrid,
                DispatcherQueue,
                HWnd,
                GetCurrentAnimationBounds,
                LogTrayWindow);

        ConfigureWindow();
        SetupEventHandlers();

        ViewModel.Items.CollectionChanged += ViewModel_ItemsCollectionChanged;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        UpdateEmptyState();
        ApplyTitleBarLayout();
    }

    private void ConfigureWindow()
    {
        ConfigureWindowCore();
    }

    private void SetupEventHandlers()
    {
        _settingsService.SettingsChanged += OnSettingsChanged;
        _localizationService.LanguageChanged += OnLanguageChanged;

        Activated += WidgetWindow_Activated;

        AppWindow.Changed += OnAppWindowChanged;
        _displayChangeWatcher = new WidgetDisplayChangeWatcher(_hWnd, DispatcherQueue, RestoreBoundsAfterDisplayChange);

        foreach (var child in ResizeGrid.Children)
        {
            if (child is FrameworkElement element && element.Tag is string tag && !string.IsNullOrEmpty(tag))
            {
                element.PointerMoved += ResizeBorder_PointerMoved;
                element.PointerReleased += ResizeBorder_PointerReleased;
                element.PointerCaptureLost += ResizeBorder_PointerCaptureLost;
            }
        }

        Closed += (_, _) =>
        {
            _isClosing = true;
            Visible = false;
            CleanupStackTransitions();
            CleanupWidgetCollapse();
            WidgetLayerService.Release(_hWnd);
            _settingsService.SettingsChanged -= OnSettingsChanged;
            _localizationService.LanguageChanged -= OnLanguageChanged;
            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            ViewModel.Items.CollectionChanged -= ViewModel_ItemsCollectionChanged;
            AppWindow.Changed -= OnAppWindowChanged;
            _displayChangeWatcher?.Dispose();
            _displayChangeWatcher = null;
            _autoRestoreTimer?.Stop();
            _autoRestoreTimer = null;
            StopBackdropRefreshTimer();
            _topMostSafetyTimer?.Stop();
            _topMostSafetyTimer = null;
            try { RemoveFileDropSubclass(); } catch (Exception ex) { App.Log($"[WidgetWindow] RemoveFileDropSubclass failed during close: {ex.Message}"); }
            try { _trayAnimation.Stop(); } catch { }
            try { _trayAnimation.RevealWindowForTrayShow(); } catch { }
            try { RestoreItemContainerTransitions(); } catch { }
            try { DisposeAcrylicController(); } catch { }
            try { DisposeMicaController(); } catch { }
            try { StopDragHighlight(); } catch { }
            try { TrackWindowClosedForDiagnostics(); } catch { }

            foreach (var child in ResizeGrid.Children)
            {
                if (child is FrameworkElement element && element.Tag is string tag && !string.IsNullOrEmpty(tag))
                {
                    element.PointerMoved -= ResizeBorder_PointerMoved;
                    element.PointerReleased -= ResizeBorder_PointerReleased;
                    element.PointerCaptureLost -= ResizeBorder_PointerCaptureLost;
                }
            }
        };
    }

    private void OnLanguageChanged()
    {
        ApplyLocalizedText();
    }

    private void ApplyLocalizedText()
    {
        TitleEditBox.PlaceholderText = _localizationService.T("Widget.TitlePlaceholder");
        ToolTipService.SetToolTip(LockButton, _localizationService.T("Widget.Lock"));
        ToolTipService.SetToolTip(FileWidgetShell.LockActionButton, _localizationService.T("Widget.Lock"));
        UpdateCollapseWidgetButtonVisual();
        MigrationTitleText.Text = _localizationService.T("Widget.Migration.Title");
        MigrationDescriptionText.Text = _localizationService.T("Widget.Migration.Description");
    }

    protected override void OnCollapseBehaviorChanged(WidgetCollapseBehavior behavior)
    {
        // 悬停展开不需要折叠按钮；仅点击展开模式显示。
        CollapseWidgetButton.Visibility = behavior == WidgetCollapseBehavior.Click
            ? Visibility.Visible
            : Visibility.Collapsed;
        UpdateCollapseWidgetButtonVisual();
    }

    protected override void OnCompactInteractionChromeUpdated()
    {
        UpdateCollapseWidgetButtonVisual();
    }

    private void UpdateCollapseWidgetButtonVisual()
    {
        // Use the intended collapse target, not bounds-animation flags —
        // IsWidgetCollapsedBoundsActive stays true through expand animation and
        // would leave the chevron stuck at 180°.
        bool collapsed = IsWidgetCollapsed;
        CollapseWidgetButtonIconRotate.Angle = collapsed ? 180 : 0;
        ToolTipService.SetToolTip(
            CollapseWidgetButton,
            _localizationService.T(collapsed ? "Widget.Compact.Expand" : "Widget.Compact.Collapse"));
    }

    private void CollapseWidgetButton_Click(object sender, RoutedEventArgs e)
    {
        if (IsWidgetCollapsed)
        {
            ExpandWidgetFromHost();
            return;
        }

        CollapseWidgetFromHost();
    }

    public void ApplyAppearancePreview()
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(ApplyAppearancePreview);
            return;
        }

        ViewModel.ApplyAppearancePreview();
        ApplyBackdropPreference();
        QueueBackdropRefresh();
    }

    public void SetMigrationBusy(bool isBusy)
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(() => SetMigrationBusy(isBusy));
            return;
        }

        _isMigrationBusy = isBusy;
        if (isBusy)
        {
            MigrationTitleText.Text = _localizationService.T("Widget.Migration.Title");
            MigrationDescriptionText.Text = _localizationService.T("Widget.Migration.Description");
        }
        MigrationOverlay.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
        MigrationProgressRing.IsActive = isBusy;
        ResizeGrid.IsHitTestVisible = !isBusy;
        RootGrid.Focus(FocusState.Programmatic);
    }

    /// <summary>
    /// Shows the overlay with "importing" text during drag-drop file transfers.
    /// Separate from SetMigrationBusy to use different localized text.
    /// </summary>
    public void SetImportBusy(bool isBusy)
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(() => SetImportBusy(isBusy));
            return;
        }

        _isMigrationBusy = isBusy;
        if (isBusy)
        {
            MigrationTitleText.Text = _localizationService.T("Widget.Import.Title");
            MigrationDescriptionText.Text = _localizationService.T("Widget.Import.Description");
            StopDragHighlight();
        }
        MigrationOverlay.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
        MigrationProgressRing.IsActive = isBusy;
        ResizeGrid.IsHitTestVisible = !isBusy;
    }

    private void WidgetWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        DispatcherQueue.TryEnqueue(() => ApplyBackdropPreference());

        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            // Desktop-fixed layer stays pinned; no TopMost fall-back restore.
            return;
        }

        // PointerPressed already Front'd; only schedule WinUI async settle.
        if (Visible)
        {
            LayerScheduleFrontSettle("file-activated");
        }
    }

    private void QueueRestoreDesktopLayerIfForegroundLeavesBentoDesk()
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            await Task.Delay(80);

            if (!Visible)
            {
                return;
            }

            IntPtr foregroundWindow = Win32Helper.GetForegroundWindow();
            if (App.Current.IsBentoDeskWindow(foregroundWindow))
            {
                _restoreDesktopLayerWhenIdle = false;
                return;
            }

            LayerOnRestore(force: true, reason: "file-deactivated");
        });
    }

    public void RestoreDesktopLayerFromManager()
    {
        LayerOnRestore(force: true, reason: "manager-restore");
    }

    public void ForceRestoreDesktopLayerFromManager()
    {
        App.LogVerbose($"[ZOrder] Widget ForceRestore hwnd=0x{_hWnd.ToInt64():X} visible={Visible} atDesktop={_isAtDesktopLayer}");
        ForceCancelTransientState();
        LayerOnRestore(force: true, reason: "manager-force-restore");
    }

    private void ForceCancelTransientState()
    {
        _restoreDesktopLayerWhenIdle = true;
        _keepRaisedUntilDeactivate = false;
        _isDeleteWidgetFlyoutOpen = false;
        _isInlineFlyoutOpen = false;
    }

    protected override void UpdateConfigBoundsFromPhysical(int x, int y, int width, int height, bool persist)
    {
        if (IsCompactBoundsStateActive)
        {
            if (persist)
            {
                SettingsService.UpdateWidget(ViewModel.Config, notifySubscribers: false);
                SettingsService.SaveDebounced(notifySubscribers: false);
            }
            return;
        }

        var bounds = new Windows.Graphics.RectInt32(x, y, width, height);
        // Use center point for consistent monitor determination across drag/resize.
        var center = new Windows.Graphics.PointInt32(
            x + Math.Max(1, width) / 2,
            y + Math.Max(1, height) / 2);
        var workArea = DisplayArea.GetFromPoint(center, DisplayAreaFallback.Nearest).WorkArea;
        WidgetPositioningService.UpdateConfigFromPhysicalBounds(ViewModel.Config, bounds, workArea);
        if (persist)
        {
            SettingsService.UpdateWidget(ViewModel.Config, notifySubscribers: false);
        }
    }

    private void UpdateEmptyState()
    {
        EmptyState.Visibility = ViewModel.Items.Count == 0 && !ViewModel.IsLoading
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ViewModel_ItemsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        QueueEmptyStateUpdate();
        RefreshCompactPresentation();
    }

    private void QueueEmptyStateUpdate()
    {
        if (_emptyStateUpdateQueued)
        {
            return;
        }

        _emptyStateUpdateQueued = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            _emptyStateUpdateQueued = false;
            UpdateEmptyState();
        });
    }

    private bool ShouldStartTitleDrag(object? originalSource)
    {
        if (originalSource is not DependencyObject source)
        {
            return true;
        }

        // 折叠态标题栏仍可拖动：悬停展开靠停留触发，与按住拖动不抢命中。
        // 按钮与重命名输入框除外。
        return !IsWithin(source, LockButton) &&
               !IsWithin(source, CollapseWidgetButton) &&
               !IsWithin(source, FileWidgetShell.LockActionButton) &&
               !IsWithin(source, FileWidgetShell.CollapseActionButton) &&
               !IsWithin(source, TitleEditBox);
    }

    private void FileTitleMoveHandleHost_PointerEntered(object sender, PointerRoutedEventArgs e) =>
        FileWidgetShell.ReportCompactMoveHandleEntered();

    private void FileTitleMoveHandleHost_PointerExited(object sender, PointerRoutedEventArgs e) =>
        FileWidgetShell.ReportCompactMoveHandleExited();

    private void FileTitleExpansionHost_PointerEntered(object sender, PointerRoutedEventArgs e) =>
        FileWidgetShell.ReportCompactExpansionEntered();

    private void FileTitleExpansionHost_PointerExited(object sender, PointerRoutedEventArgs e) =>
        FileWidgetShell.ReportCompactExpansionExited();

    private bool CanStartRenameFromTitleArea(object? originalSource)
    {
        if (originalSource is not DependencyObject source)
        {
            return false;
        }

        return ShouldOpenTitleBarFlyout(source) && IsWithin(source, TitleBarGrid);
    }

    private bool IsTitleBarDoubleClick(Win32Helper.POINT currentPoint)
    {
        if (!_hasPendingTitleBarClick)
        {
            return false;
        }

        if ((DateTime.UtcNow - _lastTitleBarClickTimeUtc).TotalMilliseconds > 420)
        {
            return false;
        }

        int deltaX = currentPoint.X - _lastTitleBarClickPoint.X;
        int deltaY = currentPoint.Y - _lastTitleBarClickPoint.Y;
        return ((deltaX * deltaX) + (deltaY * deltaY)) <= 36;
    }

    private void TrackTitleBarClick(object? originalSource, Win32Helper.POINT currentPoint)
    {
        if (!CanStartRenameFromTitleArea(originalSource))
        {
            _hasPendingTitleBarClick = false;
            return;
        }

        _lastTitleBarClickTimeUtc = DateTime.UtcNow;
        _lastTitleBarClickPoint = currentPoint;
        _hasPendingTitleBarClick = true;
    }

    private bool ShouldOpenTitleBarFlyout(object? originalSource)
    {
        if (originalSource is not DependencyObject source)
        {
            return true;
        }

        return !IsWithin(source, LockButton) &&
               !IsWithin(source, CollapseWidgetButton) &&
               !IsWithin(source, FileWidgetShell.LockActionButton) &&
               !IsWithin(source, FileWidgetShell.CollapseActionButton) &&
               !IsWithin(source, TitleEditBox) &&
               !HasAncestorOfType<TextBox>(source);
    }

    private static bool IsWithin(DependencyObject source, DependencyObject target)
    {
        var current = source;
        while (current is not null)
        {
            if (ReferenceEquals(current, target))
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private bool IsCursorOverThisWindow()
    {
        if (!Win32Helper.GetCursorPos(out var cursor) ||
            !Win32Helper.GetWindowRect(_hWnd, out var rect))
        {
            return false;
        }

        return cursor.X >= rect.Left &&
               cursor.X <= rect.Right &&
               cursor.Y >= rect.Top &&
               cursor.Y <= rect.Bottom;
    }

    private bool IsCursorOnDesktop()
    {
        if (!Win32Helper.GetCursorPos(out var cursor))
        {
            return false;
        }

        try
        {
            var display = DisplayArea.GetFromPoint(
                new Windows.Graphics.PointInt32(cursor.X, cursor.Y),
                DisplayAreaFallback.Nearest);
            var workArea = display.WorkArea;
            return cursor.X >= workArea.X &&
                   cursor.X <= workArea.X + workArea.Width &&
                   cursor.Y >= workArea.Y &&
                   cursor.Y <= workArea.Y + workArea.Height;
        }
        catch
        {
            return true;
        }
    }

    private void EnsureStoryboards()
    {
        if (_showButtonsStoryboard is not null)
        {
            return;
        }

        _showButtonsStoryboard = new Storyboard();

        var showRightOpacity = new DoubleAnimation
        {
            To = 1.0,
            Duration = new Duration(TimeSpan.FromMilliseconds(180)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(showRightOpacity, RightActionButtons);
        Storyboard.SetTargetProperty(showRightOpacity, "Opacity");
        _showButtonsStoryboard.Children.Add(showRightOpacity);

        _hideButtonsStoryboard = new Storyboard();

        var hideRightOpacity = new DoubleAnimation
        {
            To = 0.0,
            Duration = new Duration(TimeSpan.FromMilliseconds(150)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        Storyboard.SetTarget(hideRightOpacity, RightActionButtons);
        Storyboard.SetTargetProperty(hideRightOpacity, "Opacity");
        _hideButtonsStoryboard.Children.Add(hideRightOpacity);
    }

    private void RootGrid_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _isPointerOverRoot = true;
        var chromeMode = _chromeModeResolver.Resolve(ViewModel.Config, _chromeDescriptor);
        if (chromeMode is WidgetChromeMode.Overlay or WidgetChromeMode.Hidden)
        {
            return;
        }

        SetTitleActionButtonsVisible(visible: true, animate: true);
    }

    private void RootGrid_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        _isPointerOverRoot = false;
        var chromeMode = _chromeModeResolver.Resolve(ViewModel.Config, _chromeDescriptor);
        if (chromeMode is WidgetChromeMode.Overlay or WidgetChromeMode.Hidden)
        {
            SetTitleActionButtonsVisible(visible: false, animate: false);
            return;
        }

        SetTitleActionButtonsVisible(visible: false, animate: true);
    }

    private static Windows.UI.Color ApplySurfaceOpacity(Windows.UI.Color color, double opacity)
    {
        byte alpha = (byte)Math.Clamp(Math.Round(color.A * opacity), 0, 255);
        return ColorHelper.FromArgb(alpha, color.R, color.G, color.B);
    }

    private static Windows.UI.Color BuildAccentSurfaceColor(
        bool isDark,
        Windows.UI.Color accentColor,
        Windows.UI.Color baseColor,
        double accentMix,
        double overlayMix)
    {
        var tintedColor = BlendColors(baseColor, accentColor, accentMix);
        var overlayColor = isDark
            ? ColorHelper.FromArgb(0xFF, 0x12, 0x14, 0x18)
            : ColorHelper.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);

        return BlendColors(tintedColor, overlayColor, overlayMix);
    }

    private static Windows.UI.Color BlendColors(Windows.UI.Color fromColor, Windows.UI.Color toColor, double amount)
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

    private static Windows.UI.Color WithAlpha(Windows.UI.Color color, byte alpha)
    {
        return ColorHelper.FromArgb(alpha, color.R, color.G, color.B);
    }

    private static bool CanUseRequestedOperation(DataPackageOperation requestedOperation, DataPackageOperation operation)
    {
        return requestedOperation == DataPackageOperation.None ||
               SupportsOperation(requestedOperation, operation);
    }

}
