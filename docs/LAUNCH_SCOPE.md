# VITAL VR — lanzamiento 3 × 3

Decisión vigente del **13 de octubre de 2026**, acordada por el usuario y Juan:
**tres entornos —cancha, gimnasio y calle— con las mismas tres pruebas en cada uno**.
Son tres tipos de ejercicio y nueve combinaciones jugables. Sustituye el alcance
del 30 de septiembre: el centro comercial sale de la selección de lanzamiento.

## Pruebas compartidas

| Orden | Prueba | Cancha | Gimnasio | Calle |
| --- | --- | --- | --- | --- |
| 1 | Mareo y toma de síntomas, como Daniel | Sí | Sí | Sí |
| 2 | Paro cardiorrespiratorio y RCP | Sí | Sí | Sí |
| 3 | Atragantamiento y maniobras de desobstrucción | Sí | Sí | Sí |

Se elige primero el entorno y después una de las tres pruebas, siempre en ese
orden. La primera reutiliza la valoración, conversación y asistencia de Daniel
V2; la segunda reutiliza el caso de parada presenciada y sus interacciones de
RCP/DEA; la tercera reutiliza la obstrucción grave en adulto consciente, con
interacción de golpes dorsales y compresiones abdominales sobre el paciente
virtual, expulsión del objeto según la evolución del guion y reevaluación.

La revisión de desobstrucción toma como referencia las
[guías de primeros auxilios de RCUK 2025](https://www.resus.org.uk/professional-library/2025-resuscitation-guidelines/first-aid-guidelines).
La resolución programada de esta rama es una condición del ejercicio, no una
garantía de resultado clínico.

Daniel, Andrés y Camila se repiten en sus respectivas pruebas en los tres lugares.
Cambian el contexto, el entorno, los testigos y las referencias de ubicación;
las reglas de cada ejercicio proceden de una definición compartida. Las nuevas
respuestas de Daniel en exteriores usan subtítulos cuando no existe grabación.

## Implementación y mantenimiento

- `Assets/_Project/Resources/ReleaseScope.json` selecciona los nueve casos.
- `Assets/_Project/Resources/ReleaseCases.json` define la correspondencia entre
  cada combinación y sus tres casos fuente, con presentación y contexto propios.
- `ReleaseCaseVariants` compone las variantes en Unity antes de validar el
  alcance y aplicar `PatientRoster.json`; cada sesión copia sus propios datos.
- `tools/Sync-MedicalPortal.mjs` usa las mismas variantes para generar el catálogo
  público y [los IDs seleccionados](SCENARIO_LIBRARY.md).
- Calle dispone de entorno procedural, acera, calzada separada, mobiliario y zona
  de paciente despejada. El portal la representa con una ilustración identificada
  como tal hasta disponer de una captura de esta versión.
- Los diálogos se contextualizan por lugar. Las frases nuevas sin grabación se
  presentan mediante subtítulos; no reutilizan una voz que describa otro lugar.

La práctica de controles no cuenta como una de las nueve combinaciones. Los 44
guiones originales y el caso fuente de Daniel V2 permanecen en la biblioteca de
autoría. El centro comercial, la clínica dental y las demás pruebas quedan fuera
del lanzamiento. El contenido conserva el estado `CLIENT_REVIEW`.

## Verificación y cierre

La matriz de aceptación debe recorrer las nueve combinaciones desde la selección
hasta los resultados: postura, conversación o testigo, acciones propias, pausa,
reinicio y cambio de entorno. En desobstrucción se comprueba el contacto y el
movimiento del control, la interrupción de la maniobra y la expulsión visual.
Estos gestos virtuales no miden fuerza física aplicada a un paciente.

Comandos para sincronizar y verificar el catálogo y el portal:

```powershell
node tools/Sync-MedicalPortal.mjs
node --test tools/ReleaseScope.test.mjs
npm.cmd --prefix demo run build
npm.cmd --prefix demo/web run build
npm.cmd --prefix demo test
```

Verificación realizada para esta adaptación:

- Núcleo clínico: **27/27** pruebas; alcance compartido Unity/portal: **5/5**.
- Backend del portal: **17/17**; compilaciones del catálogo y React correctas.
- Navegador: selección y navegación de las nueve combinaciones, escritorio y
  móvil sin errores ni desbordes; capturas en `TestResults/release-3x3/`.
- Unity EditMode: **297/297**, incluyendo variantes, identidades compartidas,
  postura inicial, silencio durante la obstrucción y contexto de Daniel.

En Unity ejecutar las pruebas de alcance, variantes, pacientes, entornos,
`DizzinessEnvironmentIntegrationTests` y `ChokingInteractionTests`, además de las
regresiones de interacción. Los resultados de esta adaptación se registran en
`TestResults/release-3x3/`. La revisión clínica, la comprobación física en Quest y
una nueva compilación de distribución corresponden a este alcance; las evidencias
históricas siguientes no las sustituyen.

## Historial del alcance anterior

Las entregas y los resultados siguientes corresponden a **3 × 5** (gimnasio,
centro comercial y campo de fútbol). Se conservan como historial.

## Verificación de esta reducción — 2026-09-30

- Unity 6000.3.23f1: compilación de scripts correcta; **51/51 EditMode** de
  `ReleaseScopeTests`, `ClinicalScenarioV2Tests` y `ReviewCatalogTests`.
- **20/20 PlayMode** de `MedicalIntegrationTests`, `TrainingExperienceTests`,
  `ArticulatedPresentationTests` y `ClinicalScenarioV2IntegrationTests`.
- Sincronización Unity/portal: **2/2** pruebas de alcance; portal: **17/17** pruebas
  Node; validación de distribución web y compilación React correctas.
- Navegador Edge: tres tarjetas, quince nombres reales, enlaces y recarga,
  exclusión de dental, detalle V2 y vista móvil sin errores JavaScript ni desborde.

Resultados locales: `TestResults/release-scope-editmode.xml`,
`TestResults/release-scope-playmode.xml` y `TestResults/vital-release-mobile.png`.
Estas pruebas se ejecutaron en el equipo de desarrollo. No se generó una APK de
producción ni se probó el contenido en un visor Quest ni se envió a Meta.

## Pacientes y compilación de revisión — 2026-10-01

Los quince casos tienen modelos, identidades y conversaciones propias, con
mejoras de contexto y evolución para primeros intervinientes y brigadistas.
La [documentación de pacientes](PATIENT_ROSTER.md) registra **263/263 EditMode**,
**49/49 PlayMode**, el recorrido de los quince pacientes en Windows y la
[galería de capturas reales](../TestResults/PatientRosterPreview/index.html).

Se generó una [APK de desarrollo ARM64](../Builds/Android/EmergencyVR-20261001-222416.apk)
de 201 MB, con las quince apariencias y mallas incluidas, aviso de licencia y
firma v2 de depuración verificada. Esta compilación conserva el paquete
provisional y necesita prueba física en Quest. La firma de producción y el
envío a Meta siguen pendientes; los catorce diálogos nuevos todavía son textuales.
