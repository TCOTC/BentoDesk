# BentoDesk

简体中文 | [English](README.md)

[![CI](https://github.com/TCOTC/BentoDesk/actions/workflows/ci.yml/badge.svg)](https://github.com/TCOTC/BentoDesk/actions/workflows/ci.yml)
[![License: GPL v3](https://img.shields.io/badge/license-GPLv3-blue.svg)](LICENSE)
[![Windows 11](https://img.shields.io/badge/Windows-11-0078D4.svg)](#环境要求)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](#构建)

BentoDesk 是一个基于 WinUI 3 的 Windows 11 桌面整理工具。它用轻量桌面盒子帮你收纳文件、映射文件夹，也可以在桌面上控制音乐。BentoDesk 不会替换 Windows 桌面，只是在原生桌面之上补一层更好整理、更好访问、更容易临时唤起的能力。

> 本仓库基于 [DeskBox](https://github.com/Tianyu199509/DeskBox)（作者：Tianyu Zhu）fork，并更名为 BentoDesk，仍遵循 GPL-3.0。

![BentoDesk 产品封面](docs/images/brand/product-cover-zh-cn-1280x720.png)

## 下载

可以在 [GitHub Releases](https://github.com/TCOTC/BentoDesk/releases) 下载最新版安装包。

当前版本：1.3.3

- [BentoDesk_Setup_1.3.3_x64.exe](https://github.com/TCOTC/BentoDesk/releases/download/v1.3.3/BentoDesk_Setup_1.3.3_x64.exe)
- [BentoDesk_Setup_1.3.3_arm64.exe](https://github.com/TCOTC/BentoDesk/releases/download/v1.3.3/BentoDesk_Setup_1.3.3_arm64.exe)（Surface、骁龙及其他 ARM64 设备）

x64 安装器检测 .NET 10 Runtime x64 与 Windows App Runtime 2.2 x64；ARM64 安装器检测对应的 ARM64 版本。若目标电脑缺少运行时依赖，安装流程可以联网下载并安装。

## 最新更新

- **拖拽（微信 + 浏览器）**：可直接从微信聊天窗口拖拽文件和图片到盒子内；浏览器拖拽的图片和文件链接自动下载并导入；拖拽文件到文件夹项目上可直接传入文件夹。
- **叠放组管理**：右键叠放组可重命名、上移/下移排序、取消折叠/恢复折叠。
- **F7 层级可靠性修复**：修复静默回落（状态变了但画面没变）和跨进程点击检测不可靠导致盒子不回落/闪烁不收起的问题。
- **界面打磨**：修复托盘图标标签颠倒（黑色/白色）；隐藏折叠预览箭头；简化设置搜索占位符。
- **本地化**：全部五种语言新增叠放组管理等翻译字符串。

完整更新记录见 [CHANGELOG.md](CHANGELOG.md)。

## 为什么做这个产品

Windows 桌面已经陪大家用了很多年，也是很多人每天最常用的地方。但它也很容易变乱：临时文件、截图、下载内容、待处理事项，最后都堆在一起。BentoDesk 想做的是帮桌面多一层克制的整理能力，而不是把桌面变成另一个复杂系统。Windows 桌面仍然是 Windows 桌面，文件仍然是普通文件，盒子只是帮你把它们收纳、映射、查看和临时唤起。

我也很喜欢 WinUI 的原生质感，所以 BentoDesk 后续会一直尽量按 Windows 原生设计和交互规范做下去：WinUI 3 控件、Windows App SDK、DWM 圆角、亚克力质感、托盘优先的工作流。能用原生能力时会优先用原生能力，不会为了一个很小的效果随便引入很重的第三方库。安装包采用框架依赖方式，会检查目标电脑上的 .NET 与 Windows App Runtime，只在缺失时下载对应依赖。

## 功能

- **收纳盒子**：创建真实文件夹支撑的桌面盒子，用于整理文件。
- **文件夹映射**：把已有文件夹展示为桌面盒子，不改变原文件位置。
- **音乐盒子**：支持播放控制、播放模式切换、系统音量调整和自适应封面布局，可跟随封面氛围取色。
- **胶囊模式**：把盒子收起为智能摘要，可独立摆放，也可组合成能够排序和整体移动的桌面栏。
- **文件自动叠放**：按类型、日期或自定义扩展名规则整理文件盒子，不移动真实文件。
- **拖入后收纳**：拖入收纳盒子的文件默认复制到对应的真实收纳文件夹，也可在设置中改为移动。
- **托盘管理**：新建盒子、映射文件夹、显示或隐藏全部盒子、临时置顶、打开收纳目录、打开设置、开机自启和退出。
- **全局快捷键**：可用快捷键快速显示、隐藏或唤起盒子。
- **原生文件操作**：拖入、拖出、粘贴、剪切、重命名、删除、打开、在资源管理器中显示、键盘快捷键，并可通过已运行的 QuickLook 按空格预览。
- **外观调节**：支持原生材质、材质浓度、透明度、边框颜色与样式、DWM 圆角、显示密度、图标/文字大小、标题样式和封面氛围背景。
- **数据与收纳维护**：导出或恢复备份、管理自动快照、检查附件健康、调整默认收纳路径、固定到快速访问并恢复孤立收纳文件夹。

## 截图

### 桌面总览

| 浅色主题 | 深色主题 |
| --- | --- |
| ![BentoDesk 浅色桌面总览](docs/images/screenshots/zh-cn/desktop-light.png) | ![BentoDesk 深色桌面总览](docs/images/screenshots/zh-cn/desktop-dark.png) |

### 核心盒子

| 文件盒子 | 音乐盒子 |
| --- | --- |
| ![BentoDesk 文件盒子列表视图](docs/images/screenshots/zh-cn/file-widget-list.png) | ![BentoDesk 音乐盒子](docs/images/screenshots/zh-cn/music-widget.png) |

### 设置页

| 常规 | 外观 |
| --- | --- |
| ![BentoDesk 常规设置](docs/images/screenshots/zh-cn/settings-general-1-2.png) | ![BentoDesk 外观设置](docs/images/screenshots/zh-cn/settings-appearance-1-2.png) |
| 文件盒子 | 功能盒子 |
| ![BentoDesk 文件盒子设置](docs/images/screenshots/zh-cn/settings-file-widgets-1-2.png) | ![BentoDesk 功能盒子设置](docs/images/screenshots/zh-cn/settings-feature-widgets-1-2.png) |

### 品牌动效

<p align="center">
  <img src="docs/motion/bentodesk-motion-01-layer-assemble.svg" width="120" alt="BentoDesk logo layer assembly animation" />
</p>

## 环境要求

- Windows 11。
- .NET 10 Runtime x64。
- Windows App Runtime 2.2 x64。

当前项目主要在 Windows 11 下测试。Windows 10 或其他系统版本尚未完整验证。

开发环境需要 .NET 10 SDK。推荐使用安装了 Windows App SDK 工作负载的 Visual Studio。

## 安装和卸载

安装器基于 Inno Setup 构建，默认安装到当前用户目录。覆盖安装会保留现有应用设置、盒子配置和收纳目录内容；旧版如果安装在 Program Files，安装器会自动迁移，避免 BentoDesk 以管理员权限运行后影响资源管理器拖拽。

开机自启会静默启动到托盘。如果 BentoDesk 已经运行，登录时再次启动的实例会直接退出，不会弹出设置页面。

卸载时安装器会先停止正在运行的 BentoDesk，并让你选择是否删除 `%LocalAppData%\BentoDesk` 下的本地应用数据。收纳目录中的用户文件不会被静默删除；当清理可能影响用户文件时，会先提示确认。

## 构建

还原并构建：

```powershell
dotnet restore .\BentoDesk.sln -p:Platform=x64
dotnet build .\src\BentoDesk\BentoDesk.csproj --configuration Debug --no-restore -p:Platform=x64 -v:minimal
```

运行测试：

```powershell
dotnet test .\BentoDesk.sln --configuration Debug --no-restore -p:Platform=x64 -v:minimal
```

启动 Debug 应用：

```powershell
.\scripts\start-debug.ps1
```

生成 Release x64 输出和安装包：

```powershell
dotnet publish .\src\BentoDesk\BentoDesk.csproj --configuration Release -p:Platform=x64 -p:RuntimeIdentifier=win-x64 -p:SelfContained=false -p:WindowsAppSDKSelfContained=false -o .\artifacts\publish\BentoDesk\x64 -v:minimal
& 'C:\Program Files\Inno Setup 7\ISCC.exe' .\installer\BentoDesk.iss
```

安装包输出：

```text
Output\BentoDesk_Setup_1.3.3_x64.exe
```

## 项目结构

```text
src\BentoDesk                 WinUI 3 应用源码
tests\BentoDesk.Tests         核心服务测试
installer                   Inno Setup 安装脚本
docs\images                 README 和发布截图资源
docs\motion                 品牌动效方案与 SVG 资源
docs\releases               GitHub Releases 发布文案
```

## 数据位置

- 应用设置保存在 `%LocalAppData%\BentoDesk\data`。
- 默认收纳路径为 `%UserProfile%\BentoDesk`。
- `bin`、`obj`、`Output`、`artifacts` 和 `TestResults` 等生成目录已被 Git 忽略。

## 贡献与反馈

本项目目前由个人开发者独立维护，并作为长期的个人产品进行演进。为了保证代码架构的绝对一致性以及后续版权的清晰度，本项目当前暂不接受外部的代码合并（Pull Request）。

尽管如此，BentoDesk 的成长离不开社区的反馈！如果您在使用中遇到了 Bug，或者对新功能有绝佳的想法，非常欢迎您通过提交 [Issue](https://github.com/TCOTC/BentoDesk/issues) 的方式与我交流。感谢您的理解与支持！

## 反馈

BentoDesk 仍处于早期公开版本。如果 Win10/Win11 遇到文件拖不进盒子的问题，请先尝试"设置 -> 拖拽异常诊断 -> 一键修复"。如果仍有问题，可以扫码关注应用"关于"页里的公众号留言，或在 GitHub 提交 [Issue](https://github.com/TCOTC/BentoDesk/issues)。

## 作者

- 本仓库维护者：TCOTC
- 上游原作者：Tianyu Zhu（[DeskBox](https://github.com/Tianyu199509/DeskBox)）
- 开源仓库：<https://github.com/TCOTC/BentoDesk>

## 开源协议

BentoDesk 使用 [GPL-3.0-only](LICENSE) 授权（与上游 DeskBox 当前协议一致）。详见 [LICENSE_CHANGE.md](LICENSE_CHANGE.md)。
