import { readFile, writeFile, mkdir, copyFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const captures = path.resolve(root, process.argv[2] || 'TestResults/reunion-20261005/final-v4');
const destination = path.resolve(root, 'Builds/Reunion-20261006/Revision');
const scope = JSON.parse(await readFile(path.join(root, 'Assets/_Project/Resources/ReleaseScope.json'), 'utf8'));
const catalog = JSON.parse(await readFile(path.join(root, 'demo/public/scenarios.json'), 'utf8'));
const captured = new Set((await readFile(path.join(captures, 'cases.txt'), 'utf8')).trim().split(/\r?\n/));
const escape = value => String(value).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
const entries = scope.environments.flatMap(environment => environment.scenarioIds.map(id => ({ id, environment })));
if (entries.length !== 15 || captured.size !== 15) throw Error('The meeting review requires all 15 independently captured cases.');
await mkdir(path.join(destination, 'imagenes'), { recursive: true });
await mkdir(path.join(root, 'demo/public/media/scenarios'), { recursive: true });
for (const { id, environment } of entries) {
  if (!captured.has(`${id} | ${environment.id}`)) throw Error(`Wrong captured environment for ${id}`);
  for (const view of ['room', 'patient']) {
    const filename = `${id}-${view}.png`;
    const source = path.join(captures, filename);
    const bytes = await readFile(source);
    if (bytes.subarray(0, 8).toString('hex') !== '89504e470d0a1a0a' || bytes.readUInt32BE(16) !== 1440 || bytes.readUInt32BE(20) !== 810) throw Error(`Invalid capture ${filename}`);
    await copyFile(source, path.join(destination, 'imagenes', filename));
  }
}
const heroes = { gym: 'review-hypotension-v2', mall: 'confusion', football: 'football-glucose' };
for (const [environment, id] of Object.entries(heroes))
  await copyFile(path.join(captures, `${id}-room.png`), path.join(root, `demo/public/media/scenarios/${environment}.png`));

const cards = entries.map(({ id, environment }, index) => {
  const scenario = catalog.scenarios.find(s => s.id === id);
  if (!scenario) throw Error(`Missing catalog entry ${id}`);
  const title = scenario.patientIdentity?.displayName || scenario.name;
  return `<article data-environment="${escape(environment.id)}"><a class="image-link" href="imagenes/${id}-room.png" target="_blank" rel="noopener"><img loading="lazy" width="1440" height="810" src="imagenes/${id}-room.png" data-case="${id}" alt="${escape(title)} en ${escape(environment.name)}"></a><div class="card-copy"><span class="number">${String(index + 1).padStart(2, '0')} / ${escape(environment.name)}</span><h3>${escape(title)}</h3><p>${escape(scenario.patientIdentity?.role || scenario.name)}</p></div></article>`;
}).join('\n');
const html = `<!doctype html><html lang="es"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>VITAL VR · Revisión de avances</title><style>
:root{font-family:system-ui,sans-serif;color:#edf4f7;background:#09141d;font-synthesis:none}*{box-sizing:border-box}body{margin:0}main{max-width:1320px;margin:auto;padding:42px 26px 64px}header{border-bottom:1px solid #29404c;padding-bottom:34px}.brand{font-weight:800;letter-spacing:.06em}.brand b{color:#ed1939}.eyebrow{font-size:12px;letter-spacing:.13em;color:#73cdde;text-transform:uppercase;margin:32px 0 12px}h1{font-size:clamp(32px,5vw,58px);letter-spacing:-.045em;line-height:1.05;margin:0 0 18px}h2{font-size:25px;letter-spacing:-.03em}p{line-height:1.65;color:#aec0cd;max-width:850px}nav,.views{display:flex;gap:9px;flex-wrap:wrap;margin:22px 0}button{font:inherit;color:#dceaf0;background:#132936;border:1px solid #3a515e;border-radius:8px;padding:10px 17px;cursor:pointer}button[aria-pressed=true]{background:#d5f3f5;color:#103442;border-color:#d5f3f5}button:focus-visible,a:focus-visible{outline:3px solid #6ae7f3;outline-offset:4px}.summary{display:grid;grid-template-columns:repeat(3,1fr);gap:20px;margin:30px 0}.summary div{border-left:2px solid #57bbce;padding-left:18px}.summary strong{display:block;font-size:20px}.summary p{font-size:14px;margin-bottom:0}.toolbar{display:flex;align-items:center;justify-content:space-between;gap:20px;flex-wrap:wrap}#count{font-size:14px;color:#aec0cd}.gallery{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:18px}article{background:#10232f;border:1px solid #2b414d;border-radius:12px;overflow:hidden}article[hidden]{display:none}img{display:block;width:100%;height:auto;aspect-ratio:16/9;object-fit:cover}.card-copy{padding:18px}.number{font-size:11px;text-transform:uppercase;letter-spacing:.1em;color:#76c7d7}h3{font-size:21px;margin:9px 0}.card-copy p{font-size:14px;margin:0}footer{margin-top:42px;border-top:1px solid #29404c;padding-top:22px}footer p{font-size:14px}.route{display:grid;grid-template-columns:repeat(3,1fr);gap:20px;margin:24px 0 32px}.route strong{color:#edf4f7}a{color:#93e5ec}@media(max-width:850px){.gallery{grid-template-columns:repeat(2,minmax(0,1fr))}.summary,.route{grid-template-columns:1fr}}@media(max-width:540px){.gallery{grid-template-columns:1fr}main{padding:28px 18px}.views{margin-top:0}}
</style><main><header><div class="brand">VITAL <b>VR</b></div><p class="eyebrow">Revisión de avances · 6 de octubre de 2026</p><h1>Una experiencia.<br>Tres entornos. Quince casos.</h1><p>Recorrido visual de la versión en desarrollo: gimnasio, centro comercial y campo de fútbol. Las imágenes proceden de la aplicación con el perfil gráfico de Quest en Unity, sin retoque visual.</p></header>
<section class="summary" aria-label="Cambios de esta revisión"><div><strong>Espacios con más detalle</strong><p>Escaparates con volumen, materiales a escala, luminarias, mobiliario y detalles del campo.</p></div><div><strong>Personajes e iluminación</strong><p>Articulación facial ampliada, luz direccional y contactos con el suelo que siguen la postura.</p></div><div><strong>Conversaciones más claras</strong><p>El ambiente baja durante el diálogo; la boca responde al audio y las expresiones acompañan al estado del paciente.</p></div></section>
<h2>Recorrido sugerido · 8 minutos</h2><div class="route"><p><strong>1. Gimnasio</strong><br>Abre Daniel. Observa su actuación, conversa y prueba una medición. Finaliza para revisar el resultado.</p><p><strong>2. Centro comercial</strong><br>Elige un caso distinto. Revisa la profundidad de los escaparates, los testigos y las conversaciones disponibles.</p><p><strong>3. Campo de fútbol</strong><br>Explora el entorno y el equipo de atención. Prueba el caso de Andrés y revisa las interacciones con el DEA.</p></div>
<h2>Los quince casos</h2><nav aria-label="Filtrar entorno"><button data-filter="all" aria-pressed="true">Todos</button>${scope.environments.map(e => `<button data-filter="${e.id}" aria-pressed="false">${escape(e.name)}</button>`).join('')}</nav><div class="toolbar"><span id="count" role="status">15 casos</span><div class="views" aria-label="Tipo de vista"><button data-view="room" aria-pressed="true">Ver entorno</button><button data-view="patient" aria-pressed="false">Acercarse al paciente</button></div></div><section class="gallery">${cards}</section>
<footer><h2>Estado de esta entrega</h2><p>Esta revisión mejora los modelos existentes; no los sustituye por humanos escaneados. El fotorrealismo de primer plano sigue pendiente. Daniel conserva una puesta en escena más detallada que los demás casos.</p><p>Las capturas y las pruebas en Windows no acreditan el rendimiento ni la apariencia dentro de unas Meta Quest. La prueba en el visor sigue pendiente. La publicación en Meta se realiza por separado.</p><p>Para probar la versión de Windows, abre <strong>INICIAR-VITAL-VR.cmd</strong> dentro de la carpeta Windows del paquete, conservando todos sus archivos. Las voces son sintéticas y el contenido clínico continúa en revisión.</p></footer></main><script>
document.querySelectorAll('[data-filter]').forEach(button=>button.addEventListener('click',()=>{document.querySelectorAll('[data-filter]').forEach(b=>b.setAttribute('aria-pressed',String(b===button)));let count=0;document.querySelectorAll('article[data-environment]').forEach(card=>{card.hidden=button.dataset.filter!=='all'&&card.dataset.environment!==button.dataset.filter;if(!card.hidden)count++});document.getElementById('count').textContent=count+' casos'}));
document.querySelectorAll('[data-view]').forEach(button=>button.addEventListener('click',()=>{document.querySelectorAll('[data-view]').forEach(b=>b.setAttribute('aria-pressed',String(b===button)));document.querySelectorAll('img[data-case]').forEach(img=>{img.src='imagenes/'+img.dataset.case+'-'+button.dataset.view+'.png';img.closest('a').href=img.src})}));
</script></html>`;
await writeFile(path.join(destination, 'ABRIR-REVISION.html'), html, 'utf8');
await writeFile(path.join(destination, 'capturas.json'), JSON.stringify({ capturedWith: 'Unity editor, Quest rendering profile, inspection camera, UI and learner hands hidden', hardwareValidated: false, source: path.relative(root, captures), cases: entries.map(e => ({ id: e.id, environment: e.environment.id })) }, null, 2));
console.log(`Meeting review created: ${destination}; 15 cases, 30 screenshots; landing images copied.`);
