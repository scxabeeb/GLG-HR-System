(() => {
    const root = document.getElementById('subtask-drafts');
    const add = document.getElementById('add-subtask-draft');
    if (!root || !add || !root.firstElementChild) return;
    const status = document.getElementById('subtask-draft-status');
    const template = root.firstElementChild.cloneNode(true);
    for (const field of template.querySelectorAll('input, textarea, select')) {
        if (field.type === 'checkbox') field.checked = false;
        else if (field.tagName === 'SELECT') field.value = '1';
        else field.value = '';
        field.classList.remove('input-validation-error');
        field.removeAttribute('aria-invalid');
    }
    template.querySelectorAll('[data-valmsg-for]').forEach(message => {
        message.textContent = '';
        message.classList.remove('field-validation-error');
    });
    function renumber() {
        Array.from(root.children).forEach((draft, index) => {
            draft.querySelector('[data-subtask-number]').textContent = 'Subtask ' + (index + 1);
            draft.querySelector('[data-remove-subtask]').hidden = root.children.length === 1;
            for (const field of draft.querySelectorAll('[name], [id], [data-valmsg-for]')) {
                for (const attr of ['name', 'id', 'data-valmsg-for']) {
                    const old = field.getAttribute(attr);
                    if (old) field.setAttribute(attr, old.replace(/Drafts\[\d+\]/g, 'Drafts[' + index + ']')
                        .replace(/Drafts_\d+__/g, 'Drafts_' + index + '__'));
                }
            }
        });
        add.disabled = root.children.length >= 50;
    }
    add.hidden = false;
    add.addEventListener('click', () => {
        if (root.children.length >= 50) return;
        const draft = template.cloneNode(true);
        root.append(draft);
        renumber();
        draft.querySelector('textarea').focus();
        status.textContent = 'Subtask draft added.';
    });
    root.addEventListener('click', event => {
        const remove = event.target.closest('[data-remove-subtask]');
        if (!remove || root.children.length <= 1) return;
        remove.closest('.subtask-draft').remove();
        renumber();
        add.focus();
        status.textContent = 'Subtask draft removed.';
    });
    renumber();
})();
