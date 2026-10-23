(() => {
  document.querySelectorAll('input[type="password"]').forEach((input, index) => {
    if (input.dataset.visibilityReady) return;
    input.dataset.visibilityReady = 'true';
    if (!input.id) input.id = `password-field-${index}`;
    const label = input.closest('label');
    const labelText = label ? Array.from(label.childNodes).filter(n => n.nodeType === Node.TEXT_NODE).map(n => n.textContent).join(' ').trim() : document.querySelector(`label[for="${input.id}"]`)?.textContent.trim();
    if (labelText && label) input.setAttribute('aria-label', labelText);
    const wrap = document.createElement('span'); wrap.className = 'password-control';
    input.parentNode.insertBefore(wrap, input); wrap.appendChild(input);
    const button = document.createElement('button'); button.type = 'button'; button.className = 'password-toggle';
    button.textContent = 'Show'; button.setAttribute('aria-controls', input.id); button.setAttribute('aria-pressed', 'false');
    const updateLabel = shown => button.setAttribute('aria-label', `${shown ? 'Hide' : 'Show'} ${(labelText || 'password').toLowerCase()}`);
    updateLabel(false);
    button.addEventListener('click', event => {event.preventDefault();const shown = input.type === 'password';input.type = shown ? 'text' : 'password';button.textContent = shown ? 'Hide' : 'Show';button.setAttribute('aria-pressed', String(shown));updateLabel(shown);});
    wrap.appendChild(button);
  });
})();
