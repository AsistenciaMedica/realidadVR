param(
    [ValidateSet('Sync','EditMode','PlayMode','BuildDesktop','Capture')][string]$Task = 'EditMode',
    [string]$Label = 'current',
    [string]$TestFilter = ''
)
$ErrorActionPreference = 'Stop'
$sourceRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$labRoot = 'C:\Users\Juan\vital-lab'
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
if ($Label -notmatch '^[a-zA-Z0-9_-]+$') { throw 'Label must contain only letters, numbers, underscores or hyphens.' }
$evidence = Join-Path $sourceRoot 'TestResults\astra'
$output = Join-Path $sourceRoot 'Builds\Astra\VITAL-VR.exe'
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
if ($Task -eq 'Sync') {
    foreach ($folder in @('Assets','Packages','ProjectSettings')) {
        robocopy (Join-Path $sourceRoot $folder) (Join-Path $labRoot $folder) /E /R:1 /W:1 /NFL /NDL /NJH /NJS /NP
        if ($LASTEXITCODE -ge 8) { throw "Failed to sync $folder" }
    }
    Write-Output 'ASTRA_LAB_SYNC_OK'
    exit 0
}
$log = Join-Path $evidence ($Label + '-' + $Task.ToLowerInvariant() + '.log')
if ($Task -eq 'Capture') {
    if (-not (Test-Path -LiteralPath $output)) { throw 'BuildDesktop must succeed first.' }
    $capture = Join-Path $evidence $Label
    if (Test-Path -LiteralPath (Join-Path $capture 'result.txt')) { throw 'Evidence already exists. Choose a new label to preserve the previous capture.' }
    New-Item -ItemType Directory -Path $capture -Force | Out-Null
    $argsList = @('-vital-quest-look','-vital-patient-roster-smoke','-vital-capture-directory',('"'+$capture+'"'),
        '-screen-width','2064','-screen-height','2208','-screen-fullscreen','0','-logFile',('"'+$log+'"'))
    $process = Start-Process -FilePath $output -ArgumentList $argsList -WorkingDirectory $sourceRoot -WindowStyle Hidden -PassThru
    $process.WaitForExit()
    if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -Pattern 'VITAL_PATIENT_ROSTER_SMOKE PASS' -Quiet)) {
        throw "Quest-look capture failed: $log"
    }
} else {
    $argsList = @('-batchmode','-projectPath',('"'+$labRoot+'"'),'-buildTarget','Win64','-logFile',('"'+$log+'"'))
    if ($Task -eq 'BuildDesktop') {
        New-Item -ItemType Directory -Path (Split-Path $output) -Force | Out-Null
        $argsList += @('-quit','-executeMethod','EmergencyVR.Editor.Case01Build.BuildDesktop','-case01-output',('"'+$output+'"'))
    } else {
        $xml = Join-Path $evidence ($Label + '-' + $Task.ToLowerInvariant() + '.xml')
        $argsList += @('-runTests','-testPlatform',$Task,'-testResults',('"'+$xml+'"'))
        if ($TestFilter) { $argsList += @('-testFilter',$TestFilter) }
    }
    $process = Start-Process -FilePath $unity -ArgumentList $argsList -WorkingDirectory $labRoot -WindowStyle Hidden -PassThru
    Write-Output ("Unity PID: " + $process.Id)
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "Unity $Task failed with exit $($process.ExitCode): $log" }
    if ($Task -eq 'BuildDesktop') {
        if (-not (Select-String -LiteralPath $log -Pattern 'CASE01_DESKTOP_BUILT' -Quiet)) { throw "Build success marker absent: $log" }
    } else {
        if (-not (Test-Path -LiteralPath $xml)) { throw "Test results absent: $xml" }
        [xml]$result = Get-Content -LiteralPath $xml -Raw
        if ($result.'test-run'.result -ne 'Passed') { throw "Tests did not pass: $xml" }
        Write-Output ("Tests: " + $result.'test-run'.passed + ' passed, ' + $result.'test-run'.failed + ' failed.')
    }
}
Write-Output ("ASTRA_$Task PASS. Evidence: $log")
