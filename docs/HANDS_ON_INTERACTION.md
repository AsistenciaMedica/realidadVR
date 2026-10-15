# Interacción directa con el paciente y el equipo

Actualizado: 13 de octubre de 2026. Estado: **implementado sin compilar ni probar en Unity**;
revisar en local con la lista del final antes de cualquier demo.

## Qué cambia

Antes, casi todas las acciones eran botones de menú o teclas. Ahora el alumno actúa **sobre el
paciente y sobre los aparatos**. Cada zona envía exactamente la misma acción que el botón del menú,
así que el motor médico, la puntuación y los informes no cambian. El menú sigue disponible.

| Zona | Windows | Quest | Acción registrada |
| --- | --- | --- | --- |
| Hombros | Apuntar y mantener clic 1 s | Mano en el hombro + gatillo | Comprobar respuesta; después, reevaluar |
| Pecho | Mantener clic 5 s (3 s en Daniel) cerca del paciente | Mano en el pecho + gatillo | Comprobar / observar la respiración |
| Pecho (paro, tras comprobar respiración) | Clic: empieza las compresiones (igual que C) | Indica cómo hacer RCP con los grips | StartCPR lo registra la RCP |
| Cabeza (paro) | Mantener clic 1 s | Mano en la cabeza + gatillo | Abrir vía aérea; ventilaciones si el caso las tiene |
| Cabeza (Daniel) | Clic: abre la conversación | — | — |
| Espalda (atragantamiento) | Clic desde detrás: activa la maniobra (igual que H) | Indica la maniobra con dos grips | Golpes y compresiones como antes |
| DEA: tapa, encendido, botón naranja | Clic en cada control | Tocar + gatillo | Abrir, encender, analizar, descargar |
| Parches y pulsioxímetro, glucómetro, tensiómetro | Clic sobre el objeto | Tocar + gatillo | Retira el plástico / prepara el instrumento |

Además:

- **Anillo de enfoque** sobre la zona apuntada o tocada, en ámbar si estás demasiado lejos, y **arco de
  progreso** en las acciones que se mantienen.
- **Práctica guiada:** anillos discretos sobre todo lo que se puede hacer cerca. En evaluación no aparecen.
- **Reacción del paciente:** sacudida breve al tocar los hombros y una **burbuja** con lo que el alumno
  percibe («Andrés no responde a la voz ni al contacto», «No respira con normalidad: solo boqueadas»).
  En Daniel la burbuja muestra la respuesta del motor clínico V2.
- **Guía de colocación:** con un instrumento en la mano aparece su zona en el paciente (círculo, o
  recuadro para los parches) y se vuelve verde cuando al soltar quedará colocado. Si se suelta mal, el
  aviso explica por qué (sin preparar, o fuera de la zona).
- Un clic sobre una zona ya **no registra además «Comprobar respuesta»**, que antes se enviaba con
  cualquier clic sobre el cuerpo.

## Exploración con instrumentos (también con Daniel)

- El maletín está en todos los casos con **tensiómetro, pulsioxímetro, glucómetro y fonendoscopio**. En el caso
  de Daniel no cuentan en la evaluación V2 (que no tiene acciones de medida), pero leen al paciente simulado y
  muestran el valor en el aparato y junto a él.
- **Fonendoscopio:** cógelo y apóyalo en el pecho (el anillo se pone verde). Se oye el latido generado a la
  frecuencia cardíaca del paciente y a los 2,5 s aparece «Ruidos cardíacos rítmicos, N lpm» y la respiración.
  El tubo va del diafragma a tus oídos mientras lo sostienes; al soltarlo vuelve al maletín.
- **Pulso radial:** mano sobre la muñeca 4 s (Quest: dejarla quieta; Windows: mantener clic). Muestra lpm,
  ritmo y si es débil (sistólica < 90).
- El armario del DEA de la pared ya no está vacío cuando el caso no usa DEA: lleva un DEA cerrado decorativo.
- Quest: el panel vuelve a quedarse fijo (B / Y lo recoloca) y la tarjeta de primeros pasos ya no aparece.

## Fiabilidad para la demo del 15 de octubre

- Tensiómetro, pulsioxímetro y glucómetro quedan **preparados al cogerlos** (sin paso oculto de «usar»).
- Tolerancias más amplias: instrumentos 18 cm, fonendoscopio 15 cm, parches 15 cm y 75°.
- Windows: con un instrumento en la mano, mirar su zona en el paciente lo lleva allí (colocación magnética).
- Quest: la red de seguridad del giro ya no se activa por error (doblaba el giro).
- Zonas evaluadas como máximo 10 veces por segundo (sin basura por fotograma en Quest).
- El horneado de iluminación no se repite al compilar: el archivo del horneador vuelve a su versión anterior.

## Gestos reales

- **Quest, comprobar si responde:** mano sobre su hombro y moverla (sacudir o dar golpecitos) durante 1 s;
  el gatillo también vale.
- **Quest, comprobar la respiración (paro/atragantamiento):** acercar tu cara a la suya (< 50 cm) y quedarte
  5 s: ver, oír y sentir. Si el caso pide abrir la vía aérea, primero hay que abrirla.
- **Windows, comprobar la respiración:** al mantener clic en el pecho la vista baja junto a su boca mirando
  el tórax, y vuelve al soltar.
- Las manos articuladas existentes van al hombro, a la barbilla o a la muñeca al aceptarse cada acción, y
  el paciente reacciona; las zonas nuevas disparan esas mismas animaciones.

## El panel ya no tapa al paciente

- **Quest (0.1.2):** el panel lateral **te acompaña**: si caminas más de 1 m o si queda entre tus ojos y el
  paciente, se desliza al lado contrario al paciente. Solo usa la posición de la cabeza, que es fiable, y
  siempre está visible. El panel en la muñeca está programado pero desactivado hasta probarlo en un visor
  real (`-vital-wrist-panel`). Código: `TrainingExperience.WristMenu.cs`.
- Caminar con el stick izquierdo va a 1,1 m/s para reducir el mareo en usuarios nuevos.
- **Windows:** al abrir un cajón (acciones, conversación, teléfono, información), la cámara gira
  suavemente para dejar al paciente a la derecha, fuera del cajón. Mirar con el ratón cancela el giro.

Código: `Assets/_Project/Scripts/Medical/Interaction/HandsOnInteraction.cs` (zonas, entrada Windows/VR,
anillos, burbuja). Pequeños cambios en `MedicalPhysicalTool`, `PatientVisualController`,
`MedicalProcedureRig`, `DesktopDemoController`, `TrainingExperience.World/Clinical` y
`GuidedPracticeDirector`.

## Que nunca se quede atascado

- El cuerpo del paciente y los instrumentos ya no son obstáculos para el doctor (Windows y Quest).
- Al empezar, el doctor aparece junto al paciente en un sitio **libre comprobado**; si el punto previsto
  cae dentro de un mueble, busca otro alrededor.
- Si intentas caminar, no avanzas y estás dentro de algo, a los 1,2 s te mueve al sitio libre más cercano
  y lo avisa. Empujar una pared normal no lo activa.
- Levantarse tras arrodillarse solo se bloquea si de verdad hay algo encima, y lo dice.
- También se camina con las **flechas** del teclado.

## Primeros pasos (tutorial integrado)

Al empezar el primer ejercicio aparece una lista que se marca sola: caminar, mirar, acercarse, arrodillarse
y actuar sobre el paciente (Windows); caminar, girar, mirarse la muñeca y tocar al paciente (Quest).
Windows: tarjeta a la derecha, F1 la muestra u oculta. Quest: tarjeta baja a la izquierda, nunca delante
del paciente. Desaparece 4 s después de completarla. No cuenta en la evaluación. Código: `FirstStepsCoach.cs`.

Interruptor de seguridad para una demo: `EmergencyVR.exe -vital-classic-controls` desactiva las zonas.

## Lista de revisión en local

1. Compila sin errores en Unity 6000.3.23f1.
2. Mareo (Daniel), Windows: apuntar a los hombros → anillo → mantener clic → burbuja y registro en la ficha.
   Pecho: mantener 3 s. Cabeza: abre la conversación. Las teclas G/B/H siguen funcionando.
3. Paro (Andrés): hombros → «no responde»; pecho 5 s → «no respira con normalidad»; clic en el pecho →
   compresiones (C para salir). Cabeza → vía aérea si el caso la tiene.
4. DEA: clic en la tapa, en el botón verde y en el naranja; parches: clic para quitar el plástico, coger
   y soltar dentro del recuadro verde.
5. Atragantamiento (Camila): ponerse detrás y clic en la espalda activa la maniobra.
6. Quest: tocar hombros y pecho con el mando y apretar el gatillo; vibra al entrar en la zona y al completar.
7. Pausa, finalizar y reiniciar: no quedan anillos ni burbujas visibles.
8. Quest / XR Simulator: el panel está a un lado; al caminar 1 m o al ponerlo delante del paciente se aparta; los botones funcionan con el rayo.
9. Windows: con el paciente a la izquierda, abrir «Interacciones» o «Hablar» gira la vista y lo deja visible.
10. Ejecutar las pruebas EditMode y PlayMode existentes.
