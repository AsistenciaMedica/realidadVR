# Validación de demo cliente — 2026-09-13

Unity 6000.3.23f1. Entrega Windows `0.2.0-review` compilada desde Bootstrap y
TrainingRoom, con teclado/ratón y sin exigir un visor conectado.

| Comprobación | Resultado | Evidencia local |
| --- | --- | --- |
| Core C# | 27/27 | `TestResults/core-tests.txt` |
| Unity EditMode | 37/37 (35 anteriores + 2 catálogo) | `TestResults/distribution-editmode.xml` |
| Unity PlayMode | 1/1, secuencia UI/paciente original | `TestResults/distribution-playmode.xml` |
| Build Windows | Correcta | `TestResults/distribution-windows-build.log` |
| Ejecutable Windows real | Selección de paciente por raycast, caso técnico y cinco pilotos a 100/100, exportación JSON | `TestResults/desktop-player.log`: `DESKTOP_SMOKE PASS` |
| Servidor portal | 5/5 | `node --test demo/tests/server.test.mjs` |
| Navegador Edge | Tabs, filtros, modal y ancho móvil correctos | `demo/tests/browser-check.js` |
| Descarga real | HEAD, tamaño, rango HTTP y firma ZIP correctos | `TestResults/distribution-download.json` |

El 100/100 comprueba el funcionamiento de la secuencia de software, no acredita
competencia clínica. Los pilotos tienen fuentes y siguen pendientes de validación.

## Artefactos

- ZIP completo: `demo/releases/EmergencyVR-Windows.zip` (39 785 170 bytes).
- SHA256: `448e2991c666c1be6544c7659b92ec786e578ae681aefe7e704ab99844e38a42`.
- Ejecutable: `Builds/Windows/20260913-085820/EmergencyVR.exe`.
- Portal con ZIP para Railway: `Builds/RailwayDemo/20260913-040403/`.
- Índice regenerable: `Builds/client-demo-release.json`.
- Capturas: `TestResults/client-portal-desktop.png`, `client-portal-mobile.png`.
- Captura de sala existente: `TestResults/training-room-polished.png`.

Los artefactos y resultados locales están ignorados por Git. Para una nueva
compilación, seguir `CLIENT_DEMO.md`; la fecha y hash de arriba describen esta entrega.

## Límites comprobados

No hay despliegue Railway ni dominio público: falta vincular el servicio/cuenta.
El Dockerfile está preparado; no se ejecutó una build Docker en este equipo.
El log del player muestra sondeos OpenXR sin runtime disponible y fallback de
decodificación de vídeo; la demo continuó y completó la prueba sin gafas.
No se ha validado en otro PC, con mandos ni en Quest 3, ni medido rendimiento VR.
No se añadieron otros escenarios, modelos externos ni paquetes; los ajustes XR,
URP y del proyecto se conservaron al finalizar. El Environment Builder y Core
no cambiaron.
