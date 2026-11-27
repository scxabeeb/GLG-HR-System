const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
(async()=>{
 const base=process.env.E2E_BASE||'http://127.0.0.1:5199';if(new URL(base).hostname!=='127.0.0.1')throw Error('Use a disposable loopback test server');
 const asForm=values=>{const result=new URLSearchParams();for(const[key,value]of Object.entries(values)){for(const item of(Array.isArray(value)?value:[value]))result.append(key,String(item));}return result;};
 const postFields=values=>({headers:{'Content-Type':'application/x-www-form-urlencoded'},data:asForm(values).toString()});
 const browser=await chromium.launch({...(process.env.CHROME_PATH?{executablePath:process.env.CHROME_PATH}:{}),headless:true});
 try{
  const page=await browser.newPage({viewport:{width:1280,height:1000}});
  const errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.goto(base+'/Identity/Account/Login');await page.locator('#Username').fill(process.env.E2E_USER);await page.locator('#Password').fill(process.env.E2E_PASSWORD);await page.locator('.auth-submit').click();await page.waitForURL('**/Admin/Dashboard');
  const csrfRejected=await page.request.post(base+'/Admin/Roles?handler=Create',postFields({name:'CSRF blocked role',capabilities:['Employee']}));if(csrfRejected.status()!==400)throw Error('Role creation accepts a missing CSRF token');
  await page.goto(base+'/Admin/Roles');await page.getByText('+ Create role',{exact:true}).click();
  const creation=page.locator('form[action*="handler=Create"]');await creation.locator('[name=name]').fill('Browser HR coordinator');await creation.locator('input[value=HR]').check();await creation.locator('input[value=Finance]').check();await creation.getByRole('button',{name:'Create role',exact:true}).click();await page.waitForLoadState('networkidle');
  const roleId=new URL(page.url()).searchParams.get('Id');if(!roleId)throw Error('Custom role creation did not open its editor');
  const roleUrl=base+'/Admin/Roles?Id='+roleId;
  async function saveRole(denied=[],capabilities=['Employee','HR','Finance']){
   await page.goto(roleUrl);const form=page.locator('.role-permission-form');const fields=await form.evaluate(form=>Object.fromEntries(new FormData(form)));
   const payload={__RequestVerificationToken:fields.__RequestVerificationToken,id:fields.id,capabilities};
   for(const key of Object.keys(fields).filter(k=>k.startsWith('access[')))payload[key]=denied.includes(key.slice(7,-1))?'Deny':'Allow';
   const response=await page.request.post(new URL(await form.getAttribute('action'),base).href,postFields(payload));const html=await response.text();if(response.status()!==200 || !html.includes('Role permissions saved')){const validation=await page.evaluate(html=>[...new DOMParser().parseFromString(html,'text/html').querySelectorAll('.validation-summary-errors li')].map(li=>li.textContent),html);throw Error('Role matrix save failed: HTTP '+response.status()+'; '+validation.join('; '));}
  }
  await saveRole(['tasks.manage']);
  await page.goto(base+'/Admin/Users');const create=page.locator('form[action*="handler=Create"]');
  const freeProfile=await create.locator('select[name=employeeId] option').filter({hasText:'PAGE-'}).first().getAttribute('value');
  const csrf=await create.locator('input[name=__RequestVerificationToken]').inputValue();
  const created=await page.request.post(new URL(await create.getAttribute('action'),base).href,{form:{__RequestVerificationToken:csrf,username:'role.operator',email:'role-operator@example.test',password:'Access!Initial123',role:'Browser HR coordinator',employeeId:freeProfile}});
  if(created.status()!==200 || !(await created.text()).includes('Account created'))throw Error('Cannot create an employee-linked account with a custom role');
  await page.goto(base+'/Admin/Users?Search=role.operator');let card=page.locator('.user-card');const userId=await card.locator('form[action*="handler=Roles"] input[name=id]').inputValue();
  const assignment=card.locator('form[action*="handler=Roles"]');const assignmentToken=await assignment.locator('input[name=__RequestVerificationToken]').inputValue();
  const assigned=await page.request.post(new URL(await assignment.getAttribute('action'),base).href,postFields({__RequestVerificationToken:assignmentToken,id:userId,roles:['Employee','Browser HR coordinator','Management']}));
  if(assigned.status()!==200 || !(await assigned.text()).includes('Roles saved'))throw Error('Multiple role assignment failed');
  await page.goto(base+'/Admin/Users?Search=role.operator');card=page.locator('.user-card');if(!await card.locator('input[name=roles][value=Employee]').isChecked() || !await card.locator('input[name=roles][value=Management]').isChecked() || !(await card.textContent()).includes('PAGE-'))throw Error('Role assignment lost the linked employee or selected roles');
  const staff=await browser.newContext();const staffPage=await staff.newPage();
  async function mobileLogin(password){const response=await staff.request.post(base+'/api/mobile/v2/auth/login',{data:{username:'role.operator',password}});if(response.status()!==200)throw Error('Role fixture mobile login failed');return(await response.json()).accessToken;}
  let token=await mobileLogin('Access!Initial123');let response=await staff.request.post(base+'/api/mobile/v2/auth/password',{headers:{Authorization:'Bearer '+token},data:{current:'Access!Initial123',password:'Access!Permanent123'}});if(response.status()!==200)throw Error('Custom-role account cannot change initial password');token=(await response.json()).accessToken;
  await staffPage.goto(base+'/Identity/Account/Login');await staffPage.locator('#Username').fill('role.operator');await staffPage.locator('#Password').fill('Access!Permanent123');await staffPage.locator('.auth-submit').click();await staffPage.waitForURL(url=>!url.pathname.includes('/Login'));
  const auth=()=>({Authorization:'Bearer '+token});
  if((await staff.request.get(base+'/api/mobile/v2/manage/options?module=tasks',{headers:auth()})).status()!==403)throw Error('Role menu denial did not apply on mobile');
  if((await staff.request.get(base+'/Operations/Tasks?view=created')).status()!==403)throw Error('Role menu denial did not apply on web');
  if((await staff.request.get(base+'/Hr/Employees')).status()!==200)throw Error('Custom HR capability did not authorize employee records');
  const adminDenied=await staff.request.get(base+'/Admin/Roles',{maxRedirects:0});if(!(adminDenied.status()===403 || adminDenied.status()===302 && adminDenied.headers().location.includes('/Denied')))throw Error('Custom HR role can access role administration');
  async function userOverride(choice){
   await page.goto(base+'/Admin/Users?Search=role.operator');const form=page.locator('.menu-permission-form');const fields=await form.evaluate(form=>Object.fromEntries(new FormData(form)));
   const payload={};for(const[key,value]of Object.entries(fields))payload[key]=key.startsWith('access[')?'Inherit':value;payload['access[tasks.manage]']=choice;
   const result=await page.request.post(new URL(await form.getAttribute('action'),base).href,postFields(payload));if(result.status()!==200 || !(await result.text()).includes('Menu permissions saved'))throw Error('User permission override failed');
  }
  await userOverride('Allow');if((await staff.request.get(base+'/api/mobile/v2/manage/options?module=tasks',{headers:auth()})).status()!==200)throw Error('User Allow did not override role denial');
  await userOverride('Deny');if((await staff.request.get(base+'/api/mobile/v2/manage/options?module=tasks',{headers:auth()})).status()!==403)throw Error('User Deny did not revoke access');
  await userOverride('Inherit');if((await staff.request.get(base+'/api/mobile/v2/manage/options?module=tasks',{headers:auth()})).status()!==403)throw Error('Inherit did not restore the role restriction');
  await saveRole([]);if((await staff.request.get(base+'/api/mobile/v2/manage/options?module=tasks',{headers:auth()})).status()!==200)throw Error('Role Allow did not restore access on the existing mobile session');
  await saveRole([],['Employee']);const reduced=await staff.request.get(base+'/Hr/Employees',{maxRedirects:0});if(!(reduced.status()===403 || reduced.status()===302 && reduced.headers().location.includes('/Denied')))throw Error('Removed custom HR capability remains active on the old cookie');await saveRole([]);
  await page.goto(base+'/Admin/Users?Search=role.operator');await page.locator('.user-card details').first().locator('summary').first().click();await page.setViewportSize({width:390,height:844});await page.waitForTimeout(350);if(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1))throw Error('User access editor overflows on mobile');await page.locator('.user-card').scrollIntoViewIfNeeded();await page.screenshot({path:'/private/tmp/garowe-user-role-management-mobile.png',fullPage:false});await page.locator('.user-section-nav a').filter({hasText:'Menu access'}).click();if(!await page.locator('.menu-permission-form').isVisible())throw Error('Account section navigation failed');await page.setViewportSize({width:1280,height:1000});await page.locator('.user-row').scrollIntoViewIfNeeded();await page.screenshot({path:'/private/tmp/garowe-user-management-desktop.png',fullPage:false});
  const reset=page.locator('form[action*="handler=ResetPassword"]');const resetToken=await reset.locator('input[name=__RequestVerificationToken]').inputValue();
  response=await page.request.post(new URL(await reset.getAttribute('action'),base).href,{form:{__RequestVerificationToken:resetToken,id:userId,password:'Access!Reset456',confirm:'Access!Reset456'}});if(response.status()!==200 || !(await response.text()).includes('Temporary password saved'))throw Error('Administrator password reset failed');
  if((await staff.request.get(base+'/api/mobile/v2/auth/me',{headers:auth()})).status()!==401)throw Error('Reset password did not revoke old mobile token');
  const oldCookie=await staff.request.get(base+'/Hr/Employees',{maxRedirects:0});if(oldCookie.status()!==302 || !oldCookie.headers().location.includes('/Login'))throw Error('Reset password did not revoke the old cookie immediately');
  token=await mobileLogin('Access!Reset456');response=await staff.request.get(base+'/api/mobile/v2/auth/me',{headers:auth()});if(!(await response.json()).mustChangePassword)throw Error('Reset password does not require a new password');
  response=await staff.request.post(base+'/api/mobile/v2/auth/password',{headers:auth(),data:{current:'Access!Reset456',password:'Access!Final789'}});token=(await response.json()).accessToken;
  await page.goto(base+'/Admin/Users?Search=role.operator');const toggle=page.locator('form[action*="handler=Toggle"]');const toggleToken=await toggle.locator('input[name=__RequestVerificationToken]').inputValue();const toggleUrl=new URL(await toggle.getAttribute('action'),base).href;
  await page.request.post(toggleUrl,{form:{__RequestVerificationToken:toggleToken,id:userId}});if((await staff.request.get(base+'/api/mobile/v2/auth/me',{headers:auth()})).status()!==401)throw Error('Inactive account retains mobile access');
  await page.request.post(toggleUrl,{form:{__RequestVerificationToken:toggleToken,id:userId}});if((await staff.request.get(base+'/api/mobile/v2/auth/me',{headers:auth()})).status()!==401)throw Error('Reactivation revived an old revoked token');
  token=await mobileLogin('Access!Final789');if((await staff.request.get(base+'/api/mobile/v2/auth/me',{headers:auth()})).status()!==200)throw Error('Active account cannot sign in again');
  if((await staff.request.get(base+'/Admin/Roles',{maxRedirects:0})).status()===200)throw Error('Custom HR role gained administrator role management');
  await staff.close();
  if(errors.length)throw Error(errors.join('; '));
  console.log('PASS custom roles, employee multi-role assignment, role/user menu precedence, web/mobile enforcement, password reset, immediate cookie/token revocation, active/inactive transitions and mobile layout');
 }finally{await browser.close();}
})().catch(error=>{console.error(error.message);process.exit(1);});
