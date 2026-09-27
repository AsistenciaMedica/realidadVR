param(
    [ValidateSet('EditMode','PlayMode','Windows','Android')]
    [string]$Mode = 'EditMode'
)
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$unityExecutable = 'C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
$resultsDirectory = Join-Path $projectRoot 'TestResults'
New-Item -ItemType Directory -Path $resultsDirectory -Force | Out-Null
$log = Join-Path $resultsDirectory ('experience-' + $Mode.ToLowerInvariant() + '.log')
$target = if ($Mode -eq 'Android') { 'Android' } else { 'Win64' }
$unityArguments = @('-batchmode','-projectPath',('"' + $projectRoot + '"'),'-buildTarget',$target,'-logFile',('"' + $log + '"'))
if ($Mode -eq 'EditMode' -or $Mode -eq 'PlayMode') {
    $xml = Join-Path $resultsDirectory ('experience-' + $Mode.ToLowerInvariant() + '.xml')
    $unityArguments += @('-runTests','-testPlatform',$Mode,'-testResults',('"' + $xml + '"'))
} else {
    $method = if ($Mode -eq 'Windows') { 'EmergencyVR.Editor.DemoDistributionBuilder.BuildWindows' } else { 'EmergencyVR.Editor.QuestProjectSetup.BuildDevelopmentApk' }
    $unityArguments += @('-executeMethod',$method,'-quit')
}
$process = Start-Process -FilePath $unityExecutable -ArgumentList $unityArguments -WindowStyle Hidden -PassThru -Wait
Write-Output ('Unity ' + $Mode + ' exit: ' + $process.ExitCode + '. Log: ' + $log)
exit $process.ExitCode
