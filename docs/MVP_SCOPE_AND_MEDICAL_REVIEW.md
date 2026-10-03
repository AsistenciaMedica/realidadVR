# Alcance MVP y revisión clínica — 2026-09-13

> Registro histórico de los pilotos. Desde el 30 de septiembre de 2026, el alcance
> de lanzamiento es [3 escenarios × 5 casos](LAUNCH_SCOPE.md): gimnasio, centro
> comercial y campo de fútbol. La matriz siguiente refleja el estado de la fecha
> original, no el estado de implementación actual.

La petición vigente autoriza avanzar hacia demo Windows + portal Railway y
consultar fuentes médicas para crear pilotos revisables. Este documento sustituye
las restricciones históricas de fase 1 descritas en DEVELOPMENT_PLAN, sin declarar
terminados los escenarios o procedimientos aún pendientes.

## Matriz de entrega

| Elemento contratado | Implementación verificable | Trabajo pendiente |
|---|---|---|
| Gimnasio | Sin escena | Arquitectura, equipos, accesibilidad, audio y validación |
| Centro comercial | Sin escena | Zonas, circulación, recursos de emergencia |
| Clínica dental | Sin escena | Gabinete dental y mecánicas específicas |
| Campo de fútbol | Sin escena | Entorno deportivo y respuesta en exterior |
| Base de entrenamiento | TrainingRoom procedural, paciente, XR, locomoción | Personaje anatómico y revisión visual adicional |
| Valoración inicial | Selección de paciente y decisiones por botones | Exploración, signos observables, ramificaciones |
| RCP | Acción declarativa de inicio en piloto | Ritmo, profundidad, retroceso, ventilación y calidad física |
| DEA | Equipo visual; rama guiada sin descarga | Electrodos interactivos, aislamiento, análisis/ciclos y múltiples descargas |
| TA / SpO2 / glucemia | Lecturas ficticias en guion | Instrumentos, colocación y valores conectados al paciente |
| Desvanecimiento | Piloto de adulto que recuperó respuesta | Variantes, animaciones, aprobación clínica |
| Inconsciente con respiración | Piloto sin trauma, respiración normal | Vigilancia dinámica, deterioro, posición interactiva |
| Hipoglucemia | Piloto consciente que puede tragar | Variantes con alteración de consciencia y decisiones ramificadas |
| Hipotensión | Piloto de hallazgos/síntomas, causa no inferida | Diagnósticos diferenciales y conductas aprobadas |
| Parada respiratoria | Aproximación BLS de lego a ausencia de respiración normal | Rama aislada con pulso para personal entrenado |
| Desenlaces | Demo técnica cambia estados; pilotos conservan estado | Recuperación completa/verbal/inconsciente, múltiples descargas |
| Evaluación | CaseSession: secuencia, tiempo, errores, omisiones; JSON | Ponderaciones clínicas y evaluación de destrezas |
| Distribución | Adaptador PC, scripts de build y portal | Hosting vinculado, aceptación cliente, pruebas Quest |
| Meta Horizon Store | Base técnica XR | Candidata, privacidad, fichas, validación y envío |

No se presenta TrainingRoom como sustituto de los cuatro escenarios del contrato.
No se calcula un porcentaje contractual a partir de cantidad de botones o assets.

## Fuentes y decisiones de implementación

Los cinco guiones son adultos, educativos y provisionales. El número local de
emergencias debe acordarse según país objetivo; el software dice “activar
emergencias” y no adopta automáticamente el número británico de las fuentes.

- **BLS y DEA:** [RCUK 2025](https://www.resus.org.uk/professional-library/2025-resuscitation-guidelines/adult-basic-life-support-guidelines).
  El piloto distingue respuesta/respiración y solicitud de ayuda. La rama sin
  respiración normal es una sospecha de parada cardíaca para lego; no diagnostica
  parada respiratoria aislada ni decide descargar por ausencia de respiración.
  El DEA del guion indica no descargar y continuar RCP. Los clics no miden compresiones.
- **Inconsciencia con respiración:** [RCUK First Aid 2025](https://www.resus.org.uk/professional-library/2025-resuscitation-guidelines/first-aid-guidelines).
  El caso excluye trauma y respiración agónica; contempla posición lateral y
  observación. El paciente visual todavía no se recoloca mediante la acción.
- **Desvanecimiento:** [NHS Fainting](https://www.nhs.uk/symptoms/fainting/), revisión 17-08-2026.
  Se fija un adulto ya reactivo, respiración normal y sin lesión. No generalizar
  el guion a colapso durante ejercicio, síntomas persistentes o trauma.
- **Hipoglucemia:** [NHS Low blood sugar](https://www.nhs.uk/conditions/low-blood-sugar-hypoglycaemia/),
  última revisión indicada 03-08-2023; fuente con revisión prevista vencida,
  contrastada con primeros auxilios RCUK 2025. El piloto usa deglución segura,
  azúcar oral y nueva lectura ficticia tras espera abreviada. No dar productos
  orales a una persona inconsciente. No se implementan glucagón ni dosis farmacológicas.
- **Hipotensión:** [NHS Low blood pressure](https://www.nhs.uk/conditions/low-blood-pressure-hypotension/)
  y [RCUK ABCDE](https://www.resus.org.uk/library/abcde-approach). Los números son
  datos ficticios de autoría, no recomendaciones terapéuticas. No se infiere la
  causa, se prescriben líquidos o se titula oxígeno desde una lectura aislada.

Los textos y secuencias son una adaptación de autoría para revisar el software,
no una transcripción ni una certificación. Las fuentes no respaldan la puntuación
de diez puntos por acción ni un orden rígido universal: son valores provisionales
para ejercitar el evaluador. No hay penalización por umbrales temporales clínicos.
Los tiempos de respuesta se registran; la espera abreviada de glucemia no es
evidencia de una espera real de 10–15 minutos.

## Próxima revisión con el cliente

Confirmar público objetivo (lego/profesional), país, edad del paciente, protocolo
adoptado, criterios de aprobación, alternativas aceptables y señales de deterioro.
Revisar cada acción/contraindicación, mediciones iniciales, efectos y desenlaces.
Separar acciones simultáneas válidas de errores reales antes de puntuar clínicamente.

La siguiente ampliación funcional debe implementar una mecánica completa
(por ejemplo, electrodos y análisis DEA) con datos aprobados y pruebas, no añadir
solo más botones. Después, trasladar esa mecánica a los cuatro escenarios.
