(() => {
    const field = document.getElementById('InitialPassword');
    const generate = document.getElementById('generate-employee-password');
    const copy = document.getElementById('copy-employee-password');
    const status = document.getElementById('employee-password-status');
    if (!field || !globalThis.crypto?.getRandomValues) return;

    generate.hidden = false;
    copy.hidden = !navigator.clipboard;
    const randomCharacter = alphabet => {
        const sample = new Uint32Array(1);
        const limit = 2 ** 32 - (2 ** 32 % alphabet.length);
        do { crypto.getRandomValues(sample); } while (sample[0] >= limit);
        return alphabet[sample[0] % alphabet.length];
    };
    const groups = ['ABCDEFGHJKLMNPQRSTUVWXYZ', 'abcdefghijkmnopqrstuvwxyz', '23456789', '!@#%+='];
    generate.addEventListener('click', () => {
        field.value = groups.map(randomCharacter).join('') +
            Array.from({ length: 16 }, () => randomCharacter(groups.join(''))).join('');
        status.textContent = 'Temporary password generated.';
    });
    copy.addEventListener('click', async () => {
        if (!field.value) {
            status.textContent = 'Enter or generate a temporary password first.';
            return;
        }
        try {
            await navigator.clipboard.writeText(field.value);
            status.textContent = 'Temporary password copied.';
        } catch {
            status.textContent = 'Use Show to reveal the temporary password and copy it.';
        }
    });
})();
