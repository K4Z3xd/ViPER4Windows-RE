; V4Wa — ViPER4Windows Alternative (Native WinUI 3 + Hi-Res MFX APO) Installer
; Built with Inno Setup 6

#define MyAppName "V4Wa"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "V4Wa — ViPER4Windows Alternative"
#define MyAppExeName "V4Wa.exe"

[Setup]
AppId={{8F4E2B1A-7C3D-4E9A-B12F-6A8C4D9E105A}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=output
OutputBaseFilename=V4Wa-Setup-{#MyAppVersion}-x64
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName} ({#MyAppVersion})
CloseApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "attachapo"; Description: "Attach V4Wa Hi-Res MFX APO to default audio device automatically"; GroupDescription: "Audio Engine:"
Name: "foobar"; Description: "Install foobar2000 v2.x Status Reporter plugin (bit-transparent telemetry)"; GroupDescription: "Optional Integrations:"
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; Self-contained WinUI 3 native desktop app + runtime
Source: "stage\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; Native C++23 Hi-Res APO & CLI helper
Source: "..\build\ViPERHiResAPO.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\build\viper_ctl.exe"; DestDir: "{app}"; Flags: ignoreversion

; PowerShell APO install & 1-click factory restore scripts
Source: "..\scripts\install_apo.ps1"; DestDir: "{app}\scripts"; Flags: ignoreversion
Source: "..\scripts\uninstall_apo.ps1"; DestDir: "{app}\scripts"; Flags: ignoreversion

; Optional foobar2000 v2.x Status Reporter plugin
Source: "..\build\foo_dsp_viperhires.dll"; DestDir: "{app}\foobar"; Flags: ignoreversion
Source: "..\build\foo_dsp_viperhires.dll"; DestDir: "{userappdata}\foobar2000-v2\user-components-x64\foo_dsp_viperhires"; Flags: ignoreversion; Tasks: foobar

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File ""{app}\scripts\install_apo.ps1"" -AutoDefault"; StatusMsg: "Registering V4Wa Hi-Res MFX APO and attaching to default audio device..."; Flags: runhidden waituntilterminated; Tasks: attachapo
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent runasoriginaluser

[UninstallRun]
Filename: "taskkill.exe"; Parameters: "/F /IM V4Wa.exe"; Flags: runhidden; RunOnceId: "KillV4Wa"
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File ""{app}\scripts\uninstall_apo.ps1"" -AllBackedUp -UnregisterDll"; Flags: runhidden waituntilterminated; RunOnceId: "RestoreAudioApo"

[UninstallDelete]
Type: files; Name: "{userappdata}\foobar2000-v2\user-components-x64\foo_dsp_viperhires\foo_dsp_viperhires.dll"
Type: dirifempty; Name: "{userappdata}\foobar2000-v2\user-components-x64\foo_dsp_viperhires"
