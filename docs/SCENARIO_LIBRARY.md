# Catálogo de lanzamiento VITAL VR

**3 entornos, 3 pruebas compartidas por entorno: 9 combinaciones.** Cancha, gimnasio y calle.

En cada entorno: 1. Mareo y toma de síntomas; 2. Paro cardiorrespiratorio y RCP; 3. Atragantamiento y desobstrucción.

Alcance vigente: [lanzamiento 3 × 3](LAUNCH_SCOPE.md). La selección compartida por Unity y el portal está en `Assets/_Project/Resources/ReleaseScope.json`; las variantes reutilizan las reglas clínicas mediante `Assets/_Project/Resources/ReleaseCases.json`.
Daniel, Andrés y Camila conservan su identidad al cambiar de entorno; sus nueve perfiles de contexto se definen en `Assets/_Project/Resources/PatientRoster.json`.

La biblioteca de autoría conserva los 44 guiones originales en `Assets/_Project/Resources/MedicalScenarios.json` y el caso de Daniel V2 en su registro clínico. Clínica dental y los demás casos quedan para ampliaciones.

Las fichas públicas describen contenido en revisión; no acreditan aprobación clínica ni publicación en Meta. La ficha V2 exporta metadatos, sin duplicar su motor clínico.

Regenerar con `node tools/Sync-MedicalPortal.mjs`.

| ID | Paciente | Caso | Escenario | Dificultad | Revisión médica |
| --- | --- | --- | --- | --- | --- |
| football-dizziness | Daniel, 40 | 1. Mareo y toma de síntomas | Cancha | Inicial | CLIENT_REVIEW |
| arrest-witnessed | Andrés, 38 | 2. Paro cardiorrespiratorio y RCP | Cancha | Inicial | CLIENT_REVIEW |
| football-choking | Camila, 31 | 3. Atragantamiento y desobstrucción | Cancha | Inicial | CLIENT_REVIEW |
| review-hypotension-v2 | Daniel, 40 | 1. Mareo y toma de síntomas | Gimnasio | Inicial | CLIENT_REVIEW |
| gym-arrest | Andrés, 38 | 2. Paro cardiorrespiratorio y RCP | Gimnasio | Inicial | CLIENT_REVIEW |
| gym-choking | Camila, 31 | 3. Atragantamiento y desobstrucción | Gimnasio | Inicial | CLIENT_REVIEW |
| street-dizziness | Daniel, 40 | 1. Mareo y toma de síntomas | Calle | Inicial | CLIENT_REVIEW |
| street-arrest | Andrés, 38 | 2. Paro cardiorrespiratorio y RCP | Calle | Inicial | CLIENT_REVIEW |
| street-choking | Camila, 31 | 3. Atragantamiento y desobstrucción | Calle | Inicial | CLIENT_REVIEW |
