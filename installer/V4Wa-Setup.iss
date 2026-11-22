; ViPER4Windows RE (Native WinUI 3 + Hi-Res MFX APO) Installer
; Clean root directory layout:
;   {app}\ViPER4WindowsRE.exe  (native launcher)
;   {app}\bin\                 (all internal runtime, DSP, APO & script files)
;   {app}\vdc\                 (ViPER-DDC headphone correction .vdc profiles)
;   {app}\irs\                 (Convolver impulse response .irs / .wav files)
;   {app}\preset\              (Custom user full presets & EQ presets)
;   {app}\logs\                (All APO, installer & diagnostic logs for easy access)
;   {app}\unins000.exe         (Windows uninstaller)

#define MyAppName "ViPER4Windows RE"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Another ViPER4Windows RE (AI-Powered)"
#define MyAppExeName "ViPER4WindowsRE.exe"

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
OutputBaseFilename=ViPER4Windows-RE-Setup-{#MyAppVersion}-x64
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName} ({#MyAppVersion})
CloseApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "attachapo"; Description: "Attach ViPER4Windows RE Hi-Res MFX APO to default audio device automatically"; GroupDescription: "Audio Engine:"
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Dirs]
; User-accessible asset & log folders right in the installation root
Name: "{app}\vdc"; Permissions: users-modify
Name: "{app}\irs"; Permissions: users-modify
Name: "{app}\preset"; Permissions: users-modify
Name: "{app}\preset\eq"; Permissions: users-modify
Name: "{app}\logs"; Permissions: users-modify

[Files]
; Clean top-level launcher in {app}\ViPER4WindowsRE.exe
Source: "..\build\V4WaLauncher.exe"; DestDir: "{app}"; DestName: "{#MyAppExeName}"; Flags: ignoreversion

; All internal runtime files neatly packed inside {app}\bin\
Source: "stage\app\*"; DestDir: "{app}\bin"; Flags: ignoreversion recursesubdirs createallsubdirs

; Native C++23 Hi-Res APO & CLI helper inside {app}\bin\
Source: "..\build\ViPERHiResAPO.dll"; DestDir: "{app}\bin"; Flags: ignoreversion
Source: "..\build\viper_ctl.exe"; DestDir: "{app}\bin"; Flags: ignoreversion

; PowerShell APO install & 1-click factory restore scripts inside {app}\bin\scripts\
Source: "..\scripts\install_apo.ps1"; DestDir: "{app}\bin\scripts"; Flags: ignoreversion
Source: "..\scripts\uninstall_apo.ps1"; DestDir: "{app}\bin\scripts"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "icacls.exe"; Parameters: """{app}\irs"" /grant ""NT AUTHORITY\LOCAL SERVICE:(OI)(CI)R"" /T"; Flags: runhidden waituntilterminated
Filename: "icacls.exe"; Parameters: """{app}\vdc"" /grant ""NT AUTHORITY\LOCAL SERVICE:(OI)(CI)R"" /T"; Flags: runhidden waituntilterminated
Filename: "icacls.exe"; Parameters: """{app}\logs"" /grant ""NT AUTHORITY\LOCAL SERVICE:(OI)(CI)M"" /grant ""BUILTIN\Users:(OI)(CI)M"" /T"; Flags: runhidden waituntilterminated
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -Command ""New-Item -ItemType Directory -Force -Path 'C:\ProgramData\ViPERHiRes' | Out-Null; [System.IO.File]::WriteAllText('C:\ProgramData\ViPERHiRes\logs_dir.txt', '{app}\logs', [System.Text.UTF8Encoding]::new($false))"""; Flags: runhidden waituntilterminated
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File ""{app}\bin\scripts\install_apo.ps1"" -AutoDefault"; StatusMsg: "Registering ViPER4Windows RE Hi-Res MFX APO and attaching to default audio device..."; Flags: runhidden waituntilterminated; Tasks: attachapo
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent runasoriginaluser

[UninstallRun]
Filename: "taskkill.exe"; Parameters: "/F /IM V4Wa.exe"; Flags: runhidden; RunOnceId: "KillV4Wa"
Filename: "taskkill.exe"; Parameters: "/F /IM {#MyAppExeName}"; Flags: runhidden; RunOnceId: "KillLauncher"
Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File ""{app}\bin\scripts\uninstall_apo.ps1"" -AllBackedUp -UnregisterDll"; Flags: runhidden waituntilterminated; RunOnceId: "RestoreAudioApo"
