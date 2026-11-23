// Run only against an isolated local test server/database; these checks edit settings and report definitions.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
(async () => {
 const base = process.env.E2E_BASE || 'http://127.0.0.1:5199';
 if (new URL(base).hostname !== '127.0.0.1') throw Error('Settings checks require an isolated loopback server');
 if (!process.env.E2E_USER || !process.env.E2E_PASSWORD) throw Error('Provide synthetic E2E_USER and E2E_PASSWORD');
 const browser = await chromium.launch({ ...(process.env.CHROME_PATH ? {executablePath:process.env.CHROME_PATH} : {}), headless:true });
 try {
 const page = await browser.newPage({viewport:{width:390,height:844}});
 const errors=[];page.on('pageerror',e=>errors.push(e.message));
 async function fit(label) { const dimensions=await page.evaluate(()=>[innerWidth,document.documentElement.scrollWidth]); if(dimensions[1]>dimensions[0]+1)throw Error(label+' overflows'); }
 await page.goto(base+'/Identity/Account/Login');
 await page.locator('#Username').fill(process.env.E2E_USER);await page.locator('#Password').fill(process.env.E2E_PASSWORD);
 await page.locator('.auth-submit').click();await page.waitForURL('**/Admin/Dashboard');
 await page.goto(base+'/Admin/Settings#settings-general');
 const preferences=page.locator('form').filter({has:page.locator('[name="Input.CompanyName"]')});
 await preferences.locator('[name="Input.CompanyName"]').fill('Test City Administration');
 await preferences.locator('[name="Input.PortalName"]').fill('Test Civic Workspace');
 await preferences.locator('[name="Input.ShortName"]').fill('Test City');
 await preferences.locator('[name="Input.PortalSubtitle"]').fill('Civic workspace');
 await preferences.getByRole('button',{name:'Save preferences'}).click();await page.waitForLoadState('networkidle');
 if(await page.locator('.brand-name').textContent()!=='Test City')throw Error('Saved brand did not reach navigation');
 const logo=page.locator('form[action*="handler=Logo"]');
 await logo.locator('input[type=file]').setInputFiles(require('path').resolve(__dirname, '../HrSystemDhg.Web/wwwroot/images/garowe-local-government.jpg'));
 await logo.getByRole('button',{name:'Upload logo',exact:true}).click();await page.waitForLoadState('networkidle');
 let logoResponse=await page.request.get(base+'/branding/logo');if(logoResponse.status()!==200 || !logoResponse.headers()['content-type'].includes('image/jpeg'))throw Error('Logo upload failed');
 await page.goto(base+'/Admin/ReportViews?Source=Attendance#report-editor');
 if(await page.locator('[name="SelectedColumns"]').count()!==8)throw Error('Report dataset columns failed');
 await page.locator('select[name="Source"]').selectOption('Employees');await page.getByRole('button',{name:'Choose dataset'}).click();await page.waitForLoadState('networkidle');
 if(!await page.locator('[name="Input.Name"]').isVisible())throw Error('Dataset selection did not keep editor visible');

 await page.locator('[name="Input.Name"]').fill('Employee contact view');
 await page.locator('[name="Input.Description"]').fill('Configured report test');
 for(const field of await page.locator('[name="SelectedColumns"]').all())await field.uncheck();
 await page.locator('[name="SelectedColumns"][value="0"]').check();await page.locator('[name="SelectedColumns"][value="1"]').check();
 await page.getByRole('button',{name:'Save report',exact:true}).click();await page.waitForLoadState('networkidle');
 await page.locator('[data-workspace-link][href="#report-library"]').click();
 const savedLink=page.getByRole('link',{name:'Open report',exact:true});if(await savedLink.count()!==1)throw Error('Report definition did not save: '+await page.locator('.validation-summary-errors').allTextContents());
 const savedUrl=await savedLink.getAttribute('href');
 await savedLink.click();await page.waitForLoadState('networkidle');if(!await page.getByRole('heading',{name:'Employee contact view',exact:true}).first().isVisible())throw Error('Custom report title missing');
 const csv=await page.request.get(base+savedUrl+'&format=csv');if(csv.status()!==200)throw Error('Custom CSV failed');
 const csvText=await csv.text();if(!csvText.includes('Number') || !csvText.includes('Name') || csvText.includes('Email'))throw Error('Custom column projection failed');
 const pdf=await page.request.get(base+savedUrl+'&format=pdf');if(pdf.status()!==200 || !pdf.headers()['content-type'].includes('application/pdf'))throw Error('Custom PDF failed');
 const excel=await page.request.get(base+savedUrl+'&format=excel');if(excel.status()!==200 || !excel.headers()['content-type'].includes('spreadsheet'))throw Error('Custom Excel failed');
 await page.goto(base+'/Reports/All#reports-saved');if(!await page.getByRole('link',{name:/Employee contact view/}).isVisible())throw Error('Custom report absent from hub');
 await fit('Custom report hub');
 await page.goto(base+'/Admin/ReportViews');await page.getByRole('button',{name:'Disable',exact:true}).click();await page.waitForLoadState('networkidle');if((await page.request.get(base+savedUrl)).status()!==404)throw Error('Disabled report is accessible');
 await page.goto(base+'/Admin/Settings#settings-general');await page.locator('form[action*="handler=Logo"]').getByRole('button',{name:'Restore default logo'}).click();await page.waitForLoadState('networkidle');if(await page.locator('[name="Input.PortalName"]').inputValue()!=='Test Civic Workspace')throw Error('Logo change reset organization settings');
 for(const id of ['devices-list','devices-register','devices-config','devices-setup','devices-import']){await page.goto(base+'/Admin/Devices#'+id);if(!await page.locator('#'+id).isVisible())throw Error('Device settings pane failed: '+id);await fit(id);}
 console.log('PASS saved branding/logo, report creation, selected columns, CSV/PDF/Excel, report disabling and device settings views');
 if(errors.length) throw Error(errors.join('; '));
 } finally { await browser.close(); }
})().catch(error=>{ console.error(error.message);process.exit(1); });
