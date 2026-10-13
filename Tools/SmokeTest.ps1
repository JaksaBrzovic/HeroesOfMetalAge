# Plays the whole mission in the built player with an isolated save and captures screenshots into Reports/.
$ErrorActionPreference = 'Stop'

$project = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$player = Join-Path $project 'Builds\Windows\HeroesOfMetalAge.exe'
$report = Join-Path $project 'Reports\runtime-smoke.txt'
if (!(Test-Path -LiteralPath $player)) { throw 'Build the player first using Tools/Build.ps1.' }
if (Test-Path -LiteralPath $report) { Remove-Item -LiteralPath $report }

$arguments = @('--smoke-test', '-screen-fullscreen', '0', '-screen-width', '1920', '-screen-height', '1080',
    '-logFile', ('"' + (Join-Path $project 'Reports\player-smoke.log') + '"'))
# the player window must be visible: screenshots are captured from the real swap chain
$run = Start-Process -FilePath $player -ArgumentList $arguments -WindowStyle Normal -PassThru
if (!$run.WaitForExit(120000)) { $run.Kill(); throw 'Runtime smoke test timed out. Read Reports/player-smoke.log.' }
if ($run.ExitCode -ne 0 -or !(Test-Path -LiteralPath $report)) { throw "Smoke test failed ($($run.ExitCode)). Inspect Reports." }
Get-Content -LiteralPath $report
