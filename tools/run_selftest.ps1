# In-game self-test: build, validate XML, run -quicktest -pmselftest, print report and log errors.
#   powershell -ExecutionPolicy Bypass -File tools\run_selftest.ps1
# (ASCII only: Windows PowerShell 5.1 reads BOM-less UTF-8 as ANSI.)
$ErrorActionPreference = "Continue"
$root = Split-Path $PSScriptRoot -Parent
$game = "C:\Program Files (x86)\Steam\steamapps\common\RimWorld\RimWorld.exe"
$data = Join-Path $env:USERPROFILE "AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios"
$report = Join-Path $data "ParamedicSelfTest.txt"

if (Get-Process RimWorld -ErrorAction SilentlyContinue) { Write-Output "Game is running - close it first."; exit 1 }

$build = dotnet build (Join-Path $root "Source\Paramedic.csproj") -c Release -nologo -v q 2>&1 | Out-String
$errs = $build -split "`n" | Where-Object { $_ -match " error " }
if ($errs) { $errs | Select-Object -First 10; exit 1 }

python (Join-Path $root "tools\validate_xml.py")
if ($LASTEXITCODE -ne 0) { exit 1 }

if (Test-Path $report) { Remove-Item -LiteralPath $report }
$p = Start-Process -FilePath $game -ArgumentList "-quicktest", "-pmselftest" -PassThru
$t = 0
while (-not (Test-Path $report) -and -not $p.HasExited -and $t -lt 540) { Start-Sleep 5; $t += 5 }
Start-Sleep 3
if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force }

if (Test-Path $report) { Get-Content $report -Encoding UTF8 } else { Write-Output ("NO REPORT after " + $t + " s") }
Write-Output "==== Player.log: Paramedic / errors"
Select-String -Path (Join-Path $data "Player.log") -Pattern "Paramedic|PM_|^Exception|^Error" |
    Select-Object -First 25 | ForEach-Object { $_.Line.Substring(0, [Math]::Min(300, $_.Line.Length)) }
