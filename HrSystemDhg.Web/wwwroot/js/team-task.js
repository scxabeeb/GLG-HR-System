(() => {
    const team = document.getElementById('task-type-team');
    if (!team) return;
    const department = document.getElementById('Input_DepartmentId');
    const all = document.getElementById('Input_AssignToAll');
    const individuals = [...document.querySelectorAll('input[name="Input.AssigneeIds"]')].slice(1);
    function update() {
        const bulk = all.checked || Boolean(department.value);
        individuals.forEach(input => { input.disabled = bulk; });
        document.querySelectorAll('.task-single-setting').forEach(label => { label.hidden = team.checked; });
        document.getElementById('save-task-button').textContent = team.checked ? 'Create team task' : 'Create individual task';
    }
    department.addEventListener('change', () => {
        if (department.value) all.checked = false;
        update();
    });
    all.addEventListener('change', () => {
        if (all.checked) department.value = '';
        update();
    });
    document.querySelectorAll('input[name="Input.DivideWork"]').forEach(input => input.addEventListener('change', update));
    update();
})();
