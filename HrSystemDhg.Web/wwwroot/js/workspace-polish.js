(() => {
    // Explain horizontal scrolling only when the table actually needs it.
    document.querySelectorAll('.civic-content .table-scroll').forEach(viewport => {
        const table = viewport.querySelector('table');
        if (!table) return;
        const hint = document.createElement('p');
        hint.className = 'table-scroll-hint no-print';
        hint.textContent = '↔ Scroll sideways to view all columns';
        hint.hidden = true;
        viewport.before(hint);
        const update = () => {
            const overflow = viewport.clientWidth > 0 && viewport.scrollWidth > viewport.clientWidth + 2;
            hint.hidden = !overflow;
            if (overflow) {
                viewport.setAttribute('tabindex', '0');
                if (!viewport.hasAttribute('aria-label')) viewport.setAttribute('aria-label', 'Scrollable records');
            } else viewport.removeAttribute('tabindex');
        };
        if (window.ResizeObserver) {
            const observer = new ResizeObserver(update);
            observer.observe(viewport); observer.observe(table);
        }
        addEventListener('resize', update); update();
    });
    const password = document.querySelector('.auth-card input[autocomplete="current-password"]');
    const caps = document.querySelector('.caps-lock-hint');
    if (password && caps) {
        const updateCaps = event => { caps.hidden = !event.getModifierState?.('CapsLock'); };
        password.addEventListener('keyup', updateCaps);
        password.addEventListener('keydown', updateCaps);
        password.addEventListener('blur', () => { caps.hidden = true; });
    }
    const form = document.querySelector('.auth-card form');
    const submit = form?.querySelector('.auth-submit');
    if (submit) {
        const original = submit.innerHTML;
        form.addEventListener('submit', () => { submit.disabled = true; submit.textContent = 'Signing in…'; });
        addEventListener('pageshow', () => { submit.disabled = false; submit.innerHTML = original; });
    }
    // Keep mobile keyboard navigation within the open drawer.
    const sidebar = document.getElementById('workspace-nav');
    const toggle = document.querySelector('.menu-toggle');
    if (!sidebar || !toggle) return;
    const drawerOpen = () => matchMedia('(max-width:760px)').matches && document.body.classList.contains('nav-open');
    document.addEventListener('click', event => {
        if (event.target.closest('.menu-toggle') && drawerOpen()) sidebar.querySelector('#nav-search')?.focus();
        if (event.target.closest('.nav-scrim') && !drawerOpen()) toggle.focus();
    });
    document.addEventListener('keydown', event => {
        if (event.key !== 'Tab' || !drawerOpen()) return;
        const focusable = [...sidebar.querySelectorAll('a[href],button,input,summary,[tabindex="0"]')]
            .filter(element => !element.disabled && element.getClientRects().length);
        const first = focusable[0], last = focusable.at(-1);
        if (!first) return;
        if (event.shiftKey && (document.activeElement === first || !sidebar.contains(document.activeElement))) {
            event.preventDefault(); last.focus();
        } else if (!event.shiftKey && (document.activeElement === last || !sidebar.contains(document.activeElement))) {
            event.preventDefault(); first.focus();
        }
    });
})();
