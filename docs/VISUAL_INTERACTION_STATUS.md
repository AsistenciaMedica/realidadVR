# Vital VR — estado visual y de interacción

Entrega local del 13 de septiembre de 2026, validada mediante pruebas automatizadas.
Este documento distingue implementación, calidad visual y validación. Core,
EditMode, PlayMode, web y **smoke del ejecutable Windows final pasaron**; las siete
capturas se generaron desde ese player.
No se ha validado manualmente en Windows ni en Quest. El inventario médico conserva 44 variantes
`CLIENT_REVIEW` y cuatro entornos reutilizables; el cambio de presentación no
aprueba protocolos ni convierte la interacción virtual en una medición clínica.

`IMPLEMENTED`: existe una implementación reutilizable. `IMPROVED`: mejora de la
base existente. `PLACEHOLDER`: representación provisional. `NEEDS_ASSET`: falta
un recurso artístico. `NEEDS_MEDICAL_VALIDATION`: contenido/tolerancias pendientes
de revisión. Ninguna de estas etiquetas sustituye una prueba en dispositivo.

## Auditoría y reutilización

La escena original usa Bootstrap, TrainingRoom regenerable, un paciente de
primitivas con interacción XR y un catálogo médico compartido por Windows/VR.
La sala y los equipos hospitalarios existentes se conservan. Los nuevos módulos
se superponen en ejecución y restauran la sala original al cambiar de contexto.
No se crean motores médicos específicos de gimnasio, fútbol, dental o centro
comercial, ni se añaden packages ni servicios remotos.

El límite visual principal sigue siendo el humano: se sustituye la cápsula visible
por un proxy anatómico procedural con cabeza, ojos, extremidades, dedos y ropa.
**Sigue pareciendo una representación provisional; no es un personaje humano
profesional ni un asset fotorealista terminado.** Los colliders y la interacción
del paciente original se conservan; siguen la postura asistida del proxy.

## Paciente, animación y materiales

| Área | Estado | Implementación y límite |
| --- | --- | --- |
| Patient model | IMPROVED / PLACEHOLDER / NEEDS_ASSET | Proxy anatómico reutilizable. Falta humano profesional masculino/femenino, distintas edades y atleta con rig, materiales y LOD revisados. |
| Arquitectura visual | IMPLEMENTED | `PatientVisualState.FromSnapshot` y `PatientVisualController` traducen el snapshot médico a presentación. `PatientRigAdapter` permite sustituir el modelo sin reescribir escenarios. |
| Respiración | IMPLEMENTED | Excursión de tórax y abdomen: normal, rápida, lenta, superficial, laboriosa, agónica irregular y ausente. Sigue frecuencia y estado del caso; el movimiento no diagnostica por sí mismo. |
| Conciencia y mirada | IMPLEMENTED | Alerta, confusión, somnolencia e inconsciencia regulan mirada, cabeza y párpados. Transiciones interpoladas; sin seguimiento del usuario cuando no responde. |
| Facial | IMPLEMENTED / NEEDS_ASSET | Parpadeo, ojos y mandíbula básicos en proxy; adaptador para blink/jaw/pain/fear/distress en BlendShapes. Expresiones finas y labios naturales necesitan un rostro y animaciones preparados. |
| Coloración y signos | IMPLEMENTED / PLACEHOLDER | Variación de palidez, cianosis, rubor y signos a partir del estado. Representación moderada con propiedades por renderer; no un diagnóstico por color. |
| Posturas | IMPLEMENTED / PLACEHOLDER | Supino, recuperación, sentado y de pie mediante pivotes y extremidades asistidas. No equivalen a una biblioteca de animaciones clínicas capturadas. |
| Ragdoll | IMPLEMENTED ADAPTER / NEEDS_ASSET | Adaptador opcional verifica cuerpos/joints conectados, limita velocidad, asienta y retorna a pose. El proxy no activa extremidades físicas sueltas. Falta ragdoll de un humano final configurado y probado. |
| Piel, ropa, ojos | IMPROVED / NEEDS_ASSET | Materiales URP diferenciados y brillo controlado. Faltan texturas de piel/ropa, normal maps y detalles artísticos aprobados; no se declara piel final. |
| Deterioro/recuperación | IMPLEMENTED | Cambios de presentación siguen el estado clínico configurado. Una animación de recuperación o un asentamiento físico no generan ROSC ni otro desenlace por cuenta propia. |

Archivos: `Scripts/Patient/Presentation/PatientVisualController.cs`,
`PatientVisualState.cs`, `PatientRigAdapter.cs`, `PatientAnatomicalProxy.cs` y
`PatientControlledRagdoll.cs`, bajo `Assets/_Project/`.

## RCP, DEA y equipo médico

| Área | Estado | Qué se registra y qué falta |
| --- | --- | --- |
| RCP | IMPLEMENTED / NEEDS_MEDICAL_VALIDATION | Input Windows por arrastre de ratón e input XR por recorrido de mandos; mueve tórax y muestra manos asistidas. Cuenta ciclos, recorrido virtual, ritmo, liberaciones y colocación. |
| Calibración RCP | PENDING | `calibratedMannequin=false`. Los centímetros representan movimiento virtual, no profundidad sobre tórax real ni fuerza aplicada. Ratón no acredita postura de dos manos; mandos no sustituyen un maniquí instrumentado. |
| Evaluación RCP | IMPLEMENTED FOUNDATION | Métricas separadas en `ProcedureMetrics` junto al debrief. El motor conserva las reglas de cada caso; registrar métricas no valida automáticamente umbrales, técnica ni competencia clínica. |
| DEA | IMPLEMENTED / NEEDS_MEDICAL_VALIDATION | Abrir/encender, retirar respaldos, colocar ambos parches por zona/orientación, análisis y rama descarga/no descarga del caso. Tapa, pantalla, cables y reacción visual a descarga simulada. |
| Seguridad DEA | IMPLEMENTED FOUNDATION / PENDING DEVICE CHECK | Máquina de estados y contadores de intentos inseguros. No equivale a detectar cualquier contacto humano externo al tracking; necesita pruebas de contacto, retirada de manos y reanálisis con mandos reales. |
| Tensiómetro | IMPLEMENTED / PLACEHOLDER | Preparación, colocación virtual en brazo y espera de lectura del snapshot. No se simulan todos los pasos de una medición auscultatoria. |
| Pulsioxímetro | IMPLEMENTED / PLACEHOLDER | Abrir clip, colocar en dedo y mostrar SpO2 del caso. No lee sensor ni diagnostica perfusión real. |
| Glucómetro | IMPLEMENTED / PLACEHOLDER | Tira y contacto virtual con dedo, espera y lectura ficticia. No hay punción, muestra real ni prescripción libre. |
| Teléfono/vía aérea | IMPLEMENTED FOUNDATION | Objeto para solicitud de ayuda simulada y contacto del mentón que representa inclinación. Técnica, anatomía y accesibilidad necesitan revisión. |
| Autoinyector/vendaje | PLACEHOLDER / NEEDS_MEDICAL_VALIDATION | Props de entrenamiento y registro de acciones compatibles. No anunciar técnica anatómica validada ni dosificación; integración de anclajes en revisión. |
| Manos | IMPROVED / PLACEHOLDER / NEEDS_ASSET | Manos de compresión simplificadas y anclajes de equipo. No se implementa una mano anatómica completa con IK, dedos físicos o rechazo de toda penetración. |
| Háptica | IMPLEMENTED / QUEST PENDING | Impulsos solicitados a dispositivos XR que soporten la capacidad; sin hardware no se confirma sensación, amplitud ni comodidad. |

`MedicalInteractionMetrics.cs` conserva configuración, evaluador y estados DEA en
C# separado de Unity. `Scripts/Medical/Interaction/` conecta herramientas,
presentación y proveedores de input al motor existente. La exportación incorpora
datos de procedimiento sin sustituir el resultado médico. Las constantes mostradas
siguen siendo ficticias y procedentes de `MedicalScenarioDefinition`.

## Audio e inmersión

**IMPLEMENTED FOUNDATION / PLACEHOLDER.** `MedicalProcedureAudio` genera señales
de equipo, ruido respiratorio y ambiente de ventilación, con fuentes espaciales
para paciente/equipo. La respiración ausente detiene su fuente. No hay todavía
voces profesionales, diálogo grabado de pacientes/testigos, locución real de DEA,
mezcla por ambiente ni respiración agónica grabada y sincronizada de forma fina.
No presentar sonidos sintetizados como grabaciones clínicas realistas.

## Cuatro entornos

| Entorno | Estado | Mejora procedural presente |
| --- | --- | --- |
| Gimnasio | IMPROVED / PROCEDURAL | Banco, pesas, rack, cinta, suelo de entrenamiento, señalización y zona de hidratación. Equipos y materiales siguen sujetos a arte final. |
| Campo de fútbol | IMPROVED / PROCEDURAL | Exterior con líneas, porterías y red estática, banquillo, conos, balones, perímetro y gradas simples. Sin público animado ni césped complejo. |
| Centro comercial | IMPROVED / PROCEDURAL | Segmento de galería con escaparates opacos, bancos, vegetación, papelera y orientación. No es un centro comercial completo con tiendas navegables. |
| Clínica dental | IMPROVED / PROCEDURAL | Sillón, lámpara articulada, lavabo y almacenamiento. Faltan equipo dental artístico final y detalle de instrumentos específicos. |

`ScenarioEnvironmentPresenter` y `Environment/Presentation/` reutilizan geometría,
materiales opacos y luz principal. Paneles emisivos sugieren luminarias sin una
luz realtime por prop; espejos/vidrios estilizados evitan cámaras adicionales.
Los módulos se cachean y alternan: no hay cuatro simulaciones médicas paralelas.
Estas decisiones favorecen Quest, pero no demuestran por sí solas el rendimiento.

## Assets que faltan

| Necesidad | Integración y especificaciones propuestas |
| --- | --- |
| Humano masculino y femenino adulto | Licencia comercial y redistribución en build; Unity Humanoid válido, ojos/mandíbula y BlendShapes faciales; tórax deformable independiente, postura supina compatible, LOD y materiales URP. Configurar `PatientRigAdapter`, puntos anatómicos y ragdoll antes de sustituir el proxy. |
| Animaciones | Clips compatibles con el mismo avatar: consciencia, dolor, disnea, recuperación, convulsión y caída controlada. Revisión clínica de posturas y transiciones. |
| Manos | Rig con dedos y agarres coherentes con los mandos existentes; poses de compresión, DEA e instrumentos. Mantener separada la interacción de la representación. |
| Audio | Clips con licencia, voz española y consentimiento de intérpretes; paciente, testigo, DEA y ambientes. Integrar en la capa de audio sin modificar desenlaces. |
| Equipo final | DEA, sillón dental e instrumentos con escala métrica y colisiones sencillas. Sustituir superficies del prop preservando su componente funcional y anclajes. |

No se ha seleccionado ni comprado un asset externo; no hay presupuesto fiable
sin proveedor, licencia y calidad aprobados. Los límites de polígonos, atlas,
materiales y audio deben acordarse tras medir una escena en Quest 3.

## Entrega local validada

| Comprobación | Resultado | Evidencia |
| --- | --- | --- |
| Core | 27/27 PASS | `TestResults/core-tests.txt` |
| Unity EditMode | 121/121 PASS | `TestResults/visual-editmode.xml` |
| Unity PlayMode | 4/4 PASS | `TestResults/visual-playmode.xml` |
| Portal tests | 16/16 PASS | `TestResults/visual-web-tests.txt` |
| Descarga Windows completa | PASS; HEAD 200, rango 206 y SHA256 coincidente | `TestResults/visual-download.json` |
| Build web frontend/backend | PASS | Comando `npm --prefix demo run build` |
| Build Windows | PASS | `Builds/Windows/20260913-100825/EmergencyVR.exe`; `TestResults/visual-windows-build.log` |
| Smoke ejecutable Windows | PASS; proceso finalizado | `TestResults/visual-desktop-player.log`: `DESKTOP_SMOKE PASS` |
| Capturas de integración | 7 PNG generadas desde el player | [Galería](screenshots/README.md) |
| Validación manual Windows / Quest | PENDING | No realizada en esta revisión |

Los cuatro PlayMode cubren la secuencia técnica original, selección de los
cuatro entornos, RCP/DEA con guardas de contacto y exportación, y lectura de glucosa,
retirada/reagarre, gravedad, avance de 600 segundos de simulación y reinicio.
Son pruebas automatizadas del comportamiento; ese avance temporal no representa
diez minutos de estabilidad medida en un visor ni una revisión manual de ergonomía.
Las 44 variantes del catálogo siguen `CLIENT_REVIEW`; la cantidad de tests no es
el número de casos médicos ni una aprobación clínica.

El ejecutable final recorrió 45 secuencias de referencia (una técnica y 44 médicas)
y comprobó componentes de RCP/DEA, guardas de contacto y exportación JSON. El
smoke usa automatización, no una persona operando teclado/ratón ni mandos. Las
capturas son renders reales del proyecto: no imágenes conceptuales ni evidencia
de que el humano provisional ya tenga calidad comercial.

El diff final de `ProjectSettings`, configuración XR, `Packages` y assets de
Settings está vacío: esta iteración conserva esos ajustes. Esta comprobación no
demuestra rendimiento ni funcionamiento físico del visor.

La verificación de navegador comercial/admin está documentada en
[WEB_ADMIN.md](WEB_ADMIN.md). La [galería](screenshots/README.md) contiene gimnasio,
fútbol, centro comercial, clínica dental, RCP, DEA y primer plano del paciente.
La revisión humana de ergonomía y flujo por entorno sigue pendiente.

**QUEST PERFORMANCE: PENDING.** Falta medir CPU/GPU, memoria, draw calls, geometría
visible, estabilidad de frame, coste de transparencias/audio, comodidad y mandos
en Meta Quest 3. No se declara un objetivo de FPS cumplido ni una APK validada.

Entrega local **0.4.0-visual**: [ZIP Windows](../demo/releases/EmergencyVR-Windows.zip)
(39 975 284 bytes). SHA256:
`03001a9688c047ede6058e7598796f6ae0065b316f4256b3df215243ff200355`.
Portal con la misma descarga preparado en `Builds/RailwayDemo/20260913-051101/`;
índice regenerable: `Builds/client-demo-release.json`. No está publicado en Railway.

**NEXT STEP:** sustituir el humano provisional por un asset Humanoid preparado,
validar anatomía, anclajes y técnica con un responsable médico, y probar interacción,
háptica, comodidad y rendimiento en Meta Quest 3. La entrega local no implica
publicación Railway, APK validada ni certificación clínica.
