[Code]
const
  BentoDeskAdminCleanupParam = '/ADMINCLEANUP=';
  BentoDeskLegacyUninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{B3E7D4A1-8C29-4F56-9E1B-7A2D05C84F33}_is1';
  BentoDeskLegacyWowUninstallKey = 'Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{B3E7D4A1-8C29-4F56-9E1B-7A2D05C84F33}_is1';
  BentoDeskAppCompatLayersKey = 'Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers';
  BentoDeskLegacyExeName = 'BentoDesk.exe';

var
  IsMigrationAdminCleanupMode: Boolean;
  MigrationAdminCleanupPath: string;

procedure ExitProcess(ExitCode: Integer);
  external 'ExitProcess@kernel32.dll stdcall';

function NormalizeDirPath(Path: string): string;
begin
  Result := RemoveBackslashUnlessRoot(ExpandConstant(Path));
end;

function IsDefaultProgramFilesBentoDeskPath(Path: string): Boolean;
var
  NormalizedPath: string;
  ProgramFilesPath: string;
  ProgramFilesX86Path: string;
begin
  NormalizedPath := NormalizeDirPath(Path);
  ProgramFilesPath := NormalizeDirPath('{pf}\BentoDesk');
  ProgramFilesX86Path := NormalizeDirPath('{pf32}\BentoDesk');

  Result :=
    (CompareText(NormalizedPath, ProgramFilesPath) = 0) or
    (CompareText(NormalizedPath, ProgramFilesX86Path) = 0);
end;

function IsLegacyInstallPath(Path: string): Boolean;
begin
  Result :=
    (Path <> '') and
    IsDefaultProgramFilesBentoDeskPath(Path) and
    FileExists(AddBackslash(Path) + BentoDeskLegacyExeName);
end;

function TryReadLegacyInstallPathFromRegistry(var InstallPath: string): Boolean;
begin
  Result := False;
  InstallPath := '';

  if RegQueryStringValue(HKEY_LOCAL_MACHINE, BentoDeskLegacyUninstallKey, 'InstallLocation', InstallPath) and
     IsLegacyInstallPath(InstallPath) then
  begin
    Result := True;
    Exit;
  end;

  InstallPath := '';
  if RegQueryStringValue(HKEY_LOCAL_MACHINE, BentoDeskLegacyWowUninstallKey, 'InstallLocation', InstallPath) and
     IsLegacyInstallPath(InstallPath) then
  begin
    Result := True;
    Exit;
  end;
end;

function TryDetectLegacyInstallPath(var InstallPath: string): Boolean;
var
  CandidatePath: string;
begin
  Result := False;
  InstallPath := '';

  if TryReadLegacyInstallPathFromRegistry(InstallPath) then
  begin
    Result := True;
    Exit;
  end;

  CandidatePath := ExpandConstant('{pf}\BentoDesk');
  if IsLegacyInstallPath(CandidatePath) then
  begin
    InstallPath := CandidatePath;
    Result := True;
    Exit;
  end;

  CandidatePath := ExpandConstant('{pf32}\BentoDesk');
  if IsLegacyInstallPath(CandidatePath) then
  begin
    InstallPath := CandidatePath;
    Result := True;
    Exit;
  end;
end;

function TryReadAdminCleanupMode: Boolean;
var
  I: Integer;
  Param: string;
begin
  Result := False;
  MigrationAdminCleanupPath := '';

  for I := 1 to ParamCount do
  begin
    Param := ParamStr(I);
    if CompareText(Copy(Param, 1, Length(BentoDeskAdminCleanupParam)), BentoDeskAdminCleanupParam) = 0 then
    begin
      MigrationAdminCleanupPath := Copy(Param, Length(BentoDeskAdminCleanupParam) + 1, MaxInt);
      Result := IsLegacyInstallPath(MigrationAdminCleanupPath);
      Exit;
    end;
  end;
end;

procedure StopLegacyBentoDeskProcess;
var
  ResultCode: Integer;
begin
  Exec(
    ExpandConstant('{sys}\taskkill.exe'),
    '/IM BentoDesk.exe /T /F',
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode);
  Log('BentoDesk migration taskkill exit code: ' + IntToStr(ResultCode));
end;

procedure DeleteShortcutIfExists(Path: string);
begin
  if FileExists(Path) then
  begin
    if DeleteFile(Path) then
      Log('BentoDesk migration deleted shortcut: ' + Path)
    else
      Log('BentoDesk migration failed to delete shortcut: ' + Path);
  end;
end;

procedure DeleteAppCompatLayerValue(RootKey: Integer; ExePath: string);
var
  Value: string;
begin
  if ExePath = '' then
    Exit;

  if RegQueryStringValue(RootKey, BentoDeskAppCompatLayersKey, ExePath, Value) then
  begin
    if Pos('RUNASADMIN', Uppercase(Value)) > 0 then
    begin
      if RegDeleteValue(RootKey, BentoDeskAppCompatLayersKey, ExePath) then
        Log('BentoDesk migration removed AppCompat RUNASADMIN value: ' + ExePath)
      else
        Log('BentoDesk migration failed to remove AppCompat value: ' + ExePath);
    end;
  end;
end;

procedure CleanupCurrentUserAppCompatFlags(LegacyInstallPath: string);
begin
  if LegacyInstallPath <> '' then
  begin
    DeleteAppCompatLayerValue(
      HKEY_CURRENT_USER,
      AddBackslash(LegacyInstallPath) + BentoDeskLegacyExeName);
  end;

  DeleteAppCompatLayerValue(
    HKEY_CURRENT_USER,
    ExpandConstant('{localappdata}\Programs\BentoDesk\BentoDesk.exe'));
end;

function PerformMigrationAdminCleanup(LegacyInstallPath: string): Boolean;
var
  LegacyExePath: string;
begin
  Result := False;

  if not IsLegacyInstallPath(LegacyInstallPath) then
  begin
    Log('BentoDesk migration rejected cleanup path: ' + LegacyInstallPath);
    Exit;
  end;

  LegacyExePath := AddBackslash(LegacyInstallPath) + BentoDeskLegacyExeName;
  StopLegacyBentoDeskProcess;

  DeleteShortcutIfExists(ExpandConstant('{commonprograms}\BentoDesk.lnk'));
  DeleteShortcutIfExists(ExpandConstant('{commondesktop}\BentoDesk.lnk'));
  DeleteShortcutIfExists(ExpandConstant('{commonstartup}\BentoDesk.lnk'));
  DeleteShortcutIfExists(ExpandConstant('{commonappdata}\Microsoft\Windows\Start Menu\Programs\BentoDesk.lnk'));
  DeleteShortcutIfExists(ExpandConstant('{commonappdata}\Microsoft\Windows\Start Menu\Programs\Startup\BentoDesk.lnk'));
  DeleteShortcutIfExists(ExpandConstant('{userprograms}\BentoDesk.lnk'));
  DeleteShortcutIfExists(ExpandConstant('{userdesktop}\BentoDesk.lnk'));
  DeleteShortcutIfExists(ExpandConstant('{userstartup}\BentoDesk.lnk'));
  DeleteShortcutIfExists(ExpandConstant('{userappdata}\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar\BentoDesk.lnk'));

  DeleteAppCompatLayerValue(HKEY_LOCAL_MACHINE, LegacyExePath);

  if RegKeyExists(HKEY_LOCAL_MACHINE, BentoDeskLegacyUninstallKey) then
    RegDeleteKeyIncludingSubkeys(HKEY_LOCAL_MACHINE, BentoDeskLegacyUninstallKey);

  if RegKeyExists(HKEY_LOCAL_MACHINE, BentoDeskLegacyWowUninstallKey) then
    RegDeleteKeyIncludingSubkeys(HKEY_LOCAL_MACHINE, BentoDeskLegacyWowUninstallKey);

  if DirExists(LegacyInstallPath) then
  begin
    if not DelTree(LegacyInstallPath, True, True, True) then
    begin
      Log('BentoDesk migration failed to remove legacy directory: ' + LegacyInstallPath);
      Log('BentoDesk migration will continue because user-scope install can still proceed.');
    end;
  end;

  Result := True;
end;

function RunMigrationAdminCleanup(LegacyInstallPath: string): Boolean;
var
  ResultCode: Integer;
  Parameters: string;
begin
  Parameters :=
    '/SP- /CURRENTUSER /VERYSILENT /SUPPRESSMSGBOXES /NORESTART "' +
    BentoDeskAdminCleanupParam + LegacyInstallPath + '"';

  Log('BentoDesk migration launching admin cleanup for: ' + LegacyInstallPath);
  if not ShellExec(
      'runas',
      ExpandConstant('{srcexe}'),
      Parameters,
      '',
      SW_SHOW,
      ewWaitUntilTerminated,
      ResultCode) then
  begin
    Log('BentoDesk migration admin cleanup could not be launched.');
    Result := False;
    Exit;
  end;

  Log('BentoDesk migration admin cleanup exit code: ' + IntToStr(ResultCode));
  Result := ResultCode = 0;
end;

function InitializeSetup: Boolean;
var
  LegacyInstallPath: string;
begin
  IsMigrationAdminCleanupMode := TryReadAdminCleanupMode;

  if IsMigrationAdminCleanupMode then
  begin
    if PerformMigrationAdminCleanup(MigrationAdminCleanupPath) then
      ExitProcess(0)
    else
      ExitProcess(1);

    Result := False;
    Exit;
  end;

  Result := True;
  if TryDetectLegacyInstallPath(LegacyInstallPath) then
  begin
    Log('BentoDesk migration detected legacy install: ' + LegacyInstallPath);
    CleanupCurrentUserAppCompatFlags(LegacyInstallPath);

    if not RunMigrationAdminCleanup(LegacyInstallPath) then
      Log('BentoDesk migration admin cleanup failed; continuing with current-user install.');

    CleanupCurrentUserAppCompatFlags(LegacyInstallPath);
  end
  else
  begin
    CleanupCurrentUserAppCompatFlags('');
  end;
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := IsMigrationAdminCleanupMode;
end;

function PrepareToInstall(var NeedsRestart: Boolean): string;
var
  ResultCode: Integer;
begin
  Result := '';

  // Explicitly terminate BentoDesk before file copy. Restart Manager alone
  // cannot always close a tray-first app with multiple top-level windows,
  // causing the "close applications" dialog to hang and eventually timeout.
  Exec(
    ExpandConstant('{sys}\taskkill.exe'),
    '/IM BentoDesk.exe /T /F',
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode);
  Log('BentoDesk taskkill exit code: ' + IntToStr(ResultCode));

  // Give the process time to fully exit before Restart Manager runs.
  Sleep(2000);

  Log('BentoDesk process termination completed.');
end;
