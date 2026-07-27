using BentoDesk.Helpers;
using System.Runtime.InteropServices;

namespace BentoDesk.Services;

/// <summary>
/// Centralizes desktop widget Z-order: widgets stay owned by
/// <c>SHELLDLL_DefView</c> (same technique as XZDesktop's live XZDesktopWnd),
/// so they sit above desktop icons and below normal application windows.
/// </summary>
public static class WidgetLayerService
{
    private const uint SpawnWorkerWMessage = 0x052C;
    private static readonly UIntPtr ZOrderGuardSubclassId = new(0xBE70_0001);

    // Keep the delegate rooted for the process lifetime (SetWindowSubclass requirement).
    private static readonly Win32Helper.SubclassProc s_zOrderGuardProc = ZOrderGuardSubclassProc;

    private static readonly object s_desktopLayerLock = new();
    private static readonly Dictionary<IntPtr, DesktopLayerAttachment> s_desktopLayerAttachments = [];
    private static readonly HashSet<IntPtr> s_zOrderGuardInstalled = [];
    private static IntPtr s_cachedDesktopIconView;

    // Front-peer generation: delayed reassert from an older click must not
    // steal z-order back when the user has already clicked another widget.
    private static long s_frontPeerGeneration;
    private static IntPtr s_frontPeerHwnd;

    public static void MoveToDesktopBottom(IntPtr windowHandle)
    {
        bool attached = TryAttachToDesktopIconLayer(windowHandle);
        if (!attached)
        {
            FallbackToDesktopBottom(windowHandle);
            App.Log(
                $"[WidgetVis] MoveToDesktopBottom hwnd=0x{windowHandle.ToInt64():X} " +
                $"attached=False fallback=True vis={Win32Helper.IsWindowVisible(windowHandle)}");
            LogPeersSnapshotIfAnomalous("MoveToDesktopBottom-fallback", windowHandle);
            return;
        }

        // Quiet pin only — do not BringAbovePeerWidgets. Deactivate/restore used
        // to steal front from the newly clicked peer and reshuffle the whole
        // group, which flashes siblings and can drop one below Progman.
        EnsureInDesktopBand(windowHandle);
        App.Log(
            $"[WidgetVis] MoveToDesktopBottom hwnd=0x{windowHandle.ToInt64():X} attached=True");
        LogPeersSnapshotIfAnomalous("MoveToDesktopBottom", windowHandle);
    }

    public static IntPtr ClearTopMostPreservingForeground(IntPtr windowHandle)
    {
        MoveToDesktopBottom(windowHandle);
        return Win32Helper.GetForegroundWindow();
    }

    public static void ClearTopMost(IntPtr windowHandle)
    {
        MoveToDesktopBottom(windowHandle);
    }

    public static void HoldTemporaryTopMost(IntPtr windowHandle)
    {
        // Desktop-fixed layer: interaction must not lift widgets above other apps.
        MoveToDesktopBottom(windowHandle);
    }

    public static void BringToFront(IntPtr windowHandle)
    {
        MoveToDesktopBottom(windowHandle);
    }

    /// <summary>
    /// Raises one widget above its peers without leaving the desktop band
    /// (must stay below normal apps). Never uses <c>HWND_TOP</c> — that lifts
    /// WinUI windows above Chrome/other apps even when DefView-owned.
    /// </summary>
    public static void BringAbovePeerWidgets(IntPtr windowHandle)
    {
        NoteFrontPeer(windowHandle);
        RaiseAbovePeersCore(windowHandle, sinkIfAboveDesktopBand: true);
    }

    /// <summary>
    /// True when walking upward from <paramref name="windowHandle"/> hits Progman
    /// (the window currently sits below the desktop shell band).
    /// </summary>
    private static bool IsBelowProgman(IntPtr windowHandle, IntPtr progman)
    {
        if (progman == IntPtr.Zero || windowHandle == IntPtr.Zero)
        {
            return false;
        }

        IntPtr current = windowHandle;
        for (int i = 0; i < 128; i++)
        {
            current = Win32Helper.GetWindow(current, Win32Helper.GW_HWNDPREV);
            if (current == IntPtr.Zero)
            {
                return false;
            }

            if (current == progman)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Re-asserts DefView ownership and peer fronting after click/activation.
    /// </summary>
    public static void ReassertDesktopLayer(IntPtr windowHandle)
    {
        BringAbovePeerWidgets(windowHandle);
    }

    /// <summary>
    /// Schedules a single delayed reassert that is cancelled automatically when
    /// another widget becomes the front peer (prevents overlap-click flicker).
    /// </summary>
    public static void ScheduleReassertDesktopLayer(IntPtr windowHandle)
    {
        long generation = NoteFrontPeer(windowHandle);
        Microsoft.UI.Dispatching.DispatcherQueue? dispatcher = App.UiDispatcherQueue;
        if (dispatcher is null)
        {
            return;
        }

        _ = dispatcher.TryEnqueue(async () =>
        {
            // One settle pass after WinUI's async z-order; ignore if stale.
            await Task.Delay(32);
            if (!IsCurrentFrontPeer(windowHandle, generation))
            {
                return;
            }

            RaiseAbovePeersCore(windowHandle, sinkIfAboveDesktopBand: true);
        });
    }

    private static long NoteFrontPeer(IntPtr windowHandle)
    {
        lock (s_desktopLayerLock)
        {
            s_frontPeerHwnd = windowHandle;
            s_frontPeerGeneration++;
            return s_frontPeerGeneration;
        }
    }

    private static bool IsCurrentFrontPeer(IntPtr windowHandle, long generation)
    {
        lock (s_desktopLayerLock)
        {
            return generation == s_frontPeerGeneration && s_frontPeerHwnd == windowHandle;
        }
    }

    private static void RaiseAbovePeersCore(IntPtr windowHandle, bool sinkIfAboveDesktopBand)
    {
        if (!TryResolveDesktopIconView(out IntPtr defView))
        {
            App.Log($"[WidgetVis] RaiseAbovePeers skipped hwnd=0x{windowHandle.ToInt64():X} reason=no-defview");
            return;
        }

        if (!ApplyDesktopOwner(windowHandle, defView))
        {
            App.Log($"[WidgetVis] RaiseAbovePeers skipped hwnd=0x{windowHandle.ToInt64():X} reason=owner-attach-failed");
            return;
        }

        IntPtr[] peers;
        lock (s_desktopLayerLock)
        {
            peers = s_desktopLayerAttachments.Keys
                .Where(peer => peer != windowHandle && Win32Helper.IsWindow(peer))
                .ToArray();
        }

        bool needsSink = sinkIfAboveDesktopBand && IsAboveDesktopBand(windowHandle);
        bool isFront = IsFrontAmongPeers(windowHandle, peers);
        IntPtr progman = Win32Helper.FindWindow("Progman", null);
        bool selfOrphaned = IsBelowProgman(windowHandle, progman);
        bool peerOrphaned = peers.Any(peer => IsBelowProgman(peer, progman));

        // Same-box re-clicks: already front and in the desktop band — do not
        // touch Z-order (any SetWindowPos on DefView-owned peers can flash).
        if (!needsSink && isFront && !selfOrphaned && !peerOrphaned)
        {
            return;
        }

        App.Log(
            $"[WidgetVis] RaiseAbovePeers raise-self hwnd=0x{windowHandle.ToInt64():X} " +
            $"peers={peers.Length} needsSink={needsSink} isFront={isFront} " +
            $"selfOrphan={selfOrphaned} peerOrphan={peerOrphaned}");
        if (selfOrphaned || peerOrphaned)
        {
            LogPeersSnapshot("RaiseAbovePeers-before-rescue", windowHandle);
        }

        // Only move the clicked HWND into the desktop-band front seat. Full peer
        // DeferWindowPos restacks were creating a rotating below-Progman orphan
        // (other boxes flash on every click).
        RaiseSelfInDesktopBand(windowHandle);
        _ = ApplyDesktopOwner(windowHandle, defView, clearTopMost: false);

        progman = Win32Helper.FindWindow("Progman", null);
        peerOrphaned = peers.Any(peer => IsBelowProgman(peer, progman));
        if (peerOrphaned)
        {
            RescueOrphanedPeersQuiet(windowHandle, peers);
        }

        if (!IsFrontAmongPeers(windowHandle, peers))
        {
            App.Log($"[WidgetVis] RaiseAbovePeers retry self hwnd=0x{windowHandle.ToInt64():X}");
            RaiseSelfInDesktopBand(windowHandle);
        }

        LogPeersSnapshotIfAnomalous("RaiseAbovePeers-after", windowHandle);
    }

    /// <summary>
    /// Pins a single HWND into the desktop band without restacking siblings.
    /// </summary>
    private static void EnsureInDesktopBand(IntPtr windowHandle)
    {
        IntPtr progman = Win32Helper.FindWindow("Progman", null);
        if (IsAboveDesktopBand(windowHandle) || IsBelowProgman(windowHandle, progman))
        {
            RaiseSelfInDesktopBand(windowHandle);
        }
    }

    /// <summary>
    /// Moves only <paramref name="windowHandle"/> to the top of the desktop band
    /// (just under the lowest normal app above Progman).
    /// </summary>
    private static void RaiseSelfInDesktopBand(IntPtr windowHandle)
    {
        Win32Helper.ClearWindowTopMost(windowHandle);

        const uint flags =
            Win32Helper.SWP_NOMOVE |
            Win32Helper.SWP_NOSIZE |
            Win32Helper.SWP_NOACTIVATE;

        if (TryGetDesktopBandInsertAfter(windowHandle, out IntPtr insertAfter) &&
            insertAfter != IntPtr.Zero)
        {
            Win32Helper.SetWindowPos(windowHandle, insertAfter, 0, 0, 0, 0, flags);
            return;
        }

        PlaceJustAboveProgman(windowHandle);
    }

    private static string GetClassNameOrEmpty(IntPtr hWnd)
    {
        var className = new System.Text.StringBuilder(64);
        int length = Win32Helper.GetClassName(hWnd, className, className.Capacity);
        return length > 0 ? className.ToString() : string.Empty;
    }

    private static bool IsFrontAmongPeers(IntPtr windowHandle, IntPtr[] peers)
    {
        if (peers.Length == 0)
        {
            return true;
        }

        HashSet<IntPtr> peerSet = [.. peers];
        IntPtr current = windowHandle;
        for (int i = 0; i < 64; i++)
        {
            current = Win32Helper.GetWindow(current, Win32Helper.GW_HWNDPREV);
            if (current == IntPtr.Zero)
            {
                return true;
            }

            if (peerSet.Contains(current))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Pulls peers that fell below Progman back under <paramref name="front"/>
    /// without calling <see cref="PlaceJustAboveProgman"/> on them (that reorders
    /// the DefView ownership group and drops a different sibling).
    /// </summary>
    private static void RescueOrphanedPeersQuiet(IntPtr front, IntPtr[] peers)
    {
        if (peers.Length == 0)
        {
            return;
        }

        IntPtr progman = Win32Helper.FindWindow("Progman", null);
        if (progman == IntPtr.Zero)
        {
            return;
        }

        if (IsBelowProgman(front, progman))
        {
            RaiseSelfInDesktopBand(front);
        }

        const uint flags =
            Win32Helper.SWP_NOMOVE |
            Win32Helper.SWP_NOSIZE |
            Win32Helper.SWP_NOACTIVATE;

        bool rescued = false;
        foreach (IntPtr peer in peers)
        {
            if (!IsBelowProgman(peer, progman))
            {
                continue;
            }

            rescued = true;
            Win32Helper.SetWindowPos(peer, front, 0, 0, 0, 0, flags);

            if (!IsBelowProgman(peer, progman))
            {
                continue;
            }

            // Still orphaned: tuck under any healthy sibling above Progman.
            IntPtr anchor = IntPtr.Zero;
            foreach (IntPtr candidate in peers)
            {
                if (candidate != peer && !IsBelowProgman(candidate, progman))
                {
                    anchor = candidate;
                    break;
                }
            }

            if (anchor == IntPtr.Zero)
            {
                anchor = front;
            }

            if (!IsBelowProgman(anchor, progman))
            {
                Win32Helper.SetWindowPos(peer, anchor, 0, 0, 0, 0, flags);
            }
        }

        if (!rescued)
        {
            return;
        }

        App.Log(
            $"[WidgetVis] RescueOrphanedPeersQuiet front=0x{front.ToInt64():X} peers={peers.Length}");
        RaiseSelfInDesktopBand(front);
    }

    private static bool IsDesktopShellClass(IntPtr hWnd)
    {
        var className = new System.Text.StringBuilder(64);
        int length = Win32Helper.GetClassName(hWnd, className, className.Capacity);
        if (length <= 0)
        {
            return false;
        }

        string name = className.ToString();
        return name is "Progman" or "WorkerW" or "SHELLDLL_DefView";
    }

    /// <summary>
    /// True when a normal app sits between this window and Progman (covering apps).
    /// </summary>
    private static bool IsAboveDesktopBand(IntPtr windowHandle)
    {
        IntPtr progman = Win32Helper.FindWindow("Progman", null);
        if (progman == IntPtr.Zero || !Win32Helper.IsWindow(progman))
        {
            return false;
        }

        IntPtr current = windowHandle;
        for (int i = 0; i < 64; i++)
        {
            current = Win32Helper.GetWindow(current, Win32Helper.GW_HWNDNEXT);
            if (current == IntPtr.Zero)
            {
                return false;
            }

            if (current == progman)
            {
                return false;
            }

            if (IsDesktopBandShellOrPeer(current))
            {
                continue;
            }

            // A foreign top-level window between us and Progman ⇒ covering apps.
            if (Win32Helper.IsWindowVisible(current))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsDesktopBandShellOrPeer(IntPtr hWnd)
    {
        lock (s_desktopLayerLock)
        {
            if (s_desktopLayerAttachments.ContainsKey(hWnd))
            {
                return true;
            }
        }

        var className = new System.Text.StringBuilder(64);
        int length = Win32Helper.GetClassName(hWnd, className, className.Capacity);
        if (length <= 0)
        {
            return false;
        }

        string name = className.ToString();
        return name is "Progman" or "WorkerW" or "SHELLDLL_DefView";
    }

    public static void ReleaseWindow(IntPtr windowHandle)
    {
        DetachFromDesktopIconLayerIfNeeded(windowHandle);
    }

    public static void InvalidateDesktopIconViewCache()
    {
        lock (s_desktopLayerLock)
        {
            s_cachedDesktopIconView = IntPtr.Zero;
        }
    }

    /// <summary>
    /// Finds the desktop icon ListView without sending Progman <c>0x052C</c>.
    /// Safe for hot paths such as mouse hooks.
    /// </summary>
    public static IntPtr FindDesktopIconListView()
    {
        IntPtr defView = FindExistingDesktopIconView();
        if (defView == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        return FindDesktopIconListViewChild(defView);
    }

    public static bool AreDesktopIconsVisible()
    {
        IntPtr listView = FindDesktopIconListView();
        return listView != IntPtr.Zero && Win32Helper.IsWindowVisible(listView);
    }

    public static bool SetDesktopIconsVisible(bool visible)
    {
        IntPtr listView = FindDesktopIconListView();
        if (listView == IntPtr.Zero || !Win32Helper.IsWindow(listView))
        {
            App.Log("[WidgetLayer] SetDesktopIconsVisible skipped: desktop list view not found");
            return false;
        }

        bool currentlyVisible = Win32Helper.IsWindowVisible(listView);
        if (currentlyVisible == visible)
        {
            return true;
        }

        // ShowWindow 的返回值表示“之前是否可见”，不能当作成功与否。
        _ = Win32Helper.ShowWindow(
            listView,
            visible ? Win32Helper.SW_SHOWNOACTIVATE : Win32Helper.SW_HIDE);
        bool nowVisible = Win32Helper.IsWindowVisible(listView);
        bool ok = nowVisible == visible;
        App.Log($"[WidgetLayer] SetDesktopIconsVisible visible={visible} ok={ok} hwnd=0x{listView.ToInt64():X}");
        return ok;
    }

    private static bool TryAttachToDesktopIconLayer(IntPtr windowHandle)
    {
        if (!TryResolveDesktopIconView(out IntPtr defView))
        {
            App.Log("[WidgetLayer] DesktopPinned attach skipped: desktop icon view not found");
            return false;
        }

        bool alreadyAttached;
        lock (s_desktopLayerLock)
        {
            alreadyAttached = s_desktopLayerAttachments.ContainsKey(windowHandle);
        }

        if (!ApplyDesktopOwner(windowHandle, defView))
        {
            return false;
        }

        // Only sink on first attach. Re-calling PlaceJustAboveProgman on an
        // already-attached window reorders the DefView ownership group and can
        // drop sibling widgets below Progman (right-click restore repro).
        if (!alreadyAttached)
        {
            PlaceJustAboveProgman(windowHandle);
            _ = ApplyDesktopOwner(windowHandle, defView);
        }

        App.LogVerbose(
            $"[WidgetLayer] DesktopPinned owner attached hwnd=0x{windowHandle.ToInt64():X} " +
            $"defView=0x{defView.ToInt64():X} firstAttach={!alreadyAttached}");
        return true;
    }

    private static bool TryResolveDesktopIconView(out IntPtr defView)
    {
        defView = FindDesktopIconView();
        return defView != IntPtr.Zero && Win32Helper.IsWindow(defView);
    }

    /// <summary>
    /// Matches live XZDesktopWnd: <c>GetParent==0</c>, <c>GWLP_HWNDPARENT==DefView</c>.
    /// </summary>
    private static bool ApplyDesktopOwner(
        IntPtr windowHandle,
        IntPtr desktopIconView,
        bool clearTopMost = true)
    {
        if (windowHandle == IntPtr.Zero || !Win32Helper.IsWindow(windowHandle))
        {
            return false;
        }

        lock (s_desktopLayerLock)
        {
            if (!s_desktopLayerAttachments.ContainsKey(windowHandle))
            {
                s_desktopLayerAttachments[windowHandle] = new DesktopLayerAttachment(
                    Win32Helper.GetWindowLongPtr(windowHandle, Win32Helper.GWLP_HWNDPARENT),
                    Win32Helper.GetParent(windowHandle));
            }

            InstallZOrderGuard(windowHandle);

            // Undo any previous true SetParent embed experiment.
            IntPtr currentParent = Win32Helper.GetParent(windowHandle);
            if (currentParent != IntPtr.Zero)
            {
                _ = Win32Helper.SetParent(windowHandle, IntPtr.Zero);
            }

            IntPtr currentOwner = Win32Helper.GetWindowLongPtr(windowHandle, Win32Helper.GWLP_HWNDPARENT);
            if (currentOwner != desktopIconView)
            {
                Win32Helper.SetLastError(0);
                _ = Win32Helper.SetWindowLongPtr(
                    windowHandle,
                    Win32Helper.GWLP_HWNDPARENT,
                    desktopIconView);
            }

            IntPtr actualOwner = Win32Helper.GetWindowLongPtr(windowHandle, Win32Helper.GWLP_HWNDPARENT);
            if (actualOwner != desktopIconView)
            {
                int error = Marshal.GetLastWin32Error();
                App.Log($"[WidgetLayer] DesktopPinned owner attach failed hwnd=0x{windowHandle.ToInt64():X} defView=0x{desktopIconView.ToInt64():X} actual=0x{actualOwner.ToInt64():X} error={error}");
                RestoreOriginalAttachment(windowHandle);
                s_cachedDesktopIconView = IntPtr.Zero;
                return false;
            }

            if (clearTopMost)
            {
                // Only strips real WS_EX_TOPMOST; see Win32Helper.ClearWindowTopMost.
                Win32Helper.ClearWindowTopMost(windowHandle);
            }

            return true;
        }
    }

    /// <summary>
    /// Places <paramref name="windowHandle"/> immediately above <c>Progman</c>
    /// in the top-level Z-order (below normal apps). Avoids <c>HWND_TOP</c>.
    /// </summary>
    private static void PlaceJustAboveProgman(IntPtr windowHandle)
    {
        // Strip real topmost only — do not use bare HWND_NOTOPMOST (raises z-order).
        Win32Helper.ClearWindowTopMost(windowHandle);

        IntPtr progman = Win32Helper.FindWindow("Progman", null);
        if (progman == IntPtr.Zero || !Win32Helper.IsWindow(progman))
        {
            Win32Helper.SetWindowPos(
                windowHandle,
                Win32Helper.HWND_BOTTOM,
                0,
                0,
                0,
                0,
                Win32Helper.SWP_NOMOVE |
                    Win32Helper.SWP_NOSIZE |
                    Win32Helper.SWP_NOACTIVATE);
            return;
        }

        // Window currently above Progman. Inserting below it puts us right above Progman.
        IntPtr aboveProgman = Win32Helper.GetWindow(progman, Win32Helper.GW_HWNDPREV);
        if (aboveProgman == windowHandle)
        {
            return;
        }

        if (aboveProgman == IntPtr.Zero)
        {
            Win32Helper.SetWindowPos(
                windowHandle,
                Win32Helper.HWND_BOTTOM,
                0,
                0,
                0,
                0,
                Win32Helper.SWP_NOMOVE |
                    Win32Helper.SWP_NOSIZE |
                    Win32Helper.SWP_NOACTIVATE);
            return;
        }

        Win32Helper.SetWindowPos(
            windowHandle,
            aboveProgman,
            0,
            0,
            0,
            0,
            Win32Helper.SWP_NOMOVE |
                Win32Helper.SWP_NOSIZE |
                Win32Helper.SWP_NOACTIVATE);
    }

    private static void DetachFromDesktopIconLayerIfNeeded(IntPtr windowHandle)
    {
        lock (s_desktopLayerLock)
        {
            if (!s_desktopLayerAttachments.ContainsKey(windowHandle))
            {
                return;
            }

            RestoreOriginalAttachment(windowHandle);
        }
    }

    private static void FallbackToDesktopBottom(IntPtr windowHandle)
    {
        DetachFromDesktopIconLayerIfNeeded(windowHandle);
        Win32Helper.ClearWindowTopMost(windowHandle);
        Win32Helper.SetWindowToBottom(windowHandle);
    }

    private static void RestoreOriginalAttachment(IntPtr windowHandle)
    {
        if (!s_desktopLayerAttachments.TryGetValue(windowHandle, out var attachment))
        {
            return;
        }

        RemoveZOrderGuard(windowHandle);

        if (Win32Helper.GetParent(windowHandle) != IntPtr.Zero)
        {
            _ = Win32Helper.SetParent(windowHandle, attachment.OriginalParent);
        }

        Win32Helper.SetLastError(0);
        _ = Win32Helper.SetWindowLongPtr(
            windowHandle,
            Win32Helper.GWLP_HWNDPARENT,
            attachment.OriginalOwner);
        Win32Helper.ClearWindowTopMost(windowHandle);
        s_desktopLayerAttachments.Remove(windowHandle);
        App.LogVerbose($"[WidgetLayer] DesktopPinned owner detached hwnd=0x{windowHandle.ToInt64():X}");
    }

    private static void InstallZOrderGuard(IntPtr windowHandle)
    {
        if (s_zOrderGuardInstalled.Contains(windowHandle))
        {
            return;
        }

        if (Win32Helper.SetWindowSubclass(
                windowHandle,
                s_zOrderGuardProc,
                ZOrderGuardSubclassId,
                UIntPtr.Zero))
        {
            s_zOrderGuardInstalled.Add(windowHandle);
            App.LogVerbose($"[WidgetLayer] ZOrderGuard installed hwnd=0x{windowHandle.ToInt64():X}");
        }
    }

    private static void RemoveZOrderGuard(IntPtr windowHandle)
    {
        if (!s_zOrderGuardInstalled.Remove(windowHandle))
        {
            return;
        }

        _ = Win32Helper.RemoveWindowSubclass(windowHandle, s_zOrderGuardProc, ZOrderGuardSubclassId);
        App.LogVerbose($"[WidgetLayer] ZOrderGuard removed hwnd=0x{windowHandle.ToInt64():X}");
    }

    /// <summary>
    /// Blocks WinUI/activation from flashing the widget above normal apps.
    /// Allows: <c>HWND_BOTTOM</c>, peer widgets, and the exact “just above Progman”
    /// sink target. Clamps <c>HWND_TOP</c>/TopMost and any other app HWND used as
    /// a raise anchor (content-click often uses a real foreground HWND, not HWND_TOP).
    /// </summary>
    private static IntPtr ZOrderGuardSubclassProc(
        IntPtr hWnd,
        uint msg,
        UIntPtr wParam,
        IntPtr lParam,
        UIntPtr uIdSubclass,
        UIntPtr dwRefData)
    {
        if (msg == Win32Helper.WM_WINDOWPOSCHANGING && lParam != IntPtr.Zero)
        {
            var windowPos = Marshal.PtrToStructure<Win32Helper.WINDOWPOS>(lParam);
            // No safe anchor (e.g. only peers above Progman): skip clamp — never
            // invent a sibling insertAfter (that flashes the other box on top).
            if ((windowPos.flags & Win32Helper.SWP_NOZORDER) == 0 &&
                ShouldClampZOrderChange(hWnd, windowPos.hwndInsertAfter) &&
                TryGetDesktopBandInsertAfter(hWnd, out IntPtr bandInsertAfter))
            {
                windowPos.hwndInsertAfter = bandInsertAfter;
                Marshal.StructureToPtr(windowPos, lParam, fDeleteOld: false);
            }
        }

        return Win32Helper.DefSubclassProc(hWnd, msg, wParam, lParam);
    }

    private static bool ShouldClampZOrderChange(IntPtr self, IntPtr insertAfter)
    {
        if (insertAfter == Win32Helper.HWND_BOTTOM)
        {
            return false;
        }

        if (insertAfter == Win32Helper.HWND_TOP ||
            insertAfter == Win32Helper.HWND_TOPMOST ||
            insertAfter == Win32Helper.HWND_NOTOPMOST)
        {
            return true;
        }

        lock (s_desktopLayerLock)
        {
            // Peer reorder: SetWindowPos(peer, clicked) must stay allowed.
            if (s_desktopLayerAttachments.ContainsKey(insertAfter))
            {
                return false;
            }
        }

        IntPtr progman = Win32Helper.FindWindow("Progman", null);
        if (insertAfter == progman)
        {
            return false;
        }

        // PlaceJustAboveProgman uses exactly this insert-after.
        if (TryGetDesktopBandInsertAfter(self, out IntPtr bandInsertAfter) &&
            insertAfter == bandInsertAfter)
        {
            return false;
        }

        // Any other top-level HWND (e.g. Chrome as activation anchor) would leave
        // the widget covering apps below that anchor — clamp it.
        return true;
    }

    /// <summary>
    /// Safe <c>hwndInsertAfter</c> for desktop-band clamps:
    /// <list type="bullet">
    /// <item>Non-front peers → current front peer (must not race to the same app HWND).</item>
    /// <item>Front (or unknown) → lowest visible foreign window above Progman.</item>
    /// </list>
    /// Skipping peers/IME as the foreign anchor; never invent a sibling clamp target
    /// when no foreign window exists.
    /// </summary>
    private static bool TryGetDesktopBandInsertAfter(IntPtr self, out IntPtr insertAfter)
    {
        insertAfter = IntPtr.Zero;

        IntPtr frontPeer;
        HashSet<IntPtr> peers;
        lock (s_desktopLayerLock)
        {
            frontPeer = s_frontPeerHwnd;
            peers = [.. s_desktopLayerAttachments.Keys];
        }

        // Non-front widgets must stay under the designated front. Clamping every
        // peer to the same Qt/Chrome HWND makes the last clamp the visual top.
        if (self != frontPeer &&
            frontPeer != IntPtr.Zero &&
            peers.Contains(frontPeer) &&
            Win32Helper.IsWindow(frontPeer))
        {
            insertAfter = frontPeer;
            return true;
        }

        IntPtr progman = Win32Helper.FindWindow("Progman", null);
        if (progman == IntPtr.Zero || !Win32Helper.IsWindow(progman))
        {
            return false;
        }

        IntPtr walker = Win32Helper.GetWindow(progman, Win32Helper.GW_HWNDPREV);
        if (walker == IntPtr.Zero)
        {
            insertAfter = Win32Helper.HWND_BOTTOM;
            return true;
        }

        for (int i = 0; i < 64 && walker != IntPtr.Zero; i++)
        {
            if (walker != self
                && !peers.Contains(walker)
                && !IsDesktopShellClass(walker)
                && !IsIgnorableZOrderAnchor(walker)
                && Win32Helper.IsWindowVisible(walker))
            {
                insertAfter = walker;
                return true;
            }

            walker = Win32Helper.GetWindow(walker, Win32Helper.GW_HWNDPREV);
        }

        // Only peers/shell above Progman — no foreign clamp target.
        return false;
    }

    private static bool IsIgnorableZOrderAnchor(IntPtr hWnd)
    {
        string name = GetClassNameOrEmpty(hWnd);
        return name is "IME" or "MSCTFIME UI";
    }

    private static IntPtr FindDesktopIconView()
    {
        IntPtr existingDefView = FindExistingDesktopIconView();
        if (existingDefView != IntPtr.Zero)
        {
            return existingDefView;
        }

        // No existing WorkerW found: send 0x052C to Progman to spawn one.
        // Only used by attach paths — never from mouse-hook hot paths.
        IntPtr progman = Win32Helper.FindWindow("Progman", null);
        if (progman != IntPtr.Zero)
        {
            _ = Win32Helper.SendMessageTimeout(
                progman,
                SpawnWorkerWMessage,
                UIntPtr.Zero,
                IntPtr.Zero,
                Win32Helper.SMTO_NORMAL,
                1000,
                out _);

            IntPtr progmanDefView = FindDesktopIconViewChild(progman);
            if (progmanDefView != IntPtr.Zero)
            {
                lock (s_desktopLayerLock)
                {
                    s_cachedDesktopIconView = progmanDefView;
                }

                return progmanDefView;
            }
        }

        // Last resort: enum again after spawning.
        IntPtr workerDefView = FindExistingDesktopIconView(forceRescan: true);
        return workerDefView;
    }

    /// <summary>
    /// Locates an already-existing <c>SHELLDLL_DefView</c> without sending <c>0x052C</c>.
    /// </summary>
    private static IntPtr FindExistingDesktopIconView(bool forceRescan = false)
    {
        if (!forceRescan &&
            s_cachedDesktopIconView != IntPtr.Zero &&
            Win32Helper.IsWindow(s_cachedDesktopIconView))
        {
            return s_cachedDesktopIconView;
        }

        // Prefer an existing WorkerW/Progman DefView. Avoid 0x052C, which can disrupt
        // DWM composition and cause the desktop wallpaper to disappear.
        IntPtr existingDefView = IntPtr.Zero;
        Win32Helper.EnumWindows((hWnd, _) =>
        {
            IntPtr defView = FindDesktopIconViewChild(hWnd);
            if (defView != IntPtr.Zero)
            {
                existingDefView = defView;
                return false; // stop enumeration
            }

            return true;
        }, IntPtr.Zero);

        lock (s_desktopLayerLock)
        {
            s_cachedDesktopIconView = existingDefView;
        }

        return existingDefView;
    }

    private static IntPtr FindDesktopIconViewChild(IntPtr windowHandle)
    {
        return Win32Helper.FindWindowEx(windowHandle, IntPtr.Zero, "SHELLDLL_DefView", null);
    }

    private static IntPtr FindDesktopIconListViewChild(IntPtr defView)
    {
        IntPtr listView = Win32Helper.FindWindowEx(defView, IntPtr.Zero, "SysListView32", "FolderView");
        if (listView != IntPtr.Zero)
        {
            return listView;
        }

        return Win32Helper.FindWindowEx(defView, IntPtr.Zero, "SysListView32", null);
    }

    // ── Visibility / z-order diagnostics ───────────────────────

    /// <summary>
    /// Full peer snapshot for diagnosing boxes that vanish behind the desktop
    /// (below Progman) or lose visibility / owner after interaction.
    /// </summary>
    public static void LogPeersSnapshot(string reason, IntPtr focusHwnd = default)
    {
        IntPtr[] attached;
        lock (s_desktopLayerLock)
        {
            attached = s_desktopLayerAttachments.Keys.ToArray();
        }

        IntPtr progman = Win32Helper.FindWindow("Progman", null);
        IntPtr defView = FindExistingDesktopIconView();
        IntPtr frontPeer;
        long generation;
        lock (s_desktopLayerLock)
        {
            frontPeer = s_frontPeerHwnd;
            generation = s_frontPeerGeneration;
        }

        App.Log(
            $"[WidgetVis] snapshot reason={reason} count={attached.Length} " +
            $"focus=0x{focusHwnd.ToInt64():X} front=0x{frontPeer.ToInt64():X} gen={generation} " +
            $"progman=0x{progman.ToInt64():X} defView=0x{defView.ToInt64():X}");

        foreach (IntPtr hwnd in attached.OrderBy(h => h.ToInt64()))
        {
            string marker = hwnd == focusHwnd ? "*" : " ";
            App.Log($"[WidgetVis] {marker}{FormatPeerSnapshotLine(hwnd, progman, defView)}");
        }
    }

    public static void LogPeersSnapshotIfAnomalous(string reason, IntPtr focusHwnd = default)
    {
        IntPtr[] attached;
        lock (s_desktopLayerLock)
        {
            attached = s_desktopLayerAttachments.Keys.ToArray();
        }

        IntPtr progman = Win32Helper.FindWindow("Progman", null);
        IntPtr defView = FindExistingDesktopIconView();
        foreach (IntPtr hwnd in attached)
        {
            if (IsPeerAnomalous(hwnd, progman, defView))
            {
                LogPeersSnapshot($"anomaly:{reason}", focusHwnd);
                return;
            }
        }
    }

    /// <summary>
    /// Delayed anomaly checks after async WinUI / DWM z-order settles.
    /// </summary>
    public static void SchedulePeersSettleSnapshot(string reason, IntPtr focusHwnd)
    {
        Microsoft.UI.Dispatching.DispatcherQueue? dispatcher = App.UiDispatcherQueue;
        if (dispatcher is null)
        {
            return;
        }

        _ = dispatcher.TryEnqueue(async () =>
        {
            await Task.Delay(120);
            LogPeersSnapshotIfAnomalous($"{reason}-settle-120ms", focusHwnd);
            await Task.Delay(380);
            LogPeersSnapshotIfAnomalous($"{reason}-settle-500ms", focusHwnd);
        });
    }

    private static bool IsPeerAnomalous(IntPtr hwnd, IntPtr progman, IntPtr defView)
    {
        if (hwnd == IntPtr.Zero || !Win32Helper.IsWindow(hwnd))
        {
            return true;
        }

        if (!Win32Helper.IsWindowVisible(hwnd))
        {
            return true;
        }

        if (IsBelowProgman(hwnd, progman))
        {
            return true;
        }

        if (defView != IntPtr.Zero)
        {
            IntPtr owner = Win32Helper.GetWindowLongPtr(hwnd, Win32Helper.GWLP_HWNDPARENT);
            if (owner != defView)
            {
                return true;
            }
        }

        return false;
    }

    private static string FormatPeerSnapshotLine(IntPtr hwnd, IntPtr progman, IntPtr defView)
    {
        bool visible = Win32Helper.IsWindowVisible(hwnd);
        bool belowProgman = IsBelowProgman(hwnd, progman);
        int hops = CountHopsTowardProgman(hwnd, progman);
        IntPtr owner = Win32Helper.GetWindowLongPtr(hwnd, Win32Helper.GWLP_HWNDPARENT);
        bool ownerOk = defView != IntPtr.Zero && owner == defView;
        bool aboveBand = IsAboveDesktopBand(hwnd);
        bool topMost = Win32Helper.IsWindowTopMost(hwnd);
        int exStyle = Win32Helper.GetWindowLong(hwnd, Win32Helper.GWL_EXSTYLE);
        bool layered = (exStyle & Win32Helper.WS_EX_LAYERED) != 0;
        string alphaText = "?";
        if (layered &&
            Win32Helper.GetLayeredWindowAttributes(hwnd, out _, out byte alpha, out uint flags) &&
            (flags & Win32Helper.LWA_ALPHA) != 0)
        {
            alphaText = alpha.ToString();
        }

        return
            $"hwnd=0x{hwnd.ToInt64():X} vis={visible} belowProgman={belowProgman} " +
            $"hopsToProgman={hops} ownerOk={ownerOk} owner=0x{owner.ToInt64():X} " +
            $"aboveBand={aboveBand} topMost={topMost} layered={layered} alpha={alphaText}";
    }

    private static int CountHopsTowardProgman(IntPtr windowHandle, IntPtr progman)
    {
        if (progman == IntPtr.Zero || windowHandle == IntPtr.Zero)
        {
            return -1;
        }

        IntPtr current = windowHandle;
        for (int i = 1; i <= 128; i++)
        {
            current = Win32Helper.GetWindow(current, Win32Helper.GW_HWNDNEXT);
            if (current == IntPtr.Zero)
            {
                return -1;
            }

            if (current == progman)
            {
                return i;
            }
        }

        return -1;
    }

    private sealed record DesktopLayerAttachment(IntPtr OriginalOwner, IntPtr OriginalParent);
}
