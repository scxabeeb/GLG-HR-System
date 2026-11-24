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
     const csrf=await registrationPage.locator('form.employee-sheet [name="__RequestVerificationToken"]').inputValue();
     const response=await registrationContext.request.post(base+'/Hr/Register',{form:{__RequestVerificationToken:csrf,Name:'Automatic employee '+n,Email:'auto'+n+'@example.test',Hire:new Date(Date.now()+3*3600000-86400000).toISOString().slice(0,10),BranchId:branchId,DepartmentId:departmentId,Title:'Officer',Number:'FORGED',InitialPassword:'FORGED'}});
     const html=await response.text();const match=html.match(/<dt>Username<\/dt><dd>(EMP\d+)<\/dd>/);
     if(!match)throw Error('Automatic employee registration failed: '+response.status());
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
 if(errors.length) throw Error(errors.join('; '));
 } finally { await browser.close(); }
})().catch(error=>{ console.error(error.message);process.exit(1); });
