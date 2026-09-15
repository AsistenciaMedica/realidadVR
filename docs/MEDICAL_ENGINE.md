# Vital VR: ampliación del estado actual

Inspección inicial 2026-09-13, antes de las ampliaciones: Core contiene CaseSession y evaluación lineal; ScenarioManager
recibe las acciones de XR y Windows; PatientController presenta el estado; el
catálogo de revisión contiene la demo técnica y cinco pilotos; el portal Node
distribuye el ZIP y muestra datos estáticos. Solo TrainingRoom existe como ambiente
3D. Se conservan esos componentes, assets, exportación y tests.

La extensión usa JSON versionado en Resources/MedicalScenarios.json como fuente
única. MedicalScenarioDefinition describe paciente, acciones, requisitos, eventos,
ramas, puntuación, referencias y validación. MedicalScenarioRuntime es C# sin APIs
Unity: recibe reloj y seed, copia los datos, encapsula el paciente y genera debrief.
ScenarioManager delega al motor nuevo cuando hay una definición médica; CaseSession
sigue ejecutando la demo técnica. Windows y XR comparten exactamente ese punto de
entrada. El catálogo original permanece como respaldo y los cinco pilotos se
migran por ID en el catálogo de ejecución.

Los tiempos de deterioro y probabilidades no se presentan como fisiología validada.
Son parámetros educativos explícitos de los guiones, pendientes de revisión. La
calidad clínica de RCP no se infiere de pulsaciones. La nueva capa de interacción
añade muestras de movimiento y ciclos de ratón/mandos XR al informe como métricas
virtuales separadas; no son profundidad clínica calibrada ni fuerza sobre maniquí.
No existe prescripción libre. Todos los escenarios están en CLIENT_REVIEW.

Los cuatro ambientes se implementan como módulos procedurales ligeros sobre la
escena y el rig existentes, reutilizando materiales y equipo. Son blockouts
funcionales ampliados con detalle visual para revisar casos, no modelos finales
de los entornos contratados. La arquitectura visual y sus límites están en
[VISUAL_INTERACTION_STATUS.md](VISUAL_INTERACTION_STATUS.md).

Verificación de integración actual: Core 27/27, EditMode 121/121, PlayMode 4/4
y web 16/16. Build Windows generado en `Builds/Windows/20260913-100825`;
smoke del ejecutable final aprobado y proceso finalizado: 45 secuencias (una
técnica y 44 médicas), componentes RCP/DEA, contacto y JSON. La biblioteca mantiene 44 variantes
`CLIENT_REVIEW`, sin aprobación clínica ni aceptación física en Quest.
