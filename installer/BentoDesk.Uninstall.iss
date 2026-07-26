[Code]
const
  BentoDeskProcessName = 'BentoDesk.exe';
  BentoDeskLocalAppDataRoot = '{localappdata}\BentoDesk';
  BentoDeskStartupRunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';

var
  ShouldRemoveLocalAppData: Boolean;

function ConfirmRemoveLocalAppData: Boolean;
var
  AppDataRoot: string;
  MessageText: string;
begin
  Result := False;
  AppDataRoot := ExpandConstant(BentoDeskLocalAppDataRoot);

  if not DirExists(AppDataRoot) then
    Exit;

  MessageText :=
    Format(ExpandConstant('{cm:ConfirmRemoveAppData}'), [AppDataRoot]);

  Result := MsgBox(MessageText, mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES;
end;

procedure StopBentoDeskProcess;
var
  ResultCode: Integer;
begin
  Log('正在停止 BentoDesk 进程。');
  Exec(
    ExpandConstant('{sys}\taskkill.exe'),
    '/IM ' + BentoDeskProcessName + ' /T /F',
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode);

  Log('taskkill 返回代码：' + IntToStr(ResultCode));
end;

procedure RemoveStartupRegistryEntry;
var
  Value: string;
  StartupShortcutPath: string;
begin
  if RegQueryStringValue(HKEY_CURRENT_USER, BentoDeskStartupRunKey, 'BentoDesk', Value) then
  begin
    if RegDeleteValue(HKEY_CURRENT_USER, BentoDeskStartupRunKey, 'BentoDesk') then
      Log('BentoDesk uninstall removed startup registry entry.')
    else
      Log('BentoDesk uninstall failed to remove startup registry entry.');
  end;

  // Also remove the legacy startup folder shortcut.
  StartupShortcutPath := ExpandConstant('{userstartup}\BentoDesk.lnk');
  if FileExists(StartupShortcutPath) then
  begin
    if DeleteFile(StartupShortcutPath) then
      Log('BentoDesk uninstall removed legacy startup shortcut.')
    else
      Log('BentoDesk uninstall failed to remove legacy startup shortcut.');
  end;
end;

procedure RemoveLocalAppDataRoot;
var
  AppDataRoot: string;
begin
  AppDataRoot := ExpandConstant(BentoDeskLocalAppDataRoot);
  if DirExists(AppDataRoot) then
  begin
    if DelTree(AppDataRoot, True, True, True) then
      Log('BentoDesk uninstall removed local app data directory: ' + AppDataRoot)
    else
      Log('BentoDesk uninstall failed to remove local app data directory: ' + AppDataRoot);
  end;
end;

procedure RemoveTaskbarPinnedShortcut;
var
  Path: string;
begin
  Path := ExpandConstant('{userappdata}\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar\BentoDesk.lnk');
  if FileExists(Path) then
  begin
    if DeleteFile(Path) then
      Log('BentoDesk uninstall removed taskbar pinned shortcut.')
    else
      Log('BentoDesk uninstall failed to remove taskbar pinned shortcut.');
  end;
end;

procedure RemoveAppCompatFlag;
var
  ExePath: string;
  Value: string;
begin
  ExePath := ExpandConstant('{app}\BentoDesk.exe');
  if RegQueryStringValue(HKEY_CURRENT_USER, BentoDeskAppCompatLayersKey, ExePath, Value) then
  begin
    if RegDeleteValue(HKEY_CURRENT_USER, BentoDeskAppCompatLayersKey, ExePath) then
      Log('BentoDesk uninstall removed AppCompat value: ' + ExePath)
    else
      Log('BentoDesk uninstall failed to remove AppCompat value: ' + ExePath);
  end;
end;

function InitializeUninstall: Boolean;
begin
  Result := True;
  ShouldRemoveLocalAppData := ConfirmRemoveLocalAppData;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    StopBentoDeskProcess;

  if CurUninstallStep = usPostUninstall then
  begin
    RemoveStartupRegistryEntry;
    RemoveTaskbarPinnedShortcut;
    RemoveAppCompatFlag;
    if ShouldRemoveLocalAppData then
      RemoveLocalAppDataRoot
    else
      Log('BentoDesk uninstall kept local app data directory.');
  end;
end;
