// Read-only crawl of rendered internal navigation in the isolated browser fixture.
exports.audit = async (page, base) => {
 const origin = new URL(base).origin;
 const queue = ['/Admin/Dashboard','/Admin/Settings','/Reports/All','/Reports/Hub','/Me/Approvals','/Hr/Employees','/Hr/Register','/Hr/Attendance','/Hr/Leave','/Hr/Salary','/Management/Payroll','/Operations/Tasks','/Operations/Meetings?mode=manage','/Assets','/Operations/Fleet','/Admin/OrgEdit?kind=Branch','/Admin/OrgEdit?kind=Department','/Admin/OrgEdit?kind=SubDepartment'];
 const visited = new Set();const failures=[];let fragments=0;
 const key = url => {const u=new URL(url,base);u.hash='';u.pathname=u.pathname.toLowerCase().replace(/\/index$/,'');u.searchParams.sort();return u.href;};
 while(queue.length && visited.size<160) {
   const input=queue.shift();const url=new URL(input,base);url.hash='';
   if(visited.has(key(url)))continue;visited.add(key(url));
   const response=await page.request.get(url.href);
   if(response.status()>=400){failures.push(response.status()+' '+url.pathname+url.search);continue;}
   if(!response.headers()['content-type']?.includes('text/html'))continue;
   const data=await page.evaluate(html=>{const doc=new DOMParser().parseFromString(html,'text/html');return {ids:[...doc.querySelectorAll('[id]')].map(n=>n.getAttribute('id')),links:[...doc.querySelectorAll('a[href]')].map(a=>({href:a.getAttribute('href'),text:a.textContent.trim().slice(0,70)}))};},await response.text());
   const ids=new Set(data.ids);
   for(const link of data.links) {
     let next;try{next=new URL(link.href,response.url());}catch{continue;}
     if(next.origin!==origin || /\/(identity|files|api)\//i.test(next.pathname) || /\.(css|js|png|jpg|jpeg|svg|pdf)$/i.test(next.pathname) || [...next.searchParams.keys()].some(k=>k.toLowerCase()==='handler'))continue;
     if(key(next)===key(response.url()) && next.hash) {
       fragments++;
       let id;try{id=decodeURIComponent(next.hash.slice(1));}catch{continue;}
       if(!ids.has(id))failures.push('Missing section '+next.pathname+next.search+next.hash+' ('+link.text+')');
     }
     next.hash='';if(!visited.has(key(next)))queue.push(next.href);
   }
 }
 if(failures.length)throw Error('Navigation audit:\n'+[...new Set(failures)].slice(0,35).join('\n'));
 console.log('PASS read-only navigation audit: '+visited.size+' rendered pages and '+fragments+' section links');
};
