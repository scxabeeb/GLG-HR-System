(() => {
 document.querySelectorAll('[data-invite-picker]').forEach(picker => {
  const rows=[...picker.querySelectorAll('[data-invite-option]')];
  const checks=rows.map(row=>row.querySelector('input[type=checkbox]'));
  const search=picker.querySelector('[data-invite-search]');
  const limit=Number(picker.dataset.maxInvitees)||100;
  const status=picker.querySelector('[data-invite-status]');
  const normalized=value=>value.normalize('NFKD').toLocaleLowerCase().trim();
  const update=()=>{
   const selected=checks.filter(box=>box.checked);
   picker.querySelector('[data-invite-count]').textContent=selected.length;
   picker.querySelector('[data-selected-invites]').hidden=selected.length===0;
   const list=picker.querySelector('[data-selected-list]');list.replaceChildren();
   selected.forEach(box=>{const item=document.createElement('li');const name=document.createElement('span');name.textContent=box.dataset.inviteName;
    const remove=document.createElement('button');remove.type='button';remove.className='secondary';remove.textContent='Remove';remove.setAttribute('aria-label','Remove '+box.dataset.inviteName);
    remove.addEventListener('click',()=>{box.checked=false;update();});item.append(name,remove);list.append(item);});
   picker.querySelector('[data-clear-invites]').disabled=selected.length===0;
  };
  search.addEventListener('keydown',event=>{if(event.key==='Enter')event.preventDefault();});
  search.addEventListener('input',()=>{
   const term=normalized(search.value);let visible=0;
   rows.forEach(row=>{row.hidden=!normalized(row.dataset.search).includes(term);if(!row.hidden)visible++;});
   picker.querySelector('[data-invite-results]').textContent=visible+' matching employees';
   picker.querySelector('[data-invite-empty]').hidden=visible!==0;
  });
  checks.forEach(box=>box.addEventListener('change',()=>{
   if(checks.filter(item=>item.checked).length>limit){box.checked=false;status.textContent='Select up to '+limit+' invitees. Remove someone before adding another.';}
   else status.textContent='The organizer is included automatically.';
   update();
  }));
  picker.querySelector('[data-clear-invites]').addEventListener('click',()=>{checks.forEach(box=>box.checked=false);status.textContent='Selection cleared.';update();});
  picker.querySelector('[data-select-matching]').addEventListener('click',()=>{
   let count=checks.filter(box=>box.checked).length;
   rows.filter(row=>!row.hidden).forEach(row=>{const box=row.querySelector('input');if(!box.checked&&count<limit){box.checked=true;count++;}});
   status.textContent=count===limit?'Selected up to the '+limit+'-invitee limit. Refine the search to choose a smaller group.':'Matching employees selected.';update();
  });
  update();
 });
})();
