import fs from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const sourcePath = 'Assets/_Project/Resources/MedicalScenarios.json';
const scopePath = 'Assets/_Project/Resources/ReleaseScope.json';
const rosterPath = 'Assets/_Project/Resources/PatientRoster.json';
const variantsPath = 'Assets/_Project/Resources/ReleaseCases.json';
const clinicalPath = 'Assets/_Project/ClinicalCases/Gym/SymptomaticHypotension/V2/Data/review-hypotension-v2.json';
const read = path => JSON.parse(fs.readFileSync(resolve(root, path), 'utf8'));

// This is a public catalog entry, not a second implementation of the V2 engine.
export function clinicalCatalogEntry(definition) {
  const m = definition.metadata;
  if (!m?.scenarioId || !m.displayName || !m.environment || !Array.isArray(m.referenceIds))
    throw Error('Incomplete clinical catalog metadata');
  return {
    id: m.scenarioId, name: m.displayName, category: m.category, difficulty: m.difficulty,
    environment: m.environment, description: m.description, history: m.history || '',
    incident: m.incident, version: m.scenarioVersion, references: m.referenceIds,
    medicalValidationStatus: 'CLIENT_REVIEW', availability: 'AVAILABLE',
    catalogOnly: true, clinicalEngineVersion: 2,
    symptoms: [], visibleSigns: [], actions: [], recommendedSequence: [], timeline: [], outcomes: [],
    debrief: ['Valoración, comunicación y solicitud de ayuda en el simulador. Pendiente de revisión clínica.'],
  };
}

export function createReleaseCatalog(library, scope, clinicalDefinitions, roster, variants) {
  if (library.schemaVersion !== 3 || !Array.isArray(library.scenarios))
    throw Error('Invalid medical library');
  if (scope.schemaVersion !== 1 || !scope.releaseId || scope.environments?.length !== 3)
    throw Error('Release scope must contain exactly three environments');
  if (roster?.schemaVersion !== 1 || roster.profiles?.length !== 9 ||
      roster.profiles.some(p => !p) || new Set(roster.profiles.map(p => p.scenarioId)).size !== 9)
    throw Error('Release requires one patient profile for each of the nine cases');
  if (variants?.schemaVersion !== 1 || variants.cases?.length !== 9 || variants.cases.some(v => !v) ||
      new Set(variants.cases.map(v => v.id)).size !== 9)
    throw Error('Release requires nine unique exercise variants');
  const caseTypes = ['dizziness', 'cardiac-arrest', 'choking'];
  const sources = ['review-hypotension-v2', 'arrest-witnessed', 'choking-complete'];
  const postures = ['seated', 'supine', 'standing'];
  for (const profile of roster.profiles) {
    const samePerson = roster.profiles.find(p => p.id === profile.id);
    for (const key of ['displayName', 'age', 'sex', 'appearanceResource'])
      if (samePerson[key] !== profile[key]) throw Error('Recurring patient identity must remain consistent');
  }
  const all = [...library.scenarios, ...clinicalDefinitions.map(clinicalCatalogEntry)];
  const byId = new Map(all.map(s => [s.id, s]));
  if (byId.size !== all.length) throw Error('Duplicate source scenario ID');
  const environments = new Set(), ids = new Set(), scenarios = [];
  for (const environment of scope.environments) {
    if (!['gym', 'street', 'football'].includes(environment.id) || environments.has(environment.id) ||
        !environment.name || environment.scenarioIds?.length !== 3)
      throw Error('Release requires football, gym and street with three cases each');
    environments.add(environment.id);
    for (const [index, id] of environment.scenarioIds.entries()) {
      const variant = variants.cases.find(v => v.id === id);
      const source = byId.get(variant?.sourceId);
      if (!source || variant.environment !== environment.id || ids.has(id) ||
          variant.trainingCase !== caseTypes[index] || !variant.name || !variant.description || !variant.incident ||
          variant.sourceId !== sources[index] || variant.position !== postures[index] ||
          (byId.has(id) && id !== variant.sourceId))
        throw Error('Missing, duplicated or incorrectly assigned release case: ' + id);
      const scenario = { ...structuredClone(source), id, sourceScenarioId: variant.sourceId,
        trainingCase: variant.trainingCase, environment: variant.environment, name: variant.name,
        description: variant.description, history: variant.history || '', incident: variant.incident,
        symptoms: structuredClone(variant.symptoms), visibleSigns: structuredClone(variant.visibleSigns) };
      if (scenario.availability !== 'AVAILABLE') throw Error('Unavailable release case: ' + id);
      if (scenario.references.some(ref => !library.references.some(r => r.id === ref)))
        throw Error('Unknown reference in release case: ' + id);
      ids.add(id);
      const profile = roster.profiles.find(p => p.scenarioId === id);
      if (!profile || !/^[a-zA-Z0-9]+$/.test(profile.id) || !profile.displayName ||
          !Number.isInteger(profile.age) || profile.age < 18 || profile.age > 100 ||
          !['female', 'male'].includes(profile.sex) || profile.appearanceResource !== 'Visual/Appearances/' + profile.id ||
          !profile.role || !profile.context || !profile.presentingComplaint || !profile.clothingDescription ||
          !profile.witnessLines?.length || profile.witnessLines.some(line => !line?.trim()))
        throw Error('Incomplete release patient: ' + id);
      const clinical = clinicalDefinitions.find(d => d.metadata.scenarioId === variant.sourceId);
      if (clinical && (clinical.patientProfile.name !== profile.displayName ||
          clinical.patientProfile.age !== profile.age || clinical.patientProfile.sex !== profile.sex))
        throw Error('Patient identity disagrees with clinical V2 data: ' + id);
      const entry = structuredClone(scenario);
      entry.patientIdentity = structuredClone(profile);
      if (!entry.catalogOnly) {
        if (entry.initialState.consciousness === 'Unresponsive' && profile.patientOpeningLine?.trim())
          throw Error('An unresponsive patient cannot have opening speech: ' + id);
        Object.assign(entry.initialState, { patientId: profile.id, patientName: profile.displayName,
          age: profile.age, sex: profile.sex, appearance: profile.clothingDescription, dialogue: profile.patientOpeningLine || '', position: variant.position });
        Object.assign(entry.variation, { minAge: profile.age, maxAge: profile.age, sexes: [profile.sex],
          positions: [entry.initialState.position], witnessStatements: [...profile.witnessLines] });
        entry.initialDialogue = profile.patientOpeningLine || '';
      }
      scenarios.push(entry);
    }
  }
  return { ...library, releaseScope: scope, scenarios };
}

export function syncReleaseCatalog() {
  const catalog = createReleaseCatalog(read(sourcePath), read(scopePath), [read(clinicalPath)], read(rosterPath), read(variantsPath));
  const names = Object.fromEntries(catalog.releaseScope.environments.map(e => [e.id, e.name]));
  const rows = catalog.scenarios.map(s => `| ${s.id} | ${s.patientIdentity.displayName}, ${s.patientIdentity.age} | ${s.name} | ${names[s.environment]} | ${s.difficulty} | ${s.medicalValidationStatus} |`);
  fs.writeFileSync(resolve(root, 'demo/public/scenarios.json'), JSON.stringify(catalog, null, 2) + '\n');
  fs.writeFileSync(resolve(root, 'docs/SCENARIO_LIBRARY.md'), `# Catálogo de lanzamiento VITAL VR\n\n` +
    `**3 entornos, 3 pruebas compartidas por entorno: 9 combinaciones.** Cancha, gimnasio y calle.\n\n` +
    `En cada entorno: 1. Mareo y toma de síntomas; 2. Paro cardiorrespiratorio y RCP; 3. Atragantamiento y desobstrucción.\n\n` +
    `Alcance vigente: [lanzamiento 3 × 3](LAUNCH_SCOPE.md). La selección compartida por Unity y el portal está en \`${scopePath}\`; las variantes reutilizan las reglas clínicas mediante \`${variantsPath}\`.\n` +
    `Daniel, Andrés y Camila conservan su identidad al cambiar de entorno; sus nueve perfiles de contexto se definen en \`${rosterPath}\`.\n\n` +
    `La biblioteca de autoría conserva los 44 guiones originales en \`${sourcePath}\` y el caso de Daniel V2 en su registro clínico. Clínica dental y los demás casos quedan para ampliaciones.\n\n` +
    `Las fichas públicas describen contenido en revisión; no acreditan aprobación clínica ni publicación en Meta. La ficha V2 exporta metadatos, sin duplicar su motor clínico.\n\n` +
    `Regenerar con \`node tools/Sync-MedicalPortal.mjs\`.\n\n` +
    `| ID | Paciente | Caso | Escenario | Dificultad | Revisión médica |\n| --- | --- | --- | --- | --- | --- |\n` + rows.join('\n') + '\n');
  console.log('Synced release ' + catalog.releaseScope.releaseId + ': 3 environments, 3 shared cases each, 9 total.');
  return catalog;
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) syncReleaseCatalog();
