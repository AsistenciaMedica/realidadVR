// Rasterize the repo's vector logos with an already installed browser.
// Requires the existing Playwright CLI installation; never installs packages.
import { createRequire } from 'node:module';
import { readFileSync, mkdirSync, writeFileSync, existsSync } from 'node:fs';
import { resolve, relative } from 'node:path';
import { fileURLToPath } from 'node:url';

const require = createRequire(import.meta.url);
const installed = process.env.PLAYWRIGHT_CORE_PATH || resolve(process.env.APPDATA || '', 'npm/node_modules/@playwright/cli/node_modules/playwright-core');
if (!existsSync(installed)) throw new Error('Use the existing Playwright installation via PLAYWRIGHT_CORE_PATH. No packages were installed.');
const { chromium } = require(installed);
const root = fileURLToPath(new URL('../', import.meta.url));
const branding = resolve(root, 'demo/public/branding');
const unity = resolve(root, 'Assets/_Project/Resources/Branding');
mkdirSync(unity, { recursive: true });
const exports = [
  ['vital-vr-lockup.svg', resolve(unity, 'VitalVRLockup.png'), 1024, 205],
  ['vital-vr-mark.svg', resolve(unity, 'VitalVRMark.png'), 256, 171],
  ['vital-vr-social.svg', resolve(branding, 'vital-vr-social.png'), 1200, 630],
  ['../vital-mark.svg', resolve(branding, 'vital-vr-touch.png'), 180, 180],
];
const browser = await chromium.launch({ channel: 'msedge', headless: true });
try {
  const page = await browser.newPage({ deviceScaleFactor: 1 });
  for (const [source, destination, width, height] of exports) {
    const svg = readFileSync(resolve(branding, source), 'utf8');
    await page.setViewportSize({ width, height });
    await page.setContent(`<html><head><style>html,body{margin:0;background:transparent;width:100%;height:100%;overflow:hidden}svg{display:block;width:100%;height:100%}</style></head><body>${svg}</body></html>`);
    await page.screenshot({ path: destination, omitBackground: true });
    console.log(`${relative(root, destination)} (${width} x ${height})`);
  }
} finally { await browser.close(); }
// Stable GUIDs, clamp edges, no mipmaps or read/write copy; UI only.
for (const [name, guid, max] of [
  ['VitalVRLockup.png', '7e1f2689e2e54b1483a6a96e031c83a1', 1024],
  ['VitalVRMark.png', 'f28c956a32824b22b41d1e2f6a44128d', 256],
]) {
  const file = resolve(unity, name + '.meta');
  if (!existsSync(file)) writeFileSync(file, `fileFormatVersion: 2\nguid: ${guid}\nTextureImporter:\n  externalObjects: {}\n  serializedVersion: 13\n  mipmaps:\n    enableMipMap: 0\n    sRGBTexture: 1\n  isReadable: 0\n  streamingMipmaps: 0\n  maxTextureSize: ${max}\n  textureSettings:\n    serializedVersion: 2\n    filterMode: 1\n    aniso: 1\n    wrapU: 1\n    wrapV: 1\n    wrapW: 1\n  nPOTScale: 0\n  alphaUsage: 1\n  alphaIsTransparency: 1\n  textureType: 0\n  textureShape: 1\n  platformSettings:\n  - serializedVersion: 4\n    buildTarget: DefaultTexturePlatform\n    maxTextureSize: ${max}\n    textureFormat: -1\n    textureCompression: 0\n    overridden: 0\n  userData: Vital VR UI branding\n  assetBundleName:\n  assetBundleVariant:\n`, 'utf8');
}
const folderMeta = unity + '.meta';
if (!existsSync(folderMeta)) writeFileSync(folderMeta, 'fileFormatVersion: 2\nguid: 8d214a7bfa0542e787cf7b0f1c1f03d2\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n', 'utf8');
