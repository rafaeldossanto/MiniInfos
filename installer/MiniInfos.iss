; The MiniInfos installer: build it with build.ps1, which first lays the app and the mod in out\.

#define AppName "MiniInfos"
; The version lives in widget\MiniInfos.csproj alone: build.ps1 passes it in.
#ifndef AppVersion
  #error Build with build.ps1, which passes the version from widget\MiniInfos.csproj.
#endif
#define Source "out"
; Who "Adicionar ou remover programas" names: build.ps1 passes MINIINFOS_PUBLISHER when it is set.
#ifndef AppPublisher
  #define AppPublisher "MiniInfos"
#endif

[Setup]
AppId={{8E3C1F2A-6B5D-4C7E-9A1F-2D4B6E8C0A13}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppComments=Limites e tokens do Claude e do Codex
; Per user, no administrator: %LOCALAPPDATA%\Programs\MiniInfos, where /miniinfos looks for it.
PrivilegesRequired=lowest
DefaultDirName={autopf}\{#AppName}
DisableDirPage=yes
DisableProgramGroupPage=yes
OutputDir=dist
OutputBaseFilename=MiniInfos-Setup
SetupIconFile=..\widget\MiniInfos.ico
UninstallDisplayIcon={app}\MiniInfos.exe
UninstallDisplayName={#AppName}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; Its window only hides on close; the code below asks it to quit instead.
CloseApplications=no

[Languages]
Name: "ptbr"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "withclaude"; Description: "Abrir junto com Claude ou Codex (inicia com o Windows escondido)"
Name: "claudemod"; Description: "Instalar o mod nos chats do Claude Code (limites de 5h e semanal, chat atual e o comando /miniinfos)"

[InstallDelete]
; A reinstall with the startup option unticked takes the old entry away.
Type: files; Name: "{userstartup}\{#AppName}.lnk"

[Files]
Source: "{#Source}\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#Source}\claude-plugin\*"; DestDir: "{app}\claude-plugin"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\{#AppName}"; Filename: "{app}\MiniInfos.exe"; Comment: "Limites e tokens do Claude e do Codex"
Name: "{userstartup}\{#AppName}"; Filename: "{app}\MiniInfos.exe"; Parameters: "--with-claude"; Tasks: withclaude

[Run]
Filename: "{app}\MiniInfos.exe"; Description: "Abrir o MiniInfos agora"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; The startup shortcut, also when the settings window made it rather than the installer.
Type: files; Name: "{userstartup}\{#AppName}.lnk"
Type: filesandordirs; Name: "{localappdata}\MiniInfos"
Type: filesandordirs; Name: "{%USERPROFILE}\.claude\miniinfos"

[Code]
const
  MarketplaceName = 'miniinfos';
  PluginId = 'miniinfos@miniinfos';

var
  IsModMissing: Boolean;

// Asks the running MiniInfos to quit (its window only hides on close), then makes sure.
procedure QuitRunningApp();
var
  Exe: String;
  Code: Integer;
begin
  Exe := ExpandConstant('{app}\MiniInfos.exe');
  if FileExists(Exe) then
  begin
    Exec(Exe, '--quit', '', SW_HIDE, ewWaitUntilTerminated, Code);
    Sleep(1500);
  end;
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM MiniInfos.exe', '', SW_HIDE, ewWaitUntilTerminated, Code);
end;

var
  BundledPath: String;
  BundledHigh, BundledLow: Cardinal;

// Keeps the newest <Base>\<version>\<build>\claude.exe seen so far in BundledPath.
procedure FindNewestClaudeUnder(Base: String);
var
  Candidate: String;
  Version, Build: TFindRec;
begin
  if not FindFirst(Base + '\*', Version) then exit;
  try
    repeat
      if (Version.Name <> '.') and (Version.Name <> '..') and (Version.Attributes and FILE_ATTRIBUTE_DIRECTORY <> 0) then
        if FindFirst(Base + '\' + Version.Name + '\*', Build) then
        try
          repeat
            Candidate := Base + '\' + Version.Name + '\' + Build.Name + '\claude.exe';
            if (Build.Name <> '.') and (Build.Name <> '..') and FileExists(Candidate) then
              if (BundledPath = '') or (Build.LastWriteTime.dwHighDateTime > BundledHigh)
                or ((Build.LastWriteTime.dwHighDateTime = BundledHigh) and (Build.LastWriteTime.dwLowDateTime > BundledLow)) then
              begin
                BundledPath := Candidate;
                BundledHigh := Build.LastWriteTime.dwHighDateTime;
                BundledLow := Build.LastWriteTime.dwLowDateTime;
              end;
          until not FindNext(Build);
        finally
          FindClose(Build);
        end;
    until not FindNext(Version);
  finally
    FindClose(Version);
  end;
end;

// The Claude Code the desktop app carries, newest first. The Store app keeps it in its package's
// own folder (Packages\Claude_*\LocalCache\Roaming\Claude), the other installer in %APPDATA%\Claude.
function BundledClaude(): String;
var
  Package: TFindRec;
  Packages: String;
begin
  BundledPath := '';
  FindNewestClaudeUnder(ExpandConstant('{userappdata}\Claude\claude-code'));

  Packages := ExpandConstant('{localappdata}\Packages');
  if FindFirst(Packages + '\Claude_*', Package) then
  try
    repeat
      FindNewestClaudeUnder(Packages + '\' + Package.Name + '\LocalCache\Roaming\Claude\claude-code');
    until not FindNext(Package);
  finally
    FindClose(Package);
  end;

  Result := BundledPath;
end;

// Runs a `claude` command: the one on PATH (a terminal install), else the desktop app's. -1 when none ran.
function RunClaude(Arguments: String): Integer;
var
  Bundled: String;
  Code: Integer;
begin
  Result := -1;
  if Exec(ExpandConstant('{cmd}'), '/c where claude', '', SW_HIDE, ewWaitUntilTerminated, Code) and (Code = 0) then
  begin
    if Exec(ExpandConstant('{cmd}'), '/c claude ' + Arguments, '', SW_HIDE, ewWaitUntilTerminated, Code) then
      Result := Code;
    exit;
  end;

  Bundled := BundledClaude();
  if (Bundled <> '') and Exec(Bundled, Arguments, '', SW_HIDE, ewWaitUntilTerminated, Code) then
    Result := Code;
end;

procedure RemoveMod();
begin
  RunClaude('plugin uninstall ' + PluginId + ' --scope user');
  RunClaude('plugin marketplace remove ' + MarketplaceName);
end;

// The mod goes in the user's Claude Code settings, which every install of Claude Code on this account reads.
procedure InstallMod();
begin
  RemoveMod();
  IsModMissing :=
    (RunClaude('plugin marketplace add "' + ExpandConstant('{app}\claude-plugin') + '"') <> 0)
    or (RunClaude('plugin install ' + PluginId + ' --scope user') <> 0);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  QuitRunningApp();
  Result := '';
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and WizardIsTaskSelected('claudemod') then
  begin
    InstallMod();
    if IsModMissing and not WizardSilent() then
      MsgBox('O MiniInfos foi instalado, mas o mod do Claude Code não.' + #13#10#13#10 +
        'Não achei o Claude Code neste computador, ou ele recusou o mod. Os tokens aparecem normalmente; ' +
        'os limites de 5h e semanal e o chat atual só aparecem com o mod.' + #13#10#13#10 +
        'Instale o Claude Code (o app desktop ou o do terminal) e rode este instalador de novo.',
        mbInformation, MB_OK);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    QuitRunningApp();
    RemoveMod();
  end;
end;
