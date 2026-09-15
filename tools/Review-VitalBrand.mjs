// Lightweight local branding review. Uses an in-memory admin database and an
// already installed Playwright/Edge; no releases, archives or Unity builds.
import { createRequire } from 'node:module';
import { mkdirSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { randomBytes } from 'node:crypto';
import { createDemoServer } from '../demo/server.mjs';
import { passwordHash } from '../demo/backend/security.mjs';

const require = createRequire(import.meta.url);
const { chromium } = require(process.env.PLAYWRIGHT_CORE_PATH || resolve(process.env.APPDATA || '', 'npm/node_modules/@playwright/cli/node_modules/playwright-core'));
const root = fileURLToPath(new URL('../', import.meta.url));
const shots = resolve(root, 'docs/screenshots');
mkdirSync(shots, { recursive: true });
mkdirSync(resolve(root, 'TestResults'), { recursive: true });
const password = randomBytes(24).toString('base64url');
const server = createDemoServer({ root: resolve(root, 'demo'), backend: { dbPath: ':memory:', adminUser: 'brand-review', adminHash: await passwordHash(password), encryptionKey: randomBytes(32).toString('base64'), production: false, origin: '' } });
await new Promise(done => server.listen(0, '127.0.0.1', done));
const origin = `http://127.0.0.1:${server.address().port}`;
let browser;
const results = [];
const errors = [];
try {
  browser = await chromium.launch({ channel: 'msedge', headless: true });
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 }, deviceScaleFactor: 1 });
  page.on('pageerror', error => errors.push(error.message));
  page.on('response', response => { if (response.status() >= 400 && !response.url().endsWith('/api/auth/session')) errors.push(`${response.status()} ${response.url()}`); });
  async function inspect(path, screenshot, fullPage = false) {
    await page.goto(origin + path);
    await page.evaluate(async () => { await document.fonts.ready; await Promise.all([...document.images].map(image => image.decode().catch(() => {}))); });
    const result = await page.evaluate(() => ({ path: location.pathname, width: innerWidth, overflow: document.documentElement.scrollWidth > innerWidth + 1, missingImages: [...document.images].filter(image => !image.complete || image.naturalWidth === 0).map(image => image.src), theme: getComputedStyle(document.body).backgroundColor, logo: !!document.querySelector('img[src$="vital-vr-lockup.svg"]') }));
    results.push(result);
    if (result.overflow || result.missingImages.length || !result.logo) throw new Error('Branding layout or asset check failed: ' + JSON.stringify(result));
    if (screenshot) await page.screenshot({ path: resolve(shots, screenshot), fullPage });
  }
  await inspect('/', 'vital-brand-home.png');
  await inspect('/scenarios');
  await inspect('/about');
  await inspect('/contact');
  await inspect('/branding/guide.html', 'vital-brand-guide.png', true);
  await inspect('/admin', 'vital-brand-login.png');
  await page.getByLabel('Usuario', { exact: true }).fill('brand-review');
  await page.getByLabel('Contraseña', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Iniciar sesión' }).click();
  await page.locator('#admin-app').waitFor({ state: 'visible' });
  await page.getByRole('heading', { name: 'Cada equipo, preparado.' }).waitFor();
  await page.screenshot({ path: resolve(shots, 'vital-brand-admin.png') });
  await page.goto(origin + '/admin/clients');
  await page.getByRole('button', { name: 'Crear cliente' }).click();
  await page.locator('#editor').waitFor({ state: 'visible' });
  await page.screenshot({ path: resolve(shots, 'vital-brand-admin-form.png') });
  await page.getByRole('button', { name: 'Cerrar ×', exact: true }).click();
  await page.setViewportSize({ width: 390, height: 844 });
  await inspect('/', 'vital-brand-mobile.png');
  for (const path of ['/scenarios', '/about', '/contact', '/admin', '/admin/clients', '/admin/licenses', '/admin/activations', '/admin/scenarios', '/branding/guide.html']) await inspect(path);
  await page.goto(origin + '/admin/clients');
  await page.getByRole('button', { name: 'Crear cliente' }).click();
  const dialog = await page.locator('#editor').boundingBox();
  if (dialog.x < 0 || dialog.x + dialog.width > 391) throw new Error('Mobile admin dialog exceeds viewport');
  if (errors.length) throw new Error(errors.join('\n'));
  writeFileSync(resolve(root, 'TestResults/branding-browser.json'), JSON.stringify({ pass: true, checks: results, browserErrors: errors, adminLogin: 'PASS', adminDialog: 'PASS', mobileAdminDialog: 'PASS' }, null, 2));
  console.log(`Branding browser review PASS: ${results.length} desktop/mobile routes, all images loaded, admin login/dialog and mobile dialog. Screenshots: docs/screenshots/vital-brand-*.png`);
} finally {
  if (browser) await browser.close();
  server.closeAllConnections();
  await new Promise(done => server.close(done));
}
