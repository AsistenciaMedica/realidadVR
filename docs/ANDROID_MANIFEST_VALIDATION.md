# Manifiesto Android y foveación fija

La política de VITAL VR está definida en `QuestQualityControl`: foveación SRP de
nivel 1 y `FoveationFlags = 0`. No usa datos de mirada. Se mantienen los dispositivos
seleccionados en Meta Quest Support, incluido Quest Pro.

OpenXR 1.16 agrupa las extensiones de foveación fija y ocular en
`FoveatedRenderingFeature`. Al seleccionar Quest Pro, su generador añade una
feature ocular obligatoria y un permiso aunque la aplicación utilice sólo foveación
fija. `QuestFixedFoveationManifest` corrige el manifiesto generado mediante
`IPostGenerateGradleAndroidProject`, después del generador XR. Añade reglas de
fusión `tools:node="remove"` en el manifiesto de `launcher` para:

- `uses-feature`: `oculus.software.eye_tracking`.
- `uses-permission`: `com.oculus.permission.EYE_TRACKING`.
- `uses-permission`: `android.permission.EYE_TRACKING_FINE`.

La corrección sólo se activa con `FoveatedRenderingFeature` habilitada, API
SRPFoveation, nivel positivo y política sin `GazeAllowed`. Es idempotente y no
modifica `com.oculus.supportedDevices`, el seguimiento de cabeza ni la actividad de
arranque. No se modifica `PackageCache`. Si se adopta seguimiento ocular, deben
revisarse la política, los permisos y la validación.

El paquete todavía intenta solicitar el permiso ocular en su inicialización; sin
declaración en el manifiesto no puede obtenerlo. Según la documentación de OpenXR,
la foveación fija funciona cuando no se habilita `GazeAllowed` o se deniega ese
permiso. Esto no sustituye una prueba en un visor.

## Validación de la APK terminada

Las pruebas EditMode comprueban los marcadores y que la lista de dispositivos se
conserve. Antes de distribuir hay que inspeccionar **la APK final**, porque Gradle
fusiona los manifiestos después de ejecutar el hook.

Ejemplo PowerShell con el SDK incluido en el Editor instalado; sustituir
`RUTA_APK_GENERADA` por la salida real del build:

```powershell
$aapt2 = 'C:/Program Files/Unity/Hub/Editor/6000.3.23f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/build-tools/36.0.0/aapt2.exe'
$apk = 'RUTA_APK_GENERADA'
$manifest = & $aapt2 dump xmltree --file AndroidManifest.xml $apk
if ($LASTEXITCODE -ne 0) { throw 'No se pudo inspeccionar el manifiesto de la APK.' }
New-Item -ItemType Directory -Force 'TestResults/astra' | Out-Null
$manifest | Set-Content -Encoding UTF8 'TestResults/astra/android-manifest.txt'
$forbidden = 'oculus\.software\.eye_tracking|com\.oculus\.permission\.EYE_TRACKING|android\.permission\.EYE_TRACKING_FINE|android\.permission\.CAMERA|com\.google\.ar\.core'
if ($manifest -match $forbidden) { throw 'La APK declara seguimiento ocular, cámara o ARCore no utilizados.' }
$manifest | Select-String 'com.oculus.supportedDevices|android.hardware.vr.headtracking|versionCode|package|debuggable' -Context 0,2
```

Comprobar que `com.oculus.supportedDevices` coincide con la selección de Meta Quest
Support, que `android.hardware.vr.headtracking` sigue siendo obligatorio y que la
versión, identificador y condición de release son los esperados. Archivar el dump
junto con el hash SHA-256 de esa APK. Un dump pendiente no cuenta como validación
aprobada. El seguimiento, los permisos visibles y el rendimiento en Quest siguen
requiriendo el visor.

Fuentes: [hook oficial de Unity 6.3](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Android.IPostGenerateGradleAndroidProject.OnPostGenerateGradleAndroidProject.html),
[foveación en OpenXR 1.16](https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.16/manual/features/foveatedrendering.html),
[reglas de fusión de Android](https://developer.android.com/build/manage-manifests#node-markers).
`AndroidProjectFilesModifier` no permite modificar los módulos por defecto
`launcher` y `unityLibrary` en esta versión: [API Unity 6.3](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Android.AndroidProjectFiles.html).
