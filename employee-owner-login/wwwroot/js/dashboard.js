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
  const dailySalesColors = ['#2563eb', '#0f766e', '#d97706', '#9333ea', '#64748b'];
  const dailyCanvas = document.getElementById('dailySalesChart');
  const lowStockCanvas = document.getElementById('lowStockChart');

  const renderDailySalesChart = () => {
    if (!dailyCanvas) return;
    const selectedCategory = document.querySelector('.chart-mode.active')?.dataset.category || 'Whole Chicken';
    const points = (state.dailySalesTrend || state.DailySalesTrend || [])
      .filter(item => (item.category || item.Category) === selectedCategory);
    const labels = [...new Set(points.map(item => item.label || item.Label))];
    const products = [...new Set(points.map(item => item.product || item.Product))];
    const datasets = products.map((product, index) => ({
      label: product,
      data: labels.map(label => {
        const point = points.find(item => (item.product || item.Product) === product && (item.label || item.Label) === label);
        return point?.value ?? point?.Value ?? 0;
      }),
      borderColor: dailySalesColors[index % dailySalesColors.length],
      backgroundColor: 'transparent',
      tension: 0.35,
      pointRadius: 2
    }));
    if (!labels.length) return;

    if (!dailySalesChartInstance) {
      dailySalesChartInstance = new Chart(dailyCanvas, {
        type: 'line',
        data: { labels, datasets },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          animation: false,
          scales: { y: { beginAtZero: true, title: { display: true, text: selectedCategory === 'Whole Chicken' ? 'pcs' : 'kg' } } },
          plugins: { legend: { display: datasets.length > 1 } }
        }
      });
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
    const colors = slices.map(item => item.color || item.Color || '#2563eb');
    if (!lowStockChartInstance) {
      lowStockChartInstance = new Chart(lowStockCanvas, {
        type: 'doughnut',
        data: { labels, datasets: [{ data: values, backgroundColor: colors }] },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          animation: false,
          plugins: { legend: { position: 'bottom' } }
        }
      });
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

    if (consumptionCanvas && state.weeklyConsumption) {
      const labels = state.weeklyConsumption.map(item => item.product || item.Product);
      const pcs = state.weeklyConsumption.map(item => (item.units || item.Units) === 'pcs' ? item.value || item.Value : null);
      const kg = state.weeklyConsumption.map(item => (item.units || item.Units) === 'kg' ? item.value || item.Value : null);
      const datasets = [
        { label: 'pcs', data: pcs, backgroundColor: '#2563eb', barThickness: 18 },
        { label: 'kg', data: kg, backgroundColor: '#0f766e', barThickness: 18 }
      ];
      if (!consumptionChartInstance) {
        consumptionChartInstance = new Chart(consumptionCanvas, {
          type: 'bar',
          data: { labels, datasets },
          options: { indexAxis: 'y', responsive: true, maintainAspectRatio: false, scales: { x: { beginAtZero: true } }, plugins: { legend: { display: true } } }
        });
      } else {
        consumptionChartInstance.data.labels = labels;
        consumptionChartInstance.data.datasets = datasets;
        consumptionChartInstance.update();
      }
    }

    if (depletionCanvas && state.activeAlerts && state.activeAlerts.length) {
      const criticalProduct = state.activeAlerts[0];
      const labels = Array.from({ length: 7 }, (_, index) => `D${index + 1}`);
      const stock = criticalProduct.currentStock ?? criticalProduct.CurrentStock ?? 0;
      const forecast = criticalProduct.forecast ?? criticalProduct.Forecast ?? 0;
      const trend = Array.from({ length: 7 }, (_, index) => Math.max(0, stock - ((index + 1) * forecast)));
      depletionChartInstance?.destroy();
      depletionChartInstance = new Chart(depletionCanvas, {
        type: 'line',
        data: {
          labels,
          datasets: [{
            label: criticalProduct.productName || criticalProduct.ProductName,
            data: trend,
            borderColor: '#dc2626',
            backgroundColor: 'rgba(220, 38, 38, 0.12)',
            fill: true,
            tension: 0.4
          }]
        },
        options: { responsive: true, maintainAspectRatio: false, scales: { y: { beginAtZero: true } }, plugins: { legend: { display: false } } }
      });
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
      row.style.display = matchesText && matchesStatus && matchesCategory ? '' : 'none';
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
          toast.style.color = '#16a34a';
        }

        window.location.reload();
      } catch (error) {
        const toast = document.getElementById('quick-sale-toast');
        if (toast) {
          toast.textContent = error.message || 'Sale could not be recorded.';
          toast.style.color = '#dc2626';
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
    renderCharts();
    setInterval(() => refreshDashboard(), 30000);
  }
});
