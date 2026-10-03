param([switch]$PreflightOnly, [switch]$VerifyExistingApk)
$ErrorActionPreference = 'Stop'
if ($PreflightOnly -and $VerifyExistingApk) { throw 'Choose either -PreflightOnly or -VerifyExistingApk.' }

function ConvertTo-AstraProcessArgument([string]$Argument) {
    # Windows command-line quoting for ProcessStartInfo.Arguments, including embedded
    # quotes and trailing backslashes. No shell interprets these arguments.
    '"' + [regex]::Replace([regex]::Replace($Argument, '(\\*)"', '$1$1\"'), '(\\+)$', '$1$1') + '"'
}

function Export-AstraPublicCertificate([string]$Keytool, [string]$Store, [string]$Alias, [string]$Destination) {
    # Oracle keytool: -exportcert defaults to binary DER; :env reads the password
    # from the child environment. No password or private key enters args or output.
    # https://docs.oracle.com/en/java/javase/17/docs/specs/man/keytool.html
    $start = New-Object System.Diagnostics.ProcessStartInfo
    $start.FileName = $Keytool
    $publicArguments = @('-exportcert', '-alias', $Alias, '-keystore', $Store,
        '-storepass:env', 'VITAL_KEYSTORE_PASS', '-file', $Destination)
    $start.Arguments = ($publicArguments | ForEach-Object { ConvertTo-AstraProcessArgument $_ }) -join ' '
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.RedirectStandardInput = $true
    $export = New-Object System.Diagnostics.Process
    $export.StartInfo = $start
    try {
        if (-not $export.Start()) { throw 'Cannot start public certificate export.' }
        $export.StandardInput.Close()
        # Successful keytool exports normally write a confirmation to stderr.
        # Capture both streams as data so PowerShell does not mistake this for an error.
        $stdout = $export.StandardOutput.ReadToEndAsync()
        $stderr = $export.StandardError.ReadToEndAsync()
        if (-not $export.WaitForExit(30000)) {
            $export.Kill()
            $export.WaitForExit()
            throw 'Public certificate export timed out.'
        }
        $null = $stdout.GetAwaiter().GetResult()
        $null = $stderr.GetAwaiter().GetResult()
        if ($export.ExitCode -ne 0) { throw ('Public certificate export failed; keytool exit code ' + $export.ExitCode + '.') }
        if (-not (Test-Path -LiteralPath $Destination -PathType Leaf) -or (Get-Item -LiteralPath $Destination).Length -eq 0) {
            throw 'Keytool did not produce a public DER certificate.'
        }
    } finally { $export.Dispose() }
}
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$labRoot = 'C:\Users\Juan\vital-lab'
$editorRoot = 'C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor'
$evidence = Join-Path $projectRoot 'TestResults/astra'
$signingFile = 'C:\Users\Juan\.vitalvr\LEEME-FIRMA.txt'
$keystore = 'C:\Users\Juan\.vitalvr\vitalvr-release.keystore'

# A release must be gated by linked, actual results. This script does not upload or invite anyone.
. (Join-Path $PSScriptRoot 'Test-AstraReleaseEvidence.ps1')
$validation = Get-AstraReleaseEvidence -ProjectRoot $projectRoot -LabRoot $labRoot -AllowAndroidShaderPrefilters
$commandPrefix = if ($VerifyExistingApk) { 'postflight' } else { 'final' }
$portalReportPath = Join-Path $evidence ($commandPrefix + '-portal-sync.txt')
$scopeReportPath = Join-Path $evidence ($commandPrefix + '-release-scope.txt')

Push-Location $projectRoot
try {
    & node tools/Sync-MedicalPortal.mjs | Tee-Object -FilePath $portalReportPath
    if ($LASTEXITCODE -ne 0) { throw 'Medical portal sync failed.' }
    & node --test tools/ReleaseScope.test.mjs | Tee-Object -FilePath $scopeReportPath
    if ($LASTEXITCODE -ne 0) { throw 'Release scope tests failed.' }
} finally { Pop-Location }
$preflight = [pscustomobject]@{ validation=$validation;
    portalSync=(Get-AstraEvidenceRecord $portalReportPath);
    releaseScopeTests=(Get-AstraEvidenceRecord $scopeReportPath) }
$preflightName = if ($VerifyExistingApk) { 'release-postflight.json' } else { 'release-preflight.json' }
$preflightPath = Join-Path $evidence $preflightName
$preflight | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $preflightPath -Encoding UTF8
if ($PreflightOnly) { Write-Output 'ASTRA_RELEASE_PREFLIGHT_VERIFIED. No signing values read; no Unity build or upload launched.'; exit 0 }

# Keep the local signing values in this process only; never print or copy them into the repository.
$lines = Get-Content -LiteralPath $signingFile -Encoding UTF8
$aliasLine = @($lines | Where-Object { $_ -match '^\s*Alias\s*:' })
$passwordLine = @($lines | Where-Object { $_ -match '(?i)(contrase|password).*:' })
if ($aliasLine.Count -ne 1 -or $passwordLine.Count -ne 1 -or -not (Test-Path -LiteralPath $keystore)) {
    throw 'Local signing instructions must contain one alias and one password entry.'
}
$alias = ($aliasLine[0] -split ':', 2)[1].Trim()
$password = ($passwordLine[0] -split ':', 2)[1].Trim()
if (-not $alias -or -not $password) { throw 'The local signing configuration is incomplete.' }
$savedEnvironment = @{}
foreach ($name in @('VITAL_VERSION_CODE','VITAL_KEYSTORE_PATH','VITAL_KEYSTORE_PASS','VITAL_KEY_ALIAS','VITAL_KEY_PASS','JAVA_HOME')) {
    $savedEnvironment[$name] = [System.Environment]::GetEnvironmentVariable($name, 'Process')
}
try {
    $env:VITAL_VERSION_CODE = '2'
    $env:VITAL_KEYSTORE_PATH = $keystore
    $env:VITAL_KEYSTORE_PASS = $password
    $env:VITAL_KEY_ALIAS = $alias
    $env:VITAL_KEY_PASS = $password
    $env:JAVA_HOME = Join-Path $editorRoot 'Data/PlaybackEngines/AndroidPlayer/OpenJDK'
    if (-not $VerifyExistingApk) {
        $log = Join-Path $evidence 'final-release-build.log'
        $arguments = @('-batchmode','-quit','-projectPath',('"' + $labRoot + '"'),'-buildTarget','Android',
            '-executeMethod','EmergencyVR.Editor.QuestProjectSetup.BuildReleaseApk','-logFile',('"' + $log + '"'))
        $process = Start-Process -FilePath (Join-Path $editorRoot 'Unity.exe') -ArgumentList $arguments -WorkingDirectory $labRoot -WindowStyle Hidden -PassThru
        $processHandle = $process.Handle
        Write-Output ('Release Unity PID: ' + $process.Id)
        $process.WaitForExit()
        $process.Refresh()
        if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -Pattern 'Release APK built:' -Quiet)) {
            throw "Release build failed; inspect $log"
        }
    }

    $apk = Join-Path $labRoot 'Builds/Android/VITAL-VR-0.1.1-2.apk'
    if (-not (Test-Path -LiteralPath $apk)) { throw 'Expected version 0.1.1, code 2 APK was not created.' }
    $sdkTools = Join-Path $editorRoot 'Data/PlaybackEngines/AndroidPlayer/SDK/build-tools/36.0.0'
    $aapt = Join-Path $sdkTools 'aapt2.exe'
    $badging = & $aapt dump badging $apk
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect APK identity.' }
    $badging | Set-Content -LiteralPath (Join-Path $evidence 'final-apk-badging.txt') -Encoding UTF8
    $packageLines = @($badging | Where-Object { $_ -cmatch '^package: ' })
    if ($packageLines.Count -ne 1 -or
        $packageLines[0] -cnotmatch "^package: name='com\.vitalvr\.training' versionCode='2' versionName='0\.1\.1'(?:\s|$)") {
        throw 'The APK package/version differs from the approved release.'
    }
    $manifest = & $aapt dump xmltree --file AndroidManifest.xml $apk
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect the merged APK manifest.' }
    $manifest | Set-Content -LiteralPath (Join-Path $evidence 'final-apk-manifest.txt') -Encoding UTF8
    if ($manifest -match 'android\.permission\.CAMERA|com\.google\.ar|com\.unity\.xr\.arcore|oculus\.software\.eye_tracking|com\.oculus\.permission\.EYE_TRACKING|android\.permission\.EYE_TRACKING_FINE') {
        throw 'Unexpected camera, ARCore or eye tracking requirement in the merged APK manifest.'
    }
    $signature = & (Join-Path $sdkTools 'apksigner.bat') verify --verbose --print-certs $apk
    if ($LASTEXITCODE -ne 0) { throw 'APK signature verification failed.' }
    $signature | Set-Content -LiteralPath (Join-Path $evidence 'final-apk-signature.txt') -Encoding UTF8
    $certificatePath = Join-Path $evidence 'final-release-signing-certificate.der'
    Export-AstraPublicCertificate -Keytool (Join-Path $env:JAVA_HOME 'bin/keytool.exe') -Store $keystore -Alias $alias -Destination $certificatePath
    $certificate = Get-AstraEvidenceRecord $certificatePath
    $signerLines = @($signature | Where-Object { $_ -cmatch '^Signer #[0-9]+ certificate SHA-256 digest:' })
    if ($signerLines.Count -ne 1 -or $signerLines[0] -cnotmatch '^Signer #1 certificate SHA-256 digest:\s*([0-9a-fA-F]{64})\s*$') {
        throw 'APK verification must identify exactly one signer certificate SHA-256 digest.'
    }
    $apkCertificateSha256 = $Matches[1].ToLowerInvariant()
    if ($apkCertificateSha256 -cne $certificate.sha256) {
        throw 'The APK signer certificate differs from the configured release keystore alias.'
    }
    $destination = Join-Path $projectRoot 'Builds/Astra/VITAL-VR-0.1.1-2.apk'
    New-Item -ItemType Directory -Path (Split-Path $destination) -Force | Out-Null
    Copy-Item -LiteralPath $apk -Destination $destination
    [pscustomobject]@{ package='com.vitalvr.training'; version='0.1.1'; versionCode=2;
        apk=$destination; sha256=(Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash;
        builtUtc=(Get-Item -LiteralPath $apk).LastWriteTimeUtc.ToString('O'); verifiedUtc=[DateTime]::UtcNow.ToString('O');
        verifiedExistingApk=[bool]$VerifyExistingApk; headsetTested=$false; uploaded=$false;
        preflight=(Get-AstraEvidenceRecord $preflightPath); validation=$validation;
        buildLog=(Get-AstraEvidenceRecord (Join-Path $evidence 'final-release-build.log'));
        apkIdentity=(Get-AstraEvidenceRecord (Join-Path $evidence 'final-apk-badging.txt'));
        apkManifest=(Get-AstraEvidenceRecord (Join-Path $evidence 'final-apk-manifest.txt'));
        apkSignature=(Get-AstraEvidenceRecord (Join-Path $evidence 'final-apk-signature.txt'));
        signingCertificate=$certificate; apkCertificateSha256=$apkCertificateSha256; signingCertificateMatches=$true } |
        ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $evidence 'final-release.json') -Encoding UTF8
    $successMarker = if ($VerifyExistingApk) { 'ASTRA_RELEASE_EXISTING_APK_VERIFIED ' } else { 'ASTRA_RELEASE_SIGNED_AND_INSPECTED ' }
    Write-Output ($successMarker + $destination)
} finally {
    foreach ($name in $savedEnvironment.Keys) { [System.Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process') }
    $password = $null
    $lines = $aliasLine = $passwordLine = $null
}
