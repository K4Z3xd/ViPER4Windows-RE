#Requires -RunAsAdministrator
param(
    [Alias("EndpointGuid")]
    [string]$DeviceGuid = "",

    [switch]$AllBackedUp,

    [switch]$UnregisterDll
)

$ErrorActionPreference = "Stop"

$ProgramDataDir = "C:\ProgramData\ViPERHiRes"
$BackupDir = Join-Path $ProgramDataDir "backups"
$LogFile = Join-Path $ProgramDataDir "apo_install.log"

function Write-Log([string]$msg) {
    $line = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $msg"
    Add-Content -Path $LogFile -Value $line -ErrorAction SilentlyContinue
}

$guids = @()
if ($DeviceGuid) { $guids += $DeviceGuid }
if ($AllBackedUp -and (Test-Path $BackupDir)) {
    Get-ChildItem -Path $BackupDir -Filter "*.original.reg" | ForEach-Object {
        $g = $_.Name -replace '\.original\.reg$', ''
        if ($g -and ($guids -notcontains $g)) { $guids += $g }
    }
    Get-ChildItem -Path $BackupDir -Filter "*.created_fresh.marker" | ForEach-Object {
        $g = $_.Name -replace '\.created_fresh\.marker$', ''
        if ($g -and ($guids -notcontains $g)) { $guids += $g }
    }
}

Write-Host "[1/3] Stopping Windows Audio service..." -ForegroundColor Cyan
Stop-Service -Name "Audiosrv" -Force -ErrorAction SilentlyContinue
for ($i = 0; $i -lt 20; $i++) {
    if (-not (Get-Process -Name "audiodg" -ErrorAction SilentlyContinue)) { break }
    Start-Sleep -Milliseconds 200
}

try {
    foreach ($guid in $guids) {
        $BackupFile = Join-Path $BackupDir "$guid.original.reg"
        $FreshMarker = Join-Path $BackupDir "$guid.created_fresh.marker"
        $RegPathRaw = "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render\$guid\FxProperties"
        $RegPathPs = "Registry::$RegPathRaw"

        if (Test-Path $RegPathPs) {
            Remove-ItemProperty -Path $RegPathPs -Name "{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},5" -ErrorAction SilentlyContinue
            Remove-ItemProperty -Path $RegPathPs -Name "{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},6" -ErrorAction SilentlyContinue
            Remove-ItemProperty -Path $RegPathPs -Name "{d3993a3f-99c2-4402-b5ec-a92a0367664b},5" -ErrorAction SilentlyContinue
            Remove-ItemProperty -Path $RegPathPs -Name "{d3993a3f-99c2-4402-b5ec-a92a0367664b},6" -ErrorAction SilentlyContinue
        }

        if (Test-Path $BackupFile) {
            Write-Host "[2/3] Restoring original FxProperties for $guid ..." -ForegroundColor Cyan
            # Use Start-Process so non-fatal stderr on TrustedInstaller subkeys never trips ErrorActionPreference=Stop
            Start-Process -FilePath "reg.exe" -ArgumentList "import `"$BackupFile`"" -NoNewWindow -Wait
        } elseif (Test-Path $FreshMarker) {
            Write-Host "[2/3] Cleaning up V4Wa-created FxProperties for $guid ..." -ForegroundColor Cyan
            Remove-Item -Path $FreshMarker -Force -ErrorAction SilentlyContinue
        }
        Write-Log "RESTORED: $guid"
    }

    if ($UnregisterDll) {
        $InstalledDir = "C:\Program Files\ViPERHiRes"
        $InstalledDll = Join-Path $InstalledDir "ViPERHiResAPO.dll"
        if (Test-Path $InstalledDll) {
            Start-Process -FilePath "regsvr32.exe" -ArgumentList "/u /s `"$InstalledDll`"" -Wait
            Remove-Item -Path $InstalledDll -Force -ErrorAction SilentlyContinue
        }
        if (Test-Path $InstalledDir) {
            Remove-Item -Path $InstalledDir -Recurse -Force -ErrorAction SilentlyContinue
        }
        Write-Log "UNREGISTERED: ViPERHiResAPO.dll removed"
    }

    Write-Host "SUCCESS! Audio restored to factory state." -ForegroundColor Green
} catch {
    Write-Log "ERROR during uninstall: $_"
    Write-Error $_
    throw
} finally {
    Write-Host "[3/3] Ensuring Windows Audio service is running..." -ForegroundColor Cyan
    Start-Service -Name "AudioEndpointBuilder" -ErrorAction SilentlyContinue
    Start-Service -Name "Audiosrv" -ErrorAction SilentlyContinue
}
