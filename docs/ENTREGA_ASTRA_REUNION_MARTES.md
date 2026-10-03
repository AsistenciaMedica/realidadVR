# VITAL VR · Entrega Astra

Entrega local cerrada el **3 de octubre de 2026**: **APK 0.1.1 firmada**, ejecutable Windows, vídeo de respaldo y capturas finales disponibles. La implementación está verificada en **simulación Quest-look en Windows**. No se ha probado un visor físico ni medido FPS de Quest. El contenido clínico conserva su estado **CLIENT_REVIEW**; esta entrega no acredita validación clínica.

Commit local de implementación: `c0e8de3` (`feat: complete VR demo flow, baked environments and Quest rendering budgets`). El repositorio y los artefactos de compilación son entregas distintas: `Builds/`, `TestResults/` y las herramientas locales de `.utmp/` están excluidos de Git.

## Qué mejoró

- Títulos centrados en la persona, preguntas coherentes con su capacidad de respuesta y constantes que se obtienen mediante las interacciones. Se conserva la fuente clínica.
- Pausa y recuperación de foco, recentrado B/Y y navegación con un solo mando; repetición de intentos y resultados independientes.
- Bienvenida dentro del gimnasio, tutorial opcional de 30 segundos y demo Daniel → Andrés. Menús VR de 1,3 m a 1,6 m, texto mayor y panel lateral durante el caso.
- Luz horneada en los tres entornos, materiales y texturas corregidos, cielo/campo revisados, postura y mirada de pacientes. DEA funcional en vitrinas o maletín, con soporte físico comprobado.
- Ambiente espacial, voces sintéticas en español, testigos y mensajes del DEA; respuesta sonora y háptica de la interfaz y del equipo. Ajustes de calidad Alta/Fluida.

Recortes permitidos por el plan: quedan pendientes las nuevas comprobaciones corporales de seguridad/respuesta/respiración, el público de las gradas, sustituir todos los tonos procedurales por clips y la tableta sostenida en la mano. Se entrega el panel lateral reducido; su superficie sigue plana. Las voces añadidas son sintéticas.

## Evidencia final

| Verificación | Resultado | Archivo |
| --- | --- | --- |
| EditMode | **304/304** | [final-editmode.xml](../TestResults/astra/final-editmode.xml) |
| PlayMode completo | **90/90** | [final-playmode.xml](../TestResults/astra/final-playmode.xml) |
| Recorrido XR de los 15 casos | **15/15**, rayo y gatillo reales del simulador | [xr-walkthrough.xml](../TestResults/astra/xr-walkthrough.xml) |
| Procedencia del subconjunto XR | Casos originales preservados y SHA del PlayMode de origen | [xr-walkthrough.provenance.json](../TestResults/astra/xr-walkthrough.provenance.json) |
| Presupuestos de los 15 casos | **15/15** | [budgets.md](../TestResults/astra/budgets.md) |
| Portal y alcance compartido | Sincronización correcta; **4/4** pruebas Node | [portal](../TestResults/astra/postflight-portal-sync.txt), [alcance](../TestResults/astra/postflight-release-scope.txt) |
| Verificación final de evidencia | Resultados originales conservados; **56 comprobaciones de hashes enlazados** | [release-postflight.json](../TestResults/astra/release-postflight.json) |
| APK final | Identidad exacta, firma v2 válida y certificado de lanzamiento coincidente; CRC correcto y ARM64 | [final-release.json](../TestResults/astra/final-release.json), [firma](../TestResults/astra/final-apk-signature.txt), [manifiesto](../TestResults/astra/final-apk-manifest.txt) |
| Windows final | Compilación correcta | [final-windows-builddesktop.log](../TestResults/astra/final-windows-builddesktop.log) |
| Captura final | **PASS**, 15 pacientes, **107 PNG**, salida 0 | [99-despues](../TestResults/astra/99-despues/), [informe](../TestResults/astra/99-despues/patient-roster-smoke.json) |

Presupuestos medidos en el reproductor Windows: máximos **76 batches**, **186 626 triángulos**, **0 luces en tiempo real** y **241,2 MiB de texturas**, incluidos render targets. Cámara normal a **2064 × 2208**, FOV 100°, QuestURP y MSAA 4x. Son presupuestos conservadores de simulación, no cifras de rendimiento del visor.

Los informes de horneado de Windows están en [bake-gym.json](../TestResults/astra/bake-gym.json), [bake-mall.json](../TestResults/astra/bake-mall.json) y [bake-football.json](../TestResults/astra/bake-football.json). Android tiene informes independientes: [gimnasio](../TestResults/astra/bake-android-gym.json), [centro comercial](../TestResults/astra/bake-android-mall.json) y [campo](../TestResults/astra/bake-android-football.json), con GPU confirmado y 2/2/1 lightmaps respectivamente. La identidad de fuentes del reproductor es `0ee7ac4bee32fef826139477dec2de0cd28787b70982828ee688d35298d12419`; cubre código runtime, JSON y configuración, no texturas/modelos/lightmaps ni código Editor. Se complementa con la compilación, horneado y capturas finales tras congelar fuentes.

Al compilar Android, URP 17.3 regeneró dos campos de selección de variantes de shaders en `QuestURP.asset`: `m_PrefilteringModeAdditionalLight` pasó de 3 a 4 y `m_PrefilterXRKeywords` de 1 a 0. La verificación reconstruye en memoria únicamente esos dos valores y exige obtener exactamente el hash validado; cualquier otro cambio falla. No modifica el asset ni los resultados originales. Resolución, MSAA, HDR e iluminación conservan sus valores. El informe registra ambos hashes y la diferencia completa; **10 comprobaciones** del verificador incluyen el rechazo de cambios de resolución, MSAA, iluminación y otros campos. El hash actual tras Android es `8ef33d71eda8b7fc5067f3e079ea58fa17c25b195a405bff369dc17f5da112c1`.

## Archivos para la reunión

- **Windows disponible:** entregar toda la carpeta [Builds/Astra](../Builds/Astra/). Abrir [INICIAR-VITAL-VR.cmd](../Builds/Astra/INICIAR-VITAL-VR.cmd), que activa Quest-look con D3D11. Conservar `VITAL-VR_Data`, `UnityPlayer.dll`, `MonoBleedingEdge` y las demás dependencias; controles en [LEEME-WINDOWS.txt](../Builds/Astra/LEEME-WINDOWS.txt).
- **Respaldo listo para copiar:** [VITAL-VR-0.1.1-Windows-PlanB.zip](../Builds/VITAL-VR-0.1.1-Windows-PlanB.zip), **204,0 MiB**, con Windows, guía, vídeo y galería. Extraerlo y abrir `VITAL-VR-Windows/INICIAR-VITAL-VR.cmd`. CRC correcto en sus 247 archivos; los 52 enlaces HTML locales apuntan a 35 destinos presentes. APK y símbolos de depuración quedan fuera de este ZIP. [Manifiesto del paquete](../TestResults/astra/windows-package.json). SHA-256: `29c21e2f8aaa0f1c76ccbf074e1b3ff3f385dbe5359978ee8408c866fd625f4a`.
- **Guía del cliente disponible:** [GUIA_PRIMER_USO.md](GUIA_PRIMER_USO.md), con instalación, controles, recentrado y demo recomendada.
- **Vídeo y comparación disponibles:** [vídeo](../TestResults/astra/plan-b/vital-vr-plan-b.mp4) y [galería antes/después](../TestResults/astra/plan-b/index.html). Usan la captura final `99-despues` y la referencia `00-antes`; las carpetas intermedias de revisión no son la entrega. Es un montaje editorial silencioso de capturas, no una intervención grabada. El [manifiesto](../TestResults/astra/plan-b/manifest.json) confirma `VERIFIED_180_SECONDS`; FFprobe verifica **180 s / 5400 fotogramas**, H.264 a 1920 × 1080 y 30 FPS. SHA-256 del vídeo: `ee28b6e4e26e6c4ba4cf533d8f132c0b1a48889beb2ed2253cd192e48d7c39ff`. Los 35 enlaces locales de la galería resuelven correctamente; conservar su carpeta completa.
- **APK disponible:** [VITAL-VR-0.1.1-2.apk](../Builds/Astra/VITAL-VR-0.1.1-2.apk), **160,1 MiB**; versión **0.1.1**, código **2**, paquete `com.vitalvr.training`, ARM64. Firma v2 válida y certificado coincidente con el alias de lanzamiento existente. El manifiesto fusionado no incluye cámara, ARCore ni seguimiento ocular. SHA-256: `ce6eb204aabfef01903a9d5432d4ef7813bc63f9c2cf06b9599cbe055c24bc23`. El [informe final](../TestResults/astra/final-release.json) enlaza evidencia y certificado público; las credenciales permanecen fuera del repositorio. **No subida a Meta todavía.**

## Reproducir la entrega local

Desde la raíz de `proyectovr`:

```powershell
python tools/Extract-AstraXRResults.py TestResults/astra/final-playmode.xml --output TestResults/astra/xr-walkthrough.xml
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Test-AstraReleaseEvidence.Tests.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Build-AstraRelease.ps1 -PreflightOnly
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Build-AstraRelease.ps1 -VerifyExistingApk
python tools/Build-AstraPlanB.py --after TestResults/astra/99-despues --validate-only
python tools/Build-AstraPlanB.py --after TestResults/astra/99-despues --ffmpeg .utmp/tools/ffmpeg-9.0.2/ffmpeg.exe --output TestResults/astra/plan-b
```

El generador rechaza una salida ya ocupada; usar otra carpeta para reproducir el vídeo sin reemplazar la entrega. Pillow y FFmpeg/FFprobe locales ya están preparados. `-PreflightOnly` comprueba la evidencia sin leer secretos, firmar ni lanzar Unity. `-VerifyExistingApk` comprueba la APK generada, exporta únicamente el certificado público del alias de lanzamiento y compara su SHA-256 con el firmante de la APK; no compila ni firma otra vez. Sin switches, el script compila y verifica una nueva APK. El extractor filtra resultados existentes: no ejecuta nuevamente los 15 recorridos ni cambia sus resultados.

## Publicaciones pendientes

**Git:** los cambios están guardados en commits locales. La revisión automática de aprobación rechazó el push porque el destino es el repositorio público `https://github.com/AsistenciaMedica/realidadVR` y exige aprobación explícita de ese destino. Esa confirmación está pendiente; no se ha reintentado el push.

**Meta:** faltan la confirmación de la aplicación/canal de prueba de la versión 1 y la confirmación de Juan de que la cuenta del cliente está invitada. No se ha confirmado ninguna subida; la publicación pública permanece sin cambios. Una APK firmada localmente no equivale a una versión disponible en el canal.

Las publicaciones siguen pendientes hasta disponer de confirmación real. Los recortes de alcance están detallados arriba; la entrega local y sus verificaciones ya están cerradas.
