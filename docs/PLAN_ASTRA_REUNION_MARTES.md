# Plan de mejoras VITAL VR — auditoría del 2 de octubre de 2026

Destinatario: **Astra**, agente que ejecuta este plan sobre `proyectovr`.
Base: APK `VITAL-VR-0.1.0-1` (`com.vitalvr.training`, código 1) ya subida a Meta.
Reunión con el cliente: **martes 6 de octubre**.

Este plan sale de revisar las capturas de `TestResults/PatientRosterPreview/` y el
código. Cada tarea indica **qué está mal, dónde está y cómo se comprueba que quedó bien**.

## Diagnóstico en una frase

El contenido clínico y los 15 pacientes están bien encaminados, pero **en el visor
VITAL VR se comporta como una aplicación de escritorio metida en un panel flotante**:
las decisiones se toman pulsando botones, los escenarios están vacíos y el equipo
médico es el mismo carro de hospital en todos los entornos. Las mejoras de abajo
buscan que se sienta como **realidad virtual de verdad**.

## Reglas para Astra

1. **Antes de tocar nada**, commit de los ~298 cambios pendientes y push a `main`:
   `git -c credential.helper= -c credential.https://github.com.helper= -c credential.helper=manager push origin main`.
   Nada de ramas nuevas.
2. **No cerrar el Unity del usuario.** Compilar en la copia `C:\Users\Juan\vital-lab`
   (robocopy de `Assets`, `Packages`, `ProjectSettings`) y traer de vuelta solo el código fuente.
3. **Solo assets gratuitos y sin cuenta:** Rocketbox (MIT), Poly Haven (CC0), Kenney (CC0),
   Objaverse (CC-BY, con atribución en `StreamingAssets/ThirdParty`).
4. **No tocar el contenido clínico.** Constantes, reglas y curvas se quedan como están;
   `CLIENT_REVIEW` y `clinicalReviewRequired: true` también.
5. **Cada tarea termina con evidencia** (captura antes y después, prueba en verde o
   medición) guardada en `TestResults/astra/`. Si algo no se probó en el visor, se dice.
6. **Commit al terminar cada bloque** y regenerar las capturas con `-vital-patient-roster-smoke`.

---

## Bloque A — Errores visibles que el cliente notará (prioridad máxima, viernes)

Son arreglos pequeños con mucho impacto en la demo.

| # | Problema encontrado | Dónde | Qué hacer | Criterio de aceptación |
| --- | --- | --- | --- | --- |
| A1 | El debrief muestra IDs internos: «CheckSceneSafety: omisión crítica — Seguridad de escena» | `Scripts/Medical/MedicalScenarioRuntime.cs:177` | Mostrar solo la etiqueta legible («Seguridad de escena: no realizada») y mantener el ID únicamente en el JSON exportado | Ningún texto en pantalla contiene `Check…`, `Call…` ni otros IDs en camelCase |
| A2 | Si el paciente está inconsciente, cada pregunta añade otra vez «El paciente no responde a la pregunta» (3 veces seguidas en el caso de Andrés) | `Scripts/Dialogue/PatientConversationController.cs:78` | No repetir la misma observación seguida; desactivar las preguntas al paciente cuando no responde y destacar «Escuchar al testigo» | Tres preguntas al paciente inconsciente dejan una sola línea en el registro |
| A3 | El briefing deja marcado por defecto el modo «Evaluación» (sin pistas) | `Scripts/UI/TrainingExperience.Pages.cs` | Marcar por defecto «Práctica guiada» | Un usuario nuevo empieza en práctica guiada |
| A4 | El monitor muestra cinco casillas vacías (FC, SpO₂, TA, FR, glucemia) aunque no se haya usado ningún aparato | `Scripts/UI/TrainingExperience.Clinical.cs` | Mostrar cada lectura solo cuando se haya medido; antes, un mensaje corto («Aún no has medido constantes») | En la parada cardiaca sin mediciones no hay casillas vacías |
| A5 | La espectadora del campo (glucemia) se recuesta hacia atrás en un banco **sin respaldo**: parece que flota | Calibración de pose sentada (`PatientRoster` / `ArticulatedPatient.cs`) | Limitar la inclinación del tronco si el asiento no tiene respaldo, o usar un banco con respaldo | En la captura lateral el tronco queda apoyado o recto |
| A6 | Pacientes sentados con brazos rígidos y la mirada al techo (Lucía, la espectadora) | `ArticulatedPatient.cs` (objetivo de mirada) | Si el paciente está consciente, mirar al usuario; manos sobre los muslos | En las capturas de pacientes conscientes, la mirada apunta a la cámara |
| A7 | En el gimnasio la cinta de correr ocupa media pantalla en el punto de vista inicial, y junto al letrero «DEA» flota un texto ilegible | Gimnasio (`Editor/Environment/`) | Mover el punto de inicio o la cinta, y quitar o ampliar ese texto | La captura inicial no tiene objetos cortados en primer plano |
| A8 | La compilación de lanzamiento usa **versionCode 1 por defecto** si falta `VITAL_VERSION_CODE`, y «Configure Android» lo reinicia a 1 | `Editor/QuestProjectSetup.cs:39` y `:184` | Hacer obligatoria la variable y que sea mayor que la última subida; no reiniciar el código al configurar | Compilar sin la variable falla con un mensaje claro |

## Bloque B — Que se sienta realidad virtual (sábado y domingo)

Es la mejora de fondo. Hoy solo RCP, DEA, mediciones, teléfono, vía aérea, vendaje y
autoinyector son acciones físicas (`MedicalProcedureRig.cs:76`). **Comprobar la seguridad,
la respuesta y la respiración se hacen pulsando un botón**, que es justo lo que un
interviniente tiene que practicar con el cuerpo.

**B1. Acciones de valoración con gestos reales** (mantener los botones como alternativa
accesible, registrando que se usó la alternativa):
- **Seguridad de escena:** se registra al mirar alrededor (giro de cabeza de más de 120°
  en total) antes de acercarse.
- **Comprobar respuesta:** tocar los dos hombros del paciente con los mandos y hablarle
  (o pulsar «Hablar»). Vibración en ambos mandos al tocar.
- **Comprobar respiración:** acercar la cabeza a la cara y el pecho del paciente y
  mantenerla unos segundos (ver, oír y sentir), con indicador circular de progreso.
- **Llamar al 112:** el teléfono físico ya existe; hacer que sonar, hablar y colgar
  tengan voz y vibración.

Aceptación: una prueba PlayMode por gesto con entradas XR simuladas, y el debrief
distingue «hecho con gesto» de «hecho con botón».

**B2. Rediseñar la interfaz para VR.** Hoy se reutiliza la pantalla de escritorio de
1440×900 a escala 0,00165: un panel de **2,4 m de ancho a 2,2 m** de distancia
(`Scripts/UI/TrainingExperience.cs:120-134`).
- Menús (inicio, selección, briefing y debrief): panel de 1,2–1,4 m de ancho a 1,5 m, texto más grande.
- **Durante el caso no debe haber panel delante.** Ficha, preguntas y acciones van a una
  **tableta en la mano izquierda** (aparece al girar la muñeca). Las constantes se ven en
  la pantalla del propio aparato (pulsioxímetro, tensiómetro, glucómetro). El registro se
  muestra como subtítulos cerca del paciente.
- Aceptación: captura en VR del caso en curso en la que se ve al paciente sin ningún panel delante.

**B3. Manos visibles.** Comprobar si en la APK se ven manos o mandos (`ArticulatedHand.cs`).
Si solo se ven mandos, usar manos con poses de agarre y de compresión durante la RCP.

**B4. Vibración en toda la interacción.** Hoy solo vibra la RCP (`CPRInputProviders.cs:87`).
Añadir un pulso corto al agarrar o soltar, al colocar los parches, al recibir la descarga del DEA y al tocar al paciente.

## Bloque C — Escenarios creíbles (domingo)

| # | Problema | Qué hacer |
| --- | --- | --- |
| C1 | El mismo **carro de hospital con monitor «DEA»** aparece en el campo de fútbol y en el centro comercial; en la vida real eso no existe ahí | Por entorno: **DEA en vitrina de pared con señal verde** (centro comercial y gimnasio) y **maletín DEA** que trae un compañero o el organizador (campo). Botiquín en lugar del carro. Mantener los anclajes funcionales de `MedicalPhysicalTool` |
| C2 | El briefing dice «un compañero solicita ayuda», pero en el campo no hay nadie; el centro comercial está vacío | Añadir 2–4 personas de Rocketbox por escenario con animación de espera (compañeros, curiosos, personal del centro). Una de ellas es el testigo y habla |
| C3 | El cielo del campo es un color plano gris azulado y las gradas son bloques | Skybox HDRI de Poly Haven, sol direccional horneado, césped con textura y gradas con algo de público estático |
| C4 | Las superficies son planas en los tres entornos | Texturas de Poly Haven de 1K con compresión ASTC 6×6 (suelo de gimnasio, baldosa de centro comercial, césped) |
| C5 | El audio está sintetizado por código (tono de 800 Hz y ruido; `MedicalProcedureAudio.cs`) | Ambiente CC0 por escenario (murmullo del centro comercial, gimnasio, viento y público en el campo) y locución del DEA en español con su voz sintética |
| C6 | Solo Daniel tiene voz grabada (39 clips en `Resources/Audio/Case01`); los otros 14 casos son solo texto | Ampliar `tools/Generate-Case01Voices.ps1` a los 14 pacientes y testigos, con subtítulos. Mantener el aviso de voz sintética |

Aceptación del bloque C: capturas nuevas de los 15 casos en `TestResults/astra/` junto a las anteriores.

## Bloque D — Rendimiento en Quest 3 (en paralelo; obligatorio antes de subir)

No existe ninguna medición en el visor. El proyecto no tiene foveated rendering ni
lightmaps, y las capturas muestran sombras en tiempo real de todos los objetos.

1. Instalar en el Quest la APK actual y la nueva, y medir con OVR Metrics Tool o
   `adb logcat` 2 minutos por escenario. Objetivo: **72 FPS estables**. Guardar en `TestResults/astra/quest-perf.md`.
2. Hornear la iluminación de los tres entornos y usar Light Probes para los personajes;
   dejar sombras en tiempo real solo para el paciente.
3. Activar el foveated rendering fijo de Meta en OpenXR, en nivel medio.
4. Static batching del entorno e instancing en los objetos repetidos. Meta recomienda menos de 150 draw calls.
5. Si el usuario no puede ponerse el visor antes del lunes, dejar preparados los comandos
   y el informe, y decir con claridad que el rendimiento no está medido.

## Bloque E — Preparar la reunión y la subida (lunes)

1. **Recorrido de demo de 5 minutos:** Daniel (gimnasio, V2) y después Andrés (parada
   con DEA en el campo). Ensayarlo entero en la APK nueva.
2. **Vídeo de respaldo** de 3 minutos grabado desde el visor.
3. Documentar en `docs/CLIENT_DEMO.md` cómo transmitir la imagen del Quest a la TV o al portátil.
4. **APK 0.1.1, código 2,** firmada con `C:/Users/Juan/.vitalvr/vitalvr-release.keystore`.
   Ejecutar antes `node tools/Sync-MedicalPortal.mjs`, `node --test tools/ReleaseScope.test.mjs`,
   EditMode y PlayMode. Subirla al mismo canal de prueba de Meta; **la publicación pública la decide el usuario**.
5. Escribir una página de «Qué mejoró desde la versión 1» para el cliente, con capturas antes y después.

---

## Orden y recortes

Orden: **A → D1 (medir) → B1 → B2 → C1 → C2 → C3/C4 → C5/C6 → B3/B4 → D2–D4 → E**.

Si el tiempo no alcanza, recortar en este orden: C6, C5, B3, C4, C3.
**No recortar** el bloque A, B1, B2, C1, la medición en el visor ni la prueba de la APK final.

## Fuera de alcance para el martes (siguiente iteración)

- Seguimiento de manos sin mandos y reconocimiento de voz en el visor
  (`DesktopVoiceInput` usa el dictado de Windows y no funciona en Quest).
- Modo instructor y multijugador, informes en el portal y nuevos escenarios.
- Validación clínica por el responsable sanitario (depende del cliente, no de Astra).
