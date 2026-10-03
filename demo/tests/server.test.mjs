import { test } from 'node:test';
import assert from 'node:assert/strict';
import { once } from 'node:events';
import { mkdtemp, mkdir, writeFile, readFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { createDemoServer } from '../server.mjs';

async function fixture(t, options = {}) {
  const root = await mkdtemp(join(tmpdir(), 'emergencyvr-demo-'));
  await mkdir(join(root, 'public')); await mkdir(join(root, 'releases'));
  await writeFile(join(root, 'public', 'index.html'), '<h1>Emergency VR</h1>');
  await mkdir(join(root, 'web', 'dist'), { recursive: true });
  await writeFile(join(root, 'web', 'dist', 'index.html'), '<h1>Emergency VR</h1>');
  await writeFile(join(root, 'public', 'scenarios.json'), await readFile(new URL('../public/scenarios.json', import.meta.url)));
  const server = createDemoServer({ root, ...options }); server.listen(0, '127.0.0.1'); await once(server, 'listening');
  t.after(async () => { server.closeAllConnections(); await new Promise(resolve => server.close(resolve)); await rm(root, { recursive: true, force: true }); });
  return { root, url: `http://127.0.0.1:${server.address().port}` };
}
test('serves landing page and Railway health endpoint', async t => {
  const { url } = await fixture(t); const page = await fetch(url);
  assert.equal(page.status, 200); assert.match(await page.text(), /Emergency VR/);
  assert.equal(page.headers.get('x-content-type-options'), 'nosniff');
  assert.equal((await (await fetch(url + '/health')).json()).status, 'ok');
});
test('does not advertise a missing build', async t => {
  const { url } = await fixture(t); assert.equal((await (await fetch(url + '/api/releases')).json()).windows.available, false);
  assert.equal((await fetch(url + '/downloads/windows')).status, 404);
});

test('release environment and case deep links reload, excluded content returns 404', async t => {
  const { url } = await fixture(t);
  const catalog = await (await fetch(url + '/api/catalog')).json();
  assert.equal(catalog.scenarios.length, 15);
  assert.deepEqual(catalog.releaseScope.environments.map(environment => environment.id), ['gym', 'mall', 'football']);
  for (const environment of catalog.releaseScope.environments) {
    assert.equal(environment.scenarioIds.length, 5);
    assert.equal(catalog.scenarios.filter(scenario => scenario.environment === environment.id).length, 5);
    const page = await fetch(url + '/scenarios/' + environment.id);
    assert.equal(page.status, 200, environment.id);
    assert.match(await page.text(), /Emergency VR/);
    for (const id of environment.scenarioIds) assert.equal((await fetch(url + '/scenarios/' + id)).status, 200, id);
  }
  for (const id of ['dental', 'dental-arrest', 'review-hypotension-v1', 'not-a-scenario']) {
    assert.equal((await fetch(url + '/scenarios/' + id)).status, 404, id);
  }
});
test('serves real download, HEAD and resumable ranges', async t => {
  const { root, url } = await fixture(t); await writeFile(join(root, 'releases', 'EmergencyVR-Windows.zip'), '0123456789');
  const release = await (await fetch(url + '/api/releases')).json(); assert.equal(release.windows.available, true); assert.equal(release.windows.bytes, 10);
  const range = await fetch(url + '/downloads/windows', { headers: { Range: 'bytes=2-5' } }); assert.equal(range.status, 206); assert.equal(await range.text(), '2345');
  const head = await fetch(url + '/downloads/windows', { method: 'HEAD' }); assert.equal(head.headers.get('content-length'), '10'); assert.equal(await head.text(), '');
  assert.equal((await fetch(url + '/downloads/windows', { headers: { Range: 'bytes=99-' } })).status, 416);
});
test('allows HTTPS release links without an embedded zip', async t => {
  const { url } = await fixture(t, { windowsUrl: 'https://example.com/releases/demo.zip' });
  const data = await (await fetch(url + '/api/releases')).json(); assert.equal(data.windows.available, true); assert.equal(data.windows.url, 'https://example.com/releases/demo.zip');
  const redirect = await fetch(url + '/downloads/windows', { redirect: 'manual' }); assert.equal(redirect.status, 302);
});
test('does not expose server source, private files or accept writes', async t => {
  const { root, url } = await fixture(t); await writeFile(join(root, 'private.json'), '{"secret":true}');
  for (const path of ['/server.mjs', '/private.json', '/%2e%2e%2fprivate.json', '/%5c..%5cprivate.json']) { const response = await fetch(url + path); assert.ok([400, 404].includes(response.status)); assert.doesNotMatch(await response.text(), /secret/); }
  assert.equal((await fetch(url, { method: 'POST' })).status, 405);
});
