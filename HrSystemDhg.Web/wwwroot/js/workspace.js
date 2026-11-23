(() => {
    document.querySelectorAll('[data-page-workspace]').forEach(workspace => {
        const links = [...workspace.querySelectorAll('[data-workspace-link]')];
        const panels = [...workspace.querySelectorAll('[data-workspace-panel]')];
        const select = id => {
            if (!panels.some(panel => panel.id === id)) return;
            panels.forEach(panel => { panel.hidden = panel.id !== id; });
            links.forEach(link => {
                const active = link.hash === `#${id}`;
                link.classList.toggle('is-active', active);
                if (active) link.setAttribute('aria-current', 'location');
                else link.removeAttribute('aria-current');
            });
        };
        const fromLocation = () => {
            const target = document.getElementById(location.hash.slice(1));
            select(target?.closest('[data-workspace-panel]')?.id || panels[0]?.id);
        };
        links.forEach(link => link.addEventListener('click', event => {
            event.preventDefault();
            history.replaceState(null, '', link.hash);
            select(link.hash.slice(1));
        }));
        // Keep the submitted form visible after server validation or a successful POST.
        const handler = new URLSearchParams(location.search).get('handler')?.toLowerCase();
        const submitted = [...workspace.querySelectorAll('form')].find(form =>
            handler && new URL(form.action).searchParams.get('handler')?.toLowerCase() === handler);
        fromLocation();
        if (!location.hash && submitted) select(submitted.closest('[data-workspace-panel]')?.id);
        window.addEventListener('hashchange', fromLocation);
        workspace.addEventListener('invalid', event => {
            select(event.target.closest('[data-workspace-panel]')?.id);
        }, true);
    });
    // Long module pages get a consistent section index without hiding records or actions.
    const content = document.querySelector('.civic-content');
    if (content && !content.querySelector('[data-page-workspace]') && !document.body.classList.contains('civic-auth')) {
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
