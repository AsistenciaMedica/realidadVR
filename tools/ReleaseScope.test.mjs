import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createReleaseCatalog } from './Sync-MedicalPortal.mjs';

const read = path => JSON.parse(readFileSync(new URL('../' + path, import.meta.url), 'utf8'));
const library = read('Assets/_Project/Resources/MedicalScenarios.json');
const scope = read('Assets/_Project/Resources/ReleaseScope.json');
const clinical = read('Assets/_Project/ClinicalCases/Gym/SymptomaticHypotension/V2/Data/review-hypotension-v2.json');
const roster = read('Assets/_Project/Resources/PatientRoster.json');

test('published catalog matches the Unity scope and preserves the authored cases', () => {
  const before = JSON.stringify(library);
  const catalog = createReleaseCatalog(library, scope, [clinical], roster);
  assert.equal(catalog.scenarios.length, 15);
  assert.deepEqual(catalog.scenarios.map(s => s.id), scope.environments.flatMap(e => e.scenarioIds));
  assert.deepEqual(scope.environments.map(e => e.id), ['gym', 'mall', 'football']);
  for (const e of scope.environments) assert.equal(catalog.scenarios.filter(s => s.environment === e.id).length, 5);
  assert.ok(!catalog.scenarios.some(s => s.environment === 'dental'));
  assert.deepEqual(read('demo/public/scenarios.json'), catalog, 'Run Sync-MedicalPortal.mjs before building');
  assert.equal(JSON.stringify(library), before);
  assert.equal(library.scenarios.length, 44);
  const daniel = catalog.scenarios.find(s => s.id === clinical.metadata.scenarioId);
  assert.equal(daniel.name, clinical.metadata.displayName);
  assert.equal(daniel.catalogOnly, true);
  assert.equal(daniel.initialState, undefined, 'Do not invent patient readings for the public V2 entry');
});

test('an invalid release fails before it can publish an incomplete or mismatched catalog', () => {
  for (const mutate of [
    p => p.environments.pop(),
    p => p.environments[0].scenarioIds.pop(),
    p => { p.environments[0].scenarioIds[1] = p.environments[0].scenarioIds[0]; },
    p => { p.environments[0].scenarioIds[1] = 'missing-case'; },
    p => { p.environments[0].scenarioIds[1] = 'arrest-witnessed'; },
    p => { p.environments[2].id = 'dental'; },
  ]) {
    const invalid = structuredClone(scope);
    mutate(invalid);
    assert.throws(() => createReleaseCatalog(library, invalid, [clinical], roster));
  }
});

test('public patient identity matches Unity authoring for all fifteen cases', () => {
  const catalog = createReleaseCatalog(library, scope, [clinical], roster);
  assert.equal(new Set(catalog.scenarios.map(s => s.patientIdentity.id)).size, 15);
  assert.equal(new Set(catalog.scenarios.map(s => s.patientIdentity.appearanceResource)).size, 15);
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
    p => { p.profiles.find(profile => profile.id === 'Rosa').patientOpeningLine = 'Incorrect speech'; },
  ]) {
    const invalid = structuredClone(roster);
    mutate(invalid);
    assert.throws(() => createReleaseCatalog(library, scope, [clinical], invalid));
  }
});
