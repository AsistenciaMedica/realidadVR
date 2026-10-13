async (page) => {
  const failures = [];
  page.on('pageerror', error => failures.push(error.message));
  await page.goto('http://127.0.0.1:4311/');
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.locator('.environment-preview-card').first().waitFor();
  await page.locator('.home-environments').scrollIntoViewIfNeeded();
  await page.waitForFunction(() => [...document.images].every(i => i.complete && i.naturalWidth > 0));
  if (await page.locator('.environment-preview-card').count() !== 3) throw Error('Expected three environment links');
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.screenshot({ path: 'TestResults/reunion-20261005/landing-desktop.png', fullPage: true });
  await page.setViewportSize({ width: 390, height: 844 });
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.screenshot({ path: 'TestResults/reunion-20261005/landing-mobile.png', fullPage: true });
  if (await page.evaluate(() => document.documentElement.scrollWidth > innerWidth)) throw Error('Mobile horizontal overflow');
  await page.goto('http://127.0.0.1:4311/scenarios');
  await page.locator('.scenario-preview').first().waitFor();
  if (await page.locator('.scenario-preview').count() !== 3) throw Error('Missing catalog preview');
  for (const environment of ['gym', 'mall', 'football']) {
    await page.goto('http://127.0.0.1:4311/scenarios/' + environment);
    await page.locator('.scenario-detail-visual img').waitFor();
    await page.waitForFunction(() => [...document.images].every(i => i.complete && i.naturalWidth > 0));
    if (await page.locator('.included-cases li').count() !== 3) throw Error('Expected three cases in ' + environment);
  }
  await page.goto('file:///C:/Users/Juan/Desktop/Intregracion/proyectovr/Builds/Reunion-20261006/Revision/ABRIR-REVISION.html');
  if (await page.locator('article').count() !== 15) throw Error('Review must contain 15 cases');
  for (const environment of ['gym', 'mall', 'football']) {
    await page.locator('[data-filter="' + environment + '"]').click();
    if (await page.locator('article:visible').count() !== 3) throw Error('Gallery filter mismatch');
  }
  await page.locator('[data-view="patient"]').click();
  await page.locator('article:visible').last().scrollIntoViewIfNeeded();
  await page.waitForFunction(() => [...document.querySelectorAll('article:not([hidden]) img')].every(i => i.complete && i.naturalWidth > 0));
  await page.locator('[data-filter="all"]').click();
  await page.locator('[data-view="room"]').click();
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.evaluate(() => { document.querySelectorAll('img').forEach(i => i.loading = 'eager'); window.scrollTo(0, 0); });
  await page.waitForFunction(() => [...document.images].every(i => i.complete && i.naturalWidth > 0));
  await page.screenshot({ path: 'TestResults/reunion-20261005/review-desktop.png', fullPage: true });
  if (failures.length) throw Error(failures.join('\n'));
  console.log('PASS: landing desktop/mobile, 3 environments, 15 detail entries, offline review filters and patient views, no page errors.');
}
