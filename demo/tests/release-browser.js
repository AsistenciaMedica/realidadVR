async page => {
  const base = 'http://127.0.0.1:4311';
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  const catalog = await (await page.request.get(base + '/api/catalog')).json();
  if (catalog.scenarios.length !== 15 || catalog.releaseScope.environments.length !== 3) throw Error('Release scope mismatch');
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.goto(base);
  await page.getByText('casos por escenario', { exact: true }).waitFor();
  if ((await page.locator('.stats-band .stat-box strong').allTextContents()).join(',') !== '03,15,05') throw Error('Landing counts mismatch');
  await page.goto(base + '/scenarios');
  await page.locator('.scenario-card').first().waitFor();
  if (await page.locator('.scenario-card').count() !== 3) throw Error('Environment count');
  for (const environment of catalog.releaseScope.environments) {
    await page.goto(base + '/scenarios/' + environment.id);
    await page.locator('.included-cases li').first().waitFor();
    const expected = environment.scenarioIds.map(id => catalog.scenarios.find(scenario => scenario.id === id).name);
    if (JSON.stringify(await page.locator('.included-cases h4').allTextContents()) !== JSON.stringify(expected)) throw Error('Case names mismatch: ' + environment.id);
    const patients = environment.scenarioIds.map(id => catalog.scenarios.find(scenario => scenario.id === id).patientIdentity);
    const rows = await page.locator('.included-cases li').allTextContents();
    for (let i = 0; i < patients.length; i++) {
      if (!rows[i].includes(patients[i].displayName) || !rows[i].includes(patients[i].age + ' años')) throw Error('Patient identity mismatch: ' + environment.id);
      if (/\uFFFD|\p{L}\?\p{L}/u.test(rows[i])) throw Error('Damaged UTF-8 text: ' + environment.id);
    }
  }
  for (const scenario of catalog.scenarios) {
    await page.goto(base + '/scenarios/' + scenario.id);
    await page.reload();
    await page.locator('.included-cases li[aria-current]').waitFor();
    if ((await page.locator('.included-cases li[aria-current] h4').textContent()) !== scenario.name) throw Error('Case reload mismatch: ' + scenario.id);
    if (!(await page.locator('.included-cases li[aria-current]').textContent()).includes(scenario.patientIdentity.displayName)) throw Error('Patient missing on deep link: ' + scenario.id);
  }
  for (const excluded of ['dental', 'dental-arrest', 'review-hypotension-v1']) {
    if ((await page.request.get(base + '/scenarios/' + excluded)).status() !== 404) throw Error('Excluded scenario exposed');
  }
  await page.goto(base + '/scenarios.html');
  await page.locator('.scenario-card').first().waitFor();
  if (await page.locator('.scenario-card').count() !== 15) throw Error('Legacy catalog count');
  await page.locator('#environment').selectOption('gym');
  if (await page.locator('.scenario-card').count() !== 5) throw Error('Environment filter');
  const clinicalCase = catalog.scenarios.find(scenario => scenario.catalogOnly);
  await page.getByRole('button', { name: 'Revisar ' + clinicalCase.name, exact: true }).click();
  await page.locator('dialog[open]').waitFor();
  if (!(await page.locator('#detail-content').textContent()).includes('comunicación')) throw Error('Clinical v2 detail');
  await page.keyboard.press('Escape');
  await page.setViewportSize({ width: 390, height: 844 });
  for (const path of ['/', '/scenarios', ...catalog.releaseScope.environments.map(environment => '/scenarios/' + environment.id)]) {
    await page.goto(base + path);
    await page.locator(path === '/' ? '.stats-band .stat-box strong' : path === '/scenarios' ? '.scenario-card' : '.included-cases li').first().waitFor();
    if (await page.evaluate(() => document.documentElement.scrollWidth > innerWidth)) throw Error('Mobile overflow: ' + path);
  }
  await page.screenshot({ path: 'TestResults/vital-release-mobile.png', fullPage: true });
  if (errors.length) throw Error(errors.join('\n'));
  console.log('PASS 3 scenarios, 15 case names and patient identities, all 15 case reloads, UTF-8, excluded content, v2 legacy detail and mobile layouts.');
}
