param([string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe')
$ErrorActionPreference = 'Stop'
$taskProject = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (!(Test-Path -LiteralPath $UnityPath)) { throw "Unity executable missing: $UnityPath" }
if (Test-Path -LiteralPath (Join-Path $taskProject 'Temp\UnityLockfile')) { throw 'Close this project in Unity before running the batch build.' }
$taskArguments = @('-batchmode','-quit','-projectPath',('"' + $taskProject + '"'),'-executeMethod','PrototypePipeline.Build','-logFile',('"' + (Join-Path $taskProject 'build.log') + '"'))
$taskBuild = Start-Process -FilePath $UnityPath -ArgumentList $taskArguments -WindowStyle Hidden -PassThru -Wait
if ($taskBuild.ExitCode -ne 0) { throw "Unity build failed ($($taskBuild.ExitCode)). Read build.log." }
Write-Host "Build ready: $(Join-Path $taskProject 'Builds\Windows\HeroesOfMetalAge.exe')"
