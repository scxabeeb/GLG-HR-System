(() => {
 const sizes=['20','50','100','200','All'];
 const make=(tag,text,attrs={})=>{const node=document.createElement(tag);if(text!==null)node.textContent=text;Object.entries(attrs).forEach(([key,value])=>node.setAttribute(key,value));return node;};
 const init=(scope=document)=>{
  scope.querySelectorAll('[data-registration-list]').forEach((list,index)=>{
   if(list.dataset.pagerReady)return;
   list.dataset.pagerReady='true';
   const key=(list.id||list.dataset.registrationList+'-'+index).replace(/[^a-z0-9_-]/gi,'-');
   const groups=[];
   if(list.tagName==='TABLE'){
    [...list.tBodies].flatMap(body=>[...body.rows]).filter(row=>[...row.cells].some(cell=>cell.tagName==='TD')).forEach(row=>{
     if(list.dataset.rowStart && !row.matches(list.dataset.rowStart) && groups.length)groups.at(-1).push(row);
     else groups.push([row]);
    });
   }else{
    const rows=list.dataset.listItems?[...list.querySelectorAll(list.dataset.listItems)]:[...list.children];
    rows.forEach(row=>groups.push([row]));
   }
   const sizeKey='size_'+key,pageKey='page_'+key,storageKey='garowe-list-size:'+location.pathname+':'+key;
   let stored='20';try{stored=sessionStorage.getItem(storageKey)||'20';}catch{}
   let size='20',page=1;
   const toolbar=make('div',null,{class:'registration-list-toolbar'});
   const label=make('label','Records per page '),select=make('select',null,{'aria-label':'Records per page'});
   sizes.forEach(value=>select.append(make('option',value,{value})));label.append(select);toolbar.append(label);
   const footer=make('div',null,{class:'list-pagination'}),status=make('p',null,{role:'status','aria-live':'polite'}),nav=make('nav',null,{class:'list-page-links','aria-label':'List pages'});
   footer.append(status,nav);
   list.before(toolbar);list.after(footer);
   const pageCount=()=>size==='All'?1:Math.max(1,Math.ceil(groups.length/Number(size)));
   const updateUrl=()=>{const url=new URL(location.href);url.searchParams.set(sizeKey,size);url.searchParams.set(pageKey,String(page));history.pushState(null,'',url);};
   const render=()=>{
    page=Math.max(1,Math.min(page,pageCount()));select.value=size;
    const start=size==='All'?0:(page-1)*Number(size),end=size==='All'?groups.length:Math.min(groups.length,start+Number(size));
    groups.forEach((group,n)=>group.forEach(row=>{const hidden=n<start||n>=end;row.classList.toggle('registration-page-hidden',hidden);if(hidden)row.querySelectorAll('video,audio').forEach(media=>media.pause());}));
    status.textContent='Showing '+(groups.length?start+1:0)+'–'+end+' of '+groups.length+' records';nav.replaceChildren();
    const button=(text,target,enabled,aria)=>{const node=make('button',text,{type:'button',class:'secondary','aria-label':aria||text});node.disabled=!enabled;node.addEventListener('click',()=>{page=target;render();updateUrl();});nav.append(node);};
    if(pageCount()>1){
     button('First',1,page>1,'First page');button('Previous',page-1,page>1,'Previous page');
     for(let n=Math.max(1,page-2);n<=Math.min(pageCount(),page+2);n++){
      if(n===page)nav.append(make('span',String(n),{'aria-current':'page'}));else button(String(n),n,true,'Page '+n);
     }
     button('Next',page+1,page<pageCount(),'Next page');button('Last',pageCount(),page<pageCount(),'Last page');
    }
   };
   const revealTarget=()=>{
    if(!location.hash)return false;
    let target;try{target=document.getElementById(decodeURIComponent(location.hash.slice(1)));}catch{return false;}
    const n=groups.findIndex(group=>group.some(row=>row===target||row.contains(target)));
    if(n<0||size==='All')return false;
    page=Math.floor(n/Number(size))+1;return true;
   };
   const restore=()=>{const params=new URL(location.href).searchParams;const requested=params.get(sizeKey)||stored;size=sizes.includes(requested)?requested:'20';const raw=Number(params.get(pageKey));page=Number.isSafeInteger(raw)&&raw>0?raw:1;if(!params.has(pageKey))revealTarget();render();};
   select.addEventListener('change',()=>{size=select.value;stored=size;page=1;try{sessionStorage.setItem(storageKey,size);}catch{}render();updateUrl();});
   window.addEventListener('popstate',restore);
   window.addEventListener('hashchange',()=>{if(revealTarget())render();});
   restore();
  });
 };
 window.GaroweRegistrationLists={init};init();
})();
