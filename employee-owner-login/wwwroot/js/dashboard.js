document.addEventListener('DOMContentLoaded', () => {
  const state = window.dashboardState || {};
  const sidebar = document.getElementById('sidebar');
  const menuButton = document.querySelector('.mobile-menu');
  const offlineBanner = document.getElementById('offlineBanner');

  if (menuButton && sidebar) {
    menuButton.addEventListener('click', () => sidebar.classList.toggle('open'));
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
    // Polling keeps the dashboard live without a full page reload: fetch the latest summary, then re-render the charts in place.
    const dailyCanvas = document.getElementById('dailySalesChart');
    const lowStockCanvas = document.getElementById('lowStockChart');
    const consumptionCanvas = document.getElementById('consumptionChart');
    const depletionCanvas = document.getElementById('depletionChart');

    if (dailyCanvas && state.dailySalesTrend) {
      const labels = state.dailySalesTrend.map(item => item.key || item.Key || item[0]);
      const values = state.dailySalesTrend.map(item => item.value || item.Value || item[1]);
      if (window.dailySalesChart) window.dailySalesChart.destroy();
      window.dailySalesChart = new Chart(dailyCanvas, {
        type: 'line',
        data: {
          labels,
          datasets: [{
            label: 'Units sold',
            data: values,
            borderColor: '#2563eb',
            backgroundColor: 'rgba(37, 99, 235, 0.15)',
            fill: true,
            tension: 0.35,
            pointRadius: 3,
            pointHoverRadius: 5
          }]
        },
        options: { responsive: true, maintainAspectRatio: false, scales: { y: { beginAtZero: true } }, plugins: { legend: { display: false } } }
      });
    }

    if (lowStockCanvas && state.lowStockDistribution) {
      const slices = state.lowStockDistribution;
      if (window.lowStockChart) window.lowStockChart.destroy();
      window.lowStockChart = new Chart(lowStockCanvas, {
        type: 'doughnut',
        data: {
          labels: slices.map(item => item.label || item.Label),
          datasets: [{
            data: slices.map(item => item.value || item.Value),
            backgroundColor: slices.map(item => item.color || item.Color || '#2563eb')
          }]
        },
        options: { responsive: true, maintainAspectRatio: false, plugins: { legend: { position: 'bottom' } } }
      });
    }

    if (consumptionCanvas && state.weeklyConsumption) {
      const labels = state.weeklyConsumption.map(item => item.key || item.Key || item[0]);
      const values = state.weeklyConsumption.map(item => item.value || item.Value || item[1]);
      if (window.consumptionChart) window.consumptionChart.destroy();
      window.consumptionChart = new Chart(consumptionCanvas, {
        type: 'bar',
        data: {
          labels,
          datasets: [{ label: 'Units consumed', data: values, backgroundColor: '#60a5fa' }]
        },
        options: { responsive: true, maintainAspectRatio: false, scales: { y: { beginAtZero: true } }, plugins: { legend: { display: false } } }
      });
    }

    if (depletionCanvas && state.activeAlerts && state.activeAlerts.length) {
      const criticalProduct = state.activeAlerts[0];
      const labels = Array.from({ length: 14 }, (_, index) => `D${index + 1}`);
      const trend = Array.from({ length: 14 }, (_, index) => Math.max(0, (criticalProduct.currentStock || criticalProduct.CurrentStock || 0) - ((index + 1) * 2)));
      if (window.depletionChart) window.depletionChart.destroy();
      window.depletionChart = new Chart(depletionCanvas, {
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
  const statusButtons = document.querySelectorAll('.chip');
  const stockRows = Array.from(document.querySelectorAll('#stockTableBody tr'));
  let activeStatus = 'All';

  const applyTableFilter = () => {
    const query = (stockFilter?.value || '').toLowerCase();
    stockRows.forEach(row => {
      const name = (row.dataset.name || '').toLowerCase();
      const status = row.dataset.status || '';
      const matchesText = !query || name.includes(query);
      const matchesStatus = activeStatus === 'All' || status === activeStatus;
      row.style.display = matchesText && matchesStatus ? '' : 'none';
    });
  };

  if (stockFilter) {
    stockFilter.addEventListener('input', applyTableFilter);
  }

  statusButtons.forEach(button => {
    button.addEventListener('click', () => {
      activeStatus = button.dataset.status || 'All';
      statusButtons.forEach(btn => btn.classList.toggle('active', btn === button));
      applyTableFilter();
    });
  });

  const quickSaleForm = document.getElementById('quick-sale-form');
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

        await refreshDashboard();
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

  renderCharts();
  setInterval(() => refreshDashboard(), 30000);
});
