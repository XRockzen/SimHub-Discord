<#
  Compile le plugin avec le MSBuild livré avec Windows (aucune installation nécessaire).
    .\build.ps1           compile seulement (bin\Release\User.DiscordControl.dll)
    .\build.ps1 -Deploy   compile puis copie la DLL dans SimHub (SimHub doit être fermé)
#>
param([switch]$Deploy)
$ErrorActionPreference = "Stop"
$root    = $PSScriptRoot
$simhub  = if ($env:SIMHUB_INSTALL_PATH) { $env:SIMHUB_INSTALL_PATH } else { "F:\Program Files (x86)\SimHub\" }
$msbuild = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"

# Le chemin de SimHub passe par SIMHUB_INSTALL_PATH (lu par le .csproj), pas par un /p: :
# son "\" final échapperait le guillemet de l'argument.
# PostBuildEvent vidé : la copie dans SimHub se fait seulement avec -Deploy.
$env:SIMHUB_INSTALL_PATH = $simhub
& $msbuild "$root\User.DiscordControl.csproj" /nologo /v:minimal /p:Configuration=Release /p:PostBuildEvent=
if ($LASTEXITCODE -ne 0) { Write-Host "ERREUR de compilation" -ForegroundColor Red; exit 1 }

if ($Deploy) {
    if (Get-Process SimHubWPF -ErrorAction SilentlyContinue) { Write-Host "Ferme SimHub avant de déployer." -ForegroundColor Red; exit 1 }
    Copy-Item "$root\bin\Release\User.DiscordControl.dll" $simhub -Force
    Copy-Item "$root\bin\Release\User.DiscordControl.pdb" $simhub -Force -ErrorAction SilentlyContinue
    Write-Host "Plugin copié dans $simhub" -ForegroundColor Green
}
