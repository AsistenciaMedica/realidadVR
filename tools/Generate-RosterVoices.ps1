# Reproducible local Spanish TTS for the existing roster text; does not edit clinical content.
# Keep exact transcripts in the manifest so runtime playback cannot reveal an unauthored reply.
param([switch]$Force)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$roster = Get-Content -Raw -Encoding UTF8 (Join-Path $projectRoot 'Assets/_Project/Resources/PatientRoster.json') | ConvertFrom-Json
$medical = Get-Content -Raw -Encoding UTF8 (Join-Path $projectRoot 'Assets/_Project/Resources/MedicalScenarios.json') | ConvertFrom-Json
$out = Join-Path $projectRoot 'Assets/_Project/Resources/Audio/Roster'
New-Item -ItemType Directory -Force $out | Out-Null
$lines = New-Object System.Collections.Generic.List[object]
function Add-Line($profile, [string]$speaker, [string]$text, [string]$sex) {
    if ([string]::IsNullOrWhiteSpace($text)) { return }
    if ($lines | Where-Object { $_.scenarioId -eq $profile.scenarioId -and $_.speaker -eq $speaker -and $_.text -ceq $text }) { return }
    $id = '{0}-{1:d3}' -f $profile.id.ToLowerInvariant(), $lines.Count
    $lines.Add([pscustomobject]@{scenarioId=$profile.scenarioId; speaker=$speaker; text=$text; resource="Audio/Roster/$id"; voice=$(if ($sex -eq 'female') {'Microsoft Sabina'} else {'Microsoft Raul'})})
}
function Short-Reply([string]$text) {
    $phrase = (($text -split '[.;!?]' | Where-Object { $_.Length -gt 0 }) | Select-Object -First 1)
    if ($null -eq $phrase) { $phrase = $text }
    return (($phrase.Trim() -split ' ' | Where-Object { $_.Length -gt 0 } | Select-Object -First 9) -join ' ') + [char]0x2026
}
foreach ($profile in $roster.profiles) {
    if ($profile.scenarioId -eq 'review-hypotension-v2') { continue } # Existing CASE 01 voice performance.
    $patient = $profile.displayName
    $years = 'a' + [char]0xf1 + 'os'
    Add-Line $profile $patient "Me llamo $patient. Tengo $($profile.age) $years." $profile.sex
    Add-Line $profile $patient ($patient + [char]0x2026 + " $($profile.age) $years" + [char]0x2026) $profile.sex
    foreach ($reply in @($profile.patientOpeningLine, $profile.historyReply)) {
        Add-Line $profile $patient $reply $profile.sex
        if (![string]::IsNullOrWhiteSpace($reply)) { Add-Line $profile $patient (Short-Reply $reply) $profile.sex }
    }
    $definition = $medical.scenarios | Where-Object { $_.id -eq $profile.scenarioId } | Select-Object -First 1
    $witnessSex = if ($definition.environment -eq 'football') {'male'} else {'female'}
    foreach ($reply in $profile.witnessLines) { Add-Line $profile 'Testigo' $reply $witnessSex }
}

Add-Type -AssemblyName System.Runtime.WindowsRuntime
$null = [Windows.Media.SpeechSynthesis.SpeechSynthesizer, Windows.Media.SpeechSynthesis, ContentType = WindowsRuntime]
$null = [Windows.Storage.Streams.DataReader, Windows.Storage.Streams, ContentType = WindowsRuntime]
$asTask = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]
function Await($operation, [Type]$type) { $task = $asTask.MakeGenericMethod($type).Invoke($null, @($operation)); $task.Wait() | Out-Null; $task.Result }
$synth = New-Object Windows.Media.SpeechSynthesis.SpeechSynthesizer
$allVoices = [Windows.Media.SpeechSynthesis.SpeechSynthesizer]::AllVoices
foreach ($line in $lines) {
    $file = Join-Path $projectRoot ('Assets/_Project/Resources/' + $line.resource + '.wav')
    if (!(Test-Path -LiteralPath $file) -or $Force) {
        $voice = $allVoices | Where-Object { $_.DisplayName -eq $line.voice } | Select-Object -First 1
        if ($null -eq $voice) { throw "Voice not installed: $($line.voice)" }
        $synth.Voice = $voice
        $escaped = [System.Security.SecurityElement]::Escape($line.text)
        $ssml = "<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='$($voice.Language)'><prosody rate='-3%' volume='95'>$escaped</prosody></speak>"
        $stream = Await ($synth.SynthesizeSsmlToStreamAsync($ssml)) ([Windows.Media.SpeechSynthesis.SpeechSynthesisStream])
        $reader = New-Object Windows.Storage.Streams.DataReader($stream.GetInputStreamAt(0))
        $size = [uint32]$stream.Size
        Await ($reader.LoadAsync($size)) ([uint32]) | Out-Null
        $bytes = New-Object byte[] $size
        $reader.ReadBytes($bytes)
        [IO.File]::WriteAllBytes($file, $bytes)
    }
    if (!(Test-Path -LiteralPath ($file + '.meta'))) {
        $guid = [guid]::NewGuid().ToString('N')
        $metadata = @"
fileFormatVersion: 2
guid: $guid
AudioImporter:
  externalObjects: {}
  serializedVersion: 8
  defaultSettings:
    serializedVersion: 2
    loadType: 1
    sampleRateSetting: 1
    sampleRateOverride: 22050
    compressionFormat: 1
    quality: 0.65
    conversionMode: 0
    preloadAudioData: 0
  platformSettingOverrides: {}
  forceToMono: 1
  normalize: 1
  loadInBackground: 0
  ambisonic: 0
  3D: 1
  userData:
  assetBundleName:
  assetBundleVariant:
"@
        [IO.File]::WriteAllText($file + '.meta', $metadata.TrimEnd() + "`n", (New-Object Text.UTF8Encoding($false)))
    }
}
$manifest = [pscustomobject]@{note='Synthetic Spanish voices generated locally with Windows SpeechSynthesis. Source text is the unchanged PatientRoster.json; actual playback requires an exact transcript match.'; lines=$lines.ToArray()}
[IO.File]::WriteAllText((Join-Path $projectRoot 'Assets/_Project/Resources/Audio/RosterVoices.json'), ($manifest | ConvertTo-Json -Depth 5), (New-Object Text.UTF8Encoding($false)))
Write-Output ('ROSTER_VOICES_OK clips={0} patients={1}' -f $lines.Count, (($roster.profiles | Where-Object { $_.scenarioId -ne 'review-hypotension-v2' }).Count))
