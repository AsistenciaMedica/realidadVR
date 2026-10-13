# Plan Astra — VITAL VR listo para el «wow» del martes

Destinatario: **Astra**, agente que ejecuta este plan sobre `proyectovr`.
Fecha: viernes 2 de octubre de 2026. Entrega: **lunes 5 por la noche**.

## Revisión de los tres entornos — entrega del 6 de octubre

Juan amplió la prioridad a los **tres entornos y los quince casos**, incluyendo la
landing. Esta sección sustituye las prioridades históricas de limitar el trabajo
al gimnasio que aparecen más abajo.

- Centro comercial: escaparates con profundidad, estantes y productos físicos,
  acabados de madera/piedra, columnas y luminarias de techo.
- Campo: bordes, drenaje, postes de iluminación y césped menos saturado.
- Conjunto: materiales a escala, luz sobre los personajes, sombras de contacto
  calculadas desde la postura y letreros que respetan la profundidad de la escena.
- Los quince pacientes conservan canales faciales adicionales cuando existen en
  el modelo original. Parpadeo menos regular, expresión de malestar y articulación
  de boca suavizada por audio. Daniel conserva su actuación más detallada; esto
  no equivale a extender esa actuación completa a los otros catorce pacientes.
- El ambiente baja durante las conversaciones y vuelve gradualmente después.
- La landing y las páginas de escenarios muestran capturas de la aplicación.
  Se comprobaron escritorio, móvil, los tres enlaces y los quince casos.

La galería `Builds/Reunion-20261006/Revision/ABRIR-REVISION.html` funciona sin red:
30 imágenes, filtros por entorno y vista general/cercana. Proceden de
`TestResults/reunion-20261005/final-v4`; son cámaras de inspección con interfaz y
manos del usuario ocultas. No son capturas dentro de unas Quest ni imágenes
generadas. La carpeta `before` de esta revisión se marcó como evidencia inválida
porque su primer capturador no cambiaba de caso correctamente; no se utiliza.

Se conservan los modelos Rocketbox actuales: **no se acredita fotorealismo ni se
han importado humanos escaneados**. La apariencia, los controles y la fluidez en
las Meta Quest del cliente necesitan una prueba en ese visor. No se ha subido
esta revisión a Meta. El contenido clínico continúa en revisión.

Preparación reproducible: `tools/Build-MeetingReview.mjs` genera la galería y las
imágenes de la landing; `tools/Check-MeetingWeb.js` verifica ambas en navegador.
`tools/Package-MeetingDelivery.ps1` exige APK firmada, evidencia de validación y
Windows de la misma revisión antes de generar el ZIP de entrega.

La medición del reproductor de Windows detectó 113 llamadas de dibujo en Daniel.
Se eliminó el pase de sombras dinámicas duplicado en la arquitectura horneada y
se agruparon las piezas inmóviles del banco, conservando sus colisiones. La
comprobación `allscenes-batched-final-playmode.xml` pasó en los quince casos:
máximo 93 llamadas, 166 611 triángulos y 230,5 MiB de texturas, con una luz en
tiempo real y MSAA 4×. Son presupuestos de carga en Windows; no FPS de Quest.

Validación final de esta revisión: **304/304 EditMode y 93/93 PlayMode**, sin
pruebas omitidas. `TestResults/astra/final-editmode.xml` y `final-playmode.xml`
contienen los resultados; `xr-walkthrough.xml` conserva los quince recorridos
XR aprobados extraídos de ese PlayMode, con su archivo de procedencia. La
huella de las fuentes validadas es
`9042b5e7828da066215cae553eaa9407b0154706f7ab9f0e60fbdd0c0fc53f80`.
La compilación web y la comprobación del navegador con las imágenes finales
también pasaron. Las evidencias fallidas anteriores se conservan por separado.
El ejecutable final de Windows completó además el recorrido de quince casos
(`TestResults/astra/meeting-windows-final/result.txt`: PASS), con capturas del
propio reproductor y salida normal. Las mallas faciales y los horneados finales
también están copiados al proyecto fuente.

Entrega local generada y verificada el 6 de octubre:

- APK `Builds/Astra/VITAL-VR-0.1.1-3.apk`: paquete `com.vitalvr.training`, versión
  `0.1.1`, código **3**, 164 117 672 bytes. Firma comparada con el certificado de
  la clave local existente. Evidencia final: `TestResults/astra/final-release.json`
  y `release-postflight.json`, con estado `EVIDENCE_VERIFIED`.
- Windows: `Builds/Astra/INICIAR-VITAL-VR.cmd` y su carpeta completa.
- Paquete de reunión: `Builds/VITAL-VR-Reunion-20261006-v3.zip`, preparado por
  `tools/Package-MeetingDelivery.ps1` con Windows, Android, galería y guía local.

La APK **no se ha subido a Meta**; se entrega para el panel que usa Juan. La
landing se compiló localmente, sin desplegarla. La prueba con Quest sigue pendiente.

## Revisión de actuación — 5 de octubre

Juan aclaró que el cliente es el dueño del proyecto y revisará los avances. La
build existente se subió mediante el panel web de Meta. La prioridad actual es
mejorar el realismo de los personajes antes de generar otra APK.

Se revisó primero Daniel y la compañera del gimnasio:

- Se conservaron siete canales faciales adicionales del modelo original de Daniel:
  articulación de labios, párpados y comisuras. El movimiento de labios responde al
  audio; no se presenta como reconocimiento exacto de fonemas.
- Se amplió la actuación capturada a clavículas y dedos, con transiciones suaves
  al repetir las tomas. Postura sentada asimétrica y gesto hacia la cara con contacto
  de dedos, recorrido curvo y acompañamiento del tronco.
- Se corrigió la orientación de cabeza/ojos y la atención de la compañera. Sus
  gestos usan un tramo expresivo de la animación existente.
- Se mantuvieron los pies apoyados después de la actuación: en este esqueleto las
  piernas son hijas de Spine y heredaban su movimiento. Se conserva la asistencia
  física y la presentación de estados sin respuesta.
- Se corrigió la restauración de la configuración gráfica del gimnasio cuando
  cambia el nivel de calidad antes de destruir la escena.

**Verificación:** 16/16 PlayMode en
`TestResults/astra/presence-final-playmode.xml` (actuación/apoyos/pausa,
animación en ejecución, integración clínica V2 y recurrencia). Es una selección
de pruebas correspondiente a estos cambios; no una nueva validación completa
de los quince casos. `DanielMesh.asset` actualizado en el proyecto fuente,
con el mismo GUID. No se reemplazaron los materiales locales del usuario.

**Revisión visual:** `TestResults/presence-20261005/index.html` compara los vídeos
`before.mp4` y `after.mp4`: 216 fotogramas reales por secuencia, a 12 pasos de
animación por segundo (18 s), sin audio, renderizados en Unity con perfil Quest
y cámara de inspección. No son imágenes generadas ni evidencia de FPS en visor.
Los vídeos y resultados están excluidos de Git por las reglas existentes.

Esta iteración conserva los modelos y texturas actuales y no acredita un acabado
fotorealista. La valoración estética del resultado sigue pendiente de Juan.
La landing, la nueva APK y la carga a Meta quedan para después de esta revisión,
según su última indicación. No se generó ni subió una APK en esta iteración.

## Prioridad actual — revisión visual del gimnasio (3 de octubre)

Por indicación de Juan, mejorar primero el gimnasio y Daniel; los demás escenarios
esperan su valoración en **Meta XR Simulator**. Juan realiza la prueba visual.
Esta revisión solo compila y hornea el gimnasio, sin ejecutar suites ni recorridos.

- Acabados diferenciados de caucho, madera, pared, acero y tapizado, con escala de textura corregida.
- Distribución despejada, ventanas con remates y detalles de uso; banco y colchoneta con bordes redondeados.
- Daniel apoya las manos en banco/muslo, suaviza la mirada y los movimientos e incorpora parpadeo irregular.
- Luz principal con sombras para personajes y horneado del gimnasio disponible también en el proyecto fuente.
- Se conserva el modelo humano actual y el contenido clínico. La aceptación del realismo queda pendiente de Juan.

Preparación reproducible: `tools/Prepare-GymVisualPreview.ps1`. Compila y hornea en
`C:\Users\Juan\vital-lab`, y copia el gimnasio y sus dependencias al proyecto fuente.
Evidencia local: `TestResults/gym-visual-revision/`. Las APK/ZIP anteriores no contienen esta revisión.
Compilación completada y horneado GPU finalizado a las 12:26 de Bogotá: 2 lightmaps,
67 superficies y 100 probes. Recursos copiados al proyecto.

### Actuación tras la prueba de Juan

Juan sigue percibiendo los personajes inmóviles y sin iniciativa. La siguiente revisión
se centra en su comportamiento en el primer gimnasio; la mejora de materiales no resuelve esa percepción.

- Daniel inicia un gesto a los 1,6 segundos, ajusta el apoyo, alterna miradas y reconoce el acercamiento.
  La actuación tiene reloj propio y funciona aunque falte un clip; respeta conciencia, pausa y asistencia.
- La compañera empieza a gesticular a los 3,4 segundos, mira al paciente y al usuario, parpadea
  y mueve la mandíbula con su audio existente.
- Mateo tiene otra secuencia de reposos, miradas y gestos, con los pies anclados.
- Los paramédicos diferencian la recepción del relevo y la atención visual al paciente.

Para esta iteración se usa `tools/Prepare-GymVisualPreview.ps1 -CompileOnly`: sin nuevo
horneado, pruebas automáticas ni recorrido en simulador. Log en `TestResults/gym-character-presence/`.
Compilación aislada completada sin errores. La evaluación visual y la aceptación siguen correspondiendo a Juan.

## La situación

- El martes 6 **el cliente se pondrá el Quest por primera vez**. Será la primera
  prueba en un visor real. Nadie del equipo tiene visor antes: **todo se verifica sin gafas**.
- Objetivo: que funcione de principio a fin a la primera y que el cliente diga «wow»
  en los primeros 30 segundos.
- Base: APK `VITAL-VR-0.1.0-1` (`com.vitalvr.training`, código 1) en el panel de Meta.

## Hallazgo principal: en el Quest se ve mucho peor que en las capturas

Las capturas de `TestResults/PatientRosterPreview/` se generan con el perfil de escritorio.
`Scripts/Desktop/DesktopAtmosphere.cs` aplica **solo en PC** sombras suaves, SSAO, HDR,
tonemapping ACES, bloom y corrección de color. En el Quest (`Settings/QuestURP.asset`):

- sin HDR y con `m_ShadowDistance: 0`, es decir, **sin sombras**;
- sin lightmaps ni oclusión ambiental: luz ambiental plana o trilight
  (`ScenarioEnvironmentPresenter.cs:187`, `DemoProjectBuilder.cs:174`);
- sin skybox (`RenderSettings.skybox = null`).

Resultado: personajes «pegados» al suelo sin sombra, paredes planas y un cielo de un
solo color. **La forma de tener calidad de PC en el Quest sin perder FPS es hornear la
luz**: sombras, oclusión y rebote de luz guardados en texturas, que cuestan casi nada
en tiempo real. Este es el centro del plan.

## Reglas

1. **Primero**, commit de los ~298 cambios pendientes y push a `main`:
   `git -c credential.helper= -c credential.https://github.com.helper= -c credential.helper=manager push origin main`.
   Nada de ramas nuevas. Hacer commit al cerrar cada fase.
2. **No cerrar el Unity del usuario**: compilar en `C:\Users\Juan\vital-lab` (robocopy de
   `Assets`, `Packages` y `ProjectSettings`) y traer de vuelta solo los fuentes.
3. **Solo assets gratuitos y sin cuenta**: Rocketbox (MIT), Poly Haven (CC0), Kenney (CC0),
   Objaverse (CC-BY, con atribución en `StreamingAssets/ThirdParty`).
4. **No tocar el contenido clínico**: constantes, reglas y curvas se quedan igual;
   `CLIENT_REVIEW` también.
5. **Evidencia en `TestResults/astra/`**: capturas antes y después, pruebas y presupuestos.
   Nunca escribir «probado en Quest»; lo correcto es «verificado en simulación».

---

## Fase 0 — Ver lo que verá el cliente, sin visor (viernes, primero)

Sin esto, cualquier mejora visual se juzga sobre una imagen que no es la del Quest.

1. Añadir al ejecutable de Windows la opción **`-vital-quest-look`**, que:
   - usa el nivel de calidad y el `QuestURP` del Quest y no aplica `DesktopAtmosphere`;
   - renderiza con la resolución por ojo del Quest 3 (aprox. 2064×2208) y FOV de ~100°;
   - activa el mismo camino de código que la VR (`IsDesktop == false`): UI en espacio
     del mundo, rig XR y controles de mando.
2. Recorrido de capturas `-vital-quest-look -vital-patient-roster-smoke` de los 15 casos y
   de los menús → `TestResults/astra/00-antes/`. **Esta es la línea base del «antes».**
3. Desde aquí, todas las capturas de comparación se hacen con `-vital-quest-look`.

## Fase 1 — Que todo funcione a la primera (viernes y sábado)

El cliente estará solo con el visor. Cualquier error o pantalla sin salida arruina la demo.

### 1.1 Errores visibles ya detectados

| # | Problema | Dónde | Arreglo |
| --- | --- | --- | --- |
| 1 | El debrief muestra IDs internos («CheckSceneSafety: omisión crítica…») | `Scripts/Medical/MedicalScenarioRuntime.cs:177` | Mostrar solo la etiqueta legible; el ID queda únicamente en el JSON |
| 2 | Si el paciente no responde, la misma frase se repite en cada pregunta (×3 en Andrés) | `Scripts/Dialogue/PatientConversationController.cs:78` | No repetir la misma observación; desactivar las preguntas y destacar «Escuchar al testigo» |
| 3 | El briefing empieza en «Evaluación» (sin pistas) | `Scripts/UI/TrainingExperience.Pages.cs` | Empezar en «Práctica guiada» |
| 4 | El monitor muestra 5 casillas vacías antes de medir nada | `Scripts/UI/TrainingExperience.Clinical.cs` | Mostrar cada lectura solo cuando se haya medido |
| 5 | La espectadora del campo se recuesta en un banco sin respaldo y parece flotar | Pose sentada (`ArticulatedPatient.cs`) | Tronco recto o banco con respaldo |
| 6 | Pacientes conscientes miran al techo y tienen los brazos rígidos | `ArticulatedPatient.cs` | Mirar al usuario y apoyar las manos en los muslos |
| 7 | En el gimnasio, la cinta de correr tapa el punto de vista inicial; junto al letrero DEA flota un texto ilegible | Gimnasio (`Editor/Environment/`) | Recolocar el punto de inicio o la cinta; quitar o ampliar el texto |
| 8 | La compilación de lanzamiento usa versionCode 1 si falta `VITAL_VERSION_CODE`, y «Configure Android» lo reinicia a 1 | `Editor/QuestProjectSetup.cs:39` y `:184` | Variable obligatoria; no reiniciar el código. **Meta rechaza un código repetido** |

### 1.2 Recorrido automático completo en modo VR

- Prueba PlayMode con **entrada XR simulada** (XR Interaction Simulator de XRI 3.3.2 o
  dispositivos simulados de Input System) que recorra los **15 casos**: inicio, selección,
  briefing, caso, pausa, reanudación, finalización, debrief, repetir y volver al inicio.
  Todo con apuntar y gatillo, sin teclado.
- Fallará si aparece alguna excepción o error en el log, si una pantalla no tiene botón de
  salida o si algún texto no cabe en su caja.
- Resultado: `TestResults/astra/xr-walkthrough.xml` con **15/15**.

### 1.3 Comportamiento propio del Quest (requisitos de Meta que no se pueden probar sin visor)

Implementar y cubrir con pruebas PlayMode:
- `OnApplicationPause` / `OnApplicationFocus(false)` (botón Meta o quitarse el visor):
  pausar el caso y el audio; al volver, mostrar el menú de pausa recentrado.
- Al arrancar y al pulsar B/Y: recentrar la interfaz delante del usuario y a la altura de
  sus ojos, tanto sentado como de pie.
- Si se pierde el seguimiento de un mando, o se usa uno solo, todo debe poder hacerse con el otro.
- Si el cliente suelta los mandos y el Quest cambia a manos, que la app no se cuelgue.
- Revisar el manifiesto fusionado de la APK: `com.unity.xr.arcore` está en
  `Packages/manifest.json` y no tiene uso en Quest. Quitar el paquete si no se usa y
  confirmar que no hay permisos de cámara ni de ARCore.

## Fase 2 — Render «wow» en el Quest (sábado y domingo)

Comparar siempre con `-vital-quest-look` contra `00-antes`.

### 2.1 Iluminación horneada (el mayor salto visual)

Para gimnasio, centro comercial y campo:
- Objetos del entorno `Static`; luces principales en **Baked/Mixed**; GPU Lightmapper con
  **oclusión ambiental horneada**, sombras suaves y rebote de luz.
- Lightmaps de 2048 como máximo, 1–2 por entorno, con compresión.
- **Light Probes** en las zonas del paciente y los personajes, para que se iluminen igual que la sala.
- **Reflection Probes horneados** en cada entorno: el metal de las máquinas y las baldosas
  dejan de verse planos.
- **Sombra de contacto** bajo el paciente y los personajes (decal o quad con textura
  difusa): con luz horneada, los personajes que se mueven no tendrían sombra.

Los entornos se generan por código (`Editor/Environment/`). El horneado debe formar parte
del build (`Case01Build.cs` / `QuestProjectSetup`) para que sea reproducible, no un paso manual.

### 2.2 Cielo, materiales y ambiente

- **Campo**: HDRI de Poly Haven (cielo despejado de tarde) como skybox y fuente de luz
  ambiental; sol direccional horneado; césped con textura y franjas de corte; gradas con
  textura y público estático (personajes Rocketbox con LOD bajo).
- **Gimnasio**: suelo de caucho, paredes y metal con texturas Poly Haven de 1K y normal maps;
  paneles de luz emisivos coherentes con el horneado.
- **Centro comercial**: baldosa brillante (aprovecha el reflection probe), escaparates con
  interiores iluminados falsos (textura emisiva, sin geometría) y plantas.
- Texturas en ASTC 6×6 con mipmaps; nada por encima de 1K salvo la cara del paciente (2K).
- Niebla lineal suave en el campo, para dar profundidad.

### 2.3 Personajes que parecen vivos

- Revisar en el Quest los materiales de piel, ojos y pelo de Rocketbox (shader URP Lit o
  SimpleLit): normal map activo, smoothness bajo en la piel y brillo en los ojos.
- Todos los conscientes: **parpadeo, respiración visible, microgestos de reposo y mirada
  que sigue al usuario** cuando se acerca a menos de 2 m.
- Los inconscientes: respiración según el estado (ya existe) y nada de rigidez total.

### 2.4 Escenarios creíbles

- **Quitar el carro de hospital con monitor «DEA»** del campo y del centro comercial (en la
  vida real no existe ahí):
  - centro comercial y gimnasio: **DEA en vitrina de pared con señal verde**;
  - campo: **maletín DEA** que trae un compañero.
  Conservar los anclajes funcionales de `MedicalPhysicalTool`.
- **Gente alrededor**: 2–4 personajes Rocketbox por escenario (compañeros, curiosos,
  personal del centro) con animación de espera. El briefing dice que «un compañero solicita
  ayuda», y ese compañero tiene que estar y hablar.

### 2.5 Sonido

- Ambiente espacial CC0 por escenario: murmullo y música lejana en el centro comercial,
  máquinas y música en el gimnasio, viento, silbatos y público en el campo.
- Voces sintéticas en español para los **14 pacientes y testigos** que solo tienen texto,
  ampliando `tools/Generate-Case01Voices.ps1` (hoy solo Case01 tiene audio: 39 clips).
- **DEA que habla en español** («Pegue los parches», «No toque al paciente», «Analizando…»).
- Sustituir los tonos sintetizados por código (`MedicalProcedureAudio.cs`) por clips CC0.

## Fase 3 — Que se sienta realidad virtual (domingo)

### 3.1 Primeros 60 segundos (lo que más influye en el «wow»)

1. Al arrancar: fundido a una **sala de bienvenida** con luz horneada y el logo VITAL VR, en
   lugar de un panel sobre fondo plano.
2. Una voz de bienvenida y un **tutorial de 30 segundos**: apuntar y pulsar el gatillo,
   agarrar un objeto, teletransportarse y recentrar con B/Y. Se puede saltar.
3. Botón destacado **«Demo recomendada»** que lleva directo a Daniel (gimnasio) y después a
   Andrés (parada con DEA en el campo).
4. Transiciones con fundido a negro entre pantallas y escenarios; nunca un corte brusco.

### 3.2 Interfaz pensada para VR

Hoy se reutiliza la pantalla de escritorio de 1440×900: un panel de 2,4 m de ancho a 2,2 m
(`Scripts/UI/TrainingExperience.cs:120-134`).
- Menús a 1,3 m de ancho y 1,6 m de distancia, con texto más grande, curvatura ligera,
  sonido y vibración al pasar por encima y al pulsar.
- **Durante el caso, ningún panel delante del paciente.** Ficha, preguntas y acciones van en
  una **tableta en la mano izquierda**; las constantes, en la pantalla de cada aparato; el
  registro, como subtítulos cerca del paciente.

### 3.3 Acciones con el cuerpo

Hoy comprobar la seguridad, la respuesta y la respiración se hace con botones; solo RCP, DEA,
mediciones, teléfono y vía aérea son físicas (`MedicalProcedureRig.cs:76`).
- **Seguridad de escena**: mirar alrededor (giro de cabeza acumulado de más de 120°).
- **Respuesta**: tocar los dos hombros del paciente con los mandos.
- **Respiración**: acercar la cabeza a la cara del paciente unos segundos, con un círculo de progreso.
- Mantener los botones como alternativa, y que el debrief indique cuál se usó.

### 3.4 Vibración

Hoy solo vibra la RCP (`CPRInputProviders.cs:87`). Añadir vibración al agarrar y soltar, al
pulsar botones, al tocar al paciente, al colocar los parches y en la descarga del DEA.

## Fase 4 — Rendimiento sin visor: presupuestos conservadores (en paralelo)

Sin Quest no se pueden medir los FPS, así que se trabaja con márgenes amplios y se
comprueban de forma automática:

| Presupuesto por escenario (vista del jugador) | Límite |
| --- | --- |
| Draw calls / batches | ≤ 100 |
| Triángulos visibles | ≤ 300 000 |
| Luces en tiempo real | 1 como máximo (el resto horneadas) |
| Memoria de texturas | ≤ 250 MB en total |
| Post-procesado en Quest | Ninguno |
| Materiales transparentes grandes | Ninguno a pantalla completa |

- Prueba EditMode/PlayMode que cargue cada escenario con la calidad Quest, recoja
  `UnityStats` o `ProfilerRecorder` y falle si se supera un límite. Informe en
  `TestResults/astra/budgets.md`.
- Activar el **foveated rendering fijo de Meta** (OpenXR) en nivel alto; MSAA 4x y render scale 1.0.
- Static batching del entorno y GPU instancing de los objetos repetidos.
- En «Ajustes», opción **Calidad: Alta / Fluida**. Fluida baja el render scale a 0,85 y
  desactiva el público lejano, por si el martes algo va lento.

## Fase 5 — Entrega al cliente (lunes)

1. **APK 0.1.1, código 2**, firmada con `C:/Users/Juan/.vitalvr/vitalvr-release.keystore`.
   Antes, todo en verde: `node tools/Sync-MedicalPortal.mjs`,
   `node --test tools/ReleaseScope.test.mjs`, EditMode, PlayMode, el recorrido XR 15/15
   y los presupuestos.
2. Subirla al **canal de prueba** de Meta en el que está la versión 1.
   **Juan** confirma que la cuenta de Meta del cliente está invitada a ese canal.
   La publicación pública no se toca.
3. **Guía de una página para el cliente** (`docs/GUIA_PRIMER_USO.md`, en lenguaje sencillo):
   cómo instalar desde el canal, cómo ajustar el visor, botones de los mandos, cómo
   recentrar (B/Y) y el recorrido recomendado.
4. **Plan B**: ejecutable de Windows actualizado y vídeo de 3 minutos en `-vital-quest-look`,
   por si falla la instalación o el Wi-Fi.
5. **«Qué mejoró desde la versión 1»**: comparación de capturas de `00-antes` con las finales.

---

## Calendario

| Día | Trabajo |
| --- | --- |
| Viernes | Commit inicial · Fase 0 · Fase 1.1 |
| Sábado | Fases 1.2 y 1.3 · Fase 2.1 (horneado) · Fase 4 (presupuestos) |
| Domingo | Fases 2.2–2.5 · Fase 3 |
| Lunes | Repetir el recorrido XR y los presupuestos · Fase 5 · candidato final |

## Si falta tiempo, recortar en este orden

Fase 3.3 (gestos), público en las gradas, sustitución de tonos sintetizados, voces de los
14 casos y fase 3.2 (tableta en la mano; se puede dejar solo el panel reducido).

**No recortar nunca**: Fase 0, Fase 1 completa, horneado de luz (2.1), cielo y materiales
del campo (2.2), cambio del carro de hospital (2.4), los primeros 60 segundos (3.1),
presupuestos (Fase 4) ni la APK de la Fase 5.
