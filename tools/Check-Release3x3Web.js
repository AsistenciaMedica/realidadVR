async page => {
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  const base = 'http://127.0.0.1:4321';
  await page.goto(base + '/');
  await page.locator('.stats-band .stat-box strong').first().waitFor();
  const counts = await page.locator('.stats-band .stat-box strong').allTextContents();
  if (counts.join(',') !== '03,09,03') throw Error('Incorrect release counts: ' + counts);
  const catalog = await (await page.request.get(base + '/scenarios.json')).json();
  for (const viewport of [{ width: 1440, height: 1000 }, { width: 390, height: 844 }]) {
    await page.setViewportSize(viewport);
    await page.goto(base + '/scenarios');
    await page.locator('.scenario-card').first().waitFor();
    if (await page.locator('.scenario-card').count() !== 3) throw Error('Expected three environments');
    if (await page.locator('a[href="/scenarios/mall"]').count()) throw Error('Archived mall is selectable');
    if (await page.locator('svg.scenario-preview').count() !== 1) throw Error('Street illustration missing');
    for (const environment of catalog.releaseScope.environments) {
      await page.getByRole('link', { name: 'Explorar escenario' }).nth(catalog.releaseScope.environments.indexOf(environment)).click();
      await page.locator('.included-cases li').first().waitFor();
      const actual = await page.locator('.included-cases h4').allTextContents();
      const expected = environment.scenarioIds.map(id => catalog.scenarios.find(s => s.id === id).name);
      if (JSON.stringify(actual) !== JSON.stringify(expected)) throw Error('Wrong cases for ' + environment.id);
      for (const id of environment.scenarioIds) {
        await page.locator(`.included-cases h4 a[href="/scenarios/${id}"]`).click();
        await page.reload();
        const selected = page.locator('.included-cases li[aria-current] h4 a');
        await selected.waitFor();
        if (await selected.getAttribute('href') !== `/scenarios/${id}`) throw Error('Wrong selected case: ' + id);
        if (await page.evaluate(() => document.documentElement.scrollWidth > innerWidth)) throw Error('Horizontal overflow: ' + id);
      }
      await page.goto(base + '/scenarios');
      await page.locator('.scenario-card').first().waitFor();
    }
    await page.locator('img').evaluateAll(images => images.forEach(image => image.loading = 'eager'));
    await page.waitForFunction(() => [...document.images].every(image => image.complete && image.naturalWidth > 0));
    await page.screenshot({ path: `TestResults/release-3x3/portal-${viewport.width}.png`, fullPage: true });
  }
  if (errors.length) throw Error(errors.join('\n'));
  console.log('PASS: three environments, nine combinations, shared case order, deep links, desktop/mobile and no browser errors.');
}
