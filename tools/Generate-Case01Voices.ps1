# Generates placeholder WAV voices for CASE 01 from voice-lines.json with the local Windows speech voices.
# Output: Assets/_Project/Resources/Audio/Case01/<id>.wav (loaded by id at runtime). Replace with recorded
# actors before a commercial release; keep file names.
param([switch]$Force)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$manifest = Get-Content -Raw -Encoding UTF8 (Join-Path $root 'Assets/_Project/ClinicalCases/Gym/SymptomaticHypotension/V2/Audio/voice-lines.json') | ConvertFrom-Json
$out = Join-Path $root 'Assets/_Project/Resources/Audio/Case01'
New-Item -ItemType Directory -Force $out | Out-Null

Add-Type -AssemblyName System.Runtime.WindowsRuntime
$null = [Windows.Media.SpeechSynthesis.SpeechSynthesizer, Windows.Media.SpeechSynthesis, ContentType = WindowsRuntime]
$null = [Windows.Storage.Streams.DataReader, Windows.Storage.Streams, ContentType = WindowsRuntime]
$asTask = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]
function Await($operation, [Type]$type) { $task = $asTask.MakeGenericMethod($type).Invoke($null, @($operation)); $task.Wait() | Out-Null; $task.Result }

$synth = New-Object Windows.Media.SpeechSynthesis.SpeechSynthesizer
$all = [Windows.Media.SpeechSynthesis.SpeechSynthesizer]::AllVoices
foreach ($line in $manifest.lines) {
    $file = Join-Path $out ($line.id + '.wav')
    if ((Test-Path $file) -and -not $Force) { continue }
    $style = $manifest.voices.($line.speaker)
    $voice = $all | Where-Object { $_.DisplayName -eq $style.voice } | Select-Object -First 1
    if ($null -eq $voice) { throw "Voice not installed: $($style.voice)" }
    $synth.Voice = $voice
    $text = [System.Security.SecurityElement]::Escape($line.text)
    $ssml = "<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='$($voice.Language)'><prosody rate='$($style.rate)' pitch='$($style.pitch)' volume='$($style.volume)'>$text</prosody></speak>"
    $stream = Await ($synth.SynthesizeSsmlToStreamAsync($ssml)) ([Windows.Media.SpeechSynthesis.SpeechSynthesisStream])
    $reader = New-Object Windows.Storage.Streams.DataReader($stream.GetInputStreamAt(0))
    $size = [uint32]$stream.Size
    Await ($reader.LoadAsync($size)) ([uint32]) | Out-Null
    $bytes = New-Object byte[] $size
    $reader.ReadBytes($bytes)
    [System.IO.File]::WriteAllBytes($file, $bytes)
    Write-Output ("{0} ({1} KB)" -f $line.id, [math]::Round($size / 1KB))
}
