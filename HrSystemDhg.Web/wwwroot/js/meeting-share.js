document.querySelectorAll('[data-copy-meeting]').forEach(button => {
    button.addEventListener('click', async () => {
        const group = button.closest('details');
        const link = group.querySelector('input');
        const status = group.querySelector('[role=status]');
        try {
            await navigator.clipboard.writeText(link.value);
            status.textContent = 'Link copied.';
        } catch {
            link.focus();
            link.select();
            status.textContent = 'Link selected. Use your device’s copy command.';
        }
    });
});
