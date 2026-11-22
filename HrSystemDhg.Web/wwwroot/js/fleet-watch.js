(() => {
    const panel = document.querySelector('#fleet-watch');
    if (!panel) return;
    const button = panel.querySelector('button');
    const status = panel.querySelector('[role=status]');
    const map = panel.querySelector('a');
    map.style.display = 'none';
    let watching = false, timer, request;
    async function readPosition() {
        if (!watching) return;
        const current = new AbortController();
        request = current;
        try {
            const response = await fetch(panel.dataset.url, { cache: 'no-store', signal: current.signal, redirect: 'error' });
            if (!response.ok) throw new Error('unavailable');
            const { position } = await response.json();
            if (!watching || request !== current) return;
            if (!position) { status.textContent = 'No tracker fixes yet.'; return; }
            const lat = Number(position.latitude).toFixed(6);
            const lon = Number(position.longitude).toFixed(6);
            status.textContent = `${lat}, ${lon} · ${position.localTime}${position.stale ? ' · Older than 15 minutes' : ''}`;
            map.href = `https://www.openstreetmap.org/?mlat=${lat}&mlon=${lon}#map=16/${lat}/${lon}`;
            map.hidden = false;
            map.style.display = '';
        } catch (error) {
            if (watching && request === current && error.name !== 'AbortError') status.textContent = 'Could not refresh the latest position.';
        } finally {
            if (watching && request === current) timer = setTimeout(readPosition, 15000);
        }
    }
    button.addEventListener('click', () => {
        watching = !watching;
        button.setAttribute('aria-pressed', String(watching));
        button.textContent = watching ? 'Stop watching' : 'Watch latest position';
        if (watching) { status.textContent = 'Checking for a position…'; readPosition(); }
        else { clearTimeout(timer); request?.abort(); status.textContent = 'Position watch stopped.'; }
    });
})();
