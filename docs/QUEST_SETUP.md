# Configuración exacta de Unity y Quest 3

Estado: procedimiento preparado y APIs contrastadas; no ejecutado en Unity en
esta entrega. No hay Editor/Hub detectados. La instalación/licencia, la cuenta
Meta y los permisos dentro del visor requieren tu intervención. No se instaló
software fuera de la carpeta del proyecto.

## 1. Instalar y abrir

1. Instalar [Unity Hub](https://unity.com/download) e iniciar sesión con tu cuenta.
2. Hub → Settings/Preferences → Licenses → Add license → elegir la licencia
   aplicable a tu cuenta. Completar la activación.
3. Abrir [Unity 6000.3.23f1](https://unity.com/releases/editor/whats-new/6000.3.23f1)
   → **Install** → abrir Unity Hub. Instalar el Editor Windows x64.
4. En los módulos marcar **Android Build Support**, expandirlo y marcar
   **Android SDK & NDK Tools** y **OpenJDK**. Si ya está instalado: Hub → Installs
   → engranaje/menú del Editor → Add modules → esas tres opciones → Install.
5. Hub → Projects → Add → **Add project from disk** →
   `C:\Users\Juan\Desktop\proyectovr` → abrir con 6000.3.23f1.
6. Esperar importación y resolución de paquetes. Si Input System pide habilitar
   el backend y reiniciar, aceptar. No actualizar paquetes automáticamente.
7. Window → General → Console: comprobar que no haya errores rojos de compilación.

No usar `New project` en esta carpeta. `ProjectSettings` está preparado con la
versión de Editor; Unity generará el resto de ajustes iniciales al abrirlo.

## 2. Crear la demo con los menús del proyecto

1. **Emergency VR → 1 - Import Starter Assets**. Esperar a que termine la
   recompilación. Importa solo la muestra oficial Starter Assets de XRI 3.3.2.
2. **Emergency VR → 2 - Generate demo**. Esperar el mensaje `Demo assets generated`.
   Se crean las escenas, rig, paciente, cubo, UI, caso y pipeline URP.
3. **Emergency VR → 3 - Configure Android OpenXR**. Esperar `Android configured`.
4. Si avisa de cambio en Active Input Handling: File → Save Project, cerrar
   Unity y reabrir desde Hub antes de Play Mode o build.
5. File → Build Profiles → **Android** → **Switch Platform**. Si la ventana usa
   perfiles personalizados: Add Build Profile → Android → Add Build Profile →
   seleccionar el nuevo perfil → **Switch Profile**. Mantener ajustes globales,
   sin overrides de Player Settings ni de Scene List en esta iteración.
6. Edit → Preferences → External Tools → Android: usar **JDK installed with Unity**,
   **Android SDK tools installed with Unity** y **Android NDK installed with Unity**.

El generador añade Bootstrap primero y TrainingRoom segundo a la lista global
de escenas. Mantiene escenas adicionales. Si las escenas de demo ya existen,
no se sobrescriben; los cambios posteriores se hacen en el Editor. Si una
generación falla, corregir el error indicado en Console antes de continuar.

## 3. Verificar lo que aplica el configurador

Aunque el menú 3 intenta configurar estas opciones por API, comprobarlas en la
primera apertura. Estas rutas también son el procedimiento alternativo si una
configuración del Editor no se puede automatizar.

| Ruta | Valor |
| --- | --- |
| Edit → Project Settings → Player → Android → Other Settings → Color Space | Linear |
| … → Auto Graphics API | Desmarcado |
| … → Graphics APIs | Solo Vulkan |
| … → Scripting Backend | IL2CPP |
| … → Target Architectures | ARM64 marcado, ARMv7 desmarcado |
| … → Active Input Handling | Input System Package (New); reiniciar si cambia |
| … → Minimum API Level | Android 12L / API 32, baseline local Quest 3 |
| … → Target API Level | Automatic (highest installed) |
| … → Package Name | com.emergencyvr.trainingdemo |
| Edit → Project Settings → XR Plug-in Management → Android | OpenXR marcado |
| … → Initialize XR on Startup | Marcado |
| XR Plug-in Management → OpenXR → pestaña Android → Render Mode | Single Pass Instanced |
| … → OpenXR Feature Groups / features | Meta Quest Support habilitado |
| … → Enabled Interaction Profiles → + | Oculus Touch Controller Profile |
| Edit → Project Settings → Graphics → Default Render Pipeline | Assets/_Project/Settings/QuestURP.asset |
| Edit → Project Settings → Quality → Render Pipeline Asset | QuestURP en cada nivel |

Los ajustes Android no activan por sí mismos un runtime XR de Windows para Play
Mode. Para la primera validación, usar APK standalone. Oculus Touch aquí es un
perfil del paquete OpenXR oficial; no instalar Oculus Integration.

El target API automático es para iterar localmente con el SDK del Editor. No es
una declaración de cumplimiento de los requisitos de publicación de Meta Store.
La feature Meta Quest Support genera las adaptaciones de manifiesto; no añadir
un AndroidManifest manual ni permisos innecesarios en esta fase.

## 4. Validar antes de compilar

1. Edit → Project Settings → XR Plug-in Management → **Project Validation** →
   pestaña **Android** → revisar los problemas → **Fix** en cada error aplicable.
   Los avisos restantes deben entenderse; no activar funciones ajenas al MVP.
   Revisar también las reglas de Starter Assets si el paquete las muestra.
2. Emergency VR → **4 - Validate project setup**. Debe terminar sin excepciones.
3. Window → General → **Test Runner** → **EditMode** → **Run All**.
   Esperado: 27 casos compartidos y 3 tests Unity, todos aprobados.
4. Project → Assets → _Project → Scenes → Bootstrap → doble clic en Bootstrap.
   El rig se encuentra en TrainingRoom, no es necesario duplicarlo en Bootstrap.

Play Mode sin runtime XR conectado no prueba mandos ni teleport. Las pruebas
EditMode no necesitan visor. Un simulador de entrada puede incorporarse después;
no está importado ni forma parte de esta entrega.

## 5. Preparar Quest 3

1. Completar cuenta de desarrollador/organización y verificación si Meta las
   solicita, siguiendo [Device Setup de Meta](https://developers.meta.com/horizon/documentation/native/android/mobile-device-setup/).
2. En el teléfono, abrir **Meta Horizon** con la misma cuenta del visor →
   Devices/Dispositivos → seleccionar Quest 3 → Headset settings/Configuración
   del visor → **Developer Mode/Modo de desarrollador** → activar. La ubicación
   del selector Devices puede variar según la versión de la app.
3. Conectar el visor al PC con cable USB-C de datos. Ponerse el visor y aceptar
   **Allow USB debugging**; marcar **Always allow from this computer** si es tu PC.
4. En PowerShell, ejecutar:

```powershell
adb devices -l
```

Debe aparecer el visor en estado `device`. `unauthorized` requiere aceptar el
diálogo en el visor. Una lista vacía requiere revisar cable, puerto y drivers;
no significa que la aplicación haya fallado. Si hay varios dispositivos, usar
`adb -s SERIAL ...` en los comandos siguientes con el serial del Quest.

## 6. Crear e instalar la APK

1. Emergency VR → **5 - Build development APK**.
2. Esperar `APK built: ...`. El archivo queda en
   `Builds/Android/EmergencyVR-AAAAMMDD-HHMMSS.apk`; cada build usa otro nombre.
3. Instalar usando el nombre real informado en Console:

```powershell
adb -s SERIAL install -r '.\Builds\Android\EmergencyVR-AAAAMMDD-HHMMSS.apk'
```

4. En el visor: Biblioteca de aplicaciones → filtro **Unknown Sources / Orígenes
   desconocidos** → **Emergency VR Technical Demo**. Ejecutar la prueba completa
   de [TESTING](TESTING.md). El filtro puede moverse entre versiones de Horizon OS.
5. Para diagnóstico, sin borrar registros del dispositivo:

```powershell
adb -s SERIAL logcat -s Unity
```

No se genera AAB, firma de producción ni publicación. El identificador de paquete
es provisional y deberá definirse con el cliente antes de distribuir comercialmente.

## Problemas frecuentes

- Menú Emergency VR ausente: resolver los errores de compilación de Console y
  comprobar la versión del Editor y las dependencias del manifest.
- Falta `XR Origin (XR Rig)`: ejecutar menú 1 y esperar; no añadir otro rig a mano.
- Materiales magenta: comprobar URP 17.3.0 y QuestURP asignado; revisar compilación
  de Shader Graph de Starter Assets. No importar shaders de otros pipelines.
- Sin botones VR: revisar un solo EventSystem, XRUIInputModule, world-space Canvas,
  TrackedDeviceGraphicRaycaster y cámara de rig asignada. El generador los enlaza.
- Sin teleport: apuntar a pads turquesa; comprobar máscara XRI bit 31, provider y
  ambos ControllerInputActionManager con Smooth Motion/Smooth Turn desactivados.
- Sin tracking en Play Mode de PC: Android OpenXR no configura un runtime Windows.
  Validar primero la APK. Quest Link sería otra configuración, no hecha aquí.
- Fallo de SDK/NDK/JDK: volver a los módulos del Editor y External Tools; no
  sustituirlos por versiones del Android SDK global sin revisar compatibilidad.

Referencias: [build Android en Unity 6.3](https://docs.unity3d.com/6000.3/Documentation/Manual/android-BuildProcess.html),
[configuración Meta](https://developers.meta.com/horizon/documentation/unity/unity-project-configuration/),
[Meta Quest Support](https://developers.meta.com/horizon/documentation/unity/unity-openxr-settings-quest/).
