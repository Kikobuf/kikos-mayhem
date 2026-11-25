<#
  Kiko's Mayhem — one-shot Windows setup.

  What it does:
    1. Downloads SteamCMD (if missing)
    2. Installs / updates the SCP: Secret Laboratory Dedicated Server (Steam app 996560)
    3. Installs KikosMayhem.dll into the LabAPI plugins folder
       (from -PluginDll, or builds it from this repo with -Build)

  Usage (PowerShell, from the repo folder):
    powershell -ExecutionPolicy Bypass -File scripts\install-server.ps1
    powershell -ExecutionPolicy Bypass -File scripts\install-server.ps1 -ServerDir "D:\SCPSL" -Build
    powershell -ExecutionPolicy Bypass -File scripts\install-server.ps1 -PluginDll "C:\Downloads\KikosMayhem.dll"
#>
param(
    [string]$ServerDir = "C:\SCPSL-Server",
    [string]$PluginDll = "",
    [switch]$Build,
    [int]$Port = 0   # 0 = install for every port ("global"); or e.g. 7777 for one server
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$SteamCmdDir = Join-Path $ServerDir "steamcmd"

Write-Host "== Kiko's Mayhem setup ==" -ForegroundColor Red

# 1. SteamCMD
if (-not (Test-Path (Join-Path $SteamCmdDir "steamcmd.exe"))) {
    Write-Host "Downloading SteamCMD..."
    New-Item -ItemType Directory -Force -Path $SteamCmdDir | Out-Null
    $zip = Join-Path $SteamCmdDir "steamcmd.zip"
    Invoke-WebRequest "https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip" -OutFile $zip
    Expand-Archive $zip -DestinationPath $SteamCmdDir -Force
    Remove-Item $zip
}

# 2. SCP:SL Dedicated Server
Write-Host "Installing / updating SCP:SL Dedicated Server into $ServerDir ..."
$steamcmd = Join-Path $SteamCmdDir "steamcmd.exe"
for ($i = 0; $i -lt 3; $i++) {
    & $steamcmd +force_install_dir "$ServerDir" +login anonymous +app_update 996560 validate +quit
    if (Test-Path (Join-Path $ServerDir "SCPSL_Data\Managed\Assembly-CSharp.dll")) { break }
    Write-Host "SteamCMD hiccup, retrying..."
}
if (-not (Test-Path (Join-Path $ServerDir "SCPSL_Data\Managed\Assembly-CSharp.dll"))) { throw "Server install failed." }

# 3. Plugin
if ($Build) {
    Write-Host "Building KikosMayhem.dll from source (needs the .NET 8 SDK)..."
    $managed = Join-Path $ServerDir "SCPSL_Data\Managed"
    dotnet build (Join-Path $RepoRoot "src\KikosMayhem\KikosMayhem.csproj") -c Release "-p:SL_REFERENCES=$managed"
    $PluginDll = Join-Path $RepoRoot "src\KikosMayhem\bin\Release\net48\KikosMayhem.dll"
}
if ($PluginDll -eq "") {
    $candidate = Join-Path $RepoRoot "KikosMayhem.dll"
    if (Test-Path $candidate) { $PluginDll = $candidate }
}
if ($PluginDll -eq "" -or -not (Test-Path $PluginDll)) {
    Write-Warning "No KikosMayhem.dll found. Download it from the repo's Releases page and re-run with -PluginDll <path>, or use -Build."
} else {
    $folder = if ($Port -eq 0) { "global" } else { "$Port" }
    $pluginDir = Join-Path $env:APPDATA "SCP Secret Laboratory\LabAPI\plugins\$folder"
    New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
    Copy-Item $PluginDll $pluginDir -Force
    Write-Host "Installed plugin to $pluginDir" -ForegroundColor Green
}

Write-Host ""
Write-Host "Done! Start the server with:  scripts\start-server.bat `"$ServerDir`"" -ForegroundColor Green
Write-Host "Config appears after first start in: $env:APPDATA\SCP Secret Laboratory\LabAPI\configs\<port>\KikosMayhem\config.yml"
Write-Host "Optional music/sfx: drop .wav files into the 'audio' folder next to that config (see README)."
