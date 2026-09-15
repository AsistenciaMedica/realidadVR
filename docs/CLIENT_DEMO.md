# Entrega de revisión — Windows, Unity y Railway

El portal ha evolucionado a Vital VR Web + API con catálogo y administración.
La configuración vigente de autenticación, licencias y despliegue está en
[WEB_ADMIN.md](WEB_ADMIN.md). El informe 0.2 enlazado al final es evidencia histórica.

## Probar sin gafas

Compilar desde **Emergency VR > Demo > Build Windows client demo** con Windows
como plataforma activa. La compilación usa Bootstrap y TrainingRoom existentes,
añade `EMERGENCYVR_DESKTOP` solo al player y restaura la inicialización XR del
Editor tras la compilación. No modifica los loaders ni los paquetes XR.

El ejecutable y su carpeta Data deben distribuirse juntos. Ejecutar:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Stage-ClientDemo.ps1
```

Esto crea `demo/releases/EmergencyVR-Windows.zip`, calcula SHA256 y prepara una
carpeta independiente `Builds/RailwayDemo/<fecha>` con portal y descarga. El ZIP
no se versiona en Git. No se incluyen repositorio Unity, contrato ni datos del cliente.

Controles Windows:

| Control | Acción |
| --- | --- |
| WASD / botón derecho | Caminar / mirar alrededor |
| Clic sobre paciente | Seleccionar al paciente |
| E | Coger o soltar equipo bajo el cursor |
| Q | Usar el equipo sujeto o apuntado: encender DEA, preparar parche o instrumento |
| Rueda del ratón | Acercar/alejar equipo sujeto entre 0,35 y 2 m |
| Z / X | Girar equipo horizontalmente (yaw) |
| Shift + Z / X | Inclinar equipo (pitch) |
| Alt + Z / X | Rodar equipo sobre su eje (roll), útil para orientar parches |
| C | Activar/desactivar RCP cerca del tórax, sin objeto sujeto |
| Clic y arrastrar hacia abajo en RCP | Comprimir tórax virtual; soltar permite retroceso |
| Esc / R | Ocultar/mostrar HUD / regresar al inicio |

Seleccionar caso, iniciar intento, realizar acciones/instrumentos, finalizar y
guardar el resultado JSON. El recorrido virtual de compresión no es profundidad
clínica calibrada. Las interacciones requieren revisión manual de ergonomía en
Windows y comprobación con mandos reales en Quest.

Los resultados locales están en
`%USERPROFILE%/AppData/LocalLow/EmergencyVR/Emergency VR Technical Demo/ReviewResults`.
No se envían al servidor. Incluyen identificador del caso, acciones, tiempos,
errores, omisiones, resultado, carácter provisional y fuentes.

Para revisar teclado/ratón en Editor:
**Emergency VR > Demo > Play with keyboard and mouse**. Al salir de Play se
restablece la selección de modo; abrir Bootstrap normalmente conserva XR.

## Meta XR Simulator y Quest

En Unity abrir Bootstrap y pulsar Play con la configuración XR existente.
El panel original conserva inicio/finalización y sus botones. A su lado aparece
un panel de casos y acciones; no añade un EventSystem ni raycaster nuevo.
Los callbacks existentes siguen manejando la selección del paciente.

Quest 3 conserva la ruta de compilación Android de QuestProjectSetup. Esta entrega
prioriza Windows; no declara una APK instalada, probada en visor ni publicada.
El mismo catálogo y panel están disponibles para la futura build Android.

## Portal local

```powershell
node demo/server.mjs
```

Abrir http://localhost:4310. No requiere npm install ni dependencias de terceros.
`node --test demo/tests/server.test.mjs` verifica servidor, descargas y rutas.
La descarga se activa únicamente si existe ZIP local o una URL HTTPS configurada.

## Railway

Hay dos formas de desplegar el artefacto preparado:

1. Desde la carpeta `Builds/RailwayDemo/<fecha>`, enlazar un servicio Railway y
   desplegar su contenido con Railway CLI (`railway link`, `railway up`). Esa
   carpeta contiene el ZIP completo y `railway.json`/Dockerfile.
2. Conectar el repositorio al servicio y establecer **Root Directory `/demo`**.
   Como Git no contiene el ZIP, configurar `EMERGENCYVR_WINDOWS_URL` con una URL
   HTTPS real de la versión publicada. Sin ella, el portal muestra descarga pendiente.

El servidor escucha en `0.0.0.0` y usa `PORT` asignado por Railway; healthcheck
`/health`. Generar un dominio público en Networking y comprobar página y descarga.
La administración requiere SQLite en un volumen persistente y las variables
de producción descritas en [WEB_ADMIN.md](WEB_ADMIN.md). Railway aloja el portal
y los archivos, no ejecuta una aplicación gráfica Unity Windows en el servidor.

No hay credenciales ni servicio Railway vinculado en este workspace. La preparación
local no implica que el portal esté desplegado. Antes de publicar, revisar la
visibilidad de la demo con el cliente. La web y descarga son públicas; `/admin`
y las API administrativas requieren autenticación.

Fuentes técnicas consultadas: [Unity Build scripts](https://docs.unity3d.com/Manual/build-script-build.html),
[Railway Dockerfiles](https://docs.railway.com/guides/dockerfiles).

Resultados de esta entrega: [validación y artefactos](CLIENT_DEMO_VALIDATION.md).

## Archivos principales

- `Assets/_Project/Scripts/Desktop/DesktopDemoController.cs`: adaptador exclusivo de demo PC.
- `Assets/_Project/Scripts/Scenarios/ReviewCaseCatalog.cs`, `ReviewCaseSession.cs`: catálogo y sesión de revisión.
- `Assets/_Project/Scripts/UI/ReviewCasePanel.cs`: presentación VR del catálogo.
- `Assets/_Project/Editor/DemoDistribution/`: generación de casos y build Windows.
- `Assets/_Project/Resources/ReviewCaseCatalog.asset`: seis entradas, incluida la demo original.
- `Assets/_Project/ScriptableObjects/Cases/Review/`: cinco guiones editables.
- `demo/`: portal, imagen real, matriz de avance, servidor y despliegue.
- `tools/Stage-ClientDemo.ps1`: distribución reproducible.

No editar los casos generados mediante el código esperando que se sobrescriban:
el generador preserva los assets existentes para permitir su revisión clínica
en Inspector. Puntos, secuencias y desenlaces no se consideran aprobados.
