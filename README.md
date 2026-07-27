# BentoDesk

English | [简体中文](README.zh-CN.md)

[![CI](https://github.com/TCOTC/BentoDesk/actions/workflows/ci.yml/badge.svg)](https://github.com/TCOTC/BentoDesk/actions/workflows/ci.yml)
[![License: GPL v3](https://img.shields.io/badge/license-GPLv3-blue.svg)](LICENSE)
[![Windows 11](https://img.shields.io/badge/Windows-11-0078D4.svg)](#requirements)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](#build)

BentoDesk is a lightweight WinUI 3 desktop organizer for Windows 11. It creates native-feeling desktop widgets for collecting files, mapping folders, and controlling music from the desktop. It does not replace the Windows desktop shell; it adds one focused layer for keeping everyday things easier to reach, easier to sort, and easier to bring forward when you need them.

> This repository is a fork of [DeskBox](https://github.com/Tianyu199509/DeskBox) by Tianyu Zhu, renamed to BentoDesk, and remains licensed under GPL-3.0.

![BentoDesk product cover](docs/images/brand/product-cover-en-us-1280x720.png)

## Download

Download the latest installer from [GitHub Releases](https://github.com/TCOTC/BentoDesk/releases).

Current release: 1.3.3

- [BentoDesk_Setup_1.3.3_x64.exe](https://github.com/TCOTC/BentoDesk/releases/download/v1.3.3/BentoDesk_Setup_1.3.3_x64.exe)
- [BentoDesk_Setup_1.3.3_arm64.exe](https://github.com/TCOTC/BentoDesk/releases/download/v1.3.3/BentoDesk_Setup_1.3.3_arm64.exe) (Surface, Snapdragon, and other ARM64 PCs)

The x64 installer checks for .NET 10 Runtime x64 and Windows App Runtime 2.2 x64; the ARM64 installer checks for the ARM64 variants. If a dependency is missing, the setup flow can download and install it for you.

## What's New In 1.3.3

- **Drag & drop (WeChat + browser)**: Drag files and images directly from WeChat chat windows into grid items. Browser URL drops (images, file links) are automatically downloaded and imported. Files dropped onto folder items transfer into the folder.
- **Stack group management**: Rename stack groups, reorder them (move up/down), and disable/restore stacking per group — all from the right-click context menu.
- **F7 Z-order reliability fix**: Fixed silent restore (state changed but visual didn't) and unreliable cross-process click detection that caused widgets to stay on top or flicker without collapsing.
- **UI polish**: Fixed swapped tray icon labels (Black/White); hidden collapsed-preview chevron; simplified settings search placeholder.
- **Localization**: New strings for all features across zh-CN, en-US, ja-JP, de-DE, pt-BR.

See the full [changelog](CHANGELOG.md).

## Why BentoDesk Exists

The Windows desktop has been one of the most-used places on the PC for decades, but for many people it also becomes the easiest place to make a mess. BentoDesk exists to keep that familiar desktop useful without turning it into something else. Your desktop stays the Windows desktop, and your files stay normal files; BentoDesk simply gives you small, tidy places to collect, map, search, edit, and bring things forward.

The project is intentionally built around native Windows behavior. I like the texture and restraint of WinUI, so BentoDesk will keep following native Windows patterns wherever practical: WinUI 3 controls, Windows App SDK, DWM corners, acrylic-style surfaces, tray-first behavior, and conservative dependencies. The installer stays framework-dependent: it checks .NET and Windows App Runtime on the target PC and downloads only a missing dependency.

## Features

- **Managed desktop widgets**: create file collection widgets backed by a real folder.
- **Folder mapping**: display an existing folder as a desktop widget without moving its contents.
- **Music widget**: control playback, switch playback mode, adjust system volume, and use responsive album-art layouts with optional album-color ambience.
- **Expand & collapse**: collapse widgets into a compact state, place them independently, and expand on click or hover.
- **Automatic file stacks**: group related file-widget items by type, date or prioritized custom extension rules without moving the actual files.
- **Copy into managed storage**: dropped files are copied into the managed widget's real storage folder by default; move remains available in Settings.
- **Tray controls**: create widgets, map folders, show or hide all widgets, temporarily raise widgets, open managed storage, open Settings, toggle startup launch, and exit.
- **Global hotkey**: enable a keyboard shortcut for quickly showing, hiding, or raising widgets.
- **Native file operations**: drag in, drag out, paste, cut, rename, delete, open, reveal in Explorer, use keyboard shortcuts, and preview through a running QuickLook instance with Space.
- **Appearance controls**: tune native material, intensity, opacity, border color/style, DWM corners, display density, icon/text size, title icons and cover ambience.
- **Data and storage maintenance**: export or restore backups, inspect automatic snapshots and attachment health, change the managed storage root, pin it to Quick Access, and recover orphan folders.

## Screenshots

BentoDesk includes both English and Chinese localization. The screenshots below highlight the Windows 11-style desktop widgets, feature widgets, and Settings.

### Desktop Overview

| Light theme | Dark theme |
| --- | --- |
| ![BentoDesk light desktop overview](docs/images/screenshots/en-us/desktop-light.png) | ![BentoDesk dark desktop overview](docs/images/screenshots/en-us/desktop-dark.png) |

### Core Widgets

| File widget | Music widget |
| --- | --- |
| ![BentoDesk file widget](docs/images/screenshots/en-us/file-widget.png) | ![BentoDesk music widget](docs/images/screenshots/en-us/music-widget.png) |

### Settings

| General | Appearance |
| --- | --- |
| ![BentoDesk general settings](docs/images/screenshots/en-us/settings-general-1-2.png) | ![BentoDesk appearance settings](docs/images/screenshots/en-us/settings-appearance-1-2.png) |
| File widgets | Feature widgets |
| ![BentoDesk file widget settings](docs/images/screenshots/en-us/settings-file-widgets-1-2.png) | ![BentoDesk feature widget settings](docs/images/screenshots/en-us/settings-feature-widgets-1-2.png) |

### Logo Motion

<p align="center">
  <img src="docs/motion/bentodesk-motion-01-layer-assemble.svg" width="120" alt="BentoDesk logo layer assembly animation" />
</p>

## Requirements

- Windows 11.
- .NET 10 Runtime x64.
- Windows App Runtime 2.2 x64.

BentoDesk is currently tested on Windows 11. Windows 10 may work in some environments, but it is not a validated target.

For development, install the .NET 10 SDK. Visual Studio with Windows App SDK workload is recommended.

## Install And Uninstall

The installer is built with Inno Setup. It installs BentoDesk for the current user by default, lets you change the install folder, and preserves existing app settings, widget configuration, and managed storage content during overwrite installs. Older administrator installs under Program Files are migrated automatically so Explorer drag/drop can keep working normally.

Startup launch is handled silently through the tray. If BentoDesk is already running and Windows starts it again at login, the second startup instance exits without opening Settings.

During uninstall, BentoDesk stops the running app first and lets you choose whether to remove app-local data under `%LocalAppData%\BentoDesk`. Managed storage content is not deleted silently; when cleanup may affect user files, the installer asks before removing anything.

## Build

Restore and build:

```powershell
dotnet restore .\BentoDesk.sln -p:Platform=x64
dotnet build .\src\BentoDesk\BentoDesk.csproj --configuration Debug --no-restore -p:Platform=x64 -v:minimal
```

Run tests:

```powershell
dotnet test .\BentoDesk.sln --configuration Debug --no-restore -p:Platform=x64 -v:minimal
```

Launch the Debug app:

```powershell
.\scripts\start-debug.ps1
```

Create a Release x64 publish output and installer:

```powershell
dotnet publish .\src\BentoDesk\BentoDesk.csproj --configuration Release -p:Platform=x64 -p:RuntimeIdentifier=win-x64 -p:SelfContained=false -p:WindowsAppSDKSelfContained=false -o .\artifacts\publish\BentoDesk\x64 -v:minimal
& 'C:\Program Files\Inno Setup 7\ISCC.exe' .\installer\BentoDesk.iss
```

Installer output:

```text
Output\BentoDesk_Setup_1.3.3_x64.exe
```

## Project Structure

```text
src\BentoDesk                 WinUI 3 app source
tests\BentoDesk.Tests         core service tests
installer                   Inno Setup scripts
docs\images                 README and release images
docs\motion                 logo motion concepts and SVG assets
docs\releases               GitHub Releases copy
```

## Data Locations

- Settings are stored under `%LocalAppData%\BentoDesk\data`.
- The default managed storage root is `%UserProfile%\BentoDesk`.
- Generated folders such as `bin`, `obj`, `Output`, `artifacts`, and `TestResults` are ignored by Git.

## Contributing

BentoDesk is currently developed and maintained entirely by a solo developer. To ensure architectural consistency and maintain clear copyright for future project paths, I am not accepting external Pull Requests (PRs) at this time.

However, community feedback is crucial to the project's growth! If you encounter any bugs, have feature requests, or want to share UI/UX feedback, please feel free to open an [Issue](https://github.com/TCOTC/BentoDesk/issues). Thank you for your support and understanding!

## Feedback

BentoDesk is still an early public release. If file drag/drop fails on Windows 10/11, try Settings -> Drag-and-drop diagnostics -> Repair first. If the issue remains, please open an [issue](https://github.com/TCOTC/BentoDesk/issues) with reproduction details, or follow the WeChat public account shown in the app's About page and leave a message there.

## Author

- Maintainer of this fork: TCOTC
- Upstream author: Tianyu Zhu ([DeskBox](https://github.com/Tianyu199509/DeskBox))
- Repository: <https://github.com/TCOTC/BentoDesk>

## License

BentoDesk is licensed under [GPL-3.0-only](LICENSE), matching the current upstream DeskBox license.
See [LICENSE_CHANGE.md](LICENSE_CHANGE.md) for notes.
