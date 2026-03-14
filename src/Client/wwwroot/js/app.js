window.SentinelCrypto = {

    // ── Keyboard shortcuts ─────────────────────────────────────────────────
    registerShortcuts(dotnetRef) {
        const handler = (e) => {
            // Don't fire inside inputs
            const tag = document.activeElement?.tagName;
            const inInput = tag === 'INPUT' || tag === 'TEXTAREA';

            if ((e.metaKey || e.ctrlKey) && e.key === 'k') {
                e.preventDefault();
                dotnetRef.invokeMethodAsync('ToggleCommandPalette');
                return;
            }
            if (e.key === 'Escape') {
                dotnetRef.invokeMethodAsync('OnEscape');
                return;
            }
            if (!inInput && !e.metaKey && !e.ctrlKey && !e.altKey && !e.shiftKey) {
                const n = parseInt(e.key);
                if (n >= 1 && n <= 4) {
                    dotnetRef.invokeMethodAsync('SetView', n - 1);
                }
            }
        };
        document.addEventListener('keydown', handler);
        // Return cleanup handle
        return { dispose: () => document.removeEventListener('keydown', handler) };
    },

    disposeHandle(handle) {
        if (handle && typeof handle.dispose === 'function') handle.dispose();
    },

    // ── Utility ────────────────────────────────────────────────────────────
    focusSelector(selector) {
        setTimeout(() => document.querySelector(selector)?.focus(), 50);
    },

    applyBodyClass(cls, add) {
        document.body.classList.toggle(cls, add);
    },

    // ── Trend Analyzer charts ──────────────────────────────────────────────
    renderPriceChart(labels, prices, sma20, sma50, bbUpper, bbLower) {
        if (window._sc_priceChart) { window._sc_priceChart.destroy(); window._sc_priceChart = null; }
        const ctx = document.getElementById('price-chart');
        if (!ctx) return;
        const mono = "'JetBrains Mono', monospace";
        window._sc_priceChart = new Chart(ctx, {
            type: 'line',
            data: {
                labels,
                datasets: [
                    { label: 'BB Lower', data: bbLower, borderColor: 'rgba(100,116,139,0.3)', borderWidth: 1, pointRadius: 0, fill: false, order: 5 },
                    { label: 'BB Upper', data: bbUpper, borderColor: 'rgba(100,116,139,0.3)', borderWidth: 1, pointRadius: 0, fill: '-1', backgroundColor: 'rgba(100,116,139,0.06)', order: 4 },
                    { label: 'SMA 50',   data: sma50,   borderColor: 'rgba(245,158,11,0.75)', borderWidth: 1.5, borderDash: [5,5], pointRadius: 0, fill: false, order: 3 },
                    { label: 'SMA 20',   data: sma20,   borderColor: 'rgba(139,92,246,0.75)', borderWidth: 1.5, borderDash: [5,5], pointRadius: 0, fill: false, order: 2 },
                    { label: 'Price',    data: prices,  borderColor: '#00d4aa', borderWidth: 2, pointRadius: 0, tension: 0.1, fill: false, order: 1 },
                ]
            },
            options: {
                responsive: true, maintainAspectRatio: false, animation: { duration: 400 },
                interaction: { mode: 'index', intersect: false },
                plugins: {
                    legend: { labels: { color: '#484f58', font: { family: mono, size: 11 }, boxWidth: 18, padding: 14 } },
                    tooltip: {
                        backgroundColor: 'rgba(6,8,16,0.93)', borderColor: 'rgba(255,255,255,0.07)', borderWidth: 1,
                        titleColor: '#f0f4f8', bodyColor: '#8b949e',
                        titleFont: { family: mono, size: 12 }, bodyFont: { family: mono, size: 11 },
                        callbacks: {
                            label: c => c.raw == null ? null :
                                ` ${c.dataset.label}: ${Number(c.raw).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 4 })}`
                        }
                    }
                },
                scales: {
                    x: { grid: { color: 'rgba(255,255,255,0.03)' }, ticks: { color: '#484f58', font: { family: mono, size: 10 }, maxTicksLimit: 8, maxRotation: 0 } },
                    y: { position: 'right', grid: { color: 'rgba(255,255,255,0.03)' }, ticks: { color: '#484f58', font: { family: mono, size: 10 }, callback: v => Number(v).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 4 }) } }
                }
            }
        });
    },

    updateMlPrediction(futureLabels, predicted, upper, lower) {
        const chart = window._sc_priceChart;
        if (!chart) return;
        const mono = "'JetBrains Mono', monospace";

        // Extend labels
        const allLabels = [...chart.data.labels, ...futureLabels];

        // Pad historical datasets with nulls for the future slots
        const histLen = chart.data.labels.length;
        const futLen  = futureLabels.length;

        // Connecting point: repeat last historical price so the prediction line starts from there
        const lastPrice = chart.data.datasets.find(d => d.label === 'Price')
            ?.data.filter(v => v != null).at(-1) ?? null;
        const connector = [lastPrice, ...predicted.slice(1)];

        // Pad existing datasets
        chart.data.datasets.forEach(ds => {
            const pad = new Array(futLen).fill(null);
            ds.data = [...ds.data, ...pad];
        });

        // ML confidence band (lower fill)
        chart.data.datasets.push({
            label: 'ML Lower',
            data: [...new Array(histLen - 1).fill(null), lastPrice, ...lower.slice(1)],
            borderColor: 'transparent',
            borderWidth: 0,
            pointRadius: 0,
            fill: false,
            order: 10
        });
        // ML confidence band (upper fill to lower)
        chart.data.datasets.push({
            label: 'ML Upper',
            data: [...new Array(histLen - 1).fill(null), lastPrice, ...upper.slice(1)],
            borderColor: 'rgba(99,102,241,0.15)',
            borderWidth: 1,
            borderDash: [2, 4],
            pointRadius: 0,
            fill: '-1',
            backgroundColor: 'rgba(99,102,241,0.08)',
            order: 9
        });
        // ML predicted line
        chart.data.datasets.push({
            label: 'ML Forecast',
            data: [...new Array(histLen - 1).fill(null), lastPrice, ...connector],
            borderColor: '#818cf8',
            borderWidth: 2,
            borderDash: [6, 3],
            pointRadius: 0,
            tension: 0.3,
            fill: false,
            order: 0
        });

        chart.data.labels = allLabels;

        // Add NOW annotation
        chart.options.plugins.annotation = {
            annotations: {
                nowLine: {
                    type: 'line',
                    xMin: chart.data.labels[histLen - 1],
                    xMax: chart.data.labels[histLen - 1],
                    borderColor: 'rgba(255,255,255,0.15)',
                    borderWidth: 1,
                    borderDash: [4, 4],
                    label: {
                        content: 'NOW',
                        display: true,
                        position: 'start',
                        yAdjust: 8,
                        backgroundColor: 'rgba(0,0,0,0)',
                        color: 'rgba(255,255,255,0.25)',
                        font: { family: mono, size: 9 }
                    }
                }
            }
        };

        chart.update('none');
    },

    renderRsiChart(labels, rsi) {
        if (window._sc_rsiChart) { window._sc_rsiChart.destroy(); window._sc_rsiChart = null; }
        const ctx = document.getElementById('rsi-chart');
        if (!ctx) return;
        const mono = "'JetBrains Mono', monospace";
        const ob70 = labels.map(() => 70);
        const os30 = labels.map(() => 30);
        window._sc_rsiChart = new Chart(ctx, {
            type: 'line',
            data: {
                labels,
                datasets: [
                    { label: 'Overbought 70', data: ob70, borderColor: 'rgba(239,68,68,0.35)',   borderWidth: 1, borderDash: [4,4], pointRadius: 0, fill: false },
                    { label: 'Oversold 30',   data: os30, borderColor: 'rgba(16,185,129,0.35)', borderWidth: 1, borderDash: [4,4], pointRadius: 0, fill: false },
                    { label: 'RSI',           data: rsi,  borderColor: '#6366f1', borderWidth: 1.5, pointRadius: 0, fill: false },
                ]
            },
            options: {
                responsive: true, maintainAspectRatio: false, animation: { duration: 400 },
                interaction: { mode: 'index', intersect: false },
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        backgroundColor: 'rgba(6,8,16,0.93)', borderColor: 'rgba(255,255,255,0.07)', borderWidth: 1,
                        titleColor: '#f0f4f8', bodyColor: '#8b949e',
                        titleFont: { family: mono, size: 12 }, bodyFont: { family: mono, size: 11 },
                        filter: i => i.datasetIndex === 2,
                        callbacks: { label: c => ` RSI: ${Number(c.raw).toFixed(1)}` }
                    }
                },
                scales: {
                    x: { grid: { color: 'rgba(255,255,255,0.03)' }, ticks: { color: '#484f58', font: { family: mono, size: 10 }, maxTicksLimit: 8, maxRotation: 0 } },
                    y: { position: 'right', min: 0, max: 100, grid: { color: 'rgba(255,255,255,0.03)' }, ticks: { color: '#484f58', font: { family: mono, size: 10 }, stepSize: 20 } }
                }
            }
        });
    }
};
