# TrainingRoom procedural

La herramienta amplía la TrainingRoom existente usando las rutas de
`DemoProjectBuilder`. No ejecuta Generate demo, QuestProjectSetup ni cambios de
packages, OpenXR, XRI, URP global, Bootstrap o lógica clínica.

## Generar y limpiar

1. Fuera de Play Mode, esperar la compilación de Unity **6000.3.23f1**.
2. **Emergency VR > Environment > Generate Training Room**.
3. Abrir `Assets/_Project/Scenes/Training/TrainingRoom.unity`.

El menú abre la escena aditivamente si hace falta y guarda el resultado. Si la
escena ya tenía cambios sin guardar, los conserva y deja el guardado al usuario.
Puede ejecutarse desde Bootstrap u otra escena sin reemplazarla.

**Clear Generated Environment** restaura los seis placeholders originales y
el ambiente anterior. Elimina exclusivamente los objetos registrados por
`GeneratedEnvironment`, no objetos encontrados por nombre. Los objetos nuevos
añadidos como hijos de objetos generados se conservan como raíces, con su
posición mundial. Los prefabs, meshes y materiales quedan disponibles.

La generación repetida reemplaza la instancia del entorno. La geometría de
prefabs/materiales solo se crea si falta: las ediciones artísticas en esos assets
persisten. Para volver a una versión procedural de un prefab editado, conservar
una copia propia y retirar únicamente ese prefab generado antes de generar.
No editar directamente los meshes combinados: se regeneran al recrear su prefab.

## Organización y medidas

```text
Environment
  Architecture / Floor, Ceiling, Walls, Doors, Windows, Skirting
  Furniture / HospitalBed, MedicalCabinet, MedicalCart, Stool, SideTable
  MedicalEquipment / PatientMonitor, IVStand, OxygenTank,
                     DefibrillatorPlaceholder, Supplies
  Decoration / WallServicePanel
PatientArea / PatientSpawnPoint, TreatmentZone
Lighting / CeilingFixture, BakedCeilingLight
```

XR, Training Systems, Patient Placeholder, Training VR Panel, XR EventSystem,
Room light, cubo agarrable y los cuatro Teleport pad conservan sus objetos,
transformaciones y referencias. No se reubican bajo nuevas raíces para evitar
cambios innecesarios en sus prefabs y referencias.

Unidades en metros. Interior: x = −3.5…3.5, z = −3…5, altura 3.
Puerta corredera aparcada: abertura libre de 1.2 × 2.1 m. Ventana interior con
vidrio esmerilado opaco. Camilla: colchón de 0.86 × 2.02 m a 0.75 m; barandas,
cabeceras y ruedas elevan/amplían la envolvente. Centro en (1.55, 0, 2.1), bajo
el paciente existente. La mesa sostiene el cubo agarrable en su posición original.
PatientSpawnPoint es un anclaje informativo; no mueve ni crea pacientes.
TreatmentZone es una marca visual sin collider ni lógica médica.

## Assets reutilizables

`Assets/_Project/Art/Prefabs/Generated/` contiene 18 prefabs en Architecture,
Furniture, Medical y Props. Cada prefab separa **Visual** de **Physics**.
Para sustituir por un modelo real, editar el prefab, cambiar los hijos de Visual
y conservar el root, su escala 1, su pivote y Physics. Añadir lógica al root si
posteriormente se necesita; no a los meshes. Guardar la edición del prefab para
que persista al regenerar la escena.

`Assets/_Project/Art/Materials/Generated/` contiene materiales URP/Lit:
M_Wall_Hospital, M_Floor_Hospital, M_Ceiling, M_Metal, M_PlasticWhite,
M_MedicalBlue, M_DarkEquipment, M_Glass; M_Light y M_Screen añaden emisión sencilla.
El vidrio opaco evita transparencia y ordenamiento. El monitor y desfibrilador
son visuales sin lecturas clínicas ni acciones nuevas.

`Assets/_Project/Art/Meshes/Generated/` contiene cilindro de 12 lados y meshes
combinados por material/prefab. No hay MeshColliders; se usan BoxColliders simples,
sin colliders en las piezas visuales. Se generan UV2 y marcas BatchingStatic /
ContributeGI para permitir un bake posterior.

## Iluminación y rendimiento

Se conserva la luz direccional original sin sombras y se añade ambiente claro.
Tres luminarias de techo usan emisión, con dos luces **Baked** preparadas. No se
ha calculado un lightmap: las luces baked solo aportarán luz después de hornear.
La sala es visible inmediatamente mediante la direccional y el ambiente. No se
añaden luces realtime si ya existe la direccional ni se modifica postprocesado.
Si se retira esa luz, una regeneración crea una direccional sin sombras de respaldo.

El aspecto y los tests de Editor no equivalen a una medición de rendimiento en
Quest 3. Pendiente medir GPU, CPU y memoria en el visor en una fase posterior.

## Validación

- Dominio: `powershell -NoProfile -ExecutionPolicy Bypass -File tools/Test-Core.ps1`.
- Unity Test Runner > EditMode: pruebas existentes más TrainingRoomEnvironmentTests.
  Guardar escenas antes; los tests que mutan el entorno descartan sus cambios y
  restauran las escenas abiertas. Si hay cambios sin guardar se omiten esas pruebas.
- Unity Test Runner > PlayMode: TrainingRoomSmokeTests verifica Bootstrap, cámara,
  UI, callback PatientInteraction, estados y resultado 100/100. Invoca eventos;
  no sustituye una prueba de rayos con mandos.
- **Emergency VR > Environment > Capture Training Room Preview** escribe una
  imagen URP en `TestResults/training-room-preview.png`, sin guardar una cámara nueva.

Resultados locales en `TestResults/` (ignorados por Git). No generar APK todavía.

### Resultado de esta entrega — 12 de septiembre de 2026

- Compilación y generación: completadas en Unity 6000.3.23f1.
- Core: **27 aprobados, 0 fallos** (`core-tests.txt`).
- EditMode: **35 aprobados, 0 fallos, 0 omitidos** (`environment-editmode.xml`).
- PlayMode: **1 aprobado, 0 fallos** (`environment-playmode.xml`). Bootstrap
  cargó TrainingRoom con Meta XR Simulator activo; la secuencia UI/paciente
  produjo 100/100. Los eventos se invocaron automáticamente, sin probar mandos físicos.
- Captura URP revisada: `training-room-preview.png`.
- Entorno añadido: **36 instancias, 69 renderers, 2.136 triángulos y 26
  BoxColliders**, excluyendo XR, paciente y UI preexistentes. Estos conteos no
  equivalen a draw calls ni a un perfil de GPU (`environment-geometry-stats.json`).
- Comparación de TrainingRoom contra la versión previa: 152 bloques serializados
  originales conservados, ninguno eliminado. Solo cambian seis estados de
  activación, ambiente y lista de raíces, además de los objetos nuevos.
- Sin cambios a Bootstrap, DemoProjectBuilder, QuestProjectSetup, paquetes,
  configuración XR ni scripts de lógica médica. Sin build Android.

Límites observados: la captura muestra un visual de mando magenta; los materiales
Controller_Grey/Controller_White de Starter Assets todavía referencian el shader
integrado original. No se modificaron assets de XRI. El log del simulador emitió
`Met Expectation: sessionData == nullptr` en compositor_readback; no impidió el
test, pero conviene comprobar la imagen del simulador en la revisión manual.
Tracking, rayos, teleport con mandos y rendimiento Quest 3 siguen pendientes
de esa comprobación manual; no se dan por validados por el resultado automático.

## Comprobación manual en Meta XR Simulator

1. Usar la configuración del simulador que ya funciona; no volver a configurar XR.
2. Abrir Bootstrap y pulsar Play. Confirmar que carga la nueva TrainingRoom.
3. Revisar altura del suelo, cámara y ambos mandos; comprobar rayos y giro de 30°.
4. Teletransportarse a las cuatro zonas con stick arriba/soltar; comprobar que se
   puede observar/acceder al paciente y que ningún mueble invade el destino.
5. Agarrar/soltar el cubo con Grip: debe apoyarse en la nueva mesa o suelo.
6. Trigger en Iniciar caso demo; Grip sobre paciente; Trigger en Transición demo;
   Finalizar. Esperar cambios de color/estado y 100/100.
7. Reiniciar y finalizar antes de tiempo para comprobar omisiones y UI.
8. Revisar Console, clipping del techo/paredes y acceso alrededor de la camilla.

Más adelante sustituir primero camilla, monitor, IV stand, oxígeno, carro,
gabinete, desfibrilador, suministros y maniquí/paciente. La sustitución del
paciente requiere conservar PatientController, renderer configurado,
XRSimpleInteractable, colliders y PatientInteraction; no forma parte de este generador.

Referencias: [prefabs de Unity](https://docs.unity3d.com/Manual/CreatingPrefabs.html),
[render requests URP](https://docs.unity3d.com/Manual/urp/User-Render-Requests.html),
[tests por CLI](https://docs.unity3d.com/Manual/test-framework/run-tests-from-command-line.html).
