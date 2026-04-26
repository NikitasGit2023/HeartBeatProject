; =============================================================================
; HeartBeat Monitor — Inno Setup Installer
; Requires: Inno Setup 6.3+
; =============================================================================

#define AppName        "HeartBeat Monitor"
#define AppVersion     "1.0.0"
#define AppPublisher   "HeartBeat"
#define AppId          "{{A3F8C2D1-7E4B-4F6A-9C3D-2B5E8A1F0D94}"
#define TxExe          "HeartBeatProject.Tx.exe"
#define RxExe          "HeartBeatProject.Rx.exe"
#define TxSvcName      "HeartbeatTX"
#define RxSvcName      "HeartbeatRX"
#define TxPublish      "..\publish\TX"
#define RxPublish      "..\publish\RX"
#define DefaultInstDir "C:\Heartbeat"

; ---------------------------------------------------------------------------
[Setup]
; ---------------------------------------------------------------------------
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=http://localhost
AppSupportURL=http://localhost

DefaultDirName={#DefaultInstDir}
DefaultGroupName={#AppName}
DisableDirPage=no
DisableProgramGroupPage=no

OutputDir=output
OutputBaseFilename=HeartBeatMonitor-Setup-{#AppVersion}

Compression=lzma2/ultra64
SolidCompression=yes
InternalCompressLevel=ultra

PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible

WizardStyle=modern
WizardSizePercent=120

UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\TX\{#TxExe}

; ---------------------------------------------------------------------------
[Languages]
; ---------------------------------------------------------------------------
Name: "english"; MessagesFile: "compiler:Default.isl"

; ---------------------------------------------------------------------------
[Tasks]
; ---------------------------------------------------------------------------
Name: "desktopicon";   Description: "Create a &Desktop shortcut";    GroupDescription: "Shortcuts:"; Flags: checkedonce
Name: "startmenuicon"; Description: "Create a &Start Menu shortcut"; GroupDescription: "Shortcuts:"; Flags: checkedonce

; ---------------------------------------------------------------------------
[Files]
; ---------------------------------------------------------------------------
; ---- TX payload (Check: IsTxMode guards file copy at install time) --------
Source: "publish\Tx\HeartBeatProject.Tx.exe"; DestDir: "{app}\TX"; Flags: ignoreversion; Check: IsTxMode
Source: "publish\Tx\appsettings.json"; DestDir: "{app}\TX"; Flags: ignoreversion;                                    Check: IsTxMode
Source: "publish\Tx\nlog.config";      DestDir: "{app}\TX"; Flags: ignoreversion;                                    Check: IsTxMode
Source: "publish\Tx\wwwroot\*";        DestDir: "{app}\TX\wwwroot"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: IsTxMode

; ---- RX payload ------------------------------------------------------------
Source: "publish\Rx\HeartBeatProject.Rx.exe";        DestDir: "{app}\RX"; Flags: ignoreversion;                                    Check: IsRxMode
Source: "publish\Rx\appsettings.json"; DestDir: "{app}\RX"; Flags: ignoreversion;                                    Check: IsRxMode
Source: "publish\Rx\nlog.config";      DestDir: "{app}\RX"; Flags: ignoreversion;                                    Check: IsRxMode
Source: "publish\Rx\wwwroot\*";        DestDir: "{app}\RX\wwwroot"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: IsRxMode

; ---------------------------------------------------------------------------
[Dirs]
; ---------------------------------------------------------------------------
Name: "{#DefaultInstDir}\Shared\HeartbeatFiles"
Name: "{app}\TX\Logs"; Check: IsTxMode
Name: "{app}\RX\Logs"; Check: IsRxMode

; ---------------------------------------------------------------------------
[Icons]
; ---------------------------------------------------------------------------
Name: "{autodesktop}\{#AppName} TX"; Filename: "{app}\TX\{#TxExe}"; WorkingDir: "{app}\TX"; Tasks: desktopicon;   Check: IsTxMode
Name: "{autodesktop}\{#AppName} RX"; Filename: "{app}\RX\{#RxExe}"; WorkingDir: "{app}\RX"; Tasks: desktopicon;   Check: IsRxMode
Name: "{group}\{#AppName} TX";       Filename: "{app}\TX\{#TxExe}"; WorkingDir: "{app}\TX"; Tasks: startmenuicon; Check: IsTxMode
Name: "{group}\{#AppName} RX";       Filename: "{app}\RX\{#RxExe}"; WorkingDir: "{app}\RX"; Tasks: startmenuicon; Check: IsRxMode
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"

; ---------------------------------------------------------------------------
[Registry]
; ---------------------------------------------------------------------------
; Persist install location; full settings saved by Pascal code after install
Root: HKLM; Subkey: "Software\{#AppPublisher}\{#AppName}"; \
    ValueType: string; ValueName: "InstallDir"; ValueData: "{app}"; \
    Flags: uninsdeletekey

; ---------------------------------------------------------------------------
[Run]
; ---------------------------------------------------------------------------
; "Launch application now?" checkbox on the Finish page (checked by default)
Filename: "{app}\TX\{#TxExe}"; Description: "Launch {#AppName} now"; \
    Flags: postinstall nowait skipifsilent; Check: IsTxMode
Filename: "{app}\RX\{#RxExe}"; Description: "Launch {#AppName} now"; \
    Flags: postinstall nowait skipifsilent; Check: IsRxMode

; ---------------------------------------------------------------------------
[Code]

const
  RegKey = 'Software\HeartBeat\HeartBeat Monitor';

var
  { Runtime configuration collected from wizard pages }
  GMode          : string;   { 'TX' or 'RX' }
  GTxFolder      : string;   { TX-only: folder TX writes heartbeat files into }
  GRxFolder      : string;   { RX-only: folder RX reads heartbeat files from }
  GSmtpServer    : string;
  GSmtpPort      : string;
  GSmtpUser      : string;
  GSmtpPass      : string;
  GSmtpFrom      : string;
  GSmtpTo        : string;
  GSmtpSsl       : Boolean;
  GIsReinstall   : Boolean;

  { Uninstall state }
  GKeepSettings    : Boolean; { True = preserve appsettings.json + nlog.config }
  GUninstAppDir    : string;  { Captured before uninstall log removes registry }
  GUninstModeDir   : string;  { TX or RX subdir within GUninstAppDir }
  GConfigBackupDir : string;  { Temp dir used to hold config files during removal }

  { Custom wizard page handles }
  PageMode      : TInputOptionWizardPage;
  PageTxConfig  : TInputQueryWizardPage;
  PageRxConfig  : TInputQueryWizardPage;
  PageSmtp      : TInputQueryWizardPage;
  PageSummary   : TWizardPage;
  SummaryMemo   : TNewMemo;

{ =========================================================================== }
{ Mode helpers — used by [Files]/[Dirs]/[Icons] Check: directives             }
{ =========================================================================== }

function IsTxMode: Boolean;
begin
  Result := CompareText(GMode, 'TX') = 0;
end;

function IsRxMode: Boolean;
begin
  Result := CompareText(GMode, 'RX') = 0;
end;

{ =========================================================================== }
{ Utility                                                                      }
{ =========================================================================== }

function JsonBool(B: Boolean): string;
begin
  if B then Result := 'true' else Result := 'false';
end;

function YesNo(B: Boolean): string;
begin
  if B then Result := 'Yes' else Result := 'No';
end;

{ Escape backslashes for JSON string literals (e.g. C:\foo -> C:\\foo) }
function EscapeBackslashes(S: string): string;
var I: Integer;
begin
  Result := '';
  for I := 1 to Length(S) do
    if S[I] = '\' then Result := Result + '\\'
    else Result := Result + S[I];
end;

{ Run sc.exe silently, ignore result code (errors logged by SC itself) }
procedure ExecSc(Params: string);
var RC: Integer;
begin
  Exec(ExpandConstant('{sys}\sc.exe'), Params, '', SW_HIDE,
       ewWaitUntilTerminated, RC);
end;

{ =========================================================================== }
{ Registry persistence — saves/restores all wizard inputs across installs      }
{ =========================================================================== }

procedure LoadRegistrySettings;
var S: string;
begin
  GIsReinstall := RegQueryStringValue(HKLM, RegKey, 'Mode', S) and (S <> '');
  if not GIsReinstall then Exit;

  GMode := S;
  RegQueryStringValue(HKLM, RegKey, 'TxFolder',      GTxFolder);
  RegQueryStringValue(HKLM, RegKey, 'RxFolder',      GRxFolder);
  RegQueryStringValue(HKLM, RegKey, 'SmtpServer',    GSmtpServer);
  RegQueryStringValue(HKLM, RegKey, 'SmtpPort',      GSmtpPort);
  RegQueryStringValue(HKLM, RegKey, 'SmtpUser',      GSmtpUser);
  RegQueryStringValue(HKLM, RegKey, 'SmtpPass',      GSmtpPass);
  RegQueryStringValue(HKLM, RegKey, 'SmtpFrom',      GSmtpFrom);
  RegQueryStringValue(HKLM, RegKey, 'SmtpTo',        GSmtpTo);
  if RegQueryStringValue(HKLM, RegKey, 'SmtpSsl', S) then
    GSmtpSsl := CompareText(S, 'true') = 0;
end;

procedure SaveRegistrySettings;
begin
  RegWriteStringValue(HKLM, RegKey, 'Mode',          GMode);
  RegWriteStringValue(HKLM, RegKey, 'TxFolder',      GTxFolder);
  RegWriteStringValue(HKLM, RegKey, 'RxFolder',      GRxFolder);
  RegWriteStringValue(HKLM, RegKey, 'SmtpServer',    GSmtpServer);
  RegWriteStringValue(HKLM, RegKey, 'SmtpPort',      GSmtpPort);
  RegWriteStringValue(HKLM, RegKey, 'SmtpUser',      GSmtpUser);
  RegWriteStringValue(HKLM, RegKey, 'SmtpPass',      GSmtpPass);
  RegWriteStringValue(HKLM, RegKey, 'SmtpFrom',      GSmtpFrom);
  RegWriteStringValue(HKLM, RegKey, 'SmtpTo',        GSmtpTo);
  RegWriteStringValue(HKLM, RegKey, 'SmtpSsl', JsonBool(GSmtpSsl));
end;

{ =========================================================================== }
{ appsettings.json writer — called in ssPostInstall                            }
{ =========================================================================== }

function BuildAlertsJson: string;
begin
  Result :=
    '  "Alerts": {'                                                              + #13#10 +
    '    "EnableEmail": '  + JsonBool(GSmtpServer <> '') + ','                  + #13#10 +
    '    "SmtpServer": "'  + GSmtpServer  + '",'                                + #13#10 +
    '    "Port": '         + GSmtpPort    + ','                                  + #13#10 +
    '    "From": "'        + GSmtpFrom    + '",'                                 + #13#10 +
    '    "To": "'          + GSmtpTo      + '",'                                 + #13#10 +
    '    "Username": "'    + GSmtpUser    + '",'                                 + #13#10 +
    '    "Password": "'    + GSmtpPass    + '",'                                 + #13#10 +
    '    "EnableSsl": '    + JsonBool(GSmtpSsl) + ','                           + #13#10 +
    '    "EnableSnmp": false,'                                                   + #13#10 +
    '    "SnmpHost": "",'                                                        + #13#10 +
    '    "SnmpPort": 162,'                                                       + #13#10 +
    '    "Community": "public",'                                                 + #13#10 +
    '    "EnableSyslog": false,'                                                 + #13#10 +
    '    "SyslogHost": "",'                                                      + #13#10 +
    '    "SyslogPort": 514,'                                                     + #13#10 +
    '    "SyslogFacility": "Local0"'                                             + #13#10 +
    '  }';
end;

procedure WriteAppSettings(AppDir: string);
var Path, Folder, Json: string;
begin
  if IsTxMode then
  begin
    Path   := AppDir + '\TX\appsettings.json';
    Folder := EscapeBackslashes(GTxFolder);
    Json :=
      '{'                                                                                          + #13#10 +
      '  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },' + #13#10 +
      '  "AllowedHosts": "*",'                                                                    + #13#10 +
      '  "Urls": "http://localhost:5000",'                                                         + #13#10 +
      '  "Heartbeat": {'                                                                           + #13#10 +
      '    "FolderPath": "' + Folder + '",'                                                        + #13#10 +
      '    "FileNamePrefix": "heartbeat",'                                                         + #13#10 +
      '    "IntervalSeconds": 30,'                                                                 + #13#10 +
      '    "OverwriteExisting": true,'                                                             + #13#10 +
      '    "LogFolderPath": ""'                                                                    + #13#10 +
      '  },'                                                                                       + #13#10 +
      BuildAlertsJson                                                                              + #13#10 +
      '}';
  end
  else
  begin
    Path   := AppDir + '\RX\appsettings.json';
    Folder := EscapeBackslashes(GRxFolder);
    Json :=
      '{'                                                                                          + #13#10 +
      '  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },' + #13#10 +
      '  "AllowedHosts": "*",'                                                                    + #13#10 +
      '  "Urls": "http://localhost:5002",'                                                         + #13#10 +
      '  "Heartbeat": {'                                                                           + #13#10 +
      '    "FolderPath": "' + Folder + '",'                                                        + #13#10 +
      '    "FileNamePrefix": "heartbeat",'                                                         + #13#10 +
      '    "CheckIntervalSeconds": 10,'                                                            + #13#10 +
      '    "ThresholdSeconds": 60'                                                                 + #13#10 +
      '  },'                                                                                       + #13#10 +
      BuildAlertsJson                                                                              + #13#10 +
      '}';
  end;

  SaveStringToFile(Path, Json, False);
end;

{ =========================================================================== }
{ Wizard page creation and prefill                                             }
{ =========================================================================== }

procedure InitializeWizard;
begin
  { Defaults (overwritten by LoadRegistrySettings on reinstall) }
  GMode      := 'TX';
  GSmtpPort  := '587';
  GSmtpSsl   := True;

  LoadRegistrySettings;

  { ---- Page 1: Mode ---- }
  PageMode := CreateInputOptionPage(wpWelcome,
    'Installation Type',
    'Select the station role.',
    'What role will this machine serve?',
    True, False);
  PageMode.Add('TX — Transmitter  (writes heartbeat files)');
  PageMode.Add('RX — Receiver     (monitors heartbeat files)');
  if IsRxMode then PageMode.SelectedValueIndex := 1
  else PageMode.SelectedValueIndex := 0;

  { ---- Page 2: TX Config ---- }
  PageTxConfig := CreateInputQueryPage(PageMode.ID,
    'TX Configuration',
    'Transmitter settings.',
    'Enter the folder path where heartbeat files will be written:');
  PageTxConfig.Add('Heartbeat Folder Path:', False);
  if GTxFolder <> '' then PageTxConfig.Values[0] := GTxFolder
  else PageTxConfig.Values[0] := 'C:\Heartbeat\Shared\HeartbeatFiles';

  { ---- Page 3: RX Config ---- }
  PageRxConfig := CreateInputQueryPage(PageTxConfig.ID,
    'RX Configuration',
    'Receiver settings.',
    'Enter the folder details for this RX station:');
  PageRxConfig.Add('Receive Folder Path  (RX reads files from here):', False);
  if GRxFolder <> '' then PageRxConfig.Values[0] := GRxFolder
  else PageRxConfig.Values[0] := 'C:\Heartbeat\Shared\HeartbeatFiles';

  { ---- Page 4: SMTP ---- }
  PageSmtp := CreateInputQueryPage(PageRxConfig.ID,
    'Email / SMTP Configuration',
    'Alert email settings.',
    'Leave SMTP Server blank to disable email alerts entirely:');
  PageSmtp.Add('SMTP Server:', False);
  PageSmtp.Add('SMTP Port:', False);
  PageSmtp.Add('Sender Email Address (From):', False);
  PageSmtp.Add('Recipient Address(es) — comma or semicolon separated:', False);
  PageSmtp.Add('SMTP Username:', False);
  PageSmtp.Add('SMTP Password:', True);   { True = mask with asterisks }
  PageSmtp.Values[0] := GSmtpServer;
  PageSmtp.Values[1] := GSmtpPort;
  PageSmtp.Values[2] := GSmtpFrom;
  PageSmtp.Values[3] := GSmtpTo;
  PageSmtp.Values[4] := GSmtpUser;
  PageSmtp.Values[5] := GSmtpPass;

  { ---- Page 5: Summary (confirmation before copy) ---- }
  PageSummary := CreateCustomPage(PageSmtp.ID,
    'Installation Summary',
    'Review your configuration before installing.');
  SummaryMemo := TNewMemo.Create(WizardForm);
  SummaryMemo.Parent     := PageSummary.Surface;
  SummaryMemo.Left       := 0;
  SummaryMemo.Top        := 0;
  SummaryMemo.Width      := PageSummary.SurfaceWidth;
  SummaryMemo.Height     := PageSummary.SurfaceHeight;
  SummaryMemo.ScrollBars := ssVertical;
  SummaryMemo.ReadOnly   := True;
  SummaryMemo.Text       := 'Complete all pages then click Next to review settings.';
end;

{ =========================================================================== }
{ Page navigation                                                              }
{ =========================================================================== }

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
  { Skip TX config page when RX mode, and vice-versa }
  if PageID = PageTxConfig.ID then Result := IsRxMode;
  if PageID = PageRxConfig.ID then Result := IsTxMode;
end;

{ =========================================================================== }
{ Summary page content builder                                                 }
{ =========================================================================== }

procedure UpdateSummary;
var S: string;
begin
  S := '=== MODE: ' + GMode + ' ===' + #13#10 + #13#10;

  if IsTxMode then
    S := S + 'TX Folder Path  : ' + GTxFolder + #13#10
  else
    S := S + 'RX Folder Path  : ' + GRxFolder + #13#10;

  S := S + #13#10 + '--- Email / SMTP ---' + #13#10;
  if GSmtpServer = '' then
    S := S + '(disabled — SMTP Server not set)' + #13#10
  else
  begin
    S := S + 'Server   : ' + GSmtpServer        + #13#10;
    S := S + 'Port     : ' + GSmtpPort          + #13#10;
    S := S + 'From     : ' + GSmtpFrom          + #13#10;
    S := S + 'To       : ' + GSmtpTo            + #13#10;
    S := S + 'Username : ' + GSmtpUser          + #13#10;
    S := S + 'SSL/TLS  : ' + YesNo(GSmtpSsl)   + #13#10;
  end;

  S := S + #13#10 + 'Install directory : ' + WizardDirValue;

  SummaryMemo.Text := S;
end;

{ =========================================================================== }
{ Per-page validation and variable capture                                     }
{ =========================================================================== }

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;

  if CurPageID = PageMode.ID then
  begin
    if PageMode.SelectedValueIndex = 0 then GMode := 'TX'
    else GMode := 'RX';
  end

  else if CurPageID = PageTxConfig.ID then
  begin
    GTxFolder := Trim(PageTxConfig.Values[0]);
    if GTxFolder = '' then
    begin
      MsgBox('Please enter the heartbeat folder path for this TX station.',
             mbError, MB_OK);
      Result := False; Exit;
    end;
  end

  else if CurPageID = PageRxConfig.ID then
  begin
    GRxFolder := Trim(PageRxConfig.Values[0]);
    if GRxFolder = '' then
    begin
      MsgBox('Please enter the receive folder path for this RX station.',
             mbError, MB_OK);
      Result := False; Exit;
    end;
  end

  else if CurPageID = PageSmtp.ID then
  begin
    GSmtpServer := Trim(PageSmtp.Values[0]);
    GSmtpPort   := Trim(PageSmtp.Values[1]);
    GSmtpFrom   := Trim(PageSmtp.Values[2]);
    GSmtpTo     := Trim(PageSmtp.Values[3]);
    GSmtpUser   := Trim(PageSmtp.Values[4]);
    GSmtpPass   :=      PageSmtp.Values[5];   { do not trim passwords }
    if GSmtpPort = '' then GSmtpPort := '587';
    { Auto-detect SSL from port — no separate SSL page needed }
    if (GSmtpPort = '587') or (GSmtpPort = '465') then GSmtpSsl := True
    else if GSmtpPort = '25' then GSmtpSsl := False
    else GSmtpSsl := True;
    if (GSmtpServer <> '') and (GSmtpFrom = '') then
    begin
      MsgBox('Please enter the Sender Email Address (From).', mbError, MB_OK);
      Result := False; Exit;
    end;
    if (GSmtpServer <> '') and (GSmtpTo = '') then
    begin
      MsgBox('Please enter a Recipient Email Address (To).', mbError, MB_OK);
      Result := False; Exit;
    end;
  end;
end;

{ Refresh GMode from the radio button whenever the user navigates back }
procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = PageMode.ID then
  begin
    if PageMode.SelectedValueIndex = 0 then GMode := 'TX'
    else GMode := 'RX';
  end;

  if CurPageID = PageSummary.ID then
    UpdateSummary;
end;

{ =========================================================================== }
{ Post-install: write config, register & start service                         }
{ =========================================================================== }

procedure CurStepChanged(CurStep: TSetupStep);
var AppDir, ExePath, SvcName, SvcDisplay: string;
begin
  { Stop the old service before Inno Setup overwrites the executable }
  if CurStep = ssInstall then
  begin
    if IsTxMode then SvcName := '{#TxSvcName}'
    else SvcName := '{#RxSvcName}';
    ExecSc('stop ' + SvcName);
    Sleep(2000);
  end;

  if CurStep = ssPostInstall then
  begin
    AppDir := ExpandConstant('{app}');

    if IsTxMode then
    begin
      ExePath    := AppDir + '\TX\{#TxExe}';
      SvcName    := '{#TxSvcName}';
      SvcDisplay := '{#AppName} TX';
    end
    else
    begin
      ExePath    := AppDir + '\RX\{#RxExe}';
      SvcName    := '{#RxSvcName}';
      SvcDisplay := '{#AppName} RX';
    end;

    { Write user-configured appsettings.json, overwriting the published default }
    WriteAppSettings(AppDir);

    { Remove stale service entry (no-op on fresh install, needed on update) }
    ExecSc('delete ' + SvcName);
    Sleep(500);

    { Register as auto-start Windows Service }
    ExecSc('create ' + SvcName +
           ' binPath= "' + ExePath + '"' +
           ' start= auto' +
           ' DisplayName= "' + SvcDisplay + '"');

    { Start immediately — user need not reboot }
    ExecSc('start ' + SvcName);

    { Persist all wizard inputs so the next install can prefill them }
    SaveRegistrySettings;
  end;
end;

{ =========================================================================== }
{ Uninstaller — prompt for settings retention, stop/delete service, preserve  }
{ or discard appsettings.json + nlog.config based on user choice               }
{ =========================================================================== }

function InitializeUninstall: Boolean;
var S: string; Answer: Integer;
begin
  Result := True;

  { Determine which mode is installed }
  GMode := 'TX';
  if RegQueryStringValue(HKLM, RegKey, 'Mode', S) and (S <> '') then
    GMode := S;

  { Ask whether to keep configuration files }
  Answer := MsgBox(
    'Do you want to keep your existing settings?' + #13#10 + #13#10 +
    'Yes  —  removes binaries and the Windows service.' + #13#10 +
    '         Preserves appsettings.json and nlog.config.' + #13#10 + #13#10 +
    'No   —  removes everything, including all configuration files.',
    mbConfirmation, MB_YESNO);
  GKeepSettings := (Answer = IDYES);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var SvcName: string;
begin
  if CurUninstallStep = usUninstall then
  begin
    { Stop and delete the Windows service for the installed mode }
    if IsTxMode then SvcName := '{#TxSvcName}'
    else SvcName := '{#RxSvcName}';
    ExecSc('stop '   + SvcName);
    Sleep(2000);
    ExecSc('delete ' + SvcName);

    { Capture install paths now — {app} is still valid; our registry key may be
      removed later when the uninstall log processes [Registry] entries.        }
    GUninstAppDir  := ExpandConstant('{app}');
    if IsTxMode then GUninstModeDir := GUninstAppDir + '\TX'
    else             GUninstModeDir := GUninstAppDir + '\RX';

    { Back up config files to a temp folder before the uninstall log deletes them }
    if GKeepSettings then
    begin
      GConfigBackupDir := ExpandConstant('{tmp}') + '\HeartBeatCfgBackup';
      CreateDir(GConfigBackupDir);
      FileCopy(GUninstModeDir + '\appsettings.json',
               GConfigBackupDir + '\appsettings.json', False);
      FileCopy(GUninstModeDir + '\nlog.config',
               GConfigBackupDir + '\nlog.config', False);
    end;
  end;

  if CurUninstallStep = usPostUninstall then
  begin
    { Restore backed-up config files after the uninstall log has finished }
    if GKeepSettings and (GConfigBackupDir <> '') then
    begin
      CreateDir(GUninstAppDir);
      CreateDir(GUninstModeDir);
      FileCopy(GConfigBackupDir + '\appsettings.json',
               GUninstModeDir   + '\appsettings.json', False);
      FileCopy(GConfigBackupDir + '\nlog.config',
               GUninstModeDir   + '\nlog.config', False);
    end;
  end;
end;
