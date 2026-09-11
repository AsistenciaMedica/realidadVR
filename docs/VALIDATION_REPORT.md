# Informe de validación — 2026-09-07

## Estado real

| Área | Entregado | Validado |
| --- | --- | --- |
| Proyecto Unity | Assets, manifest, versión fijada, asmdefs y herramientas Editor | Estructura fuente; importación pendiente |
| Android / Quest | Configurador IL2CPP, ARM64, Vulkan y documentación | No ejecutado en Editor |
| OpenXR | Configurador loader, startup, Meta Quest Support, Touch y single pass | APIs contrastadas; runtime pendiente |
| XR | Generador usa rig oficial, teleport, snap, grab y UI | Fuentes del paquete revisadas; no probado con mandos |
| Escenas/prefabs | Código generador Bootstrap y TrainingRoom | Aún no generados como assets `.unity`/`.prefab` |
| Arquitectura | Core puro, adaptadores Unity, ScriptableObjects | Dominio compilado |
| Paciente | Estados y capa de presentación | Transiciones puras probadas; visuales pendientes |
| Escenarios | Definiciones y sesión desacopladas de escena | Dominio probado; carga Unity pendiente |
| Evaluación | Orden, tiempos, errores, omisiones y resultado | Dominio probado; UI pendiente |
| Pruebas | 27 puras + 3 Unity | 27 aprobadas, 0 fallos; 3 Unity pendientes |
| Build | Menú para APK de desarrollo | No compilada ni instalada |
| Git | Repositorio e ignorados Unity | Inicializado, sin commits |

## Evidencia ejecutada

`powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Test-Core.ps1`

Salida: `RESULT: 27 passed; 0 failed.` Compilación C# Framework con
`/warnaserror+`, salida del proceso 0. Log local en `TestResults/core-tests.txt`.

Esta comprobación compila **solo Core + suite compartida + runner**. No compila
MonoBehaviours, generadores, paquetes Unity ni integración XR. No hay mediciones
de FPS ni observación de la demo en un visor.

Verificaciones estáticas adicionales realizadas: 6 archivos JSON/asmdef válidos;
54 archivos `.meta`, sin GUID duplicados ni metadatos faltantes; sintaxis del
script PowerShell válida. Git ignora caché, resultados, Library y builds locales.

## Pendientes y límites

- Unity/Hub no detectados en las ubicaciones inspeccionadas. Instalar/activar y
  abrir el proyecto según QUEST_SETUP antes de validar la integración.
- Generar escenas/prefabs y assets del caso desde el menú. No se escribieron
  archivos `.unity` simulados que aparenten haber sido generados por el Editor.
- Resolver paquetes en Unity y conservar el lock resultante. Compatibilidad
  documental y revisión de código no garantizan compilación conjunta.
- Ejecutar tests EditMode, validación oficial de OpenXR, build y prueba en Quest.
- El caso es una secuencia lineal técnica con dos acciones, sin lógica médica.
- No hay RCP, DEA, dispositivos de signos, cuatro entornos, audio ni hand tracking.
- No hay guardado persistente ni pausa automática de la evaluación al quitarse
  el visor. Los tiempos usan reloj real e incluyen tiempo en segundo plano.
- La demo usa la fuente integrada y placeholders; legibilidad y comodidad deben
  comprobarse físicamente. No hay evaluación clínica validada.

## Próximo hito

Primera apertura del proyecto con Unity 6000.3.23f1 → ejecutar menús 1–4 →
EditMode → APK de desarrollo → aceptación física documentada en TESTING.
No iniciar RCP/DEA ni otras fases antes de cerrar ese hito.
