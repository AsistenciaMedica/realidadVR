param([ValidateRange(800,3840)][int]$Width=1440,[ValidateRange(600,2160)][int]$Height=900)
$ErrorActionPreference = 'Stop'
$sourceRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$player = Join-Path $sourceRoot 'Builds\Case01\VITAL-VR.exe'
$captures = Join-Path $sourceRoot ('Builds\Case01\Captures\' + $Width + 'x' + $Height)
$log = Join-Path $sourceRoot ('TestResults\case01-player-' + $Width + 'x' + $Height + '.log')
New-Item -ItemType Directory -Path $captures -Force | Out-Null
$arguments = @('-screen-width',$Width,'-screen-height',$Height,'-screen-fullscreen','0','-vital-case01-smoke',
 '-vital-case01-captures',('"' + $captures + '"'),'-logFile',('"' + $log + '"'))
$process = Start-Process -FilePath $player -ArgumentList $arguments -WorkingDirectory $sourceRoot -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(180000)) {
    Stop-Process -Id $process.Id -Force
    throw ('The CASE01 validation player exceeded its deadline: ' + $log)
}
if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -Pattern 'CASE01_PLAYER_SMOKE PASS' -Quiet)) { throw ('CASE01 player failed: ' + $log) }
Write-Output ('CASE01_PLAYER_SMOKE PASS. Captures: ' + $captures)
