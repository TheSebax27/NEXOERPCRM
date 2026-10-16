window.nexoCharts = (function () {
    var instances = {};

    function fmt(v) {
        var n = Math.abs(v);
        if (n >= 1e6) return (v / 1e6).toFixed(1).replace(/\.0$/, '') + 'M';
        if (n >= 1e3) return Math.round(v / 1e3) + 'K';
        return Math.round(v).toLocaleString('es-CO');
    }

    var palette = ['#7C5CFF', '#14B8A6', '#F59E0B', '#0EA5E9'];

    function render(id, type, labels, series, maxXTicks) {
        var existing = instances[id];
        if (existing) { existing.destroy(); delete instances[id]; }
        var el = document.getElementById(id);
        if (!el) return;

        var gridColor = 'rgba(128,128,128,0.1)';
        var tickColor = '#9ca3af';

        var datasets = series.map(function (s, i) {
            var color = palette[i % palette.length];
            return {
                label: s.name,
                data: s.data,
                borderColor: color,
                backgroundColor: type === 'bar' ? color + 'BB' : color + '18',
                fill: type === 'line',
                tension: 0.35,
                pointRadius: type === 'line' ? 3 : 0,
                pointHoverRadius: 6,
                borderWidth: 2
            };
        });

        instances[id] = new Chart(el, {
            type: type,
            data: { labels: labels, datasets: datasets },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: { mode: 'index', intersect: false },
                animation: { duration: 300 },
                plugins: {
                    legend: {
                        display: true,
                        position: 'bottom',
                        labels: { boxWidth: 10, padding: 20, color: tickColor, font: { size: 12 } }
                    },
                    tooltip: {
                        backgroundColor: 'rgba(15,17,26,0.95)',
                        titleColor: '#e2e8f0',
                        bodyColor: '#cbd5e1',
                        borderColor: 'rgba(255,255,255,0.1)',
                        borderWidth: 1,
                        padding: 12,
                        callbacks: {
                            label: function (ctx) {
                                return ' ' + ctx.dataset.label + ': $' + fmt(ctx.raw);
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        ticks: {
                            color: tickColor,
                            maxTicksLimit: maxXTicks || 8,
                            maxRotation: 0,
                            autoSkip: true,
                            font: { size: 11 }
                        },
                        grid: { color: gridColor }
                    },
                    y: {
                        ticks: {
                            color: tickColor,
                            callback: function (v) { return fmt(v); },
                            maxTicksLimit: 6,
                            font: { size: 11 }
                        },
                        grid: { color: gridColor }
                    }
                }
            }
        });
    }

    function destroy(id) {
        var c = instances[id];
        if (c) { c.destroy(); delete instances[id]; }
    }

    return { render: render, destroy: destroy };
})();
