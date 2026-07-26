# BentoDesk 盒子层级（Z-Order）生命周期与排查手册

> 文档性质：技术实现手册 + 故障复盘指南。
> 适用场景：启动闪层、Win+D 后消失、桌面层 attach 失败、托盘显示/隐藏异常等。
> 关联文档：`docs/architecture/widget_layer_workspace_plan.md`（历史规划）、`docs/architecture/current_architecture.md`（整体架构）。
> 最后更新：2026-07-27（仅保留桌面固定层；显示路径改为 cloak → show → attach → reveal）。

---

## 1. 系统目标

盒子（widget）是一组**无边框 Win32 窗口**，始终钉在**桌面图标层**：

| 状态 | 含义 | Z-order 位置 |
|---|---|---|
| 桌面静置（DesktopResting） | 常态 | 桌面图标上方 / 普通应用下方（attach 到 `SHELLDLL_DefView`） |
| 显示中 | 托盘/快捷键显示 | 同上；兄弟窗口间可用 `BringAbovePeerWidgets` 微调 |
| 隐藏（Hidden） | 不可见 | — |

实现要点：

1. `WidgetLayerService` 通过 `GWLP_HWNDPARENT` 把窗口 owner 绑到桌面 `SHELLDLL_DefView`。
2. 所有原“临时置顶 / 回落”入口（`HoldTemporaryTopMost`、`BringToFront`、`ClearTopMost*`）都改为 `MoveToDesktopBottom`（重新 attach）。
3. 托盘 / F7“唤起”重定向为 `SetAllWidgetsVisibleAsync(true)`（显示/隐藏），不再进入 RaisedSession。

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
| `WidgetLayerService.MoveToDesktopBottom` | attach 到桌面图标层；失败则 `HWND_BOTTOM` 兜底 |
| `WidgetLayerService.BringAbovePeerWidgets` | 桌面层内兄弟排序（不抬出桌面层） |
| `WidgetLayerService.ReleaseWindow` | 关闭时 detach owner |
| `WidgetWindow` / `ContentWidgetWindow`.`ShowPreparedAtDesktopLayer` | alpha=0 → attach → show → 下 tick 清 alpha |
| `Win32Helper.SetTemporaryWindowAlpha` / `ClearTemporaryWindowAlpha` | 防闪层：盖住含 Mica 的整窗 |
| `WidgetManager.RaiseWidgetsFromTrayAsync` | 重定向为桌面层显示 |

---

## 4. 常见故障

### 4.1 启动时盒子闪一下盖住其他应用

- 检查是否仍有路径在 reveal 之后才 `PushToBottom`。
- 检查 `TryAttachToDesktopIconLayer` 是否失败并落入普通 `HWND_BOTTOM`（仍可能短暂靠前）。

### 4.2 Win+D / 显示桌面后盒子消失

- attach 失败时窗口不在桌面容器内，会被“显示桌面”一并最小化。
- 查日志 `[WidgetLayer] DesktopPinned attach skipped/failed`。

### 4.3 壁纸丢失

- 避免在热路径反复向 Progman 发送 `0x052C`。仅 attach 找不到现有 DefView 时才 spawn。

---

## 5. 修改检查清单

- [ ] 显示路径是否保持 cloak → show → attach → reveal
- [ ] 是否误用 `BringWindowTemporarilyToFront` / 持久 TopMost 抬出桌面层
- [ ] `WidgetWindow` 与 `ContentWidgetWindow` 是否同步
- [ ] 托盘/快捷键是否仍是显示/隐藏，而不是临时置顶
