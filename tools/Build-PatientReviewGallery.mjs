import { readFile, writeFile, access } from 'node:fs/promises';
import path from 'node:path';

const directory = path.resolve(process.argv[2] || 'TestResults/PatientRosterPreview');
const report = JSON.parse(await readFile(path.join(directory, 'patient-roster-smoke.json'), 'utf8'));
if (report.result !== 'PASS' || report.patients.length !== 15) throw Error('A complete successful roster capture is required.');
const escape = text => String(text ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
const cards = [];
for (const patient of report.patients) {
  const filename = path.basename(patient.screenshotPrefix) + '-patient.png';
  await access(path.join(directory, filename));
  cards.push(`<article><a href="${escape(filename)}"><img src="${escape(filename)}" alt="Captura de ${escape(patient.name)}" loading="lazy"></a><div><h2>${escape(patient.name)} <small>${patient.age} años</small></h2><p>${escape(patient.briefingTitle)}</p><p class="detail">${escape(patient.scenarioId)} · ${escape(patient.sourceModel)}</p></div></article>`);
}
await writeFile(path.join(directory, 'index.html'), `<!doctype html><html lang="es"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>VITAL VR · Los 15 pacientes</title><style>
*{box-sizing:border-box}body{margin:0;background:#0b1520;color:#edf6fa;font:16px/1.5 system-ui,sans-serif}main{max-width:1440px;margin:auto;padding:40px 24px}h1{font-size:clamp(28px,4vw,48px);margin:8px 0}header{max-width:900px;margin-bottom:30px}.eyebrow{color:#74d7cb;letter-spacing:.12em;font-size:13px}header p{color:#b9cbd6}section{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:20px}article{overflow:hidden;border:1px solid #294050;border-radius:12px;background:#142332}img{display:block;width:100%;aspect-ratio:16/9;object-fit:contain;background:#080f15}article div{padding:18px}h2{margin:0;font-size:22px}small{color:#b9cbd6;font-size:15px;font-weight:400}article p{margin:8px 0}.detail{font-size:12px;color:#91a9b8;overflow-wrap:anywhere}a{color:#74d7cb}a:focus-visible{outline:3px solid #f9c267;outline-offset:3px}@media(max-width:950px){section{grid-template-columns:repeat(2,minmax(0,1fr))}}@media(max-width:580px){section{grid-template-columns:1fr}main{padding:24px 16px}}
</style><main><header><div class="eyebrow">VITAL VR · GIMNASIO / CENTRO COMERCIAL / CAMPO DE FÚTBOL</div><h1>Quince casos. Quince pacientes.</h1><p>Capturas reales del ejecutable de Windows. Pulsa una imagen para ver el archivo completo. Esta galería permite revisar personajes y presentación; las pruebas en visor y la revisión clínica siguen siendo necesarias para el lanzamiento.</p><p>Las vistas externas del paciente ocultan temporalmente las manos, las mangas y el reloj del rescatador para revisar el cuerpo completo. El paciente, el equipo, el mobiliario y los obstáculos del entorno permanecen visibles. Durante la práctica se conserva la vista del rescatador.</p><a href="patient-roster-smoke.json">Ver informe del recorrido técnico</a></header><section>${cards.join('\n')}</section></main></html>`, 'utf8');
console.log(path.join(directory, 'index.html'));
