# VITAL VR

**Simulación y entrenamiento clínico inmersivo.** Proyecto Unity 6000.3.23f1 con demo Windows,
alcance de lanzamiento de **3 escenarios y 5 casos por escenario (15 casos)**:
gimnasio, centro comercial y campo de fútbol. Incluye RCP virtual, DEA manipulable
y portal comercial con administración. Los casos siguen
`CLIENT_REVIEW`. La nueva experiencia incluye bienvenida, catálogo por entorno,
preparación, monitor clínico, pausa real y revisión de resultados.

Alcance vigente: [lanzamiento 3 × 5 y pendientes para Meta](docs/LAUNCH_SCOPE.md).
La clínica dental y los demás casos se conservan para futuras ampliaciones.
Los quince casos tienen [pacientes distintos, contexto y conversación propios](docs/PATIENT_ROSTER.md),
orientados a primeros intervinientes y brigadistas.

Para continuar desde este repositorio: **Emergency VR → Demo → Play with keyboard
and mouse**. Ver [experiencia de entrenamiento](docs/TRAINING_EXPERIENCE.md), [demo Windows](docs/CLIENT_DEMO.md),
[estado visual y pruebas](docs/VISUAL_INTERACTION_STATUS.md) y
[capturas reales](docs/screenshots/README.md).

Identidad del simulador: **VITAL VR**, con interfaz nativa compartida entre Desktop
y VR. El [branding del portal web](docs/BRANDING.md) tiene su documentación propia.
Los registros visuales anteriores describen entregas históricas; el flujo actual
se documenta en [TRAINING_EXPERIENCE](docs/TRAINING_EXPERIENCE.md).

Se conserva **TrainingRoom** con arquitectura, mobiliario y prefabs reemplazables.
Para regenerarla y revisar su validación, ver
[TrainingRoom procedural](docs/TRAINING_ROOM_ENVIRONMENT.md).
La validación de comodidad y rendimiento con hardware Quest 3 sigue pendiente.

No es un dispositivo médico ni software clínico certificado. La demo prueba
interacciones y estados arbitrarios; no enseña un protocolo médico aprobado.

## Configuración inicial de un checkout nuevo

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

## Recorrido de entrenamiento

Al abrir la aplicación aparece la bienvenida **VITAL VR** sobre un fondo neutro.
La escena base se prepara en segundo plano y se revela al iniciar el ejercicio.

1. Elegir uno de los tres escenarios y uno de sus cinco casos.
2. Leer la preparación y elegir práctica guiada o evaluación.
3. Pulsar **Iniciar entrenamiento** para activar el caso y su reloj.
4. Usar equipo y acciones; consultar monitor y ficha independientemente del menú.
5. Pausar con Escape en Desktop o B/Y en VR; continuar o finalizar el intento.
6. Revisar resumen, acciones, cronología, métricas y referencias; guardar el informe.

La ayuda incluye una práctica de controles que conserva la secuencia técnica
original. El catálogo, evolución clínica y scoring existentes se reutilizan.

## Pruebas que ya se pueden ejecutar

Desde PowerShell en la raíz, sin instalar dependencias adicionales:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Test-Core.ps1
```

Resultado de esta entrega: **27 pruebas aprobadas, 0 fallos**, compilación C# del
dominio con advertencias tratadas como errores. Esto no compila los adaptadores
Unity. Pruebas y límites: [TESTING](docs/TESTING.md) y [VALIDATION_REPORT](docs/VALIDATION_REPORT.md).

## Documentación

- [Nueva experiencia de entrenamiento y verificación](docs/TRAINING_EXPERIENCE.md).
- [Demo Windows y portal Railway](docs/CLIENT_DEMO.md).
- [Alcance de lanzamiento: 3 escenarios × 5 casos](docs/LAUNCH_SCOPE.md).
- [Catálogo seleccionado e IDs](docs/SCENARIO_LIBRARY.md).
- [Antecedentes y fuentes de los cinco pilotos médicos](docs/MVP_SCOPE_AND_MEDICAL_REVIEW.md).
- [Arquitectura y extensión de casos](docs/ARCHITECTURE.md).
- [Plan por fases](docs/DEVELOPMENT_PLAN.md).
- [Instalación, OpenXR y Quest](docs/QUEST_SETUP.md).
- [Dependencias, versiones y fuentes](docs/DEPENDENCIES.md).
- [Pruebas y aceptación en visor](docs/TESTING.md).
- [Estado real de la entrega](docs/VALIDATION_REPORT.md).

Versionar `Assets` y sus `.meta`, `Packages`
(incluido `packages-lock.json` cuando Unity lo genere), `ProjectSettings` y docs.
`Library`, `Temp`, `Logs`, `Builds`, cachés y resultados locales están ignorados.

El catálogo de lanzamiento incluye 15 casos en revisión clínica. La biblioteca de
autoría conserva los 44 guiones originales y el caso de Daniel V2. RCP, DEA y adquisiciones
son interacciones virtuales; las métricas no acreditan una técnica clínica real.
El seguimiento de manos sin mandos y la validación física en Quest requieren
trabajo y comprobaciones específicos.
