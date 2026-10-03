param()
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$labRoot = 'C:\Users\Juan\vital-lab'
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
$evidence = Join-Path $projectRoot 'TestResults/gym-visual-revision'
New-Item -ItemType Directory -Path $evidence -Force | Out-Null

# Compile and bake only the gym in the isolated project. No test suite, player,
# screenshots, simulator or user-owned Unity process is started/stopped here.
foreach ($relative in @('Assets/_Project/Scripts', 'Assets/_Project/Editor')) {
    & robocopy (Join-Path $projectRoot $relative) (Join-Path $labRoot $relative) /E /R:1 /W:1 /NFL /NDL /NJH /NJS /NP
    if ($LASTEXITCODE -ge 8) { throw ('Source synchronization failed: ' + $relative) }
}
$materials = 'Assets/_Project/Resources/Visual/Materials'
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $projectRoot $materials) -Filter 'Gym*' -File) {
    Copy-Item -LiteralPath $file.FullName -Destination (Join-Path (Join-Path $labRoot $materials) $file.Name)
}
$log = Join-Path $evidence 'compile-and-bake.log'
$arguments = @('-batchmode', '-quit', '-force-d3d11', '-projectPath', ('"' + $labRoot + '"'),
    '-executeMethod', 'EmergencyVR.Editor.Environment.EnvironmentLightingBaker.BakeGymVisualRevision',
    '-logFile', ('"' + $log + '"'))
$process = Start-Process -FilePath $unity -ArgumentList $arguments -WorkingDirectory $labRoot -WindowStyle Hidden -PassThru
$processHandle = $process.Handle
Write-Output ('GYM_PREVIEW_BAKE_PID ' + $process.Id)
$process.WaitForExit()
$process.Refresh()
if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -Pattern 'VITAL_GYM_VISUAL_REVISION_BAKED' -Quiet)) {
    throw ('Gym compile/bake failed. See ' + $log)
}

# The source project must contain the exported prefab AND its lightmap dependencies
# for a normal Meta XR Simulator Play session to see the same baked room.
foreach ($relative in @('Assets/_Project/Resources/BakedEnvironments/gym', 'Assets/_Project/Scenes/BakedEnvironments/gym')) {
    $destination = Join-Path $projectRoot $relative
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    & robocopy (Join-Path $labRoot $relative) $destination /E /R:1 /W:1 /NFL /NDL /NJH /NJS /NP
    if ($LASTEXITCODE -ge 8) { throw ('Gym resource transfer failed: ' + $relative) }
    if (Test-Path -LiteralPath ((Join-Path $labRoot $relative) + '.meta')) {
        Copy-Item -LiteralPath ((Join-Path $labRoot $relative) + '.meta') -Destination ($destination + '.meta')
    }
}
foreach ($relative in @('Assets/_Project/Scenes/BakedEnvironments/gym.unity', 'Assets/_Project/Scenes/BakedEnvironments/gym.unity.meta')) {
    Copy-Item -LiteralPath (Join-Path $labRoot $relative) -Destination (Join-Path $projectRoot $relative)
}
Copy-Item -LiteralPath (Join-Path $labRoot 'TestResults/astra/bake-gym.json') -Destination (Join-Path $evidence 'bake-gym.json')
Write-Output 'GYM_PREVIEW_READY_IN_SOURCE. Compile and GPU bake only; visual testing belongs to the user.'
