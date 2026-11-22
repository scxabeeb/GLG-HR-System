(() => {
    const list = document.getElementById('today-punches');
    if (!list) return;

    async function refresh() {
        try {
            if (document.hidden) return;
            const response = await fetch(list.dataset.refreshUrl, { cache: 'no-store' });
            if (response.ok && !response.redirected) {
                const rows = await response.text();
                if (rows.trim() !== list.innerHTML.trim()) list.innerHTML = rows;
            }
        } catch {
            // Keep the last visible records during a brief connection loss.
        } finally {
            window.setTimeout(refresh, 15000);
        }
    }

    window.setTimeout(refresh, 15000);
})();
