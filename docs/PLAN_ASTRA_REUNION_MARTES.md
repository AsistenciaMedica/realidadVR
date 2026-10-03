# Plan Astra — VITAL VR listo para el «wow» del martes

Destinatario: **Astra**, agente que ejecuta este plan sobre `proyectovr`.
Fecha: viernes 2 de octubre de 2026. Entrega: **lunes 5 por la noche**.

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
