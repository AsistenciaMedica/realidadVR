param([string]$ProjectPath)
$ErrorActionPreference = 'Stop'
$sourceRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$projectRoot = if ($ProjectPath) { (Resolve-Path -LiteralPath $ProjectPath).Path } else { $sourceRoot }
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
$output = Join-Path $sourceRoot 'Builds\Case01\VITAL-VR.exe'
$log = Join-Path $sourceRoot 'TestResults\case01-build.log'
New-Item -ItemType Directory -Path (Split-Path $output), (Split-Path $log) -Force | Out-Null
$arguments = @('-batchmode','-quit','-projectPath',('"' + $projectRoot + '"'),'-buildTarget','Win64',
 '-executeMethod','EmergencyVR.Editor.Case01Build.BuildDesktop','-case01-output',('"' + $output + '"'),'-logFile',('"' + $log + '"'))
$process = Start-Process -FilePath $unity -ArgumentList $arguments -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru
$process.WaitForExit()
if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -Pattern 'CASE01_DESKTOP_BUILT' -Quiet)) { throw ('Desktop build failed: ' + $log) }
Write-Output ('Desktop build ready: ' + $output)
