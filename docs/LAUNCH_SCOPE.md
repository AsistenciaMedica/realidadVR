# VITAL VR — lanzamiento 3 × 5

Decisión vigente del 30 de septiembre de 2026: **3 escenarios y 5 casos por
escenario, 15 casos en total**, para concentrar la primera versión comercial.
Escenarios elegidos por el usuario: **gimnasio, centro comercial y campo de fútbol**.
Este documento sustituye el alcance de lanzamiento anterior de cuatro entornos
y 44 variantes. No declara completada la validación clínica ni la publicación.

## Contenido incluido

| Escenario | Cinco casos seleccionados |
| --- | --- |
| Gimnasio | Daniel: malestar después del ejercicio (V2); pérdida breve de respuesta al llegar; hipoglucemia con confusión y deglución segura; dolor torácico con disnea; crisis asmática |
| Centro comercial | Inconsciente con respiración; sin respiración normal; obstrucción parcial con tos eficaz; paciente confuso; deshidratación con respuesta conservada |
| Campo de fútbol | Parada presenciada con DEA; recuperación de pérdida breve de conciencia; hipoglucemia en una espectadora; agotamiento por calor del organizador; dificultad respiratoria con fatiga en un espectador |

Se reutilizan casos existentes y sus interacciones. Daniel V2 conserva su motor
de valoración y comunicación. La iteración del 1 de octubre incorpora un paciente
distinto por caso y revisa contexto, conversación y evolución de los catorce
casos legacy; véase [pacientes y realismo](PATIENT_ROSTER.md). El tutorial de
controles permanece separado y no cuenta como un caso de los quince.

La [tabla de casos e IDs](SCENARIO_LIBRARY.md) se genera desde
`Assets/_Project/Resources/ReleaseScope.json`. Unity selecciona sus casos del mismo
perfil que publica el portal. Los 44 guiones originales y el registro V2 se
conservan para autoría; la clínica dental y los demás casos quedan fuera de la
selección de lanzamiento. Esta iteración reduce el alcance visible y el trabajo
de validación; no acredita una reducción del tamaño del APK ni elimina assets
compartidos de `Resources`.

## Funciones que se cierran para la primera versión

- Bienvenida, selección de escenario y caso, preparación e inicio.
- Práctica guiada y evaluación existentes, con controles por mandos.
- Interacciones requeridas por los quince casos, pausa, repetición y resultados.
- Resultado local y debrief según el motor de cada caso.
- Portal comercial y administración con los mismos tres escenarios y quince casos.

Las ampliaciones de escenarios, variantes, procedimientos y seguimiento de manos
se planifican después del lanzamiento. No se añaden como requisito de esta versión.

## Orden de cierre

1. **Fijar catálogo:** validar 3 × 5, navegación y correspondencia Unity/portal.
2. **Cerrar los quince casos:** recorrer cada uno desde selección hasta resultado;
   revisar contenido, instrucciones, acciones y desenlaces con el responsable
   clínico. Mantener `CLIENT_REVIEW` hasta disponer de la revisión correspondiente.
3. **Probar en Quest:** comprobar mandos, UI, confort, pausa del sistema,
   recuperación del seguimiento y rendimiento en los tres ambientes.
4. **Preparar candidato:** identidad de paquete definitiva, APK de producción
   firmado, permisos/manifiesto comprobados y prueba de instalación limpia.
5. **Preparar venta y envío:** precio, ficha, capturas del candidato real, política
   de privacidad publicada y clasificación por edades; cargar en el panel Meta,
   probar el binario distribuido y enviarlo a revisión.

El recorte concentra estas comprobaciones; no permite deducir una fecha de
aprobación ni afirmar que los quince casos tienen el mismo nivel de acabado.

## Meta: comprobaciones de publicación

Fuentes oficiales consultadas el 30 de septiembre de 2026. Revisar de nuevo los
requisitos contra el candidato que se vaya a enviar:

| Comprobación | Evidencia necesaria |
| --- | --- |
| Funcionamiento y contenido anunciado | Recorridos de los 15 casos y ficha fiel al binario; [VRC](https://developers.meta.com/vr/resources/publish-quest-req/) |
| Paquete de distribución | ARM64, firma APK v2 y manifiesto conforme; [empaquetado](https://developers.meta.com/vr/resources/vrc-quest-packaging-1/) |
| Interacción, foco y seguimiento | Arranque, pausa/reanudación del sistema, mandos y recuperación en visor; [VRC funcionales](https://developers.meta.com/vr/resources/publish-quest-req/) |
| Rendimiento | Medición en visor conforme al criterio vigente; [Performance.1](https://developers.meta.com/vr/resources/vrc-quest-performance-1/) |
| Privacidad | Política pública HTTPS coherente con los datos tratados; [Privacy.1](https://developers.meta.com/vr/resources/vrc-quest-privacy-1/) |
| Ficha y envío | Capturas, assets, idiomas, controles, clasificación IARC y precio; [metadatos](https://developers.meta.com/vr/resources/publish-app-metadata/) y [envío](https://developers.meta.com/vr/resources/publish-submit/) |

`QuestProjectSetup.BuildDevelopmentApk` genera una **APK de desarrollo** con el
identificador provisional `com.emergencyvr.trainingdemo`; no es evidencia de una
versión firmada para tienda. Los informes anteriores mantienen pendiente la
validación física en Quest. La cuenta Meta, firma de producción, precio y ficha
pública necesitan sus propias evidencias antes del envío.

## Mantener sincronizado el alcance

Desde la raíz del proyecto:

```powershell
node tools/Sync-MedicalPortal.mjs
node --test tools/ReleaseScope.test.mjs
npm.cmd --prefix demo run build
npm.cmd --prefix demo/web run build
npm.cmd --prefix demo test
```

La sincronización comprueba existencia, unicidad, entorno y disponibilidad antes
de escribir el catálogo. También actualiza `SCENARIO_LIBRARY.md`. La ficha pública
de Daniel V2 usa sus metadatos reales y no duplica el motor clínico ni presenta
lecturas inventadas. Compilar el frontend después de sincronizar evita servir
estadísticas o tarjetas de una versión anterior.

En Unity, ejecutar las pruebas de alcance y de integración del catálogo, además
del recorrido en visor. Las pruebas de la biblioteca completa siguen teniendo
sentido para conservar el contenido de futuras versiones.

## Verificación de esta reducción — 2026-09-30

- Unity 6000.3.23f1: compilación de scripts correcta; **51/51 EditMode** de
  `ReleaseScopeTests`, `ClinicalScenarioV2Tests` y `ReviewCatalogTests`.
- **20/20 PlayMode** de `MedicalIntegrationTests`, `TrainingExperienceTests`,
  `ArticulatedPresentationTests` y `ClinicalScenarioV2IntegrationTests`.
- Sincronización Unity/portal: **2/2** pruebas de alcance; portal: **17/17** pruebas
  Node; validación de distribución web y compilación React correctas.
- Navegador Edge: tres tarjetas, quince nombres reales, enlaces y recarga,
  exclusión de dental, detalle V2 y vista móvil sin errores JavaScript ni desborde.

Resultados locales: `TestResults/release-scope-editmode.xml`,
`TestResults/release-scope-playmode.xml` y `TestResults/vital-release-mobile.png`.
Estas pruebas se ejecutaron en el equipo de desarrollo. No se generó una APK de
producción ni se probó el contenido en un visor Quest ni se envió a Meta.

## Pacientes y compilación de revisión — 2026-10-01

Los quince casos tienen modelos, identidades y conversaciones propias, con
mejoras de contexto y evolución para primeros intervinientes y brigadistas.
La [documentación de pacientes](PATIENT_ROSTER.md) registra **263/263 EditMode**,
**49/49 PlayMode**, el recorrido de los quince pacientes en Windows y la
[galería de capturas reales](../TestResults/PatientRosterPreview/index.html).

Se generó una [APK de desarrollo ARM64](../Builds/Android/EmergencyVR-20261001-222416.apk)
de 201 MB, con las quince apariencias y mallas incluidas, aviso de licencia y
firma v2 de depuración verificada. Esta compilación conserva el paquete
provisional y necesita prueba física en Quest. La firma de producción y el
envío a Meta siguen pendientes; los catorce diálogos nuevos todavía son textuales.
