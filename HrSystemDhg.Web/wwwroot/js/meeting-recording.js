window.createMeetingRecorder = (root, grid, send) => {
    const indicator = root.querySelector('#room-recording-indicator');
    const button = root.querySelector('#room-record');
    const upload = root.querySelector('#room-recording-upload');
    const status = root.querySelector('#room-recording-status');
    const fallback = root.querySelector('#room-recording-fallback');
    let active, pending, saving = false, version = -1, unsavedUrl;
    const width = 640, height = 360, framesPerSecond = 12;
    const limit = 92 * 1024 * 1024;

    function cleanup(session) {
        clearInterval(session.tick);
        session.video?.getTracks().forEach(t => t.stop());
        session.context.close().catch(() => {});
    }
    function stop() {
        if (pending) {
            clearTimeout(pending.timeout);
            cleanup(pending);
            pending = null;
            if (button) button.disabled = false;
        }
        if (active?.recorder.state === 'recording') active.recorder.stop();
    }
    async function save(session) {
        send({ type: 'record-stop' });
        cleanup(session);
        active = null;
        button.setAttribute('aria-pressed', 'false');
        button.querySelector('span').textContent = 'Start recording';
        saving = true;
        button.disabled = true;
        const blob = new Blob(session.chunks, { type: session.recorder.mimeType });
        try {
            if (!blob.size) { status.textContent = 'No recording data was captured.'; return; }
            status.textContent = 'Saving recording…';
            const data = new FormData(upload);
            const ext = blob.type.startsWith('video/mp4') ? 'mp4' : 'webm';
            data.append('video', blob, 'recording.' + ext);
            const response = await fetch(upload.action, { method: 'POST', body: data, credentials: 'same-origin', redirect: 'error' });
            if (!response.ok || !(await response.json()).saved) throw new Error();
            status.textContent = 'Recording saved. Open Saved recordings to play it.';
        } catch {
            if (unsavedUrl) URL.revokeObjectURL(unsavedUrl);
            unsavedUrl = URL.createObjectURL(blob);
            fallback.href = unsavedUrl;
            fallback.download = blob.type.startsWith('video/mp4') ? 'meeting.mp4' : 'meeting.webm';
            fallback.hidden = false;
            status.textContent = 'Could not save to Garowe City Management System. Download this recording before closing the room.';
        } finally {
            saving = false;
            button.disabled = false;
        }
    }
    function draw(session) {
        const videos = [...grid.querySelectorAll('video')];
        const wanted = new Set();
        for (const video of videos) {
            for (const track of video.srcObject?.getAudioTracks() ?? []) {
                if (track.readyState !== 'live') continue;
                wanted.add(track);
                if (!session.sources.has(track)) {
                    const source = session.context.createMediaStreamSource(new MediaStream([track]));
                    const gain = session.context.createGain();
                    gain.gain.value = .65;
                    source.connect(gain).connect(session.audio);
                    session.sources.set(track, { source, gain });
                }
            }
        }
        for (const [track, nodes] of session.sources) if (!wanted.has(track)) {
            nodes.source.disconnect();
            nodes.gain.disconnect();
            session.sources.delete(track);
        }
        const ctx = session.canvas.getContext('2d');
        const cols = videos.length > 1 ? (videos.length > 4 ? 3 : 2) : 1;
        const rows = Math.max(1, Math.ceil(videos.length / cols));
        const w = width / cols, h = height / rows;
        ctx.fillStyle = '#10213b';
        ctx.fillRect(0, 0, width, height);
        ctx.font = '13px system-ui';
        if (!videos.length) { ctx.fillStyle = '#fff'; ctx.fillText('Garowe City Management System meeting', 24, 40); }
        videos.forEach((video, i) => {
            const x = (i % cols) * w, y = Math.floor(i / cols) * h;
            if (video.readyState >= 2 && video.videoWidth && video.videoHeight) {
                const scale = Math.min(w / video.videoWidth, h / video.videoHeight);
                const width = video.videoWidth * scale, height = video.videoHeight * scale;
                ctx.drawImage(video, x + (w - width) / 2, y + (h - height) / 2, width, height);
            }
            ctx.fillStyle = '#0b192ecc';
            ctx.fillRect(x, y + h - 28, w, 28);
            ctx.fillStyle = '#fff';
            ctx.fillText(video.parentElement.querySelector('figcaption')?.textContent ?? 'Participant', x + 8, y + h - 8, w - 16);
        });
    }
    function begin(session) {
        draw(session);
        session.video = session.canvas.captureStream(framesPerSecond);
        const media = new MediaStream([...session.video.getVideoTracks(), ...session.audio.stream.getAudioTracks()]);
        const mimeType = ['video/webm;codecs=vp8,opus', 'video/webm', 'video/mp4'].find(type => MediaRecorder.isTypeSupported(type));
        if (!mimeType) throw new Error('Unsupported recording format');
        const recorder = session.recorder = new MediaRecorder(media, { mimeType, videoBitsPerSecond: 320000, audioBitsPerSecond: 48000 });
        session.tick = setInterval(() => draw(session), 1000 / framesPerSecond);
        recorder.ondataavailable = ({ data }) => {
            if (!data.size) return;
            session.chunks.push(data);
            session.bytes += data.size;
            if (session.bytes >= limit) stop();
        };
        recorder.onstop = () => save(session);
        recorder.onerror = () => { status.textContent = 'The browser stopped recording.'; stop(); };
        recorder.start(1000);
        active = session;
        button.disabled = false;
        button.setAttribute('aria-pressed', 'true');
        button.querySelector('span').textContent = 'Stop & save recording';
        status.textContent = 'Recording. Keep this tab open until it is saved.';
    }
    if (button) {
        button.addEventListener('click', async () => {
            if (active) { stop(); return; }
            if (pending || saving) return;
            if (!window.MediaRecorder || !window.AudioContext || !HTMLCanvasElement.prototype.captureStream) {
                status.textContent = 'This browser cannot record meetings. Try a current desktop browser.';
                return;
            }
            button.disabled = true;
            const canvas = document.createElement('canvas');
            canvas.width = width; canvas.height = height;
            const context = new AudioContext();
            const session = { canvas, context, audio: context.createMediaStreamDestination(), sources: new Map(), chunks: [], bytes: 0 };
            pending = session;
            try {
                await context.resume();
                if (pending !== session) return;
                session.timeout = setTimeout(() => {
                    if (pending === session) { stop(); send({ type: 'record-stop' }); status.textContent = 'Recording could not start. Try again.'; }
                }, 6000);
                send({ type: 'record-start' });
            } catch {
                stop();
                status.textContent = 'Recording could not start.';
            }
        });
        window.addEventListener('beforeunload', event => {
            if (active || saving || unsavedUrl) { event.preventDefault(); event.returnValue = ''; }
        });
    }
    return {
        notice(message) {
            if (message.version < version) return;
            version = message.version;
            indicator.hidden = !message.active;
            if (message.active && message.own && pending) {
                const session = pending;
                pending = null;
                clearTimeout(session.timeout);
                try { begin(session); }
                catch {
                    cleanup(session);
                    send({ type: 'record-stop' });
                    button.disabled = false;
                    status.textContent = 'Recording is not supported in this browser.';
                }
            }
        },
        end() {
            stop();
            indicator.hidden = true;
            version = -1;
        }
    };
};
