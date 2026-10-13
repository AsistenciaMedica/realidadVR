param([ValidateRange(2,2147483647)][int]$VersionCode = 3)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$delivery = Join-Path $root 'Builds/Reunion-20261006'
$playerRoot = Join-Path $root 'Builds/Astra'
$evidence = Join-Path $root 'TestResults/astra'
$release = Get-Content -LiteralPath (Join-Path $evidence 'final-release.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$player = Get-Content -LiteralPath (Join-Path $playerRoot 'VITAL-VR.exe.fingerprint.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ($release.versionCode -ne $VersionCode -or $release.validation.status -ne 'EVIDENCE_VERIFIED' -or
    $player.developmentBuild -or $player.sourceSha256 -ne $release.validation.sourceSha256) {
    throw 'The signed APK, validated source and production Windows player must belong to the same revision.'
}
$apk = Join-Path $playerRoot ('VITAL-VR-0.1.1-' + $VersionCode + '.apk')
if ((Get-FileHash -LiteralPath $apk -Algorithm SHA256).Hash -ne $release.sha256) { throw 'APK differs from its verified release evidence.' }
if (-not (Test-Path -LiteralPath (Join-Path $delivery 'Revision/ABRIR-REVISION.html'))) { throw 'Generate the visual review first.' }
foreach ($folder in @('Windows','Android')) { New-Item -ItemType Directory -Path (Join-Path $delivery $folder) -Force | Out-Null }
foreach ($name in @('VITAL-VR.exe','UnityPlayer.dll','UnityCrashHandler64.exe','INICIAR-VITAL-VR.cmd','LEEME-WINDOWS.txt','VITAL-VR.exe.fingerprint.json')) {
    Copy-Item -LiteralPath (Join-Path $playerRoot $name) -Destination (Join-Path $delivery 'Windows')
}
foreach ($name in @('VITAL-VR_Data','MonoBleedingEdge','D3D12')) {
    if (Test-Path -LiteralPath (Join-Path $playerRoot $name)) {
        robocopy (Join-Path $playerRoot $name) (Join-Path $delivery ('Windows/' + $name)) /E /R:1 /W:1 /NFL /NDL /NJH /NJS /NP
        if ($LASTEXITCODE -ge 8) { throw ('Windows runtime copy failed: ' + $name) }
    }
}
Copy-Item -LiteralPath $apk -Destination (Join-Path $delivery 'Android')
$notes = @'
VITAL VR - revision para la reunion del 6 de octubre de 2026

1. Para presentar el avance sin instalar nada:
   Abre Revision/ABRIR-REVISION.html. Funciona sin internet.
   Puedes filtrar los tres entornos y revisar los quince pacientes.

2. Para probar en Windows:
   Abre Windows/INICIAR-VITAL-VR.cmd. Conserva la carpeta completa.
   Raton: apuntar y seleccionar. Boton derecho: mirar. G: agarrar equipo.
   B o Y: recentrar y pausar. Empieza con el tutorial de controles.

3. Para actualizar el canal de prueba de Meta:
   La APK nueva esta en Android. Se sube por el panel que ya utilizas.
   Este paquete no confirma una subida ni una instalacion en el visor.
   Verifica en ese panel que su codigo de version supera el instalado.

Recorrido sugerido: Daniel en el gimnasio, un caso del centro comercial
y Andres en el campo. Finaliza cada intento antes de elegir el siguiente.

Avances: escaparates con profundidad, materiales, detalles del campo,
iluminacion sobre los personajes, expresiones y boca al hablar,
sombras de contacto y mezcla del ambiente durante las conversaciones.
La landing usa capturas de la aplicacion y se ha revisado en movil.

Limites: se han mejorado los modelos existentes; no son nuevos humanos
escaneados. Las pruebas son en Windows con simulacion XR. Falta la
comprobacion de apariencia, fluidez y controles en unas Meta Quest reales.
El contenido clinico sigue en revision. Las voces son sinteticas.
'@
[IO.File]::WriteAllText((Join-Path $delivery 'LEEME-REUNION.txt'), $notes, [Text.Encoding]::UTF8)
$manifest = [ordered]@{ version='0.1.1'; versionCode=$VersionCode; package='com.vitalvr.training';
    sourceSha256=$player.sourceSha256; apkSha256=$release.sha256; headsetTested=$false; uploaded=$false;
    windowsExecutableSha256=(Get-FileHash -LiteralPath (Join-Path $delivery 'Windows/VITAL-VR.exe') -Algorithm SHA256).Hash;
    packagedUtc=[DateTime]::UtcNow.ToString('O'); cases=15; environments=3 }
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $delivery 'entrega.json') -Encoding UTF8
$zip = Join-Path $root ('Builds/VITAL-VR-Reunion-20261006-v' + $VersionCode + '.zip')
Compress-Archive -LiteralPath $delivery -DestinationPath $zip -CompressionLevel Fastest -Force
Write-Output ('MEETING_DELIVERY_READY ' + $zip)
Get-Item -LiteralPath $zip | Select-Object FullName,Length
