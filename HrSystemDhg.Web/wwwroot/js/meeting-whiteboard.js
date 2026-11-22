window.createMeetingWhiteboard = (send) => {
    const panel = document.getElementById('room-board');
    const toggle = document.getElementById('room-board-toggle');
    const canvas = document.getElementById('room-board-canvas');
    const status = document.getElementById('room-board-status');
    const context = canvas.getContext('2d');
    let generation = -1;
    let pointer = null;
    let previous;

    function show(open) {
        panel.hidden = !open;
        toggle.setAttribute('aria-expanded', String(open));
    }

    function clear() {
        context.clearRect(0, 0, canvas.width, canvas.height);
        status.textContent = '';
    }

    function draw([x, y, nextX, nextY]) {
        context.strokeStyle = '#143875';
        context.lineWidth = 3;
        context.lineCap = 'round';
        context.beginPath();
        context.moveTo(x * canvas.width / 1000, y * canvas.height / 1000);
        context.lineTo(nextX * canvas.width / 1000, nextY * canvas.height / 1000);
        context.stroke();
    }

    function point(event) {
        const bounds = canvas.getBoundingClientRect();
        const scale = (value, size) => Math.max(0, Math.min(1000, Math.round(1000 * value / size)));
        return [scale(event.clientX - bounds.left, bounds.width), scale(event.clientY - bounds.top, bounds.height)];
    }

    toggle.onclick = () => show(panel.hidden);
    document.getElementById('room-board-clear')?.addEventListener('click', () => send({ type: 'board-clear' }));
    canvas.onpointerdown = event => {
        if (event.button !== 0 || pointer !== null || generation < 0) return;
        pointer = event.pointerId;
        previous = point(event);
        canvas.setPointerCapture(pointer);
    };
    canvas.onpointermove = event => {
        if (event.pointerId !== pointer) return;
        const next = point(event);
        if (Math.hypot(next[0] - previous[0], next[1] - previous[1]) < 3) return;
        send({ type: 'board-line', generation, line: [...previous, ...next] });
        previous = next;
    };
    canvas.onlostpointercapture = () => { pointer = null; };

    return {
        load(board) {
            generation = board.generation;
            clear();
            board.lines.forEach(draw);
        },
        receive(message) {
            if (message.generation < generation) return;
            if (message.generation > generation) {
                generation = message.generation;
                clear();
                pointer = null;
            }
            if (message.type === 'board-line') draw(message.line);
            if (message.type === 'board-full') status.textContent = 'The board is full. The organizer can clear it to continue drawing.';
        },
        reset() {
            generation = -1;
            pointer = null;
            clear();
            show(false);
        }
    };
};
