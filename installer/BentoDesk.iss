; BentoDesk 安装脚本
; 构建命令：
; dotnet publish ..\src\BentoDesk\BentoDesk.csproj --configuration Release -p:Platform=x64 -p:RuntimeIdentifier=win-x64 -p:SelfContained=false -p:WindowsAppSDKSelfContained=false -o ..\artifacts\publish\BentoDesk\x64 -v:minimal

#define MyAppName "BentoDesk"
#define MyAppVersion "1.3.3"
#define MyAppVersionInfo "1.3.3.0"
#define MyAppPublisher "TCOTC"
#define MyAppExeName "BentoDesk.exe"
#define MyAppOutputBaseName "BentoDesk_Setup"
#ifndef MyAppReleaseDir
#define MyAppReleaseDir "..\\Output\\perf-final-v085"
#endif

[Setup]
; AppId 用于唯一标识同一个应用。
AppId={{B3E7D4A1-8C29-4F56-9E1B-7A2D05C84F33}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppComments=安装包会按需检测并下载 .NET 10 Runtime 和 Windows App Runtime 2.2。
UninstallDisplayName={#MyAppName} {#MyAppVersion}
UninstallDisplayIcon={app}\Assets\bentodesk.ico
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
DefaultDirName={localappdata}\Programs\{#MyAppName}
DisableProgramGroupPage=yes
DisableDirPage=no
PrivilegesRequired=lowest
UsePreviousAppDir=no
UsePreviousPrivileges=no
; BentoDesk is a tray-first WinUI app with multiple top-level windows. Restart
; Manager cannot always close the whole process through a single window, so
; allow Setup to terminate BentoDesk after the normal close attempt times out.
CloseApplications=force
CloseApplicationsFilter={#MyAppExeName}
RestartApplications=no
OutputDir=..\Output
OutputBaseFilename={#MyAppOutputBaseName}_{#MyAppVersion}_x64
SetupIconFile=..\src\BentoDesk\Assets\bentodesk.ico
VersionInfoVersion={#MyAppVersionInfo}
VersionInfoProductVersion={#MyAppVersionInfo}
VersionInfoTextVersion={#MyAppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ShowLanguageDialog=no

[Languages]
Name: "chinesesimplified"; MessagesFile: "Languages\ChineseSimplified.isl"

[CustomMessages]
chinesesimplified.ConfirmRemoveAppData=是否同时删除 BentoDesk 应用数据？%n%n这些数据包含设置、格子布局、随记图片缓存和日志：%1%n%n选择"否"会保留这些数据，之后重新安装 BentoDesk 时仍可继续使用。
chinesesimplified.DependencyDownloadTitle=正在准备 BentoDesk 运行环境
chinesesimplified.DependencyDownloadSubtitle=正在下载缺少的运行时依赖。
chinesesimplified.DependencyInstallTitle=正在准备 BentoDesk 运行环境
chinesesimplified.DependencyInstallSubtitle=正在安装缺少的运行时依赖。
chinesesimplified.DownloadingDotNet=正在下载 .NET 10 Runtime x64...
chinesesimplified.DownloadingWinAppRuntime=正在下载 Windows App Runtime 2.2 x64...
chinesesimplified.InstallingDependency=正在安装 %1...%n这可能需要几分钟，请勿关闭此窗口。
chinesesimplified.NeedsRestart=运行时依赖已安装，但 Windows 需要重启。请重启电脑后重新运行 BentoDesk 安装程序。

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[InstallDelete]
Type: files; Name: "{userdesktop}\{#MyAppName}.lnk"; Tasks: desktopicon
Type: filesandordirs; Name: "{app}\Microsoft.WindowsAppRuntime"
Type: files; Name: "{app}\Microsoft.WinUI.dll"
Type: files; Name: "{app}\Microsoft.Windows.SDK.NET.dll"
Type: files; Name: "{app}\DirectML.dll"
Type: files; Name: "{app}\onnxruntime.dll"

[Files]
Source: "{#MyAppReleaseDir}\*"; DestDir: "{app}"; Excludes: "BentoDesk.Updater.*"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#MyAppReleaseDir}\BentoDesk.Updater.*"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{userprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\bentodesk.ico"
Name: "{userdesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\bentodesk.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent runasoriginaluser

#include "BentoDesk.Dependencies.iss"
#include "BentoDesk.Uninstall.iss"
