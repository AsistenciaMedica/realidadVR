# Identidad visual de Vital VR

La referencia principal es el visor cyan con pulso rojo de la imagen aportada:
**Vital** blanco en cursiva, **VR** rojo y fondo azul marino. Lema:
**Entrena hoy. Salva vidas mañana.** El logo se reconstruyó como SVG escalable;
las variantes del collage se usan como referencia de color e iconografía, sin
mezclar varios emblemas en la misma interfaz.

Ver la [guía visual](screenshots/vital-brand-guide.png),
la [portada web](screenshots/vital-brand-home.png) y
la [vista móvil](screenshots/vital-brand-mobile.png).
Con el portal local iniciado, abrir `/branding/guide.html` para consultar y
descargar las variantes SVG por separado.

## Paleta y tipografía

| Uso | Color |
| --- | --- |
| Fondo / azul marino | `#031123` |
| Tarjetas y paneles | `#0B2037` |
| Rojo del logo y pulso | `#ED1939` |
| Botón principal | `#C8102E` |
| Visor, foco y acentos | `#08ADD6` |
| Texto principal | `#F7F9FC` |
| Texto secundario | `#ADBED0` |
| Bordes | `#29425C` |

Web: Segoe UI con alternativas Arial/Helvetica del sistema, sin descargar fuentes.
Logo: Arial negrita cursiva. Unity conserva su fuente integrada para la UI y usa
el logo PNG para mantener la tipografía de marca. Texto blanco sobre botón rojo:
**5,58:1**; el hover mantiene contraste y añade un borde cyan. No se comunica
un estado únicamente mediante color.

## Recursos y aplicación

- `demo/public/branding/`: logos principales claro/oscuro, vertical transparente,
  horizontales transparentes claro/oscuro, símbolo, cuatro iconos, tarjeta social
  SVG/PNG, icono Apple Touch, `palette.json`, `tokens.css` y guía HTML/CSS.
- `demo/public/vital-mark.svg`: favicon cuadrado con fondo marino.
- Las cinco páginas web comparten cabecera, pie, colores, botones y formularios.
  Las cuatro públicas tienen metadatos sociales; administración conserva `noindex`.
  La imagen social usa una ruta local: al publicar se debe configurar su URL
  absoluta con el dominio definitivo para los lectores de Open Graph.
- `Assets/_Project/Resources/Branding/`: `VitalVRLockup.png` (1024×205) y
  `VitalVRMark.png` (256×171), transparentes, sin mipmaps ni copia CPU legible;
  aproximadamente 1 MB de textura RGBA sin comprimir entre ambos.
- `VitalBrand.cs`: paleta compartida, carga de logos, cabeceras y botones Unity.
  Aplicada al panel desktop, entrenamiento y selector de casos.
- Rótulos corporativos procedurales de gimnasio, clínica dental y club de fútbol.
  Los logos no capturan raycasts. La identidad acompaña la señalización; se
  conservan colores de seguridad, equipos y materiales clínicos.

Mantener proporciones del logo, espacio libre y variante adecuada al fondo.
Usar el símbolo en espacios pequeños, el horizontal en navegación y el vertical
en presentaciones. Evitar estirar el logo o aplicar el rojo corporativo a todos
los instrumentos clínicos.

## Regeneración y comprobación ligera

Desde la raíz del proyecto:

```powershell
node tools/Generate-VitalBrand.mjs
node tools/Render-VitalBrand.mjs
node tools/Review-VitalBrand.mjs
```

El primer script genera los SVG y tokens. Los otros dos usan la instalación
existente de Playwright y Edge; no instalan paquetes. Si Playwright está en otra
ubicación, establecer `PLAYWRIGHT_CORE_PATH` al módulo `playwright-core` instalado.
La revisión de administración usa una base en memoria y credenciales efímeras;
no modifica clientes, licencias ni contraseñas locales.

Comprobado en esta fase:

- Tests web: **16/16**.
- Navegador: **16 rutas/vistas de escritorio y móvil**, recursos cargados, sin
  desbordamiento horizontal, acceso admin y formulario en ambas resoluciones.
  Evidencia: `TestResults/branding-browser.json`.
- C# runtime: **compilación Roslyn correcta**, después de los cambios de UI y
  señalización. Evidencia: `TestResults/branding-runtime-check.log`.
- Capturas web: `docs/screenshots/vital-brand-*.png`.

No se ejecutó Unity ni se repitieron Core/EditMode/PlayMode en esta fase.
La importación y revisión visual de la nueva UI y los rótulos en Unity quedan
pendientes. Las capturas de entornos anteriores corresponden a la demo 0.4.0;
no muestran todavía esta nueva identidad en el simulador.

**No se recompiló Windows, no se ejecutó Stage-ClientDemo y no se regeneró ningún
ZIP.** El ejecutable y el ZIP publicados localmente siguen siendo los anteriores;
el branding Unity aparecerá al abrir el proyecto y en la próxima compilación.
