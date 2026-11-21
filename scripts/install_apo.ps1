#Requires -RunAsAdministrator
param(
    [Alias("EndpointGuid")]
    [string]$DeviceGuid = "",

    [switch]$AutoDefault,

    [string]$DllSource = ""
)

$ErrorActionPreference = "Stop"

if (-not $DeviceGuid -and $AutoDefault) {
    $ctlCandidates = @(
        (Join-Path $PSScriptRoot "..\viper_ctl.exe"),
        (Join-Path $PSScriptRoot "..\build\viper_ctl.exe")
    )
    $ctl = $ctlCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if ($ctl) {
        $json = & $ctl devices 2>$null
        $devs = $json | ConvertFrom-Json
        $def = $devs | Where-Object { $_.isDefault } | Select-Object -First 1
        if (-not $def) { $def = $devs | Select-Object -First 1 }
        if ($def -and $def.guid) { $DeviceGuid = $def.guid }
    }
}

if (-not $DeviceGuid) {
    throw "No target audio device GUID provided or detected."
}

if (-not $DllSource) {
    $localDll = Join-Path $PSScriptRoot "..\ViPERHiResAPO.dll"
    if (Test-Path $localDll) {
        $DllSource = $localDll
    } else {
        $DllSource = Join-Path $PSScriptRoot "..\build\ViPERHiResAPO.dll"
    }
}

$ApoClsid = "{8F4E2B1A-7C3D-4E9A-B12F-6A8C4D9E105A}"
$ModesAll = @(
    "{c18e2f7e-933d-4965-b7d1-1eef228d2af3}", # DEFAULT
    "{4780004e-7133-41d8-8c74-660dadd2c0ee}", # MEDIA
    "{b26feb0d-ec94-477c-9494-d1ab8e753f6e}", # MOVIE
    "{98951333-b9cd-48b1-a0a3-ff40682d73f7}"  # COMMUNICATIONS
)
$InstallDir = "C:\Program Files\ViPERHiRes"
$ProgramDataDir = "C:\ProgramData\ViPERHiRes"
$BackupDir = Join-Path $ProgramDataDir "backups"

if (-not (Test-Path $DllSource)) {
    throw "ViPERHiResAPO.dll not found at: $DllSource. Build the project first."
}

# 1. Prepare ProgramData folder with permissions for LOCAL SERVICE (audiodg.exe) & Users (UI)
New-Item -ItemType Directory -Force -Path $BackupDir | Out-Null
icacls $ProgramDataDir /grant "NT AUTHORITY\LOCAL SERVICE:(OI)(CI)M" /grant "BUILTIN\Users:(OI)(CI)M" /grant "Everyone:(OI)(CI)R" /T | Out-Null

# 2. Copy APO DLL to C:\Program Files\ViPERHiRes (accessible by audiodg.exe / LOCAL SERVICE)
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
$InstalledDll = Join-Path $InstallDir "ViPERHiResAPO.dll"

Write-Host "[1/5] Stopping Windows Audio service briefly to replace DLL..." -ForegroundColor Cyan
Stop-Service -Name "Audiosrv" -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 400

try {
    Copy-Item -Path $DllSource -Destination $InstalledDll -Force
    icacls $InstallDir /grant "NT AUTHORITY\LOCAL SERVICE:(OI)(CI)RX" /T | Out-Null

    # 3. Register COM + Windows APO
    Write-Host "[2/5] Registering ViPERHiResAPO.dll..." -ForegroundColor Cyan
    $regProc = Start-Process -FilePath "regsvr32.exe" -ArgumentList "/s `"$InstalledDll`"" -Wait -PassThru
    if ($regProc.ExitCode -ne 0) {
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

    # 5. Take ownership of FxProperties (TrustedInstaller -> Administrators) and attach ViPERHiRes APO (MFX)
    Write-Host "[4/5] Attaching ViPERHiRes APO (MFX Hi-Res) to device $DeviceGuid ..." -ForegroundColor Cyan

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

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct TOKEN_PRIVILEGES {
        public uint PrivilegeCount;
        public long Luid;
        public uint Attributes;
    }

    static void EnablePrivilege(string name) {
        IntPtr token;
        if (!OpenProcessToken(System.Diagnostics.Process.GetCurrentProcess().Handle, 0x0028, out token)) {
            throw new Exception("OpenProcessToken failed: " + Marshal.GetLastWin32Error());
        }
        TOKEN_PRIVILEGES tp = new TOKEN_PRIVILEGES { PrivilegeCount = 1, Attributes = 2 };
        if (!LookupPrivilegeValue(null, name, out tp.Luid)) {
            throw new Exception("LookupPrivilegeValue failed for " + name);
        }
        if (!AdjustTokenPrivileges(token, false, ref tp, (uint)Marshal.SizeOf(typeof(TOKEN_PRIVILEGES)), IntPtr.Zero, IntPtr.Zero)) {
            throw new Exception("AdjustTokenPrivileges failed for " + name);
        }
    }

    public static void GrantAdminFullControl(string subKeyPath) {
        EnablePrivilege("SeTakeOwnershipPrivilege");
        EnablePrivilege("SeRestorePrivilege");
        EnablePrivilege("SeBackupPrivilege");

        var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)) {
            using (var key = baseKey.OpenSubKey(subKeyPath, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.TakeOwnership)) {
                if (key == null) throw new Exception("Registry key not found: " + subKeyPath);
                var sec = new RegistrySecurity();
                sec.SetOwner(admins);
                key.SetAccessControl(sec);
            }
            using (var key = baseKey.OpenSubKey(subKeyPath, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.ReadPermissions | RegistryRights.ChangePermissions)) {
                if (key == null) throw new Exception("Registry key not found on ACL step: " + subKeyPath);
                var sec = key.GetAccessControl();
                sec.AddAccessRule(new RegistryAccessRule(admins, RegistryRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
                key.SetAccessControl(sec);
            }
        }
    }
}
"@

    if (-not ("RegOwnership" -as [type])) {
        Add-Type -TypeDefinition $csharp
    }

    $subKey = "SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render\$DeviceGuid\FxProperties"
    [RegOwnership]::GrantAdminFullControl($subKey)

    # Attach as MFX (Mode Effect = ,6) so ALL apps (browsers, music players, games) are processed in one master Hi-Res chain
    Remove-ItemProperty -Path $RegPathPs -Name "{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},5" -ErrorAction SilentlyContinue
    New-ItemProperty -Path $RegPathPs -Name "{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},6" -Value $ApoClsid -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $RegPathPs -Name "{d3993a3f-99c2-4402-b5ec-a92a0367664b},6" -Value $ModesAll -PropertyType MultiString -Force | Out-Null
    # Clear Windows 11 Audio Enhancements circuit-breaker inside FxProperties (DisableSysFx = 0)
    New-ItemProperty -Path $RegPathPs -Name "{1da5d803-d492-4edd-8c23-e0c0ffee7f0e},5" -Value 0 -PropertyType DWord -Force | Out-Null

    # Ensure Audio Enhancements are enabled (DisableSysFx = 0)
    $PropsSubKey = "SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render\$DeviceGuid\Properties"
    try {
        [RegOwnership]::GrantAdminFullControl($PropsSubKey)
        New-ItemProperty -Path "Registry::HKLM\$PropsSubKey" -Name "{1da5d803-d492-4edd-8c23-e0c0ffee7f0e},5" -Value 0 -PropertyType DWord -Force | Out-Null
    } catch {
        Write-Warning "Could not update DisableSysFx flag (non-fatal): $_"
    }

    Write-Host "SUCCESS! ViPERHiRes APO installed on device $DeviceGuid." -ForegroundColor Green
} catch {
    $errMsg = "$(Get-Date -Format o) ERROR installing APO on ${DeviceGuid}: $_"
    Add-Content -Path (Join-Path $ProgramDataDir "apo_install.log") -Value $errMsg -ErrorAction SilentlyContinue
    Write-Error $errMsg
    throw
} finally {
    Write-Host "[5/5] Ensuring Windows Audio service is running..." -ForegroundColor Cyan
    Start-Service -Name "Audiosrv" -ErrorAction SilentlyContinue
}
