# Signed Quest APK for the Meta release channel, built straight from THIS project folder.
# Use it when Build-AstraRelease.ps1 is blocked by its validation-evidence gate and the build must still
# go out (for example the client test on Thursday). It does not upload anything.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/Build-QuestQuick.ps1 -VersionCode 4
#
# Requirements: Unity closed, Android platform already selected once in this project, the release keystore
# and its LEEME-FIRMA.txt in C:\Users\Juan\.vitalvr (same files used for 0.1.0 and 0.1.1).
# VersionCode must be higher than every build already uploaded (Alpha has 1 and 3, so 4 or more).
param([ValidateRange(2,2147483647)][int]$VersionCode = 4)
$ErrorActionPreference = 'Stop'

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$editorRoot = 'C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor'
$signingFile = 'C:\Users\Juan\.vitalvr\LEEME-FIRMA.txt'
$keystore = 'C:\Users\Juan\.vitalvr\vitalvr-release.keystore'
$log = Join-Path $projectRoot 'Builds\quest-quick-build.log'

if (-not (Test-Path -LiteralPath (Join-Path $editorRoot 'Unity.exe'))) { throw "Unity 6000.3.23f1 not found in $editorRoot" }
if (-not (Test-Path -LiteralPath $keystore) -or -not (Test-Path -LiteralPath $signingFile)) { throw 'Release keystore or LEEME-FIRMA.txt not found in C:\Users\Juan\.vitalvr' }
$versionLine = Select-String -LiteralPath (Join-Path $projectRoot 'ProjectSettings\ProjectSettings.asset') -Pattern '^\s*bundleVersion:\s*(\S+)' | Select-Object -First 1
$versionName = $versionLine.Matches[0].Groups[1].Value

# Signing values stay in this process only; nothing is printed or written to the repository.
$lines = Get-Content -LiteralPath $signingFile -Encoding UTF8
$aliasLine = @($lines | Where-Object { $_ -match '^\s*Alias\s*:' })
$passwordLine = @($lines | Where-Object { $_ -match '(?i)(contrase|password).*:' })
if ($aliasLine.Count -ne 1 -or $passwordLine.Count -ne 1) { throw 'LEEME-FIRMA.txt must contain one Alias line and one password line.' }
$alias = ($aliasLine[0] -split ':', 2)[1].Trim()
$password = ($passwordLine[0] -split ':', 2)[1].Trim()

New-Item -ItemType Directory -Force -Path (Join-Path $projectRoot 'Builds') | Out-Null
$saved = @{}
foreach ($name in @('VITAL_VERSION_CODE','VITAL_KEYSTORE_PATH','VITAL_KEYSTORE_PASS','VITAL_KEY_ALIAS','VITAL_KEY_PASS','JAVA_HOME')) {
    $saved[$name] = [System.Environment]::GetEnvironmentVariable($name, 'Process')
}
try {
    $env:VITAL_VERSION_CODE = [string]$VersionCode
    $env:VITAL_KEYSTORE_PATH = $keystore
    $env:VITAL_KEYSTORE_PASS = $password
    $env:VITAL_KEY_ALIAS = $alias
    $env:VITAL_KEY_PASS = $password
    $env:JAVA_HOME = Join-Path $editorRoot 'Data\PlaybackEngines\AndroidPlayer\OpenJDK'
    Write-Output "Building VITAL VR $versionName (code $VersionCode). This takes 15-40 minutes; progress in $log"
    $arguments = @('-batchmode','-quit','-projectPath',('"' + $projectRoot + '"'),'-buildTarget','Android',
        '-executeMethod','EmergencyVR.Editor.QuestProjectSetup.BuildReleaseApk','-logFile',('"' + $log + '"'))
    $process = Start-Process -FilePath (Join-Path $editorRoot 'Unity.exe') -ArgumentList $arguments -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "Unity build failed (exit $($process.ExitCode)). Look for 'error' near the end of $log" }
} finally {
    foreach ($name in $saved.Keys) { [System.Environment]::SetEnvironmentVariable($name, $saved[$name], 'Process') }
}

$apk = Join-Path $projectRoot ("Builds\Android\VITAL-VR-$versionName-$VersionCode.apk")
if (-not (Test-Path -LiteralPath $apk)) { throw "APK not found: $apk" }
$size = [math]::Round((Get-Item -LiteralPath $apk).Length / 1MB, 1)
Write-Output "OK: $apk ($size MB)."
Write-Output 'Next: install it on your own Quest first (adb install -r), test it, then Dashboard > Release channels > Alpha > Upload new build.'
