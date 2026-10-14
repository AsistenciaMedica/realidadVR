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

## El panel ya no tapa al paciente

- **Quest:** el panel de opciones va en la **muñeca izquierda**, como una tablet. Aparece al mirarte la
  mano y se desvanece al mirar al paciente (con 1,2 s de margen). Al empezar se muestra 3 s para que se
  descubra. Sin mando izquierdo, o en la simulación de Quest en Windows, vuelve al panel lateral fijo.
  Código: `TrainingExperience.WristMenu.cs`.
- **Windows:** al abrir un cajón (acciones, conversación, teléfono, información), la cámara gira
  suavemente para dejar al paciente a la derecha, fuera del cajón. Mirar con el ratón cancela el giro.

Código: `Assets/_Project/Scripts/Medical/Interaction/HandsOnInteraction.cs` (zonas, entrada Windows/VR,
anillos, burbuja). Pequeños cambios en `MedicalPhysicalTool`, `PatientVisualController`,
`MedicalProcedureRig`, `DesktopDemoController`, `TrainingExperience.World/Clinical` y
`GuidedPracticeDirector`.

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
8. Quest: mirarse la mano izquierda muestra el panel; mirar al paciente lo oculta; los botones siguen funcionando con el rayo derecho.
9. Windows: con el paciente a la izquierda, abrir «Interacciones» o «Hablar» gira la vista y lo deja visible.
10. Ejecutar las pruebas EditMode y PlayMode existentes.
