import { randomBytes,randomUUID,createHmac } from 'node:crypto';
import { existsSync,readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { openStore } from './store.mjs';
import { hash,passwordMatches,encrypt,decrypt,fault,text,oneOf,body,RateLimiter } from './security.mjs';

export function createApi(root,options={}){
 const production=options.production??process.env.NODE_ENV==='production';
 const localPath=resolve(root,'data/local-config.json');
 const local=!production&&existsSync(localPath)?JSON.parse(readFileSync(localPath,'utf8')):{};
 const config={adminUser:process.env.ADMIN_USER||local.adminUser||'admin',adminHash:process.env.ADMIN_PASSWORD_HASH||local.adminHash||'',encryptionKey:process.env.LICENSE_ENCRYPTION_KEY||local.encryptionKey||'',origin:process.env.PUBLIC_ORIGIN||'',bypass:process.env.DEVELOPMENT_LICENSE_BYPASS==='true',...options};
 let validOrigin=false;try{const url=new URL(config.origin);validOrigin=url.protocol==='https:'&&url.origin===config.origin;}catch{}
 if(production&&(!/^[a-f0-9]{32}:[a-f0-9]{128}$/.test(config.adminHash)||!config.encryptionKey||!validOrigin||config.bypass))throw Error('Production requires ADMIN_PASSWORD_HASH, LICENSE_ENCRYPTION_KEY, HTTPS PUBLIC_ORIGIN without path; bypass must be disabled.');
 const key=config.encryptionKey?Buffer.from(config.encryptionKey,'base64'):null;
 if(key&&key.length!==32)throw Error('LICENSE_ENCRYPTION_KEY must be 32 random bytes encoded in base64.');
 const db=openStore(options.dbPath||process.env.DATABASE_PATH||resolve(root,'data/vital.sqlite'));
 const sessions=new Map(),limiter=new RateLimiter();
 const catalogPath=resolve(root,'public/scenarios.json');const catalog=existsSync(catalogPath)?JSON.parse(readFileSync(catalogPath,'utf8')):{scenarios:[],actions:[],references:[]};
 const all=(sql,...args)=>db.prepare(sql).all(...args),get=(sql,...args)=>db.prepare(sql).get(...args),run=(sql,...args)=>db.prepare(sql).run(...args);
 const audit=(action,id)=>run('INSERT INTO audit(createdAt,action,entityId) VALUES(?,?,?)',new Date().toISOString(),action,id);
 const cookieName=production?'__Host-vital_session':'vital_session';
 const setCookie=(res,value,age)=>res.setHeader('Set-Cookie',`${cookieName}=${value}; Path=/; HttpOnly; SameSite=Strict; Max-Age=${age}${production?'; Secure':''}`);
 function session(req){const token=(req.headers.cookie||'').split(';').map(s=>s.trim()).find(s=>s.startsWith(cookieName+'='))?.slice(cookieName.length+1);const s=token?sessions.get(hash(token)):null;if(!s||s.expires<Date.now()){if(token)sessions.delete(hash(token));return null;}return s;}
 function sameOrigin(req){const expected=config.origin||`http://${req.headers.host}`;if(req.headers.origin&&req.headers.origin!==expected)throw fault(403,'Origen no permitido.');if(req.headers['sec-fetch-site']==='cross-site')throw fault(403,'Origen no permitido.');}
 function admin(req,mutation=false){const s=session(req);if(!s)throw fault(401,'Inicia sesión.');if(mutation){sameOrigin(req);if(req.headers['x-csrf-token']!==s.csrf)throw fault(403,'Sesión inválida. Recarga la página.');}return s;}
 function expiry(value){if(value===null||value==='')return null;const d=new Date(text(value,40,true));if(!Number.isFinite(d.getTime()))throw fault(400,'Fecha inválida.');return d.toISOString();}
 function email(value){const v=text(value,254,true);if(!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v))throw fault(400,'Email inválido.');return v;}
 function statusOf(l){return !['REVOKED','SUSPENDED'].includes(l.status)&&l.expiresAt&&l.expiresAt<=new Date().toISOString()?'EXPIRED':l.status;}
 function licenseView(l){const {keyHash,keyCipher,...safe}=l;return {...safe,status:statusOf(l),licenseKey:'VITAL-••••-••••-••••-'+l.keySuffix,devices:get("SELECT COUNT(*) AS n FROM activations WHERE licenseId=? AND status='ACTIVE'",l.id).n};}
 function client(data,old={}){const x={...old,...data};return {name:text(x.name,150,true),legalName:text(x.legalName),email:email(x.email),phone:text(x.phone,60),contactName:text(x.contactName,150),status:oneOf(x.status||'ACTIVE',['ACTIVE','SUSPENDED','INACTIVE']),notes:text(x.notes,2000)};}
 const features=['medical-scenarios','windows','quest','json-export'];
 async function handle(req,res,path){
  if(!path.startsWith('/api/')||path==='/api/releases')return false;
  const send=(status,data)=>{res.writeHead(status,{'Content-Type':'application/json; charset=utf-8','Cache-Control':'no-store'});res.end(req.method==='HEAD'?undefined:JSON.stringify(data));};
  try{
   const ip=req.socket.remoteAddress||'unknown';
   if(!limiter.allow('global:'+ip,600,60000))throw fault(429,'Demasiadas solicitudes. Intenta más tarde.');
   if(path==='/api/catalog'&&req.method==='GET'){send(200,catalog);return true;}
   if(path==='/api/auth/login'&&req.method==='POST'){
    sameOrigin(req);if(!limiter.allow('login:'+ip,10,900000))throw fault(429,'Demasiados intentos. Intenta más tarde.');
    if(!config.adminHash||!key)throw fault(503,'Administrador sin configurar. Ejecuta la configuración local o define variables de producción.');
    const data=await body(req);const valid=await passwordMatches(text(data.password,256,true),config.adminHash);
    if(!valid||text(data.username,100,true)!==config.adminUser)throw fault(401,'Credenciales incorrectas.');
    for(const [id,s]of sessions)if(s.expires<Date.now())sessions.delete(id);
    if(sessions.size>=1000)throw fault(429,'Límite de sesiones.');
    const token=randomBytes(32).toString('base64url'),csrf=randomBytes(24).toString('base64url');sessions.set(hash(token),{csrf,expires:Date.now()+8*3600000});setCookie(res,token,8*3600);audit('admin.login','admin');send(200,{authenticated:true,csrf});return true;
   }
   if(path==='/api/auth/session'&&req.method==='GET'){const s=admin(req);send(200,{authenticated:true,csrf:s.csrf,username:config.adminUser});return true;}
   if(path==='/api/auth/logout'&&req.method==='POST'){const s=admin(req,true);for(const [id,value]of sessions)if(value===s)sessions.delete(id);setCookie(res,'',0);send(200,{ok:true});return true;}
   if(path==='/api/contact'&&req.method==='POST'){
    sameOrigin(req);if(!limiter.allow('contact:'+ip,5,3600000))throw fault(429,'Límite de solicitudes alcanzado.');const d=await body(req);if(d.consent!==true)throw fault(400,'Confirma el tratamiento de tus datos de contacto.');
    const id=randomUUID();run('INSERT INTO inquiries VALUES(?,?,?,?,?,?,?)',id,text(d.name,150,true),text(d.company,150),email(d.email),text(d.phone,60),text(d.message,3000,true),new Date().toISOString());send(201,{ok:true,id,message:'Solicitud registrada. El equipo podrá consultarla en administración.'});return true;
   }
   if(['/api/licenses/validate','/api/licenses/activate','/api/licenses/deactivate'].includes(path)&&req.method==='POST'){
    if(!limiter.allow('license:'+ip,100,60000))throw fault(429,'Demasiadas solicitudes de licencia.');
    const d=await body(req),licenseKey=text(d.licenseKey,100,true).toUpperCase(),device=text(d.deviceId,200,true),platform=oneOf(d.platform,['WINDOWS','QUEST']);
    if(config.bypass&&!production&&['127.0.0.1','::1','::ffff:127.0.0.1'].includes(ip)&&licenseKey==='DEVELOPMENT'){send(200,{valid:true,developmentBypass:true,client:'Desarrollo local',expiresAt:null,features});return true;}
    if(!key)throw fault(503,'Licencias sin configurar.');
    const l=get('SELECT * FROM licenses WHERE keyHash=?',hash(licenseKey));const c=l?get('SELECT * FROM clients WHERE id=?',l.clientId):null;
    if(!l||!c||statusOf(l)!=='ACTIVE'||c.status!=='ACTIVE'){send(200,{valid:false,reason:'LICENSE_UNAVAILABLE'});return true;}
    const deviceHash=createHmac('sha256',key).update(l.id+':'+device).digest('hex');
    const current=get('SELECT * FROM activations WHERE licenseId=? AND deviceId=?',l.id,deviceHash);
    if(current&&current.platform!==platform){send(200,{valid:false,reason:'DEVICE_PLATFORM_MISMATCH'});return true;}
    if(path.endsWith('/deactivate')){if(current)run("UPDATE activations SET status='INACTIVE' WHERE id=?",current.id);audit('device.deactivate',current?.id||l.id);send(200,{valid:false,deactivated:true});return true;}
    if(path.endsWith('/activate')){
     db.exec('BEGIN IMMEDIATE');try{
      const count=get("SELECT COUNT(*) AS n FROM activations WHERE licenseId=? AND status='ACTIVE'",l.id).n;
      if(current?.status!=='ACTIVE'&&count>=l.maxDevices){db.exec('ROLLBACK');send(200,{valid:false,reason:'DEVICE_LIMIT'});return true;}
      const now=new Date().toISOString();
      if(current)run("UPDATE activations SET status='ACTIVE',lastSeenAt=?,deviceName=? WHERE id=?",now,text(d.deviceName,150)||'Dispositivo',current.id);
      else run('INSERT INTO activations VALUES(?,?,?,?,?,?,?,?)',randomUUID(),l.id,deviceHash,text(d.deviceName,150)||'Dispositivo',platform,now,now,'ACTIVE');
      run('UPDATE licenses SET activatedAt=COALESCE(activatedAt,?) WHERE id=?',now,l.id);audit('device.activate',l.id);db.exec('COMMIT');
     }catch(e){db.exec('ROLLBACK');throw e;}
    }else if(current?.status!=='ACTIVE'){send(200,{valid:false,reason:'ACTIVATION_REQUIRED'});return true;}
    else run('UPDATE activations SET lastSeenAt=? WHERE id=?',new Date().toISOString(),current.id);
    send(200,{valid:true,client:c.name,expiresAt:l.expiresAt,features,checkAfterSeconds:3600});return true;
   }
   if(path.startsWith('/api/admin/')){
    admin(req,!['GET','HEAD'].includes(req.method));
    if(path==='/api/admin/dashboard'&&req.method==='GET'){
     const licenses=all('SELECT * FROM licenses').map(licenseView),soon=Date.now()+30*86400000;
     send(200,{activeClients:get("SELECT COUNT(*) AS n FROM clients WHERE status='ACTIVE'").n,activeLicenses:licenses.filter(l=>l.status==='ACTIVE').length,expiringLicenses:licenses.filter(l=>l.status==='ACTIVE'&&l.expiresAt&&Date.parse(l.expiresAt)<=soon).length,activeDevices:get("SELECT COUNT(*) AS n FROM activations WHERE status='ACTIVE'").n,scenarios:catalog.scenarios.length,recentActivations:all('SELECT id,licenseId,deviceName,platform,activatedAt,status FROM activations ORDER BY activatedAt DESC LIMIT 10'),inquiries:all('SELECT * FROM inquiries ORDER BY createdAt DESC LIMIT 50')});return true;
    }
    if(path==='/api/admin/scenarios'&&req.method==='GET'){send(200,catalog);return true;}
    if(path==='/api/admin/clients'&&req.method==='GET'){send(200,all('SELECT * FROM clients ORDER BY createdAt DESC'));return true;}
    if(path==='/api/admin/clients'&&req.method==='POST'){
     const d=client(await body(req)),id=randomUUID(),now=new Date().toISOString();run('INSERT INTO clients VALUES(?,?,?,?,?,?,?,?,?)',id,d.name,d.legalName,d.email,d.phone,d.contactName,d.status,now,d.notes);audit('client.create',id);send(201,{id,...d,createdAt:now});return true;
    }
    let match=path.match(/^\/api\/admin\/clients\/([a-f0-9-]+)$/);
    if(match&&['GET','PATCH'].includes(req.method)){
     const old=get('SELECT * FROM clients WHERE id=?',match[1]);if(!old)throw fault(404,'Cliente no encontrado.');
     if(req.method==='GET'){const licenses=all('SELECT * FROM licenses WHERE clientId=?',old.id).map(licenseView);send(200,{...old,licenses,activations:all('SELECT a.id,a.licenseId,a.deviceName,a.platform,a.activatedAt,a.lastSeenAt,a.status FROM activations a JOIN licenses l ON l.id=a.licenseId WHERE l.clientId=?',old.id)});return true;}
     const d=client(await body(req),old);run('UPDATE clients SET name=?,legalName=?,email=?,phone=?,contactName=?,status=?,notes=? WHERE id=?',d.name,d.legalName,d.email,d.phone,d.contactName,d.status,d.notes,old.id);audit('client.update',old.id);send(200,{...old,...d});return true;
    }
    if(path==='/api/admin/licenses'&&req.method==='GET'){send(200,all('SELECT * FROM licenses ORDER BY createdAt DESC').map(licenseView));return true;}
    if(path==='/api/admin/licenses'&&req.method==='POST'){
     if(!key)throw fault(503,'Cifrado sin configurar.');const d=await body(req),c=get('SELECT * FROM clients WHERE id=?',text(d.clientId,100,true));if(!c)throw fault(400,'Selecciona un cliente existente.');
     const max=Number(d.maxDevices);if(!Number.isInteger(max)||max<1||max>1000)throw fault(400,'Límite de dispositivos inválido.');
     const licenseKey='VITAL-'+randomBytes(16).toString('hex').toUpperCase().match(/.{8}/g).join('-'),id=randomUUID(),now=new Date().toISOString();
     run('INSERT INTO licenses VALUES(?,?,?,?,?,?,?,?,?,?,?)',id,c.id,hash(licenseKey),encrypt(licenseKey,key),licenseKey.slice(-8),oneOf(d.status||'ACTIVE',['ACTIVE','PENDING']),now,null,expiry(d.expiresAt||null),max,text(d.notes,2000));audit('license.create',id);send(201,{...licenseView(get('SELECT * FROM licenses WHERE id=?',id)),licenseKey});return true;
    }
    match=path.match(/^\/api\/admin\/licenses\/([a-f0-9-]+)(\/reveal)?$/);
    if(match&&['GET','PATCH','POST'].includes(req.method)){
     const l=get('SELECT * FROM licenses WHERE id=?',match[1]);if(!l)throw fault(404,'Licencia no encontrada.');
     if(match[2]&&req.method==='POST'){audit('license.reveal',l.id);send(200,{licenseKey:decrypt(l.keyCipher,key)});return true;}
     if(!match[2]&&req.method==='GET'){send(200,licenseView(l));return true;}
     if(!match[2]&&req.method==='PATCH'){
      const d=await body(req);const status=oneOf(d.status||l.status,['ACTIVE','SUSPENDED','EXPIRED','REVOKED','PENDING']);if(l.status==='REVOKED'&&status!=='REVOKED')throw fault(409,'Una clave revocada no se reactiva; genera otra.');
      const max=d.maxDevices===undefined?l.maxDevices:Number(d.maxDevices);if(!Number.isInteger(max)||max<1||max>1000||max<licenseView(l).devices)throw fault(409,'Límite inferior a dispositivos activos o fuera de rango.');
      const clientId=d.clientId===undefined?l.clientId:text(d.clientId,100,true);if(!get('SELECT id FROM clients WHERE id=?',clientId))throw fault(400,'Cliente no encontrado.');
      if(clientId!==l.clientId&&get('SELECT COUNT(*) AS n FROM activations WHERE licenseId=?',l.id).n>0)throw fault(409,'Una licencia con historial de dispositivos no se reasigna.');
      run('UPDATE licenses SET clientId=?,status=?,expiresAt=?,maxDevices=?,notes=? WHERE id=?',clientId,status,d.expiresAt===undefined?l.expiresAt:expiry(d.expiresAt),max,d.notes===undefined?l.notes:text(d.notes,2000),l.id);audit('license.update',l.id);send(200,licenseView(get('SELECT * FROM licenses WHERE id=?',l.id)));return true;
     }
    }
    if(path==='/api/admin/activations'&&req.method==='GET'){send(200,all('SELECT id,licenseId,deviceName,platform,activatedAt,lastSeenAt,status FROM activations ORDER BY activatedAt DESC'));return true;}
    match=path.match(/^\/api\/admin\/activations\/([a-f0-9-]+)$/);
    if(match&&req.method==='PATCH'){const d=await body(req);if(d.status!=='INACTIVE')throw fault(400,'Solo se permite desactivar desde administración.');const change=run("UPDATE activations SET status='INACTIVE' WHERE id=?",match[1]);if(!change.changes)throw fault(404,'Activación no encontrada.');audit('device.admin-deactivate',match[1]);send(200,{ok:true});return true;}
   }
   send(404,{error:'Ruta API no encontrada.'});return true;
  }catch(error){send(error.status||500,{error:error.status?error.message:'No se pudo completar la operación.'});return true;}
 }
 handle.close=()=>db.close();handle.hasScenario=id=>catalog.scenarios.some(s=>s.id===id);handle.hasEnvironment=id=>Boolean(catalog.releaseScope?.environments?.some(e=>e.id===id)&&catalog.scenarios.some(s=>s.environment===id));return handle;
}
