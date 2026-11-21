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

$guids = @()
if ($DeviceGuid) {
    $guids += $DeviceGuid
}
if ($AllBackedUp -and (Test-Path $BackupDir)) {
    Get-ChildItem -Path $BackupDir -Filter "*.original.reg" | ForEach-Object {
        $g = $_.Name -replace '\.original\.reg$', ''
        if ($g -and ($guids -notcontains $g)) {
            $guids += $g
        }
    }
}

Write-Host "[1/3] Stopping Windows Audio service..." -ForegroundColor Cyan
Stop-Service -Name "Audiosrv" -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

foreach ($guid in $guids) {
    $BackupFile = Join-Path $BackupDir "$guid.original.reg"
    $RegPathRaw = "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render\$guid\FxProperties"
    $RegPathPs = "Registry::$RegPathRaw"

    if (Test-Path $BackupFile) {
        Write-Host "[2/3] Restoring original FxProperties for $guid ..." -ForegroundColor Cyan
        Remove-ItemProperty -Path $RegPathPs -Name "{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},5" -ErrorAction SilentlyContinue
        Remove-ItemProperty -Path $RegPathPs -Name "{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},6" -ErrorAction SilentlyContinue
        Remove-ItemProperty -Path $RegPathPs -Name "{d3993a3f-99c2-4402-b5ec-a92a0367664b},5" -ErrorAction SilentlyContinue
        Remove-ItemProperty -Path $RegPathPs -Name "{d3993a3f-99c2-4402-b5ec-a92a0367664b},6" -ErrorAction SilentlyContinue
        & reg.exe import $BackupFile | Out-Null
    } else {
        Write-Host "[2/3] Removing V4Wa APO keys for $guid ..." -ForegroundColor Yellow
        Remove-ItemProperty -Path $RegPathPs -Name "{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},5" -ErrorAction SilentlyContinue
        Remove-ItemProperty -Path $RegPathPs -Name "{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},6" -ErrorAction SilentlyContinue
        Remove-ItemProperty -Path $RegPathPs -Name "{d3993a3f-99c2-4402-b5ec-a92a0367664b},5" -ErrorAction SilentlyContinue
        Remove-ItemProperty -Path $RegPathPs -Name "{d3993a3f-99c2-4402-b5ec-a92a0367664b},6" -ErrorAction SilentlyContinue
    }
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
}

Write-Host "[3/3] Restarting Windows Audio service..." -ForegroundColor Cyan
Start-Service -Name "Audiosrv" -ErrorAction SilentlyContinue
Write-Host "SUCCESS! Audio restored to factory state." -ForegroundColor Green
