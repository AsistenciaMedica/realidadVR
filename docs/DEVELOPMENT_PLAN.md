# Plan de desarrollo

## Alcance vigente — 2026-09-30

La primera versión comercial queda limitada a **gimnasio, centro comercial y campo
de fútbol, con cinco casos por escenario (15 total)**. El orden de cierre y las
comprobaciones para Meta están en [LAUNCH_SCOPE.md](LAUNCH_SCOPE.md).
El resto de este documento conserva el historial de fases de septiembre; sus
restricciones y cuatro entornos originales no definen el lanzamiento actual.

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
