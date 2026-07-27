# BentoDesk 盒子层级（Z-Order）生命周期与排查手册

> 文档性质：技术实现手册 + 故障复盘指南。
> 适用场景：启动闪层、Win+D 后消失、桌面层 attach 失败、托盘显示/隐藏异常等。
> 关联文档：`docs/architecture/widget_layer_workspace_plan.md`（历史规划）、`docs/architecture/current_architecture.md`（整体架构）。
> 最后更新：2026-07-27（对照小智桌面实机：`GWLP_HWNDPARENT=DefView`，不是 `SetParent`）。

---

## 1. 系统目标

盒子（widget）是一组**无边框 Win32 窗口**，始终钉在**桌面图标层**：

| 状态 | 含义 | Z-order 位置 |
|---|---|---|
| 桌面静置（DesktopResting） | 常态 | 桌面图标上方 / 普通应用下方（owner = `SHELLDLL_DefView`） |
| 显示中 | 托盘/快捷键显示 | 同上；兄弟窗口间可用 `BringAbovePeerWidgets` 微调 |
| 隐藏（Hidden） | 不可见 | — |

### 1.1 小智桌面（XZDesktop）实机对照（2026-07-27）

对正在运行的 `XZDesktopWnd` 探测结果：

| 字段 | 值 | 含义 |
|---|---|---|
| `GetParent` | `0` | **不是** `SetParent` 真子窗口 |
| `GWLP_HWNDPARENT` | `SHELLDLL_DefView` | **owner 挂靠**到图标层 |
| 样式 | `WS_POPUP` + `WS_EX_TOOLWINDOW` | 顶层 popup，工具窗 |
| DefView 子序 | `XZDesktopWnd` → … → `SysListView32` | 在图标 ListView **之上** |

结论：XZ 的 `BindShellWnd` / `SetParent` 日志不能直接当现行方案；主桌面窗与 BentoDesk 目标一致，都是 **DefView owner**。`XZGuarder` 才是 `SetParent` 到 DefView 的真 child。

实现要点：

1. `WidgetLayerService` 通过 `GWLP_HWNDPARENT` 把窗口 owner 绑到 `SHELLDLL_DefView`（与 XZDesktopWnd 一致）。
2. **禁止对盒子使用 `HWND_TOP`**：即便 owner 已是 DefView，WinUI 窗口被 `HWND_TOP` / 激活后仍会出现在 Chrome 等应用之上（实机 EnumWindows 已确认）。
3. **`ClearWindowTopMost` 仅在确有 `WS_EX_TOPMOST` 时调用**：对普通窗口发 `HWND_NOTOPMOST` 会把它抬到所有非 TopMost 窗口之上，表现为点标题栏兄弟反序 + 盖住应用。
4. 桌面 band 定位：插到 `Progman` 正上方；兄弟置顶用 `SetWindowPos(peer, clicked)`。Activated 后立刻 + 下一 tick + 约 32ms 再 `ReassertDesktopLayer`。
5. **防闪现 / 路径统一**：`WM_WINDOWPOSCHANGING` 护栏——非 front peer 的 clamp 锚点必须是当前 front（禁止所有盒子抢同一个 Qt/Chrome HWND，否则 last-writer 闪到最前）；front 锚到最低普通应用；无安全锚点则不 clamp。Root `PointerPressed` 立刻 `ReassertDesktopLayer`。
6. **重叠点击防闪烁**：始终 `DeferWindowPos` 沉底 front + 叠兄弟；已 front 则跳过；失败再 retry。延迟 reassert 用 front-peer generation。叠兄弟时按**现有 Z-order 自底向上**插入，避免点 3 时把 1/2 的相对顺序打乱。
7. 托盘 / F7“唤起”重定向为 `SetAllWidgetsVisibleAsync(true)`（显示/隐藏），不再进入 RaisedSession。

宿主类型（改动时需同步）：

| 宿主类 | 文件 | 用于 |
|---|---|---|
| `WidgetWindow` | `src/BentoDesk/Views/WidgetWindow.*.cs` | 文件收纳/文件夹映射盒子 |
| `ContentWidgetWindow` | `src/BentoDesk/Views/ContentWidgetWindow.*.cs` | 音乐等内容型盒子 |

---

## 2. 显示时序（防闪层）

**本质问题**：`AppWindow.Show` 会把 HWND 短暂放到普通顶层；DWM cloak / Composition opacity 盖不住 Mica（系统背景画在 HWND 上）。

**本质解法**：Show 期间用 Win32 `WS_EX_LAYERED` + alpha=0 隐藏整窗，attach 到桌面层后，在下一 dispatcher tick 再清 alpha。启动时托盘 `Activate` / 恢复结束后用 `TryRestoreForegroundWindow` 归还前台焦点。

```
SetTemporaryWindowAlpha(0)
  → PushToBottom / attach
  → AppWindow.Show(false) + SW_SHOWNOACTIVATE
  → PushToBottom
  → 下一 tick：PushToBottom → 恢复位置/视觉 → 再 Push
  → 等待约 32ms（AppWindow 异步抬层常发生在首 tick 之后）
  → PushToBottom → ClearTemporaryWindowAlpha
```

入口：`ShowPreparedAtDesktopLayer`（`revealWindow:false` 可暂缓清 alpha，供 `InitializeAsync` 后再 `CompleteTrayShowWithoutAnimation`）。

不要在清 alpha 之前 `RestoreWindowPosition` / `RestoreVisualState`（否则会在错误层级上全亮露脸）。

附带保持：`ApplyWindowBounds` 在桌面层时，`AppWindow.MoveAndResize` 后重新 attach；`FRAMECHANGED` 的 `SetWindowPos` 必须带 `SWP_NOZORDER`（`IntPtr.Zero` 就是 `HWND_TOP`）。

---

## 3. 核心 API

| 位置 | 职责 |
|---|---|
| `WidgetLayerService.MoveToDesktopBottom` | owner → DefView，并排到图标 ListView 上方 |
| `WidgetLayerService.BringAbovePeerWidgets` | 保证 owner 后 `HWND_TOP`（兄弟置顶） |
| `WidgetLayerService.ReassertDesktopLayer` | 激活后重新钉回桌面层 |
| `WidgetLayerService.ReleaseWindow` | 关闭时还原 owner |
| `WidgetWindow` / `ContentWidgetWindow`.`ShowPreparedAtDesktopLayer` | alpha=0 → attach → show → 下 tick 清 alpha |
| `Win32Helper.SetTemporaryWindowAlpha` / `ClearTemporaryWindowAlpha` | 防闪层：盖住含 Mica 的整窗 |

---

## 4. 常见故障

### 4.1 启动时盒子闪一下盖住其他应用

- 检查是否仍有路径在 reveal 之后才 `PushToBottom`。
- 检查 owner attach 是否失败并落入普通 `HWND_BOTTOM`。

### 4.2 Win+D / 显示桌面后盒子消失

- attach 失败时窗口不在桌面 band，会被“显示桌面”一并最小化。
- 查日志 `[WidgetLayer] DesktopPinned owner attach failed`。

### 4.3 壁纸丢失

- 避免在热路径反复向 Progman 发送 `0x052C`。仅 attach 找不到现有 DefView 时才 spawn。

### 4.4 点击标题栏后层级反过来

- `BringAbovePeerWidgets` 不得先走“沉到图标上方栈底”的 resting 路径。
- 应为：保证 DefView owner → `HWND_TOP`。

### 4.5 点击标题栏/内容盖住其他应用

- 实机：owner 已是 DefView 仍可能排在 Chrome 前面——元凶是 `HWND_TOP` / WinUI 激活抬层，不是 owner 没挂上。
- 核对 EnumWindows 前几名是否出现 Bento 盒子在普通应用之上。
- 修复：`PlaceJustAboveProgman` + 兄弟 `SetWindowPos(peer, clicked)`，全程不用 `HWND_TOP`。
- Activated 里调用 `ReassertDesktopLayer`。

---

## 5. 修改检查清单

- [ ] 显示路径是否保持 cloak → show → attach → reveal
- [ ] attach 是否为 `GWLP_HWNDPARENT=DefView`（对照 XZDesktopWnd）
- [ ] 静置是否在 `SysListView32` 上方（禁止盲目 `HWND_BOTTOM`）
- [ ] `BringAbovePeerWidgets` 是否跳过 resting 沉底
- [ ] `Activated` 是否 `ReassertDesktopLayer`
- [ ] `WidgetWindow` 与 `ContentWidgetWindow` 是否同步
- [ ] 托盘/快捷键是否仍是显示/隐藏，而不是临时置顶
