param(
    [ValidateSet('Experience','Regression')][string]$Mode = 'Experience',
    [ValidateRange(800,3840)][int]$Width = 1440,
    [ValidateRange(600,2160)][int]$Height = 900
)
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$release = Get-Content -LiteralPath (Join-Path $projectRoot 'Builds\Windows\latest-build.json') -Raw | ConvertFrom-Json
$releaseDirectory = [IO.Path]::GetFullPath((Join-Path $projectRoot $release.directory))
$buildRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'Builds\Windows')) + [IO.Path]::DirectorySeparatorChar
if (-not $releaseDirectory.StartsWith($buildRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Release directory is outside the Windows build folder.' }
$executable = Join-Path $releaseDirectory 'EmergencyVR.exe'
if (-not (Test-Path -LiteralPath $executable)) { throw 'Build Windows before running acceptance.' }
$captureDirectory = Join-Path $projectRoot ('Builds\UXPreview\' + $Width + 'x' + $Height)
$log = Join-Path $projectRoot ('TestResults\experience-player-' + $Mode.ToLowerInvariant() + '-' + $Width + 'x' + $Height + '.log')
$playerArguments = @('-screen-width',$Width,'-screen-height',$Height,'-screen-fullscreen','0','-logFile',('"' + $log + '"'))
if ($Mode -eq 'Experience') { $playerArguments += @('-vital-ux-smoke','-vital-capture-directory',('"' + $captureDirectory + '"')) }
else { $playerArguments += '-demo-smoke-test' }
$process = Start-Process -FilePath $executable -ArgumentList $playerArguments -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru -Wait
$marker = if ($Mode -eq 'Experience') { 'VITAL_UX_SMOKE PASS' } else { 'DESKTOP_SMOKE PASS' }
if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -Pattern $marker -Quiet)) { throw ('Player acceptance failed: ' + $log) }
Write-Output ($marker + '. Captures: ' + $captureDirectory + '. Log: ' + $log)
