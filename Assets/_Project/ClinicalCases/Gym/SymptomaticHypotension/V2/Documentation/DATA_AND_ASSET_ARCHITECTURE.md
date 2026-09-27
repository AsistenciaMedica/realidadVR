# VITAL VR — CASE 01 v2: datos y assets de Etapa B

Estado: prototipo formativo aprobado para desarrollo, pendiente de revisión clínica. No certificado ni validado por una entidad sanitaria. La Etapa B construye contratos y datos; no acredita las animaciones, voz, ropa, instrumentos o composición visual de la Etapa C y siguientes.

## Registro sin sustituir los casos existentes

`Resources/MedicalScenarios.json` permanece intacto: contiene los 44 escenarios médicos legacy, incluido `review-hypotension-v1`. `MedicalLibraryLoader.Load()` carga y valida esa biblioteca y después compone el registro complementario mediante `ClinicalScenarioV2Catalog.LoadInto()`.

`Resources/ClinicalScenarioV2Registry.asset` referencia directamente el `TextAsset` `Data/review-hypotension-v2.json` y el `ScriptableObject` `Assets/Case01HypotensionAssets.asset`. Las referencias Unity hacen alcanzables estos recursos en el build aunque el JSON resida fuera de Resources. El registro impide sustituir silenciosamente un caso existente o registrar dos veces el mismo ID.

El catálogo final contiene 44 escenarios médicos legacy + CASE 01 v2. La demo técnica continúa siendo una entrada separada del catálogo de experiencia.

## Separación de responsabilidades

| Recurso | Responsabilidad |
| --- | --- |
| JSON clínico | Identidad, metadata, capabilities, permisos explícitos, estados, transiciones, trayectorias, diálogos de texto, objetivos, alternativas y trazabilidad. |
| `Case01HypotensionAssets` | Referencias Unity a prefabs, controlador, clips, materiales, sprites y recursos UI. No conserva fisiología, permisos, progreso ni estado del intento. |
| Registro Resources | Enlace de build entre el TextAsset y sus recursos de presentación. No es un motor. |
| `ClinicalScenarioV2Catalog` | Adaptación de datos al catálogo existente. No crea un reloj ni conserva un paciente mutable. |
| `MedicalScenarioRuntime` | Única autoridad clínica mutable; el estado de presentación y las observaciones proceden del intento. |

`ClinicalScenarioV2Definition` complementa `ClinicalCaseDefinition`, no lo reemplaza. El adaptador crea reglas de interacción con cero puntos y sin prerrequisitos de instrumentos. El esquema legacy necesita una entrada `OutcomeRule`; ese adaptador no convierte v2 en una evaluación porcentual: la capability de objetivos selecciona su ruta propia del runtime.

Las acciones del perfil inicial son TalkToPatient, AssessResponsiveness, ObserveBreathing, AssistPatient, RequestHelp, DelegateHelp, ReassessPatient y PerformHandover. Instrumentos, mediciones y procedimientos avanzados están vacíos. Tener enumerados I1/I2 no concede permisos. La futura configuración debe habilitar cada recurso por escenario y perfil.

## Identidad y valores

ID `review-hypotension-v2`; versión `2.0.0`; especificación `0.1`; perfil exportado `I0_FIRST_RESPONDER`. `buildVersion` se suministra desde `Application.version` al crear el intento, nunca desde el JSON.

Daniel permanece hombre de 40 años. El adaptador anula la variabilidad legacy para este caso. Sus valores iniciales configurados son FC 88, TA 85/55, FR 18 y SpO2 97, consciente y orientado, capaz de hablar y cooperar, sentado con apoyo internamente. La posición clínica declarada no demuestra que la presentación provisional ya posea una animación sentada correcta.

No se publican esos valores como observaciones por cargar el caso. El historial observado empieza vacío. Los textos de síntomas proceden de respuestas de entrevista explícitas, no de `PatientSnapshot.dialogue` ni de la descripción del catálogo.

## Curvas y ramas

Estados presentes: HYP_00_INITIAL_PRESYNCOPE, HYP_01_SUPPORTED_OBSERVATION, HYP_02_IMPROVING, HYP_03_PERSISTENT_SYMPTOMS, HYP_04_RECURRENT_PRESYNCOPE y HYP_07_HANDOVER. Los identificadores HYP_05_BRIEF_TLOC y HYP_06_EARLY_RECOVERY_AFTER_TLOC están reservados únicamente en esta documentación; no aparecen como estados o destinos del JSON.

Las curvas 01 y 02 usan 30 + 30 segundos como parámetro de prototipo dentro de la ventana candidata de 30–90 segundos de la Clinical Spec. Persistencia y recurrencia usan 30 segundos de interpolación técnica provisional: **no representan un plazo clínico recomendado, un pronóstico ni un temporizador para demorar ayuda**. Su revisión queda anotada expresamente en R_PERSISTENT_TRAJECTORY y R_RECURRENT_TRAJECTORY para la Etapa E. Todos los valores/curvas y reglas relevantes tienen `clinicalReviewRequired = true`.

El runtime inicia cada curva desde el valor real al entrar en el estado, evitando saltos al cambiar de rama. Persistencia requiere una señal explícita; recurrencia exige incorporación física confirmada y su señal correspondiente. No hay parada cardiaca, pérdida de consciencia ni caída automática. El relevo conserva las constantes alcanzadas, sin forzar recuperación.

La respuesta de estado 01 se mantiene neutral al entrar: «Sigo mareado». La variante «así estoy algo mejor» exige un tiempo mínimo configurado de 30 segundos; con la transición favorable actual se pasa a estado 02 en ese momento. Por tanto no se promete una mejoría inmediata por solicitar ayuda o terminar una animación. Esta condición es de autoría pendiente de integrar con la presentación y diálogo definitivos.

## Referencias y procedencia

Las reglas `SPEC_0_1` son decisiones de autoría de la especificación aprobada y de la petición de Etapa B, no recomendaciones universales. Su fuente local es `docs/CLINICAL_IMPLEMENTATION_SPEC_HIPOTENSION_SINTOMATICA.md`. Las referencias heredadas `bp` y `faint` aportan contexto general; no validan cifras, curvas o evaluación. La etiqueta `CLIENT_REVIEW` del adaptador mantiene compatibilidad con el esquema antiguo; `clinicalReviewRequired` deja explícita la revisión aún pendiente.

Los `audioReference` del JSON son identificadores simbólicos de respuesta, no rutas de assets. `Case01HypotensionAssets.FindAudio()` puede resolverlos por nombre de clip cuando existan clips asignados. Actualmente devuelve null y corresponde utilizar el subtítulo/fallback de texto, sin fingir voz terminada.

## Organización y límites

Data y Assets contienen los recursos de esta fase. Prefabs, Animations, Audio y Materials se mantienen como directorios reservados, sin producción de assets final. Tests reserva pruebas específicas de contenido; las pruebas de contratos reutilizables siguen en los assemblies EditMode/PlayMode generales existentes. Los scripts generales viven en Scripts/Medical/V2, Scripts/Dialogue, Scripts/Patient y Scripts/Scenarios.

La API de datos sigue las reglas de serialización oficial de Unity: clases C# serializables para `JsonUtility`, referencias `UnityEngine.Object` en ScriptableObjects. Documentación consultada con Context7: https://docs.unity3d.com/Manual/json-serialization.html y https://docs.unity3d.com/Manual/script-serialization-best-practices.html.
