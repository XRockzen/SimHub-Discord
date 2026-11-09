<#
  Publie une release GitHub à partir de la version de plugin\Properties\AssemblyInfo.cs.
  Le plugin installé la détectera au prochain démarrage de SimHub.

    .\release.ps1              compile et prépare dist\ sans rien publier
    .\release.ps1 -Publish     compile, crée le tag et publie la release

  Le code doit être commité et poussé avant -Publish. Notes : section de CHANGELOG.md.
#>
param([switch]$Publish)
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$utf8 = New-Object System.Text.UTF8Encoding($false)
$dll  = "User.DiscordControl.dll"

$info = [IO.File]::ReadAllText("$root\plugin\Properties\AssemblyInfo.cs", $utf8)
$m = [regex]::Match($info, 'AssemblyVersion\("(\d+\.\d+\.\d+)')
if (-not $m.Success) { throw "Version introuvable dans AssemblyInfo.cs" }
$tag = "v" + $m.Groups[1].Value

& "$root\plugin\build.ps1"
if ($LASTEXITCODE -ne 0) { exit 1 }

$dist = "$root\dist\$tag"
New-Item -ItemType Directory -Force $dist | Out-Null
Copy-Item "$root\plugin\bin\Release\$dll" $dist -Force
$sha = (Get-FileHash "$dist\$dll" -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$dist\$dll.sha256", "$sha  $dll`n", $utf8)

$log = [IO.File]::ReadAllText("$root\CHANGELOG.md", $utf8)
$section = [regex]::Match($log, "(?ms)^## $([regex]::Escape($tag))\s*\r?\n(.*?)(?=^## |\z)")
if (-not $section.Success) { throw "Section $tag absente de CHANGELOG.md" }
$notes = "$dist\notes.md"
[IO.File]::WriteAllText($notes, $section.Groups[1].Value.Trim(), $utf8)

Write-Host "$tag prêt dans $dist (sha256 $sha)" -ForegroundColor Green
if (-not $Publish) { exit 0 }

# git et gh écrivent leur progression sur stderr : on les juge sur le code de retour
$ErrorActionPreference = "Continue"

$dirty = git -C $root status --porcelain
if ($dirty) { Write-Host "Commite tes changements avant de publier." -ForegroundColor Red; exit 1 }
git -C $root push origin HEAD
if ($LASTEXITCODE -ne 0) { exit 1 }

gh release create $tag "$dist\$dll" "$dist\$dll.sha256" --repo XRockzen/SimHub-Discord --title $tag --notes-file $notes --target (git -C $root rev-parse HEAD)
if ($LASTEXITCODE -ne 0) { exit 1 }
Write-Host "Release $tag publiée." -ForegroundColor Green
