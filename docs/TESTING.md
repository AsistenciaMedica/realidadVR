# Pruebas y criterio de aceptación

## Estado de integración visual — 2026-09-13

| Suite | Resultado | Evidencia |
| --- | --- | --- |
| Core | 27/27 | `TestResults/core-tests.txt` |
| Unity EditMode | 121/121 | `TestResults/visual-editmode.xml` |
| Unity PlayMode | 4/4 | `TestResults/visual-playmode.xml` |
| Web/API | 16/16 | `TestResults/visual-web-tests.txt` |
| Build frontend/backend web | Correcto | `npm --prefix demo run build` |
| Build Windows | Correcto | `Builds/Windows/20260913-100825/EmergencyVR.exe`; `TestResults/visual-windows-build.log` |
| Smoke ejecutable Windows | PASS, proceso finalizado | `TestResults/visual-desktop-player.log`: `DESKTOP_SMOKE PASS` |
| Capturas player | 7 PNG | [Galería](screenshots/README.md) |

PlayMode cubre la demo original, los cuatro entornos, RCP/DEA con contacto y
exportación, y glucómetro con lectura, reagarre, gravedad, avance de 600 segundos
simulados y reinicio. No sustituye una sesión manual prolongada con teclado o
mandos. El smoke Windows recorre 45 secuencias de referencia (una técnica y
44 médicas), componentes RCP/DEA, guardas de contacto y exportación JSON.
No hay aceptación física Windows/Quest declarada.
Biblioteca: exactamente **44 variantes CLIENT_REVIEW**, no 44 protocolos aprobados.

Estado por sistema y límites: [VISUAL_INTERACTION_STATUS.md](VISUAL_INTERACTION_STATUS.md).

## Nivel 1: dominio C# sin Unity

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Test-Core.ps1
```

Requiere el compilador .NET Framework de Windows, ya detectado en este equipo.
No descarga NUnit ni instala SDKs. Compila los archivos reales de Core y ejecuta
`DomainTestCases`, exactamente los mismos 27 casos que expone NUnit en EditMode.
Salida local: `TestResults/core-tests.txt`; ejecutable local: `CoreTests.exe`.

Cobertura conductual: estados de entrada/salida, secuencia correcta, errores de
orden, duplicados, acciones desconocidas, puntuación acotada, finalización precoz,
omisiones, reloj inválido, plazos, definiciones inválidas, overflow de puntos,
copias independientes, resultados inmutables y reinicios de intento.

**Ejecutado en esta entrega: 27/27.** No son pruebas de Unity ni de dispositivos.

## Nivel 2: Unity EditMode

Después de importar Starter Assets y generar la demo:

Window → General → Test Runner → EditMode → Run All.

- 27 casos compartidos del dominio.
- 1 test de conversión ScriptableObject y aislamiento de datos.
- 1 test del caso generado y su etiqueta de demo técnica.
- 1 test estructural de TrainingRoom: scripts presentes, cámara, rig, inputs,
  pads de teleport, snap, objeto agarrable, UI y referencia al escenario.

Base anterior al motor ampliado y polish: **37/37 aprobados**, incluidos los cinco tests del
Environment Builder y dos del catálogo de revisión. Evidencia local:
`TestResults/distribution-editmode.xml`. Las pruebas de assets requieren ejecutar
el generador; no omiten esa precondición silenciosamente. La suite actual ampliada
tiene 121/121, según la evidencia de integración al principio de este documento.

Ejecución batch alternativa, con Editor cerrado y assets ya generados:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe' -batchmode -projectPath 'C:\Users\Juan\Desktop\proyectovr' -runTests -testPlatform EditMode -testResults 'TestResults\editmode.xml' -logFile 'TestResults\editmode.log'
```

Crear `TestResults` antes si no existe. No añadir `-quit` al comando de tests;
el test runner gestiona su salida. Revisar XML/log y código de salida.

## Nivel 3: build Android

Emergency VR → 4 - Validate project setup, validación oficial OpenXR Android y
Emergency VR → 5 - Build development APK.

Exigir: sin errores de scripts, sin referencias perdidas, build IL2CPP/ARM64
completada y APK instalada. Guardar log de Unity y el nombre de APK usado.
**No ejecutado.**

## Nivel 4: aceptación física en Quest 3

Registrar Editor/paquetes, APK, versión Horizon OS, dispositivo, fecha y resultado.
Marcar cada punto solo después de observarlo:

- [ ] Iniciar desde la APK → Bootstrap → TrainingRoom visible sin pantalla negra.
- [ ] Tracking 6DoF del visor; escala y suelo correctos; cámara no enterrada.
- [ ] Mandos izquierdo y derecho visibles/con seguimiento y selección.
- [ ] Apuntar a cada pad y soltar el stick transporta al usuario sobre el suelo.
- [ ] Giro lateral del stick produce pasos de 30°; no movimiento continuo ni salto.
- [ ] Agarrar y soltar cubo con ambas manos por separado; cae sobre el suelo.
- [ ] Rayos seleccionan cada botón de UI, con feedback visual y texto legible.
- [ ] Grip al paciente antes de iniciar pide iniciar; no registra una acción.
- [ ] Iniciar → paciente UnconsciousBreathing y contador 0.
- [ ] Seleccionar paciente → Recovering, cambio de color y contador 1.
- [ ] Transición demo → Recovered y contador 2.
- [ ] Finalizar → 100/100, 0 errores, 0 omisiones y duración coherente.
- [ ] Nuevo intento → ningún estado, contador ni resumen anterior se arrastra.
- [ ] Transición demo antes del paciente → error de orden, paciente sin cambio.
- [ ] Finalizar sin acciones → 0/100 y 2 omitidas.
- [ ] Repetir selección de paciente → registra duplicado, no avanza otro paso.
- [ ] Pausar/reanudar visor: verificar qué sucede con tracking y tiempos reales.
- [ ] Diez minutos de uso: sin excepción de Unity, crash ni degradación visible.

La base **no está aceptada como demo VR funcional** hasta pasar estos puntos.

## Rendimiento

Usar Development Build y conectar Window → Analysis → Profiler → Active Profiler
al Android Player. Registrar CPU/GPU frame time, memoria, draw calls y GC Alloc
en reposo y durante teleport/agarre/UI. Si GPU Profiler no está disponible en la
configuración del dispositivo, usar la herramienta oficial de métricas de Meta
tras preparar su instalación en una iteración de profiling.

Como presupuesto inicial de ingeniería para 72 Hz: cada frame dispone de unos
13,89 ms. Esto no configura ni demuestra la frecuencia real del visor; registrar
la frecuencia observada y comparar con su presupuesto. Confirmar en build sin
instrumentación antes de extraer conclusiones comerciales.

La sala evita sombras y postprocesado. No se ha medido FPS, memoria, temperatura,
polígonos efectivos ni draw calls en Quest. No se añade pooling a unos pocos
objetos persistentes: se incorporará donde el perfil muestre asignaciones repetidas.
