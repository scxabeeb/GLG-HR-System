document.addEventListener('click', event => {
    const button = event.target.closest('[data-payroll-details]');
    if (!button) return;
    document.getElementById(button.dataset.payrollDetails)?.showModal();
});
