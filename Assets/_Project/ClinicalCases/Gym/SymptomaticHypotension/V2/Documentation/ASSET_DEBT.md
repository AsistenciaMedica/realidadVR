# VITAL VR — CASE 01: deuda de presentación tras Etapa B

El registro de recursos **no significa que los assets finales existan**. No se ha descargado, comprado, generado ni sustituido ningún avatar, clip, ropa o voz en esta etapa.

| Campo del ScriptableObject | Recurso asignado | Estado |
| --- | --- | --- |
| Patient prefab | Prefab compartido existente `Resources/Visual/Patient.prefab` | Provisional; conserva el asset actual. No acredita ropa deportiva, pose sentada ni animaciones CASE 01. |
| AnimatorController | Sin asignar | No se ha creado un controlador final CASE 01. |
| Animation clips | Vacío | No existen clips finales CASE 01 registrados aquí. |
| Audio clips | Vacío | Diálogo de texto/subtítulo disponible; voz profesional pendiente. No voz robótica presentada como final. |
| Materials | Vacío | No hay materiales nuevos de ropa, palidez o sudor CASE 01. |
| Equipment prefabs | Vacío | TA y SpO2 avanzados deshabilitados en I0; contratos no equivalen a instrumentos terminados. |
| Environment prefabs | Vacío | Sigue el entorno procedural compartido. No se ha construido una escena CASE 01 nueva. |
| Sprites / UI resources | Vacío | Se reutiliza la interfaz compartida VITAL VR; no hay UI assets específicos nuevos. |

SeatedPresyncope, SeekSupport, AssistedToSupine, SupineBreathing, WeakSupportedIdle, Recovering, RiseAttempt, RegainSupport, OfferArm, RestArm, OfferFinger, WithdrawFinger, CuffInflationReaction, NaturalBlink, GazeAttention, SpeechVisemes, PallorSweat y HandoverListening siguen requiriendo producción/integración y validación visual según la capacidad real del avatar. La respiración, rig y expresiones compartidos existentes no se renombran para fingir este conjunto de assets terminado.

El banco actual y la colocación del paciente no se han cambiado: la pose inicial sentada correcta y la transición asistida con suelo, apoyos y obstáculos pertenecen a etapas posteriores. Confirmar postura física en los tests verifica el contrato, no una animación o una colisión ya resueltas.

El testigo no tiene NPC nuevo. La llamada no tiene voces ni operador final. El listado vacío de equipamiento de I0 no retira funciones de los otros escenarios.

Las interpolaciones de persistencia/recurrencia requieren revisión de autoría clínica en Etapa E. No se habilitan estados de pérdida completa de consciencia. Tampoco se instala Animation Rigging ni se incorporan nuevas dependencias externas.

El rendimiento de Quest 3, comodidad, fidelidad corporal, ropa, mano/banco/pie/suelo, voz y artefactos visuales exigen QA de las fases de presentación, incluido hardware cuando esté disponible. Compilar un APK o pasar pruebas de datos no demuestra esos resultados.
