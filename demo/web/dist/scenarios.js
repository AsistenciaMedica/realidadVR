const env={gym:'Gimnasio',mall:'Centro comercial',dental:'Clínica dental',football:'Campo de fútbol'};
const el=(tag,text,cls)=>{const e=document.createElement(tag);e.textContent=text;if(cls)e.className=cls;return e;};
const controls=['query','category','environment','difficulty','validation','procedure','availability'].map(id=>document.getElementById(id));
const dialog=document.querySelector('#scenario-detail'),content=document.querySelector('#detail-content');
document.querySelector('#close-detail').addEventListener('click',()=>dialog.close());
dialog.addEventListener('close',()=>{if(location.pathname.startsWith('/scenarios/'))history.replaceState({},'', '/scenarios');});
dialog.addEventListener('click',e=>{if(e.target===dialog)dialog.close();});
document.querySelector('#filters').addEventListener('submit',e=>e.preventDefault());
try {
 const response=await fetch('/scenarios.json');if(!response.ok)throw Error('Carga fallida');const library=await response.json();
 const actionLabel=id=>library.actions.find(a=>a.id===id)?.label||id;
 const addSection=(title,lines)=>{content.append(el('h3',title));const list=el('ul','');for(const line of lines)list.append(el('li',line));content.append(list);};
 function finishDetail(s){
  content.append(el('h3','Referencias médicas'));
  for(const id of s.references){const r=library.references.find(r=>r.id===id);if(!r)continue;const a=el('a',`${r.organization} · ${r.title} · ${r.year}`);a.href=r.url;a.target='_blank';a.rel='noopener noreferrer';content.append(a,el('p','Consultado: '+r.accessedAt));}
  if(location.pathname!=='/scenarios/'+s.id)history.pushState({},'', '/scenarios/'+s.id);
  dialog.showModal();document.querySelector('#close-detail').focus();
 }
 function detail(s){
  content.replaceChildren();const title=el('h2',s.name);title.id='detail-title';content.append(el('p',s.category+' / '+env[s.environment],'eyebrow'),title,el('p',s.medicalValidationStatus+' · '+s.difficulty,'validation-note'),el('p',s.description),el('p',s.incident));
  if(s.catalogOnly){
   addSection('Contexto',[s.history]);
   addSection('Entrenamiento',['Practica la valoración inicial y la comunicación con el paciente dentro del simulador.', 'El contenido y la evaluación del caso están pendientes de revisión médica.']);
   if(s.debrief.length)addSection('Revisión del caso',s.debrief);
   finishDetail(s);return;
  }
  const p=s.initialState,v=s.variation;
  addSection('Paciente',[`${p.age} años base · variación ${v.minAge}–${v.maxAge} · ${v.sexes.join(', ')}`,s.history,`Conciencia: ${p.consciousness}. Respiración: ${p.respiration}. Circulación: ${p.circulation}.`,s.initialDialogue,...s.symptoms,...s.visibleSigns]);
  addSection('Constantes simuladas',[`FC ${p.heartRate} lpm · TA ${p.systolic}/${p.diastolic} mmHg · SpO2 ${p.spo2}%`,`Glucemia ${p.glucose} mmol/L · FR ${p.respiratoryRate}/min · Temperatura ${p.temperature} °C · Dolor ${p.pain}/10`,p.circulation==='pulseless'?'Los valores cero representan ausencia de lectura fiable sin perfusión.':'Valores ficticios; no son lecturas de sensores.']);
  addSection('Acciones esperadas',s.recommendedSequence.map((id,i)=>{const a=s.actions.find(a=>a.id===id);return `${i+1}. ${actionLabel(a.action)}${a.critical?' · CRÍTICA':''}${a.earliestSeconds?` · espera ${a.earliestSeconds}s desde ${a.anchorAction||'inicio'}`:''}`;}));
  addSection('Otras acciones y condiciones',s.actions.filter(a=>a.kind!=='required').map(a=>`${actionLabel(a.action)} · ${a.kind}${a.guard?' · condición: '+a.guard:''}`));
  addSection('Timeline',[s.timingBasis,...s.timeline.map(e=>`${e.afterSeconds}s desde ${e.anchorAction||'inicio'} · ${e.kind}: ${e.message}${e.unlessActions.length?' · se evita si: '+e.unlessActions.join(', '):''}`)]);
  addSection('Desenlaces',s.outcomes.map(o=>`${o.outcome} · requiere: ${o.requiresActions.join(', ')||'rama de respaldo'}${o.maximumSeconds?' · máximo '+o.maximumSeconds+'s':''}${o.requiresEvents.length?' · eventos '+o.requiresEvents.join(', '):''}`));
  addSection('Evaluación',[`Penalización base: ${s.errorPenalty}. Tope con errores críticos: ${s.criticalScoreCap}/100.`,`Acciones requeridas: ${s.actions.filter(a=>a.kind==='required').length}. Opcionales sin penalización por omitir.`,...s.actions.filter(a=>a.deadlineSeconds).map(a=>`${actionLabel(a.action)}: ventana educativa ${a.deadlineSeconds}s.`),'Secciones: valoración, vía aérea, respiración, circulación, RCP, DEA, mediciones, decisiones y tiempos. El informe añade métricas de recorrido virtual de RCP y pasos físicos del DEA; no son mediciones clínicas calibradas.']);
  addSection('Debrief y variación',[...s.debrief,`Seed reproducible. Variación de FC ±${v.heartRateSpread}; SpO2 ±${v.spo2Spread}; glucemia ±${v.glucoseSpread}; deterioro ±${v.timelineJitterSeconds}s.`,...v.witnessStatements]);
  finishDetail(s);
 }
 for(const [i,key] of [[1,'category'],[2,'environment'],[3,'difficulty'],[4,'medicalValidationStatus'],[6,'availability']])for(const value of [...new Set(library.scenarios.map(s=>s[key]))].sort()){const option=el('option',key==='environment'?env[value]:value==='AVAILABLE'?'Disponible para revisión':value);option.value=value;controls[i].append(option);}
 for(const a of library.actions.filter(a=>library.scenarios.some(s=>s.actions.some(r=>r.action===a.id&&r.kind==='required')))){const option=el('option',a.label);option.value=a.id;controls[5].append(option);}
 const params=new URLSearchParams(location.search);for(const control of controls)if(params.has(control.id))control.value=params.get(control.id);
 function render(){
  const [q,c,e,d,v,p,a]=controls.map(x=>x.value);const rows=library.scenarios.filter(s=>(!q||(s.name+' '+s.id+' '+s.symptoms.join(' ')).toLocaleLowerCase('es').includes(q.toLocaleLowerCase('es')))&&(!c||s.category===c)&&(!e||s.environment===e)&&(!d||s.difficulty===d)&&(!v||s.medicalValidationStatus===v)&&(!p||s.actions.some(r=>r.action===p&&r.kind==='required'))&&(!a||s.availability===a));
  document.querySelector('#count').textContent=`${rows.length} de ${library.scenarios.length} escenarios`;
  const grid=document.querySelector('#scenario-grid');grid.replaceChildren();
  for(const s of rows){const card=el('article','','scenario-card');card.append(el('p',s.category,'eyebrow'),el('h2',s.name),el('p',(env[s.environment]||s.environment)+' · '+s.difficulty),el('p',s.description,'scenario-description'),el('span',s.medicalValidationStatus,'status'),el('p',s.availability==='AVAILABLE'?'Disponible para revisión':'Próximamente','muted'));const button=el('button','Revisar caso →');button.setAttribute('aria-label','Revisar '+s.name);button.addEventListener('click',()=>detail(s));card.append(button);grid.append(card);}
  if(!rows.length)grid.append(el('p','No hay coincidencias. Cambia o limpia los filtros.'));
 }
 controls.forEach(c=>c.addEventListener('input',render));document.querySelector('#filters').addEventListener('reset',()=>setTimeout(render,0));render();
 if(location.pathname.startsWith('/scenarios/')){const scenario=library.scenarios.find(s=>s.id===decodeURIComponent(location.pathname.slice(11)));if(scenario)detail(scenario);}
}catch(error){document.querySelector('#count').textContent='No se pudo cargar la biblioteca. Recarga para intentarlo de nuevo.';}
