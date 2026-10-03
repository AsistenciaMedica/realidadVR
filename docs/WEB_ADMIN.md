# Vital VR Web + API

El portal existente incluye landing comercial, `/about`, `/contact`, catálogo
`/scenarios` y detalle `/scenarios/:id`. El branding usa navy, cyan y rojo y el
tagline «Entrena hoy. Salva vidas mañana.». El SVG provisional sigue la referencia
del visor con ECG suministrada; los PNG finales se pueden colocar en
`demo/public/branding/`.

## Ejecutar y comprobar

Requiere Node 24. El frontend React se compila antes de arrancar el servidor:

```powershell
npm --prefix demo/web ci
npm --prefix demo/web run build
npm --prefix demo run setup:local
npm --prefix demo start
npm --prefix demo run build
npm --prefix demo test
```

`setup:local` se ejecuta una sola vez. Guarda credenciales privadas de desarrollo
en `demo/data/ADMIN-LOGIN.txt` y hash/cifrado en `local-config.json`, ambos ignorados
por Git y excluidos del Docker y del staging. No regenerar la clave de cifrado de
una base de datos existente: se necesita para recuperar las licencias guardadas.

Abrir `http://localhost:4310/admin`. Hay un administrador configurado por instancia.
Las sesiones duran ocho horas y se invalidan al cerrar sesión o reiniciar el
proceso. El sitio público y la descarga no requieren autenticación; los datos
administrativos y todas sus modificaciones sí.

## Funciones

| Área | Implementación |
| --- | --- |
| Catálogo | Tres escenarios: gimnasio, centro comercial y campo de fútbol, con cinco casos cada uno (15 en total). El portal comercial muestra los cinco nombres por escenario; el administrador consulta el mismo catálogo. La vista heredada conserva los filtros y las fichas clínicas compatibles con cada motor. |
| Clientes | Crear, listar, buscar, editar, suspender/reactivar; detalle con licencias y dispositivos |
| Licencias | Generar, asignar, cambiar expiración y capacidad, suspender/reactivar, revocar permanentemente, revelar/copiar con autenticación |
| Activaciones | Activar, validar, desactivar; límite por licencia; reintentos del mismo dispositivo idempotentes; consulta/desactivación administrativa |
| Dashboard | Clientes y licencias activos, expiración a 30 días, dispositivos, escenarios, últimas activaciones, solicitudes de demo |
| Contacto | Formulario validado, consentimiento explícito y persistencia para consulta administrativa; no envía correo automáticamente |

El alcance de lanzamiento se declara en
`Assets/_Project/Resources/ReleaseScope.json`. La biblioteca médica completa se conserva en
`Assets/_Project/Resources/MedicalScenarios.json`; el caso de hipotensión seleccionado
usa su ficha v2 de `Assets/_Project/ClinicalCases/Gym/SymptomaticHypotension/V2/Data/`. Ejecutar
`node tools/Sync-MedicalPortal.mjs` desde la raíz al cambiarla; actualiza
`demo/public/scenarios.json` (15 casos y su `releaseScope`) y `docs/SCENARIO_LIBRARY.md`.
React carga este JSON para los escenarios, nombres de casos y conteos; volver a
compilar el frontend después de sincronizar. Las rutas `/scenarios/gym`,
`/scenarios/mall`, `/scenarios/football` y los 15 IDs de caso admiten recarga directa.
La clínica dental y los casos aplazados devuelven 404. No existe un segundo catálogo
médico manual ni un CMS de aprobación. La ficha v2 de hipotensión expone metadata
de catálogo (`catalogOnly`); no se interpreta con constantes o puntuación del motor antiguo.
Los casos permanecen `CLIENT_REVIEW` hasta la revisión responsable.

## API de licencias

`POST /api/licenses/activate`, `/validate` y `/deactivate` reciben JSON:

```json
{
  "licenseKey": "CLAVE_ENTREGADA_AL_CLIENTE",
  "deviceId": "identificador-local-persistente-del-dispositivo",
  "deviceName": "Equipo de formación",
  "platform": "WINDOWS"
}
```

`platform` acepta `WINDOWS` o `QUEST`. Primero activar; validar devuelve
`valid`, `client`, `expiresAt`, `features` y `checkAfterSeconds: 3600`. Una clave
suspendida, revocada, expirada o asignada a cliente inactivo deja de validar.
`ACTIVATION_REQUIRED`, `DEVICE_LIMIT` y `DEVICE_PLATFORM_MISMATCH` identifican
rechazos de dispositivo. Desactivar libera una plaza. No se ha conectado esta
API al arranque de Unity: la demo interna sigue funcionando sin licencia.

El bypass exige `DEVELOPMENT_LICENSE_BYPASS=true`, conexión loopback, clave
`DEVELOPMENT` y entorno de desarrollo. Producción rechaza el arranque si está
habilitado. No es DRM ni prueba de identidad física del visor: `deviceId` es un
identificador del cliente y una clave debe compartirse solo con su organización.

## Persistencia y acceso

`backend/store.mjs` usa SQLite nativo de Node, WAL, claves foráneas y transacciones
para el límite de dispositivos. Tablas: clients, licenses, activations, inquiries,
audit. No guarda resultados médicos, datos de pacientes ni telemetría Unity.

La contraseña se verifica con scrypt y sal aleatoria. Las licencias tienen
128 bits aleatorios; la búsqueda usa SHA256 y la recuperación usa AES-256-GCM.
Los identificadores de dispositivo se guardan mediante HMAC. Las listas no
exponen claves completas, hashes ni secretos. Las sesiones usan tokens aleatorios
en cookies HttpOnly, SameSite=Strict, Secure en producción y token CSRF para
modificaciones. Se validan origen, campos y tamaños; hay límites de peticiones
en memoria por dirección de conexión. Detrás de un proxy estos límites se
comparten por conexión de origen; no se confía en cabeceras reenviadas arbitrarias.

## Railway

El servicio está **preparado, no desplegado**. Elegir Root Directory `/demo` al
conectar el repo, o la carpeta de `Builds/RailwayDemo/<fecha>` generada por
`tools/Stage-ClientDemo.ps1`. `Dockerfile` usa Node 24 y `railway.json` comprueba
`/health`. Escucha en `0.0.0.0` y respeta el `PORT` de Railway.

Al conectar GitHub, seleccionar la rama `main`, **Root Directory `/demo`** y
**Config File Path `/demo/railway.json`**. La ruta del archivo de configuración
es relativa al repositorio completo, aunque el servicio use una subcarpeta.
Después, generar el dominio público, configurar las variables y el volumen
indicados abajo y desplegar. Ver [monorepos en Railway](https://docs.railway.com/deployments/monorepo).

| Variable / recurso | Valor requerido |
| --- | --- |
| `NODE_ENV` | `production` (ya incluido en Docker) |
| `PUBLIC_ORIGIN` | Origen HTTPS real, sin barra final, ruta, query ni credenciales |
| `ADMIN_USER` | Nombre del administrador; predeterminado `admin` |
| `ADMIN_PASSWORD_HASH` | Hash scrypt `salHex32:hashHex128`, generado con `passwordHash` de `backend/security.mjs` |
| `LICENSE_ENCRYPTION_KEY` | 32 bytes aleatorios en base64; conservar con la base de datos |
| `DEVELOPMENT_LICENSE_BYPASS` | `false` o sin definir |
| `DATABASE_PATH` | `/app/data/vital.sqlite` |
| Volumen persistente | Montar en `/app/data`; una sola réplica |
| `RAILWAY_RUN_UID` | `0` si el volumen root no admite escritura del usuario node, según la documentación Railway |
| `EMERGENCYVR_WINDOWS_URL` | URL HTTPS del ZIP cuando no se incluya ZIP local |

Generar secretos nuevos para producción, guardarlos en las variables del servicio
y en un gestor privado. No copiar `ADMIN-LOGIN.txt` ni la configuración local al
directorio público. El proceso falla antes de escuchar si faltan credenciales,
el hash es inválido, el origen no es HTTPS válido o el bypass está habilitado.
El volumen es imprescindible: sin él, los redeploys perderían la base de datos.
Programar backups del volumen y conservar la clave de cifrado separadamente;
restaurar una copia debe incluir la base consistente y esa misma clave.

Las sesiones en memoria se pierden en cada despliegue. Los volúmenes Railway no
admiten réplicas y pueden causar una breve pausa al redeplegar. Esta arquitectura
es para una administración pequeña; varias instancias requerirían sustituir el
almacén de sesiones y base por servicios compartidos.

Referencias consultadas: [Railway volumes](https://docs.railway.com/volumes/reference),
[Railway Dockerfiles](https://docs.railway.com/guides/dockerfiles).

Pendiente de configuración: cuenta/servicio Railway, dominio y secretos de
producción, volumen/backups, datos del titular para aviso legal y privacidad,
logo final aprobado y eventual integración de licencias en clientes Unity.
No se ha realizado una build Docker ni un despliegue Railway en este equipo.

## Verificación del alcance 3 × 5 (2026-09-30)

- Build de frontend estático y backend correcta; compilación React con Vite correcta.
- Tests Node: 17/17, incluidos los tres enlaces de escenario, los 15 enlaces de
  caso y el rechazo de clínica dental y del caso antiguo de hipotensión.
- Smoke Edge: conteos 3/15/5, los 15 nombres exactos del catálogo, recarga de
  enlaces, ficha v2 de hipotensión en el catálogo heredado y ausencia de errores JS.
- Home, catálogo y tres detalles en móvil 390×844 sin desbordamiento horizontal.
  Captura revisada: `TestResults/vital-release-mobile.png`.
- La página heredada conserva tres escenarios, pestañas e imagen ampliable.

Las pruebas de navegador usan `demo/tests/browser-server.mjs` (loopback 4311,
SQLite en memoria). Ejecutar `demo/tests/release-browser.js` y
`demo/tests/browser-check.js` mediante playwright-cli. Estas comprobaciones no
crean clientes, licencias ni solicitudes. El recorrido administrativo ampliado
permanece en `demo/tests/medical-browser.js`; no se ejecutó en este recorte.
