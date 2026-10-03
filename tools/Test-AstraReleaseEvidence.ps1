# Read-only release evidence validation. Dot-source this file; no Unity or signing is launched.
function Get-AstraEvidenceRecord([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Missing release evidence: $Path" }
    [pscustomobject]@{ path=[IO.Path]::GetFullPath($Path); bytes=(Get-Item -LiteralPath $Path).Length;
        sha256=(Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
}

function Get-AstraTextSha256([string]$Text) {
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { ([BitConverter]::ToString($algorithm.ComputeHash([Text.Encoding]::UTF8.GetBytes($Text)))).Replace('-', '').ToLowerInvariant() }
    finally { $algorithm.Dispose() }
}

function Get-AstraAndroidShaderPrefilterDifference([string]$RelativePath, [string]$Text, [string]$ExpectedSha256) {
    # URP 17.3 regenerates these serialized shader-selection fields during build:
    # ShaderBuildPreprocessor.cs:309-311, 499-504, 1047-1062 and
    # UniversalRenderPipelineAssetPrefiltering.cs:276-286. Android retains XR and
    # the additional-light OFF variant. These are not runtime lighting settings.
    # Accept only the observed Windows -> Android pair, and prove that EVERY
    # other byte still reproduces the original validation hash. Never edit assets.
    if ($RelativePath -cne 'Assets/_Project/Settings/QuestURP.asset') { return $null }
    $lightPattern = '(?m)^  m_PrefilteringModeAdditionalLight: 4$'
    $xrPattern = '(?m)^  m_PrefilterXRKeywords: 0$'
    if ([regex]::Matches($Text, $lightPattern).Count -ne 1 -or [regex]::Matches($Text, $xrPattern).Count -ne 1) { return $null }
    $validatedText = [regex]::Replace($Text, $lightPattern, '  m_PrefilteringModeAdditionalLight: 3')
    $validatedText = [regex]::Replace($validatedText, $xrPattern, '  m_PrefilterXRKeywords: 1')
    if ((Get-AstraTextSha256 $validatedText) -cne $ExpectedSha256) { return $null }
    [pscustomobject]@{ path=$RelativePath; kind='URP_BUILD_GENERATED_SHADER_PREFILTERS';
        validatedSha256=$ExpectedSha256; currentSha256=(Get-AstraTextSha256 $Text);
        changes=@(
            [pscustomobject]@{ field='m_PrefilteringModeAdditionalLight'; validatedWindows=3; currentAndroid=4 },
            [pscustomobject]@{ field='m_PrefilterXRKeywords'; validatedWindows=1; currentAndroid=0 }) }
}

function Get-AstraXmlCaseKey([System.Xml.XmlNode]$Node) {
    $attributes = [ordered]@{}
    foreach ($attribute in @($Node.Attributes | Sort-Object Name)) { $attributes[$attribute.Name] = $attribute.Value }
    $children = @()
    foreach ($child in $Node.ChildNodes) {
        if ($child.NodeType -eq [System.Xml.XmlNodeType]::Element) {
            $children += Get-AstraXmlCaseKey $child
        } elseif (($child.NodeType -eq [System.Xml.XmlNodeType]::Text -or
                   $child.NodeType -eq [System.Xml.XmlNodeType]::CDATA) -and -not [string]::IsNullOrWhiteSpace($child.Value)) {
            $children += 'text:' + $child.Value
        }
    }
    [pscustomobject]@{ name=$Node.Name; attributes=$attributes; children=$children } | ConvertTo-Json -Depth 80 -Compress
}

function Get-AstraReleaseEvidence([string]$ProjectRoot, [string]$LabRoot, [switch]$AllowAndroidShaderPrefilters) {
    $evidenceRoot = Join-Path $ProjectRoot 'TestResults/astra'
    $xmlDocuments = @{}
    $xmlRecords = @()
    foreach ($name in @('final-editmode.xml', 'final-playmode.xml', 'xr-walkthrough.xml')) {
        $path = Join-Path $evidenceRoot $name
        $record = Get-AstraEvidenceRecord $path
        [xml]$document = Get-Content -LiteralPath $path -Raw -Encoding UTF8
        $run = $document.'test-run'
        $cases = @($document.SelectNodes('//test-case'))
        if ($null -eq $run -or $run.result -ne 'Passed' -or [int]$run.failed -ne 0 -or
            [int]$run.total -le 0 -or $cases.Count -ne [int]$run.total -or
            @($cases | Where-Object { $_.result -ne 'Passed' }).Count -ne 0 -or [int]$run.passed -ne $cases.Count) {
            throw "All actual cases must pass in $name; empty, skipped, failed or incomplete results cannot authorize a release."
        }
        $xmlDocuments[$name] = $document
        $xmlRecords += [pscustomobject]@{ file=$record; passed=$cases.Count; result=[string]$run.result }
    }

    $xrClass = 'EmergencyVR.Tests.QuestXRWalkthroughTests'
    $expectedMethods = @('Daniel_Hypotension', 'Gym_Faint', 'Gym_Glucose', 'Gym_ChestPain', 'Gym_Asthma',
        'Mall_UnconsciousBreathing', 'Mall_AbnormalBreathing', 'Mall_Choking', 'Mall_Confusion', 'Mall_Dehydration',
        'Andres_Arrest', 'Football_Faint', 'Football_Glucose', 'Football_HeatExhaustion', 'Football_Hypoxia')
    $play = $xmlDocuments['final-playmode.xml']
    $xr = $xmlDocuments['xr-walkthrough.xml']
    $originalCases = @($play.SelectNodes('//test-case') | Where-Object { $_.classname -eq $xrClass })
    $extractedCases = @($xr.SelectNodes('//test-case'))
    if ($originalCases.Count -ne 15 -or $extractedCases.Count -ne 15) { throw 'Exactly fifteen original and extracted XR cases are required.' }
    foreach ($method in $expectedMethods) {
        $fullName = $xrClass + '.' + $method
        $original = @($originalCases | Where-Object { $_.fullname -eq $fullName -and $_.methodname -eq $method })
        $extracted = @($extractedCases | Where-Object { $_.classname -eq $xrClass -and $_.fullname -eq $fullName -and $_.methodname -eq $method })
        if ($original.Count -ne 1 -or $extracted.Count -ne 1 -or
            (Get-AstraXmlCaseKey $original[0]) -cne (Get-AstraXmlCaseKey $extracted[0])) {
            throw "XR extraction differs from the actual final PlayMode result: $method"
        }
    }
    $playRecord = ($xmlRecords | Where-Object { $_.file.path -eq (Join-Path $evidenceRoot 'final-playmode.xml') }).file
    $xrRecord = ($xmlRecords | Where-Object { $_.file.path -eq (Join-Path $evidenceRoot 'xr-walkthrough.xml') }).file
    $provenanceRecord = Get-AstraEvidenceRecord (Join-Path $evidenceRoot 'xr-walkthrough.provenance.json')
    $provenance = Get-Content -LiteralPath $provenanceRecord.path -Raw -Encoding UTF8 | ConvertFrom-Json
    $originProperty = @($xr.SelectNodes('/test-run/properties/property') | Where-Object { $_.name -eq 'AstraSourceSha256' })
    if ($provenance.kind -ne 'EXTRACTED_ORIGINAL_RESULTS' -or $provenance.newTestExecution -ne $false -or
        $provenance.source.sha256 -cne $playRecord.sha256 -or $provenance.output.sha256 -cne $xrRecord.sha256 -or
        $provenance.testCaseElementsUnchanged -ne $true -or $originProperty.Count -ne 1 -or
        $originProperty[0].value -cne $playRecord.sha256) {
        throw 'XR provenance does not identify the current final-playmode.xml and extracted XML.'
    }

    $validation = Get-AstraBudgetEvidence -ProjectRoot $ProjectRoot -LabRoot $LabRoot -AllowAndroidShaderPrefilters:$AllowAndroidShaderPrefilters
    $validation.status = 'EVIDENCE_VERIFIED'
    $validation | Add-Member -NotePropertyName xml -NotePropertyValue $xmlRecords
    $validation | Add-Member -NotePropertyName xrProvenance -NotePropertyValue $provenanceRecord
    return $validation
}

function Get-AstraBudgetEvidence([string]$ProjectRoot, [string]$LabRoot, [switch]$AllowAndroidShaderPrefilters) {
    $evidenceRoot = Join-Path $ProjectRoot 'TestResults/astra'
    $player = Join-Path $LabRoot 'Builds/AstraValidation/VITAL-VR.exe'
    $playerRecord = Get-AstraEvidenceRecord $player
    $sidecarRecord = Get-AstraEvidenceRecord ($player + '.fingerprint.json')
    $built = Get-Content -LiteralPath $sidecarRecord.path -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($built.schemaVersion -ne 1 -or $built.developmentBuild -ne $true -or
        $built.sourceSha256 -cnotmatch '^[a-f0-9]{64}$' -or @($built.files).Count -eq 0) {
        throw 'The laboratory validation player needs a valid development-build source fingerprint.'
    }
    # Recheck the recorded text inputs against the lab. Binary assets/lightmaps are outside
    # this runtime fingerprint; the source freeze and final build/bake evidence remain required.
    $labPrefix = [IO.Path]::GetFullPath($LabRoot).TrimEnd('\') + '\'
    $canonical = New-Object Text.StringBuilder
    [void]$canonical.Append("VITAL_SOURCE_FINGERPRINT_V1`n")
    $currentCanonical = New-Object Text.StringBuilder
    [void]$currentCanonical.Append("VITAL_SOURCE_FINGERPRINT_V1`n")
    $generatedDifferences = @()
    $seenPaths = @{}
    foreach ($entry in $built.files) {
        $relative = [string]$entry.path
        $path = [IO.Path]::GetFullPath((Join-Path $LabRoot $relative))
        if (-not $path.StartsWith($labPrefix, [StringComparison]::OrdinalIgnoreCase) -or
            $seenPaths.ContainsKey($relative) -or -not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw 'A validation fingerprint source path is missing, duplicated or outside the laboratory.'
        }
        $seenPaths[$relative] = $true
        $normalized = [IO.File]::ReadAllText($path).Replace("`r`n", "`n").Replace("`r", "`n")
        $currentHash = Get-AstraTextSha256 $normalized
        if ($currentHash -cne $entry.sha256) {
            $difference = $null
            if ($AllowAndroidShaderPrefilters) {
                $difference = Get-AstraAndroidShaderPrefilterDifference -RelativePath $relative -Text $normalized -ExpectedSha256 $entry.sha256
            }
            if ($null -eq $difference) { throw "Laboratory source changed after validation build: $relative" }
            $generatedDifferences += $difference
        }
        [void]$canonical.Append($relative).Append([char]0).Append([string]$entry.sha256).Append("`n")
        [void]$currentCanonical.Append($relative).Append([char]0).Append($currentHash).Append("`n")
    }
    if ((Get-AstraTextSha256 $canonical.ToString()) -cne $built.sourceSha256) { throw 'Validation fingerprint entries do not reproduce its source hash.' }
    $currentPaths = @()
    foreach ($specification in @(
        @('Assets/_Project/Scripts', '*.cs'), @('Assets/_Project/Resources', '*.json'), @('Assets/_Project/Settings', 'Quest*.asset'))) {
        $directory = Join-Path $LabRoot $specification[0]
        if (Test-Path -LiteralPath $directory -PathType Container) {
            $currentPaths += @(Get-ChildItem -LiteralPath $directory -Filter $specification[1] -File -Recurse |
                ForEach-Object { $_.FullName.Substring($labPrefix.Length).Replace('\', '/') })
        }
    }
    $currentPaths = @($currentPaths | Where-Object { $_ -cne 'Assets/_Project/Resources/Validation/BuildFingerprint.json' })
    foreach ($relative in @('ProjectSettings/QualitySettings.asset', 'ProjectSettings/GraphicsSettings.asset',
        'ProjectSettings/ProjectVersion.txt', 'Packages/manifest.json', 'Packages/packages-lock.json')) {
        if (Test-Path -LiteralPath (Join-Path $LabRoot $relative) -PathType Leaf) { $currentPaths += $relative }
    }
    if (@(Compare-Object @($seenPaths.Keys) $currentPaths).Count -ne 0) {
        throw 'The laboratory source file set changed after validation build. Rebuild before signing.'
    }

    $scopeRecord = Get-AstraEvidenceRecord (Join-Path $ProjectRoot 'Assets/_Project/Resources/ReleaseScope.json')
    $scope = Get-Content -LiteralPath $scopeRecord.path -Raw -Encoding UTF8 | ConvertFrom-Json
    $expectedIds = @($scope.environments | ForEach-Object { $_.scenarioIds })
    if ($expectedIds.Count -ne 15 -or @($expectedIds | Sort-Object -Unique).Count -ne 15) { throw 'ReleaseScope must identify fifteen distinct cases.' }
    $summaryRecord = Get-AstraEvidenceRecord (Join-Path $evidenceRoot 'budget-result.json')
    $summary = Get-Content -LiteralPath $summaryRecord.path -Raw -Encoding UTF8 | ConvertFrom-Json
    $summaryIds = @($summary.samples | ForEach-Object { $_.scenario })
    if ($summary.result -ne 'PASS' -or $summary.sourceSha256 -cne $built.sourceSha256 -or
        [string]::IsNullOrWhiteSpace($summary.runId) -or -not [string]::IsNullOrEmpty($summary.failure) -or
        @($summary.runtimeErrors).Count -ne 0 -or $summaryIds.Count -ne 15 -or
        @(Compare-Object $expectedIds $summaryIds).Count -ne 0) {
        throw 'The budget summary must pass all fifteen release cases from the current laboratory player in one run.'
    }
    $budgetFiles = @(Get-ChildItem -LiteralPath $evidenceRoot -Filter 'budget-*.json' -File | Where-Object { $_.Name -ne 'budget-result.json' })
    if ($budgetFiles.Count -ne 15) { throw 'Exactly fifteen per-case budget files are required; remove no evidence automatically.' }
    $budgetRecords = @()
    foreach ($id in $expectedIds) {
        $record = Get-AstraEvidenceRecord (Join-Path $evidenceRoot ('budget-' + $id + '.json'))
        $sample = Get-Content -LiteralPath $record.path -Raw -Encoding UTF8 | ConvertFrom-Json
        $summarySample = @($summary.samples | Where-Object { $_.scenario -ceq $id })
        if ($sample.scenario -cne $id -or $sample.passed -ne $true -or $sample.runId -cne $summary.runId -or
            $sample.sourceSha256 -cne $built.sourceSha256 -or $summarySample.Count -ne 1 -or
            ($sample | ConvertTo-Json -Depth 80 -Compress) -cne ($summarySample[0] | ConvertTo-Json -Depth 80 -Compress)) {
            throw "Budget evidence is stale, mixed or differs from its summary: $id"
        }
        $budgetRecords += [pscustomobject]@{ file=$record; scenario=$id; runId=$sample.runId; sourceSha256=$sample.sourceSha256 }
    }
    $budgetMarkdown = Get-AstraEvidenceRecord (Join-Path $evidenceRoot 'budgets.md')
    $budgetText = Get-Content -LiteralPath $budgetMarkdown.path -Raw -Encoding UTF8
    if (-not $budgetText.Contains($built.sourceSha256) -or -not $budgetText.Contains('Result: **PASS**')) {
        throw 'The canonical budgets.md does not report the current passing source fingerprint.'
    }
    [pscustomobject]@{ schemaVersion=1; checkedUtc=[DateTime]::UtcNow.ToString('O'); status='BUDGET_EVIDENCE_VERIFIED';
        headsetTested=$false; uploaded=$false;
        validationPlayer=$playerRecord; validationFingerprint=$sidecarRecord; sourceSha256=$built.sourceSha256;
        currentSourceSha256=(Get-AstraTextSha256 $currentCanonical.ToString()); buildGeneratedDifferences=$generatedDifferences;
        fingerprintScope='Runtime C# and JSON, Quest settings and package/quality configuration; excludes binary assets, lightmaps and Editor code.';
        releaseScope=$scopeRecord; budgetRunId=$summary.runId; budgetSummary=$summaryRecord;
        budgetReport=$budgetMarkdown; budgets=$budgetRecords }
}
