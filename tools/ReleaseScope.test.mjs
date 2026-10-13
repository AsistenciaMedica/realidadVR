import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createReleaseCatalog } from './Sync-MedicalPortal.mjs';

const read = path => JSON.parse(readFileSync(new URL('../' + path, import.meta.url), 'utf8'));
const library = read('Assets/_Project/Resources/MedicalScenarios.json');
const scope = read('Assets/_Project/Resources/ReleaseScope.json');
const clinical = read('Assets/_Project/ClinicalCases/Gym/SymptomaticHypotension/V2/Data/review-hypotension-v2.json');
const roster = read('Assets/_Project/Resources/PatientRoster.json');
const variants = read('Assets/_Project/Resources/ReleaseCases.json');

test('published catalog matches the Unity scope and preserves the authored cases', () => {
  const before = JSON.stringify(library);
  const catalog = createReleaseCatalog(library, scope, [clinical], roster, variants);
  assert.equal(catalog.scenarios.length, 9);
  assert.deepEqual(catalog.scenarios.map(s => s.id), scope.environments.flatMap(e => e.scenarioIds));
  assert.deepEqual(scope.environments.map(e => e.id), ['football', 'gym', 'street']);
  for (const e of scope.environments) assert.equal(catalog.scenarios.filter(s => s.environment === e.id).length, 3);
  assert.ok(!catalog.scenarios.some(s => s.environment === 'dental'));
  assert.deepEqual(read('demo/public/scenarios.json'), catalog, 'Run Sync-MedicalPortal.mjs before building');
  assert.equal(JSON.stringify(library), before);
  assert.equal(library.scenarios.length, 44);
  const daniel = catalog.scenarios.find(s => s.id === clinical.metadata.scenarioId);
  assert.equal(daniel.name, variants.cases.find(v => v.id === daniel.id).name);
  assert.equal(daniel.catalogOnly, true);
  assert.equal(daniel.initialState, undefined, 'Do not invent patient readings for the public V2 entry');
});

test('an invalid release fails before it can publish an incomplete or mismatched catalog', () => {
  for (const mutate of [
    p => p.environments.pop(),
    p => p.environments[0].scenarioIds.pop(),
    p => { p.environments[0].scenarioIds[1] = p.environments[0].scenarioIds[0]; },
    p => { p.environments[0].scenarioIds[1] = 'missing-case'; },
    p => { p.environments[0].scenarioIds[1] = 'gym-arrest'; },
    p => { p.environments[2].id = 'dental'; },
  ]) {
    const invalid = structuredClone(scope);
    mutate(invalid);
    assert.throws(() => createReleaseCatalog(library, invalid, [clinical], roster, variants));
  }
});

test('every environment repeats the same three exercises and preserves their mechanics', () => {
  const catalog = createReleaseCatalog(library, scope, [clinical], roster, variants);
  for (const environment of scope.environments) {
    const cases = catalog.scenarios.filter(s => s.environment === environment.id);
    assert.deepEqual(cases.map(s => s.trainingCase), ['dizziness', 'cardiac-arrest', 'choking']);
    assert.deepEqual(cases.map(s => s.sourceScenarioId), ['review-hypotension-v2', 'arrest-witnessed', 'choking-complete']);
    assert.equal(cases[0].catalogOnly, true);
    assert.equal(cases[1].initialState.position, 'supine');
    assert.equal(cases[2].initialState.position, 'standing');
    assert.equal(cases[2].initialDialogue, '');
    for (const entry of cases.slice(1)) {
      const source = library.scenarios.find(s => s.id === entry.sourceScenarioId);
      assert.deepEqual(entry.actions, source.actions);
      assert.deepEqual(entry.timeline, source.timeline);
    }
  }
  for (const mutate of [
    v => { v.cases[0].trainingCase = 'choking'; },
    v => { v.cases[2].sourceId = 'choking-partial'; },
    v => { v.cases[2].position = 'supine'; },
    v => { v.cases[0].environment = 'mall'; },
  ]) {
    const invalid = structuredClone(variants); mutate(invalid);
    assert.throws(() => createReleaseCatalog(library, scope, [clinical], roster, invalid));
  }
});

test('public patient identity matches Unity authoring for all nine combinations', () => {
  const catalog = createReleaseCatalog(library, scope, [clinical], roster, variants);
  assert.equal(new Set(catalog.scenarios.map(s => s.patientIdentity.id)).size, 3);
  assert.equal(new Set(catalog.scenarios.map(s => s.patientIdentity.appearanceResource)).size, 3);
  for (const entry of catalog.scenarios) {
    assert.deepEqual(entry.patientIdentity, roster.profiles.find(p => p.scenarioId === entry.id));
    if (entry.catalogOnly) continue;
    assert.equal(entry.initialState.patientName, entry.patientIdentity.displayName);
    assert.equal(entry.initialState.age, entry.patientIdentity.age);
    assert.equal(entry.initialState.sex, entry.patientIdentity.sex);
    assert.deepEqual(entry.variation.sexes, [entry.patientIdentity.sex]);
    if (entry.initialState.consciousness === 'Unresponsive') assert.equal(entry.initialDialogue, '');
  }
});

test('mismatched identities cannot be exported as a release', () => {
  for (const mutate of [
    p => p.profiles.pop(),
    p => { p.profiles[1].id = p.profiles[0].id; },
    p => { p.profiles[1].scenarioId = 'missing'; },
    p => { p.profiles[1].appearanceResource = 'Visual/Patient'; },
    p => { p.profiles.find(profile => profile.id === 'Daniel').age++; },
    p => { p.profiles.find(profile => profile.id === 'Andres').patientOpeningLine = 'Incorrect speech'; },
  ]) {
    const invalid = structuredClone(roster);
    mutate(invalid);
    assert.throws(() => createReleaseCatalog(library, scope, [clinical], invalid, variants));
  }
});
