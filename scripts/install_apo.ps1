#Requires -RunAsAdministrator
param(
    [Parameter(Mandatory = $true)]
    [string]$DeviceGuid,

    [string]$DllSource = "$PSScriptRoot\..\build\ViPERHiResAPO.dll"
)

$ErrorActionPreference = "Stop"

$ApoClsid = "{8F4E2B1A-7C3D-4E9A-B12F-6A8C4D9E105A}"
$ModeDefault = "{c18e2f7e-933d-4965-b7d1-1eef228d2af3}"
$InstallDir = "C:\Program Files\ViPERHiRes"
$ProgramDataDir = "C:\ProgramData\ViPERHiRes"
$BackupDir = Join-Path $ProgramDataDir "backups"

if (-not (Test-Path $DllSource)) {
    throw "ViPERHiResAPO.dll not found at: $DllSource. Build the project first."
}

# 1. Prepare ProgramData folder with permissions for LOCAL SERVICE (audiodg.exe) & Users (UI)
New-Item -ItemType Directory -Force -Path $BackupDir | Out-Null
icacls $ProgramDataDir /grant "NT AUTHORITY\LOCAL SERVICE:(OI)(CI)RX" /grant "BUILTIN\Users:(OI)(CI)M" /T | Out-Null

# 2. Copy APO DLL to C:\Program Files\ViPERHiRes (accessible by audiodg.exe / LOCAL SERVICE)
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
$InstalledDll = Join-Path $InstallDir "ViPERHiResAPO.dll"

Write-Host "[1/5] Stopping Windows Audio service briefly to replace DLL..." -ForegroundColor Cyan
Stop-Service -Name "Audiosrv" -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 400

Copy-Item -Path $DllSource -Destination $InstalledDll -Force
icacls $InstallDir /grant "NT AUTHORITY\LOCAL SERVICE:(OI)(CI)RX" /T | Out-Null

# 3. Register COM + Windows APO
Write-Host "[2/5] Registering ViPERHiResAPO.dll..." -ForegroundColor Cyan
$regProc = Start-Process -FilePath "regsvr32.exe" -ArgumentList "/s `"$InstalledDll`"" -Wait -PassThru
if ($regProc.ExitCode -ne 0) {
    Start-Service -Name "Audiosrv" -ErrorAction SilentlyContinue
    throw "regsvr32 failed with exit code $($regProc.ExitCode)"
}

# 4. Backup original FxProperties BEFORE touching Registry
$RegPathRaw = "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render\$DeviceGuid\FxProperties"
$RegPathPs = "Registry::$RegPathRaw"
$BackupFile = Join-Path $BackupDir "$DeviceGuid.original.reg"

if (-not (Test-Path $BackupFile)) {
    Write-Host "[3/5] Backing up factory FxProperties to $BackupFile ..." -ForegroundColor Cyan
    & reg.exe export $RegPathRaw $BackupFile /y | Out-Null
} else {
    Write-Host "[3/5] Original backup already exists: $BackupFile (preserved)" -ForegroundColor Yellow
}

# 5. Take ownership of FxProperties (TrustedInstaller -> Administrators) and attach ViPERHiRes APO (SFX)
Write-Host "[4/5] Attaching ViPERHiRes APO (SFX Hi-Res) to device $DeviceGuid ..." -ForegroundColor Cyan

$csharp = @"
using System;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32;

public static class RegOwnership {
    [DllImport("advapi32.dll", SetLastError = true)]
    static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, out long lpLuid);
    [DllImport("advapi32.dll", SetLastError = true)]
    static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, bool DisableAllPrivileges, ref TOKEN_PRIVILEGES NewState, uint BufferLength, IntPtr PreviousState, IntPtr ReturnLength);

    [StructLayout(LayoutKind.Sequential)]
    struct TOKEN_PRIVILEGES {
        public uint PrivilegeCount;
        public long Luid;
        public uint Attributes;
    }

    static void EnablePrivilege(string name) {
        IntPtr token;
        if (!OpenProcessToken(System.Diagnostics.Process.GetCurrentProcess().Handle, 0x0028, out token)) return;
        TOKEN_PRIVILEGES tp = new TOKEN_PRIVILEGES { PrivilegeCount = 1, Attributes = 2 };
        if (LookupPrivilegeValue(null, name, out tp.Luid)) {
            AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
        }
    }

    public static void GrantAdminFullControl(string subKeyPath) {
        EnablePrivilege("SeTakeOwnershipPrivilege");
        EnablePrivilege("SeRestorePrivilege");
        EnablePrivilege("SeBackupPrivilege");

        var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        using (var key = Registry.LocalMachine.OpenSubKey(subKeyPath, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.TakeOwnership)) {
            if (key == null) throw new Exception("Registry key not found: " + subKeyPath);
            var sec = key.GetAccessControl();
            sec.SetOwner(admins);
            key.SetAccessControl(sec);
        }
        using (var key = Registry.LocalMachine.OpenSubKey(subKeyPath, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.ChangePermissions)) {
            var sec = key.GetAccessControl();
            sec.AddAccessRule(new RegistryAccessRule(admins, RegistryRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            key.SetAccessControl(sec);
        }
    }
}
"@

if (-not ("RegOwnership" -as [type])) {
    Add-Type -TypeDefinition $csharp
}

$subKey = "SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render\$DeviceGuid\FxProperties"
[RegOwnership]::GrantAdminFullControl($subKey)

# Ensure FxProperties exists & set SFX APO CLSID + Supported Default Streaming Mode
New-ItemProperty -Path $RegPathPs -Name "{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},5" -Value $ApoClsid -PropertyType String -Force | Out-Null
New-ItemProperty -Path $RegPathPs -Name "{d3993a3f-99c2-4402-b5ec-a92a0367664b},5" -Value @($ModeDefault) -PropertyType MultiString -Force | Out-Null

# Ensure Audio Enhancements are enabled (DisableSysFx = 0)
$PropsSubKey = "SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render\$DeviceGuid\Properties"
try {
    [RegOwnership]::GrantAdminFullControl($PropsSubKey)
    New-ItemProperty -Path "Registry::HKLM\$PropsSubKey" -Name "{1da5d803-d492-4edd-8c23-e0c0ffee7f0e},5" -Value 0 -PropertyType DWord -Force | Out-Null
} catch {
    Write-Warning "Could not update DisableSysFx flag (non-fatal): $_"
}

Write-Host "[5/5] Restarting Windows Audio service..." -ForegroundColor Cyan
Start-Service -Name "Audiosrv"
Write-Host "SUCCESS! ViPERHiRes APO installed on device $DeviceGuid." -ForegroundColor Green
Write-Host "If anything sounds wrong, run: .\scripts\uninstall_apo.ps1 -DeviceGuid '$DeviceGuid'" -ForegroundColor Yellow
