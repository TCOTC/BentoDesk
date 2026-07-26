# BentoDesk 官网版开发与打包操作手册

日期：2026-07-26

BentoDesk 仅通过官网 / GitHub 分发（Inno Setup 安装包）。本文档用于日常开发、调试和发布。

## 一、分发总览

| 项目 | 说明 |
| --- | --- |
| 安装形态 | Inno Setup 安装包 |
| 更新方式 | 应用内更新 + `BentoDesk.Updater.exe` + Inno 覆盖安装 |
| 开机自启 | HKCU Run 注册表 |
| 关于页渠道文案 | `官网版` |
| 捐赠二维码 | 显示 |
| Updater 产物 | Release 构建并复制；Debug 默认跳过以加速日常开发 |
| 运行时依赖 | 安装器检测 .NET / Windows App Runtime |

## 二、开发时怎么跑

日常 UI、格子、设置、文件、待办、随记、音乐等功能开发，直接构建即可：

```powershell
dotnet build .\src\BentoDesk\BentoDesk.csproj `
  -c Debug `
  -p:Platform=x64 `
  -v:minimal
```

日常交互调试统一使用脚本启动：

```powershell
.\scripts\start-debug.ps1
```

如果希望启动前顺手重新构建：

```powershell
.\scripts\start-debug.ps1 -Build
```

注意：

- 日常 Debug 启动脚本会固定运行 `src\BentoDesk\bin\x64\Debug\<TargetFramework>\BentoDesk.exe`。
- 不要手动运行 `src\BentoDesk\bin\x64\Debug\<TargetFramework>\win-x64\BentoDesk.exe`，这个目录可能残留旧 RID 构建产物，容易出现“进程启动后立刻崩溃”或“跑的不是最新代码”。
- 只有 Release 发布和安装器产物需要显式使用 `RuntimeIdentifier`。

预期：

- 关于页版本行显示 `官网版`。
- 捐赠二维码显示。
- 应用内更新使用下载 / 安装逻辑。
- 开机自启走注册表。
- Release 输出目录包含 `BentoDesk.Updater.exe`。

## 三、发版打包

### 3.1 发版前统一检查

1. 确认版本号一致：
   - `src/BentoDesk/BentoDesk.csproj`
   - Inno 脚本中的版本信息
   - README / CHANGELOG / 官网更新清单

2. 跑测试：

```powershell
dotnet test .\tests\BentoDesk.Tests\BentoDesk.Tests.csproj -c Release -v:minimal
```

3. 检查 Git 范围。
   - 可以提交：`src/`、`installer/`、`scripts/`、`tests/`、`docs/architecture/`、README、CHANGELOG。
   - 不要提交：`.codex-temp/`、`artifacts/`、`bin/`、`obj/`、临时截图和本地草稿。
   - 网站 `bentodesk-site/` 是否提交要单独决定，不要混进应用发版提交里。

### 3.2 官网版打包

走现有 Inno 链路。

关键要求：

- 需要构建并复制 `BentoDesk.Updater.exe`。
- 安装器需要检测 `.NET 10` 和 Windows App Runtime 2.2。
- 应用内更新 manifest 指向官网 / GitHub 安装包。
- 关于页保留官网、GitHub、捐赠等入口。

验收清单：

- 干净机器能安装并启动。
- 覆盖安装旧版本后数据保留。
- 应用内检查更新、下载、安装、重启链路正常。
- 缺少运行时时，安装器提示和引导正确。
- 开机自启开关有效。
- 托盘、快捷键、多屏/DPI、文件拖拽、系统音量等底层能力正常。

### 3.3 应用内更新发布清单

应用内更新默认先读取：

```text
https://github.com/TCOTC/BentoDesk/releases/latest
```

如果该清单不可用，客户端会兜底读取 GitHub 最新 Release API。官网清单仍然是主通道，因为它可以控制稳定版本、国内网盘入口、SHA-256、灰度和回滚；GitHub 兜底只用于防止清单漏发时完全无法检查更新。

每次发布版本时必须执行：

1. 发布 GitHub Release，并上传：
   - `BentoDesk_Setup_x.y.z_x64.exe`
   - `BentoDesk_Setup_x.y.z_x64.exe.sha256`

2. 核对 GitHub Release 资产：
   - tag 是 `vx.y.z`
   - Release 不是 Draft
   - Release 不是 Prerelease，除非刻意做预发布
   - 安装包大小和本地 `Output` 一致
   - 安装包 digest / `.sha256` 和本地 `Get-FileHash` 一致

3. 更新并部署官网清单：
   - `bentodesk-site/public/update/stable.json`
   - `version`
   - `downloadUrl`
   - `sha256`
   - `size`
   - `releaseNotesUrl`
   - `summary`

4. 部署后从公网验证：

```powershell
curl.exe -i https://github.com/TCOTC/BentoDesk/releases/latest
```

预期：

- HTTP 200
- `Content-Type` 是 JSON
- `version`、`downloadUrl`、`sha256`、`size` 与 GitHub Release 完全一致

5. 用旧版本实机验证完整链路：
   - 检查更新
   - 下载更新
   - 点击安装
   - BentoDesk 退出
   - 安装器继续执行
   - 安装完成后 BentoDesk 重启
   - 数据和设置保留

6. 如果后续更新流程、清单字段、下载源、网盘链接、安装器参数或 GitHub 兜底策略有调整，必须同步更新本文档。

## 四、发版核对清单

1. Direct Debug 自测。
2. 单元测试通过。
3. Direct Release / Inno 打包。
4. 在干净机器或虚拟机验证安装。
5. 验证应用内更新链路。
6. 发布 GitHub Release / 安装包。
7. 更新官网更新清单。
