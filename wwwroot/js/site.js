// HealthTrack client-side helpers

// Shared palette for Chart.js charts
const HT = {
    teal: '#0f766e', tealFill: 'rgba(15,118,110,.12)',
    orange: '#f97316', orangeFill: 'rgba(249,115,22,.15)',
    blue: '#2563eb', purple: '#7c3aed', grey: '#94a3b8'
};

if (window.Chart) {
    Chart.defaults.font.family = '"Segoe UI", system-ui, sans-serif';
    Chart.defaults.color = '#6b7280';
    Chart.defaults.plugins.legend.labels.usePointStyle = true;
    Chart.defaults.maintainAspectRatio = false;
}

/**
 * Draws a chart on the canvas with the given id.
 * @param {string} id      canvas element id
 * @param {string} type    'line' | 'bar' | 'doughnut'
 * @param {string[]} labels x-axis labels
 * @param {object[]} datasets Chart.js datasets
 * @param {object} extra   extra Chart.js options (merged)
 */
function htChart(id, type, labels, datasets, extra) {
    const el = document.getElementById(id);
    if (!el || !window.Chart) return null;
    const options = Object.assign({
        interaction: { mode: 'index', intersect: false },
        scales: type === 'doughnut' ? {} : {
            x: { grid: { display: false } },
            y: { beginAtZero: type === 'bar', grid: { color: '#eef2f1' } }
        }
    }, extra || {});
    return new Chart(el, { type: type, data: { labels: labels, datasets: datasets }, options: options });
}

// Refresh the notification badge every 60 seconds.
(function pollNotifications() {
    const badge = document.getElementById('bell-count');
    if (!badge) return;
    setInterval(async () => {
        try {
            const r = await fetch('/Notifications/UnreadCount', { headers: { 'Accept': 'application/json' } });
            if (!r.ok) return;
            const data = await r.json();
            badge.textContent = data.count;
            badge.classList.toggle('d-none', data.count === 0);
        } catch { /* offline - ignore */ }
    }, 60000);
})();

// Dynamic rows for the workout-plan and diet-plan editors.
// A <template> holds one row with "__i__" as the index placeholder.
document.addEventListener('click', function (e) {
    const add = e.target.closest('[data-add-row]');
    if (add) {
        const body = document.getElementById(add.dataset.addRow);
        const tpl = document.getElementById(add.dataset.template);
        const index = body.querySelectorAll('tr').length;
        body.insertAdjacentHTML('beforeend', tpl.innerHTML.replace(/__i__/g, index));
    }
    const remove = e.target.closest('[data-remove-row]');
    if (remove) {
        const tbody = remove.closest('tbody');
        remove.closest('tr').remove();
        // Re-number so model binding receives a continuous index (0,1,2...)
        tbody.querySelectorAll('tr').forEach((tr, i) => {
            tr.querySelectorAll('[name]').forEach(input => {
                input.name = input.name.replace(/\[\d+\]/, '[' + i + ']');
            });
        });
    }
});

// Confirmation for destructive buttons: <button data-confirm="Are you sure?">
document.addEventListener('submit', function (e) {
    const btn = e.submitter;
    if (btn && btn.dataset.confirm && !window.confirm(btn.dataset.confirm)) e.preventDefault();
});
