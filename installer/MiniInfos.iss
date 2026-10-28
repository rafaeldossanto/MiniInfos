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
AppComments={cm:AppDescription}
; Per user, no administrator: %LOCALAPPDATA%\Programs\MiniInfos, where /miniinfos looks for it.
PrivilegesRequired=lowest
DefaultDirName={autopf}\{#AppName}
DisableDirPage=yes
DisableProgramGroupPage=yes
OutputDir=dist
OutputBaseFilename=MiniInfos-Setup
SetupIconFile=..\widget\Assets\MiniInfos.ico
UninstallDisplayIcon={app}\MiniInfos.exe
UninstallDisplayName={#AppName}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; Its window only hides on close; the code below asks it to quit instead.
CloseApplications=no
LanguageDetectionMethod=uilanguage
ShowLanguageDialog=no
UsePreviousLanguage=no

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "ptbr"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[CustomMessages]
en.AppDescription=Your AI tokens and limits
ptbr.AppDescription=Limites e tokens das suas IAs
en.WithApps=Open with your AI apps (start hidden with Windows)
ptbr.WithApps=Abrir junto com seus apps de IA (inicia com o Windows escondido)
en.ClaudeMod=Install the Claude Code chat mod (five-hour and weekly limits, current chat and the /miniinfos command)
ptbr.ClaudeMod=Instalar o mod nos chats do Claude Code (limites de 5h e semanal, chat atual e o comando /miniinfos)
en.OpenNow=Open MiniInfos now
ptbr.OpenNow=Abrir o MiniInfos agora
en.ModMissingTitle=MiniInfos was installed, but the Claude Code mod was not.
ptbr.ModMissingTitle=O MiniInfos foi instalado, mas o mod do Claude Code não.
en.ModMissingReason=Claude Code was not found on this computer, or it rejected the mod. Token counts work normally; five-hour and weekly limits and the current chat require the mod.
ptbr.ModMissingReason=Não achei o Claude Code neste computador, ou ele recusou o mod. Os tokens aparecem normalmente; os limites de 5h e semanal e o chat atual só aparecem com o mod.
en.ModMissingAction=Install Claude Code (the desktop or terminal app) and run this installer again.
ptbr.ModMissingAction=Instale o Claude Code (o app desktop ou o do terminal) e rode este instalador de novo.

[Tasks]
Name: "withclaude"; Description: "{cm:WithApps}"
Name: "claudemod"; Description: "{cm:ClaudeMod}"

[InstallDelete]
; A reinstall with the startup option unticked takes the old entry away.
Type: files; Name: "{userstartup}\{#AppName}.lnk"

[Files]
Source: "{#Source}\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#Source}\claude-plugin\*"; DestDir: "{app}\claude-plugin"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\{#AppName}"; Filename: "{app}\MiniInfos.exe"; Comment: "{cm:AppDescription}"
Name: "{userstartup}\{#AppName}"; Filename: "{app}\MiniInfos.exe"; Parameters: "--with-claude"; Tasks: withclaude

[Run]
Filename: "{app}\MiniInfos.exe"; Description: "{cm:OpenNow}"; Flags: nowait postinstall skipifsilent
; An update from the app runs silently with /RELAUNCH=1: the new version opens when it is done.
Filename: "{app}\MiniInfos.exe"; Flags: nowait; Check: IsRelaunch

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

// The app's own update asks for the new version to open at the end (see Updater.InstallerArguments).
function IsRelaunch(): Boolean;
begin
  Result := ExpandConstant('{param:RELAUNCH|0}') = '1';
end;

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

// Keeps Candidate in BundledPath when it is the newest copy of Claude Code seen so far.
procedure KeepIfNewest(Candidate: String; Written: TFileTime);
begin
  if (BundledPath = '') or (Written.dwHighDateTime > BundledHigh)
    or ((Written.dwHighDateTime = BundledHigh) and (Written.dwLowDateTime > BundledLow)) then
  begin
    BundledPath := Candidate;
    BundledHigh := Written.dwHighDateTime;
    BundledLow := Written.dwLowDateTime;
  end;
end;

// The desktop app's copies: <Base>\<version>\<build>\claude.exe.
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
              KeepIfNewest(Candidate, Build.LastWriteTime);
          until not FindNext(Build);
        finally
          FindClose(Build);
        end;
    until not FindNext(Version);
  finally
    FindClose(Version);
  end;
end;

// The editor extension's own copy, which it never puts on PATH:
// <Extensions>\anthropic.claude-code-<version>-<platform>\resources\native-binary\claude.exe.
procedure FindNewestExtensionClaude(Extensions: String);
var
  Candidate: String;
  Extension: TFindRec;
begin
  if not FindFirst(Extensions + '\anthropic.claude-code-*', Extension) then exit;
  try
    repeat
      Candidate := Extensions + '\' + Extension.Name + '\resources\native-binary\claude.exe';
      if (Extension.Attributes and FILE_ATTRIBUTE_DIRECTORY <> 0) and FileExists(Candidate) then
        KeepIfNewest(Candidate, Extension.LastWriteTime);
    until not FindNext(Extension);
  finally
    FindClose(Extension);
  end;
end;

// A Claude Code installed outside PATH, or before PATH reached this setup. The native installer's
// %USERPROFILE%\.local\bin\claude.exe and npm's claude.cmd first, then the newest copy the desktop app
// carries (the Store app in its package's own folder, the other installer in %APPDATA%\Claude) or an
// editor extension carries (VS Code and its forks, the folders InstalledApps.EditorFolders names).
function InstalledClaude(): String;
var
  Package: TFindRec;
  Packages, Profile: String;
begin
  Profile := ExpandConstant('{%USERPROFILE}');
  Result := Profile + '\.local\bin\claude.exe';
  if FileExists(Result) then exit;
  Result := ExpandConstant('{userappdata}\npm\claude.cmd');
  if FileExists(Result) then exit;

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
  if BundledPath = '' then
  begin
    FindNewestExtensionClaude(Profile + '\.vscode\extensions');
    FindNewestExtensionClaude(Profile + '\.vscode-insiders\extensions');
    FindNewestExtensionClaude(Profile + '\.vscode-oss\extensions');
    FindNewestExtensionClaude(Profile + '\.cursor\extensions');
    FindNewestExtensionClaude(Profile + '\.windsurf\extensions');
  end;

  Result := BundledPath;
end;

// Runs a `claude` command: the one on PATH (a terminal install), else one installed elsewhere. -1 when none ran.
function RunClaude(Arguments: String): Integer;
var
  Claude: String;
  Code: Integer;
begin
  Result := -1;
  if Exec(ExpandConstant('{cmd}'), '/c where claude', '', SW_HIDE, ewWaitUntilTerminated, Code) and (Code = 0) then
  begin
    if Exec(ExpandConstant('{cmd}'), '/c claude ' + Arguments, '', SW_HIDE, ewWaitUntilTerminated, Code) then
      Result := Code;
    exit;
  end;

  Claude := InstalledClaude();
  if Claude = '' then exit;
  // npm's claude.cmd is a script: cmd runs it, keeping the quotes around its path and arguments.
  if CompareText(ExtractFileExt(Claude), '.cmd') = 0 then
  begin
    if Exec(ExpandConstant('{cmd}'), '/c ""' + Claude + '" ' + Arguments + '"', '', SW_HIDE, ewWaitUntilTerminated, Code) then
      Result := Code;
  end
  else if Exec(Claude, Arguments, '', SW_HIDE, ewWaitUntilTerminated, Code) then
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
      MsgBox(CustomMessage('ModMissingTitle') + #13#10#13#10 +
        CustomMessage('ModMissingReason') + #13#10#13#10 + CustomMessage('ModMissingAction'),
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
