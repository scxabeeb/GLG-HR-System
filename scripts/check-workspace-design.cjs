// Read-only visual and interaction checks against the disposable fixture.
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
const fs=require('fs');
(async()=>{
 const base=process.env.E2E_BASE||'http://127.0.0.1:5199';
 if(new URL(base).hostname!=='127.0.0.1')throw Error('Design checks require a disposable local server');
 const out=process.env.DESIGN_SCREENSHOTS||'/private/tmp/garowe-design-review/screenshots';fs.mkdirSync(out,{recursive:true});
 const browser=await chromium.launch({headless:true,executablePath:process.env.CHROME_PATH});
 try{
  const page=await browser.newPage();const errors=[];page.on('pageerror',e=>errors.push(e.message));
  async function fit(label){if(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1))throw Error(label+' overflows the viewport');}
  for(const width of [1440,768,390,320]){
   await page.setViewportSize({width,height:960});await page.goto(base+'/Identity/Account/Login');await fit('Login '+width);
   if(await page.locator('.civic-content').evaluate(e=>getComputedStyle(e).paddingTop)!=='0px')throw Error('Login is not edge-to-edge');
   if(width<=760 && await page.locator('#Password').evaluate(e=>parseFloat(getComputedStyle(e).fontSize))<16)throw Error('Mobile login input triggers browser zoom');
   if(!await page.locator('#Username').isVisible() || !await page.locator('#Password').isVisible())throw Error('Login inputs missing');
   if(width===1440||width===390)await page.screenshot({path:out+'/login-'+width+'.png',fullPage:true});
  }
  await page.setViewportSize({width:390,height:960});
  await page.locator('#Username').fill('language-check@example.test');await page.locator('#Password').fill('temporary-test-value');
  await page.locator('#workspace-language').selectOption('so');await page.locator('#workspace-theme').click();
  if(await page.locator('html').getAttribute('lang')!=='so'||await page.locator('html').getAttribute('data-theme')!=='dark')throw Error('Display preferences did not apply');
  if(!await page.getByRole('heading',{name:'Soo gal goobtaada shaqada'}).isVisible())throw Error('Somali sign-in translation missing');
  if(await page.locator('#Username').inputValue()!=='language-check@example.test'||await page.locator('#Password').inputValue()!=='temporary-test-value')throw Error('Language selection changed entered credentials');
  await fit('Somali dark login');await page.screenshot({path:out+'/login-somali-dark.png',fullPage:true});
  await page.reload();if(await page.locator('html').getAttribute('lang')!=='so'||await page.locator('html').getAttribute('data-theme')!=='dark')throw Error('Display preferences did not persist');
  await page.locator('#workspace-language').selectOption('en');await page.locator('#workspace-theme').click();
  await page.locator('#Username').fill(process.env.E2E_USER);await page.locator('#Password').fill(process.env.E2E_PASSWORD);
  await page.locator('.auth-submit').click();await page.waitForURL('**/Admin/Dashboard');
  await page.setViewportSize({width:1440,height:1000});await page.locator('#workspace-language').selectOption('so');await page.locator('#workspace-theme').click();
  await page.screenshot({path:out+'/dashboard-somali-dark.png',fullPage:true});await fit('Somali dark dashboard');
  await page.locator('#workspace-language').selectOption('en');await page.locator('#workspace-theme').click();
  for(const width of [1440,1024,768,390,320]){
   await page.setViewportSize({width,height:1000});await page.goto(base+'/Admin/Dashboard');await fit('Dashboard '+width);
   if(!await page.locator('.dashboard-welcome').isVisible())throw Error('Dashboard welcome panel missing');
   if(width===1440||width===390)await page.screenshot({path:out+'/dashboard-'+width+'.png',fullPage:true});
  }
  await page.setViewportSize({width:390,height:844});await page.locator('.menu-toggle').click();
  if(!await page.locator('#nav-search').evaluate(e=>e===document.activeElement))throw Error('Menu search does not receive keyboard focus');
  await page.locator('#nav-search').fill('Payroll');
  const payroll=page.locator('#workspace-nav a[href="/Management/Payroll"]').first();
  if(!await payroll.isVisible())throw Error('Menu search leaves matching submenu collapsed');
  await page.screenshot({path:out+'/menu-mobile.png',fullPage:false});
  await page.keyboard.press('Escape');if(await page.locator('body').evaluate(e=>e.classList.contains('nav-open')))throw Error('Escape does not close navigation');
  for(const route of ['/Hr/Employees','/Hr/Attendance','/Hr/Leave','/Hr/Salary','/Management/Payroll','/Operations/Tasks?view=created','/Operations/Meetings?mode=manage','/Assets','/Operations/Fleet','/Admin/Settings','/Reports/All','/Reports?report=Employees']){
   for(const width of [1440,390]){
    await page.setViewportSize({width,height:960});const response=await page.goto(base+route);if(response.status()>=400)throw Error(route+' returned '+response.status());
    await fit(route+' '+width);
    if(route==='/Reports/All'||route==='/Reports?report=Employees')await page.screenshot({path:out+'/'+(route.includes('?')?'report-results':'reports')+'-'+width+'.png',fullPage:true});
   }
  }
  await page.setViewportSize({width:1440,height:960});await page.goto(base+'/Admin/Dashboard');
  if((await page.locator('#expand-menus').textContent()).includes('Expand'))await page.locator('#expand-menus').click();
  await page.locator('#expand-menus').click();await page.reload();
  if(await page.locator('#workspace-nav details[data-menu][open]').count())throw Error('Collapsed menus reopen after refresh');
  const activeWorkspace=page.locator('details[data-menu=workspace]');await activeWorkspace.locator(':scope > summary').click();await page.waitForTimeout(80);await page.reload();
  if(!await activeWorkspace.getAttribute('open') && await activeWorkspace.getAttribute('open')===null)throw Error('Expanded menu preference was lost');
  await activeWorkspace.locator(':scope > summary').click();await page.waitForTimeout(80);await page.reload();
  if(await activeWorkspace.getAttribute('open')!==null)throw Error('Active menu ignores explicit collapsed preference');
  if(errors.length)throw Error(errors.join('; '));
  console.log('PASS responsive login/dashboard at 320–1440px, focusable searchable menu, and 12 module layouts; screenshots: '+out);
 }finally{await browser.close();}
})().catch(e=>{console.error(e.message);process.exit(1);});
