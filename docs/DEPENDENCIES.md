# Versiones, dependencias y procedencia

Consulta realizada el 2026-09-07 mediante Context7, documentación oficial y
metadatos/código descargado del registro oficial de paquetes Unity.

| Componente | Versión fijada | Motivo y compatibilidad documental |
| --- | --- | --- |
| Unity Editor | 6000.3.23f1, revisión 09d2ecc7fb28 | Unity 6.3 LTS, release oficial del 26 de agosto de 2026 |
| XR Interaction Toolkit | 3.3.2 | Grab, selección, UI, teleport, snap; exige Unity 6000.0+ |
| OpenXR Plugin | 1.16.1 | Runtime Quest y perfiles de mandos; exige Unity 2022.3+ |
| XR Plug-in Management | 4.6.1 | Inicializa loader Android; incluido en las actualizaciones del Editor elegido |
| XR Core Utilities | 2.4.0 | XROrigin; requerido por XRI 3.3.2 |
| Input System | 1.17.0 | Acciones y bindings; supera mínimos de XRI 1.8.1 y OpenXR 1.6.3 |
| Universal RP | 17.3.0 | Pipeline de Unity 6.3; incluye Shader Graph necesario para Starter Assets |
| uGUI | 2.0.0 | Canvas/UI world-space; mínimo requerido por XRI 3.3.2 |
| Test Framework | 1.4.6 | NUnit en EditMode; declaración Unity 2019.4+ |

Las versiones están fijadas en `Packages/manifest.json`, sin rangos ni previews.
Unity resolverá las dependencias transitivas; **no se fabricó un packages-lock.json**.
Conservar/versionar el lock que genere Package Manager tras la primera importación.

URP 17.3 es un paquete core ligado a Unity 6.3. Su metadata no apareció en el
endpoint público general del registro; la versión y API `Create(rendererData)`
se verificaron en su documentación oficial. La resolución completa del conjunto
y compilación real quedan pendientes del Editor; cumplir mínimos no sustituye
esa prueba de integración.

Se inspeccionaron fuentes de XRI para nombres de namespaces, componentes, campos
serializados del rig y la máscara de teleport; fuentes de OpenXR y XR Management
para los métodos de configuración y IDs de features. Las descargas se guardaron
en `.cache/packages`, ignorado por Git, sin instalar herramientas en el sistema.

## Decisiones de dependencias

- No Meta XR SDK: esta base no usa passthrough, anclajes, extensiones de manos
  ni servicios Meta que lo requieran.
- No `com.unity.xr.oculus` ni Oculus Integration. El nombre **Oculus Touch
  Controller Profile** es el perfil oficial de entrada del paquete OpenXR;
  no supone instalar el SDK Oculus antiguo.
- `legacyinputhelpers` puede aparecer como dependencia transitiva oficial de
  OpenXR/XR Management; el código del proyecto no usa APIs Oculus heredadas.
- Starter Assets se importa desde XRI, sin copiar snippets de proyectos ajenos.
- No assets comerciales, paquetes de audio, red, telemetría ni analytics propios.

## Assets y licencias

Geometría del proyecto: primitivas generadas localmente. Materiales del proyecto:
colores simples, sin texturas externas. Texto: fuente integrada de Unity.
Rig, bindings y sus recursos visuales: muestra oficial Starter Assets. Conservar
licencias y notices del paquete Unity; no atribuir sus assets como propios.
No se ha concedido una licencia de distribución al código propio: la decisión
corresponde al titular del proyecto. Antes de distribuir comercialmente, revisar
la licencia de Unity elegida y registrar cualquier asset que se añada posteriormente.

## Fuentes

- [Release del Editor 6000.3.23f1](https://unity.com/releases/editor/whats-new/6000.3.23f1).
- [Soporte Unity 6](https://unity.com/releases/unity-6/support).
- [Starter Assets 3.3](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@3.3/manual/samples-starter-assets.html).
- [OpenXR 1.16](https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.16/manual/index.html).
- [URP core para Unity 6.3](https://docs.unity3d.com/6000.3/Documentation/Manual/com.unity.render-pipelines.universal.html).
- [API UniversalRenderPipelineAsset 17.3](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@17.3/api/UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset.html).
- [Configuración Meta Quest Support](https://developers.meta.com/horizon/documentation/unity/unity-openxr-settings-quest/).
- [Registro oficial XRI](https://packages.unity.com/com.unity.xr.interaction.toolkit),
  [OpenXR](https://packages.unity.com/com.unity.xr.openxr),
  [XR Management](https://packages.unity.com/com.unity.xr.management),
  [Input System](https://packages.unity.com/com.unity.inputsystem),
  [Test Framework](https://packages.unity.com/com.unity.test-framework).
