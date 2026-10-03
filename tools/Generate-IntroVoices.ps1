# Reproducible, explicitly synthetic control-orientation speech. No clinical text is generated here.
param([switch]$Force)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$manifest = Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot 'IntroVoiceLines.json') | ConvertFrom-Json
$destination = Join-Path $projectRoot 'Assets/_Project/Resources/Audio/Intro'
New-Item -ItemType Directory -Force -Path $destination | Out-Null
Add-Type -AssemblyName System.Runtime.WindowsRuntime
$null = [Windows.Media.SpeechSynthesis.SpeechSynthesizer, Windows.Media.SpeechSynthesis, ContentType = WindowsRuntime]
$null = [Windows.Storage.Streams.DataReader, Windows.Storage.Streams, ContentType = WindowsRuntime]
$asTask = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]
function Await-VoiceOperation($operation, [Type]$type) {
    $task = $asTask.MakeGenericMethod($type).Invoke($null, @($operation))
    if (!$task.Wait(60000)) { throw 'Speech synthesis exceeded the 60 second operation limit.' }
    $task.Result
}
$synth = New-Object Windows.Media.SpeechSynthesis.SpeechSynthesizer
try {
    $voice = [Windows.Media.SpeechSynthesis.SpeechSynthesizer]::AllVoices | Where-Object { $_.DisplayName -eq $manifest.voice } | Select-Object -First 1
    if ($null -eq $voice) { throw "Required Spanish voice is not installed: $($manifest.voice)" }
    $synth.Voice = $voice
    foreach ($line in $manifest.lines) {
        $target = Join-Path $destination ($line.id + '.wav')
        if ((Test-Path -LiteralPath $target) -and -not $Force) { continue }
        $text = [System.Security.SecurityElement]::Escape($line.text)
        $ssml = "<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='$($voice.Language)'><prosody rate='+4%' pitch='+0%'>$text</prosody></speak>"
        $stream = Await-VoiceOperation ($synth.SynthesizeSsmlToStreamAsync($ssml)) ([Windows.Media.SpeechSynthesis.SpeechSynthesisStream])
        $reader = New-Object Windows.Storage.Streams.DataReader($stream.GetInputStreamAt(0))
        try {
            $size = [uint32]$stream.Size
            Await-VoiceOperation ($reader.LoadAsync($size)) ([uint32]) | Out-Null
            $bytes = New-Object byte[] $size
            $reader.ReadBytes($bytes)
            [System.IO.File]::WriteAllBytes($target, $bytes)
            Write-Output ("{0}: {1} bytes, synthetic Spanish orientation" -f $line.id, $size)
        } finally { $reader.Dispose(); $stream.Dispose() }
    }
} finally { $synth.Dispose() }
foreach ($line in $manifest.lines) {
    $target = Join-Path $destination ($line.id + '.wav')
    if ((Test-Path -LiteralPath $target) -and !(Test-Path -LiteralPath ($target + '.meta'))) {
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
    sampleRateSetting: 0
    sampleRateOverride: 16000
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
  userData: Synthetic Spanish orientation; transcript in tools/IntroVoiceLines.json
  assetBundleName:
  assetBundleVariant:
"@
        [IO.File]::WriteAllText($target + '.meta', $metadata.TrimEnd() + "`n", (New-Object Text.UTF8Encoding($false)))
    }
    elseif (Test-Path -LiteralPath ($target + '.meta')) {
        # Preserve the native Windows speech rate (currently 16 kHz). Optimize may
        # discard detail; an override above the source rate cannot create detail.
        $metadata = [IO.File]::ReadAllText($target + '.meta')
        $metadata = $metadata -replace '(?m)^(\s+)sampleRateSetting: \d+\r?$', '${1}sampleRateSetting: 0'
        [IO.File]::WriteAllText($target + '.meta', $metadata, (New-Object Text.UTF8Encoding($false)))
    }
}
