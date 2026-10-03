# Reproducible local Spanish device prompts. Source text belongs to the presentation layer only.
param([switch]$Force)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$input = Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot 'AEDVoiceLines.json') | ConvertFrom-Json
$lines = $input.lines
New-Item -ItemType Directory -Force (Join-Path $projectRoot 'Assets/_Project/Resources/Audio/AED') | Out-Null
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

$manifest = [pscustomobject]@{note='Synthetic Spanish device voice generated locally with Windows SpeechSynthesis (Microsoft Sabina). Text mirrors existing AED display messages, not clinical logic. This is not an external CC0 recording.'; lines=$lines}
[IO.File]::WriteAllText((Join-Path $projectRoot 'Assets/_Project/Resources/Audio/AEDVoices.json'), ($manifest | ConvertTo-Json -Depth 5), (New-Object Text.UTF8Encoding($false)))
Write-Output ('AED_VOICES_OK clips={0}' -f $lines.Count)
