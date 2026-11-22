(() => {
    const inbox = document.querySelector('[data-unread-url]');
    if (!inbox) return;
    const badge = inbox.querySelector('.badge');

    async function refresh() {
        if (document.hidden) return;
        try {
            const response = await fetch(inbox.dataset.unreadUrl, { headers: { Accept: 'application/json' } });
            if (!response.ok || response.redirected) return;
            const { count } = await response.json();
            if (!Number.isInteger(count) || badge.textContent === String(count)) return;
            badge.textContent = String(count);
            badge.setAttribute('aria-label', `${count} unread notifications`);
            badge.hidden = count === 0;
        } catch {
            // Keep the last known count while the connection is unavailable.
        }
    }

    setInterval(refresh, 30000);
    document.addEventListener('visibilitychange', refresh);
})();
