param(
    [ValidateSet('Sync','SyncSources','SyncFiles','EditMode','PlayMode','BuildDesktop','BuildBudget','Capture','CaptureBudget','Bake','ValidateGym')][string]$Task = 'EditMode',
    [string]$Label = 'current',
    [string]$TestFilter = '',
    [string]$FileList = '',
    [string]$FileManifest = '',
    [ValidateSet('Default','D3D11')][string]$GraphicsApi = 'Default'
)
$ErrorActionPreference = 'Stop'
$sourceRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$labRoot = 'C:\Users\Juan\vital-lab'
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
if ($Label -notmatch '^[a-zA-Z0-9_-]+$') { throw 'Label must contain only letters, numbers, underscores or hyphens.' }
$evidence = Join-Path $sourceRoot 'TestResults\astra'
$output = Join-Path $sourceRoot 'Builds\Astra\VITAL-VR.exe'
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
if ($Task -eq 'SyncFiles') {
    $files = @($FileList.Split(',',[StringSplitOptions]::RemoveEmptyEntries))
    if ($FileManifest) {
        $manifestPath = [IO.Path]::GetFullPath((Join-Path $sourceRoot $FileManifest))
        if (-not $manifestPath.StartsWith($sourceRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
            throw 'The synchronization manifest must stay within the source project.'
        }
        $manifestFiles = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach ($manifestFile in $manifestFiles) { $files += [string]$manifestFile }
    }
    foreach ($relative in $files) {
        $sourceFile = [IO.Path]::GetFullPath((Join-Path $sourceRoot $relative))
        $labFile = [IO.Path]::GetFullPath((Join-Path $labRoot $relative))
        if (-not $sourceFile.StartsWith($sourceRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or
            -not $labFile.StartsWith($labRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'File must stay within the source and lab project.' }
        New-Item -ItemType Directory -Path (Split-Path $labFile) -Force | Out-Null
        Copy-Item -LiteralPath $sourceFile -Destination $labFile
    }
    Write-Output 'ASTRA_LAB_FIXES_SYNC_OK'
    exit 0
}
if ($Task -eq 'Sync' -or $Task -eq 'SyncSources') {
    # Keep the lab's generated meshes, materials, lightmaps and platform import settings
    # when iterating on code/audio. A full initial Sync still copies the authored project.
    $folders = if ($Task -eq 'Sync') { @('Assets','Packages','ProjectSettings') } else {
        @('Assets/_Project/Scripts','Assets/_Project/Editor','Assets/_Project/Tests',
          'Assets/_Project/Resources/Audio','Assets/StreamingAssets/ThirdParty')
    }
    foreach ($folder in $folders) {
        robocopy (Join-Path $sourceRoot $folder) (Join-Path $labRoot $folder) /E /R:1 /W:1 /NFL /NDL /NJH /NJS /NP
        if ($LASTEXITCODE -ge 8) { throw "Failed to sync $folder" }
    }
    if ($Task -eq 'SyncSources') {
        foreach ($name in @('RosterVoices.json','AEDVoices.json')) {
            foreach ($suffix in @('', '.meta')) {
                $relative = 'Assets/_Project/Resources/' + $name + $suffix
                if (Test-Path -LiteralPath (Join-Path $sourceRoot $relative)) {
                    Copy-Item -LiteralPath (Join-Path $sourceRoot $relative) -Destination (Join-Path $labRoot $relative)
                }
            }
        }
    }
    # Reconcile only explicitly retired AR assets; never mirror-delete the lab or its cache.
    foreach ($relative in @('Assets/XR/Loaders/ARCoreLoader.asset','Assets/XR/Resources/ARCoreRuntimeSettings.asset',
        'Assets/XR/Settings/ARCoreSettings.asset','Assets/XR/Settings/XRSimulationSettings.asset')) {
        foreach ($suffix in @('', '.meta')) {
            $labFile = Join-Path $labRoot ($relative + $suffix)
            if (-not (Test-Path -LiteralPath (Join-Path $sourceRoot ($relative + $suffix))) -and (Test-Path -LiteralPath $labFile)) {
                Remove-Item -LiteralPath $labFile
            }
        }
    }
    Write-Output 'ASTRA_LAB_SYNC_OK'
    exit 0
}
$log = Join-Path $evidence ($Label + '-' + $Task.ToLowerInvariant() + '.log')
if ($Task -eq 'Capture' -or $Task -eq 'CaptureBudget') {
    if ($Task -eq 'CaptureBudget') { $output = Join-Path $labRoot 'Builds/AstraValidation/VITAL-VR.exe' }
    if (-not (Test-Path -LiteralPath $output)) { throw 'BuildDesktop must succeed first.' }
    $capture = Join-Path $evidence $Label
    if (Test-Path -LiteralPath (Join-Path $capture 'result.txt')) { throw 'Evidence already exists. Choose a new label to preserve the previous capture.' }
    New-Item -ItemType Directory -Path $capture -Force | Out-Null
    $argsList = @('-vital-quest-look','-vital-patient-roster-smoke','-vital-capture-directory',('"'+$capture+'"'),
        '-screen-width','2064','-screen-height','2208','-screen-fullscreen','0','-logFile',('"'+$log+'"'))
    if ($GraphicsApi -eq 'D3D11') { $argsList += '-force-d3d11' }
    $process = Start-Process -FilePath $output -ArgumentList $argsList -WorkingDirectory $sourceRoot -WindowStyle Hidden -PassThru
    $processHandle = $process.Handle
    $process.WaitForExit()
    $process.Refresh()
    Write-Output ("Capture exit code: " + $process.ExitCode)
    if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -Pattern 'VITAL_PATIENT_ROSTER_SMOKE PASS' -Quiet)) {
        throw "Quest-look capture failed: $log"
    }
} else {
    $argsList = @('-batchmode','-projectPath',('"'+$labRoot+'"'),'-buildTarget','Win64','-logFile',('"'+$log+'"'))
    if ($Task -eq 'ValidateGym') {
        $argsList += @('-quit','-executeMethod','EmergencyVR.Editor.QuestGymTextureSetup.ReimportAndValidate')
    } elseif ($Task -eq 'Bake') {
        $argsList += @('-quit','-executeMethod','EmergencyVR.Editor.Environment.EnvironmentLightingBaker.BakeAll')
    } elseif ($Task -eq 'BuildDesktop' -or $Task -eq 'BuildBudget') {
        if ($Task -eq 'BuildBudget') { $output = Join-Path $labRoot 'Builds/AstraValidation/VITAL-VR.exe' }
        New-Item -ItemType Directory -Path (Split-Path $output) -Force | Out-Null
        $argsList += @('-quit','-executeMethod','EmergencyVR.Editor.Case01Build.BuildDesktop','-case01-output',('"'+$output+'"'))
        if ($Task -eq 'BuildBudget') { $argsList += '-vital-validation-build' }
    } else {
        $xml = Join-Path $evidence ($Label + '-' + $Task.ToLowerInvariant() + '.xml')
        $argsList += @('-runTests','-testPlatform',$Task,'-testResults',('"'+$xml+'"'))
        if ($TestFilter) { $argsList += @('-testFilter',$TestFilter) }
    }
    $process = Start-Process -FilePath $unity -ArgumentList $argsList -WorkingDirectory $labRoot -WindowStyle Hidden -PassThru
    Write-Output ("Unity PID: " + $process.Id)
    $started = $process.StartTime
    $process.WaitForExit()
    if ($Task -eq 'PlayMode') {
        $budgetReport = Join-Path $labRoot 'TestResults/astra/budgets.md'
        if ((Test-Path -LiteralPath $budgetReport) -and (Get-Item -LiteralPath $budgetReport).LastWriteTime -ge $started) {
            Copy-Item -LiteralPath $budgetReport -Destination (Join-Path $evidence ($Label + '-budgets.md'))
            Copy-Item -LiteralPath $budgetReport -Destination (Join-Path $evidence 'budgets.md')
            Copy-Item -Path (Join-Path $labRoot 'TestResults/astra/budget-*.json') -Destination $evidence
        }
    }
    if ($process.ExitCode -ne 0) { throw "Unity $Task failed with exit $($process.ExitCode): $log" }
    if ($Task -eq 'Bake') {
        if (-not (Select-String -LiteralPath $log -Pattern 'VITAL_ENVIRONMENTS_BAKED' -Quiet)) { throw "Bake success marker absent: $log" }
        Copy-Item -Path (Join-Path $labRoot 'TestResults/astra/bake-*.json') -Destination $evidence
    } elseif ($Task -eq 'BuildDesktop' -or $Task -eq 'BuildBudget') {
        if (-not (Select-String -LiteralPath $log -Pattern 'CASE01_DESKTOP_BUILT' -Quiet)) { throw "Build success marker absent: $log" }
        if ($Task -eq 'BuildDesktop') {
            # Publish these only after a successful final Windows build; do not relabel an older player.
            $delivery = Split-Path $output
            if (-not (Test-Path -LiteralPath (Join-Path $delivery 'VITAL-VR_Data')) -or
                -not (Test-Path -LiteralPath (Join-Path $delivery 'UnityPlayer.dll'))) {
                throw 'The Windows player is incomplete: VITAL-VR_Data and UnityPlayer.dll must accompany the executable.'
            }
            @'
@echo off
setlocal
cd /d "%~dp0"
if not exist "%~dp0VITAL-VR.exe" (
    echo Falta VITAL-VR.exe. Extrae y conserva la carpeta completa del respaldo.
    pause
    exit /b 1
)
"%~dp0VITAL-VR.exe" -vital-quest-look -force-d3d11 -screen-fullscreen 0
endlocal
'@ | Set-Content -LiteralPath (Join-Path $delivery 'INICIAR-VITAL-VR.cmd') -Encoding ASCII
            @'
VITAL VR - respaldo de Windows

Extrae y conserva la carpeta completa. Abre INICIAR-VITAL-VR.cmd.
No muevas solo VITAL-VR.exe: necesita VITAL-VR_Data, UnityPlayer.dll,
MonoBleedingEdge y las demas carpetas/DLLs del reproductor.

Controles de la simulacion:
- Raton: apuntar; clic izquierdo: seleccionar con el gatillo virtual.
- Mantener clic derecho y mover el raton: mirar alrededor.
- G: mantener agarre; soltar G: soltar equipo.
- B / Y: recentrar el menu y abrir la pausa durante el caso.
- W/A/S/D: stick izquierdo virtual; Q/E: giro por pasos.
- Mayus izquierda: controlar la mano izquierda mientras se mantiene.

Usa el tutorial de controles o la Demo recomendada para comenzar.
Esta es una simulacion Quest-look en Windows, sin visor y sin medicion
de rendimiento de Quest. El lanzamiento usa Direct3D 11.

Si has recibido tambien el video de respaldo, dura tres minutos y es
un montaje de capturas con texto, sin audio ni procedimientos grabados.
La guia del cliente no confirma que una APK este subida a Meta.
'@ | Set-Content -LiteralPath (Join-Path $delivery 'LEEME-WINDOWS.txt') -Encoding UTF8
        }
    } elseif ($Task -eq 'ValidateGym') {
        if (-not (Select-String -LiteralPath $log -Pattern 'VITAL_GYM_TEXTURE_BINDINGS_VERIFIED' -Quiet)) {
            throw "Gym texture validation marker absent: $log"
        }
    } else {
        if (-not (Test-Path -LiteralPath $xml)) { throw "Test results absent: $xml" }
        [xml]$result = Get-Content -LiteralPath $xml -Raw
        if ($result.'test-run'.result -ne 'Passed') { throw "Tests did not pass: $xml" }
        Write-Output ("Tests: " + $result.'test-run'.passed + ' passed, ' + $result.'test-run'.failed + ' failed.')
    }
}
Write-Output ("ASTRA_$Task PASS. Evidence: $log")
