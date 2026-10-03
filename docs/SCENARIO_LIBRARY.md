# Catálogo de lanzamiento VITAL VR

**3 escenarios, 5 casos por escenario: 15 casos en total.** Gimnasio, centro comercial y campo de fútbol.

Alcance vigente: [lanzamiento 3 × 5](LAUNCH_SCOPE.md). La selección compartida por Unity y el portal está en `Assets/_Project/Resources/ReleaseScope.json`.
Las quince identidades ficticias, su ropa y su contexto se definen en `Assets/_Project/Resources/PatientRoster.json`. La edad y el sexo permanecen coherentes con el personaje al repetir el intento.

La biblioteca de autoría conserva los 44 guiones originales en `Assets/_Project/Resources/MedicalScenarios.json` y el caso de Daniel V2 en su registro clínico. Clínica dental y los demás casos quedan para ampliaciones.

Las fichas públicas describen contenido en revisión; no acreditan aprobación clínica ni publicación en Meta. La ficha V2 exporta metadatos, sin duplicar su motor clínico.

Regenerar con `node tools/Sync-MedicalPortal.mjs`.

| ID | Paciente | Caso | Escenario | Dificultad | Revisión médica |
| --- | --- | --- | --- | --- | --- |
| review-hypotension-v2 | Daniel, 40 | Daniel · Malestar después del ejercicio | Gimnasio | Inicial | CLIENT_REVIEW |
| gym-faint | Lucía, 28 | Pérdida breve de respuesta en el gimnasio | Gimnasio | Inicial | CLIENT_REVIEW |
| glucose-moderate | Mateo, 32 | Hipoglucemia con confusión y deglución segura | Gimnasio | Inicial | CLIENT_REVIEW |
| chest-pain | Ricardo, 54 | Dolor torácico con disnea | Gimnasio | Inicial | CLIENT_REVIEW |
| asthma | Sara, 26 | Crisis asmática · inhalador propio | Gimnasio | Inicial | CLIENT_REVIEW |
| review-unconscious-breathing-v1 | Rosa, 32 | Inconsciente con respiración · piloto migrado | Centro comercial | Inicial | CLIENT_REVIEW |
| review-abnormal-breathing-v1 | Javier, 36 | Sin respiración normal · piloto migrado | Centro comercial | Inicial | CLIENT_REVIEW |
| choking-partial | Camila, 31 | Obstrucción parcial · tos eficaz | Centro comercial | Inicial | CLIENT_REVIEW |
| confusion | Beatriz, 44 | Paciente confuso · origen no confirmado | Centro comercial | Inicial | CLIENT_REVIEW |
| dehydration | Luis, 45 | Deshidratación con respuesta conservada | Centro comercial | Inicial | CLIENT_REVIEW |
| arrest-witnessed | Andrés, 38 | Parada presenciada · DEA precoz | Campo de fútbol | Inicial | CLIENT_REVIEW |
| football-faint | Pablo, 24 | Recuperación de pérdida breve de conciencia | Campo de fútbol | Inicial | CLIENT_REVIEW |
| football-glucose | Elena, 34 | Hipoglucemia entre el público del campo | Campo de fútbol | Inicial | CLIENT_REVIEW |
| heat-exhaustion | Sergio, 32 | Agotamiento por calor | Campo de fútbol | Inicial | CLIENT_REVIEW |
| hypoxia | Miguel, 42 | Hipoxia con fatiga | Campo de fútbol | Intermedio | CLIENT_REVIEW |
