(() => {
    'use strict';
    const dataEl = document.getElementById('progress-data');
    if (!dataEl || typeof Chart === 'undefined') return;
    const d = JSON.parse(dataEl.textContent);
    const reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    const css = n => getComputedStyle(document.documentElement).getPropertyValue(n).trim();
    const charts = [];

    function theme() {
        Chart.defaults.font.family = "Arial, Helvetica, sans-serif";
        Chart.defaults.font.size = 12;
        Chart.defaults.color = css('--ink-soft');
        Chart.defaults.borderColor = css('--line');
    }

    function build() {
        charts.forEach(c => c.destroy());
        charts.length = 0;
        theme();
        const pool = css('--pool'), pulse = css('--pulse');
        const anim = reduce ? false : { duration: 1100, easing: 'easeOutCubic' };

        const w = document.getElementById('weightChart');
        if (w) {
            const ctx = w.getContext('2d');
            const grad = ctx.createLinearGradient(0, 0, 0, 300);
            grad.addColorStop(0, pool + '55');
            grad.addColorStop(1, pool + '00');
            charts.push(new Chart(w, {
                type: 'line',
                data: {
                    labels: d.weightLabels,
                    datasets: [
                        { label: 'Weight (kg)', data: d.weights, borderColor: pool, backgroundColor: grad, fill: true, tension: .35, pointRadius: 3, yAxisID: 'y' },
                        { label: 'BMI', data: d.bmis, borderColor: pulse, borderDash: [5, 4], tension: .35, pointRadius: 2, yAxisID: 'y1' }
                    ]
                },
                options: {
                    maintainAspectRatio: false, animation: anim, interaction: { mode: 'index', intersect: false },
                    scales: {
                        y: { position: 'left', title: { display: true, text: 'kg' } },
                        y1: { position: 'right', grid: { drawOnChartArea: false }, title: { display: true, text: 'BMI' } }
                    },
                    plugins: { legend: { position: 'bottom' } }
                }
            }));
        }

        const c = document.getElementById('caloriesChart');
        if (c) {
            charts.push(new Chart(c, {
                type: 'bar',
                data: {
                    labels: d.dayLabels,
                    datasets: [
                        { label: 'Burned', data: d.burned, backgroundColor: pool, borderRadius: 4 },
                        { label: 'Eaten', data: d.eaten, backgroundColor: pulse, borderRadius: 4 }
                    ]
                },
                options: {
                    maintainAspectRatio: false, animation: anim,
                    scales: { x: { ticks: { maxTicksLimit: 7 } }, y: { beginAtZero: true } },
                    plugins: { legend: { position: 'bottom' } }
                }
            }));
        }
    }

    build();
    document.addEventListener('ht-theme-changed', build);
})();
