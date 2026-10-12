# Runs the EditMode tests, generates the bootstrap scene and builds the Windows development player.
param([string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe')
$ErrorActionPreference = 'Stop'

$project = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$reports = Join-Path $project 'Reports'
if (!(Test-Path -LiteralPath $UnityPath)) { throw "Unity executable missing: $UnityPath" }
if (Test-Path -LiteralPath (Join-Path $project 'Temp\UnityLockfile')) { throw 'Close this project in Unity before running the batch build.' }
New-Item -ItemType Directory -Force -Path $reports | Out-Null

function Invoke-Unity([string[]]$arguments, [string]$failure) {
    $run = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
    if ($run.ExitCode -ne 0) { throw "$failure ($($run.ExitCode))." }
}

$testResults = Join-Path $reports 'editmode-tests.xml'
Invoke-Unity @('-batchmode', '-projectPath', ('"' + $project + '"'), '-runTests', '-testPlatform', 'EditMode',
    '-assemblyNames', 'HeroesOfMetalAge.Tests.EditMode', '-testResults', ('"' + $testResults + '"'),
    '-logFile', ('"' + (Join-Path $reports 'editmode-tests.log') + '"')) 'EditMode tests failed. Read Reports/editmode-tests.xml'
Write-Host "Tests passed: $testResults"

Invoke-Unity @('-batchmode', '-quit', '-projectPath', ('"' + $project + '"'), '-executeMethod', 'HeroesOfMetalAge.EditorTools.PrototypePipeline.Build',
    '-logFile', ('"' + (Join-Path $project 'build.log') + '"')) 'Unity build failed. Read build.log'
Write-Host "Build ready: $(Join-Path $project 'Builds\Windows\HeroesOfMetalAge.exe')"
