var currentRole = "Owner";

document.addEventListener("DOMContentLoaded", () => {
  document.querySelectorAll(".nav-link").forEach((link) => {
    const linkPath = new URL(link.href, window.location.origin).pathname
      .replace(/\/$/, "")
      .toLowerCase();
    const currentPath = window.location.pathname
      .replace(/\/$/, "")
      .toLowerCase();
    const isDashboard =
      linkPath === "/dashboard" &&
      (currentPath === "" || currentPath === "/dashboard");
    if (linkPath === currentPath || isDashboard) {
      link.classList.add("active");
      link.setAttribute("aria-current", "page");
    }
  });

  if (currentRole !== "Owner") {
    document.querySelectorAll(".owner-only").forEach((item) => item.remove());
    document
      .querySelectorAll("[data-owner-only]")
      .forEach((item) => item.remove());
  }

  const state = window.dashboardState || {};
  const sidebar = document.getElementById("sidebar");
  const menuButton = document.querySelector(".mobile-menu");
  const sidebarOverlay = document.getElementById("sidebarOverlay");
  // Tablet only: reveals the icon rail's labels. The control is a no-op on
  // desktop (hidden) and on mobile (the drawer handles that width instead).
  const railToggle = document.querySelector(".sidebar-toggle");
  const offlineBanner = document.getElementById("offlineBanner");
  let weeklyStockChartInstance;
  let thresholdChartInstance;
  let consumptionChartInstance;
  let depletionChartInstance;
  const animatedCharts = new Set();
  const motionPreference = window.matchMedia(
    "(prefers-reduced-motion: reduce)",
  );
  const animateKpiValues = () => {
    if (motionPreference.matches) return;
    document.querySelectorAll(".kpi-value").forEach((value) => {
      const walker = document.createTreeWalker(value, NodeFilter.SHOW_TEXT);
      const textNodes = [];
      while (walker.nextNode()) textNodes.push(walker.currentNode);
      textNodes.forEach((node) => {
        const original = node.nodeValue;
        if (!/\d/.test(original)) return;
        const start = performance.now();
        const duration = 520;
        const tick = (now) => {
          const progress = Math.min((now - start) / duration, 1);
          const eased = 1 - Math.pow(1 - progress, 4);
          node.nodeValue = original.replace(
            /\d[\d,]*(?:\.\d+)?%?/g,
            (token) => {
              const digits = token.endsWith("%") ? token.slice(0, -1) : token;
              const numericValue = Number(digits.replace(/,/g, ""));
              const decimals = (digits.split(".")[1] || "").length;
              const current = (numericValue * eased).toFixed(decimals);
              return `${Number(current).toLocaleString(undefined, {
                minimumFractionDigits: decimals,
                maximumFractionDigits: decimals,
              })}${token.endsWith("%") ? "%" : ""}`;
            },
          );
          if (progress < 1) requestAnimationFrame(tick);
          else node.nodeValue = original;
        };
        requestAnimationFrame(tick);
      });
    });
  };
  const chartRoot = getComputedStyle(document.documentElement);
  // Global fallback only. Prefer paletteFor(canvas) so a chart can pick up the
  // values its own card sets (see the --chart-dark-*/--chart-light-* blocks in
  // tokens.css and dashboard.css).
  const chartColor = (token) => chartRoot.getPropertyValue(token).trim();

  // Resolve a chart's colours from the CANVAS, not from :root, so the dark cards
  // and the white plotting sheet can each carry their own palette. Falls back to
  // the :root token when the canvas has no scoped value.
  const paletteFor = (canvas) => {
    const scoped = canvas ? getComputedStyle(canvas) : null;
    const scopedColor = (token) =>
      scoped ? scoped.getPropertyValue(token).trim() : "";
    const pick = (token, fallback) =>
      scopedColor(token) || chartColor(token) || fallback;

    const fillTop = pick("--chart-fill-top", "");
    const fillBottom = pick("--chart-fill-bottom", "transparent");
    const criticalFill = pick("--chart-critical-fill", "transparent");

    return {
      primary: pick("--chart-primary", "#b41818"),
      secondary: pick("--chart-secondary", "#8f0d0d"),
      tertiary: pick("--chart-tertiary", "#6d4a47"),
      quaternary: pick("--chart-quaternary", "#8a4b08"),
      grid: pick("--chart-grid", "#b5b5b5"),
      critical: pick("--chart-critical", "#b41818"),
      warning: pick("--chart-warning", "#8a4b08"),
      healthy: pick("--chart-healthy", "#176b3a"),
      criticalFill,
      legend: pick("--chart-legend", "") || pick("--muted", "#3d3d3d"),
      text: pick("--chart-text", "") || pick("--text", "#111111"),
      muted: pick("--chart-tick", "") || pick("--muted", "#3d3d3d"),
      white: pick("--white", "#ffffff"),
      border: pick("--chart-border", "") || pick("--border", "#dcdcdc"),
      font: pick("--font-body", "sans-serif"),
      // Vertical fade painted under the trend lines: strong at the line,
      // transparent at the baseline. Scriptable so it survives a resize; falls
      // back to the flat token before the first layout pass.
      lineFill: (context) => {
        const area = context.chart.chartArea;
        if (!area) return criticalFill;
        const gradient = context.chart.ctx.createLinearGradient(
          0,
          area.top,
          0,
          area.bottom,
        );
        gradient.addColorStop(0, fillTop || criticalFill);
        gradient.addColorStop(1, fillBottom);
        return gradient;
      },
    };
  };

  const chartBaseOptions = (palette, scales, chartId) => ({
    responsive: true,
    maintainAspectRatio: false,
    animation:
      motionPreference.matches || (chartId && animatedCharts.has(chartId))
        ? false
        : { duration: 620, easing: "easeOutQuart" },
    color: palette.muted,
    font: { family: palette.font, size: 11 },
    layout: { padding: { top: 4, right: 8, bottom: 0, left: 4 } },
    scales,
    plugins: {
      legend: {
        labels: {
          color: palette.legend,
          font: { family: palette.font, size: 11, weight: "600" },
          boxWidth: 8,
          boxHeight: 8,
          usePointStyle: true,
          pointStyle: "circle",
          padding: 16,
        },
      },
      tooltip: {
        backgroundColor: palette.text,
        titleColor: palette.white,
        bodyColor: palette.white,
        borderColor: palette.border,
        borderWidth: 1,
        cornerRadius: 8,
        padding: 10,
        displayColors: true,
        titleFont: { family: palette.font, weight: "700" },
        bodyFont: { family: palette.font },
      },
    },
  });
  const chartAxis = (palette, showGrid = true) => ({
    grid: {
      display: showGrid,
      color: palette.grid,
      lineWidth: 0.75,
      drawTicks: false,
    },
    border: { display: false },
    ticks: {
      color: palette.muted,
      padding: 8,
      font: { family: palette.font, size: 11 },
    },
  });
  const setChartEmpty = (canvas, empty) => {
    if (!canvas) return;
    canvas.classList.toggle("hidden", empty);
    canvas.parentElement
      ?.querySelector(`[data-chart-empty="${canvas.id}"]`)
      ?.classList.toggle("hidden", !empty);
  };
  const weeklyStockCanvas = document.getElementById("weeklyStockChart");
  const thresholdCanvas = document.getElementById("thresholdChart");
  const stockOverviewRows = () =>
    state.stockOverview || state.StockOverview || [];
  const stockField = (row, field) =>
    row[field] ?? row[field[0].toUpperCase() + field.slice(1)];
  const sortedStockRows = () =>
    [...stockOverviewRows()].sort((left, right) => {
      const leftSales = Number(stockField(left, "weeklyConsumption")) || 0;
      const rightSales = Number(stockField(right, "weeklyConsumption")) || 0;
      const leftUrgency =
        leftSales > 0
          ? Number(stockField(left, "currentStock")) / leftSales
          : Number.POSITIVE_INFINITY;
      const rightUrgency =
        rightSales > 0
          ? Number(stockField(right, "currentStock")) / rightSales
          : Number.POSITIVE_INFINITY;
      return leftUrgency - rightUrgency;
    });
  const stockTooltip = (context, rows) => {
    const units = stockField(rows[context.dataIndex], "units") || "";
    const value = Number(context.raw).toLocaleString(undefined, {
      maximumFractionDigits: 2,
    });
    return `${context.dataset.label}: ${value} ${units}`.trim();
  };
  const renderStockChart = (
    canvas,
    chart,
    chartId,
    secondLabel,
    secondField,
    currentColor,
  ) => {
    if (!canvas) return chart;
    const rows = sortedStockRows();
    setChartEmpty(canvas, !rows.length);
    if (!rows.length) {
      chart?.destroy();
      return null;
    }

    const palette = paletteFor(canvas);
    const labels = rows.map((row) => stockField(row, "product"));
    const datasets = [
      {
        label: "Current stock",
        data: rows.map((row) => Number(stockField(row, "currentStock")) || 0),
        backgroundColor:
          typeof currentColor === "function"
            ? rows.map(currentColor)
            : currentColor,
        borderRadius: 3,
        barThickness: 8,
        maxBarThickness: 12,
      },
      {
        label: secondLabel,
        data: rows.map((row) => Number(stockField(row, secondField)) || 0),
        backgroundColor: "#a2a2a2",
        borderRadius: 3,
        barThickness: 8,
        maxBarThickness: 12,
      },
    ];
    const base = chartBaseOptions(
      palette,
      {
        x: { ...chartAxis(palette), beginAtZero: true },
        y: { ...chartAxis(palette, false) },
      },
      chartId,
    );
    const options = {
      ...base,
      indexAxis: "y",
      plugins: {
        ...base.plugins,
        tooltip: {
          ...base.plugins.tooltip,
          callbacks: { label: (context) => stockTooltip(context, rows) },
        },
      },
    };
    if (!chart) {
      chart = new Chart(canvas, {
        type: "bar",
        data: { labels, datasets },
        options,
      });
      animatedCharts.add(chartId);
      return chart;
    }
    chart.data.labels = labels;
    chart.data.datasets = datasets;
    chart.options = options;
    chart.update("none");
    return chart;
  };
  const renderWeeklyStockChart = () => {
    weeklyStockChartInstance = renderStockChart(
      weeklyStockCanvas,
      weeklyStockChartInstance,
      "weeklyStockChart",
      "Last 7 days sales",
      "weeklyConsumption",
      "#4f9de8",
    );
  };
  const renderThresholdChart = () => {
    thresholdChartInstance = renderStockChart(
      thresholdCanvas,
      thresholdChartInstance,
      "thresholdChart",
      "Reorder threshold",
      "reorderThreshold",
      (row) =>
        Number(stockField(row, "currentStock")) <
          Number(stockField(row, "reorderThreshold"))
          ? "#ff7670"
          : "#61d394",
    );
  };

  const closeDrawer = () => {
    sidebar?.classList.remove("open");
    sidebarOverlay?.classList.remove("open");
    document.body.classList.remove("drawer-open");
    menuButton?.setAttribute("aria-expanded", "false");
  };

  const toggleDrawer = () => {
    const isOpen = sidebar?.classList.toggle("open") ?? false;
    sidebarOverlay?.classList.toggle("open", isOpen);
    document.body.classList.toggle("drawer-open", isOpen);
    menuButton?.setAttribute("aria-expanded", String(isOpen));
  };

  if (menuButton && sidebar) {
    menuButton.setAttribute("aria-expanded", "false");
    menuButton.addEventListener("click", toggleDrawer);
    sidebarOverlay?.addEventListener("click", closeDrawer);
    sidebar
      .querySelectorAll(".nav-link")
      .forEach((link) => link.addEventListener("click", closeDrawer));
    document.addEventListener("keydown", (event) => {
      if (event.key === "Escape") closeDrawer();
    });
  }

  // Tablet rail: toggles the label column of the collapsed sidebar.
  if (railToggle) {
    const setRailExpanded = (expanded) => {
      document.body.classList.toggle("rail-expanded", expanded);
      railToggle.setAttribute("aria-expanded", String(expanded));
      railToggle.setAttribute(
        "aria-label",
        expanded ? "Collapse navigation" : "Expand navigation",
      );
    };

    setRailExpanded(false);
    railToggle.addEventListener("click", () =>
      setRailExpanded(!document.body.classList.contains("rail-expanded")),
    );
    document.addEventListener("keydown", (event) => {
      if (event.key === "Escape") setRailExpanded(false);
    });
    // Clicking the content, the shell bar or the scrim puts the rail back.
    // Uses the toggle class, not the new button class, so this cannot clash
    // with the drawer handler above.
    document.addEventListener("click", (event) => {
      if (!document.body.classList.contains("rail-expanded")) return;
      if (
        event.target.closest(".sidebar") ||
        event.target.closest(".sidebar-toggle")
      )
        return;
      setRailExpanded(false);
    });
  }

  const showOffline = () => {
    if (offlineBanner) {
      const timeLabel = new Date().toLocaleTimeString([], {
        hour: "2-digit",
        minute: "2-digit",
      });
      offlineBanner.textContent = `Offline – showing last update at ${timeLabel}`;
      offlineBanner.classList.remove("hidden");
    }
  };

  const hideOffline = () => {
    if (offlineBanner) {
      offlineBanner.classList.add("hidden");
    }
  };

  const renderCharts = () => {
    // Refresh the chart data within their fixed plot areas.
    const consumptionCanvas = document.getElementById("consumptionChart");
    const depletionCanvas = document.getElementById("depletionChart");
    renderWeeklyStockChart();
    renderThresholdChart();

    if (consumptionCanvas) {
      const palette = paletteFor(consumptionCanvas);
      const weeklyConsumption =
        state.weeklyConsumption || state.WeeklyConsumption || [];
      setChartEmpty(consumptionCanvas, !weeklyConsumption.length);
      if (!weeklyConsumption.length) {
        consumptionChartInstance?.destroy();
        consumptionChartInstance = null;
      } else {
        const labels = weeklyConsumption.map(
          (item) => item.product || item.Product,
        );
        const pcs = weeklyConsumption.map((item) =>
          (item.units || item.Units) === "pcs"
            ? item.value || item.Value
            : null,
        );
        const kg = weeklyConsumption.map((item) =>
          (item.units || item.Units) === "kg" ? item.value || item.Value : null,
        );
        const datasets = [
          {
            label: "pcs",
            data: pcs,
            backgroundColor: palette.primary,
            borderRadius: 4,
            barThickness: 14,
            maxBarThickness: 16,
          },
          {
            label: "kg",
            data: kg,
            backgroundColor: palette.quaternary,
            borderRadius: 4,
            barThickness: 14,
            maxBarThickness: 16,
          },
        ];
        if (!consumptionChartInstance) {
          consumptionChartInstance = new Chart(consumptionCanvas, {
            type: "bar",
            data: { labels, datasets },
            options: {
              ...chartBaseOptions(
                palette,
                {
                  x: { ...chartAxis(palette), beginAtZero: true },
                  y: { ...chartAxis(palette, false) },
                },
                "consumptionChart",
              ),
              indexAxis: "y",
              plugins: {
                ...chartBaseOptions(palette).plugins,
                legend: {
                  ...chartBaseOptions(palette).plugins.legend,
                  display: true,
                },
              },
            },
          });
          animatedCharts.add("consumptionChart");
        } else {
          consumptionChartInstance.data.labels = labels;
          consumptionChartInstance.data.datasets = datasets;
          consumptionChartInstance.update("none");
        }
      }
    }

    const activeAlerts = state.activeAlerts || state.ActiveAlerts || [];
    setChartEmpty(depletionCanvas, !activeAlerts.length);
    if (depletionCanvas && activeAlerts.length) {
      const palette = paletteFor(depletionCanvas);
      const criticalProduct = activeAlerts[0];
      const labels = Array.from({ length: 7 }, (_, index) => `D${index + 1}`);
      const stock =
        criticalProduct.currentStock ?? criticalProduct.CurrentStock ?? 0;
      const forecast =
        criticalProduct.forecast ?? criticalProduct.Forecast ?? 0;
      const trend = Array.from({ length: 7 }, (_, index) =>
        Math.max(0, stock - (index + 1) * forecast),
      );
      const dataset = {
        label: criticalProduct.productName || criticalProduct.ProductName,
        data: trend,
        borderColor: palette.critical,
        backgroundColor: palette.lineFill,
        borderWidth: 2,
        pointRadius: 0,
        pointHoverRadius: 4,
        pointHoverBackgroundColor: palette.critical,
        pointHoverBorderColor: palette.white,
        fill: true,
        tension: 0.32,
      };
      if (depletionChartInstance) {
        depletionChartInstance.data.labels = labels;
        depletionChartInstance.data.datasets = [dataset];
        depletionChartInstance.update("none");
      } else {
        depletionChartInstance = new Chart(depletionCanvas, {
          type: "line",
          data: { labels, datasets: [dataset] },
          options: {
            ...chartBaseOptions(
              palette,
              {
                x: { ...chartAxis(palette, false) },
                y: { ...chartAxis(palette), beginAtZero: true },
              },
              "depletionChart",
            ),
            plugins: {
              ...chartBaseOptions(palette).plugins,
              legend: {
                ...chartBaseOptions(palette).plugins.legend,
                display: false,
              },
            },
          },
        });
        animatedCharts.add("depletionChart");
      }
    } else {
      depletionChartInstance?.destroy();
      depletionChartInstance = null;
    }
  };

  const dashboardTabs = Array.from(
    document.querySelectorAll("[data-dashboard-tab]"),
  );
  const dashboardPanels = Array.from(
    document.querySelectorAll("[data-dashboard-panel]"),
  );
  const chartByDashboardTab = {
    "weekly-sales": () => consumptionChartInstance,
    depletion: () => depletionChartInstance,
  };
  const activateDashboardTab = (tab, moveFocus = false) => {
    if (!tab) return;
    const selectedKey = tab.dataset.dashboardTab;
    dashboardTabs.forEach((item) => {
      const selected = item === tab;
      item.classList.toggle("active", selected);
      item.setAttribute("aria-selected", String(selected));
      item.tabIndex = selected ? 0 : -1;
    });
    dashboardPanels.forEach((panel) => {
      panel.hidden = panel.dataset.dashboardPanel !== selectedKey;
    });
    if (moveFocus) tab.focus();

    const chart = chartByDashboardTab[selectedKey]?.();
    if (chart) requestAnimationFrame(() => chart.resize());
  };

  dashboardTabs.forEach((tab, index) => {
    tab.addEventListener("click", () => activateDashboardTab(tab));
    tab.addEventListener("keydown", (event) => {
      let nextIndex = index;
      if (event.key === "ArrowRight")
        nextIndex = (index + 1) % dashboardTabs.length;
      else if (event.key === "ArrowLeft")
        nextIndex = (index - 1 + dashboardTabs.length) % dashboardTabs.length;
      else if (event.key === "Home") nextIndex = 0;
      else if (event.key === "End") nextIndex = dashboardTabs.length - 1;
      else return;
      event.preventDefault();
      activateDashboardTab(dashboardTabs[nextIndex], true);
    });
  });

  const stockFilter = document.getElementById("stockFilter");
  const categoryFilter = document.getElementById("categoryFilter");
  const statusButtons = document.querySelectorAll(".chip");
  const stockRows = Array.from(document.querySelectorAll("#stockTableBody tr"));
  let activeStatus = "All";

  const applyTableFilter = () => {
    const query = (stockFilter?.value || "").toLowerCase();
    stockRows.forEach((row) => {
      const name = (row.dataset.name || "").toLowerCase();
      const status = row.dataset.status || "";
      const category = row.dataset.category || "";
      const matchesText = !query || name.includes(query);
      const matchesStatus = activeStatus === "All" || status === activeStatus;
      const matchesCategory =
        !categoryFilter ||
        categoryFilter.value === "All" ||
        categoryFilter.value === category;
      const isVisible = matchesText && matchesStatus && matchesCategory;
      row.style.display = isVisible ? "" : "none";
      if (
        isVisible &&
        !motionPreference.matches &&
        typeof row.animate === "function"
      ) {
        row.animate([{ opacity: 0.72 }, { opacity: 1 }], {
          duration: 150,
          easing: "ease-out",
        });
      }
    });
  };

  if (stockFilter) {
    stockFilter.addEventListener("input", applyTableFilter);
  }
  if (categoryFilter) {
    categoryFilter.addEventListener("change", applyTableFilter);
  }

  statusButtons.forEach((button) => {
    button.addEventListener("click", () => {
      activeStatus = button.dataset.status || "All";
      statusButtons.forEach((btn) =>
        btn.classList.toggle("active", btn === button),
      );
      applyTableFilter();
    });
  });

  const quickSaleForm = document.getElementById("quick-sale-form");
  const productSelect = document.getElementById("productId");
  const quantityInput = document.getElementById("quantity");
  const updateQuantityStep = () => {
    const unit = productSelect?.selectedOptions[0]?.dataset.unit;
    if (!quantityInput) return;
    quantityInput.step = unit === "kg" ? "0.5" : "1";
    quantityInput.value =
      unit === "kg"
        ? "1"
        : String(Math.round(Number(quantityInput.value) || 1));
  };
  productSelect?.querySelectorAll("option").forEach((option, index) => {
    option.dataset.unit = index === 0 ? "pcs" : "kg";
  });
  productSelect?.addEventListener("change", updateQuantityStep);
  updateQuantityStep();
  if (quickSaleForm) {
    quickSaleForm.addEventListener("submit", async (event) => {
      event.preventDefault();
      const form = event.currentTarget;
      const formData = new FormData(form);
      const productId = formData.get("productId");
      const quantity = formData.get("quantity");

      try {
        const today = new Date().toISOString().slice(0, 10);
        const response = await fetch("/Sales/RecordSale", {
          method: "POST",
          headers: {
            RequestVerificationToken:
              formData.get("__RequestVerificationToken")?.toString() || "",
            "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8",
            "X-Requested-With": "XMLHttpRequest",
          },
          body: new URLSearchParams({
            productId: String(productId),
            quantity: String(quantity),
            date: today,
          }).toString(),
        });

        if (!response.ok) {
          const data = await response
            .json()
            .catch(() => ({ message: "Unable to record the sale." }));
          throw new Error(data.message || "Unable to record the sale.");
        }

        const toast = document.getElementById("quick-sale-toast");
        if (toast) {
          toast.textContent = "Sale recorded successfully.";
          toast.style.color = "var(--healthy)";
        }

        window.location.reload();
      } catch (error) {
        const toast = document.getElementById("quick-sale-toast");
        if (toast) {
          toast.textContent = error.message || "Sale could not be recorded.";
          toast.style.color = "var(--critical)";
        }
      }
    });
  }

  async function refreshDashboard() {
    try {
      const response = await fetch("/Dashboard/Summary", {
        headers: { "X-Requested-With": "XMLHttpRequest" },
      });
      if (!response.ok) throw new Error("summary unavailable");
      const summary = await response.json();
      if (summary && summary.activeAlerts) {
        Object.assign(state, summary);
        renderCharts();
        hideOffline();
      }
    } catch (error) {
      console.warn("Polling failed", error);
      showOffline();
    }
  }

  if (
    document.getElementById("weeklyStockChart") ||
    document.getElementById("stockTableBody")
  ) {
    animateKpiValues();
    renderCharts();
    setInterval(() => refreshDashboard(), 30000);
  }

  // Chart.js keeps a stale size after viewport changes (DevTools open/close,
  // sidebar toggling, etc.). Debounce a resize so charts reflow after layout
  // settles.
  let __chartResizeTimer;
  window.addEventListener("resize", () => {
    clearTimeout(__chartResizeTimer);
    __chartResizeTimer = setTimeout(() => {
      weeklyStockChartInstance?.resize();
      thresholdChartInstance?.resize();
      consumptionChartInstance?.resize();
      depletionChartInstance?.resize();
    }, 200);
  });
});
