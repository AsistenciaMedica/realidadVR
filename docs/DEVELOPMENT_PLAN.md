# Plan de desarrollo

## Alcance vigente — 2026-10-13

La primera versión comercial queda limitada a **cancha, gimnasio y calle, con
las mismas tres pruebas por entorno (9 combinaciones): mareo y toma de síntomas,
paro cardiorrespiratorio con RCP y atragantamiento con desobstrucción**. El orden de cierre y las
comprobaciones para Meta están en [LAUNCH_SCOPE.md](LAUNCH_SCOPE.md).
El resto de este documento conserva el historial de fases de septiembre; sus
restricciones y cuatro entornos originales no definen el lanzamiento actual.

## Revisión de Quest y pendientes — 2026-10-13

**Prioridad: poder entrar y actuar con los dos mandos físicos en Quest.**
El usuario informa de seguimiento de cabeza, pero de falta de respuesta de los
mandos. Se desconoce si llegó a iniciar el ejercicio. No hay registro del visor
ni confirmación de la versión instalada: la causa de ese fallo sigue abierta.
Las pruebas locales con dispositivos simulados no validan el runtime de Quest.

### Hallazgos comprobados

| Hallazgo | Evidencia | Consecuencia |
| --- | --- | --- |
| Caminar con el joystick está desactivado en ambos mandos. Hay teletransporte y giro por pasos de 30°. | `DemoProjectBuilder.ConfigureRig`, prefab `QuestControllerRig` y `GeneratedProjectTests`. | Empujar el joystick no produce la marcha continua que esperaba el usuario. No explica por sí solo que fallen botones y agarre. |
| Los destinos de teletransporte se ocultan al seleccionar un entorno. | `ScenarioEnvironmentPresenter` guarda los renderizadores de `Teleport pad` y los desactiva en `Select`. | Los destinos conservan sus componentes de interacción, pero no se ven: descubrir cómo desplazarse resulta difícil. |
| Bienvenida, selección, pausa y transiciones bloquean locomoción y selección de objetos del mundo. | `TrainingExperience.BlocksWorldInput`, filtro de selección y activación de proveedores de locomoción. | Si no pudo iniciar la sesión, tampoco podía practicar con objetos. Los botones de la interfaz deben seguir respondiendo. |
| El proyecto Android tiene OpenXR al inicio, Meta Quest Support, Oculus Touch e Input System activos. | `Assets/XR/`, `ProjectSettings` y `QuestProjectSetup.FindIssues`. | No se encontró una falta evidente de configuración básica. Esto no acredita la configuración ni el funcionamiento de la APK instalada. |
| Las acciones de agarre, gatillo y joystick usan los Starter Assets; el perfil Oculus Touch instalado declara los controles y usos correspondientes. | `XRI Default Input Actions.inputactions` y código de OpenXR 1.16.1 en PackageCache. | La lectura estática no demuestra que los bindings estén rotos. Falta comprobar su resolución y los eventos durante una sesión nativa. |
| La última APK local es `VITAL-VR-0.1.0-1.apk`, del 1 de octubre, versión 0.1.0, código 1, paquete `com.vitalvr.training`, ARM64. | AAPT2, fecha del archivo y SHA-256 en [evidencia de APK](../TestResults/quest-audit-20261013/apk-evidence.json). | Es anterior a los cambios del 13 de octubre. No se ha confirmado que sea exactamente la entrega instalada desde Meta. El proyecto actual indica 0.1.1. |

### Orden de trabajo y condición de cierre

| Prioridad | Trabajo pendiente | Se considera terminado cuando… |
| --- | --- | --- |
| P0 · Mandos y entrada | Generar una APK de diagnóstico actual; identificar versión/código; comprobar seguimiento, gatillo y agarre en bienvenida y después el puntero y los botones. Registrar dispositivos, perfil activo, acciones habilitadas, controles resueltos y errores OpenXR si falla. | En Quest, ambos mandos siguen el movimiento, seleccionan tutorial/entorno/ejercicio y permiten iniciar una sesión desde un arranque en frío. |
| P0 · Recuperación | Comprobar pérdida y recuperación de seguimiento, menú del sistema, pausa y vuelta a la app. | La sesión vuelve a responder sin quedar atrapada en pausa ni requerir reinstalación. |
| P1 · Desplazamiento | Corregir la visibilidad de destinos y la ayuda. Propuesta de control: marcha con joystick izquierdo, giro por pasos con derecho y teletransporte opcional; revisar conflictos de acciones antes de activarlo. Actualizar las pruebas que hoy exigen marcha desactivada. | Se puede llegar al paciente y al material en los tres entornos, con límites y colisiones coherentes, también tras cambiar de ejercicio. |
| P1 · Interacciones | Revisar alcance, agarre, traslado, liberación, colocación y señales de contacto de cada herramienta funcional; completar RCP y desobstrucción con mandos reales. | El usuario distingue qué puede manipular y completa las acciones propias de las nueve combinaciones sin teclado ni asistencia del desarrollador. |
| P2 · Realismo | Sustituir formas provisionales del material por modelos y materiales creíbles; conservar botiquines/DEA propios de espacios públicos; revisar postura, mirada y gestos de pacientes/testigos. | No aparecen elementos clínicos fuera de contexto; manos, objetos y pacientes mantienen escala y contacto convincentes. Las mejoras se revisan dentro del visor. |
| P2 · Cierre de entrega | Recorrer la matriz 3 × 3 hasta resultados; medir rendimiento en Quest; completar revisión clínica y preparar una versión identificable de distribución. | Hay evidencia por caso, tiempos de cuadro medidos en hardware y aprobación del contenido. El código de versión supera el último realmente publicado en Meta. |

El código de versión Android local todavía es 1. El flujo de publicación exige
un valor mayor que `LastUploadedVersionCode`, actualmente 1; antes de distribuir
hay que contrastar ese dato local con el canal de Meta. Cambiar solo el nombre
visible de versión no identifica suficientemente una nueva entrega.

### Qué ya está preparado y qué falta probar

- La bienvenida actual muestra versión y estado de cada mando, incluyendo
  gatillo y agarre, sin requerir seleccionar un botón. Está en
  `TrainingExperience.Vr.cs` y `TrainingExperience.Lifecycle.cs`. Es diagnóstico
  de entrada básica: ver «PULSADO» no demuestra que la selección XR funcione.
- Hay mejoras locales en calle, iluminación exterior, gestos de testigos y
  señales de contacto de desobstrucción. La
  [comparativa visual](../TestResults/visual-improvements/comparison.html)
  permite revisar las capturas; no representa una prueba en Quest.
- Evidencia local: [9/9 pruebas de controles y recorrido](../TestResults/controller-audit-20261013.xml),
  [2/2 de diagnóstico y agarre del DEA en los tres entornos](../TestResults/controller-grab-final-20261013.xml)
  y [16/16 de configuración y regresiones relacionadas](../TestResults/visual-improvements/configuration-final.xml).
  Son pruebas del Editor; el agarre simulado no prueba el transporte nativo de
  eventos de los mandos del visor ni todos los procedimientos.
- No se ha generado una nueva APK con estos cambios, ni instalado en Quest,
  medido rendimiento en hardware o publicado esta revisión en Meta.

### Cómo acotar el fallo en la próxima prueba física

1. Anotar versión y código de compilación; comprobar que ambos mandos funcionan
   en el menú del sistema y abrir la app desde cero.
2. En bienvenida, mover cada mando y pulsar gatillo y agarre por separado.
   Registrar lo que muestra el diagnóstico, el puntero y la interfaz.
3. Si no hay seguimiento ni entrada básica, investigar dispositivo, sesión y
   perfil OpenXR. Si hay entrada básica pero no responde la interfaz, investigar
   acciones, interactores, raycast y módulo de entrada de UI. Son rutas de
   diagnóstico, no causas ya confirmadas.
4. Entrar en práctica y en un ejercicio; agarrar y soltar el DEA en un caso de
   paro, comprobar desplazamiento y repetir pausa/reanudación. Si los menús
   responden pero el objeto no, revisar estado de la sesión, filtros, capas y
   distancia antes de atribuirlo a OpenXR.
5. Si se reproduce, conservar el registro de la app y el estado exacto de la
   pantalla. Asociar la corrección y la repetición de la prueba a esa misma
   compilación y dispositivo.

Referencias técnicas consultadas: [locomoción de XRI 3.3](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@3.3/manual/locomotion.html)
y [entrada de OpenXR 1.16](https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.16/manual/input.html),
contrastadas con el código instalado de ambas dependencias.

## Realismo de escenarios — 2026-10-13

El cliente aprobó mejorar el realismo de los tres entornos. No hay visor en el
equipo de desarrollo: toda comprobación en Quest la realiza el cliente con la
APK entregada. Las mejoras se montan con modelos y fotografías de uso libre
(CC BY 4.0 y CC0); los paquetes de pago quedan como opción, no como requisito.

### Cancha — hecha, pendiente de visto bueno

- Césped fotográfico, porterías con red a medida reglamentaria, dos gradas de
  aluminio y 17 álamos perimetrales. Atribuciones en
  `Assets/ThirdParty/FieldModels/CREDITS.md`; preparación reproducible con
  `tools/Prepare-FieldModels.py`.
- La cámara del visor dibujaba hasta 30 m y cortaba el fondo del campo. En
  cancha y calle el plano lejano pasa a 90 m y la niebla empieza a 30 m.
- Evidencia local: [capturas](../TestResults/field-realism/v3/), EditMode
  297/297, PlayMode 94/95 antes de corregir la prueba de bienvenida, y
  presupuesto de render en los nueve casos. La cancha sube de ~84 000 a
  ~206 000 triángulos (límite 300 000) sin añadir lotes. Es una medición del
  reproductor de Windows: el coste de los 90 m en Quest está sin medir.
- Falta en la cancha: banquillo cubierto, nevera y caseta siguen siendo formas
  simples; el tono del césped admite ajuste según opinión del cliente.

### Pendiente, por orden

| Orden | Trabajo | Estado |
| --- | --- | --- |
| 1 | **Calle.** Sustituir fachadas, coches, farolas, banco, papelera y árboles por modelos con textura fotográfica; añadir cielo e iluminación horneada (hoy no tiene). | Modelos elegidos y descargados; sin integrar. |
| 2 | **Gimnasio.** Ya usa modelos reales; revisar acabados y detalles. | Sin empezar. |
| 3 | **Cancha, segunda pasada.** Banquillo, caseta y accesorios (botellas, bolsas, petos). | Sin empezar. |
| 4 | **APK 0.1.1 de diagnóstico** con código de versión mayor que 1, para que el cliente pruebe mandos y rendimiento. | Sin generar. |
| 5 | **P1 del plan de Quest:** marcha con joystick, destinos de teletransporte visibles, agarre y uso de objetos. | Sin empezar. |

Los paquetes de pago valorados para la cancha (9,99–49,99 USD en Unity Asset
Store) no se han comprado; con el resultado actual no son necesarios.

## Historial de septiembre

Actualización 2026-09-13: la nueva entrega añade una demo Windows, un portal
preparado para Railway y cinco guiones médicos declarativos para revisión.
Ver [distribución](CLIENT_DEMO.md) y [alcance vigente](MVP_SCOPE_AND_MEDICAL_REVIEW.md).
Las fases siguientes conservan el contexto histórico de la base técnica.

## Alcance autorizado de esta iteración

Fase 1 y base de fase 2: Bootstrap → TrainingRoom → interacción con objeto y
paciente → inicio de caso técnico → transición de estado → registro → resumen.
No implementar todavía procedimientos de RCP, DEA, mediciones ni escenarios finales.

## Inspección inicial — 2026-09-07

- Carpeta completamente vacía, sin proyecto ni repositorio.
- Unity y Hub no encontrados en PATH, rutas de instalación habituales ni registro
  de aplicaciones instaladas. No se afirma haber buscado cada disco del equipo.
- Git disponible. ADB disponible. .NET runtime presente, SDK de .NET ausente.
- Compilador C# de .NET Framework disponible para pruebas puras locales.
- Sin evidencia de un visor conectado ni de una licencia Unity activada.

## Orden técnico

1. Verificar documentación y dependencias oficiales; fijar versiones.
2. Crear proyecto fuente Unity, estructura, ignorados Git y documentación.
3. Implementar dominio C# puro con validación, máquina de estados y evaluación.
4. Implementar adaptadores Unity y generador de escenas/prefabs/configuración.
5. Ejecutar pruebas puras y verificaciones estáticas disponibles.
6. Abrir/importar en Unity, generar la demo, validar OpenXR y ejecutar EditMode.
7. Compilar APK de desarrollo e instalar en Quest 3; pasar prueba física.

Los pasos 1–5 pueden realizarse en este entorno. Los pasos 6–7 dependen del
Editor instalado/licenciado y del visor; no se confunden con pruebas completadas.
El generador reduce el montaje manual y no reemplaza escenas ya existentes.

## Decisiones

- Unity 6.3 LTS 6000.3.23f1, verificado en las notas oficiales de publicación.
- XR Interaction Toolkit 3.3.2 y Starter Assets oficiales, mandos primero.
- OpenXR; no instalar Oculus Integration ni Meta XR SDK en esta fase.
- URP para un pipeline móvil mantenible y materiales de Starter Assets.
- ScriptableObjects como datos de autoría; copias inmutables en las sesiones.
- Sin servicios remotos, datos personales, assets comerciales ni audio externo.
- El ejemplo es una prueba de software, no un protocolo clínico aprobado.

## Roadmap posterior (fuera de esta entrega)

3. RCP: eventos de compresión, métricas y parámetros aprobados configurables.
4. DEA: flujo y decisiones de descarga configurables, sin deducirlas del diagnóstico.
5. Tensión, SpO2 y glucemia simuladas.
6. Casos clínicos y ramificaciones aprobadas.
7. Gimnasio, centro comercial, clínica dental, campo de fútbol.
8. Evaluación ampliada, audio, diálogos y presentación.
9. Optimización medida en Quest 3.
10. Preparación de publicación, sin publicar automáticamente.

No iniciar estas fases antes de validar la base en el visor.
