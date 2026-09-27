# CASE 01 — STAGE B IMPLEMENTATION REPORT

Estado: **CASE 01 — STAGE B COMPLETE**. Implementación y validación cerradas el 24 de septiembre de 2026: 160 EditMode y 21 PlayMode aprobadas, sin fallos ni pruebas omitidas. No se ha iniciado la Etapa C.

## 1. Archivos creados

Las rutas siguientes parten de `Assets/_Project/`, salvo los dos runners de `tools/`. Se enumeran los archivos creados en **esta Etapa B**, no todos los archivos actualmente sin seguimiento en Git.

| Módulo | Archivos nuevos |
| --- | --- |
| `Scripts/Medical/V2/` | `ScenarioCapabilities.cs`, `TrainingProfileDefinition.cs`, `ClinicalScenarioV2Definition.cs`, `ClinicalStateDefinition.cs`, `ClinicalTransitionDefinition.cs`, `ClinicalStateMachine.cs`, `VitalTrajectory.cs`, `PatientClinicalState.cs`, `ClinicalEvent.cs`, `ObservationRecord.cs`, `ObservedPatientDataStore.cs`, `MedicalScenarioRuntime.Clinical.cs` |
| `Scripts/Dialogue/` | `DialogueIntent.cs`, `DialogueResponseDefinition.cs`, `ClinicalDialogueController.cs` |
| `Scripts/Patient/` | `PatientPositionTransitionController.cs` |
| `Scripts/Scenarios/` | `Case01HypotensionAssets.cs`, `ClinicalScenarioV2Registry.cs`, `ClinicalScenarioV2Catalog.cs`, `ClinicalV2Report.cs` |
| `Scripts/UI/` | `ClinicalObservationText.cs`, `TrainingExperience.V2.cs` |
| `Resources/` | `ClinicalScenarioV2Registry.asset` |
| `Tests/EditMode/` | `ClinicalScenarioV2Tests.cs` |
| `Tests/PlayMode/` | `ClinicalScenarioV2IntegrationTests.cs` |
| `ClinicalCases/Gym/SymptomaticHypotension/V2/Data/` | `review-hypotension-v2.json` |
| `ClinicalCases/Gym/SymptomaticHypotension/V2/Assets/` | `Case01HypotensionAssets.asset` |
| `ClinicalCases/Gym/SymptomaticHypotension/V2/Documentation/` | `DATA_AND_ASSET_ARCHITECTURE.md`, `ASSET_DEBT.md`, `STAGE_B_IMPLEMENTATION_REPORT.md` |
| `tools/`, en la raíz del proyecto | `Test-StageB.ps1`, `Sync-StageBValidation.ps1` |

Cada nuevo script, JSON, asset y documento dentro de Assets tiene su archivo `.meta`. También se crean los `.meta` de las carpetas nuevas. Los archivos `V2/Prefabs/.gitkeep`, `V2/Animations/.gitkeep`, `V2/Audio/.gitkeep`, `V2/Materials/.gitkeep` y `V2/Tests/.gitkeep` conservan esos directorios vacíos sin simular recursos terminados. `.utmp/stage-b-validation/` y `TestResults/` son salidas de validación, no nuevas funcionalidades del producto.

## 2. Archivos modificados

Rutas relativas a `Assets/_Project/`:

| Archivo preexistente | Cambio de Etapa B |
| --- | --- |
| `Scripts/Medical/MedicalScenarioDefinition.cs` | Definición v2 opcional y política de repetición, manteniendo los valores por defecto legacy. |
| `Scripts/Medical/MedicalScenarioRuntime.cs` | Integración de la extensión clínica, metadata y debrief; conserva la autoridad del runtime existente. |
| `Scripts/Medical/MedicalLibraryLoader.cs` | Composición del registro v2 después de cargar y validar la biblioteca anterior. |
| `Scripts/Scenarios/ScenarioManager.cs` | Conexión del intento, pausa, tiempo y adaptadores de la nueva capacidad. |
| `Scripts/Scenarios/ReviewCaseSession.cs` | Ficha basada en observaciones y selección del esquema de exportación v2. |
| `Scripts/Patient/PatientController.cs` | Entrega del estado clínico proyectado a la presentación. |
| `Scripts/Patient/Presentation/PatientVisualController.cs` | Consumo de la fase respiratoria del runtime en v2. |
| `Scripts/Medical/Interaction/MedicalProcedureRig.cs` | Permisos del perfil y adaptación de tablet/interacciones para v2. |
| `Scripts/Medical/Interaction/MedicalPhysicalTool.cs` | Protección de la ruta de instrumentos según capabilities y permisos. |
| `Scripts/Medical/Interaction/MedicalProcedureAudio.cs` | Evita el loop respiratorio independiente legacy en v2. |
| `Scripts/UI/TrainingExperience.cs` | Integración de la experiencia compartida con las capacidades v2. |
| `Scripts/UI/TrainingExperience.Clinical.cs` | Selección de observaciones y resultados por objetivos para v2. |
| `Scripts/UI/TrainingExperience.Pages.cs` | Briefing y opciones de práctica acordes con la separación de información. |
| `Scripts/Desktop/DesktopDemoController.cs` | Adaptación del smoke del catálogo ampliado, conservando la ruta Desktop compartida. |
| `Tests/PlayMode/MedicalIntegrationTests.cs` | Verificación explícita de 44 casos médicos legacy + v2 + demo técnica. |

`TrainingExperience.cs`, `TrainingExperience.Clinical.cs` y `TrainingExperience.Pages.cs` **ya existían antes de esta fase** como parte del rediseño VITAL VR, aunque continuaban sin seguimiento en Git. Aquí se modifican; no se presentan como creados en Etapa B. `TrainingExperience.Layout.cs` y `TrainingExperience.Validation.cs` también son preexistentes y no se modifican en esta etapa. Tampoco se atribuyen a Etapa B los cambios previos de materiales XR, avatar, paquetes, branding o entorno presentes en el workspace.

El archivo `Resources/MedicalScenarios.json`, que contiene los 44 escenarios anteriores, se conserva. SHA-256 idéntico al iniciar y cerrar esta etapa: `F60EDA87A7E2B28E91254E99CDD82E87185C8B0277CC8E985C3082D721986B34`.

## 3. Nueva arquitectura

`MedicalScenarioRuntime` conserva la única autoridad clínica mutable. La máquina de estados es un componente interno sin reloj, paciente ni MonoBehaviour propios. Los adaptadores Unity envían intenciones y hechos mediante el reloj de `ScenarioManager`.

Las capacidades habilitan el nuevo comportamiento. Las definiciones anteriores conservan la política legacy. Los permisos son listas explícitas por perfil y escenario; ningún nivel I1/I2 otorga permisos generales.

El JSON contiene datos clínicos serializables y referencias simbólicas; el ScriptableObject contiene recursos Unity. Un registro de TextAssets hace que el JSON del caso forme parte de la carga del producto.

El enlace `MedicalScenarioDefinition.clinicalV2` es `[NonSerialized]` y se compone al cargar ese registro. Esto evita que el serializador inline de Unity materialice una definición v2 vacía en los casos antiguos. La definición clínica independiente sí se serializa a JSON; su validación sigue siendo estricta y `Copy()` conserva una copia profunda del complemento.

## 4. Flujo Runtime → ClinicalState

La proyección `PatientClinicalState` es inmutable y separada del snapshot mutable interno. Expone identidad del intento, estado, tiempo, postura solicitada/efectiva/clínica, signos, capacidades de habla/cooperación y fase respiratoria.

La evolución y la integral de FR se calculan en el runtime. La fase no se reinicia al cambiar FR. La presentación consume la proyección; no resuelve otra fisiología. El loop respiratorio antiguo independiente no se usa en v2; el audio coordinado se integra en una etapa posterior.

## 5. Flujo ObservedPatientData

Cada intento tiene un historial propio, inicialmente vacío. El runtime añade observaciones de entrevista, observación física y adquisición autorizada. Sus consumidores reciben copias; los valores internos no se copian automáticamente a la ficha.

Una adquisición inválida se representa con `valid=false` y `hasValue=false`, sin copiar signos reales. La hora es tiempo de simulación del intento. Las mediciones anteriores permanecen históricas al evolucionar el paciente.

La UI v2 y la tablet consumen el historial. El modo guiado no rellena observaciones con datos internos. Esta etapa no activa una TeachingOverlay de constantes internas.

La exportación de v2 usa `Scripts/Scenarios/ClinicalV2Report.cs`, con **`schemaVersion = 4`**, seleccionado por `usesObjectiveBasedEvaluation`. `ReviewCaseSession.ExportResult()` guarda el JSON en `Application.persistentDataPath/ReviewResults/attempt-<fecha UTC>.json`. El archivo se construye con copias de los registros del resultado final.

| Grupo del esquema | Campos exportados |
| --- | --- |
| Identidad y versiones | `attemptId`, `scenarioId`, `caseId` — alias de compatibilidad del ID —, `caseName`, `scenarioVersion`, `clinicalSpecVersion`, `trainingProfile`, `buildVersion` |
| Contexto | `schemaVersion`, `createdUtc`, `durationSeconds`, `clinicalReviewRequired` |
| `clinicalEvents[]` | ID del evento/intento, tiempo de simulación, escenario/versiones/perfil/build, estado clínico, tipo, origen, resultado, calidad y metadata clave/valor |
| `objectives[]` | `objectiveId`, `name`, `result`, `evidenceEventIds[]` |
| `measurements[]` | Identidad, tipo, tiempo/estado/origen/calidad/validez, `hasValue`, `value`, `systolic`, `diastolic`, `unit` |
| `interviews[]` | Contexto de observación, `intent`, `responseId`, `text`, `subtitle` |
| `physicalObservations[]` | Contexto de observación y `description` |

`ObjectiveResult` expresa No evaluado, No demostrado, Logrado con ayuda o Logrado autónomamente. Su valor serializado de enum identifica esa categoría; no es una nota. Para mediciones inválidas, `hasValue=false` indica ausencia de valor clínico y los campos numéricos no deben interpretarse como una lectura. El esquema v2 no contiene `scorePercent` ni snapshots internos de signos no adquiridos. La exportación legacy conserva su esquema anterior.

## 6. Flujo de postura

Solicitud → Requested → Preparing → Transitioning → validación externa → confirmación al runtime.

Cada solicitud obtiene un ticket vinculado al intento. Confirmaciones antiguas, de otro intento, durante pausa o sin la fase previa se rechazan. Cancelar o fallar no establece supino. La UI sólo solicita/cancela; no ofrece un botón que finja validación física.

La animación, los apoyos y la comprobación geométrica final pertenecen a las etapas C/D. Los tests usan un adaptador de validación controlado para probar el contrato; eso no acredita una transición corporal final.

## 7. Estados implementados

`HYP_00_INITIAL_PRESYNCOPE`, `HYP_01_SUPPORTED_OBSERVATION`, `HYP_02_IMPROVING`, `HYP_03_PERSISTENT_SYMPTOMS`, `HYP_04_RECURRENT_PRESYNCOPE`, `HYP_07_HANDOVER`.

Los identificadores 05/06 están reservados y excluidos de la definición y de las transiciones alcanzables.

Datos iniciales: Daniel, hombre, 40 años, alerta/orientado, sentado con apoyo, FC 88, TA 85/55, FR 18, SpO₂ 97, puede hablar y cooperar. Son datos del JSON del prototipo, no constantes universales. Reglas y curvas conservan `clinicalReviewRequired=true`.

## 8. Compatibilidad legacy

44 escenarios médicos anteriores + CASE 01 v2 = 45 escenarios médicos; con la demo técnica son 46 entradas. v1 sigue disponible. La regresión verifica contenido de los 44 originales, duplicados y puntuación legacy. v2 utiliza resultados por objetivos y no muestra una puntuación porcentual.

## 9. Tests añadidos

36 casos EditMode en `ClinicalScenarioV2Tests` y 8 PlayMode en `ClinicalScenarioV2IntegrationTests`.

Carga/composición, datos iniciales, estados reservados, copias y proyección, separación de información interna/adquirida, adquisición válida/inválida/repetida, permisos I0/I1/I2 configurados, objetivos y alternativas, confirmación/cancelación de postura, tiempo/fase/curvas, diálogo, ayuda temprana, pausa, reset, versiones y serialización.

Las suites PlayMode comprueban las conexiones de escena y UI. Las pruebas se ejecutan sobre una copia de validación del proyecto para conservar el editor abierto del usuario.

Para reproducir la validación desde la raíz de `proyectovr`, sincronizar primero la copia y ejecutar después las suites completas:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Sync-StageBValidation.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Test-StageB.ps1 -Mode All -ProjectPath ".\.utmp\stage-b-validation"
```

La copia incluye Assets, Packages y ProjectSettings, con reutilización del PackageCache local. El runner utiliza Unity `6000.3.23f1`, target `Win64` y `-runTests`; `-Mode EditMode` o `-Mode PlayMode` permite ejecutar una suite concreta. Los XML y logs se escriben en `TestResults/stage-b-<modo>-<id>.xml` y `.log` del proyecto fuente. Después de una suite aprobada se actualizan también `stage-b-editmode.xml/.log` o `stage-b-playmode.xml/.log`. El runner exige resultado Passed, código de salida correcto y un total de pruebas mayor que cero; una compilación sin XML no se acepta como validación.

## 10. Resultados EditMode

**160 aprobadas, 0 fallos, 0 omitidas**, ejecución completa en Unity el 24 de septiembre de 2026. Incluye 36 nuevas y las 124 anteriores: 58 de `MedicalRuntimeTests` (incluida ejecución completa de los 44 casos), 27 de dominio y 39 de catálogo, presentación, contacto, interacción y entorno.

Evidencia: `TestResults/stage-b-editmode.xml` y `TestResults/stage-b-editmode.log`. XML original: `stage-b-editmode-744fb05ad8f443dd98f008db712005a3.xml`. Además se ejecutó `tools/Test-Core.ps1`: 27 aprobadas, 0 fallos; esas mismas 27 también están incluidas en las 160 de Unity.

## 11. Resultados PlayMode

**21 aprobadas, 0 fallos, 0 omitidas**, ejecución completa en Unity el 24 de septiembre de 2026. Incluye las 8 nuevas y las 13 anteriores: 4 de presentación articulada, 5 de experiencia de entrenamiento, 2 de integración médica, 1 de herramientas y 1 de la escena de entrenamiento.

Evidencia: `TestResults/stage-b-playmode.xml` y `TestResults/stage-b-playmode.log`. XML original: `stage-b-playmode-3c02006ab9e0451c9ddf249c59687414.xml`. Se ejecutó la escena `TrainingRoom` y sus conexiones reales; las confirmaciones físicas de los tests son controladas. La compatibilidad compartida Desktop/XR se conserva y compila; no se afirma una prueba manual de todos los flujos Desktop ni validación en un Quest 3 físico.

Se corrigieron los fallos descubiertos durante la validación: invocación de curvas, serialización inline del complemento opcional, retorno de acciones repetidas, cancelación de tickets al llegar a relevo y actualización de la proyección antes de pausar. Las suites finales se ejecutaron después de esas correcciones; no se eliminaron ni omitieron pruebas.

Al cerrar, los 108 archivos `.cs`, `.json` y `.asmdef` de `Assets/_Project/` coinciden por SHA-256 con la copia ensayada. El detalle queda en `TestResults/stage-b-source-manifest.json`.

## 12. Deudas técnicas y límites de la etapa

- No se han creado animaciones finales, ropa, rig nuevo, NPC, voces ni instrumental final.
- La referencia de avatar es provisional; la postura corporal visible todavía requiere las etapas C/D.
- Las curvas de mejora usan 30 + 30 segundos dentro del intervalo candidato de la especificación aprobada. Las interpolaciones de persistencia/recurrencia usan 30 segundos como parámetro técnico provisional, pendiente de revisión en la etapa E. No son plazos clínicos ni disparadores automáticos para pedir ayuda.
- La ayuda y el relevo disponen de contratos; el operador, la voz y la representación física se conectarán después. Una solicitud no equivale a confirmación.
- I0 no tiene instrumentos de medición autorizados. Las pruebas de adquisición habilitan una configuración I1 explícita; no presentan el instrumental avanzado como terminado.
- JSON/ScriptableObject y exportación mantienen la marca de revisión clínica pendiente. La aprobación de desarrollo no certifica contenido sanitario.

El alcance se detiene en la arquitectura de la Etapa B.

Límite práctico: en una sesión manual, solicitar asistencia no completa por sí mismo el traslado. Todavía falta el adaptador corporal final que ejecute y valide espacio, banco, suelo, pies, manos, apoyos y tracking antes de confirmar la postura. El contrato y las pruebas permiten preparar esa integración, pero no demuestran una transferencia física terminada, ausencia de clipping ni comodidad en Quest 3. Esos resultados corresponden a las etapas de presentación e interacción posteriores.
