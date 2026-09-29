var currentRole = "Owner";

document.addEventListener('DOMContentLoaded', () => {
  document.querySelectorAll('.nav-link').forEach(link => {
    const linkPath = new URL(link.href, window.location.origin).pathname.replace(/\/$/, '').toLowerCase();
    const currentPath = window.location.pathname.replace(/\/$/, '').toLowerCase();
    const isDashboard = linkPath === '/dashboard' && (currentPath === '' || currentPath === '/dashboard');
    if (linkPath === currentPath || isDashboard) {
      link.classList.add('active');
      link.setAttribute('aria-current', 'page');
    }
  });

  if (currentRole !== 'Owner') {
    document.querySelectorAll('.owner-only').forEach(item => item.remove());
    document.querySelectorAll('[data-owner-only]').forEach(item => item.remove());
  }

  const state = window.dashboardState || {};
  const sidebar = document.getElementById('sidebar');
  const menuButton = document.querySelector('.mobile-menu');
  const sidebarOverlay = document.getElementById('sidebarOverlay');
  const offlineBanner = document.getElementById('offlineBanner');
  let dailySalesChartInstance;
  let lowStockChartInstance;
  let consumptionChartInstance;
  let depletionChartInstance;
  const animatedCharts = new Set();
  const motionPreference = window.matchMedia('(prefers-reduced-motion: reduce)');
  const animateKpiValues = () => {
    if (motionPreference.matches) return;
    document.querySelectorAll('.kpi-value').forEach(value => {
      const walker = document.createTreeWalker(value, NodeFilter.SHOW_TEXT);
      const textNodes = [];
      while (walker.nextNode()) textNodes.push(walker.currentNode);
      textNodes.forEach(node => {
        const original = node.nodeValue;
        if (!/\d/.test(original)) return;
        const start = performance.now();
        const duration = 520;
        const tick = now => {
          const progress = Math.min((now - start) / duration, 1);
          const eased = 1 - Math.pow(1 - progress, 4);
          node.nodeValue = original.replace(/\d[\d,]*(?:\.\d+)?%?/g, token => {
            const digits = token.endsWith('%') ? token.slice(0, -1) : token;
            const numericValue = Number(digits.replace(/,/g, ''));
            const decimals = (digits.split('.')[1] || '').length;
            const current = (numericValue * eased).toFixed(decimals);
            return `${Number(current).toLocaleString(undefined, {
              minimumFractionDigits: decimals,
              maximumFractionDigits: decimals
            })}${token.endsWith('%') ? '%' : ''}`;
          });
          if (progress < 1) requestAnimationFrame(tick);
          else node.nodeValue = original;
        };
        requestAnimationFrame(tick);
      });
    });
  };
  const chartRoot = getComputedStyle(document.documentElement);
  const chartColor = token => chartRoot.getPropertyValue(token).trim();
  const chartPalette = {
    primary: chartColor('--chart-primary'),
    secondary: chartColor('--chart-secondary'),
    tertiary: chartColor('--chart-tertiary'),
    quaternary: chartColor('--chart-quaternary'),
    grid: chartColor('--chart-grid'),
    critical: chartColor('--chart-critical'),
    warning: chartColor('--chart-warning'),
    healthy: chartColor('--chart-healthy'),
    criticalFill: chartColor('--chart-critical-fill'),
    text: chartColor('--text'),
    muted: chartColor('--muted'),
    white: chartColor('--white'),
    border: chartColor('--border'),
    font: chartColor('--font-body')
  };
  const salesSeriesColors = [chartPalette.primary, chartPalette.secondary, chartPalette.quaternary, chartPalette.tertiary];
  const chartBaseOptions = (scales, chartId) => ({
    responsive: true,
    maintainAspectRatio: false,
    animation: motionPreference.matches || (chartId && animatedCharts.has(chartId))
      ? false
      : { duration: 620, easing: 'easeOutQuart' },
    color: chartPalette.muted,
    font: { family: chartPalette.font, size: 11 },
    layout: { padding: { top: 4, right: 8, bottom: 0, left: 4 } },
    scales,
    plugins: {
      legend: {
        labels: {
          color: chartPalette.muted,
          font: { family: chartPalette.font, size: 11, weight: '600' },
          boxWidth: 8,
          boxHeight: 8,
          usePointStyle: true,
          pointStyle: 'circle',
          padding: 16
        }
      },
      tooltip: {
        backgroundColor: chartPalette.text,
        titleColor: chartPalette.white,
        bodyColor: chartPalette.white,
        borderColor: chartPalette.border,
        borderWidth: 1,
        cornerRadius: 8,
        padding: 10,
        displayColors: true,
        titleFont: { family: chartPalette.font, weight: '700' },
        bodyFont: { family: chartPalette.font }
      }
    }
  });
  const chartAxis = (showGrid = true) => ({
    grid: { display: showGrid, color: chartPalette.grid, lineWidth: 0.75, drawTicks: false },
    border: { display: false },
    ticks: { color: chartPalette.muted, padding: 8, font: { family: chartPalette.font, size: 11 } }
  });
  const setChartEmpty = (canvas, empty) => {
    if (!canvas) return;
    canvas.classList.toggle('hidden', empty);
    canvas.parentElement?.querySelector(`[data-chart-empty="${canvas.id}"]`)?.classList.toggle('hidden', !empty);
  };
  const dailyCanvas = document.getElementById('dailySalesChart');
  const lowStockCanvas = document.getElementById('lowStockChart');

  const renderDailySalesChart = () => {
    if (!dailyCanvas) return;
    const selectedCategory = document.querySelector('.chart-mode.active')?.dataset.category || 'Whole Chicken';
    const points = (state.dailySalesTrend || state.DailySalesTrend || [])
      .filter(item => (item.category || item.Category) === selectedCategory);
    const labels = [...new Set(points.map(item => item.label || item.Label))];
    const products = [...new Set(points.map(item => item.product || item.Product))];
    if (!labels.length) {
      setChartEmpty(dailyCanvas, true);
      dailySalesChartInstance?.destroy();
      dailySalesChartInstance = null;
      return;
    }
    setChartEmpty(dailyCanvas, false);
    const datasets = products.map((product, index) => ({
      label: product,
      data: labels.map(label => {
        const point = points.find(item => (item.product || item.Product) === product && (item.label || item.Label) === label);
        return point?.value ?? point?.Value ?? 0;
      }),
      borderColor: salesSeriesColors[index % salesSeriesColors.length],
      backgroundColor: 'transparent',
      borderWidth: 2,
      tension: 0.32,
      pointRadius: 0,
      pointHoverRadius: 4,
      pointHoverBackgroundColor: salesSeriesColors[index % salesSeriesColors.length],
      pointHoverBorderColor: chartPalette.white
    }));
    if (!dailySalesChartInstance) {
      dailySalesChartInstance = new Chart(dailyCanvas, {
        type: 'line',
        data: { labels, datasets },
        options: {
          ...chartBaseOptions({
            x: { ...chartAxis(false) },
            y: { ...chartAxis(), beginAtZero: true, title: { display: true, text: selectedCategory === 'Whole Chicken' ? 'pcs' : 'kg', color: chartPalette.muted, font: { family: chartPalette.font, size: 11, weight: '600' } } }
          }, 'dailySalesChart'),
          plugins: { ...chartBaseOptions().plugins, legend: { ...chartBaseOptions().plugins.legend, display: datasets.length > 1 } }
        }
      });
      animatedCharts.add('dailySalesChart');
      return;
    }

    dailySalesChartInstance.data.labels = labels;
    dailySalesChartInstance.data.datasets = datasets;
    dailySalesChartInstance.options.scales.y.title.text = selectedCategory === 'Whole Chicken' ? 'pcs' : 'kg';
    dailySalesChartInstance.options.plugins.legend.display = datasets.length > 1;
    dailySalesChartInstance.update('none');
  };

  const renderLowStockChart = () => {
    if (!lowStockCanvas) return;
    const slices = state.lowStockDistribution || state.LowStockDistribution || [];
    const healthy = slices.length === 0 || (slices.length === 1 && (slices[0].label || slices[0].Label) === 'All products are healthy');
    const emptyState = document.getElementById('lowStockEmpty');
    if (emptyState) emptyState.classList.toggle('hidden', !healthy);
    lowStockCanvas.classList.toggle('hidden', healthy);

    if (healthy) {
      lowStockChartInstance?.destroy();
      lowStockChartInstance = null;
      return;
    }

    const labels = slices.map(item => item.label || item.Label);
    const values = slices.map(item => item.value ?? item.Value ?? 0);
    const colors = slices.map((item, index) => {
      const sourceColor = (item.color || item.Color || '').toLowerCase();
      if (sourceColor === '#ef4444' || sourceColor === '#b42318') return chartPalette.critical;
      if (sourceColor === '#f59e0b' || sourceColor === '#8a4b08') return chartPalette.warning;
      if (sourceColor === '#10b981' || sourceColor === '#176b3a') return chartPalette.healthy;
      return index % 2 === 0 ? chartPalette.primary : chartPalette.secondary;
    });
    if (!lowStockChartInstance) {
      lowStockChartInstance = new Chart(lowStockCanvas, {
        type: 'doughnut',
        data: { labels, datasets: [{ data: values, backgroundColor: colors }] },
        options: {
          ...chartBaseOptions(undefined, 'lowStockChart'),
          cutout: '72%',
          radius: '88%',
          plugins: { ...chartBaseOptions().plugins, legend: { ...chartBaseOptions().plugins.legend, position: 'bottom' } }
        }
      });
      animatedCharts.add('lowStockChart');
      return;
    }

    lowStockChartInstance.data.labels = labels;
    lowStockChartInstance.data.datasets[0].data = values;
    lowStockChartInstance.data.datasets[0].backgroundColor = colors;
    lowStockChartInstance.update('none');
  };

  const closeDrawer = () => {
    sidebar?.classList.remove('open');
    sidebarOverlay?.classList.remove('open');
    document.body.classList.remove('drawer-open');
    menuButton?.setAttribute('aria-expanded', 'false');
  };

  const toggleDrawer = () => {
    const isOpen = sidebar?.classList.toggle('open') ?? false;
    sidebarOverlay?.classList.toggle('open', isOpen);
    document.body.classList.toggle('drawer-open', isOpen);
    menuButton?.setAttribute('aria-expanded', String(isOpen));
  };

  if (menuButton && sidebar) {
    menuButton.setAttribute('aria-expanded', 'false');
    menuButton.addEventListener('click', toggleDrawer);
    sidebarOverlay?.addEventListener('click', closeDrawer);
    sidebar.querySelectorAll('.nav-link').forEach(link => link.addEventListener('click', closeDrawer));
    document.addEventListener('keydown', event => {
      if (event.key === 'Escape') closeDrawer();
    });
  }

  const showOffline = () => {
    if (offlineBanner) {
      const timeLabel = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
      offlineBanner.textContent = `Offline – showing last update at ${timeLabel}`;
      offlineBanner.classList.remove('hidden');
    }
  };

  const hideOffline = () => {
    if (offlineBanner) {
      offlineBanner.classList.add('hidden');
    }
  };

  const renderCharts = () => {
    // Refresh the chart data within their fixed plot areas.
    const consumptionCanvas = document.getElementById('consumptionChart');
    const depletionCanvas = document.getElementById('depletionChart');
    renderDailySalesChart();
    renderLowStockChart();

    if (consumptionCanvas) {
      const weeklyConsumption = state.weeklyConsumption || state.WeeklyConsumption || [];
      setChartEmpty(consumptionCanvas, !weeklyConsumption.length);
      if (!weeklyConsumption.length) {
        consumptionChartInstance?.destroy();
        consumptionChartInstance = null;
      } else {
        const labels = weeklyConsumption.map(item => item.product || item.Product);
        const pcs = weeklyConsumption.map(item => (item.units || item.Units) === 'pcs' ? item.value || item.Value : null);
        const kg = weeklyConsumption.map(item => (item.units || item.Units) === 'kg' ? item.value || item.Value : null);
        const datasets = [
          { label: 'pcs', data: pcs, backgroundColor: chartPalette.primary, borderRadius: 4, barThickness: 14, maxBarThickness: 16 },
          { label: 'kg', data: kg, backgroundColor: chartPalette.quaternary, borderRadius: 4, barThickness: 14, maxBarThickness: 16 }
        ];
        if (!consumptionChartInstance) {
          consumptionChartInstance = new Chart(consumptionCanvas, {
            type: 'bar',
            data: { labels, datasets },
            options: {
              ...chartBaseOptions({
                x: { ...chartAxis(), beginAtZero: true },
                y: { ...chartAxis(false) }
              }, 'consumptionChart'),
              indexAxis: 'y',
              plugins: { ...chartBaseOptions().plugins, legend: { ...chartBaseOptions().plugins.legend, display: true } }
            }
          });
          animatedCharts.add('consumptionChart');
        } else {
          consumptionChartInstance.data.labels = labels;
          consumptionChartInstance.data.datasets = datasets;
          consumptionChartInstance.update('none');
        }
      }
    }

    const activeAlerts = state.activeAlerts || state.ActiveAlerts || [];
    setChartEmpty(depletionCanvas, !activeAlerts.length);
    if (depletionCanvas && activeAlerts.length) {
      const criticalProduct = activeAlerts[0];
      const labels = Array.from({ length: 7 }, (_, index) => `D${index + 1}`);
      const stock = criticalProduct.currentStock ?? criticalProduct.CurrentStock ?? 0;
      const forecast = criticalProduct.forecast ?? criticalProduct.Forecast ?? 0;
      const trend = Array.from({ length: 7 }, (_, index) => Math.max(0, stock - ((index + 1) * forecast)));
      const dataset = {
            label: criticalProduct.productName || criticalProduct.ProductName,
            data: trend,
            borderColor: chartPalette.critical,
            backgroundColor: chartPalette.criticalFill,
            borderWidth: 2,
            pointRadius: 0,
            pointHoverRadius: 4,
            pointHoverBackgroundColor: chartPalette.critical,
            pointHoverBorderColor: chartPalette.white,
            fill: true,
            tension: 0.32
          };
      if (depletionChartInstance) {
        depletionChartInstance.data.labels = labels;
        depletionChartInstance.data.datasets = [dataset];
        depletionChartInstance.update('none');
      } else {
        depletionChartInstance = new Chart(depletionCanvas, {
          type: 'line',
          data: { labels, datasets: [dataset] },
          options: {
          ...chartBaseOptions({
            x: { ...chartAxis(false) },
            y: { ...chartAxis(), beginAtZero: true }
          }, 'depletionChart'),
          plugins: { ...chartBaseOptions().plugins, legend: { ...chartBaseOptions().plugins.legend, display: false } }
          }
        });
        animatedCharts.add('depletionChart');
      }
    } else {
      depletionChartInstance?.destroy();
      depletionChartInstance = null;
    }
  };

  const stockFilter = document.getElementById('stockFilter');
  const categoryFilter = document.getElementById('categoryFilter');
  const statusButtons = document.querySelectorAll('.chip');
  const stockRows = Array.from(document.querySelectorAll('#stockTableBody tr'));
  let activeStatus = 'All';

  const applyTableFilter = () => {
    const query = (stockFilter?.value || '').toLowerCase();
    stockRows.forEach(row => {
      const name = (row.dataset.name || '').toLowerCase();
      const status = row.dataset.status || '';
      const category = row.dataset.category || '';
      const matchesText = !query || name.includes(query);
      const matchesStatus = activeStatus === 'All' || status === activeStatus;
      const matchesCategory = !categoryFilter || categoryFilter.value === 'All' || categoryFilter.value === category;
      const isVisible = matchesText && matchesStatus && matchesCategory;
      row.style.display = isVisible ? '' : 'none';
      if (isVisible && !motionPreference.matches && typeof row.animate === 'function') {
        row.animate([{ opacity: 0.72 }, { opacity: 1 }], { duration: 150, easing: 'ease-out' });
      }
    });
  };

  if (stockFilter) {
    stockFilter.addEventListener('input', applyTableFilter);
  }
  if (categoryFilter) {
    categoryFilter.addEventListener('change', applyTableFilter);
  }

  document.querySelectorAll('.chart-mode').forEach(button => {
    button.addEventListener('click', () => {
      document.querySelectorAll('.chart-mode').forEach(item => item.classList.toggle('active', item === button));
      renderDailySalesChart();
    });
  });

  statusButtons.forEach(button => {
    button.addEventListener('click', () => {
      activeStatus = button.dataset.status || 'All';
      statusButtons.forEach(btn => btn.classList.toggle('active', btn === button));
      applyTableFilter();
    });
  });

  const quickSaleForm = document.getElementById('quick-sale-form');
  const productSelect = document.getElementById('productId');
  const quantityInput = document.getElementById('quantity');
  const updateQuantityStep = () => {
    const unit = productSelect?.selectedOptions[0]?.dataset.unit;
    if (!quantityInput) return;
    quantityInput.step = unit === 'kg' ? '0.5' : '1';
    quantityInput.value = unit === 'kg' ? '1' : String(Math.round(Number(quantityInput.value) || 1));
  };
  productSelect?.querySelectorAll('option').forEach((option, index) => {
    option.dataset.unit = index === 0 ? 'pcs' : 'kg';
  });
  productSelect?.addEventListener('change', updateQuantityStep);
  updateQuantityStep();
  if (quickSaleForm) {
    quickSaleForm.addEventListener('submit', async (event) => {
      event.preventDefault();
      const form = event.currentTarget;
      const formData = new FormData(form);
      const productId = formData.get('productId');
      const quantity = formData.get('quantity');

      try {
        const response = await fetch('/Dashboard/QuickSale', {
          method: 'POST',
          headers: {
            'RequestVerificationToken': formData.get('__RequestVerificationToken')?.toString() || '',
            'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8'
          },
          body: new URLSearchParams({ productId: String(productId), quantity: String(quantity) }).toString()
        });

        if (!response.ok) {
          const data = await response.json().catch(() => ({ message: 'Unable to record the sale.' }));
          throw new Error(data.message || 'Unable to record the sale.');
        }

        const toast = document.getElementById('quick-sale-toast');
        if (toast) {
          toast.textContent = 'Sale recorded successfully.';
          toast.style.color = 'var(--healthy)';
        }

        window.location.reload();
      } catch (error) {
        const toast = document.getElementById('quick-sale-toast');
        if (toast) {
          toast.textContent = error.message || 'Sale could not be recorded.';
          toast.style.color = 'var(--critical)';
        }
      }
    });
  }

  async function refreshDashboard() {
    try {
      const response = await fetch('/Dashboard/Summary', {
        headers: { 'X-Requested-With': 'XMLHttpRequest' }
      });
      if (!response.ok) throw new Error('summary unavailable');
      const summary = await response.json();
      if (summary && summary.activeAlerts) {
        Object.assign(state, summary);
        renderCharts();
        hideOffline();
      }
    } catch (error) {
      console.warn('Polling failed', error);
      showOffline();
    }
  }

  if (document.getElementById('dailySalesChart') || document.getElementById('stockTableBody')) {
    animateKpiValues();
    renderCharts();
    setInterval(() => refreshDashboard(), 30000);
  }
});
