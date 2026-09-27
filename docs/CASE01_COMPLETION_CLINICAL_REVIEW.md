# CASE 01: cierre jugable y revisión clínica del itinerario I0

Fecha de contraste documental: **24-09-2026**. Alcance: `review-hypotension-v2`, España, primer interviniente no sanitario con facilitación de un responsable titulado. Este documento concreta el cierre educativo del prototipo; **no constituye una aprobación clínica ni certifica competencias**.

Se han leído la [especificación clínica v0.1](CLINICAL_IMPLEMENTATION_SPEC_HIPOTENSION_SINTOMATICA.md) y el [JSON v2](../Assets/_Project/ClinicalCases/Gym/SymptomaticHypotension/V2/Data/review-hypotension-v2.json). El JSON ya limita I0 a comunicación, observación, asistencia, ayuda, reevaluación y relevo, sin instrumental. Sus estados son 00–04 y 07. Se mantienen las constantes y curvas aprobadas para el prototipo, sus tiempos de autoría y `clinicalReviewRequired: true`. No se añaden estados 05/06, tratamientos ni diagnósticos.

## Evidencia contrastada y límites

| Referencia primaria | Hallazgo pertinente y uso en este caso |
| --- | --- |
| [ERC First Aid 2025](https://www.erc.edu/media/i2vllpae/gl2025-12-faid-e.pdf), introducción, tabla 1, expectativas y valoración inicial | Prioriza seguridad, ayuda precoz y actuación dentro de la formación. Incluye respuesta, respiración y vigilancia del deterioro. El presíncope quedó fuera del alcance específico de 2025; su omisión no demuestra ineficacia de las medidas previas. No presentar una secuencia propia de CASE 01 como algoritmo oficial ERC. |
| [AHA/American Red Cross First Aid 2024](https://cpr.heart.org/en/resuscitation-science/2024-first-aid-guidelines), §§4.2–4.3, 5.1 y 6.8 | En presíncope vasovagal/ortostático contempla posición segura, sentada con asistencia o acostada. Indica activar emergencias ante síncope, empeoramiento, recurrencia o ausencia de mejoría en 1–2 minutos; este último criterio es 2b/C-EO. No obliga a esperar si ya se necesita ayuda. Las contramaniobras tienen condiciones y no se recomiendan con síntomas de infarto/ictus. El primer episodio de Daniel no demuestra una etiología. |
| [ESC, guía de síncope de 2018](https://www.escardio.org/guidelines/clinical-practice-guidelines/all-esc-practice-guidelines/syncope/) y [calendario de guías](https://www.escardio.org/guidelines/clinical-practice-guidelines/guidelines-development/guidelines-publication-schedule/) | El catálogo sigue identificando la guía de 2018; la siguiente sobre síncope y trastornos autonómicos cardiovasculares está prevista para 2027. Un seminario de 2025 no equivale a una nueva guía. Se conserva la distinción narrativa durante/después del ejercicio de la especificación; no se deriva un diagnóstico por esa respuesta. El texto completo de OUP no fue accesible en esta consulta. |
| [Administración General del Estado: números de urgencia](https://administracion.gob.es/tu-espacio-europeo/derechos-obligaciones/ciudadanos/asistencia-sanitaria/numeros-urgencia), actualizada 27-07-2026; [SUMMA 112: contactar](https://www.comunidad.madrid/hospital/summa112/nosotros/contactar) | España utiliza el 112; la gestión corresponde a servicios autonómicos. SUMMA solicita lugar, descripción, número de afectados y contacto, además de atender instrucciones y no finalizar prematuramente la conversación. En el producto, localización y contacto serán datos ficticios del escenario. |
| [SAMUR–Protección Civil, guía de primeros auxilios](https://www.madrid.es/UnidadesDescentralizadas/Emergencias/Samur-PCivil/Samur/ApartadosSecciones/09_QueHacerEnEmergencias/Ficheros/Guia_PrimerosAuxilios_SAMUR.pdf), «Solicitud de ayuda al 112» y «Cómo hacer la transferencia» | Propone comunicar cambios, facilitar la localización a los equipos y transmitir la información recabada al relevo. Sirve para diseñar la conversación, sin fijar un tiempo de llegada ni reproducir un protocolo interno del operador. |

La adaptación siguiente es **diseño de interacción y evaluación formativa del proyecto**, trazable a O1–O5 y R01–R20 de la especificación. Ninguna de estas fuentes valida las cifras, interpolaciones, animaciones o umbrales de evaluación del JSON.

## Contrato observable de las acciones

| Acción del alumno | Evidencia mínima registrable | Comportamiento del producto |
| --- | --- | --- |
| Acercarse y establecer contacto | Intención dirigida a Daniel y respuesta efectivamente presentada. | La voz, mirada, subtítulos y registro corresponden al mismo estado. Abrir una ficha no equivale a valorar consciencia. |
| Valorar respuesta | Resultado observado, momento y procedencia de la interacción. | Daniel continúa consciente en todo el núcleo actual. No agregar pérdida de consciencia por inactividad ni mostrar una escala profesional como tarea I0. |
| Observar respiración | Observación explícita y resultado accesible mediante cuerpo/audio/texto descriptivo. | No revelar SpO2 ni FR numérica como si se hubieran medido. La accesibilidad conserva la misma información pertinente y registra la ayuda técnica. |
| Mantener apoyo seguro | Situación efectiva estable, aceptación de ayuda y continuidad del acompañamiento o colaborador. | Es una alternativa válida de OBJ_02. No exigir mover de nuevo a quien ya está protegido. Esta opción no dispara artificialmente la curva de supino. |
| Ayudar a posición supina | Cooperación, transición completada y postura efectiva confirmada. | Solicitud y resultado son eventos diferentes. Cancelar o fallar la transición no concede el resultado ni cambia el estado clínico. No convertir la pantalla de confirmación en prueba de destreza manual real. |
| Obtener información pertinente | Preguntas y respuestas recibidas sobre inicio y datos de alarma disponibles en el guion. | La entrevista puede interrumpirse para ayudar. O3 no exige completar un cuestionario entero ni inferir una etiología. Las respuestas sobre síntomas actuales se ajustan a mejoría, persistencia o recurrencia. |
| Reevaluar | Nueva observación posterior, contexto y comparación con lo ya conocido. | Preguntar cómo está, comprobar respuesta y observar respiración pueden repetirse. Una reevaluación no adquiere retrospectivamente información omitida ni recompensa clics duplicados. |
| Pedir o delegar ayuda | Destinatario, solicitud, respuesta y confirmación independiente. | Disponible desde el inicio. Distinguir colaborador del centro, comunicación simulada con 112 y llegada de relevo. Delegar no equivale a abandonar. |
| Comunicar el relevo | Receptor disponible, información realmente conocida y aceptación del receptor. | Admitir «no lo he comprobado». Una solicitud no genera por sí sola `HandoverCompleted`. No completar el informe con constantes internas desconocidas. |

El apoyo estable debe poder realizarse tanto en Desktop como en XR. Un control equivalente puede representar una intención que el hardware no reconoce con fiabilidad. La interfaz debe identificar qué se demostró en la simulación y qué técnica física continúa pendiente de observación por el instructor.

Supino no significa obligación universal de acostar a cualquier persona mareada. El caso conserva su contexto aprobado, con Daniel colaborador y sin trauma/disnea representados. Cualquier extensión a esos contextos exige sus propias guardas y revisión. Elevar piernas, contramaniobras, ingesta o tratamientos no son requisitos del cierre I0.

## Recorrido y finales

1. El briefing presenta **malestar tras ejercicio**, rol I0 y medios de ayuda. No anticipa un diagnóstico que el alumno deba adivinar. Se explica que las comunicaciones son simuladas.
2. El alumno obtiene información, organiza protección y solicita ayuda en un orden compatible con la situación. Puede mantener apoyo mientras prepara asistencia. El guion no exige instrumentos ni todas las preguntas antes de avisar.
3. El motor conserva las evoluciones 00–04 existentes. Los tiempos de interpolación no se convierten en cuenta atrás para pedir ayuda. La persistencia o recurrencia expresada debe estar disponible para ser reconocida, sin activarla como castigo por usar menús.
4. Durante la espera se ofrece una oportunidad real de reevaluar. Si hay recurrencia, el informe distingue la primera respuesta de la actuación tras el nuevo síntoma; un éxito anterior no resuelve automáticamente esa nueva oportunidad.
5. Un receptor acepta explícitamente el relevo. **07 hereda la situación del paciente**: completar el encuentro no borra síntomas ni significa alta para volver al ejercicio.

Debe existir un final normal «**Relevo completado**» y un final pedagógico «**Sesión finalizada antes del relevo**» para abandono del intento o cierre del instructor. Ambos abren el debriefing y permiten repetir. El segundo no finge un traspaso asistencial ni una muerte. No bloquear el final o la reflexión hasta que el alumno consiga todas las casillas: las omisiones son precisamente evidencia que revisar.

El final normal puede alcanzarse con objetivos todavía no demostrados si el receptor acepta la información disponible; registrar ese resultado sin inventar una observación. Una intervención del instructor se registra como tal. La mejoría fisiológica es independiente de la completitud educativa.

## Debriefing formativo

Mostrar primero el tipo de cierre, qué ocurrió, qué pudo observarse y qué se comunicó. Por objetivo utilizar **observado autónomamente / logrado con ayuda / no demostrado / no evaluable**; no porcentaje, nota de aprobado ni competencia sanitaria certificada.

| Objetivo | Evidencia que el debriefing debe poder explicar |
| --- | --- |
| OBJ_01 | Respuesta y respiración efectivamente observadas; distinguir dato disponible, dato explorado y dato omitido. |
| OBJ_02 | Apoyo o postura efectiva, alternativa elegida y posibles interrupciones. Una solicitud de asistencia incompleta no demuestra protección. |
| OBJ_03 | Historia obtenida y límites de lo conocido. No atribuir un diagnóstico a una pregunta sobre alimentación o ejercicio. |
| OBJ_04 | Qué cambió entre observaciones; si el alumno detectó mejoría, persistencia o recurrencia presentada. |
| OBJ_05 | A quién se avisó, qué quedó confirmado, qué se transmitió y quién aceptó el relevo. |

Para cada oportunidad guardar hora activa, estado, señal presentada, acción, resultado y ayudas. Mostrar fallos técnicos aparte; un fallo de tracking, audio o registro puede hacer una observación no evaluable. No asignar errores por ramas que nunca ocurrieron. La instrumentación avanzada queda fuera de I0, sin aparecer como omisión.

Propuestas de reflexión del facilitador: «¿Qué te hizo pedir ayuda?», «¿Qué cambio encontraste al reevaluar?» y «¿Qué información te faltaba cuando entregaste el relevo?». Seleccionar dos o tres decisiones respaldadas por la timeline, no reproducir una reprimenda genérica. La evidencia puede exportarse con versión del caso, perfil, modo, ayudas y estado de revisión clínica.

## Qué puede quedar terminado y qué sigue requiriendo revisión

**Cierre técnico del prototipo:** todas las acciones I0 son accesibles; las consecuencias y el relato coinciden; ayuda y relevo tienen respuesta; existen ambos finales; debriefing y reinicio funcionan; se conservan las curvas autorizadas. Esto es comprobable mediante pruebas del producto.

**`clinicalReviewRequired: true`:** continúa aplicándose al contenido clínico del caso y a las reglas del JSON. Consultar fuentes o pasar pruebas técnicas no cambia esa condición. La selección de I0 resuelve el alcance funcional para este trabajo, pero no identifica por sí sola al revisor ni acredita al alumno.

Antes de etiquetar el contenido como clínicamente validado, el responsable sanitario debe revisar y dejar constancia de versión, identidad, fecha y decisión sobre: narrativa y evolución de Daniel (BC-02/03), asistencia y alternativas representadas (BC-05), criterios de observación/debriefing y ayudas (BC-07), concordancia audiovisual y accesibilidad (BC-08), protocolo del centro y responsabilidades (BC-09). BC-04 permanece excluido; BC-06 no bloquea el cierre de I0 porque no utiliza instrumental.

No sustituir `clinicalReviewRequired` por una marca falsa de validación ni impedir las tareas técnicas ya autorizadas por ese pendiente. La revisión se refiere a publicación como contenido validado, no a si el prototipo puede terminarse y revisarse.

## Comprobaciones concretas de aceptación

- Mantener apoyo permite demostrar OBJ_02 y alcanzar relevo sin forzar la transición supina ni alterar sus curvas.
- La asistencia cancelada no concede postura, protección ni mejoría.
- Avisar al inicio y delegar con confirmación son rutas accesibles, sin requisito de instrumentos o entrevista completa.
- Respuesta, respiración y estado actual pueden reevaluarse; la segunda observación tiene hora propia.
- Síntomas, diálogo, cuerpo y debriefing permanecen coherentes en 00, 01, 02, 03 y 04.
- Ayuda solicitada, ayuda confirmada, receptor disponible y relevo aceptado son hechos distinguibles.
- El relevo no exige desaparición del mareo ni una cifra oculta, y conserva el estado previo en 07.
- Cerrar anticipadamente permite debriefing honesto sin declarar relevo completado.
- Pausa, reinicio y cambio de modo no conservan observaciones, ayuda o aceptación de un intento anterior.
- El resultado identifica I0, versión y `clinicalReviewRequired`; no muestra una nota clínica global ni certificación.

Estas son condiciones de aceptación propuestas; este documento **no afirma que se hayan ejecutado ni superado** las pruebas de Unity o del visor.
