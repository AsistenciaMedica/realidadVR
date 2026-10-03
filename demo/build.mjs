import { readFileSync,existsSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
const mode=process.argv[2]||'all';
if(mode==='all'||mode==='frontend'){
 for(const file of ['index.html','scenarios.html','about.html','contact.html','admin.html','style.css','vital.css','commercial.css']){const path=new URL('./public/'+file,import.meta.url);if(!existsSync(path))throw Error('Missing frontend '+file);const text=readFileSync(path,'utf8');if(/[\uFFFD]|[A-Za-z]\?[A-Za-z]|\?(?:N|A)\b/.test(text))throw Error('Corrupted text in '+file);}
 for(const file of ['app.js','home.js','scenarios.js','admin.js','contact.js'])check('public/'+file);
 const catalog=JSON.parse(readFileSync(new URL('./public/scenarios.json',import.meta.url),'utf8'));
 const scope=catalog.releaseScope, environments=scope?.environments||[];
 if(scope?.schemaVersion!==1||scope.releaseId!=='vital-vr-mvp-3x5'||environments.length!==3||catalog.scenarios.length!==15)throw Error('Expected the 3-scenario, 15-case release catalog');
 const caseIds=environments.flatMap(environment=>environment.scenarioIds);
 if(new Set(environments.map(environment=>environment.id)).size!==3||new Set(caseIds).size!==15||new Set(catalog.scenarios.map(scenario=>scenario.id)).size!==15||environments.some(environment=>environment.scenarioIds.length!==5||environment.scenarioIds.some(id=>!catalog.scenarios.some(scenario=>scenario.id===id&&scenario.environment===environment.id))))throw Error('Each release scenario must include five distinct matching cases');
 console.log('Frontend build validated: static HTML/CSS/JS and '+catalog.scenarios.length+' catalog entries. No bundler required.');
}
if(mode==='all'||mode==='backend'){for(const file of ['server.mjs','backend/api.mjs','backend/store.mjs','backend/security.mjs'])check(file);console.log('Backend build validated: native Node 24 modules.');}
function check(file){const result=spawnSync(process.execPath,['--check',fileURLToPath(new URL(file,import.meta.url))],{stdio:'inherit'});if(result.status!==0)throw Error('Syntax check failed: '+file);}
