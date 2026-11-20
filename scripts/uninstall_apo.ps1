#Requires -RunAsAdministrator
param(
    [Parameter(Mandatory = $true)]
    [string]$DeviceGuid,

    [switch]$UnregisterDll
)

$ErrorActionPreference = "Stop"

$ProgramDataDir = "C:\ProgramData\ViPERHiRes"
$BackupDir = Join-Path $ProgramDataDir "backups"
$BackupFile = Join-Path $BackupDir "$DeviceGuid.original.reg"
$RegPathRaw = "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render\$DeviceGuid\FxProperties"
$RegPathPs = "Registry::$RegPathRaw"

Write-Host "[1/3] Stopping Windows Audio service..." -ForegroundColor Cyan
Stop-Service -Name "Audiosrv" -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 400

if (Test-Path $BackupFile) {
    Write-Host "[2/3] Restoring original FxProperties from $BackupFile ..." -ForegroundColor Cyan
    # Remove ViPERHiRes SFX entries first, then re-import exact original backup
    Remove-ItemProperty -Path $RegPathPs -Name "{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},5" -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path $RegPathPs -Name "{d3993a3f-99c2-4402-b5ec-a92a0367664b},5" -ErrorAction SilentlyContinue
    & reg.exe import $BackupFile | Out-Null
} else {
    Write-Host "[2/3] No backup file found; removing ViPERHiRes APO keys directly..." -ForegroundColor Yellow
    Remove-ItemProperty -Path $RegPathPs -Name "{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},5" -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path $RegPathPs -Name "{d3993a3f-99c2-4402-b5ec-a92a0367664b},5" -ErrorAction SilentlyContinue
}

if ($UnregisterDll) {
    $InstalledDll = "C:\Program Files\ViPERHiRes\ViPERHiResAPO.dll"
    if (Test-Path $InstalledDll) {
        Start-Process -FilePath "regsvr32.exe" -ArgumentList "/u /s `"$InstalledDll`"" -Wait
    }
}

Write-Host "[3/3] Restarting Windows Audio service..." -ForegroundColor Cyan
Start-Service -Name "Audiosrv"
Write-Host "SUCCESS! Restored device $DeviceGuid to its original state." -ForegroundColor Green
