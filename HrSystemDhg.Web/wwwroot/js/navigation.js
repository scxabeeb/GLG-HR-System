(() => {
 const groups = [...document.querySelectorAll('#workspace-nav details[data-menu]')];
 const all = document.getElementById('expand-menus');
 const path = location.pathname.replace(/\/$/, '') || '/';
 const read = key => { try { return localStorage.getItem(key); } catch { return null; } };
 const save = (key, value) => { try { localStorage.setItem(key, value); } catch {} };
 const update = () => { const expanded = groups.every(g => g.open); all.textContent = expanded ? 'Collapse all' : 'Expand all'; all.setAttribute('aria-label', expanded ? 'Collapse all menu groups' : 'Expand all menu groups'); };
 groups.forEach(group => {
   group.open = read('dhg-menu-' + group.dataset.menu) === 'open';
   group.querySelectorAll('a').forEach(link => {
     if ((link.pathname.replace(/\/$/, '') || '/') === path) { link.setAttribute('aria-current', 'page'); group.open = true; }
   });
   group.addEventListener('toggle', () => { save('dhg-menu-' + group.dataset.menu, group.open ? 'open' : 'closed'); update(); });
 });
 all.addEventListener('click', () => { const open = !groups.every(g => g.open); groups.forEach(g => { g.open = open; }); update(); });
 update();
 const toggle = document.querySelector('.menu-toggle');
 function sync() { const open = document.body.classList.contains('nav-open'); toggle.setAttribute('aria-expanded', String(open)); document.getElementById('workspace-nav').inert = matchMedia('(max-width:760px)').matches && !open; }
 window.toggleNavigation = () => { document.body.classList.toggle('nav-open'); sync(); };
 addEventListener('resize', sync); sync();
 document.addEventListener('keydown', event => { if (event.key === 'Escape' && document.body.classList.contains('nav-open')) { window.toggleNavigation(); toggle.focus(); } });
})();
