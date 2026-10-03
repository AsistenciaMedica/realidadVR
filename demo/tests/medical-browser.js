async page=>{
 const errors=[];page.on('pageerror',e=>errors.push(e.message));
 const base='http://127.0.0.1:4311';
 const catalog=await (await page.request.get(base+'/api/catalog')).json();
 if(catalog.scenarios.length!==15||catalog.releaseScope.environments.length!==3)throw Error('Release scope mismatch');
 await page.setViewportSize({width:1440,height:1000});await page.goto(base+'/');
 await page.getByText('casos por escenario',{exact:true}).waitFor();
 if((await page.locator('.stats-band .stat-box strong').allTextContents()).join(',')!=='03,15,05')throw Error('Landing counts mismatch');
 await page.screenshot({path:'TestResults/vital-landing-desktop.png'});
 await page.goto(base+'/scenarios');await page.locator('.scenario-card').first().waitFor();if(await page.locator('.scenario-card').count()!==3)throw Error('Environment catalog count');
 for(const environment of catalog.releaseScope.environments){
  await page.goto(base+'/scenarios/'+environment.id);await page.locator('.included-cases li').first().waitFor();
  const expected=environment.scenarioIds.map(id=>catalog.scenarios.find(s=>s.id===id).name);
  if(JSON.stringify(await page.locator('.included-cases h4').allTextContents())!==JSON.stringify(expected))throw Error('Case names mismatch: '+environment.id);
  await page.locator('.included-cases h4 a').first().click();await page.reload();await page.locator('.included-cases li[aria-current]').waitFor();
 }
 for(const excluded of ['dental','dental-arrest','review-hypotension-v1'])if((await page.request.get(base+'/scenarios/'+excluded)).status()!==404)throw Error('Excluded scenario exposed');
 await page.goto(base+'/scenarios.html');await page.locator('.scenario-card').first().waitFor();if(await page.locator('.scenario-card').count()!==15)throw Error('Legacy catalog count');
 await page.locator('#environment').selectOption('gym');if(await page.locator('.scenario-card').count()!==5)throw Error('Environment filter');
 const clinicalCase=catalog.scenarios.find(s=>s.catalogOnly);await page.getByRole('button',{name:'Revisar '+clinicalCase.name,exact:true}).click();await page.locator('dialog[open]').waitFor();
 if(!(await page.locator('#detail-content').textContent()).includes('comunicación'))throw Error('Clinical v2 catalog detail');
 await page.keyboard.press('Escape');await page.getByRole('button',{name:'Limpiar',exact:true}).click();await page.locator('#environment').selectOption('football');await page.locator('#procedure').selectOption('StartCPR');if(await page.locator('.scenario-card').count()!==1)throw Error('Procedure filter');
 await page.locator('.scenario-card button').first().click();await page.locator('dialog[open]').waitFor();await page.keyboard.press('Escape');
 await page.setViewportSize({width:390,height:844});await page.goto(base+'/scenarios');await page.locator('.scenario-card').first().waitFor();await page.screenshot({path:'TestResults/vital-catalog-mobile.png'});
 if(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth))throw Error('Catalog overflow');
 await page.goto(base+'/scenarios/gym');await page.locator('.included-cases li').first().waitFor();if(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth))throw Error('Detail overflow');
 await page.goto(base+'/');await page.getByText('casos por escenario',{exact:true}).waitFor();await page.screenshot({path:'TestResults/vital-landing-mobile.png'});if(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth))throw Error('Landing overflow');
 await page.context().clearCookies();await page.goto('http://127.0.0.1:4311/admin/clients');await page.locator('#login').waitFor();await page.locator('[name=username]').fill('admin');await page.locator('[name=password]').fill('Test-only-password-123!');await page.getByRole('button',{name:'Iniciar sesión',exact:true}).click();
 const clientName='TEST · Centro de formación '+Date.now();await page.getByRole('button',{name:'Crear cliente',exact:true}).click();await page.locator('#editor [name=name]').fill(clientName);await page.locator('#editor [name=email]').fill('test@example.com');await page.locator('#editor').getByRole('button',{name:'Guardar',exact:true}).click();await page.locator('#editor').waitFor({state:'hidden'});
 await page.getByRole('link',{name:'Licencias',exact:true}).click();await page.getByRole('button',{name:'Generar licencia',exact:true}).click();await page.locator('#editor').getByRole('button',{name:'Generar clave segura',exact:true}).click();await page.getByLabel('Clave de licencia',{exact:true}).waitFor();const key=await page.getByLabel('Clave de licencia',{exact:true}).inputValue();if(!key.startsWith('VITAL-'))throw Error('License not generated');await page.locator('#close-editor').click();
 await page.getByRole('searchbox',{name:'Buscar',exact:true}).fill(key.slice(-8));await page.getByRole('button',{name:'Suspender',exact:true}).click();await page.getByRole('searchbox',{name:'Buscar',exact:true}).fill(key.slice(-8));await page.getByRole('button',{name:'Reactivar',exact:true}).waitFor();await page.getByRole('button',{name:'Reactivar',exact:true}).click();await page.getByRole('searchbox',{name:'Buscar',exact:true}).fill(key.slice(-8));await page.getByRole('button',{name:'Suspender',exact:true}).waitFor();
 await page.screenshot({path:'TestResults/vital-admin-mobile.png'});if(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth))throw Error('Admin overflow');
 const activation=await page.request.post('http://127.0.0.1:4311/api/licenses/activate',{data:{licenseKey:key,deviceId:'browser-'+Date.now(),deviceName:'Visor de prueba',platform:'QUEST'}});if(!(await activation.json()).valid)throw Error('Fixture activation failed');
 await page.getByRole('link',{name:'Dispositivos',exact:true}).click();await page.getByRole('button',{name:'Desactivar',exact:true}).click();await page.getByText('INACTIVE',{exact:true}).first().waitFor();
 await page.getByRole('link',{name:'Escenarios',exact:true}).click();await page.getByRole('link',{name:'Consultar',exact:true}).first().waitFor();if(await page.getByRole('link',{name:'Consultar',exact:true}).count()!==15)throw Error('Admin catalog mismatch');
 await page.getByRole('link',{name:'Licencias',exact:true}).click();await page.getByRole('searchbox',{name:'Buscar',exact:true}).fill(key.slice(-8));await page.getByRole('button',{name:'Revocar',exact:true}).click();await page.getByRole('button',{name:'Confirmar revocación',exact:true}).click();await page.locator('#editor').waitFor({state:'hidden'});await page.getByRole('searchbox',{name:'Buscar',exact:true}).fill(key.slice(-8));await page.getByText('REVOKED',{exact:true}).waitFor();
 await page.setViewportSize({width:1440,height:1000});await page.getByRole('link',{name:'Resumen',exact:true}).click();await page.locator('.admin-stats').waitFor();await page.screenshot({path:'TestResults/vital-admin-desktop.png'});
 await page.getByRole('button',{name:'Cerrar sesión',exact:true}).click();await page.locator('#login').waitFor();
 await page.setViewportSize({width:390,height:844});await page.goto('http://127.0.0.1:4311/contact.html');await page.locator('[name=name]').fill('Prueba navegador');await page.locator('[name=email]').fill('browser@example.com');await page.locator('[name=message]').fill('Solicito una demo de revisión.');await page.locator('[name=consent]').check();await page.getByRole('button',{name:'Solicitar demostración',exact:true}).click();await page.getByText('Solicitud registrada.',{exact:false}).waitFor();await page.screenshot({path:'TestResults/vital-contact-mobile.png'});if(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth))throw Error('Contact overflow');
 if(errors.length)throw Error(errors.join('\n'));console.log('PASS landing, responsive catalog, filters, slug reload, admin authentication, client/license create, suspend/reactivate, logout and contact.');
}

