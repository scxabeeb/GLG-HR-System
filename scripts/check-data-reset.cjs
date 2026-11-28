// Destructive regression test: ONLY the disposable local fixture is accepted.
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
(async()=>{
 const base=process.env.E2E_BASE||'http://127.0.0.1:5199';if(new URL(base).hostname!=='127.0.0.1')throw Error('Reset checks require a disposable local database');
 const browser=await chromium.launch({headless:true,executablePath:process.env.CHROME_PATH});
 try{
  const page=await browser.newPage();await page.goto(base+'/Identity/Account/Login');await page.locator('#Username').fill(process.env.E2E_USER);await page.locator('#Password').fill(process.env.E2E_PASSWORD);await page.locator('.auth-submit').click();await page.waitForURL('**/Admin/Dashboard');
  const csrf=await page.request.post(base+'/Admin/DataReset',{form:{Confirmation:'RESET EMPLOYEE DATA'}});if(csrf.status()!==400)throw Error('Reset accepts missing CSRF');
  await page.goto(base+'/Admin/DataReset');const before=await page.locator('tbody').textContent();
  if(!/Employees\s*[1-9]/.test(before))throw Error('Reset fixture has no employees');
  await page.getByRole('button',{name:'Create reset backup',exact:true}).click();await page.waitForLoadState('networkidle');
  const id=new URL(page.url()).searchParams.get('BackupId');if(!id)throw Error('Reset backup was not created');
  const backup=await page.request.get(base+'/Admin/DataReset?handler=Download&id='+id);if(backup.status()!==200)throw Error('Reset backup cannot be retrieved');
  const data=await backup.json();if(!data.tables.Employees.length||!data.tables.Users.length||!data.fingerprint)throw Error('Reset backup lacks employee/account recovery data');
  const form=page.locator('form').filter({has:page.locator('input[name=Confirmation]')});const token=await form.locator('[name=__RequestVerificationToken]').inputValue();
  const rejected=await page.request.post(base+'/Admin/DataReset',{form:{__RequestVerificationToken:token,BackupId:id,Confirmation:'wrong'}});if(!(await rejected.text()).includes('type RESET EMPLOYEE DATA exactly'))throw Error('Reset accepts an incorrect confirmation');
  if((await page.locator('tbody').textContent())!==before)throw Error('Invalid reset changed records');
  await page.locator('#Confirmation').fill('RESET EMPLOYEE DATA');await page.getByRole('button',{name:'Delete employee data and employee accounts',exact:true}).click();await page.waitForLoadState('networkidle');
  if(!await page.getByRole('status').filter({hasText:'Employee, attendance, salary, payroll and leave records cleared.'}).count())throw Error('Reset did not complete');
  const counts=await page.locator('tbody tr td:last-child').allTextContents();if(counts.some(x=>x.trim()!=='0'))throw Error('HR records remain after reset');
  if((await page.request.get(base+'/Admin/Devices')).status()!==200||(await page.request.get(base+'/Admin/Organization')).status()!==200)throw Error('Reset lost administrator or configuration access');
  if((await page.request.get(base+'/Admin/DataReset?handler=Download&id='+id)).status()!==200)throw Error('Reset removed its backup');
  await page.getByRole('button',{name:'Create reset backup',exact:true}).click();await page.waitForLoadState('networkidle');
  const afterId=new URL(page.url()).searchParams.get('BackupId');const after=await(await page.request.get(base+'/Admin/DataReset?handler=Download&id='+afterId)).json();
  const oldPins=['Employees','ZkPunchs','ZkPunchReviews','ZkDeviceCommands'].flatMap(key=>data.tables[key].map(row=>Number(row.ZkPin??row.Pin))).filter(value=>Number.isInteger(value)&&value>=0&&value<=999999999);
  const counter=after.tables.EmployeeNumberSequences[0];if(counter.NextNumber!==1||counter.NextPin<Math.max(0,...oldPins)+1)throw Error('Reset reuses a previously assigned terminal PIN');
  for(const key of ['AssetEmployees','AssetActivityEmployees','VehicleDrivers','TripDrivers'])if(JSON.stringify(data.tables[key].map(x=>x.Id))!==JSON.stringify(after.tables[key].map(x=>x.Id)))throw Error('Reset removed records from another module');
  console.log('PASS employee reset: authenticated Admin + CSRF + backup + exact confirmation; HR records removed, administrator/configuration retained, backup retained');
 }finally{await browser.close();}
})().catch(e=>{console.error(e.message);process.exit(1);});
