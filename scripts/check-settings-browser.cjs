// Run only against an isolated local test server/database; these checks edit settings and report definitions.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
(async () => {
 const base = process.env.E2E_BASE || 'http://127.0.0.1:5199';
 if (new URL(base).hostname !== '127.0.0.1') throw Error('Settings checks require an isolated loopback server');
 if (!process.env.E2E_USER || !process.env.E2E_PASSWORD) throw Error('Provide synthetic E2E_USER and E2E_PASSWORD');
 const browser = await chromium.launch({ ...(process.env.CHROME_PATH ? {executablePath:process.env.CHROME_PATH} : {}), headless:true });
 try {
 const page = await browser.newPage({viewport:{width:390,height:844}});
 page.setDefaultNavigationTimeout(60000);
 const errors=[];page.on('pageerror',e=>errors.push(e.message));
 async function fit(label) { const dimensions=await page.evaluate(()=>[innerWidth,document.documentElement.scrollWidth]); if(dimensions[1]>dimensions[0]+1)throw Error(label+' overflows'); }
 await page.goto(base+'/Identity/Account/Login');
 await page.locator('#Username').fill(process.env.E2E_USER);await page.locator('#Password').fill(process.env.E2E_PASSWORD);
 await page.locator('.auth-submit').click();await page.waitForURL('**/Admin/Dashboard');
 await page.goto(base+'/Admin/OrgEdit?kind=Branch');
 await page.locator('input[name="Name"]').fill('Blank fields test office');await page.locator('input[name="Code"]').fill('TESTEMPTY');
 await page.getByRole('button',{name:'Save unit',exact:true}).click();await page.waitForURL('**/Admin/Organization');
 const branchEdit=await page.locator('.card').filter({has:page.getByRole('heading',{name:/Blank fields test office/})}).getByRole('link',{name:'Edit branch',exact:true}).getAttribute('href');
 const branchId=new URL(base+branchEdit).searchParams.get('id');
 await page.goto(base+branchEdit);if(await page.locator('input[name="City"]').inputValue()!=='' || await page.locator('input[name="Address"]').inputValue()!=='')throw Error('Blank optional branch fields were not retained');
 await page.getByRole('button',{name:'Save unit',exact:true}).click();await page.waitForURL('**/Admin/Organization');
 await page.goto(base+'/Admin/OrgEdit?kind=Branch');await page.locator('input[name="Name"]').fill('Duplicate office');await page.locator('input[name="Code"]').fill('TESTEMPTY');await page.getByRole('button',{name:'Save unit',exact:true}).click();await page.waitForLoadState('networkidle');if(!(await page.locator('.validation-summary-errors').textContent() || '').includes('unique'))throw Error('Duplicate branch code was not rejected');
 await page.goto(base+'/Admin/OrgEdit?kind=Department');await page.locator('input[name="Name"]').fill('Planning department');await page.locator('input[name="Code"]').fill('PLAN');await page.locator('select[name="ParentId"]').selectOption(branchId);await page.getByRole('button',{name:'Save unit',exact:true}).click();await page.waitForURL('**/Admin/Organization');
 const departmentEdit=await page.locator('.org-unit').filter({hasText:'Planning department'}).getByRole('link',{name:'Edit',exact:true}).getAttribute('href');const departmentId=new URL(base+departmentEdit).searchParams.get('id');
 await page.goto(base+'/Admin/OrgEdit?kind=SubDepartment');await page.locator('input[name="Name"]').fill('Planning desk');await page.locator('select[name="ParentId"]').selectOption(departmentId);await page.getByRole('button',{name:'Save unit',exact:true}).click();await page.waitForURL('**/Admin/Organization');
 await page.goto(base+'/Admin/OrgEdit?kind=Branch');
 const sample=await page.request.get(base+'/Admin/OrgEdit?handler=Sample');
 const importForm=page.locator('form[action*="handler=Import"]');const token=await importForm.locator('input[name="__RequestVerificationToken"]').inputValue();
 const imported=await page.request.post(new URL(await importForm.getAttribute('action'),base).href,{multipart:{__RequestVerificationToken:token,upload:{name:'organization.csv',mimeType:'text/csv',buffer:await sample.body()}}});
 if(imported.status()!==200 || !(await imported.text()).includes('Example office'))throw Error('Organization sample import with blank address failed');
 const adminState=await page.context().storageState();
 const employeeIds=await Promise.all([0,1].map(async n=>{
   const registrationContext=await browser.newContext({storageState:adminState});
   try{
     const registrationPage=await registrationContext.newPage();await registrationPage.goto(base+'/Hr/Register');
     if(n===0 && process.env.REGISTRATION_SCREENSHOT){
       for(const width of [1440,768,390,320]){
         await registrationPage.setViewportSize({width,height:1000});
         await registrationPage.waitForTimeout(300);
         if(await registrationPage.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1))throw Error('Registration overflows at '+width);
         if(width===390 && await registrationPage.locator('#workspace-nav').evaluate(el=>el.getBoundingClientRect().right>1))throw Error('Mobile navigation obscures registration');
         if(width===1440 || width===390)await registrationPage.screenshot({path:process.env.REGISTRATION_SCREENSHOT+'-'+width+'.png',fullPage:true});
       }
     }
     const csrf=await registrationPage.locator('form.employee-sheet [name="__RequestVerificationToken"]').inputValue();
     if(await registrationPage.locator('#employee-login,input[name="Pin"],input[name="InitialPassword"]').count())throw Error('Registration still exposes manual credentials or terminal PIN');
     const response=await registrationContext.request.post(base+'/Hr/Register',{form:{__RequestVerificationToken:csrf,Name:'Automatic employee '+n,Email:'auto'+n+'@example.test',Hire:new Date(Date.now()+3*3600000-86400000).toISOString().slice(0,10),BranchId:branchId,DepartmentId:departmentId,Title:'Officer',Number:'FORGED',InitialPassword:'FORGED',Pin:'FORGED'}});
     const html=await response.text();const match=html.match(/Employee saved: <strong>(EMP\d+)<\/strong>/);
     if(!match)throw Error('Automatic employee registration failed: '+response.status());
     if(html.includes('<dt>Initial password</dt>'))throw Error('Onboarding still displays login credentials');
     return match[1];
   }finally{await registrationContext.close();}
 }));
 if(employeeIds[0]===employeeIds[1] || employeeIds.some(x=>!/^EMP\d{4,}$/.test(x)) || Math.abs(Number(employeeIds[0].slice(3))-Number(employeeIds[1].slice(3)))!==1)throw Error('Employee sequence is invalid');
 const employeeContext=await browser.newContext();
 const nativeLogin=await employeeContext.request.post(base+'/api/mobile/v2/auth/login',{data:{username:employeeIds[0],password:employeeIds[0]+'123'}});
 if(nativeLogin.status()!==200)throw Error('Generated EMPID login failed');
 const session=await nativeLogin.json();
 const auth={Authorization:'Bearer '+session.accessToken};
 const blocked=await employeeContext.request.get(base+'/api/mobile/v2/tasks',{headers:auth});
 if(blocked.status()!==403)throw Error('Initial password change is not enforced');
 const changed=await employeeContext.request.post(base+'/api/mobile/v2/auth/password',{headers:auth,data:{current:employeeIds[0]+'123',password:'Changed!Password123'}});
 if(changed.status()!==200)throw Error('Employee cannot change initial password: '+changed.status());
 const oldLogin=await employeeContext.request.post(base+'/api/mobile/v2/auth/login',{data:{username:employeeIds[0],password:employeeIds[0]+'123'}});
 if(oldLogin.status()!==401)throw Error('Initial password still works after change');
 await employeeContext.close();
 await page.goto(base+'/Hr/Register');
 const employeeImportToken=await page.locator('#employee-import input[name="__RequestVerificationToken"]').inputValue();
 const employeeCsv='Number,Name,Email,Hire,BranchId,DepartmentId,SubDepartmentId,Title,Pin,TerminalId,InitialPassword\n'+[2,3].map(n=>',Imported '+n+',auto'+n+'@example.test,'+new Date(Date.now()+3*3600000-86400000).toISOString().slice(0,10)+','+branchId+','+departmentId+',,Officer,,,').join('\n');
 const employeeImport=await page.request.post(base+'/Hr/Register?handler=Import',{multipart:{__RequestVerificationToken:employeeImportToken,upload:{name:'employees.csv',mimeType:'text/csv',buffer:Buffer.from(employeeCsv)}}});
 if(!(await employeeImport.text()).includes('Imported 2 record(s).'))throw Error('Employee CSV import failed with automatic credentials');
 const employeeExport=await page.request.get(base+'/Hr/Register?handler=Export');
 const exportText=await employeeExport.text();
 if(!exportText.includes('auto2@example.test') || exportText.includes(employeeIds[0]+'123'))throw Error('Employee export failed or exposed passwords');
 const pinExport=await page.request.get(base+'/Hr/Register?handler=Export');
 const csvLines=(await pinExport.text()).trim().split(/\r?\n/).map(line=>line.split(',').map(value=>value.replace(/^"|"$/g,'')));
 const headers=csvLines[0];const automaticRows=csvLines.slice(1).filter(row=>/^auto[0-3]@example.test$/.test(row[headers.indexOf('Email')]));
 const pins=automaticRows.map(row=>row[headers.indexOf('Pin')]);
 if(pins.length!==4 || new Set(pins).size!==4 || pins.some(pin=>!(/^[1-9][0-9]{0,8}$/).test(pin)))throw Error('Automatic terminal PINs are missing or duplicated');
 console.log('PASS automatic unique numeric terminal PINs and hidden registration credentials');
 console.log('PASS employee CSV import generates IDs and accounts, and export omits passwords');
 console.log('PASS concurrent sequential automatic EMP IDs, ignored supplied credentials, generated mobile login and mandatory password change');
 console.log('PASS branch create/edit with blank optional fields, duplicate code validation, department/sub-department creation and organization CSV import');
 await page.goto(base+'/Admin/Devices#devices-register');
 const deviceForm=page.locator('#devices-register form');await deviceForm.locator('input[name="name"]').fill('Branch terminal');await deviceForm.locator('select[name="branchId"]').selectOption(branchId);await deviceForm.locator('input[name="location"]').fill('Reception');await deviceForm.getByRole('button',{name:'Add device',exact:true}).click();await page.waitForLoadState('networkidle');
 await page.goto(base+'/Admin/Devices#devices-list');
 let deviceRow=page.locator('#devices-list tbody tr').filter({hasText:'Branch terminal'});if(!(await deviceRow.textContent()).includes('Blank fields test office'))throw Error('Device registration did not save branch');
 if(await page.locator('select[name="departmentId"]').count()!==0)throw Error('Department selector still appears for devices');
 const deviceAnchor=await deviceRow.getByRole('link',{name:'Edit / configure'}).getAttribute('href');
 await page.goto(base+'/Admin/Devices'+deviceAnchor);const locationForm=page.locator(deviceAnchor+' form[action*="handler=Edit"]');
 const anotherBranch=await locationForm.locator('select[name="branchId"] option').filter({hasText:'Example office'}).getAttribute('value');
 await locationForm.locator('select[name="branchId"]').selectOption(anotherBranch);await locationForm.locator('input[name="name"]').fill('Branch terminal updated');await locationForm.locator('input[name="endpoint"]').fill('Updated reference');await locationForm.getByRole('button',{name:'Save device'}).click();await page.waitForLoadState('networkidle');
 await page.goto(base+'/Admin/Devices#devices-list');deviceRow=page.locator('#devices-list tbody tr').filter({hasText:'Branch terminal'});if(!(await deviceRow.textContent()).includes('Example office'))throw Error('Device reassignment did not persist');
 const deviceCsv=await page.request.get(base+'/Admin/Devices?handler=Export');if(!(await deviceCsv.text()).includes('BranchId'))throw Error('Device export omits branch');
 await page.goto(base+'/Admin/Devices#devices-import');
 const deviceImportForm=page.locator('#devices-import form[action*="handler=Import"]');const deviceImportToken=await deviceImportForm.locator('input[name="__RequestVerificationToken"]').inputValue();
 const deviceSample=await page.request.get(base+'/Admin/Devices?handler=Sample');
 const deviceImport=await page.request.post(new URL(await deviceImportForm.getAttribute('action'),base).href,{multipart:{__RequestVerificationToken:deviceImportToken,upload:{name:'devices.csv',mimeType:'text/csv',buffer:await deviceSample.body()}}});
 if(deviceImport.status()!==200 || !(await deviceImport.text()).includes('Example terminal'))throw Error('Device CSV branch import failed');
 console.log('PASS device branch registration, branch reassignment, branch dropdowns and branch CSV import/export');
 await page.goto(base+'/Admin/Devices#devices-list');
 const editedRow=page.locator('#devices-list tbody tr').filter({hasText:'Branch terminal updated'});if(await editedRow.count()!==1 || !(await editedRow.textContent()).includes('Updated reference'))throw Error('Device edit did not persist');
 await page.locator('select[name="Connection"]').selectOption('unpaired');await page.getByRole('button',{name:'Apply',exact:true}).click();await page.waitForLoadState('networkidle');if(await page.locator('#devices-list tbody tr').filter({hasText:'Branch terminal updated'}).count()!==1)throw Error('Unpaired filter hid manual registration');
 await page.goto(base+'/Admin/Devices?Connection=online');if(await page.locator('#devices-list tbody tr').filter({hasText:'Branch terminal updated'}).count()!==0)throw Error('Online filter includes unpaired device');
 await page.goto(base+'/Admin/Devices#devices-list');page.once('dialog',dialog=>dialog.accept());await page.locator('#devices-list tbody tr').filter({hasText:'Branch terminal updated'}).getByRole('button',{name:'Delete device'}).click();await page.waitForLoadState('networkidle');if(await page.locator('#devices-list tbody tr').filter({hasText:'Branch terminal updated'}).count()!==0)throw Error('Unused device was not deleted');
 const historical=page.locator('#devices-list tbody tr').filter({hasText:'Legacy terminal'});
 if(await historical.count()){page.once('dialog',dialog=>dialog.accept());await historical.getByRole('button',{name:'Delete device'}).click();await page.waitForLoadState('networkidle');if(!(await page.locator('.notice[role="status"]').textContent()).includes('history'))throw Error('Historical device deletion was not blocked');}
 await page.goto(base+'/Admin/OrgEdit?kind=Branch');await page.locator('input[name="Name"]').fill('Delete test branch');await page.locator('input[name="Code"]').fill('DELTEST');await page.getByRole('button',{name:'Save unit',exact:true}).click();await page.waitForURL('**/Admin/Organization');
 let deleteCard=page.locator('.card').filter({has:page.getByRole('heading',{name:/Delete test branch/})});const deleteBranchId=(await deleteCard.getAttribute('id')).split('-')[1];
 await page.goto(base+'/Admin/OrgEdit?kind=Department');await page.locator('input[name="Name"]').fill('Delete test department');await page.locator('input[name="Code"]').fill('DELD');await page.locator('select[name="ParentId"]').selectOption(deleteBranchId);await page.getByRole('button',{name:'Save unit',exact:true}).click();await page.waitForURL('**/Admin/Organization');
 const deleteDeptRow=page.locator('.org-unit').filter({hasText:'Delete test department'});const deleteDepartmentId=(await deleteDeptRow.getAttribute('id')).split('-')[1];
 page.once('dialog',dialog=>dialog.accept());await page.locator('#branch-'+deleteBranchId).getByRole('button',{name:'Delete branch',exact:true}).click();await page.waitForLoadState('networkidle');if(!(await page.locator('.notice[role="status"]').textContent()).includes('cannot be deleted'))throw Error('Branch with child department was deleted');
 await page.goto(base+'/Admin/OrgEdit?kind=SubDepartment');await page.locator('input[name="Name"]').fill('Delete test desk');await page.locator('select[name="ParentId"]').selectOption(deleteDepartmentId);await page.getByRole('button',{name:'Save unit',exact:true}).click();await page.waitForURL('**/Admin/Organization');
 const subRow=page.locator('tr').filter({hasText:'Delete test desk'});page.once('dialog',dialog=>dialog.accept());await subRow.getByRole('button',{name:'Delete sub-department',exact:true}).click();await page.waitForLoadState('networkidle');if(await page.locator('tr').filter({hasText:'Delete test desk'}).count())throw Error('Unused sub-department was not deleted');
 page.once('dialog',dialog=>dialog.accept());await page.locator('#department-'+deleteDepartmentId).getByRole('button',{name:'Delete department',exact:true}).click();await page.waitForLoadState('networkidle');if(await page.locator('#department-'+deleteDepartmentId).count())throw Error('Unused department was not deleted');
 page.once('dialog',dialog=>dialog.accept());await page.locator('#branch-'+deleteBranchId).getByRole('button',{name:'Delete branch',exact:true}).click();await page.waitForLoadState('networkidle');if(await page.locator('#branch-'+deleteBranchId).count())throw Error('Unused branch was not deleted');
 console.log('PASS device editing/status filters, unused device deletion, historical device protection, and organization deletion/child safeguards');


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
 await page.goto(base+'/Admin/Devices#devices-list');
 const connectorRow=page.locator('#devices-list tbody tr').filter({hasText:'Legacy terminal'});
 if(await connectorRow.count()){
   const anchor=await connectorRow.getByRole('link',{name:'Edit / configure',exact:true}).getAttribute('href');await page.goto(base+'/Admin/Devices'+anchor);
   const connectorForm=page.locator(anchor+' form[action*="handler=Connector"]');const connectorToken=await connectorForm.locator('input[name="__RequestVerificationToken"]').inputValue();
   const connectorUrl=new URL(await connectorForm.getAttribute('action'),base).href;
   const download=await page.request.post(connectorUrl,{form:{__RequestVerificationToken:connectorToken}});if(download.status()!==200)throw Error('Connector configuration download failed');
   const config=await download.json();const terminal=config.terminals[0];if(terminal.key.length!==64 || !download.headers()['cache-control'].includes('no-store'))throw Error('Invalid connector configuration security');
   const bridgeUrl=base+'/api/attendance/bridge/'+terminal.id+'/heartbeat';const bridgeHeaders={Authorization:'Bearer '+terminal.key,'X-Terminal-Serial':terminal.serial,'X-Terminal-Address':terminal.address};
   if((await page.request.get(bridgeUrl,{headers:bridgeHeaders})).status()!==200)throw Error('Issued connector key was not accepted');
   await page.request.post(connectorUrl,{form:{__RequestVerificationToken:connectorToken,revoke:'true'}});
   if((await page.request.get(bridgeUrl,{headers:bridgeHeaders})).status()!==401)throw Error('Revoked connector key still works');
 }
 const script=await page.request.get(base+'/Admin/Devices?handler=ConnectorScript');if(script.status()!==200 || !(await script.text()).includes('Private-LAN ADMS receiver'))throw Error('Connector script download failed');
 console.log('PASS admin connector configuration download, device-scoped key issuance/revocation and script download');
 await page.goto(base+'/Hr/Attendance#attendance-clock');
 const clockForm=page.locator('form[action*="handler=ReviewClock"]').first();
 if(await clockForm.count()){
   const actual=new Date(Date.now()+3*3600000-10*60000).toISOString().slice(0,19);
   await clockForm.locator('[name="correctedTime"]').fill(actual);
   await clockForm.locator('[name="reason"]').fill('Confirmed timestamp from synthetic integration fixture');
   await Promise.all([page.waitForNavigation(),clockForm.locator('button[value="correct"]').click()]);
   await page.goto(base+'/Hr/Attendance?ClockHistory=true#attendance-clock');
   if(!await page.locator('#attendance-clock').getByText('Corrected',{exact:true}).count())throw Error('Clock exception correction was not saved');
   console.log('PASS clock review correction and retained review history');
 }
 console.log('PASS saved branding/logo, report creation, selected columns, CSV/PDF/Excel, report disabling and device settings views');
 await page.goto(base+'/Management/Payroll#payroll-manage');
 if(!await page.getByRole('heading',{name:'Payroll approval workflow',exact:true}).isVisible())throw Error('Payroll workflow page failed');
 if(!await page.getByRole('button',{name:'Generate payroll',exact:true}).count())throw Error('Administrator cannot prepare payroll');
 if(await page.locator('form[action*="handler=Generate"] input[name="SalaryDays"]').inputValue()!=='26')throw Error('Payroll default divisor is not 26');
 if(process.env.E2E_PAYROLL_PERIOD){
   await page.setViewportSize({width:1280,height:1000});
   await page.goto(base+'/Management/Payroll?PeriodId='+process.env.E2E_PAYROLL_PERIOD+'#payroll-register');
   await page.locator('[data-payroll-details="payroll-detail-'+process.env.E2E_PAYROLL_LINE+'"]').first().click();
   const dialog=page.locator('dialog[open]');
   if(!await dialog.locator('.payroll-deduction-table').isVisible() || !(await dialog.textContent()).includes('15.38') || !(await dialog.textContent()).includes('check-out missing'))throw Error('Payroll deduction table did not render saved causes and amounts');
   await dialog.getByText('Current attendance',{exact:true}).click();
   if(!(await dialog.locator('.deduction-attendance-context').textContent()).includes('10:30 AM'))throw Error('Current attendance context is missing');
   await page.screenshot({path:'/private/tmp/garowe-payroll-deduction-table.png',fullPage:false});
   await page.goto(base+'/Me/Payslip?id='+process.env.E2E_PAYROLL_LINE);
   if(!await page.getByRole('heading',{name:'Attendance deduction breakdown',exact:true}).isVisible() || !(await page.locator('.payslip-deduction-breakdown').textContent()).includes('15.38'))throw Error('Payslip deduction table is missing');
   await page.screenshot({path:'/private/tmp/garowe-payslip-deduction-table.png',fullPage:true});
   console.log('PASS payroll and payslip deduction tables display source, saved formula and exact amounts');
 }
 await page.goto(base+'/Admin/Users?Search=other@example.test');
 const userCard=page.locator('.user-card').filter({has:page.getByRole('heading',{name:'other@example.test',exact:true})});
 await userCard.locator('details').first().locator('summary').first().click();
 const permissionForm=userCard.locator('form[action*="handler=MenuAccess"]');
 if(await permissionForm.locator('select[name^="access["]').count()<35)throw Error('Menu and submenu catalog missing');
 const tasksModule=permissionForm.locator('details').filter({has:page.locator('select[name="access[tasks.manage]"]')});
 await tasksModule.locator('summary').click();
 await permissionForm.locator('select[name="access[tasks.manage]"]').selectOption('Deny');
 await page.setViewportSize({width:390,height:844});await fit('User menu permissions');
 await page.setViewportSize({width:1280,height:1000});await tasksModule.scrollIntoViewIfNeeded();await page.screenshot({path:'/private/tmp/garowe-user-menu-permissions.png',fullPage:false});
 await permissionForm.getByRole('button',{name:'Save menu permissions',exact:true}).click();
 const permissionContext=await browser.newContext();
 try {
   const staffPage=await permissionContext.newPage();
   await staffPage.goto(base+'/Identity/Account/Login');await staffPage.locator('#Username').fill('other@example.test');await staffPage.locator('#Password').fill(process.env.E2E_PASSWORD);await staffPage.locator('.auth-submit').click();await staffPage.waitForURL(url=>!url.pathname.includes('/Login'));
   const blocked=await staffPage.request.get(base+'/Operations/Tasks?view=created');if(blocked.status()!==403)throw Error('Denied task management URL remains accessible');
   await staffPage.goto(base+'/Operations/Tasks?view=mine');
   if(await staffPage.locator('a[href*="view=created"]').count() || await staffPage.locator('form[action*="handler=Save"]').count())throw Error('Denied task menu/editor remains visible');
 } finally {await permissionContext.close();}
 await page.goto(base+'/Admin/Users?Search=other@example.test');
 await page.locator('.user-card details').first().locator('summary').first().click();
 const restore=page.locator('form[action*="handler=MenuAccess"]');await restore.locator('details').filter({has:page.locator('select[name="access[tasks.manage]"]')}).locator('summary').click();await restore.locator('select[name="access[tasks.manage]"]').selectOption('Allow');await restore.getByRole('button',{name:'Save menu permissions',exact:true}).click();
 console.log('PASS menu/submenu permission editor, direct URL denial and hidden task editor');
 await page.goto(base+'/Admin/Users');
 const createUserForm=page.locator('form[action*="handler=Create"]');
 const userCsrf=await createUserForm.locator('[name="__RequestVerificationToken"]').inputValue();
 for(const [account,role] of [['secretary-check@example.test','General Secretary'],['unlinked-check@example.test','Employee']]) {
   const response=await page.request.post(base+'/Admin/Users?handler=Create',{form:{__RequestVerificationToken:userCsrf,email:account,password:'Secretary!check8401',role,employeeId:''}});
   if(response.status()!==200 || !(await response.text()).includes(account))throw Error('Cannot create '+role+' without employee profile');
 }
 // Usernames are independent of recovery email; both identifiers can sign in.
 for(const form of [
   {username:'city.user',email:'',role:'Employee'},
   {username:'city.manager',email:'manager-check@example.test',role:'Management'}
 ]) {
   const response=await page.request.post(base+'/Admin/Users?handler=Create',{form:{__RequestVerificationToken:userCsrf,password:'Usernames!check8401',employeeId:'',...form}});
   if(response.status()!==200 || !(await response.text()).includes(form.username))throw Error('Username-based creation failed');
 }
 for(const form of [{username:'',email:''},{username:'CITY.MANAGER',email:''},{username:'manager-check@example.test',email:''}]) {
   const response=await page.request.post(base+'/Admin/Users?handler=Create',{form:{__RequestVerificationToken:userCsrf,password:'Usernames!check8401',employeeId:'',role:'Employee',...form}});
   const html=await response.text();if(response.status()!==200 || !html.includes('validation-summary-errors'))throw Error('Missing or duplicate login was accepted');
 }
 for(const loginName of ['city.user','city.manager','manager-check@example.test']) {
   const response=await page.request.post(base+'/api/mobile/v2/auth/login',{data:{username:loginName,password:'Usernames!check8401'}});
   if(response.status()!==200)throw Error('Mobile sign-in failed for username/email account ('+response.status()+')');
 }
 const managerContext=await browser.newContext();
 try {
   const login=await managerContext.request.post(base+'/api/mobile/v2/auth/login',{data:{username:'city.manager',password:'Usernames!check8401'}});
   const changed=await managerContext.request.post(base+'/api/mobile/v2/auth/password',{headers:{Authorization:'Bearer '+(await login.json()).accessToken},data:{current:'Usernames!check8401',password:'Usernames!updated8401'}});
   if(changed.status()!==200)throw Error('Username account mandatory password change failed');
   const managerPage=await managerContext.newPage();await managerPage.goto(base+'/Identity/Account/Login');await managerPage.locator('#Username').fill(' city.manager ');await managerPage.locator('#Password').fill('Usernames!updated8401');await managerPage.locator('.auth-submit').click();await managerPage.waitForURL('**/Management/Dashboard');
 } finally {await managerContext.close();}
 const userExport=await page.request.get(base+'/Admin/Users?handler=Export');
 const userCsv=await userExport.text();if(!userCsv.split('\n')[0].includes('Username') || !userCsv.includes('city.manager') || userCsv.includes('Usernames!check8401'))throw Error('User export lost usernames or included passwords');
 for(const [filename,contents] of [
   ['username-users.csv','Email,Role,EmployeeId,TemporaryPassword,Username\n,Employee,,Import!check8401,import.user\n'],
   ['legacy-users.csv','Email,Role,EmployeeId,TemporaryPassword\nlegacy-check@example.test,Employee,,Import!check8401\n']
 ]) {
   const response=await page.request.post(base+'/Admin/Users?handler=Import',{multipart:{__RequestVerificationToken:userCsrf,upload:{name:filename,mimeType:'text/csv',buffer:Buffer.from(contents)}}});
   if(response.status()!==200 || !(await response.text()).includes(filename.startsWith('username')?'import.user':'legacy-check@example.test'))throw Error('User CSV compatibility failed');
 }
 console.log('PASS username-only, email-only and combined registration; web/mobile sign-in, duplicate rejection, password-free export, new and legacy CSV import');
 const secretaryContext=await browser.newContext();
 try {
   const login=await secretaryContext.request.post(base+'/api/mobile/v2/auth/login',{data:{username:'secretary-check@example.test',password:'Secretary!check8401'}});
   if(login.status()!==200)throw Error('Secretary sign-in failed');
   const temporaryToken=(await login.json()).accessToken;
   const changed=await secretaryContext.request.post(base+'/api/mobile/v2/auth/password',{headers:{Authorization:'Bearer '+temporaryToken},data:{current:'Secretary!check8401',password:'Secretary!updated8401'}});
   if(changed.status()!==200)throw Error('Secretary temporary password change failed');
   const token=(await changed.json()).accessToken;
   const options=await secretaryContext.request.get(base+'/api/mobile/v2/manage/options?module=meetings',{headers:{Authorization:'Bearer '+token}});
   if(options.status()!==200 || !(await options.json()).canAssign)throw Error('Secretary does not inherit Management staff access on mobile');
   const secretaryPage=await secretaryContext.newPage();await secretaryPage.goto(base+'/Identity/Account/Login');await secretaryPage.locator('#Username').fill('secretary-check@example.test');await secretaryPage.locator('#Password').fill('Secretary!updated8401');await secretaryPage.locator('.auth-submit').click();await secretaryPage.waitForURL('**/Management/Dashboard');
   if((await secretaryPage.goto(base+'/Management/Payroll')).status()!==200)throw Error('Secretary cannot open Management payroll');
   const adminOnly=await secretaryPage.request.get(base+'/Admin/Users',{maxRedirects:0});if(adminOnly.status()!==302 || !(adminOnly.headers().location||'').includes('/Denied'))throw Error('Secretary unexpectedly received Admin access');
 } finally {await secretaryContext.close();}
 // A profile can be linked after account creation without generating another login.
 await page.goto(base+'/Admin/Users?Search=unlinked-check@example.test');
 const unlinked=page.locator('.user-card');await unlinked.locator('details').first().locator('summary').first().click();
 const linkForm=unlinked.locator('form[action*="handler=Link"]');
 const linkToken=await linkForm.locator('[name="__RequestVerificationToken"]').inputValue();
 const accountId=await linkForm.locator('[name="id"]').inputValue();
 const available=await linkForm.locator('select option[value]:not([value=""])').first().getAttribute('value');
 if(available){const linked=await page.request.post(base+'/Admin/Users?handler=Link',{form:{__RequestVerificationToken:linkToken,id:accountId,employeeId:available}});if(linked.status()!==200 || !(await linked.text()).includes('Employee profile linked'))throw Error('Cannot link profile after creating account');}
 console.log('PASS optional employee profiles, General Secretary inherits Management on web/mobile without Admin access, and post-registration profile linking');
 console.log('PASS payroll workflow page, clear approval stages and administrator preparation controls');
 if(errors.length) throw Error(errors.join('; '));
 } finally { await browser.close(); }
})().catch(error=>{ console.error(error.message);process.exit(1); });
