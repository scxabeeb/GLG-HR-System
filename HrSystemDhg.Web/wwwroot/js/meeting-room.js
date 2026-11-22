(() => {
    const root = document.getElementById('meeting-room');
    if (!root) return;
    const form = document.getElementById('join-room');
    const controls = document.getElementById('room-controls');
    const status = document.getElementById('room-status');
    const stage = document.getElementById('room-stage');
    const endedPanel = document.getElementById('room-ended');
    const endAll = document.getElementById('room-end-all');
    const count = document.getElementById('room-count');
    const lobbyTitle = document.getElementById('room-lobby-title');
    const grid = document.getElementById('room-video');
    const mic = document.getElementById('room-mute');
    const camera = document.getElementById('room-camera');
    const share = document.getElementById('room-share-screen');
    const submit = form.querySelector('button');
    const peers = new Map();
    let socket, heartbeat, stream, screen, preview, joining = false;
    function updateCount() {
        count.textContent = String(grid.childElementCount);
    }

    async function toggleFullscreen(figure, video) {
        if (document.fullscreenElement === figure) {
            await document.exitFullscreen();
            return;
        }
        if (figure.classList.contains('room-video-expanded')) {
            figure.classList.remove('room-video-expanded');
            return;
        }
        try {
            if (figure.requestFullscreen) {
                await figure.requestFullscreen();
                return;
            }
            if (video.webkitSupportsFullscreen && video.webkitEnterFullscreen) {
                video.webkitEnterFullscreen();
                return;
            }
        } catch {
            // Embedded browsers may block native fullscreen.
        }
        if (figure.isConnected) figure.classList.add('room-video-expanded');
    }

    document.addEventListener('keydown', event => {
        if (event.key === 'Escape') grid.querySelector('.room-video-expanded')?.classList.remove('room-video-expanded');
    });

    function tile(name, local = false) {
        const figure = document.createElement('figure');
        const video = document.createElement('video');
        video.autoplay = video.playsInline = true;
        video.muted = local;
        figure.addEventListener('click', () => toggleFullscreen(figure, video));
        const avatar = document.createElement('span');
        avatar.className = 'room-avatar';
        avatar.setAttribute('aria-hidden', 'true');
        avatar.textContent = name.trim().slice(0, 1).toUpperCase();
        const label = document.createElement('figcaption');
        label.textContent = name;
        const fullscreen = document.createElement('button');
        fullscreen.type = 'button';
        fullscreen.className = 'room-video-fullscreen';
        const expand = document.createElement('span');
        expand.textContent = 'Fullscreen';
        const collapse = document.createElement('span');
        collapse.textContent = 'Exit fullscreen';
        fullscreen.append(expand, collapse);
        const icon = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
        const use = document.createElementNS(icon.namespaceURI, 'use');
        icon.setAttribute('aria-hidden', 'true');
        use.setAttribute('href', root.dataset.icons + '#fullscreen');
        icon.append(use);
        fullscreen.prepend(icon);
        fullscreen.onclick = event => {
            event.stopPropagation();
            toggleFullscreen(figure, video);
        };
        figure.append(avatar, video, label, fullscreen);
        grid.append(figure);
        updateCount();
        return { figure, video };
    }
    function send(message) {
        if (socket?.readyState === WebSocket.OPEN) socket.send(JSON.stringify(message));
    }
    const board = window.createMeetingWhiteboard(send);
    const recorder = window.createMeetingRecorder?.(root, grid, send);
    function remove(id) {
        const peer = peers.get(id);
        if (!peer) return;
        peer.pc.close();
        peer.figure.remove();
        peers.delete(id);
        updateCount();
    }
    function end(message, closed = false) {
        const wasJoined = !stage.hidden;
        recorder?.end();
        const current = socket;
        socket = null;
        if (current && current.readyState < WebSocket.CLOSING) current.close();
        clearInterval(heartbeat);
        [...peers.keys()].forEach(remove);
        stream?.getTracks().forEach(t => t.stop());
        stream = null;
        screen?.stop();
        screen = null;
        preview = null;
        board.reset();
        updateShareButton();
        grid.replaceChildren();
        controls.hidden = true;
        stage.hidden = true;
        form.hidden = closed;
        endedPanel.hidden = !closed;
        if (endAll) endAll.disabled = false;
        if (wasJoined && !closed) {
            lobbyTitle.textContent = 'Ready to rejoin?';
            submit.textContent = 'Rejoin meeting';
        }
        submit.disabled = joining = false;
        if (message) {
            status.textContent = message;
            if (closed) endedPanel.focus();
            else if (wasJoined) submit.focus();
        }
    }
    function peerFor(id, name) {
        if (peers.has(id)) return peers.get(id);
        const pc = new RTCPeerConnection({ iceServers: JSON.parse(root.dataset.ice) });
        const { figure, video } = tile(name);
        const incoming = new MediaStream();
        video.srcObject = incoming;
        const state = { pc, figure, candidates: [] };
        peers.set(id, state);
        pc.ontrack = event => { incoming.addTrack(event.track); video.play().catch(() => {}); };
        pc.onicecandidate = ({ candidate }) => { if (candidate) send({ type: 'ice', to: id, payload: candidate.toJSON() }); };
        pc.onconnectionstatechange = () => {
            if (pc.connectionState === 'failed') status.textContent = 'A participant’s media could not connect. Check the network or TURN relay.';
        };
        return state;
    }
    function outgoing(kind) {
        return (kind === 'video' && screen) || stream?.getTracks().find(track => track.kind === kind) || null;
    }

    function updateShareButton() {
        share.disabled = !navigator.mediaDevices?.getDisplayMedia;
        share.querySelector('span').textContent = screen ? 'Stop sharing' : 'Share screen';
        share.setAttribute('aria-pressed', String(!!screen));
        if (preview) {
            const tracks = [outgoing('video'), outgoing('audio')].filter(Boolean);
            preview.srcObject = new MediaStream(tracks);
        }
    }

    async function updateSharedVideo() {
        await Promise.all([...peers.values()].map(async ({ pc }) => {
            const sender = pc.getTransceivers().find(t => t.receiver.track.kind === 'video')?.sender;
            if (sender && pc.signalingState !== 'closed') await sender.replaceTrack(outgoing('video'));
        }));
        updateShareButton();
    }

    async function stopSharing(track) {
        if (!track || screen !== track) return;
        screen = null;
        track.stop();
        try { await updateSharedVideo(); }
        catch { status.textContent = 'Could not restore camera video for every participant.'; }
        updateShareButton();
    }

    share.onclick = async () => {
        if (screen) { await stopSharing(screen); return; }
        const current = socket;
        share.disabled = true;
        try {
            const capture = await navigator.mediaDevices.getDisplayMedia({ video: true, audio: false });
            const track = capture.getVideoTracks()[0];
            if (socket !== current || !track || track.readyState !== 'live') {
                capture.getTracks().forEach(item => item.stop());
                return;
            }
            screen = track;
            track.addEventListener('ended', () => stopSharing(track), { once: true });
            await updateSharedVideo();
            if (screen === track) status.textContent = 'You are sharing your screen.';
        } catch {
            if (socket === current) {
                await stopSharing(screen);
                status.textContent = 'Screen sharing could not start. Choose a screen or window and try again.';
            }
        } finally {
            if (socket === current) updateShareButton();
        }
    };

    async function receive(message) {
        if (message.type === 'ended') {
            end('', true);
            status.textContent = '';
            endedPanel.focus();
            return;
        }
        if (message.type === 'error') { end(message.message); return; }
        if (message.type === 'recording') { recorder?.notice(message); return; }
        if (message.type.startsWith('board-')) { board.receive(message); return; }
        if (message.type === 'mute-all') {
            const track = outgoing('audio');
            if (track) {
                track.enabled = false;
                mic.setAttribute('aria-pressed', 'true');
                mic.querySelector('span').textContent = 'Unmute microphone';
                status.textContent = 'The organizer muted your microphone. You can unmute when ready.';
            }
            return;
        }
        if (message.type === 'joined') { peerFor(message.from, message.name); return; }
        if (message.type === 'left') { remove(message.from); return; }
        if (message.type === 'welcome') {
            board.load(message.board);
            recorder?.notice({ active: message.recording, version: message.version });
            form.hidden = true;
            controls.hidden = false;
            stage.hidden = false;
            status.textContent = 'You joined the meeting.';
            for (const participant of message.peers) {
                if (!socket) return;
                const { pc } = peerFor(participant.id, participant.name);
                for (const kind of ['audio', 'video']) {
                    pc.addTransceiver(outgoing(kind) || kind, { direction: 'sendrecv' });
                }
                await pc.setLocalDescription(await pc.createOffer());
                send({ type: 'offer', to: participant.id, payload: pc.localDescription });
            }
            return;
        }
        if (!['offer', 'answer', 'ice'].includes(message.type)) return;
        const peer = peerFor(message.from, message.name);
        if (message.type === 'ice') {
            if (!peer.pc.remoteDescription) { if (peer.candidates.length < 128) peer.candidates.push(message.payload); }
            else await peer.pc.addIceCandidate(message.payload);
            return;
        }
        await peer.pc.setRemoteDescription(message.payload);
        for (const candidate of peer.candidates.splice(0)) await peer.pc.addIceCandidate(candidate);
        if (message.type === 'offer') {
            for (const transceiver of peer.pc.getTransceivers()) {
                transceiver.direction = 'sendrecv';
                await transceiver.sender.replaceTrack(outgoing(transceiver.receiver.track.kind));
            }
            await peer.pc.setLocalDescription(await peer.pc.createAnswer());
            send({ type: 'answer', to: message.from, payload: peer.pc.localDescription });
        }
    }
    function toggleTrack(kind, button, on, off) {
        const track = stream?.getTracks().find(t => t.kind === kind);
        if (!track) return;
        track.enabled = !track.enabled;
        button.setAttribute('aria-pressed', String(!track.enabled));
        button.querySelector('span').textContent = track.enabled ? on : off;
    }
    mic.onclick = () => toggleTrack('audio', mic, 'Mute microphone', 'Unmute microphone');
    camera.onclick = () => toggleTrack('video', camera, 'Turn camera off', 'Turn camera on');
    document.getElementById('room-mute-all')?.addEventListener('click', () => {
        send({ type: 'mute-all' });
    });
    document.getElementById('room-leave').onclick = () => end('You left. Rejoin whenever you’re ready while the meeting is open.');
    endAll?.addEventListener('click', () => {
        if (!window.confirm('End this meeting for everyone? Participants will be disconnected.')) return;
        endAll.disabled = true;
        send({ type: 'end-meeting' });
        const current = socket;
        setTimeout(() => {
            if (socket === current && current) endAll.disabled = false;
        }, 5000);
    });
    window.addEventListener('pagehide', () => end());
    form.addEventListener('submit', async event => {
        event.preventDefault();
        if (joining || socket) return;
        if (!window.isSecureContext || !window.RTCPeerConnection) {
            status.textContent = 'Use a supported browser over HTTPS or on localhost.';
            return;
        }
        const name = document.getElementById('room-name').value.trim();
        if (!name) { status.textContent = 'Enter a display name.'; return; }
        joining = submit.disabled = true;
        status.textContent = 'Connecting…';
        try {
            if (document.getElementById('room-media').checked) {
                const media = await navigator.mediaDevices.getUserMedia({
                    audio: true,
                    video: { width: { ideal: 1280 }, height: { ideal: 720 }, frameRate: { ideal: 24 } }
                });
                if (!joining) { media.getTracks().forEach(track => track.stop()); return; }
                const cameraTrack = media.getVideoTracks()[0];
                if (cameraTrack && 'contentHint' in cameraTrack) cameraTrack.contentHint = 'detail';
                stream = media;
            }
        } catch {
            end('Camera or microphone is unavailable. Uncheck camera and microphone to join without them.');
            return;
        }
        try {
            preview = tile('You', true).video;
            updateShareButton();
            mic.disabled = !stream?.getAudioTracks().length;
            camera.disabled = !stream?.getVideoTracks().length;
            mic.querySelector('span').textContent = 'Mute microphone';
            camera.querySelector('span').textContent = 'Turn camera off';
            mic.setAttribute('aria-pressed', 'false');
            camera.setAttribute('aria-pressed', 'false');
            const url = new URL(root.dataset.signal, location.href);
            url.protocol = location.protocol === 'https:' ? 'wss:' : 'ws:';
            const current = socket = new WebSocket(url);
            let incoming = Promise.resolve();
            current.onopen = () => {
                if (socket !== current) return;
                send({ ticket: root.dataset.ticket, name });
                heartbeat = setInterval(() => send({ type: 'ping' }), 7000);
            };
            current.onmessage = event => {
                incoming = incoming.then(() => socket === current && receive(JSON.parse(event.data))).catch(() => {
                    if (socket === current) status.textContent = 'Could not connect a participant’s media.';
                });
            };
            current.onclose = () => { if (socket === current) end('The room disconnected. You can try joining again.'); };
        } catch {
            end('Could not connect to the room.');
        }
    });
})();
