import fs from 'node:fs';
const path='Assets/_Project/Resources/MedicalScenarios.json';
const text=fs.readFileSync(path,'utf8');const data=JSON.parse(text);
if(data.schemaVersion!==3||data.scenarios.length<30)throw Error('Incomplete medical library');
fs.writeFileSync('demo/public/scenarios.json',text);
const environments={gym:'Gimnasio',mall:'Centro comercial',dental:'Clínica dental',football:'Campo de fútbol'};
const rows=data.scenarios.map(s=>`| ${s.id} | ${s.name} | ${s.category} | ${environments[s.environment]} | ${s.difficulty} | ${s.medicalValidationStatus} |`);
fs.writeFileSync('docs/SCENARIO_LIBRARY.md','# Biblioteca Vital VR\n\n'+data.scenarios.length+' escenarios/variantes adultos. Reglas declarativas con adaptadores de interacción Windows/XR; pendientes de validación médica.\n\nFuente editable: `Assets/_Project/Resources/MedicalScenarios.json`.\nLos cuatro ambientes son módulos procedurales reutilizables.\nEstado visual, RCP virtual y calibración pendiente: [VISUAL_INTERACTION_STATUS.md](VISUAL_INTERACTION_STATUS.md).\n\n| ID | Nombre | Categoría | Ambiente | Dificultad | Medical validation status |\n| --- | --- | --- | --- | --- | --- |\n'+rows.join('\n')+'\n');
console.log('Synced '+data.scenarios.length+' scenarios and review table.');
