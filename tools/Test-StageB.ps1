param(
    [ValidateSet('All','EditMode','PlayMode')]
    [string]$Mode = 'All',
    [string]$ProjectPath
)
$ErrorActionPreference = 'Stop'
$sourceRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$projectRoot = if ($ProjectPath) { (Resolve-Path -LiteralPath $ProjectPath).Path } else { $sourceRoot }
$unityExecutable = 'C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
$resultsDirectory = Join-Path $sourceRoot 'TestResults'
New-Item -ItemType Directory -Path $resultsDirectory -Force | Out-Null
$modes = if ($Mode -eq 'All') { @('EditMode','PlayMode') } else { @($Mode) }
foreach ($testMode in $modes) {
    $runId = [Guid]::NewGuid().ToString('N')
    $log = Join-Path $resultsDirectory ('stage-b-' + $testMode.ToLowerInvariant() + '-' + $runId + '.log')
    $xml = Join-Path $resultsDirectory ('stage-b-' + $testMode.ToLowerInvariant() + '-' + $runId + '.xml')
    $arguments = @('-batchmode','-projectPath',('"' + $projectRoot + '"'),'-buildTarget','Win64',
        '-runTests','-testPlatform',$testMode,'-testResults',('"' + $xml + '"'),'-logFile',('"' + $log + '"'))
    Write-Output ('Running all ' + $testMode + ' tests. Log: ' + $log)
    $process = Start-Process -FilePath $unityExecutable -ArgumentList $arguments -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru
    $process.WaitForExit()
    if (-not (Test-Path -LiteralPath $xml)) { throw ('Unity produced no test result. Exit ' + $process.ExitCode + '. Inspect ' + $log) }
    [xml]$report = Get-Content -LiteralPath $xml -Raw
    $run = $report.'test-run'
    Write-Output ($testMode + ': ' + $run.result + ', passed=' + $run.passed + ', failed=' + $run.failed + ', skipped=' + $run.skipped + '. XML: ' + $xml)
    if ($process.ExitCode -ne 0 -or $run.result -ne 'Passed' -or [int]$run.total -eq 0) { throw ('Test failure. Inspect ' + $xml + ' and ' + $log) }
    Copy-Item -LiteralPath $xml -Destination (Join-Path $resultsDirectory ('stage-b-' + $testMode.ToLowerInvariant() + '.xml'))
    Copy-Item -LiteralPath $log -Destination (Join-Path $resultsDirectory ('stage-b-' + $testMode.ToLowerInvariant() + '.log'))
}
