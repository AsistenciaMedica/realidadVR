# Emergency VR — base para Meta Quest 3

Primera iteración: fase 1 y base de fase 2 de un simulador de emergencias en VR.
La entrega es un **proyecto fuente preparado para Unity**, con lógica comprobada
y herramientas para generar la demo. **Todavía no se ha importado/compilado en
Unity, generado las escenas con el Editor ni probado una APK en Quest 3**:
Unity/Hub no fueron encontrados en este equipo.

No es un dispositivo médico ni software clínico certificado. La demo prueba
interacciones y estados arbitrarios; no enseña un protocolo médico aprobado.

## Arranque

1. Instalar Unity **6000.3.23f1 (6.3 LTS)** con Android Build Support,
   Android SDK & NDK Tools y OpenJDK; activar la licencia desde Hub.
2. Hub → Projects → Add → Add project from disk → seleccionar **esta carpeta**.
   No crear otro proyecto dentro de ella.
3. Abrir el proyecto; esperar a Package Manager y a la compilación de scripts.
4. Unity → **Emergency VR → 1 - Import Starter Assets**. Esperar la recompilación.
5. **Emergency VR → 2 - Generate demo**.
6. **Emergency VR → 3 - Configure Android OpenXR**. Cerrar y reabrir Unity si
   aparece el aviso de cambio de Active Input Handling.
7. Seguir [QUEST_SETUP](docs/QUEST_SETUP.md) para activar el target, validar,
   compilar e instalar. No basta con pulsar Play para probar standalone.

Los menús 1–3 preparan referencias, prefabs, escenas, casos y configuración. No
hay que montar manualmente un rig ni enlazar botones. El menú 2 conserva escenas,
prefabs, materiales y casos existentes; vuelve a asignar URP y el orden de escenas.

## Demo prevista

Bootstrap carga TrainingRoom. La sala tiene un rig con mandos, cuatro zonas de
teleport, giro de 30°, un cubo agarrable, un paciente placeholder y UI world-space.

1. Moverse entre zonas turquesa y agarrar/soltar el cubo con Grip.
2. Apuntar al panel y pulsar Trigger sobre **Iniciar caso demo**.
3. Seleccionar el paciente con Grip: registra `demo.inspect`, cambia de estado/color.
4. Pulsar **Transición demo**: registra `demo.confirm`, cambia al estado final.
5. Pulsar **Finalizar**: mostrar puntuación, duración, errores y omisiones.

Los dos pasos correctos dan 100/100. Finalizar antes muestra omisiones. Ejecutar
el segundo paso primero registra un error sin avanzar el estado. Los cambios de
estado/color son pruebas técnicas, no resultados clínicos.

## Pruebas que ya se pueden ejecutar

Desde PowerShell en la raíz, sin instalar dependencias adicionales:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Test-Core.ps1
```

Resultado de esta entrega: **27 pruebas aprobadas, 0 fallos**, compilación C# del
dominio con advertencias tratadas como errores. Esto no compila los adaptadores
Unity. Pruebas y límites: [TESTING](docs/TESTING.md) y [VALIDATION_REPORT](docs/VALIDATION_REPORT.md).

## Documentación

- [Arquitectura y extensión de casos](docs/ARCHITECTURE.md).
- [Plan por fases](docs/DEVELOPMENT_PLAN.md).
- [Instalación, OpenXR y Quest](docs/QUEST_SETUP.md).
- [Dependencias, versiones y fuentes](docs/DEPENDENCIES.md).
- [Pruebas y aceptación en visor](docs/TESTING.md).
- [Estado real de la entrega](docs/VALIDATION_REPORT.md).

Git inicializado, sin commits. Versionar `Assets` y sus `.meta`, `Packages`
(incluido `packages-lock.json` cuando Unity lo genere), `ProjectSettings` y docs.
`Library`, `Temp`, `Logs`, `Builds`, cachés y resultados locales están ignorados.

No se incluyen RCP, DEA, mediciones, hand tracking ni entornos finales en esta fase.
