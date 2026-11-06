# Clean mod build for Steam Workshop: only the files the game needs.
#   powershell -ExecutionPolicy Bypass -File tools\make_release.ps1 [-Out <dir>]
# (ASCII only: Windows PowerShell 5.1 reads BOM-less UTF-8 as ANSI.)
param([string]$Out = "")
$root = Split-Path $PSScriptRoot -Parent
if ($Out -eq "") { $Out = Join-Path (Split-Path $root -Parent) "Paramedic-release" }

$build = dotnet build (Join-Path $root "Source\Paramedic.csproj") -c Release -nologo -v q 2>&1 | Out-String
if ($build -match " error ") { Write-Output $build; exit 1 }
python (Join-Path $root "tools\validate_xml.py")
if ($LASTEXITCODE -ne 0) { exit 1 }

# Mods\Paramedic is usually a link to this repo: deleting through it would wipe the repo.
if ((Test-Path -LiteralPath $Out) -and ((Get-Item -LiteralPath $Out -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
    Write-Output ("refusing to overwrite a link: " + $Out); exit 1
}
if (Test-Path -LiteralPath $Out) { Remove-Item -LiteralPath $Out -Recurse -Force }
New-Item -ItemType Directory -Path $Out | Out-Null
foreach ($d in "About", "Defs", "Languages", "Patches", "Textures") {
    Copy-Item -LiteralPath (Join-Path $root $d) -Destination (Join-Path $Out $d) -Recurse
}
# .dds next to .png come from an external texture converter and are not part of the mod.
Get-ChildItem -LiteralPath (Join-Path $Out "Textures") -Recurse -Filter *.dds | Remove-Item -Force
foreach ($v in "1.6", "1.7") {
    $dir = Join-Path $Out ($v + "\Assemblies")
    New-Item -ItemType Directory -Path $dir | Out-Null
    Copy-Item -LiteralPath (Join-Path $root ($v + "\Assemblies\Paramedic.dll")) -Destination $dir
}
Copy-Item -LiteralPath (Join-Path $root "LoadFolders.xml") -Destination $Out
Copy-Item -LiteralPath (Join-Path $root "LICENSE") -Destination $Out
$size = (Get-ChildItem -LiteralPath $Out -Recurse -File | Measure-Object Length -Sum).Sum / 1MB
Write-Output ("release: " + $Out + " (" + [Math]::Round($size, 2) + " MB)")
