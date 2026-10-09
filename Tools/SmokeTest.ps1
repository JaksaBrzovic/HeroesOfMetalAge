$ErrorActionPreference = 'Stop'
$taskProject = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskPlayer = Join-Path $taskProject 'Builds\Windows\HeroesOfMetalAge.exe'
if (!(Test-Path -LiteralPath $taskPlayer)) { throw 'Build the player first using Tools/Build.ps1.' }
$taskReport = Join-Path $taskProject 'Reports\runtime-smoke.txt'
if (Test-Path -LiteralPath $taskReport) { Remove-Item -LiteralPath $taskReport }
$taskArguments = @('--smoke-test','-screen-fullscreen','0','-screen-width','1600','-screen-height','900','-logFile',('"' + (Join-Path $taskProject 'Reports\player-smoke.log') + '"'))
# This is the actual playable game window; visibility is required for IMGUI screenshot verification.
$taskRun = Start-Process -FilePath $taskPlayer -ArgumentList $taskArguments -WindowStyle Normal -PassThru
if (!$taskRun.WaitForExit(120000)) { $taskRun.Kill(); throw 'Runtime smoke test timed out. Read Reports/player-smoke.log.' }
if ($taskRun.ExitCode -ne 0 -or !(Test-Path -LiteralPath $taskReport)) { throw "Smoke test failed ($($taskRun.ExitCode)). Inspect Reports." }
Get-Content -LiteralPath $taskReport
