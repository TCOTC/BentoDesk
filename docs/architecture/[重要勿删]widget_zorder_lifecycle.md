# BentoDesk 盒子层级（Z-Order）生命周期与排查手册

> 文档性质：技术实现手册 + 故障复盘指南。
> 适用场景：启动闪层、Win+D 后消失、桌面层 attach 失败、托盘显示/隐藏异常、点击兄弟闪烁等。
> 关联文档：`docs/architecture/widget_layer_workspace_plan.md`（历史规划；动态层级已延后）、`docs/architecture/current_architecture.md`（整体架构）。
> 最后更新：2026-07-27（三原语 Pin / Front / Rescue 收敛；对照小智桌面：`GWLP_HWNDPARENT=DefView`）。

---

## 1. 系统目标

盒子（widget）是一组**无边框 Win32 窗口**，始终钉在**桌面图标层**（单一模式：桌面固定层）：

| 状态 | 含义 | Z-order 位置 |
|---|---|---|
| 桌面静置（DesktopPinned） | 常态 | 桌面图标上方 / 普通应用下方（owner = `SHELLDLL_DefView`） |
| Front 微调 | 用户点击后 | 仍在桌面 band 内，仅被点盒子压过兄弟 |
| 隐藏（Hidden） | 不可见 | — |

托盘 / F7 只做**显示 / 隐藏**，不做临时全局置顶，不进入 RaisedSession。

### 1.1 小智桌面（XZDesktop）实机对照（2026-07-27）

| 字段 | 值 | 含义 |
|---|---|---|
| `GetParent` | `0` | **不是** `SetParent` 真子窗口 |
| `GWLP_HWNDPARENT` | `SHELLDLL_DefView` | **owner 挂靠**到图标层 |
| 样式 | `WS_POPUP` + `WS_EX_TOOLWINDOW` | 顶层 popup，工具窗 |

结论：主桌面窗与 BentoDesk 目标一致，都是 **DefView owner**。

### 1.2 物理不变量

1. `GWLP_HWNDPARENT == SHELLDLL_DefView`，`GetParent == 0`
2. 禁止 `HWND_TOP` / 真 `WS_EX_TOPMOST` 作为抬层手段
3. 全体盒子在 Progman 之上；有人掉到 Progman 下 → `Rescue`
4. **`Pin` 永不抢 peer front**；只有用户意图才 `Front`
5. 一次用户手势最多有效 `Front` 一次（front-peer generation 门闩）

### 1.3 实现要点

1. `WidgetLayerService` 通过 `GWLP_HWNDPARENT` 绑到 `SHELLDLL_DefView`。
2. **禁止 `HWND_TOP`**：即便 owner 已是 DefView，WinUI 被 `HWND_TOP` / 激活后仍会盖住 Chrome。
3. **`ClearWindowTopMost` 仅在确有 `WS_EX_TOPMOST` 时调用**：对普通窗口发 `HWND_NOTOPMOST` 会误抬层。
4. `Front` 只抬被点 HWND（`RaiseSelfInDesktopBand`）；**禁止**对全体兄弟做 `DeferWindowPos` 全组 restack（会轮转 orphan，点击闪烁）。
5. `WM_WINDOWPOSCHANGING` 护栏：非 front peer 的 clamp 锚点必须是当前 front。
6. 窗口侧只走 `WidgetWindowBase.LayerOn*` 门面，禁止散落直接调 Z-order。

宿主类型：

| 宿主类 | 文件 | 用于 |
|---|---|---|
| `WidgetWindow` | `src/BentoDesk/Views/WidgetWindow.*.cs` | 文件收纳/文件夹映射盒子 |
| `ContentWidgetWindow` | `src/BentoDesk/Views/ContentWidgetWindow.*.cs` | 音乐等内容型盒子 |

---

## 2. 显示时序（防闪层）

**本质问题**：`AppWindow.Show` 会把 HWND 短暂放到普通顶层；DWM cloak / Composition opacity 盖不住 Mica。

**本质解法**：Show 期间用 Win32 `WS_EX_LAYERED` + alpha=0；`Pin`（`LayerOnShow` / `PushToBottom`）后再清 alpha。

```
SetTemporaryWindowAlpha(0)
  → LayerOnShow / Pin
  → AppWindow.Show(false) + SW_SHOWNOACTIVATE
  → Pin
  → 下一 tick：Pin → 恢复位置/视觉 → 再 Pin
  → 等待约 32ms
  → Pin → ClearTemporaryWindowAlpha
```

入口：`ShowPreparedAtDesktopLayer`。

`ApplyWindowBounds` 在桌面层时，`MoveAndResize` 后只 `LayerAfterBoundsChange` → **Pin**（不得 `Front`，避免改大小抢兄弟 front）。`FRAMECHANGED` 必须带 `SWP_NOZORDER`。

---

## 3. 核心 API（三原语）

### 3.1 服务层 `WidgetLayerService`

| API | 职责 |
|---|---|
| `Pin(hwnd, reason)` | attach DefView + 必要时钉回 desktop band；**不** `NoteFrontPeer` |
| `Front(hwnd, reason)` | `NoteFrontPeer` + 只抬自己；必要时 `Rescue` |
| `Rescue(preferredFront?, reason)` | 仅拉回 below-Progman 的 peer；禁止对 peer 调 `PlaceJustAboveProgman` |
| `ScheduleFront(hwnd, reason)` | 32ms 延迟 settle，generation 门闩（补偿 WinUI 异步抬层） |
| `Release(hwnd)` | 关闭时还原 owner；清空 stale front 锚点 |

### 3.2 窗口门面 `WidgetWindowBase`

| 方法 | 时机 | 服务调用 |
|---|---|---|
| `LayerOnUserActivate` | 内容/标题点击、交互开始 | `Front` |
| `LayerScheduleFrontSettle` | `Activated`（不再立刻再 Front） | `ScheduleFront` |
| `LayerOnShow` | 防闪层多拍 | `Pin` |
| `LayerOnRestore` | 失活 / 交互结束 / 显示器恢复 | `Pin` |
| `LayerAfterBoundsChange` | `MoveAndResize` 后 | `Pin` |
| `LayerOnHide` / `LayerOnClose` | 隐藏 / 关闭 | `Pin` / `Release` |

日志约定：`[WidgetVis] op=Pin|Front|Rescue reason=... hwnd=... gen=...`

---

## 4. 常见故障

### 4.1 启动时盒子闪一下盖住其他应用

- 检查是否仍有路径在 reveal 之后才 `Pin`。
- 检查 owner attach 是否失败并落入普通 `HWND_BOTTOM`。

### 4.2 Win+D / 显示桌面后盒子消失

- attach 失败时窗口不在桌面 band，会被“显示桌面”一并最小化。
- 查日志 `[WidgetLayer] DesktopPinned owner attach failed`。

### 4.3 壁纸丢失

- 避免在热路径反复向 Progman 发送 `0x052C`。仅 attach 找不到现有 DefView 时才 spawn。

### 4.4 点击盒子时其他盒子闪烁

- **根因（已修）**：全组 `DeferWindowPos` restack + 失活 `Restore` 抢 front，轮转把兄弟沉到 Progman 下（日志 `belowProgman=True` / `anomaly:*`）。
- **现行规则**：`Front` 只抬自己；`Pin` 不抢 front；orphan 用 `Rescue`（不对 peer 调 `PlaceJustAboveProgman`）。
- 一次点击路径：`PointerPressed` → `LayerOnUserActivate`（Front）→ `Activated` → `LayerScheduleFrontSettle`（仅 settle）。

### 4.5 点击标题栏/内容盖住其他应用

- 元凶是 `HWND_TOP` / WinUI 激活抬层，不是 owner 没挂上。
- 核对 EnumWindows：Bento 盒子不得排在普通应用之上。
- 修复：`RaiseSelfInDesktopBand` + ZOrderGuard；全程不用 `HWND_TOP`。

---

## 5. 修改检查清单

- [ ] 显示路径是否保持 alpha=0 → Pin → show → Pin → reveal
- [ ] attach 是否为 `GWLP_HWNDPARENT=DefView`
- [ ] 静置是否在 Progman / 图标之上（禁止盲目 `HWND_BOTTOM`）
- [ ] 新代码是否只走 `Pin` / `Front` / `Rescue` / `LayerOn*`（无 TopMost 别名、无全组 restack）
- [ ] `Activated` 是否仅 `ScheduleFront`（不立刻二次 Front）
- [ ] `MoveAndResize` 后是否仅 `Pin`
- [ ] `WidgetWindow` 与 `ContentWidgetWindow` 是否同步
- [ ] 托盘/快捷键是否仍是显示/隐藏
- [ ] 点击三盒回归：无轮转 `belowProgman=True`，无肉眼闪烁
