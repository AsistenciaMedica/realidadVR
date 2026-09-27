# VITAL VR — capacidad del avatar de CASE 01

El avatar sigue siendo el Rocketbox `Sports_Male_01` existente, importado desde `Assets/ThirdParty/Rocketbox/Patient.fbx`. No se ha descargado ni comprado otro personaje. La licencia MIT y procedencia se conservan en esa carpeta. Este informe distingue recursos existentes, capacidades técnicas y calidad pendiente de validar.

## Recurso realmente utilizado

`Resources/Visual/Patient.prefab` contiene un SkinnedMeshRenderer con 4.874 vértices, 7.358 triángulos, 80 bindings óseos y dos materiales. El importador es Generic; no tiene un Avatar Humanoid validado ni un AnimatorController activo. `VisualAssetBuilder` elimina Animator y conserva seis canales faciales más `VitalBreath` y `VitalCompression` en `PatientMesh.asset`.

El FBX original sí conserva esqueleto corporal, cinco cadenas de tres articulaciones por mano, mandíbula, labios, lengua, ojos, párpados y cejas, además de los 15 visemas `AA_VI_00_Sil`–`AA_VI_14_U`. Los visemas no están en la malla runtime compacta. No se afirma que el prefab activo tenga lip sync.

Los únicos clips corporales fuente son `m_idle_breathe_01.fbx` y `m_idle_cough_01.fbx`, usados de pie en los escenarios heredados. No hay clips capturados denominados SeatedPresyncope, AssistedToSupine, RiseAttempt o RegainSupport.

## Capacidades

| Área | Estado y alcance real |
| --- | --- |
| Humano y esqueleto | YA DISPONIBLE. Se reutiliza exactamente el personaje importado. |
| Humanoid / Animator | POSIBLE CON EL ASSET ACTUAL; requieren configuración/validación. La coreografía actual usa huesos Generic directamente. |
| Manos y dedos | YA DISPONIBLE. Los dedos existen; no son sensores de fuerza ni dedos físicos con garantía anticolisión. |
| Cara y párpados | YA DISPONIBLE: blink izquierdo/derecho, mandíbula, cejas y tensión labial. Se emplean canales reales. |
| Visemas | POSIBLE CON EL ASSET ACTUAL recuperando los canales del FBX en una variante. REQUIERE ANIMACIÓN DE HABLA y AUDIO. |
| Daniel fijo | YA DISPONIBLE: aspecto masculino adulto y perfil Daniel/40 años en datos; no hay variación aleatoria entre intentos. La edad aparente es una decisión artística, no una propiedad certificada del mesh. |
| Ropa deportiva | Camiseta CASE 01 derivada del mesh licenciado con pesos del mismo esqueleto, material opaco y respiración torácica. No sustituye el avatar. REQUIERE QA visual de cuello, mangas y axilas; no hay simulación física de tejido. |
| Respiración | Fase clínica única alimenta deformación del torso y camiseta. No se codifica una FR fija en la presentación V2. |
| Sentado y asistencia | Coreografía procedural CASE 01 con root continuo, solver de extremidades y anclajes reales del banco. No se etiqueta como captura de movimiento o clip profesional. |
| Espacio y apoyos | Comprobaciones de volúmenes anatómicos antes y durante el recorrido; validación de soporte final antes de confirmar el ticket clínico. No equivale a física de tejidos ni a entrenar fuerza manual real. |
| Ragdoll | Adaptador compartido conservado, desactivado en este caso consciente. |
| Palidez | Existe tinte de piel compartido. Sudor detallado REQUIERE texturas/materiales específicos; no se anuncia como implementado. |
| Testigo | No existe un segundo avatar diferenciado vestido de empleado. La interfaz puede usar testigo fuera de campo; un NPC visible REQUIERE recurso/variante y animación adicionales. |
| Voz | No existen grabaciones profesionales de Daniel, operador o testigo. Los subtítulos y referencias de audio permiten incorporarlas; REQUIERE AUDIO. |

## Separación del resto de escenarios

`Case01PatientPresentation` se activa solamente para una definición registrada con presentación y confirmación física de posición. Durante su actividad deshabilita `ArticulatedPatient`, conserva el mismo skin, utiliza `PatientVisualController` y restaura el controlador y las transformaciones al salir. El generador del gimnasio heredado, los archivos FBX y la malla compartida no se regeneran.

El montaje específico sitúa un banco con asiento, respaldo y patas separados cerca de cardio y una zona despejada. El banco heredado usa un collider macizo que no describe el espacio debajo del asiento; no se utiliza como prueba de apoyo del cuerpo nuevo.

`PatientPositionTransitionController` continúa siendo el contrato con el motor. Solicitar asistencia no cambia constantes ni posición clínica efectiva. La confirmación solo se envía tras validar el recorrido y el soporte final. Pausa detiene progreso; retirar asistencia detiene movimiento; cancelar vuelve por el recorrido validado, sin teleport al banco. Obstáculos nuevos bloquean también el regreso.

## Recursos todavía necesarios para mayor fidelidad

- REQUIERE ANIMACIÓN: refinar coreografía procedural y producir clips artísticos para apoyo, debilidad, recuperación, ofrecer brazo/dedo, reacción al manguito y escucha en relevo. La existencia de huesos no significa que estos clips existan.
- REQUIERE MODELADO: acabado de prenda con costuras/cuello y revisión de recortes si el ajuste derivado no supera QA; no es obligatorio cambiar de avatar.
- REQUIERE AUDIO: interpretación española de Daniel, testigo y operador con referencias sustituibles; no usar voz sintética como entrega final de voz profesional.
- POSIBLE CON EL ASSET ACTUAL: ojos, expresiones y visemas más ricos. Recuperar solo canales utilizados para controlar memoria y deformación.
- REQUIERE NUEVO ASSET únicamente si se decide contratar externamente ropa, clips o un NPC diferenciado. No se ha adquirido ninguno.

## Quest 3

El paciente compartido actual no tiene LOD y mantiene `updateWhenOffscreen`. La camiseta comparte huesos y añade una sola malla/material; no usa cloth, ragdoll permanente o Animator adicional. Las comprobaciones emplean buffers persistentes y están activas durante asistencia. No se garantiza rendimiento en Quest 3 sin perfilado en dispositivo. Un APK compilado no valida comodidad, contacto, tracking ni ausencia de clipping.

No se habilitan pérdidas de conciencia, caídas sorpresa, SupportedLossOfTone, SidePositionAssisted ni RegainAwareness en CASE 01. Las tolerancias de interacción y trayectorias corporales son parámetros de simulación visual, no reglas clínicas universales.
