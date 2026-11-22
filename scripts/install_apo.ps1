#Requires -RunAsAdministrator
param(
    [Alias("EndpointGuid")]
    [string]$DeviceGuid = "",

    [switch]$AutoDefault,

    [string]$DllSource = ""
)

$ErrorActionPreference = "Stop"

# Critical: Never leave CWD inside the .NET 8 self-contained app's bin\ folder,
# otherwise Windows PowerShell 5.1 Add-Type (csc.exe) picks up .NET 8's System.dll!
Set-Location -Path $env:SystemRoot

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
$LogFile = Join-Path $ProgramDataDir "apo_install.log"

function Write-Log([string]$msg) {
    $line = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $msg"
    Add-Content -Path $LogFile -Value $line -ErrorAction SilentlyContinue
}

# 1. Prepare ProgramData folder with permissions for LOCAL SERVICE (audiodg.exe) & Users (V4Wa UI)
New-Item -ItemType Directory -Force -Path $BackupDir | Out-Null
icacls $ProgramDataDir /grant "NT AUTHORITY\LOCAL SERVICE:(OI)(CI)M" /grant "BUILTIN\Users:(OI)(CI)M" /grant "Everyone:(OI)(CI)R" /T 2>$null | Out-Null

if (-not $DeviceGuid -and $AutoDefault) {
    $ctlCandidates = @(
        (Join-Path $PSScriptRoot "..\viper_ctl.exe"),
        (Join-Path $PSScriptRoot "..\build\viper_ctl.exe")
    )
    $ctl = $ctlCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if ($ctl) {
        $json = & $ctl devices 2>$null
        if ($json) {
            $devs = @($json | ConvertFrom-Json) | Where-Object {
                $n = [string]$_.name
                -not ($n -match "Hands-Free|Hands Free|AG Audio|\(HFP\)") -and
                -not ($_.channels -eq 1 -and $_.sampleRate -le 24000)
            }
            $def = $devs | Where-Object { $_.isDefault } | Select-Object -First 1
            if (-not $def) { $def = $devs | Select-Object -First 1 }
            if ($def -and $def.guid) { $DeviceGuid = $def.guid }
        }
    }
}

if (-not $DeviceGuid) {
    Write-Log "ERROR: No target audio device GUID provided or detected."
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

if (-not (Test-Path $DllSource)) {
    Write-Log "ERROR: ViPERHiResAPO.dll not found at $DllSource"
    throw "ViPERHiResAPO.dll not found at: $DllSource."
}

New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
$InstalledDll = Join-Path $InstallDir "ViPERHiResAPO.dll"

$csharp = @"
using System;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32;

public static class RegOwnership {
    [DllImport("kernel32.dll")]
    static extern IntPtr GetCurrentProcess();
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
        if (!OpenProcessToken(GetCurrentProcess(), 0x0028, out token))
            throw new Exception("OpenProcessToken failed: " + Marshal.GetLastWin32Error());
        TOKEN_PRIVILEGES tp = new TOKEN_PRIVILEGES { PrivilegeCount = 1, Attributes = 2 };
        if (!LookupPrivilegeValue(null, name, out tp.Luid))
            throw new Exception("LookupPrivilegeValue failed for " + name);
        if (!AdjustTokenPrivileges(token, false, ref tp, (uint)Marshal.SizeOf(typeof(TOKEN_PRIVILEGES)), IntPtr.Zero, IntPtr.Zero))
            throw new Exception("AdjustTokenPrivileges failed for " + name);
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

try {
    if (-not ("RegOwnership" -as [type])) {
        Add-Type -TypeDefinition $csharp
    }
} catch {
    Write-Log "ERROR compiling RegOwnership helper for ${DeviceGuid}: $_"
    Write-Error $_
    throw
}

Write-Host "[1/5] Stopping Windows Audio service briefly..." -ForegroundColor Cyan
Stop-Service -Name "Audiosrv" -Force -ErrorAction SilentlyContinue

# Wait up to 4 seconds for audiodg.exe to exit and release ViPERHiResAPO.dll
for ($i = 0; $i -lt 20; $i++) {
    if (-not (Get-Process -Name "audiodg" -ErrorAction SilentlyContinue)) { break }
    Start-Sleep -Milliseconds 200
}

try {
    # 2. Copy APO DLL only if missing or changed (avoids unnecessary file-lock races on multi-device attach)
    $needCopy = $true
    if (Test-Path $InstalledDll) {
        $srcHash = (Get-FileHash -Path $DllSource -Algorithm SHA256).Hash
        $dstHash = (Get-FileHash -Path $InstalledDll -Algorithm SHA256).Hash
        if ($srcHash -eq $dstHash) { $needCopy = $false }
    }
    if ($needCopy) {
        Copy-Item -Path $DllSource -Destination $InstalledDll -Force
    }
    icacls $InstallDir /grant "NT AUTHORITY\LOCAL SERVICE:(OI)(CI)RX" /T 2>$null | Out-Null

    # Ensure audiodg.exe (LOCAL SERVICE) can read .irs / .vdc files from the app's root folders if installed there
    $appRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
    foreach ($sub in @("irs", "vdc")) {
        $assetDir = Join-Path $appRoot $sub
        if (Test-Path $assetDir) {
            icacls $assetDir /grant "NT AUTHORITY\LOCAL SERVICE:(OI)(CI)R" /T 2>$null | Out-Null
        }
    }

    # 3. Register COM + Windows APO
    Write-Host "[2/5] Registering ViPERHiResAPO.dll..." -ForegroundColor Cyan
    $regProc = Start-Process -FilePath "regsvr32.exe" -ArgumentList "/s `"$InstalledDll`"" -Wait -PassThru
    if ($regProc.ExitCode -ne 0) {
        throw "regsvr32 failed with exit code $($regProc.ExitCode)"
    }

    # 4. Ensure FxProperties exists (some USB/BT/HDMI devices on clean PCs lack it initially) & backup if present
    $DeviceSubKey = "SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render\$DeviceGuid"
    $FxSubKey = "$DeviceSubKey\FxProperties"
    $RegPathRaw = "HKLM\$FxSubKey"
    $RegPathPs = "Registry::$RegPathRaw"
    $BackupFile = Join-Path $BackupDir "$DeviceGuid.original.reg"
    $FreshMarker = Join-Path $BackupDir "$DeviceGuid.created_fresh.marker"

    if (Test-Path $RegPathPs) {
        if (-not (Test-Path $BackupFile)) {
            Write-Host "[3/5] Backing up factory FxProperties to $BackupFile ..." -ForegroundColor Cyan
            Start-Process -FilePath "reg.exe" -ArgumentList "export `"$RegPathRaw`" `"$BackupFile`" /y" -NoNewWindow -Wait
        } else {
            Write-Host "[3/5] Original backup preserved: $BackupFile" -ForegroundColor Yellow
        }
        [RegOwnership]::GrantAdminFullControl($FxSubKey)
    } else {
        Write-Host "[3/5] Device has no FxProperties yet; creating cleanly..." -ForegroundColor Yellow
        [RegOwnership]::GrantAdminFullControl($DeviceSubKey)
        New-Item -Path $RegPathPs -Force | Out-Null
        [RegOwnership]::GrantAdminFullControl($FxSubKey)
        Set-Content -Path $FreshMarker -Value "created_by_v4wa" -ErrorAction SilentlyContinue
    }

    # 5. Remove any conflicting legacy/vendor APO slots (LFX/GFX/SFX/EFX) so audiodg.exe never mutes the device,
    #    then attach V4Wa MFX APO (Mode Effect = ,6) across DEFAULT / MEDIA / MOVIE / COMMUNICATIONS
    Write-Host "[4/5] Attaching V4Wa Hi-Res MFX APO to device $DeviceGuid ..." -ForegroundColor Cyan
    foreach ($slot in @("1", "2", "3", "5", "7")) {
        Remove-ItemProperty -Path $RegPathPs -Name "{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},$slot" -ErrorAction SilentlyContinue
    }
    foreach ($modeSlot in @("5", "7")) {
        Remove-ItemProperty -Path $RegPathPs -Name "{d3993a3f-99c2-4402-b5ec-a92a0367664b},$modeSlot" -ErrorAction SilentlyContinue
    }
    New-ItemProperty -Path $RegPathPs -Name "{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},6" -Value $ApoClsid -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $RegPathPs -Name "{d3993a3f-99c2-4402-b5ec-a92a0367664b},6" -Value $ModesAll -PropertyType MultiString -Force | Out-Null
    New-ItemProperty -Path $RegPathPs -Name "{1da5d803-d492-4edd-8c23-e0c0ffee7f0e},5" -Value 0 -PropertyType DWord -Force | Out-Null

    # Ensure Audio Enhancements are enabled in Properties (DisableSysFx = 0)
    $PropsSubKey = "$DeviceSubKey\Properties"
    if (Test-Path "Registry::HKLM\$PropsSubKey") {
        try {
            [RegOwnership]::GrantAdminFullControl($PropsSubKey)
            New-ItemProperty -Path "Registry::HKLM\$PropsSubKey" -Name "{1da5d803-d492-4edd-8c23-e0c0ffee7f0e},5" -Value 0 -PropertyType DWord -Force | Out-Null
        } catch {
            Write-Log "WARN: Could not update DisableSysFx on $DeviceGuid (non-fatal): $_"
        }
    }

    Write-Log "SUCCESS: Installed V4Wa MFX APO on $DeviceGuid"
    Write-Host "SUCCESS! V4Wa APO installed on device $DeviceGuid." -ForegroundColor Green
} catch {
    Write-Log "ERROR installing APO on ${DeviceGuid}: $_"
    Write-Error $_
    throw
} finally {
    Write-Host "[5/5] Ensuring Windows Audio service is running..." -ForegroundColor Cyan
    for ($try = 0; $try -lt 5; $try++) {
        Start-Service -Name "AudioEndpointBuilder" -ErrorAction SilentlyContinue
        Start-Service -Name "Audiosrv" -ErrorAction SilentlyContinue
        if ((Get-Service -Name "Audiosrv" -ErrorAction SilentlyContinue).Status -eq "Running") { break }
        Start-Sleep -Milliseconds 500
    }
}
