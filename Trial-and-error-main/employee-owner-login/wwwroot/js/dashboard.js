// PoultryOS dashboard.
// The server sends one JSON snapshot (see DashboardSnapshot.cs). Everything on screen is drawn from it,
// and it is refreshed after every sale and every 30 seconds so all devices show the same numbers.
(() => {
    'use strict';

    // ---------------------------------------------------------------- setup

    const body = document.body;
    const isOwner = body.dataset.role === 'Owner';
    const urls = { snapshot: body.dataset.snapshotUrl, sale: body.dataset.saleUrl, restock: body.dataset.restockUrl };
    const $ = (selector, root = document) => root.querySelector(selector);

    let data = JSON.parse($('#dash-data').textContent);

    const state = {
        filter: 'all',
        query: '',
        trendId: 'all',
        depletionId: null,
        mode: 'sale',        // 'sale' | 'restock'
        productId: null,
        qty: 1,
        busy: false,
        lastUpdate: new Date(),
        online: true
    };

    // ---------------------------------------------------------------- helpers

    const nf = new Intl.NumberFormat('en-PH');
    const dayFmt = new Intl.DateTimeFormat('en-PH', { month: 'short', day: 'numeric' });
    const longDayFmt = new Intl.DateTimeFormat('en-PH', { weekday: 'long', month: 'long', day: 'numeric' });
    const timeFmt = new Intl.DateTimeFormat('en-PH', { hour: 'numeric', minute: '2-digit' });
    const parseDay = (iso) => { const [y, m, d] = iso.split('-').map(Number); return new Date(y, m - 1, d); };
    const addDays = (date, n) => { const d = new Date(date); d.setDate(d.getDate() + n); return d; };
    const pct = (v) => `${(v * 100).toFixed(1)}%`;
    const unitName = (unit, n) => (n === 1 && unit.endsWith('s') ? unit.slice(0, -1) : unit);
    const qtyText = (p, n) => `${nf.format(n)} ${unitName(p.unit, n)}`;
    const statusLabel = { critical: 'Critical', warning: 'Warning', healthy: 'Healthy' };
    const text = (id, value) => { $('#' + id).textContent = value; };

    function h(tag, props, ...kids) {
        const node = document.createElement(tag);
        for (const [key, value] of Object.entries(props || {})) {
            if (value == null || value === false) continue;
            if (key === 'class') node.className = value;
            else if (key === 'text') node.textContent = value;
            else if (key.startsWith('on')) node.addEventListener(key.slice(2), value);
            else node.setAttribute(key, value === true ? '' : value);
        }
        node.append(...kids.flat().filter((k) => k != null && k !== false));
        return node;
    }

    function s(tag, props, ...kids) {
        const node = document.createElementNS('http://www.w3.org/2000/svg', tag);
        for (const [key, value] of Object.entries(props || {})) {
            if (value == null || value === false) continue;
            node.setAttribute(key, value);
        }
        node.append(...kids.flat().filter((k) => k != null && k !== false));
        return node;
    }

    const daysText = (p) => {
        if (p.insufficientData) return 'Needs 7 days of sales';
        if (p.daysLeft == null) return 'No recent sales';
        if (p.daysLeft < 1) return 'Under a day';
        const d = Math.floor(p.daysLeft);
        return d > 365 ? 'Over a year' : `${d} ${d === 1 ? 'day' : 'days'}`;
    };
    const daysLeftPhrase = (p) => (p.daysLeft != null && !p.insufficientData ? `${daysText(p)} left` : daysText(p));
    const runsOutText = (p) => (p.depletionDate ? dayFmt.format(parseDay(p.depletionDate)) : '\u2013');
    const byId = (id) => data.products.find((p) => p.id === id);

    // ---------------------------------------------------------------- header + key figures

    function renderHeader() {
        const now = new Date();
        const hour = now.getHours();
        const greet = hour < 12 ? 'Good morning' : hour < 18 ? 'Good afternoon' : 'Good evening';
        text('greeting', `${greet}, ${body.dataset.user}`);
        text('today-line', longDayFmt.format(parseDay(data.today)));
    }

    function renderSync() {
        const pill = $('#sync');
        pill.classList.toggle('is-offline', !state.online);
        const time = timeFmt.format(state.lastUpdate);
        text('sync-text', state.online ? `Live, updated ${time}` : `Offline, showing ${time}`);
    }

    function renderKpis() {
        const k = data.kpis;
        text('kpi-stock', nf.format(k.totalStock));
        text('kpi-stock-sub', `across ${k.productCount} products`);
        text('kpi-sold', nf.format(k.soldToday));
        text('kpi-sold-sub', `${k.entriesToday} ${k.entriesToday === 1 ? 'entry' : 'entries'} so far`);

        const active = k.critical + k.warning;
        text('kpi-alerts', active);
        text('kpi-alerts-sub', active === 0 ? 'Nothing needs reordering' : `${k.critical} critical, ${k.warning} warning`);
        const alertValue = $('#kpi-alerts');
        alertValue.classList.toggle('is-critical', k.critical > 0);
        alertValue.classList.toggle('is-warning', k.critical === 0 && k.warning > 0);

        if (k.accuracy == null) {
            text('kpi-acc', '\u2013');
            text('kpi-acc-sub', 'Needs a week of sales history');
        } else {
            text('kpi-acc', pct(k.accuracy));
            const target = Math.round(k.accuracyTarget * 100);
            text('kpi-acc-sub', `${k.accuracy >= k.accuracyTarget ? 'Above' : 'Below'} the ${target}% target`);
        }
    }

    // ---------------------------------------------------------------- needs reordering

    function renderAttention() {
        const list = $('#attention-list');
        const items = data.products.filter((p) => p.status !== 'healthy');
        text('attention-count', `${items.length} of ${data.products.length} products`);

        if (items.length === 0) {
            list.replaceChildren(h('li', { class: 'empty', text: 'Nothing needs reordering. No product is at its reorder level or forecast to run out within a week.' }));
            return;
        }

        list.replaceChildren(...items.map((p) => h('li', { class: `attn attn--${p.status}` },
            h('div', { class: 'attn__who' }, h('strong', { text: p.name }), h('span', { text: p.statusReason })),
            h('div', { class: 'attn__left' }, h('strong', { text: `${qtyText(p, p.stock)} left` }), h('span', { text: daysLeftPhrase(p) })),
            h('div', { class: 'attn__act' },
                h('b', { text: p.reorderQty != null ? `Reorder ${qtyText(p, p.reorderQty)}` : 'Reorder amount needs 7 days of sales' }),
                isOwner ? h('button', { class: 'btn-quiet', type: 'button', onclick: () => startRestock(p) }, 'Log restock') : null))));
    }

    // ---------------------------------------------------------------- charts

    function niceScale(maxValue) {
        const rough = Math.max(maxValue, 1) / 4;
        const pow = 10 ** Math.floor(Math.log10(rough));
        const f = rough / pow;
        const step = (f <= 1 ? 1 : f <= 2 ? 2 : f <= 5 ? 5 : 10) * pow;
        return { step, max: Math.ceil(Math.max(maxValue, 1) / step) * step };
    }

    /**
     * Small dependency-free line chart drawn as SVG.
     * cfg: { labels, series:[{name, values, cls, dots, hollowLast}], hlines:[{value,label}], marks:[{index,text}], aria, tipTitle }
     */
    function lineChart(host, cfg) {
        const W = Math.max(260, Math.round(host.clientWidth));
        const H = W < 420 ? 200 : 236;
        const m = { l: 38, r: 12, t: 16, b: 26 };
        const iw = W - m.l - m.r;
        const ih = H - m.t - m.b;
        const n = cfg.labels.length;

        const values = cfg.series.flatMap((x) => x.values).filter((v) => v != null).concat((cfg.hlines || []).map((l) => l.value));
        const { step, max } = niceScale(Math.max(0, ...values));
        const x = (i) => m.l + (n === 1 ? iw / 2 : (iw * i) / (n - 1));
        const y = (v) => m.t + ih - (v / max) * ih;
        const fmtTick = (v) => (step < 1 ? v.toFixed(1) : nf.format(v));

        const svg = s('svg', { viewBox: `0 0 ${W} ${H}`, width: W, height: H, role: 'img', 'aria-label': cfg.aria });

        for (let v = 0; v <= max + 1e-9; v += step) {
            svg.append(s('line', { class: v === 0 ? 'axis-line' : 'grid-line', x1: m.l, x2: W - m.r, y1: y(v), y2: y(v) }));
            svg.append(s('text', { x: m.l - 8, y: y(v) + 4, 'text-anchor': 'end' }, fmtTick(v)));
        }

        const every = Math.max(1, Math.ceil(n / Math.max(2, Math.floor(iw / 62))));
        cfg.labels.forEach((label, i) => {
            // Anchor the label spacing on the day that matters: today (last point) or the first point.
            if ((cfg.anchorStart ? i : n - 1 - i) % every !== 0) return;
            svg.append(s('text', { x: x(i), y: H - 6, 'text-anchor': i === 0 ? 'start' : i === n - 1 ? 'end' : 'middle' }, label));
        });

        for (const line of cfg.hlines || []) {
            svg.append(s('line', { class: 'ref-line', x1: m.l, x2: W - m.r, y1: y(line.value), y2: y(line.value) }));
            svg.append(s('text', { class: 'ref-label', x: W - m.r, y: y(line.value) - 5, 'text-anchor': 'end' }, line.label));
        }

        for (const mark of cfg.marks || []) {
            const mx = x(mark.index);
            svg.append(s('line', { class: 'mark-line', x1: mx, x2: mx, y1: m.t, y2: m.t + ih }));
            svg.append(s('text', { class: 'mark-label', x: mx + (mx > W * 0.6 ? -6 : 6), y: m.t + 11, 'text-anchor': mx > W * 0.6 ? 'end' : 'start' }, mark.text));
        }

        for (const series of cfg.series) {
            let d = '';
            let pen = false;
            series.values.forEach((v, i) => {
                if (v == null) { pen = false; return; }
                d += `${pen ? 'L' : 'M'}${x(i).toFixed(1)} ${y(v).toFixed(1)}`;
                pen = true;
            });
            svg.append(s('path', { class: `line ${series.cls}`, d }));
            if (series.dots) {
                series.values.forEach((v, i) => {
                    if (v == null) return;
                    const hollow = series.hollowLast && i === n - 1;
                    svg.append(s('circle', { class: hollow ? 'dot dot--today' : 'dot', cx: x(i), cy: y(v), r: hollow ? 3.5 : 2.5 }));
                });
            }
        }

        // Hover / touch tooltip
        const guide = s('line', { class: 'guide', y1: m.t, y2: m.t + ih, visibility: 'hidden' });
        const tip = h('div', { class: 'tip', hidden: true });
        svg.append(guide);
        const hit = s('rect', { class: 'hit', x: m.l, y: 0, width: iw, height: H });
        const move = (event) => {
            const box = svg.getBoundingClientRect();
            const px = ((event.clientX - box.left) / box.width) * W;
            const i = Math.min(n - 1, Math.max(0, Math.round(((px - m.l) / iw) * (n - 1))));
            guide.setAttribute('x1', x(i));
            guide.setAttribute('x2', x(i));
            guide.setAttribute('visibility', 'visible');
            const rows = cfg.series
                .filter((sr) => sr.values[i] != null)
                .map((sr) => h('div', {}, h('i', { style: `background:${sr.color}` }), `${sr.name}: ${sr.format ? sr.format(sr.values[i]) : nf.format(sr.values[i])}`));
            tip.replaceChildren(h('strong', { text: cfg.tipTitle ? cfg.tipTitle(i) : cfg.labels[i] }), ...rows);
            tip.hidden = false;
            const half = tip.offsetWidth / 2;
            tip.style.left = `${Math.min(W - half, Math.max(half, x(i)))}px`;
        };
        const leave = () => { guide.setAttribute('visibility', 'hidden'); tip.hidden = true; };
        hit.addEventListener('pointermove', move);
        hit.addEventListener('pointerdown', move);
        hit.addEventListener('pointerleave', leave);
        svg.append(hit);

        host.replaceChildren(svg, tip);
    }

    function renderTrend() {
        const select = $('#trend-select');
        const signature = data.products.map((p) => p.id).join(',');
        if (select.dataset.signature !== signature) {
            select.replaceChildren(h('option', { value: 'all', text: 'All products' }),
                ...[...data.products].sort((a, b) => a.name.localeCompare(b.name)).map((p) => h('option', { value: String(p.id), text: p.name })));
            select.dataset.signature = signature;
        }
        if (![...select.options].some((o) => o.value === state.trendId)) state.trendId = 'all';
        select.value = state.trendId;

        const n = data.dates.length;
        let sold; let wma;
        if (state.trendId === 'all') {
            sold = Array(n).fill(0);
            const sum = Array(n).fill(0);
            const complete = Array(n).fill(true);
            for (const p of data.products) {
                p.history.forEach((v, i) => {
                    sold[i] += v;
                    if (p.wma[i] == null) complete[i] = false; else sum[i] += p.wma[i];
                });
            }
            wma = sum.map((v, i) => (complete[i] ? Math.round(v * 100) / 100 : null));
        } else {
            const p = byId(Number(state.trendId));
            sold = p.history;
            wma = p.wma;
        }

        const host = $('#trend-chart');
        lineChart(host, {
            labels: data.dates.map((d) => dayFmt.format(parseDay(d))),
            series: [
                { name: 'Sold', values: sold, cls: 'line--actual', dots: true, hollowLast: true, color: '#2872b9' },
                { name: '7-day average', values: wma, cls: 'line--wma', color: '#e9785d', format: (v) => v.toFixed(1) }
            ],
            tipTitle: (i) => dayFmt.format(parseDay(data.dates[i])) + (i === n - 1 ? ' (today so far)' : ''),
            aria: `Units sold per day over the last ${n} days. Today so far: ${sold[n - 1]}.`
        });
    }

    function renderHealth() {
        const k = data.kpis;
        const total = k.critical + k.warning + k.healthy;
        const R = 60;
        const C = 2 * Math.PI * R;
        const svg = s('svg', { viewBox: '0 0 152 152', width: 152, height: 152, role: 'img', 'aria-label': `${k.critical} critical, ${k.warning} warning, ${k.healthy} healthy` });
        const g = s('g', { transform: 'rotate(-90 76 76)' });

        if (total === 0) {
            g.append(s('circle', { class: 'seg seg--none', cx: 76, cy: 76, r: R }));
        } else {
            let offset = 0;
            for (const [status, count] of [['critical', k.critical], ['warning', k.warning], ['healthy', k.healthy]]) {
                if (count === 0) continue;
                const len = (count / total) * C;
                const gap = total > count ? 2 : 0;
                g.append(s('circle', { class: `seg seg--${status}`, cx: 76, cy: 76, r: R, 'stroke-dasharray': `${Math.max(len - gap, 0.1)} ${C}`, 'stroke-dashoffset': -offset }));
                offset += len;
            }
        }
        svg.append(g);

        const needs = k.critical + k.warning;
        $('#health-donut').replaceChildren(svg, h('div', { class: 'donut__center' },
            h('strong', { text: String(needs) }),
            h('span', { text: needs === 1 ? 'needs reorder' : 'need reorder' })));

        $('#health-legend').replaceChildren(...[['critical', k.critical], ['warning', k.warning], ['healthy', k.healthy]].map(([status, count]) =>
            h('button', { class: 'legend-row', type: 'button', title: `Show ${statusLabel[status].toLowerCase()} products`, onclick: () => setFilter(status, true) },
                h('i', { class: `i-${status}` }), statusLabel[status], h('b', { text: String(count) }))));
    }

    function renderCategories() {
        const max = Math.max(1, ...data.categories.map((c) => c.units));
        $('#category-bars').replaceChildren(...data.categories.map((c) => h('div', { class: 'bar-row' },
            h('span', { text: c.category }),
            h('div', { class: 'bar-track' }, h('div', { class: 'bar-fill', style: `width:${(c.units / max) * 100}%` })),
            h('b', { text: nf.format(c.units) }))));
    }

    function renderDepletion() {
        const select = $('#depletion-select');
        const signature = data.products.map((p) => p.id).join(',');
        if (select.dataset.signature !== signature) {
            select.replaceChildren(...data.products.map((p) => h('option', { value: String(p.id), text: p.name })));
            select.dataset.signature = signature;
        }
        if (state.depletionId == null || !byId(state.depletionId)) state.depletionId = data.products[0]?.id ?? null;
        select.value = String(state.depletionId);

        const host = $('#depletion-chart');
        const stats = $('#depletion-stats');
        const p = byId(state.depletionId);
        if (!p) { stats.replaceChildren(); host.replaceChildren(h('div', { class: 'chart-msg', text: 'No products yet.' })); return; }

        const stat = (label, value, cls) => h('div', {}, h('dt', { text: label }), h('dd', { class: cls, text: value }));
        stats.replaceChildren(
            stat('Average sold per day', p.dailyRate != null ? `${p.dailyRate.toFixed(2)} ${p.unit}` : '\u2013'),
            stat('Runs out on', runsOutText(p), p.status === 'critical' ? 'is-critical' : ''),
            stat('Reorder amount', p.reorderQty != null ? qtyText(p, p.reorderQty) : '\u2013'));

        if (p.insufficientData) { host.replaceChildren(h('div', { class: 'chart-msg', text: 'Not enough sales history yet. A forecast needs 7 days of sales for this product.' })); return; }
        if (!p.dailyRate) { host.replaceChildren(h('div', { class: 'chart-msg', text: `No recent sales for ${p.name}, so there is no depletion date.` })); return; }

        const horizon = 14;
        const start = parseDay(data.today);
        const projection = Array.from({ length: horizon + 1 }, (_, i) => Math.max(0, p.stock - p.dailyRate * i));
        const marks = p.daysLeft != null && p.daysLeft <= horizon
            ? [{ index: Math.min(horizon, Math.floor(p.daysLeft)), text: `Runs out ${runsOutText(p)}` }]
            : [];

        lineChart(host, {
            labels: projection.map((_, i) => dayFmt.format(addDays(start, i))),
            series: [{ name: 'Stock left', values: projection, cls: 'line--proj', color: '#e9785d', format: (v) => qtyText(p, Math.round(v)) }],
            hlines: [{ value: p.threshold, label: `Reorder level ${p.threshold}` }],
            marks,
            anchorStart: true,
            aria: `Projected stock of ${p.name} over the next ${horizon} days. ${p.depletionDate ? 'Runs out ' + runsOutText(p) + '.' : ''}`
        });
    }

    // ---------------------------------------------------------------- stock table

    function sparkline(values) {
        const w = 76; const hgt = 22; const max = Math.max(1, ...values);
        const pts = values.map((v, i) => `${((i / (values.length - 1)) * (w - 4) + 2).toFixed(1)},${(hgt - 2 - (v / max) * (hgt - 4)).toFixed(1)}`).join(' ');
        return s('svg', { class: 'spark', width: w, height: hgt, viewBox: `0 0 ${w} ${hgt}`, 'aria-hidden': 'true' }, s('polyline', { points: pts }));
    }

    function setFilter(filter, scroll) {
        state.filter = filter;
        renderTable();
        if (scroll) $('#stock-title').scrollIntoView({ behavior: matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth', block: 'start' });
    }

    function renderTable() {
        const k = data.kpis;
        const counts = { all: data.products.length, critical: k.critical, warning: k.warning, healthy: k.healthy };
        $('#filters').replaceChildren(...['all', 'critical', 'warning', 'healthy'].map((f) =>
            h('button', { class: 'filter', type: 'button', 'aria-pressed': String(state.filter === f), onclick: () => setFilter(f) },
                `${f === 'all' ? 'All' : statusLabel[f]} (${counts[f]})`)));

        const q = state.query.trim().toLowerCase();
        const rows = data.products.filter((p) =>
            (state.filter === 'all' || p.status === state.filter) &&
            (!q || p.name.toLowerCase().includes(q) || p.category.toLowerCase().includes(q)));

        text('stock-count', `Showing ${rows.length} of ${data.products.length}`);

        if (rows.length === 0) {
            $('#stock-body').replaceChildren(h('tr', {}, h('td', { class: 'table-empty', colspan: 8, text: 'No products match. Clear the search or pick a different status.' })));
            return;
        }

        $('#stock-body').replaceChildren(...rows.map((p) => h('tr', { class: p.status === 'critical' ? 'row-critical' : '' },
            h('td', {}, h('div', { class: 'cell-name' }, h('strong', { text: p.name }), h('span', { text: p.category }))),
            h('td', { class: 'num', text: qtyText(p, p.stock) }),
            h('td', { class: 'num hide-sm', text: nf.format(p.threshold) }),
            h('td', { class: 'num hide-sm', text: nf.format(p.weeklyConsumption) }),
            h('td', { class: 'hide-sm' }, sparkline(p.history)),
            h('td', { class: 'num', text: p.insufficientData || p.daysLeft == null ? '\u2013' : daysText(p) }),
            h('td', { class: 'hide-md', text: runsOutText(p) }),
            h('td', {}, h('span', { class: `pill pill--${p.status}`, text: statusLabel[p.status] })))));
    }

    // ---------------------------------------------------------------- quick entry (sale / restock)

    const chipsHost = $('#chips');
    const qtyInput = $('#qty');
    const submitBtn = $('#submit');
    const entryMsg = $('#entry-msg');
    const rail = $('#rail');

    const stableProducts = () => [...data.products].sort((a, b) => a.id - b.id);
    const selected = () => byId(state.productId);

    function renderChips() {
        const products = stableProducts();
        const signature = products.map((p) => p.id).join(',');
        if (chipsHost.dataset.signature !== signature) {
            chipsHost.replaceChildren(...products.map((p) => h('label', { class: 'chip' },
                h('input', { type: 'radio', name: 'product', value: String(p.id), onchange: () => { state.productId = p.id; renderEntry(); } }),
                h('span', { class: 'chip__body' },
                    h('span', { class: 'chip__name', text: p.name }),
                    h('span', { class: 'chip__left' }, h('i'), h('span', { text: '' }))))));
            chipsHost.dataset.signature = signature;
        }
        // Update in place so a refresh never steals focus from the person typing.
        for (const label of chipsHost.children) {
            const p = byId(Number($('input', label).value));
            const dot = $('i', label);
            dot.className = `i-${p.status}`;
            $('.chip__left span', label).textContent = `${qtyText(p, p.stock)} left`;
            $('input', label).checked = p.id === state.productId;
        }
    }

    function renderEntry() {
        if (state.productId != null && !byId(state.productId)) state.productId = null;
        const isSale = state.mode === 'sale';
        renderChips();

        $('#entry-title').textContent = isSale ? 'Record a sale' : 'Log a restock';
        submitBtn.textContent = state.busy ? 'Saving\u2026' : isSale ? 'Record sale' : 'Log restock';
        if (isOwner) {
            for (const [id, mode] of [['tab-sale', 'sale'], ['tab-restock', 'restock']]) {
                const tab = $('#' + id);
                tab.classList.toggle('is-active', state.mode === mode);
                tab.setAttribute('aria-pressed', String(state.mode === mode));
            }
        }
        if (document.activeElement !== qtyInput && Number.isFinite(state.qty)) qtyInput.value = String(state.qty);

        const p = selected();
        const preview = $('#preview');
        preview.classList.remove('is-error');
        let valid = true;

        if (!p) {
            preview.textContent = 'Choose a product.';
            valid = false;
        } else if (!Number.isInteger(state.qty) || state.qty < 1) {
            preview.textContent = 'Enter a quantity of at least 1.';
            valid = false;
        } else if (isSale && state.qty > p.stock) {
            preview.textContent = `Only ${qtyText(p, p.stock)} left in stock.`;
            preview.classList.add('is-error');
            valid = false;
        } else if (isSale) {
            const after = p.stock - state.qty;
            const days = p.dailyRate > 0 ? `, about ${Math.floor(after / p.dailyRate)} days at the current pace` : '';
            preview.textContent = `${qtyText(p, after)} left after this sale${days}.`;
        } else {
            preview.textContent = `${qtyText(p, p.stock + state.qty)} in stock after this restock.`;
        }
        submitBtn.disabled = !valid || state.busy;
    }

    function setMode(mode) {
        state.mode = mode;
        entryMsg.textContent = '';
        renderEntry();
    }

    function showMessage(message, kind) {
        entryMsg.textContent = message;
        entryMsg.className = `entry__msg is-${kind}`;
    }

    function toast(message) {
        const el = $('#toast');
        el.textContent = message;
        el.classList.add('show');
        clearTimeout(toast.timer);
        toast.timer = setTimeout(() => el.classList.remove('show'), 3600);
    }

    function sessionExpired() {
        showMessage('Your session ended. Sign in again to continue.', 'error');
        setTimeout(() => location.reload(), 1800);
    }

    async function submitEntry(event) {
        event.preventDefault();
        const p = selected();
        if (!p || submitBtn.disabled) return;

        state.busy = true;
        renderEntry();
        try {
            const response = await fetch(state.mode === 'sale' ? urls.sale : urls.restock, {
                method: 'POST',
                credentials: 'same-origin',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json',
                    'RequestVerificationToken': $('#csrf input[name="__RequestVerificationToken"]')?.value ?? ''
                },
                body: JSON.stringify({ productId: p.id, quantity: state.qty })
            });

            if (response.redirected || response.status === 401 || response.status === 403) { sessionExpired(); return; }
            const isJson = (response.headers.get('content-type') || '').includes('json');
            if (!isJson) { showMessage('Could not save. Reload the page and try again.', 'error'); return; }

            const result = await response.json();
            if (!result.ok) { showMessage(result.message, 'error'); return; }

            data = result.snapshot;
            state.qty = 1;
            state.lastUpdate = new Date();
            state.online = true;
            entryMsg.textContent = '';
            renderAll();
            toast(result.message);
            closeSheet();
        } catch {
            state.online = false;
            renderSync();
            showMessage('No connection. Nothing was saved. Check your signal and try again.', 'error');
        } finally {
            state.busy = false;
            renderEntry();
        }
    }

    function startRestock(p) {
        state.mode = 'restock';
        state.productId = p.id;
        state.qty = p.reorderQty ?? 1;
        entryMsg.textContent = '';
        renderEntry();
        openSheet();
        qtyInput.focus();
        qtyInput.select();
    }

    function renderActivity() {
        const list = $('#activity');
        if (!list) return;
        const items = data.activity || [];
        if (items.length === 0) { list.replaceChildren(h('li', { class: 'empty', text: 'No activity yet.' })); return; }
        list.replaceChildren(...items.map((a) => h('li', { class: a.action === 'Alert' ? 'is-alert' : '' },
            h('span', { class: 'activity__time', text: timeFmt.format(new Date(a.time)) }),
            h('div', {}, h('div', { class: 'activity__text', text: a.description }), h('div', { class: 'activity__who', text: a.user })))));
    }

    // ---------------------------------------------------------------- bottom sheet (small screens)

    const fab = $('#fab');
    const scrim = $('#scrim');
    let sheetOpener = null;

    function openSheet() {
        if (matchMedia('(min-width: 1360px)').matches) return;
        sheetOpener = document.activeElement;
        rail.classList.add('open');
        scrim.classList.add('open');
    }
    function closeSheet() {
        rail.classList.remove('open');
        if (!body.classList.contains('nav-open')) scrim.classList.remove('open');
        if (sheetOpener && sheetOpener.focus && document.contains(sheetOpener)) sheetOpener.focus();
        sheetOpener = null;
    }
    function closeNav() {
        body.classList.remove('nav-open');
        $('#menu-btn').setAttribute('aria-expanded', 'false');
        if (!rail.classList.contains('open')) scrim.classList.remove('open');
    }

    // ---------------------------------------------------------------- refresh

    function renderAll() {
        renderHeader();
        renderSync();
        renderKpis();
        renderAttention();
        renderTrend();
        renderHealth();
        renderCategories();
        renderDepletion();
        renderTable();
        renderEntry();
        renderActivity();
    }

    async function refresh() {
        if (state.busy || document.hidden) return;
        try {
            const response = await fetch(urls.snapshot, { credentials: 'same-origin', headers: { Accept: 'application/json' } });
            if (response.redirected || response.status === 401) { sessionExpired(); return; }
            if (!response.ok) throw new Error(String(response.status));
            data = await response.json();
            state.online = true;
            state.lastUpdate = new Date();
        } catch {
            state.online = false;
            renderSync();
            return;
        }
        renderAll();
    }

    // ---------------------------------------------------------------- wiring

    $('#trend-select').addEventListener('change', (e) => { state.trendId = e.target.value; renderTrend(); });
    $('#depletion-select').addEventListener('change', (e) => { state.depletionId = Number(e.target.value); renderDepletion(); });
    $('#search').addEventListener('input', (e) => { state.query = e.target.value; renderTable(); });
    $('#entry-form').addEventListener('submit', submitEntry);

    qtyInput.addEventListener('input', () => { state.qty = qtyInput.value === '' ? NaN : Number(qtyInput.value); renderEntry(); });
    $('#qty-dec').addEventListener('click', () => { state.qty = Math.max(1, (Number.isFinite(state.qty) ? state.qty : 1) - 1); renderEntry(); });
    $('#qty-inc').addEventListener('click', () => { state.qty = (Number.isFinite(state.qty) ? state.qty : 0) + 1; renderEntry(); });

    if (isOwner) {
        $('#tab-sale').addEventListener('click', () => setMode('sale'));
        $('#tab-restock').addEventListener('click', () => setMode('restock'));
    }

    fab.addEventListener('click', () => { openSheet(); });
    $('#sheet-close').addEventListener('click', closeSheet);
    scrim.addEventListener('click', () => { closeSheet(); closeNav(); });
    $('#menu-btn').addEventListener('click', () => {
        const open = body.classList.toggle('nav-open');
        $('#menu-btn').setAttribute('aria-expanded', String(open));
        scrim.classList.toggle('open', open || rail.classList.contains('open'));
    });

    document.addEventListener('keydown', (event) => {
        if (event.key === 'Escape') { closeSheet(); closeNav(); return; }
        const typing = /^(input|textarea|select)$/i.test(event.target.tagName);
        if (event.key === '/' && !typing) { event.preventDefault(); $('#search').focus(); }
    });

    document.addEventListener('visibilitychange', () => { if (!document.hidden) refresh(); });
    window.addEventListener('online', refresh);
    window.addEventListener('offline', () => { state.online = false; renderSync(); });
    setInterval(refresh, 30000);

    // Charts are drawn at the width of their container, so redraw when it changes.
    const widths = new WeakMap();
    const observer = new ResizeObserver((entries) => {
        for (const entry of entries) {
            const width = Math.round(entry.contentRect.width);
            if (widths.get(entry.target) === width) continue;
            widths.set(entry.target, width);
            requestAnimationFrame(() => (entry.target.id === 'trend-chart' ? renderTrend() : renderDepletion()));
        }
    });
    observer.observe($('#trend-chart'));
    observer.observe($('#depletion-chart'));

    renderAll();
})();
