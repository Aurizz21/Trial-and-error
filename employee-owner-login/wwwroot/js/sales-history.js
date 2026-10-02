document.addEventListener("DOMContentLoaded", () => {
  // ---------- page elements ----------
  const body = document.getElementById("historyBody");
  const countEl = document.getElementById("historyCount");
  const pageInfoEl = document.getElementById("historyPageInfo");
  const prevBtn = document.getElementById("historyPrev");
  const nextBtn = document.getElementById("historyNext");
  const fromInput = document.getElementById("historyFrom");
  const toInput = document.getElementById("historyTo");
  const productSelect = document.getElementById("historyProduct");
  const userSelect = document.getElementById("historyUser");
  const applyBtn = document.getElementById("applyHistory");
  const resetBtn = document.getElementById("resetHistory");
  const toast = document.getElementById("historyToast");

  if (!body) return; // not on the History page

  // ---------- state ----------
  let currentPage = 1;
  let totalPages = 1;
  let dropdownsLoaded = false;

  // ---------- toast ----------
  const showToast = (text) => {
    if (!toast) return;
    toast.textContent = text;
    toast.classList.add("show");
    setTimeout(() => toast.classList.remove("show"), 2600);
  };

  // ---------- helpers ----------
  const escapeHtml = (value) =>
    String(value ?? "")
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#39;");

  const formatNumber = (n) => Number(n ?? 0).toLocaleString();

  const formatQuantity = (qty, units) => {
    const n = Number(qty ?? 0);
    return `${n.toFixed(2).replace(/\.00$/, "")} ${escapeHtml(units)}`;
  };

  // ---------- fetch one page ----------
  const loadPage = async (page = 1) => {
    const params = new URLSearchParams();
    if (fromInput.value) params.set("from", fromInput.value);
    if (toInput.value) params.set("to", toInput.value);
    if (productSelect.value) params.set("productId", productSelect.value);
    if (userSelect.value) params.set("enteredBy", userSelect.value);
    params.set("page", page);
    params.set("pageSize", 50);

    body.innerHTML =
      '<tr><td colspan="6" class="empty-state"><strong>Loading…</strong></td></tr>';

    try {
      const response = await fetch(`/Sales/HistoryData?${params.toString()}`, {
        credentials: "same-origin",
        headers: { "X-Requested-With": "XMLHttpRequest" },
      });

      if (!response.ok) throw new Error(`Request failed (${response.status})`);
      const data = await response.json();
      if (!data.success) throw new Error("Server returned an error.");

      renderRows(data.rows);
      renderPagination(data);
      populateDropdowns(data);
    } catch (error) {
      body.innerHTML = `<tr><td colspan="6" class="empty-state"><strong>Could not load sales</strong><span>${escapeHtml(error.message)}</span></td></tr>`;
      showToast(error.message);
    }
  };

  // ---------- render table rows ----------
  const renderRows = (rows) => {
    if (!rows || rows.length === 0) {
      body.innerHTML =
        '<tr><td colspan="6" class="empty-state"><strong>No sales match your filters</strong><span>Try widening the date range or clearing filters.</span></td></tr>';
      return;
    }

    body.innerHTML = rows
      .map(
        (r) => `
            <tr>
                <td>${escapeHtml(r.soldAt)}</td>
                <td>${escapeHtml(r.time)}</td>
                <td>${escapeHtml(r.productName)}</td>
                <td>${formatQuantity(r.quantity, r.units)}</td>
                <td>${escapeHtml(r.enteredBy)}</td>
                <td>${r.notes ? escapeHtml(r.notes) : '<span class="muted">—</span>'}</td>
            </tr>
        `,
      )
      .join("");
  };

  // ---------- render pagination ----------
  const renderPagination = (data) => {
    currentPage = data.page;
    totalPages = data.totalPages || 1;

    const start =
      data.totalCount === 0 ? 0 : (data.page - 1) * data.pageSize + 1;
    const end = Math.min(data.page * data.pageSize, data.totalCount);

    countEl.textContent =
      data.totalCount === 0
        ? "No entries"
        : `Showing ${formatNumber(start)}–${formatNumber(end)} of ${formatNumber(data.totalCount)}`;

    pageInfoEl.textContent = `Page ${data.page} of ${totalPages}`;
    prevBtn.disabled = data.page <= 1;
    nextBtn.disabled = data.page >= totalPages;
  };

  // ---------- populate filter dropdowns (once) ----------
  const populateDropdowns = (data) => {
    if (dropdownsLoaded) return;
    if (data.products) {
      data.products.forEach((p) => {
        const opt = document.createElement("option");
        opt.value = p.id;
        opt.textContent = p.name;
        productSelect.append(opt);
      });
    }
    if (data.users) {
      data.users.forEach((u) => {
        const opt = document.createElement("option");
        opt.value = u;
        opt.textContent = u;
        userSelect.append(opt);
      });
    }
    dropdownsLoaded = true;
  };

  // ---------- event handlers ----------
  applyBtn.addEventListener("click", () => loadPage(1));

  resetBtn.addEventListener("click", () => {
    fromInput.value = "";
    toInput.value = "";
    productSelect.value = "";
    userSelect.value = "";
    loadPage(1);
  });

  prevBtn.addEventListener("click", () => {
    if (currentPage > 1) loadPage(currentPage - 1);
  });

  nextBtn.addEventListener("click", () => {
    if (currentPage < totalPages) loadPage(currentPage + 1);
  });

  // Enter key in the date inputs triggers Apply
  [fromInput, toInput].forEach((el) => {
    el?.addEventListener("keydown", (event) => {
      if (event.key === "Enter") loadPage(1);
    });
  });

  // ---------- initial load ----------
  loadPage(1);
});
