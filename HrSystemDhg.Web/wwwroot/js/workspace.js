(() => {
    const controllers = new Map();
    const invalidPanels = [];
    const hashTarget = hash => {
        try { return document.getElementById(decodeURIComponent(hash.replace(/^#/, ''))); }
        catch { return null; }
    };
    const activateTarget = target => {
        if (!target) return;
        const ancestors = [];
        for (let panel = target.closest('[data-workspace-panel]'); panel; panel = panel.parentElement?.closest('[data-workspace-panel]')) ancestors.unshift(panel);
        ancestors.forEach(panel => controllers.get(panel.closest('[data-page-workspace]'))?.(panel.id));
        for (let detail = target.closest('details'); detail; detail = detail.parentElement?.closest('details')) detail.open = true;
        if (target instanceof HTMLDetailsElement) target.open = true;
    };
    document.querySelectorAll('[data-page-workspace]').forEach(workspace => {
        const links = [...workspace.querySelectorAll('[data-workspace-link]')].filter(link => link.closest('[data-page-workspace]') === workspace);
        const panels = [...workspace.querySelectorAll('[data-workspace-panel]')].filter(panel => panel.closest('[data-page-workspace]') === workspace);
        const select = id => {
            if (!panels.some(panel => panel.id === id)) return;
            panels.forEach(panel => {
                panel.hidden = panel.id !== id;
                if (!panel.hidden) panel.querySelectorAll(':scope > details, :scope > details > details').forEach(details => { details.open = true; });
            });
            links.forEach(link => {
                const active = link.hash === `#${id}`;
                link.classList.toggle('is-active', active);
                if (active) link.setAttribute('aria-current', 'location');
                else link.removeAttribute('aria-current');
            });
        };
        controllers.set(workspace, select);
        select(panels[0]?.id);
        const handler = new URLSearchParams(location.search).get('handler')?.toLowerCase();
        const submitted = [...workspace.querySelectorAll('form')].find(form =>
            handler && new URL(form.action).searchParams.get('handler')?.toLowerCase() === handler);
        const editing = workspace.querySelector('[data-workspace-auto] details[open]')?.closest('[data-workspace-panel]');
        if (!location.hash && editing) select(editing.id);
        if (!location.hash && submitted) select(submitted.closest('[data-workspace-panel]')?.id);
        if (submitted && document.querySelector('.validation-summary-errors, .field-validation-error')) invalidPanels.push(submitted.closest('[data-workspace-panel]'));
        workspace.addEventListener('submit', event => {
            if (!(event.target instanceof HTMLFormElement) || event.target.method.toLowerCase() !== 'post') return;
            const panel = event.target.closest('[data-workspace-panel]');
            if (panel) {
                const action = new URL(event.target.action);
                if (!action.hash) action.hash = panel.id;
                event.target.action = action.href;
            }
        });
        workspace.addEventListener('invalid', event => {
            activateTarget(event.target);
        }, true);
    });
    const fromLocation = () => {
        const target = hashTarget(location.hash);
        if (target) activateTarget(target);
    };
    fromLocation();
    invalidPanels.forEach(activateTarget);
    const restoreLocation = () => {
        if (!location.hash) controllers.forEach((select, workspace) => select(workspace.querySelector('[data-workspace-panel]')?.id));
        fromLocation();
    };
    window.addEventListener('hashchange', restoreLocation);
    window.addEventListener('popstate', restoreLocation);
    // Cards, buttons and ordinary links use the same navigation as the section tabs.
    document.addEventListener('click', event => {
        const link = event.target.closest?.('a[href]');
        if (!link || event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey || link.hasAttribute('download') || (link.target && link.target !== '_self')) return;
        const url = new URL(link.href, location.href);
        if (url.origin !== location.origin || url.pathname !== location.pathname || url.search !== location.search || !url.hash) return;
        const target = hashTarget(url.hash);
        if (!target) return;
        event.preventDefault();
        if (location.hash !== url.hash) history.pushState(null, '', url.hash);
        activateTarget(target);
        target.scrollIntoView({ block: 'start' });
        if (!target.hasAttribute('tabindex')) target.setAttribute('tabindex', '-1');
        target.focus({ preventScroll: true });
    });
    // Long module pages get a consistent section index without hiding records or actions.
    const content = document.querySelector('.civic-content');
    if (content && !content.querySelector('[data-page-workspace], [data-no-section-index]') && !document.body.classList.contains('civic-auth')) {
        const headings = [...content.querySelectorAll('h2')].filter(heading =>
            !heading.closest('dialog, details, [hidden]') && heading.textContent.trim());
        if (headings.length >= 3) {
            const index = document.createElement('nav');
            index.className = 'workspace-section-index';
            index.setAttribute('aria-label', 'Page sections');
            const label = document.createElement('strong');
            label.textContent = 'On this page';
            index.append(label);
            headings.forEach((heading, number) => {
                if (!heading.id) heading.id = `workspace-section-${number + 1}`;
                heading.classList.add('workspace-anchor');
                const link = document.createElement('a');
                link.href = `#${heading.id}`;
                link.textContent = heading.textContent.trim();
                index.append(link);
            });
            const title = content.querySelector('h1');
            if (title) title.insertAdjacentElement('afterend', index);
        }
    }
    document.querySelectorAll('[data-open-workspace]').forEach(button => {
        button.addEventListener('click', () => {
            const editor = document.getElementById(button.dataset.openWorkspace);
            if (!(editor instanceof HTMLDetailsElement)) return;
            if (location.hash !== `#${editor.id}`) history.pushState(null, '', `#${editor.id}`);
            activateTarget(editor);
            editor.open = true;
            editor.scrollIntoView({block: 'start', behavior: 'smooth'});
            editor.querySelector('input:not([type="hidden"]), select, textarea')?.focus({preventScroll: true});
        });
    });
    // Provide the same collapsible, readable maintenance tools throughout the workspace.
    document.querySelectorAll('details').forEach(details => {
        if (details.querySelector('.validation-summary-errors, .field-validation-error')) details.open = true;
        details.addEventListener('invalid', () => { details.open = true; }, true);
    });
})();
