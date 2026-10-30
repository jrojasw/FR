// Gráfico del historial de precios por tienda (Chart.js, eje X lineal en milisegundos).
window.PriceHistory = (function () {
    const palette = ['#198754', '#0d6efd', '#dc3545', '#fd7e14', '#6f42c1', '#20c997', '#6c757d', '#d63384'];
    const clp = new Intl.NumberFormat('es-CL', { style: 'currency', currency: 'CLP', maximumFractionDigits: 0 });
    const dateFormat = new Intl.DateTimeFormat('es-CL', { day: '2-digit', month: 'short', year: 'numeric' });
    const dateTimeFormat = new Intl.DateTimeFormat('es-CL', { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' });
    const twoDays = 2 * 24 * 60 * 60 * 1000;
    let chart = null;

    function toDatasets(series) {
        return series.map((s, i) => ({
            label: s.storeName,
            // Sin stock se dibuja como hueco en la línea.
            data: s.points.map(p => ({ x: Date.parse(p.date), y: p.isAvailable ? p.price : null })),
            borderColor: palette[i % palette.length],
            backgroundColor: palette[i % palette.length],
            stepped: 'before',
            spanGaps: false,
            pointRadius: 2,
            borderWidth: 2,
        }));
    }

    async function load(options, days) {
        const response = await fetch(`${options.url}?dias=${days}`, { headers: { Accept: 'application/json' } });
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        const series = await response.json();
        const hasData = series.some(s => s.points.length > 0);
        options.empty.classList.toggle('d-none', hasData);
        options.canvas.parentElement.classList.toggle('d-none', !hasData);
        if (!hasData) return;

        const datasets = toDatasets(series);
        const xs = datasets.flatMap(d => d.data.map(p => p.x));
        // Con pocos datos (recién empezamos a seguir el producto) se muestra también la hora.
        const formatter = Math.max(...xs) - Math.min(...xs) < twoDays ? dateTimeFormat : dateFormat;
        if (chart) {
            chart.data.datasets = datasets;
            chart.options.scales.x.ticks.callback = v => formatter.format(new Date(v));
            chart.update();
            return;
        }
        chart = new Chart(options.canvas, {
            type: 'line',
            data: { datasets },
            options: {
                maintainAspectRatio: false,
                interaction: { mode: 'nearest', intersect: false },
                scales: {
                    x: { type: 'linear', ticks: { callback: v => formatter.format(new Date(v)), maxTicksLimit: 8 } },
                    y: { ticks: { callback: v => clp.format(v) } },
                },
                plugins: {
                    tooltip: {
                        callbacks: {
                            title: items => items.length ? dateTimeFormat.format(new Date(items[0].parsed.x)) : '',
                            label: item => `${item.dataset.label}: ${clp.format(item.parsed.y)}`,
                        },
                    },
                    legend: { position: 'bottom' },
                },
            },
        });
    }

    function init(options) {
        const buttons = options.range.querySelectorAll('button[data-days]');
        buttons.forEach(button => button.addEventListener('click', () => {
            buttons.forEach(b => b.classList.toggle('active', b === button));
            load(options, button.dataset.days).catch(console.error);
        }));
        const active = options.range.querySelector('button.active');
        load(options, active ? active.dataset.days : 90).catch(console.error);
    }

    return { init };
})();
