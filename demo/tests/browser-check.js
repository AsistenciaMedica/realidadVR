async page => {
  await page.goto('http://127.0.0.1:4311/index.html');
  await page.locator('.progress-row').first().waitFor();
  if (await page.locator('.progress-row').count() !== 13) throw Error('Missing progress rows');
  await page.getByRole('button', {name:'Escenarios', exact:true}).click();
  if (await page.locator('.progress-row').count() !== 3) throw Error('Scenario filter failed');
  await page.getByRole('tab', {name:'Meta Quest 3', exact:true}).click();
  if (!(await page.locator('#mode-quest').isVisible())) throw Error('Quest tab failed');
  await page.getByRole('tab', {name:'Sin gafas · Windows', exact:true}).click();
  await page.getByRole('button', {name:'Ampliar captura del gimnasio'}).click();
  if (!(await page.locator('dialog').isVisible())) throw Error('Image dialog failed');
  await page.keyboard.press('Escape');
  await page.setViewportSize({width:1440,height:1000});
  await page.screenshot({path:'TestResults/client-portal-desktop.png',fullPage:true});
  await page.setViewportSize({width:390,height:844});
  await page.screenshot({path:'TestResults/client-portal-mobile.png',fullPage:true});
  if (await page.evaluate(() => document.documentElement.scrollWidth > innerWidth)) throw Error('Mobile horizontal overflow');
  console.log('Portal browser checks passed: tabs, filters, image dialog, mobile width.');
}
