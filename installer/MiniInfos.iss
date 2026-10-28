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
en.SearchingClaude=Looking for Claude Code on this computer to install the mod...
ptbr.SearchingClaude=Procurando o Claude Code neste computador para instalar o mod...

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

// Runs MiniInfos for one of the installer's jobs (see App.xaml.cs), waiting for its answer. -1 when it did not run.
function RunApp(Argument: String): Integer;
var
  Code: Integer;
begin
  Result := -1;
  if Exec(ExpandConstant('{app}\MiniInfos.exe'), Argument, '', SW_HIDE, ewWaitUntilTerminated, Code) then
    Result := Code;
end;

procedure RemoveMod();
begin
  RunApp('--remove-mod');
end;

// The mod goes in the user's Claude Code settings, which every install of Claude Code on this account reads. The app
// looks for one on PATH, then through every file of every local drive, and registers it with the first that takes it.
procedure InstallMod();
begin
  WizardForm.StatusLabel.Caption := CustomMessage('SearchingClaude');
  IsModMissing := RunApp('--install-mod') <> 0;
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
