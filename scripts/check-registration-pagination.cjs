const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
const path=require('path');
(async()=>{
 const base=process.env.E2E_BASE||'http://127.0.0.1:5199';if(new URL(base).hostname!=='127.0.0.1')throw Error('Use an isolated loopback server');
 const browser=await chromium.launch({...(process.env.CHROME_PATH?{executablePath:process.env.CHROME_PATH}:{}),headless:true});
 try{
  const page=await browser.newPage({viewport:{width:390,height:844}});
  await page.goto(base+'/js/registration-pagination.js?Search=keep');
  await page.setContent('<style>body{margin:16px;font:14px Arial}.fixture{max-width:100%;overflow:auto}table{width:100%}</style><form id="records"><div class="fixture"><table id="demo" data-registration-list="demo"><thead><tr><th>Name</th><th>Select</th></tr></thead><tbody>'+Array.from({length:205},(_,i)=>'<tr id="record-'+i+'"><td>Record '+i+'</td><td><input type="checkbox" name="selected" value="'+i+'"/></td></tr>').join('')+'</tbody><tfoot><tr><th>Total</th><td>205</td></tr></tfoot></table></div></form><div id="teams" data-registration-list="teams">'+Array.from({length:25},(_,i)=>'<article id="team-'+i+'"><h2>Team '+i+'</h2><div id="subtask-'+i+'">Responsibility</div></article>').join('')+'</div><table id="organization" data-registration-list="organization" data-row-start=".parent"><thead><tr><th>Unit</th></tr></thead><tbody>'+Array.from({length:25},(_,i)=>'<tr class="parent"><td>Department '+i+'</td></tr><tr><td>Sub 1</td></tr><tr><td>Sub 2</td></tr>').join('')+'</tbody></table><table id="empty" data-registration-list="empty"><thead><tr><th>No records</th></tr></thead><tbody></tbody></table>');
  await page.addStyleTag({path:path.join(__dirname,'../HrSystemDhg.Web/wwwroot/css/portal.css')});
  await page.addScriptTag({path:path.join(__dirname,'../HrSystemDhg.Web/wwwroot/js/registration-pagination.js')});
  const visible=()=>page.locator('#demo > tbody > tr:visible').count();
  const footer=page.locator('#demo').locator('xpath=following-sibling::div[contains(@class,"list-pagination")]');
  const size=page.locator('#demo').locator('xpath=preceding-sibling::div[contains(@class,"registration-list-toolbar")]').getByRole('combobox');
  if(await visible()!==20 || !await page.locator('#demo thead').isVisible() || !await page.locator('#demo tfoot').isVisible())throw Error('Default table paging hides headers/totals or returns wrong count');
  await page.locator('#record-0 input').check();await footer.getByRole('button',{name:'Next page',exact:true}).click();if(!await page.locator('#record-20').isVisible()||await page.locator('#record-0').isVisible())throw Error('Next table page fails');
  await footer.getByRole('button',{name:'Previous page',exact:true}).click();if(!await page.locator('#record-0 input').isChecked())throw Error('Pagination loses form selections');
  for(const value of ['50','100','200','All']){await size.selectOption(value);if(await visible()!==(value==='All'?205:Number(value)))throw Error('Shared page size failed: '+value);if(new URL(page.url()).searchParams.get('Search')!=='keep')throw Error('Page control drops existing filters');}
  await size.selectOption('20');await footer.getByRole('button',{name:'Last page',exact:true}).click();if(await visible()!==5)throw Error('Final page count wrong');if(new URL(page.url()).searchParams.get('Search')!=='keep' || !await page.locator('#record-0 input').isChecked())throw Error('Pagination discarded filters or selections');
  if(await page.locator('#teams > article:visible').count()!==20)throw Error('Card list does not page at 20');
  await page.evaluate(()=>location.hash='subtask-24');await page.waitForFunction(()=>!document.querySelector('#team-24').classList.contains('registration-page-hidden'));
  if(!await page.locator('#subtask-24').isVisible()||await page.locator('#team-0').isVisible())throw Error('Deep link did not reveal the entire parent team');
  if(await page.locator('#organization tbody > tr:visible').count()!==60)throw Error('Department pagination splits its sub-departments');
  if(!await page.locator('#empty').locator('xpath=following-sibling::div[contains(@class,"list-pagination")]').getByRole('status').textContent().then(text=>text.includes('0–0 of 0')))throw Error('Empty list summary is wrong');
  await page.evaluate(()=>window.GaroweRegistrationLists.init());if(await page.locator('.registration-list-toolbar').count()!==4)throw Error('List initialization duplicates controls');
  await page.emulateMedia({media:'print'});if(await visible()!==205)throw Error('Printing silently omits paginated records');await page.emulateMedia({media:null});
  await page.screenshot({path:'/private/tmp/garowe-shared-registration-pagination.png',fullPage:false});
  console.log('PASS shared registration pagination: all sizes, independent tables/cards, preserved filters and selections, parent/child groups, deep links, empty lists, repeat initialization and complete print output');
 }finally{await browser.close();}
})().catch(error=>{console.error(error.message);process.exit(1);});
