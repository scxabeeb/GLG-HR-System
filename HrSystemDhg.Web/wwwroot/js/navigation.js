(() => {
    const sidebar = document.getElementById('workspace-nav');
    const groups = [...sidebar.querySelectorAll('details[data-menu]')];
    const expand = document.getElementById('expand-menus');
    const normalizePath = pathname => pathname.replace(/\/$/, '').replace(/\/(index)$/i, '').toLowerCase() || '/';
    const path = normalizePath(location.pathname);
    const params = new URLSearchParams(location.search);
    const defaults = {
        '/operations/tasks': { view: 'mine' },
        '/reports': { report: 'Employees' }
    };
    const valueFor = key => [...params].find(([name]) => name.toLowerCase() === key.toLowerCase())?.[1]
        ?? defaults[path]?.[key];
    const routes = (link, key) => (link.dataset[key] || '').split(' ').filter(Boolean).map(normalizePath);
    const matchesPrefix = prefix => path === prefix || path.startsWith(prefix + '/');

    function selectedLink(links) {
        const exact = links.filter(link => normalizePath(link.pathname) === path);
        return exact.find(link => link.search && [...new URLSearchParams(link.search)]
            .every(([key, value]) => valueFor(key)?.toLowerCase() === value.toLowerCase()))
            ?? exact.find(link => !link.search)
            ?? links.find(link => routes(link, 'routePrefix').some(matchesPrefix)
                && !routes(link, 'routeExclude').some(matchesPrefix));
    }

    function syncExpandButton() {
        const expanded = groups.every(group => group.open);
        expand.textContent = expanded ? 'Collapse all' : 'Expand all';
        expand.setAttribute('aria-label', expanded ? 'Collapse all menu groups' : 'Expand all menu groups');
    }

    groups.forEach(group => {
        const key = 'dhg-menu-' + group.dataset.menu;
        try { group.open = localStorage.getItem(key) === 'open'; } catch {}
        const current = selectedLink([...group.querySelectorAll('a')]
            .filter(link => link.closest('details[data-menu]') === group));
        if (current) {
            current.setAttribute('aria-current', normalizePath(current.pathname) === path ? 'page' : 'location');
            for (let menu = group; menu; menu = menu.parentElement.closest('details[data-menu]')) {
                menu.open = true;
            }
        }
        group.addEventListener('toggle', () => {
            try { localStorage.setItem(key, group.open ? 'open' : 'closed'); } catch {}
            syncExpandButton();
        });
    });
    if (expand && groups.length) {
        expand.addEventListener('click', () => {
            const open = !groups.every(group => group.open);
            groups.forEach(group => { group.open = open; });
            syncExpandButton();
        });
        syncExpandButton();
    }
    const hub = selectedLink([...sidebar.querySelectorAll('.nav-hubs a')]);
    if (hub) hub.setAttribute('aria-current', normalizePath(hub.pathname) === path ? 'page' : 'location');

    const toggle = document.querySelector('.menu-toggle');
    function syncMenu() {
        const open = document.body.classList.contains('nav-open');
        toggle.setAttribute('aria-expanded', String(open));
        const mobile = matchMedia('(max-width:760px)').matches;
        sidebar.inert = mobile && !open;
        const main = document.getElementById('main-content');
        if (main) main.inert = mobile && open;
    }
    window.toggleNavigation = () => {
        document.body.classList.toggle('nav-open');
        syncMenu();
    };
    addEventListener('resize', syncMenu);
    syncMenu();
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape' && document.body.classList.contains('nav-open')) {
            window.toggleNavigation();
            toggle.focus();
        }
    });

    document.querySelectorAll('.civic-content table').forEach(table => {
        if (table.closest('.table-scroll')) return;
        const viewport = document.createElement('div');
        viewport.className = 'table-scroll';
        table.before(viewport);
        viewport.append(table);
    });
})();

// Search the existing accessible module groups without changing routes or permissions.
(() => {
    const input = document.getElementById('nav-search');
    const sidebar = document.getElementById('workspace-nav');
    if (!input || !sidebar) return;
    const roots = [...sidebar.querySelectorAll('nav > details.nav-group')];
    const hubs = [...sidebar.querySelectorAll('.nav-hubs a')];
    const message = document.getElementById('nav-no-results');
    let previous;
    input.addEventListener('input', () => {
        const query = input.value.trim().toLowerCase();
        if (query && !previous) previous = new Map([...sidebar.querySelectorAll('details[data-menu]')].map(group => [group, group.open]));
        [...roots, ...hubs].forEach(item => { item.hidden = !!query && !item.textContent.toLowerCase().includes(query); });
        if (query) roots.filter(item => !item.hidden).forEach(item => { item.open = true; item.querySelectorAll('details[data-menu]').forEach(child => { if (child.textContent.toLowerCase().includes(query)) child.open = true; }); });
        else if (previous) { previous.forEach((open, group) => { group.open = open; }); previous = undefined; }
        if (message) message.hidden = !query || [...roots, ...hubs].some(item => !item.hidden);
    });
})();
