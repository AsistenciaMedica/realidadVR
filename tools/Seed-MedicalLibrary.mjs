// Initial authoring tool, not gameplay logic. JSON becomes the editable source of truth.
// Never overwrite reviewed content implicitly. Run with --write only to create a missing library.
import fs from 'node:fs';
const target='Assets/_Project/Resources/MedicalScenarios.json';
const source=(id,organization,title,year,url)=>({id,organization,title,year,url,accessedAt:'2026-09-13'});
const references=[
 source('bls','Resuscitation Council UK','Adult basic life support Guidelines','2025','https://www.resus.org.uk/professional-library/2025-resuscitation-guidelines/adult-basic-life-support-guidelines'),
 source('firstaid','Resuscitation Council UK','First Aid Guidelines','2025','https://www.resus.org.uk/professional-library/2025-resuscitation-guidelines/first-aid-guidelines'),
 ...[
 ['faint','Fainting','2026','https://www.nhs.uk/symptoms/fainting/'],
 ['glucose','Low blood sugar (hypoglycaemia)','2023','https://www.nhs.uk/conditions/low-blood-sugar-hypoglycaemia/'],
 ['hyper','High blood sugar (hyperglycaemia)','web; consultado 2026','https://www.nhs.uk/conditions/high-blood-sugar-hyperglycaemia/'],
 ['bp','Low blood pressure (hypotension)','web; consultado 2026','https://www.nhs.uk/conditions/low-blood-pressure-hypotension/'],
 ['asthma','Asthma','2025','https://www.nhs.uk/conditions/asthma/'],
 ['dyspnea','Shortness of breath','web; consultado 2026','https://www.nhs.uk/symptoms/shortness-of-breath/'],
 ['allergy','Anaphylaxis','web; consultado 2026','https://www.nhs.uk/conditions/anaphylaxis/'],
 ['seizure','What to do if someone has a seizure (fit)','web; consultado 2026','https://www.nhs.uk/symptoms/what-to-do-if-someone-has-a-seizure-fit/'],
 ['stroke','Stroke','web; consultado 2026','https://www.nhs.uk/conditions/stroke/'],
 ['head','Head injury and concussion','web; consultado 2026','https://www.nhs.uk/conditions/head-injury-and-concussion/'],
 ['heat','Heat exhaustion and heatstroke','web; consultado 2026','https://www.nhs.uk/conditions/heat-exhaustion-heatstroke/'],
 ['cold','Hypothermia','web; consultado 2026','https://www.nhs.uk/conditions/hypothermia/'],
 ['water','Dehydration','web; consultado 2026','https://www.nhs.uk/conditions/dehydration/']
 ].map(([id,title,year,url])=>source(id,'NHS',title,year,url))
];
const labels={
 CheckSceneSafety:['Seguridad de escena','Initial Assessment'],CheckResponsiveness:['Comprobar respuesta','Initial Assessment'],CallEmergencyServices:['Activar emergencias y pedir ayuda','Decision Making'],OpenAirway:['Abrir vía aérea','Airway'],CheckBreathing:['Comprobar respiración','Breathing'],CheckPulse:['Comprobar pulso (personal entrenado)','Circulation'],RecoveryPosition:['Posición lateral si respira normalmente y sin trauma','Airway'],StartCPR:['Iniciar RCP declarada','CPR'],ChestCompression:['Registrar compresiones (sin medición física)','CPR'],RescueBreaths:['Registrar ventilaciones si se está entrenado','CPR'],BringAED:['Solicitar / traer DEA','AED'],AttachAEDPads:['Encender DEA y colocar parches según dibujo','AED'],AnalyzeRhythm:['Apartarse durante análisis del DEA','AED'],DeliverAEDShock:['Confirmar nadie toca y descargar si DEA lo indica','AED'],ContinueCPR:['Reanudar RCP según DEA','CPR'],FollowNoShock:['Seguir indicación de NO descarga','AED'],CheckBloodPressure:['Medir tensión arterial simulada','Measurements'],CheckSpO2:['Medir SpO2 simulada','Measurements'],CheckGlucose:['Medir glucemia simulada (mmol/L)','Measurements'],CheckTemperature:['Medir temperatura simulada','Measurements'],GiveGlucose:['Azúcar oral solo si puede tragar con seguridad','Decision Making'],PositionPatient:['Posicionar y tranquilizar según condición','Decision Making'],ElevateLegs:['Elevar piernas si no hay lesión','Circulation'],ControlBleeding:['Identificar hemorragia','Circulation'],ApplyPressure:['Presión directa sobre sangrado','Circulation'],ApplyBandage:['Asegurar apósito tras controlar sangrado','Circulation'],UseEpinephrineAutoInjector:['Autoinyector disponible según instrucciones y entrenamiento','Decision Making'],ChokingBackBlows:['Hasta 5 golpes dorsales si tos ineficaz','Airway'],ChokingAbdominalThrusts:['Hasta 5 compresiones abdominales si persiste obstrucción','Airway'],EncourageCough:['Animar a toser si la tos es eficaz','Airway'],ImmobilizePatient:['Limitar movimientos sin comprometer vía aérea','Decision Making'],CoolPatient:['Retirar del calor e iniciar enfriamiento','Decision Making'],WarmPatient:['Proteger del frío y abrigar gradualmente','Decision Making'],AssistOwnInhaler:['Ayudar con inhalador propio según plan personal','Breathing'],ProtectFromInjury:['Proteger de golpes sin sujetar','Decision Making'],TimeSeizure:['Registrar inicio y duración de convulsión','Timing'],CheckFAST:['Valorar cara, brazo y habla; registrar inicio','Initial Assessment'],Reassess:['Reevaluar respuesta y respiración','Initial Assessment'],Handover:['Comunicar hallazgos y entregar a emergencias','Decision Making'],Monitor:['Vigilar continuamente hasta relevo','Breathing'],GiveWater:['Agua solo con deglución segura','Decision Making'],BlindFingerSweep:['Intentar barrido digital a ciegas','Airway'],RestrainSeizure:['Sujetar a la persona que convulsiona','Decision Making'],WalkPatient:['Hacer caminar al paciente inestable','Decision Making'],GiveInsulin:['Administrar insulina sin plan ni autorización','Decision Making'],PaperBag:['Respirar dentro de una bolsa','Breathing'],LeavePatient:['Abandonar al paciente','Decision Making']
};
const actions=Object.entries(labels).map(([id,[label,section]])=>({id,label,section,description:'Acción semántica compartida. La configuración del caso establece sus requisitos y efectos.'}));
const effect=(o={})=>({consciousness:'',respiration:'',circulation:'',dialogue:'',position:'',addFlags:[],removeFlags:[],spo2Delta:0,heartRateDelta:0,glucoseDelta:0,systolicDelta:0,diastolicDelta:0,respiratoryRateDelta:0,temperatureDelta:0,painDelta:0,setSwallow:false,canSwallow:false,setShockAdvised:false,shockAdvised:false,...o});
const patient=(o={})=>({consciousness:'Conscious',respiration:'normal',circulation:'normal',sex:'female',age:40,appearance:'Paciente adulto placeholder; observar los signos descritos en el panel.',dialogue:'Me encuentro mal.',position:'supine',heartRate:78,systolic:120,diastolic:80,spo2:97,glucose:5,respiratoryRate:16,temperature:37,pain:0,canSwallow:true,shockAdvised:false,flags:[],...o});
const rule=(action,id=action,o={})=>({id,action,kind:'required',feedback:labels[action][0]+'. Registro simulado.',guard:'',anchorAction:'',prerequisites:[],points:10,penalty:15,critical:false,earliestSeconds:0,deadlineSeconds:0,effect:effect(),...o});
const event=(id,afterSeconds,o={})=>({id,kind:'observation',message:'Reevaluar: el paciente continúa necesitando vigilancia.',anchorAction:'',afterSeconds,requiresActions:[],unlessActions:[],effect:effect(),...o});
const outcome=(id,name,requiresActions,o={})=>({id,outcome:name,feedback:'Desenlace de este guion; no predice la evolución de pacientes reales.',requiresActions,missingActions:[],requiresEvents:[],minimumSeconds:0,maximumSeconds:0,noCriticalErrors:true,effect:effect(),...o});
const scenarios=[];
function create(id,name,category,environment,type,options={}) {
 const s={id,name,category,difficulty:options.difficulty||'Inicial',environment,description:options.description||name+'. Valorar, actuar dentro de competencias y solicitar relevo.',history:options.history||'Antecedentes no confirmados; consultar al testigo y al paciente.',incident:options.incident||'Incidente durante actividad cotidiana. Persona adulta; guion de formación inicial.',severity:options.severity||'moderada',initialDialogue:'Me encuentro mal.',medicalValidationStatus:'CLIENT_REVIEW',version:'1',timingBasis:'Tiempos y variación de deterioro son parámetros educativos de autoría, no ventanas fisiológicas demostradas. Revisión médica pendiente.',symptoms:options.symptoms||[name],visibleSigns:options.signs||['Los signos se describen en el panel, sin modelo anatómico.'],references:[],debrief:['Priorizar amenazas vitales y comunicar cambios. Adaptar a protocolos locales y nivel de entrenamiento.'],initialState:patient(),variation:{minAge:24,maxAge:72,heartRateSpread:3,spo2Spread:1,glucoseSpread:.1,timelineJitterSeconds:5,sexes:['female','male'],witnessStatements:['Lo vi encontrarse mal hace unos momentos.','No conozco sus antecedentes; puedo colaborar llamando a emergencias.'],positions:['supine']},actions:[],timeline:[],outcomes:[],recommendedSequence:[],errorPenalty:5,criticalScoreCap:49};
 const add=(a,o={})=>{const id=o.id||a; const r=rule(a,id,o);s.actions.push(r);return r;};
 const unsafe=(a,feedback)=>add(a,{kind:'dangerous',points:0,feedback});
 add('CheckSceneSafety',{critical:true}); add('CheckResponsiveness',{prerequisites:['CheckSceneSafety'],critical:true});
 const help=()=>add('CallEmergencyServices',{prerequisites:['CheckResponsiveness'],critical:true,deadlineSeconds:120});
 const breath=()=>add('CheckBreathing',{prerequisites:['CheckResponsiveness'],critical:true});
 const monitor=()=>add('Monitor',{prerequisites:['CheckBreathing']});
 const handover=()=>add('Handover',{prerequisites:['CallEmergencyServices']});
 const arrest=(shock=0)=>{
  s.initialState=patient({consciousness:'Unresponsive',respiration:options.agonal?'agonal':'absent',circulation:'pulseless',heartRate:0,systolic:0,diastolic:0,spo2:0,respiratoryRate:0,canSwallow:false,shockAdvised:shock>0,flags:['hypoxia']});
  s.initialDialogue='No responde. No hay respiración normal. SpO2 y TA no tienen lectura fiable sin perfusión.';s.references=['bls'];
  help();breath();add('StartCPR',{prerequisites:['CheckBreathing'],guard:'arrest',critical:true,deadlineSeconds:90});
  add('BringAED',{prerequisites:['CallEmergencyServices'],critical:true});add('AttachAEDPads',{prerequisites:['BringAED'],critical:true,deadlineSeconds:180});
  add('AnalyzeRhythm',{prerequisites:['AttachAEDPads'],critical:true,feedback:shock>0?'DEA simulado: descarga indicada.':'DEA simulado: descarga NO indicada.'});
  if(shock>0) add('DeliverAEDShock',{prerequisites:['AnalyzeRhythm'],guard:'shock',critical:true,effect:effect({setShockAdvised:true,shockAdvised:false})});
  else { add('FollowNoShock',{prerequisites:['AnalyzeRhythm'],guard:'noShock',critical:true}); add('DeliverAEDShock',{kind:'optional',prerequisites:['AnalyzeRhythm'],guard:'shock',points:0}); }
  add('ContinueCPR',{prerequisites:[shock>0?'DeliverAEDShock':'FollowNoShock'],guard:'arrest',critical:true});
  if(shock>1) {
   add('AnalyzeRhythm',{id:'AnalyzeRhythm2',prerequisites:['ContinueCPR'],anchorAction:'ContinueCPR',earliestSeconds:120,effect:effect({setShockAdvised:true,shockAdvised:true}),feedback:'Nuevo análisis indicado por DEA: descarga indicada.'});
   add('DeliverAEDShock',{id:'DeliverAEDShock2',prerequisites:['AnalyzeRhythm2'],guard:'shock',critical:true,effect:effect({setShockAdvised:true,shockAdvised:false})});
   add('ContinueCPR',{id:'ContinueCPR2',prerequisites:['DeliverAEDShock2'],guard:'arrest',critical:true});
  }
  add('ChestCompression',{kind:'optional',prerequisites:['StartCPR'],guard:'arrest',points:0});add('RescueBreaths',{kind:'optional',prerequisites:['StartCPR'],guard:'arrest',points:0});handover();
  s.timeline.push(event('without-cpr',150,{kind:'deterioration',unlessActions:['StartCPR'],effect:effect({addFlags:['shock']}),message:'Sigue sin perfusión: RCP no registrada.'}));
  if(shock>0) s.outcomes.push(outcome('scripted-rosc','ROSC',shock>1?['ContinueCPR2','Handover']:['ContinueCPR','Handover'],{maximumSeconds:shock>1?360:180,effect:effect({consciousness:'Unresponsive',respiration:'normal',circulation:'normal',heartRateDelta:80,systolicDelta:105,diastolicDelta:65,spo2Delta:94,respiratoryRateDelta:16,removeFlags:['hypoxia'],dialogue:'Aparece respiración normal en el guion. Reevaluar y vigilar hasta relevo.'})}));
  s.debrief.push('Seguir el DEA, despejar contacto y minimizar interrupciones. No se exige comprobar pulso al reanimador lego. La recuperación depende del guion y no está garantizada.');
 };
 if(type==='arrest') arrest(options.shocks||0);
 else if(type==='unconscious'||type==='severeGlucose') {
  s.initialState=patient({consciousness:'Unresponsive',canSwallow:false,...(type==='severeGlucose'?{glucose:2.1,flags:['hypoglycemia']}:{})}); s.initialDialogue='No hay respuesta; respiración normal.';
  s.references=type==='severeGlucose'?['glucose','bls']:['bls','firstaid'];help();add('OpenAirway',{prerequisites:['CheckResponsiveness'],critical:true});breath();
  add('RecoveryPosition',{prerequisites:['OpenAirway','CheckBreathing'],guard:'normalBreathing',critical:true,effect:effect({position:'recovery'})});
  if(type==='severeGlucose') {add('CheckGlucose',{prerequisites:['CallEmergencyServices']});add('GiveGlucose',{kind:'optional',guard:'swallow',points:0,feedback:'No ofrecer azúcar por boca sin deglución segura.'});}
  monitor();handover(); s.outcomes.push(outcome('stable-awaiting-help','UNCONSCIOUS_STABLE',['Monitor','Handover']));
 } else if(type==='glucose') {
  s.references=['glucose','firstaid'];s.history='Diabetes conocida; ingesta insuficiente referida.';s.initialState=patient({glucose:options.moderate?2.8:3.2,consciousness:options.moderate?'Confused':'Conscious',flags:['hypoglycemia']});
  s.initialDialogue='Tengo temblor y sudor frío; puedo tragar.';breath();add('CheckGlucose',{prerequisites:['CheckResponsiveness'],critical:true});add('GiveGlucose',{prerequisites:['CheckGlucose'],guard:'swallow',critical:true});
  add('CheckGlucose',{id:'RecheckGlucose',prerequisites:['GiveGlucose'],anchorAction:'GiveGlucose',earliestSeconds:600,feedback:'Reevaluación tras 10 minutos simulados. Consultar lectura actual.'});
  help();monitor();handover();
  s.timeline.push(event('glucose-response',600,{kind:'recovery',anchorAction:'GiveGlucose',requiresActions:['GiveGlucose'],effect:effect({glucoseDelta:1.5,consciousness:'Conscious',removeFlags:['hypoglycemia'],dialogue:'El temblor disminuye en esta rama del guion.'}),message:'Han pasado 10 minutos simulados tras azúcar oral. Medir de nuevo.'}));
  // Calling for help has no deadline in this stable, swallowing-safe teaching branch.
  s.actions.find(a=>a.action==='CallEmergencyServices').deadlineSeconds=0;
  s.outcomes.push(outcome('improved','PARTIAL_RECOVERY',['RecheckGlucose','Handover'],{requiresEvents:['glucose-response']}));
 } else if(type==='syncope'||type==='hypotension'||type==='dehydration') {
  s.references=type==='syncope'?['faint']:type==='hypotension'?['bp']:['water'];
  s.initialState=patient(type==='hypotension'?{systolic:85,diastolic:55,circulation:'hypotension'}:{});s.initialDialogue='Me mareo; ya respondo y respiro normalmente.';
  breath();add('PositionPatient',{prerequisites:['CheckBreathing'],guard:'noTrauma',effect:effect({position:'supine'})});
  if(type==='syncope')add('ElevateLegs',{prerequisites:['PositionPatient'],guard:'noTrauma'});
  if(type==='hypotension') {add('CheckBloodPressure',{prerequisites:['CheckResponsiveness']});add('CheckSpO2',{prerequisites:['CheckBreathing']});}
  if(type==='dehydration')add('GiveWater',{prerequisites:['CheckResponsiveness'],guard:'swallow'});
  help();add('Reassess',{prerequisites:['PositionPatient']});handover();
  if(type==='syncope')s.outcomes.push(outcome('responds','VERBAL_RESPONSE',['Reassess','Handover'],{effect:effect({dialogue:'Estoy algo mejor; acepto valoración.'})}));
  unsafe('WalkPatient','No hacer caminar mientras persiste el mareo.');
 } else if(type==='hyper') {
  s.references=['hyper'];s.initialState=patient({glucose:19,flags:['hyperglycemia'],respiration:'fast',respiratoryRate:26});s.initialDialogue='Tengo mucha sed, náuseas y debilidad.';
  help();breath();add('CheckGlucose',{prerequisites:['CheckResponsiveness']});monitor();handover();unsafe('GiveInsulin','No se simula una dosis libre de insulina; solicitar evaluación urgente.');
 } else if(type==='asthma'||type==='dyspnea'||type==='hypoxia'||type==='anxiety') {
  s.references=type==='asthma'?['asthma']:['dyspnea'];s.initialState=patient({respiration:'fast',respiratoryRate:28,spo2:type==='hypoxia'?87:type==='anxiety'?98:92,flags:type==='anxiety'?[]:['hypoxia']});s.initialDialogue='Me cuesta respirar.';
  help();breath();add('PositionPatient',{prerequisites:['CheckBreathing'],effect:effect({position:'seated'})});add('CheckSpO2',{prerequisites:['CheckBreathing']});
  if(type==='asthma')add('AssistOwnInhaler',{prerequisites:['PositionPatient'],guard:'conscious',critical:true});
  monitor();handover();unsafe('PaperBag','No usar bolsa ni asumir ansiedad sin descartar una emergencia.');
  if(type==='hypoxia'||type==='asthma')s.timeline.push(event('respiratory-fatigue',150,{kind:'deterioration',unlessActions:['Monitor'],effect:effect({consciousness:'Drowsy',spo2Delta:-3,setSwallow:true,canSwallow:false}),message:'Aumenta la fatiga en este guion; comunicar deterioro.'}));
 } else if(type==='chokingPartial'||type==='chokingFull'||type==='chokingCollapse') {
  s.references=['firstaid'];s.initialState=patient({respiration:'fast',respiratoryRate:30,canSwallow:false,flags:['airway obstruction']});s.initialDialogue=type==='chokingPartial'?'Toso con fuerza; puedo hablar.':'No puede hablar ni toser eficazmente.';
  help();breath();
  if(type==='chokingPartial')add('EncourageCough',{prerequisites:['CheckBreathing'],critical:true});
  else {add('ChokingBackBlows',{prerequisites:['CheckBreathing'],guard:'conscious',critical:true});add('ChokingAbdominalThrusts',{prerequisites:['ChokingBackBlows'],guard:'conscious',critical:true});}
  if(type==='chokingCollapse') {
   s.timeline.push(event('collapse',30,{kind:'deterioration',effect:effect({consciousness:'Unresponsive',respiration:'absent',circulation:'pulseless',heartRateDelta:-78,systolicDelta:-120,diastolicDelta:-80,respiratoryRateDelta:-30,spo2Delta:-97}),message:'Pierde respuesta y respiración normal. Cambiar a RCP y pedir DEA.'}));
   s.variation.heartRateSpread=0;s.variation.spo2Spread=0;s.variation.timelineJitterSeconds=0;
   add('StartCPR',{prerequisites:['ChokingAbdominalThrusts'],earliestSeconds:30,guard:'arrest',critical:true});
   add('BringAED',{prerequisites:['CallEmergencyServices']});add('AttachAEDPads',{prerequisites:['BringAED']});add('AnalyzeRhythm',{prerequisites:['AttachAEDPads']});add('FollowNoShock',{prerequisites:['AnalyzeRhythm'],guard:'noShock'});add('ContinueCPR',{prerequisites:['StartCPR','FollowNoShock'],guard:'arrest'});
  } else {
   const intervention=type==='chokingPartial'?'EncourageCough':'ChokingAbdominalThrusts';
   s.timeline.push(event('obstruction-relieved',5,{kind:'recovery',anchorAction:intervention,requiresActions:[intervention],effect:effect({respiration:'normal',respiratoryRateDelta:-14,removeFlags:['airway obstruction'],dialogue:'Vuelve a hablar. Requiere revisión.'}),message:'Obstrucción resuelta en esta rama simulada.'}));
   add('Reassess',{prerequisites:[intervention],anchorAction:intervention,earliestSeconds:5});
  }
  monitor();handover();unsafe('BlindFingerSweep','No introducir dedos a ciegas en la boca.');
 } else if(type==='seizure'||type==='postictal') {
  s.references=['seizure'];s.initialState=patient({consciousness:type==='seizure'?'Unresponsive':'Confused',canSwallow:false,flags:type==='seizure'?['seizure']:[]});s.initialDialogue=type==='seizure'?'Movimientos convulsivos; no responde.':'Está confuso después del episodio.';
  help();add('ProtectFromInjury',{prerequisites:['CheckSceneSafety'],critical:true});add('TimeSeizure',{prerequisites:['CheckResponsiveness']});
  if(type==='seizure')s.timeline.push(event('seizure-ends',40,{kind:'recovery',effect:effect({removeFlags:['seizure'],consciousness:'Drowsy'}),message:'Cesa la convulsión en el guion. Reevaluar respiración.'}));
  add('CheckBreathing',{prerequisites:['CheckResponsiveness'],earliestSeconds:type==='seizure'?40:0,guard:'seizureStopped',critical:true});
  add('RecoveryPosition',{prerequisites:['CheckBreathing'],guard:'normalBreathing'});monitor();handover();unsafe('RestrainSeizure','Proteger sin inmovilizar a la fuerza ni introducir objetos en la boca.');
 } else if(type==='stroke'||type==='confused') {
  s.references=type==='stroke'?['stroke','firstaid']:['firstaid'];s.initialState=patient({consciousness:'Confused',canSwallow:false,...(type==='stroke'?{systolic:165,diastolic:95,circulation:'hypertension'}:{})});s.initialDialogue=options.dialogue||'Habla confusa; inicio súbito referido.';
  help();breath();if(type==='stroke')add('CheckFAST',{prerequisites:['CheckResponsiveness'],critical:true});add('CheckGlucose',{prerequisites:['CallEmergencyServices']});monitor();handover();
  add('GiveWater',{kind:'optional',guard:'swallow',points:0,feedback:'Evitar vía oral con deglución no confirmada.'});
 } else if(type==='allergy'||type==='anaphylaxis') {
  s.references=['allergy','firstaid'];s.initialState=patient(type==='allergy'?{flags:['allergic reaction']}:{systolic:85,diastolic:55,circulation:'hypotension',respiration:'fast',respiratoryRate:28,spo2:91,flags:['allergic reaction','shock'],...(options.unresponsive?{consciousness:'Unresponsive',respiration:'normal',respiratoryRate:16,canSwallow:false}:{})});s.initialDialogue=options.unresponsive?'No responde, mantiene respiración normal.':'Erupción tras exposición. '+(type==='anaphylaxis'?'Hinchazón y debilidad.':'Respira y habla normalmente.');
  help();breath();add('PositionPatient',{prerequisites:['CheckBreathing'],effect:effect({position:options.dyspnea?'seated-legs-extended':'supine'})});
  if(type==='anaphylaxis')add('UseEpinephrineAutoInjector',{prerequisites:['CallEmergencyServices'],critical:true,feedback:'Autoinyector prescrito disponible; uso declarado según entrenamiento e instrucciones. Si persiste, comunicar y seguir guía del dispositivo/protocolo.'});
  if(options.unresponsive)add('RecoveryPosition',{prerequisites:['CheckBreathing'],guard:'normalBreathing'});
  monitor();handover();unsafe('WalkPatient','No permitir ponerse de pie ni caminar en anafilaxia.');
 } else if(['fall','head','cervical','fracture','bleeding'].includes(type)) {
  s.references=type==='head'?['head']:['firstaid'];s.initialState=patient({pain:7,flags:type==='bleeding'?['trauma','bleeding']:['trauma','pain']});s.initialDialogue='Dolor tras una lesión. No quiere moverse.';
  help();breath();
  if(type==='bleeding') {add('ControlBleeding',{prerequisites:['CheckSceneSafety'],critical:true});add('ApplyPressure',{prerequisites:['ControlBleeding'],critical:true,effect:effect({removeFlags:['bleeding']})});add('ApplyBandage',{prerequisites:['ApplyPressure']});}
  else add('ImmobilizePatient',{prerequisites:['CheckBreathing'],critical:true});
  monitor();handover();unsafe('WalkPatient','Evitar movimiento innecesario; la vía aérea mantiene prioridad.');
  if(type==='head')s.timeline.push(event('head-drowsiness',120,{kind:'deterioration',effect:effect({consciousness:'Drowsy',setSwallow:true,canSwallow:false}),message:'Se vuelve somnoliento; informar a emergencias y reevaluar.'}));
 } else if(type==='heatstroke'||type==='heatExhaustion'||type==='cold') {
  s.references=type==='cold'?['cold']:['heat'];s.initialState=patient(type==='cold'?{temperature:34.5,flags:['cold']}:{temperature:type==='heatstroke'?40.2:38.4,consciousness:type==='heatstroke'?'Confused':'Conscious',canSwallow:type!=='heatstroke',respiration:'fast',respiratoryRate:26,flags:['heat']});s.initialDialogue='Malestar tras exposición a temperatura extrema.';
  help();breath();add('CheckTemperature',{prerequisites:['CheckResponsiveness']});add(type==='cold'?'WarmPatient':'CoolPatient',{prerequisites:['CheckSceneSafety'],critical:true});
  if(type==='heatExhaustion')add('GiveWater',{guard:'swallow',prerequisites:['CheckResponsiveness']});
  monitor();handover();
 }
 s.actions.push(rule('CheckPulse','CheckPulse',{kind:'optional',points:0,feedback:'Dato simulado para personal entrenado; no retrasar RCP del reanimador lego.'}));
 s.actions.push(rule('LeavePatient','LeavePatient',{kind:'dangerous',points:0,feedback:'Mantener asistencia y vigilancia hasta relevo.'}));
 s.recommendedSequence=s.actions.filter(a=>a.kind==='required').map(a=>a.id);
 if(!s.timeline.length)s.timeline.push(event('ongoing-observation',60,{message:'El guion sigue activo: reevaluar y comunicar cambios.'}));
 s.outcomes.push(outcome('care-completed','REQUIRES_ADVANCED_CARE',s.recommendedSequence));
 s.outcomes.push(outcome('incomplete-care','REQUIRES_ADVANCED_CARE',[],{noCriticalErrors:false,feedback:'Revisar omisiones antes de repetir el caso.'}));
 if(options.patient)Object.assign(s.initialState,options.patient);
 if(options.description)s.description=options.description;
 scenarios.push(s);
}
// Original pilot IDs retained in the medical catalog.
create('review-fainting-v1','Desvanecimiento · piloto migrado','Síncope','dental','syncope');
create('review-unconscious-breathing-v1','Inconsciente con respiración · piloto migrado','Conciencia','mall','unconscious');
create('review-hypoglycaemia-v1','Hipoglucemia consciente · piloto migrado','Glucosa','dental','glucose');
create('review-hypotension-v1','Hipotensión sintomática · piloto migrado','Síncope','gym','hypotension');
create('review-abnormal-breathing-v1','Sin respiración normal · piloto migrado','Cardiorrespiratorio','mall','arrest');
[
 ['arrest-witnessed','Parada presenciada · DEA precoz','Cardiorrespiratorio','football','arrest',{shocks:1}],
 ['arrest-unwitnessed','Parada no presenciada','Cardiorrespiratorio','mall','arrest',{shocks:1}],
 ['arrest-two-shocks','Parada · dos análisis y descargas','Cardiorrespiratorio','gym','arrest',{shocks:2,difficulty:'Intermedio'}],
 ['agonal','Respiración agónica','Cardiorrespiratorio','football','arrest',{agonal:true}],
 ['chest-pain','Dolor torácico con disnea','Cardiorrespiratorio','gym','dyspnea',{description:'Dolor opresivo y disnea durante ejercicio. Activar emergencias sin esperar mediciones.',patient:{pain:8}}],
 ['gym-faint','Síncope después de ejercicio','Síncope','gym','syncope'],
 ['football-faint','Recuperación de pérdida breve de conciencia','Síncope','football','syncope'],
 ['dehydration','Deshidratación con respuesta conservada','Temperatura / entorno','mall','dehydration'],
 ['confusion','Paciente confuso · origen no confirmado','Conciencia','mall','confused'],
 ['glucose-moderate','Hipoglucemia con confusión y deglución segura','Glucosa','gym','glucose',{moderate:true}],
 ['glucose-severe','Hipoglucemia grave sin respuesta','Glucosa','football','severeGlucose',{difficulty:'Intermedio'}],
 ['diabetes-unresponsive','Paciente diabético inconsciente','Glucosa','dental','severeGlucose'],
 ['hyperglycemia','Hiperglucemia sintomática','Glucosa','mall','hyper'],
 ['asthma','Crisis asmática · inhalador propio','Respiratorio','gym','asthma'],
 ['dyspnea','Disnea de origen desconocido','Respiratorio','mall','dyspnea'],
 ['hypoxia','Hipoxia con fatiga','Respiratorio','football','hypoxia',{difficulty:'Intermedio'}],
 ['hyperventilation','Respiración rápida durante consulta','Respiratorio','dental','anxiety'],
 ['choking-partial','Obstrucción parcial · tos eficaz','Respiratorio','mall','chokingPartial'],
 ['choking-complete','Obstrucción completa · adulto consciente','Respiratorio','mall','chokingFull'],
 ['choking-collapse','Atragantamiento que progresa a colapso','Respiratorio','dental','chokingCollapse',{difficulty:'Intermedio'}],
 ['seizure','Convulsión activa','Neurológico','mall','seizure'],
 ['postictal','Estado postictal','Neurológico','gym','postictal'],
 ['stroke','Sospecha de ictus','Neurológico','mall','stroke'],
 ['speech','Alteración súbita del habla','Neurológico','dental','stroke',{dialogue:'Intenta hablar pero no encuentra palabras.'}],
 ['weakness','Debilidad unilateral','Neurológico','football','stroke',{dialogue:'No puedo levantar bien un brazo.'}],
 ['allergy-mild','Reacción alérgica sin compromiso respiratorio','Alergia / anafilaxia','dental','allergy'],
 ['anaphylaxis','Anafilaxia tras exposición','Alergia / anafilaxia','mall','anaphylaxis'],
 ['anaphylaxis-breathing','Anafilaxia con dificultad respiratoria','Alergia / anafilaxia','dental','anaphylaxis',{dyspnea:true}],
 ['anaphylaxis-unresponsive','Anafilaxia con pérdida de conciencia','Alergia / anafilaxia','gym','anaphylaxis',{unresponsive:true,difficulty:'Intermedio'}],
 ['fall','Caída durante entrenamiento','Trauma','gym','fall'],
 ['head-injury','Traumatismo craneal deportivo','Trauma','football','head'],
 ['cervical','Sospecha de lesión cervical','Trauma','football','cervical',{difficulty:'Intermedio'}],
 ['bleeding','Herida con hemorragia externa','Trauma','gym','bleeding'],
 ['heatstroke','Golpe de calor · alteración de conciencia','Temperatura / entorno','football','heatstroke',{difficulty:'Intermedio'}],
 ['heat-exhaustion','Agotamiento por calor','Temperatura / entorno','football','heatExhaustion'],
 ['hypothermia','Hipotermia leve','Temperatura / entorno','football','cold'],
 ['fracture','Fractura sospechada tras caída','Trauma','mall','fracture'],
 ['dental-arrest','Parada inesperada durante consulta','Cardiorrespiratorio','dental','arrest',{shocks:1}],
 ['football-glucose','Hipoglucemia durante partido','Glucosa','football','glucose']
].forEach(([id,name,cat,env,type,opts])=>create(id,name,cat,env,type,opts));
if(process.argv.includes('--write')) {
 if(fs.existsSync(target))throw Error('Library exists. Edit JSON directly; seed tool refuses to replace reviewed content.');
 fs.writeFileSync(target,JSON.stringify({schemaVersion:3,actions,references,scenarios},null,2)+'\n');
 console.log('Created '+scenarios.length+' medical scenarios.');
} else console.log('Seed preview: '+scenarios.length+' scenarios. Use --write only for initial creation.');
