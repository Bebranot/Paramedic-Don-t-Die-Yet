# CE self-test runner: prepares a Combat Extended run of the in-game self-test.
#   powershell -ExecutionPolicy Bypass -File tools\run_selftest_ce.ps1 [-Run] [-Restore] [-NoDeploy] [-NoLoadSave] [-VanillaSave <file>] [-Force] [-CEOnly]
# (ASCII only: Windows PowerShell 5.1 reads BOM-less UTF-8 as ANSI. Cyrillic literals are
#  avoided; where the game writes Russian text it is matched with a \uXXXX regex.)
#
# Default (no -Run): build, validate XML, deploy the mod into the game's Mods folder,
#   swap ModsConfig.xml to tools\modlists\ce.xml (or ce_only.xml with -CEOnly) (backup in C:\RimWorldConfigBackup) and
#   print the exact launch command. The game is launched by the manager, not by this script.
# -Run: also launch the game, wait for the report, print mode/result/log errors and the
#   save-load check, then ALWAYS restore the original ModsConfig.xml in finally.
# -Restore: put the original ModsConfig.xml back (also done automatically by -Run).
# -CEOnly: use tools\modlists\ce_only.xml (CE without Armory) instead of ce.xml.
#
# Rule: the original ModsConfig.xml is never lost - it is copied to
#   C:\RimWorldConfigBackup\ModsConfig.original.xml before the swap and restored from there.
param(
    [switch]$Run,
    [switch]$Restore,
    [switch]$NoDeploy,
    [switch]$NoLoadSave,
    [string]$VanillaSave = "",
    [switch]$Force,
    [switch]$CEOnly
)

$ErrorActionPreference = "Continue"

$root       = Split-Path $PSScriptRoot -Parent
$game       = "C:\Program Files (x86)\Steam\steamapps\common\RimWorld\RimWorldWin64.exe"
$gameMods   = "C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\Paramedic"
$steamMods  = "C:\Program Files (x86)\Steam\steamapps\workshop\content\294100"
$data       = Join-Path $env:USERPROFILE "AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios"
$config     = Join-Path $data "Config\ModsConfig.xml"
$report     = Join-Path $data "ParamedicSelfTest.txt"
$loadSaveTx = Join-Path $data "DutyOptimLoadSave.txt"
$backupDir  = "C:\RimWorldConfigBackup"
$origBak    = Join-Path $backupDir "ModsConfig.original.xml"
$stateFile  = Join-Path $backupDir "ModsConfig.state.txt"
$modlist    = Join-Path $PSScriptRoot "modlists\ce.xml"
if ($CEOnly) { $modlist = Join-Path $PSScriptRoot "modlists\ce_only.xml" }

function Fail([string]$msg) { Write-Output $msg; exit 1 }

# Directory.Build.props expects <Harmony>\Current\Assemblies, but the Harmony mod now ships
# versioned folders (1.4..1.7). Resolve the folder for the game version and export HarmonyDir,
# which MSBuild picks up as a property (the child make_release.ps1 inherits it too).
function Resolve-HarmonyDir {
    $base = Join-Path $steamMods "2009463077"
    if (Test-Path -LiteralPath (Join-Path $base "Current\Assemblies\0Harmony.dll")) { return (Join-Path $base "Current\Assemblies") }
    $ver = ""
    if (Test-Path -LiteralPath $config) {
        [xml]$mc = Get-Content -LiteralPath $config -Encoding UTF8
        if ($mc.ModsConfigData.version -match "^(\d+\.\d+)") { $ver = $Matches[1] }
    }
    $candidates = @()
    if ($ver -ne "") { $candidates += (Join-Path $base ($ver + "\Assemblies")) }
    foreach ($d in (Get-ChildItem -LiteralPath $base -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending)) {
        $candidates += (Join-Path $d.FullName "Assemblies")
    }
    foreach ($c in $candidates) { if (Test-Path -LiteralPath (Join-Path $c "0Harmony.dll")) { return $c } }
    return ""
}

function Restore-Original {
    if (Test-Path -LiteralPath $origBak) {
        Copy-Item -LiteralPath $origBak -Destination $config -Force
        Remove-Item -LiteralPath $stateFile -Force -ErrorAction SilentlyContinue
        Write-Output ("ModsConfig.xml restored from " + $origBak)
    } else {
        Write-Output ("nothing to restore (" + $origBak + " not found)")
    }
}

function Backup-Original {
    New-Item -ItemType Directory -Force -Path $backupDir | Out-Null
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $copy  = Join-Path $backupDir ("ModsConfig-" + $stamp + ".xml")
    Copy-Item -LiteralPath $config -Destination $copy -Force
    Copy-Item -LiteralPath $config -Destination $origBak -Force
    Set-Content -LiteralPath $stateFile -Value $stamp -Encoding ASCII
    Write-Output ("backup: " + $copy)
}

# Writes the CE mod set into the real ModsConfig.xml, keeping version and knownExpansions.
function Set-CeModsConfig {
    if (-not (Test-Path -LiteralPath $modlist)) { Fail ("modlist not found: " + $modlist) }
    [xml]$src = Get-Content -LiteralPath $config -Encoding UTF8
    [xml]$ce  = Get-Content -LiteralPath $modlist -Encoding UTF8
    $ids = @($ce.ModsConfigData.activeMods.li)
    if ($ids.Count -eq 0) { Fail "modlist has no <activeMods>" }

    $doc  = New-Object System.Xml.XmlDocument
    $doc.AppendChild($doc.CreateXmlDeclaration("1.0", "utf-8", $null)) | Out-Null
    $rootNode = $doc.CreateElement("ModsConfigData")
    $doc.AppendChild($rootNode) | Out-Null
    if ($src.ModsConfigData.version) {
        $e = $doc.CreateElement("version"); $e.InnerText = [string]$src.ModsConfigData.version; $rootNode.AppendChild($e) | Out-Null
    }
    $am = $doc.CreateElement("activeMods")
    foreach ($id in $ids) { $li = $doc.CreateElement("li"); $li.InnerText = [string]$id; $am.AppendChild($li) | Out-Null }
    $rootNode.AppendChild($am) | Out-Null
    if ($src.ModsConfigData.knownExpansions) {
        $ke = $doc.CreateElement("knownExpansions")
        foreach ($x in @($src.ModsConfigData.knownExpansions.li)) { $li = $doc.CreateElement("li"); $li.InnerText = [string]$x; $ke.AppendChild($li) | Out-Null }
        $rootNode.AppendChild($ke) | Out-Null
    }
    $doc.Save($config)
}

# Prints Player.log errors, dropping the CE chatter (startup banner, knowledge keys, Unity lib noise).
function Show-LogErrors {
    $log = Join-Path $data "Player.log"
    if (-not (Test-Path -LiteralPath $log)) { Write-Output "no Player.log"; return }
    $lines = Get-Content -LiteralPath $log -Encoding UTF8
    Write-Output "==== Player.log errors (CE noise filtered)"
    Write-Output ("NRE: " + @($lines | Where-Object { $_ -match "NullReferenceException" }).Count +
        ", different thread: " + @($lines | Where-Object { $_ -match "different thread" }).Count +
        ", Exception: " + @($lines | Where-Object { $_ -match "Exception" -and $_ -notmatch "ArgumentNullException" }).Count +
        ", Could not/Failed: " + @($lines | Where-Object { $_ -match "Could not|Failed" -and $_ -notmatch "0 failed|Failed Allocations|Failed count" }).Count)
    $noise = "^\s*(Loading Combat Extended|Please report issues with CE|Combat Extended ::|Knowledge data was missing key CE_)|Fallback handler could not load library"
    $errs = @($lines | Where-Object { $_ -match "Exception|NullReferenceException|different thread|Could not|Failed" } |
        Where-Object { $_ -notmatch $noise } |
        Where-Object { $_ -notmatch "0 failed|Failed Allocations|Failed count" })
    $errs | Group-Object | Sort-Object Count -Descending | Select-Object -First 25 | ForEach-Object {
        $text = $_.Name.Trim()
        Write-Output ("  " + $_.Count + " x " + $text.Substring(0, [Math]::Min(240, $text.Length)))
    }
}

# Reads ParamedicSelfTest.txt and prints the per-mode verdict (req. 3a).
function Show-Selftest {
    if (-not (Test-Path -LiteralPath $report)) { Write-Output ("NO REPORT: " + $report); return }
    $text = Get-Content -LiteralPath $report -Raw -Encoding UTF8
    $mode = "?"; $verdict = "?"
    if ($text -match "(?m)^mode: (.+)$") { $mode = $Matches[1].Trim() }
    if ($text -match "Paramedic self-test: (PASS|\d+ FAIL)") { $verdict = $Matches[1] }
    Write-Output "==== ParamedicSelfTest.txt"
    Write-Output $text
    Write-Output "SUMMARY"
    Write-Output ("  mode: " + $mode)
    Write-Output ("  selftest: " + $verdict)
    Write-Output ("  file: " + $report)
}

# Loads a save with CE active and checks the DutyOptim -dutyloadsave verdict (req. 3a).
function Test-LoadSave([string]$saveName) {
    $file = Join-Path $data ("Saves\" + $saveName + ".rws")
    if (-not (Test-Path -LiteralPath $file)) { Write-Output ("load-save check skipped: " + $file + " not found"); return }
    if (Test-Path -LiteralPath $loadSaveTx) { Remove-Item -LiteralPath $loadSaveTx -Force }
    Write-Output ("==== loading save '" + $saveName + "' with CE (-dutyloadsave)")
    $p = Start-Process -FilePath $game -ArgumentList "-dutyloadsave=$saveName" -PassThru
    $t = 0
    while (-not (Test-Path -LiteralPath $loadSaveTx) -and -not $p.HasExited -and $t -lt 540) { Start-Sleep 5; $t += 5 }
    Start-Sleep 3
    if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force }
    if (-not (Test-Path -LiteralPath $loadSaveTx)) { Write-Output ("no DutyOptimLoadSave.txt after " + $t + " s"); return }
    $txt = [System.IO.File]::ReadAllText($loadSaveTx, [System.Text.Encoding]::UTF8)
    Write-Output $txt
    # DutyOptim writes "errors (since session start) N" in Russian; matched as \uXXXX to keep this file ASCII
    $m = [regex]::Match($txt, '\u043E\u0448\u0438\u0431\u043E\u043A[^\d]*(\d+)')
    if ($m.Success) {
        $n = $m.Groups[1].Value
        Write-Output ("save-load errors: " + $n + " " + $(if ($n -eq "0") { "OK" } else { "PROBLEM" }))
    } else {
        Write-Output "save-load verdict not parsed - read DutyOptimLoadSave.txt above"
    }
}

# ---------------------------------------------------------------- entry point

if (Get-Process RimWorldWin64 -ErrorAction SilentlyContinue) { Write-Output "Game is running - close it first."; exit 1 }

if ($Restore) { Restore-Original; exit 0 }

# Track A must drop CE from <incompatibleWith>, otherwise RimWorld drops one of the two mods
$aboutPath = Join-Path $root "About\About.xml"
$about = Get-Content -LiteralPath $aboutPath -Raw -Encoding UTF8
if (($about -match "(?s)<incompatibleWith>.*?CETeam\.CombatExtended.*?</incompatibleWith>") -and -not $Force) {
    Write-Output "REFUSING TO PREPARE: About\About.xml lists CETeam.CombatExtended in <incompatibleWith>."
    Write-Output "RimWorld will refuse to load both mods together, so the run would silently test vanilla."
    Write-Output "Track A must remove that line (see docs\ce\REPORT_C.md). Re-run with -Force to prepare anyway."
    exit 1
}

Write-Output "== build"
$harmonyDir = Resolve-HarmonyDir
if ($harmonyDir -eq "") { Write-Output "WARNING: 0Harmony.dll not found - build may fail; pass -p:HarmonyDir=... manually" }
else { $env:HarmonyDir = $harmonyDir; Write-Output ("HarmonyDir: " + $harmonyDir) }
$build = dotnet build (Join-Path $root "Source\Paramedic.csproj") -c Release -nologo -v q 2>&1 | Out-String
$errs = $build -split "`n" | Where-Object { $_ -match " error " }
if ($errs) { $errs | Select-Object -First 10; exit 1 }

Write-Output "== validate XML"
python (Join-Path $root "tools\validate_xml.py")
if ($LASTEXITCODE -ne 0) { exit 1 }

if (Test-Path -LiteralPath (Join-Path $steamMods "2890901044")) { Write-Output "CE: found workshop 2890901044" }
else { Write-Output "WARNING: Combat Extended workshop folder 2890901044 not found" }
if (Test-Path -LiteralPath (Join-Path $steamMods "2911110083")) { Write-Output "CE Armory: found workshop 2911110083" }
else { Write-Output "WARNING: CE Armory workshop folder 2911110083 not found" }

if (-not $NoDeploy) {
    Write-Output "== deploy mod"
    if (-not (Test-Path -LiteralPath (Split-Path $gameMods -Parent))) { Fail ("game Mods folder not found: " + (Split-Path $gameMods -Parent)) }
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "tools\make_release.ps1") -Out $gameMods
    if ($LASTEXITCODE -ne 0) { Fail "deploy failed (make_release.ps1)" }
}

Write-Output "== ModsConfig.xml"
if (Test-Path -LiteralPath $stateFile) { Write-Output "note: a previous CE config is still in place - restoring before preparing"; Restore-Original }
Backup-Original
Set-CeModsConfig
Write-Output ("CE mod set written to " + $config)

try {
    if ($Run) {
        # copy the vanilla-made save BEFORE the self-test overwrites ParamedicSelfTest.rws
        $crossSave = ""
        if ($VanillaSave -ne "") {
            if (Test-Path -LiteralPath $VanillaSave) {
                New-Item -ItemType Directory -Force -Path (Join-Path $data "Saves") | Out-Null
                $crossSave = "PMCrossCheck"
                Copy-Item -LiteralPath $VanillaSave -Destination (Join-Path $data "Saves\PMCrossCheck.rws") -Force
                Write-Output ("cross-mode save copied for the load check: " + (Join-Path $data "Saves\PMCrossCheck.rws"))
            } else {
                Write-Output ("-VanillaSave not found: " + $VanillaSave)
            }
        }

        if (Test-Path -LiteralPath $report) { Remove-Item -LiteralPath $report -Force }
        Write-Output "== launch CE self-test"
        $p = Start-Process -FilePath $game -ArgumentList "-quicktest", "-pmselftest" -PassThru
        $t = 0
        while (-not (Test-Path -LiteralPath $report) -and -not $p.HasExited -and $t -lt 720) { Start-Sleep 5; $t += 5 }
        Start-Sleep 3
        if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force }

        Show-Selftest
        if (-not $NoLoadSave) {
            if ($crossSave -ne "") { Test-LoadSave $crossSave } else { Test-LoadSave "ParamedicSelfTest" }
        }
        Show-LogErrors
    } else {
        Write-Output ""
        Write-Output "PREPARED. The game is NOT launched by this script - the manager runs it:"
        Write-Output ('  "' + $game + '" -quicktest -pmselftest')
        Write-Output "After the run, restore your config:"
        Write-Output "  powershell -ExecutionPolicy Bypass -File tools\run_selftest_ce.ps1 -Restore"
        Write-Output "(or do the whole cycle yourself: -Run, which always restores in finally)"
        Show-LogErrors
    }
} finally {
    if ($Run) { Restore-Original }
}
