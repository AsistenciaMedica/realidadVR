import http from 'node:http';
import { createReadStream } from 'node:fs';
import { stat, realpath, readFile } from 'node:fs/promises';
import { dirname, resolve, extname, sep } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { createApi } from './backend/api.mjs';

const directory = dirname(fileURLToPath(import.meta.url));
const types = { '.html': 'text/html; charset=utf-8', '.css': 'text/css; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.json': 'application/json; charset=utf-8', '.png': 'image/png', '.svg': 'image/svg+xml', '.zip': 'application/zip' };
export function createDemoServer({ root = directory, windowsUrl = process.env.EMERGENCYVR_WINDOWS_URL || '', backend = {} } = {}) {
  if (windowsUrl && new URL(windowsUrl).protocol !== 'https:') throw new Error('EMERGENCYVR_WINDOWS_URL must use HTTPS');
  const publicRoot = resolve(root, 'public');
  const reactRoot = resolve(root, 'web', 'dist');
  const zip = resolve(root, 'releases', 'EmergencyVR-Windows.zip');
  const api=createApi(root,backend);
  const server=http.createServer(async (req, res) => {
    res.setHeader('X-Content-Type-Options', 'nosniff');
    res.setHeader('Referrer-Policy', 'same-origin');
    res.setHeader('Content-Security-Policy', "default-src 'self'; img-src 'self'; script-src 'self'; style-src 'self'; object-src 'none'; frame-ancestors 'none'; base-uri 'none'");
    const json = (status, value) => { res.writeHead(status, { 'Content-Type': types['.json'], 'Cache-Control': 'no-store' }); res.end(req.method === 'HEAD' ? undefined : JSON.stringify(value)); };
    try {
      let pathname = decodeURIComponent(new URL(req.url, 'http://localhost').pathname);
      if (pathname.includes('\0') || pathname.includes('\\')) return json(400, { error: 'Invalid path' });
      if(await api(req,res,pathname)) return;
      if (!['GET', 'HEAD'].includes(req.method)) { res.setHeader('Allow', 'GET, HEAD'); return json(405, { error: 'Method not allowed' }); }
      if (pathname === '/health') return json(200, { status: 'ok' });
      if(pathname.startsWith('/scenarios/') && !api.hasScenario(pathname.slice(11)) && !api.hasEnvironment(pathname.slice(11)))return json(404,{error:'Escenario no encontrado.'});
      else if(['/admin','/admin/clients','/admin/licenses','/admin/activations','/admin/scenarios'].includes(pathname))pathname='/admin.html';
      const local = await stat(zip).then(s => s.isFile() ? s : null).catch(() => null);
      if (pathname === '/api/releases') {
        const metadata = await readFile(resolve(root, 'release.json'), 'utf8').then(s => JSON.parse(s.replace(/^\uFEFF/, ''))).catch(() => null);
        return json(200, { windows: { available: Boolean(local || windowsUrl), url: local ? '/downloads/windows' : windowsUrl || null, bytes: local?.size || null, metadata }, quest: { available: false }, web: { available: false } });
      }
      let file;
      const reactRoute = pathname === '/' || pathname === '/scenarios' || pathname.startsWith('/scenarios/') || pathname.startsWith('/assets/') || ['/technology', '/procedures', '/demo', '/about', '/contact'].includes(pathname);
      if (reactRoute) {
        const requested = pathname.startsWith('/assets/') ? resolve(reactRoot, '.' + pathname) : resolve(reactRoot, 'index.html');
        file = await realpath(requested);
        if (!file.startsWith(await realpath(reactRoot) + sep) || !types[extname(file)]) return json(404, { error: 'Not found' });
      } else if (pathname === '/downloads/windows') {
        if (!local) {
          if (windowsUrl) { res.writeHead(302, { Location: windowsUrl }); return res.end(); }
          return json(404, { error: 'Esta entrega todavía no incluye el ZIP de Windows.' });
        }
        file = zip;
        res.setHeader('Content-Disposition', 'attachment; filename="EmergencyVR-Windows.zip"');
      } else {
        const requested = resolve(publicRoot, '.' + (pathname === '/' ? '/index.html' : pathname));
        if (!requested.startsWith(publicRoot + sep)) return json(404, { error: 'Not found' });
        file = await realpath(requested);
        if (!file.startsWith(await realpath(publicRoot) + sep) || !types[extname(file)]) return json(404, { error: 'Not found' });
      }
      const info = await stat(file);
      if (!info.isFile()) return json(404, { error: 'Not found' });
      let start = 0, end = info.size - 1, status = 200;
      if (req.headers.range) {
        const match = /^bytes=(\d+)-(\d*)$/.exec(req.headers.range);
        start = match ? Number(match[1]) : -1;
        end = match?.[2] ? Math.min(Number(match[2]), end) : end;
        if (start < 0 || start > end || start >= info.size) { res.setHeader('Content-Range', `bytes */${info.size}`); return json(416, { error: 'Invalid range' }); }
        status = 206;
        res.setHeader('Content-Range', `bytes ${start}-${end}/${info.size}`);
      }
      res.writeHead(status, { 'Content-Type': types[extname(file)], 'Content-Length': end - start + 1, 'Accept-Ranges': 'bytes', 'Cache-Control': extname(file) === '.png' ? 'public, max-age=3600' : 'no-store' });
      if (req.method === 'HEAD') return res.end();
      const stream = createReadStream(file, { start, end });
      stream.on('error', () => res.destroy()); res.on('close', () => stream.destroy()); stream.pipe(res);
    } catch (error) {
      if (!res.headersSent) json(error.code === 'ENOENT' ? 404 : 400, { error: 'Recurso no disponible' });
      else res.destroy();
    }
  });
  server.on('close',()=>api.close());server.requestTimeout=15000;server.headersTimeout=10000;return server;
}
if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  const port = Number(process.env.PORT || 4310);
  createDemoServer().listen(port, '0.0.0.0', () => console.log(`Emergency VR demo listening on ${port}`));
}
