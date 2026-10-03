# Pacientes y realismo del lanzamiento

Actualizado: 1 de octubre de 2026. Público: **primeros intervinientes y brigadistas**.
Se mantiene el alcance de tres escenarios y cinco casos por escenario. Las
identidades son ficticias. El estado de revisión clínica sigue siendo
`CLIENT_REVIEW`; mejorar presentación y pruebas no equivale a aprobar el contenido.

## Identidad y presentación

Los quince casos tienen nombre, edad, papel en la escena, motivo de consulta,
antecedentes, testimonio y modelo propios. La [tabla de casos](SCENARIO_LIBRARY.md)
se genera junto con el portal. La ropa informa el contexto: hay deportistas,
visitantes, espectadores y personal del campo; no todos son jugadores entrenando.
Lucía y Ricardo llegan al gimnasio; Mateo todavía no ha empezado a entrenar.
En el campo, Andrés y Pablo son jugadores, Elena y Miguel son espectadores y
Sergio es organizador. Estos papeles se mantienen en los relatos y testimonios.

`Assets/_Project/Resources/PatientRoster.json` es la fuente de estas identidades.
`PatientRoster.Apply` las incorpora a la biblioteca después de cargar el caso V2.
La edad, el sexo y la postura inicial no cambian aleatoriamente entre intentos
del mismo personaje. Las demás variaciones del motor conservan su semilla.
La identidad acompaña al resultado, incluido el informe de Daniel V2.

Cada perfil referencia `Visual/Appearances/<id>`. Son quince mallas distintas,
no cambios de color del mismo modelo. Se conserva un único paciente físico,
su esqueleto y los puntos de contacto de RCP, DEA y mediciones. El cambio de
apariencia renueva los pesos faciales y propiedades visuales sin modificar
las poses de reposo del esqueleto. Solo se carga la apariencia seleccionada;
los recursos anteriores quedan disponibles para descarga cuando no hay referencias.

Las posturas sentadas se calibran una vez por malla sobre la suela real y el
suelo físico. El mobiliario se coloca bajo la pelvis cuando la pose termina de
asentarse y comprueba también ambos zapatos. Se retira al cambiar de postura o
perder respuesta, sin mover la raíz clínica ni modificar los anclajes de maniobras.
Daniel conserva su propio sistema de asistencia y apoyos.
Los gestos de dolor o dificultad respiratoria de los pacientes sentados usan
un objetivo frente al torso y un codo lateral, en lugar de direcciones fijas que
cruzaban el brazo. Se retiran durante compresiones y posturas tumbadas.

Los catorce nuevos modelos usan texturas de hasta 1024 píxeles, mipmaps y ASTC
6×6 en Android. El modelo de Daniel conserva sus materiales originales. Estos
límites reducen el coste previsto; el rendimiento todavía debe medirse en visor.

Los recursos proceden de [Microsoft Rocketbox](https://github.com/microsoft/Microsoft-Rocketbox),
commit `0943055db6ec570bcef9f2c8b41c9e5467c808f9`. El manifiesto
`Assets/ThirdParty/Rocketbox/Roster/sources.json` conserva procedencia y hashes.
La licencia MIT y su aviso se incluyen en `StreamingAssets/ThirdParty` para
acompañar las distribuciones. El script `tools/Acquire-PatientRoster.py` permite
reproducir la adquisición; el generador es `PatientRosterAssetBuilder.Build`.

## Conducta y aprendizaje

- Las fichas presentan personas y signos observables; el diagnóstico se muestra
  en el debrief, después del intento.
- Las preguntas sobre identidad, lo ocurrido, antecedentes y testigos usan el
  perfil del caso. Una persona sin respuesta no habla; la confusión y dificultad
  respiratoria limitan lo que puede contestar. Tras una evolución se muestra la
  observación actual, en lugar de repetir una queja inicial que ya no corresponda.
- La conversación respeta pausa y repetición. No acredita procedimientos ni
  modifica constantes, puntuación o evidencia clínica.
- Monitorizar y reevaluar pueden repetirse sin duplicar puntos, efectos clínicos
  o tiempos de eventos. Siguen comprobándose las condiciones de seguridad.
- Monitorizar no cancela por sí solo el deterioro respiratorio. Animar una tos
  eficaz no elimina automáticamente una obstrucción a los cinco segundos.

Daniel mantiene su conversación y audio existentes. Las nuevas conversaciones
son textuales; no se han producido quince voces grabadas ni animación labial
específica para ellas. Ese acabado sigue pendiente antes de anunciarlo en la ficha.

La revisión de primeros auxilios se contrastó con las guías oficiales de
[RCUK 2025: primeros auxilios](https://www.resus.org.uk/professional-library/2025-resuscitation-guidelines/first-aid-guidelines)
y [soporte vital básico](https://www.resus.org.uk/professional-library/2025-resuscitation-guidelines/adult-basic-life-support-guidelines).
La adecuación al protocolo local y al nivel de formación del comprador requiere
revisión clínica del producto; no se han añadido dosis ni intervenciones avanzadas.

## Verificación y distribución

Las suites `PatientRosterTests`, `PatientRosterVisualTests`,
`PatientConversationTests` y `ReleaseMedicalRealismTests` comprueban identidades,
recursos, conversación y rutas clínicas. Las suites PlayMode verifican los quince
cambios de apariencia sobre el mismo paciente, contactos, ciclo de vida y flujo UI.

El recorrido del ejecutable se activa con `-vital-patient-roster-smoke` y
`-vital-capture-directory <ruta>`. Guarda imágenes reales y un informe JSON;
las capturas deben inspeccionarse, especialmente postura, apoyo, pelo y materiales.
Este recorrido no sustituye la prueba de mandos, confort y rendimiento en Quest.

Datos y lógica: **263/263 EditMode** en
[patient-roster-edit-final.xml](../TestResults/patient-roster-edit-final.xml).
Funcionamiento: **49/49 PlayMode** en
[patient-roster-acceptance.xml](../TestResults/patient-roster-acceptance.xml),
incluidos los quince cambios de modelo, siete pacientes sentados con apoyo real,
gestos de dolor, compatibilidad V2 y movimiento de huesos con los clips compartidos.
Los informes de iteraciones anteriores se conservan en `TestResults`.

Ejecutable de Windows: [Builds/PatientRoster/VITAL-VR.exe](../Builds/PatientRoster/VITAL-VR.exe).
El recorrido final terminó en **PASS**: quince pacientes, quince modelos distintos,
siete apoyos sentados legacy verificados y **46 capturas PNG**. Incluye volver a
Daniel y a Lucía para comprobar los cambios de apariencia. El
[informe JSON](../TestResults/PatientRosterPreview/patient-roster-smoke.json)
registra las identidades, conversaciones y mediciones de apoyo; Daniel conserva
su comprobación de asistencia separada. La
[galería de capturas](../TestResults/PatientRosterPreview/index.html) permite revisar
los quince personajes y acceder a sus imágenes completas.

El [registro final del Player](../TestResults/patient-roster-player-final.log)
contiene `VITAL_PATIENT_ROSTER_SMOKE PASS` y cero avisos
`Non-Legacy animations cannot be sampled`. El muestreo prepara un `Animator` en
su destino real y conserva los clips compartidos con los acompañantes; las
pruebas comprueban movimiento de huesos, además de la ausencia del aviso.
Las vistas externas de la galería ocultan temporalmente manos, mangas y reloj
del rescatador para revisar el cuerpo, manteniendo paciente, equipo y mobiliario.
Durante la práctica se conserva la vista del rescatador.

Portal: **4/4 pruebas de catálogo**, **17/17 pruebas Node**, builds web correctos
y recorrido Edge de las quince rutas con recarga, identidad, Unicode y móvil.

Android: [APK de desarrollo](../Builds/Android/EmergencyVR-20261001-222416.apk)
generada correctamente el 1 de octubre de 2026; **201.027.070 bytes** (201 MB).
El archivo contiene únicamente ARM64, las quince apariencias y sus quince mallas
comprobadas por GUID, y el aviso Rocketbox idéntico al original. La firma APK v2
se verificó correctamente con certificado **Android Debug**. Usa el identificador
provisional `com.emergencyvr.trainingdemo`, versión `0.1.0`/código `1`, mínimo
SDK 32 y objetivo SDK 36; `debuggable=true`.

El [informe de comprobación del APK](../TestResults/patient-roster-apk-verification.json)
incluye tamaño, contenido y SHA-256
`bbaf1b03bd0a5fdc55152b40917f90a17e3a0b9487fdb9a49f81142cdba0fb68`.
Se conservan el [registro de compilación](../TestResults/patient-roster-android-build.log),
el [manifiesto inspeccionado](../TestResults/patient-roster-apk-badging.txt) y la
[verificación de firma](../TestResults/patient-roster-apk-signature.txt).

Esta evidencia corresponde al equipo de desarrollo, al ejecutable de Windows y
a la inspección del paquete Android. No había un visor conectado al comprobar
ADB. No acredita una prueba en Quest, una APK de producción ni preparación
completa para publicar. Continúan pendientes la revisión clínica del producto,
las voces adicionales y las comprobaciones físicas de mandos, confort y rendimiento.

El cierre para Meta continúa en [alcance de lanzamiento](LAUNCH_SCOPE.md).
