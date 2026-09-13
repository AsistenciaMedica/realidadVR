# TrainingRoom — visual polish procedural

Esta fase mejora la misma TrainingRoom y conserva el Environment Builder.
No añade escenarios, descargas, packages ni modificaciones a OpenXR/XRI o a
la lógica médica.

## Regeneración y edición posterior

**Emergency VR > Environment > Generate Training Room** abre y actualiza la
escena existente. El primer uso de una revisión visual actualiza `Visual` en
los prefabs generados conservando su GUID, raíz, componentes y `Physics`.
La revisión se guarda en `AssetImporter.userData`; las regeneraciones siguientes
reutilizan los prefabs y respetan las ediciones artísticas posteriores.

Los materiales existentes se actualizan una vez por revisión, conservando sus
GUIDs. Para revisar este código en el futuro, incrementar la revisión únicamente
cuando se pretenda migrar de nuevo los assets procedurales. Una migración de
revisión reemplaza el visual: guardar antes las sustituciones artísticas propias
en un prefab independiente. El generador no elimina meshes antiguos sin uso.

El entorno anterior se limpia antes de migrar prefabs, para que los nuevos hijos
de los prefabs no se confundan con objetos añadidos por el usuario. La limpieza
sigue usando el registro de propiedad y conserva los objetos ajenos.

**Clear Generated Environment** también restaura la posición/rotación/escala
anterior del panel, los materiales originales de los cuatro pads y la intensidad
de la luz original. No cambia la configuración de rayos, teleport o mandos.

## Cambios visuales

- Arquitectura: paredes claras con revestimiento inferior, protectores y remate
  superior; juntas discretas en suelo y techo; puerta corredera con carril,
  tirador y chapa inferior; ventana con alféizar y bandas de privacidad.
- Camilla: colchón y almohada con bordes suavizados, bastidor, elevador,
  tirantes, barandas tubulares, cabeceras, agarres, horquillas, ruedas y frenos.
- Monitor: carcasa suavizada, marco oscuro, pantalla, controles, LED, asa,
  soporte ajustable y cable. Lecturas fijas **HR 78 / SpO2 97% / BP 120/80**,
  con etiqueta DEMO. No representan el estado del caso ni cambian durante la práctica.
- IV: base de cinco brazos con ruedas, poste, collar, ganchos, bolsa etiquetada,
  cámara de goteo y tubo hacia la zona del paciente.
- Oxígeno: hombros, cuello, válvula, regulador/manómetro, etiqueta O2 y manguera.
- Carro: cajones, etiquetas, tiradores, asa, protecciones, bandeja y paquete.
- Gabinete: puertas inferiores, tiradores, compartimentos superiores abiertos
  con marco, estantes y cajas; no se añade vidrio transparente costoso.
- Desfibrilador: marco/pantalla, botones, dos palas y sus cables.
- Sala: panel de servicios con tomas, papelera con pedal, dispensador, reloj
  decorativo, señal URGENCIAS 01, SALIDA, HIGIENE y bandeja de suministros.
- Paleta: blanco, gris claro, azul desaturado, metal moderado y pantallas oscuras.
  Los pads conservan geometría/colliders y usan un color más discreto.
- UI: mismo panel y botones, ubicado en pared izquierda a x=−3.445 m, altura
  1.55 m y escala 0.00115. Despeja el paciente; no se rediseña su contenido.
- Iluminación: direccional existente a 0.8, ambiente neutro y luminarias emisivas.
  Dos luces Baked a 0.35 preparadas, sin calcular lightmaps en esta fase.

## Quest 3

Bordes suavizados de baja resolución y tubos de 12 lados. Los textos usan
quads procedurales combinados con los meshes del prefab: no dependen de fuentes,
texturas descargadas, canvases ni actualizaciones por frame. Todos los detalles
son estáticos. Se conserva una sola luz realtime sin sombras, con BoxColliders
simples separados de Visual. No se añade postprocesado.

Los conteos del entorno están en `TestResults/polished-geometry-stats.json`;
excluyen paciente, XR y UI existentes. No equivalen a draw calls ni mediciones
de CPU/GPU en visor.

## Captura y pruebas

- **Emergency VR > Environment > Capture Polished Training Room** genera
  `TestResults/training-room-polished.png` mediante URP, con cámara temporal.
- Core: `powershell -NoProfile -ExecutionPolicy Bypass -File tools/Test-Core.ps1`.
- EditMode: 35 pruebas existentes; se amplió la comprobación de restauración
  del panel, materiales y luz sin eliminar ni desactivar pruebas.
- PlayMode: prueba existente Bootstrap → TrainingRoom → UI/paciente → 100/100.
- Logs y XML de esta fase usan el prefijo `polish-` en `TestResults/`.

Después, abrir Bootstrap con la configuración de Meta XR Simulator existente:
verificar UI en la pared izquierda, lecturas del monitor, agarre del cubo,
rayos y los cuatro destinos de teleport. Repetir la secuencia del caso. La
validación manual de tracking/rayos y el perfil de rendimiento Quest 3 son
distintos de los tests automáticos. No se construye Android ni otro escenario.

## Archivos de implementación

Modificados: `EnvironmentPrefabFactory.cs`, `TrainingRoomEnvironmentBuilder.cs`,
`TrainingRoomPreview.cs`, `GeneratedEnvironment.cs`,
`TrainingRoomEnvironmentTests.cs`, TrainingRoom y assets generados existentes.

Creado: `EnvironmentVisualPolish.cs`, prefabs `Architecture/WallFinish` y
`Props/RoomDetails`, materiales M_WallLower, M_Seam, M_Teleport y M_Display,
meshes derivados y sus `.meta`, esta guía y captura/resultados locales.
