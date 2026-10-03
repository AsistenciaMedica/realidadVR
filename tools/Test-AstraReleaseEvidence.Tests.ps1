$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Test-AstraReleaseEvidence.ps1')

# The exception may accept generated shader selection only. It must never hide
# different runtime quality, another serialized field, or another source file.
$assetPath = Join-Path $PSScriptRoot '../Assets/_Project/Settings/QuestURP.asset'
$android = [IO.File]::ReadAllText($assetPath).Replace("`r`n", "`n").Replace("`r", "`n")
$windows = $android.Replace('  m_PrefilteringModeAdditionalLight: 4', '  m_PrefilteringModeAdditionalLight: 3').Replace('  m_PrefilterXRKeywords: 0', '  m_PrefilterXRKeywords: 1')
$expected = Get-AstraTextSha256 $windows
$relative = 'Assets/_Project/Settings/QuestURP.asset'
$difference = Get-AstraAndroidShaderPrefilterDifference $relative $android $expected
if ($null -eq $difference -or $difference.changes.Count -ne 2 -or
    $difference.currentSha256 -cne (Get-AstraTextSha256 $android)) {
    throw 'The exact platform-generated pair must be accepted and recorded.'
}

$invalid = @(
    @{ name='runtime resolution'; path=$relative; text=$android.Replace('  m_RenderScale: 1', '  m_RenderScale: 0.85'); hash=$expected },
    @{ name='runtime MSAA'; path=$relative; text=$android.Replace('  m_MSAA: 4', '  m_MSAA: 2'); hash=$expected },
    @{ name='runtime lighting'; path=$relative; text=$android.Replace('  m_ShadowDistance: 0', '  m_ShadowDistance: 20'); hash=$expected },
    @{ name='another generated field'; path=$relative; text=$android.Replace('  m_PrefilterDebugKeywords: 1', '  m_PrefilterDebugKeywords: 0'); hash=$expected },
    @{ name='unexpected enum value'; path=$relative; text=$android.Replace('  m_PrefilteringModeAdditionalLight: 4', '  m_PrefilteringModeAdditionalLight: 5'); hash=$expected },
    @{ name='duplicate field'; path=$relative; text=($android + "  m_PrefilterXRKeywords: 0`n"); hash=$expected },
    @{ name='partial pair'; path=$relative; text=$android.Replace('  m_PrefilterXRKeywords: 0', '  m_PrefilterXRKeywords: 1'); hash=$expected },
    @{ name='wrong file'; path='Assets/_Project/Scripts/Clinical.cs'; text=$android; hash=$expected },
    @{ name='unrelated validation hash'; path=$relative; text=$android; hash=('0' * 64) }
)
foreach ($case in $invalid) {
    if ($null -ne (Get-AstraAndroidShaderPrefilterDifference $case.path $case.text $case.hash)) {
        throw ('Unsafe fingerprint exception accepted: ' + $case.name)
    }
}
Write-Output ('ASTRA_EVIDENCE_GUARD_TESTS_PASS: ' + (1 + $invalid.Count) + ' checks; no files modified.')
