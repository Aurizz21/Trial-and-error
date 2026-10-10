document.addEventListener("DOMContentLoaded", () => {
  const motionPreference = window.matchMedia(
    "(prefers-reduced-motion: reduce)",
  );
  const overlay = document.getElementById("productModalOverlay");
  const modalBox = document.getElementById("productModalBox");
  const form = document.getElementById("productForm");
  const openButton = document.getElementById("openProductModal");
  const toast = document.getElementById("productToast");
  const body = document.getElementById("productsBody");
  const saveButton = form.querySelector('button[type="submit"]');
  const unitSelect = document.getElementById("productUnit");
  let escapeHandler;

  // ---------- toast ----------
  const showToast = (text) => {
    const icon = document.createElementNS("http://www.w3.org/2000/svg", "svg");
    icon.setAttribute("viewBox", "0 0 24 24");
    icon.setAttribute("fill", "none");
    icon.setAttribute("aria-hidden", "true");
    const mark = document.createElementNS("http://www.w3.org/2000/svg", "path");
    mark.setAttribute("d", "m5 12.5 4.2 4.2L19 7");
    mark.setAttribute("stroke", "currentColor");
    mark.setAttribute("stroke-width", "2");
    mark.setAttribute("stroke-linecap", "round");
    mark.setAttribute("stroke-linejoin", "round");
    icon.append(mark);
    const label = document.createElement("span");
    label.textContent = text;
    toast.replaceChildren(icon, label);
    toast.classList.add("show");
    setTimeout(() => toast.classList.remove("show"), 2600);
  };

  const finish = (message) => {
    sessionStorage.setItem("productToast", message);
    location.reload();
  };
  const pending = sessionStorage.getItem("productToast");
  if (pending) {
    sessionStorage.removeItem("productToast");
    showToast(pending);
  }

  // ---------- talking to the server ----------
  const antiForgeryToken = () =>
    document.querySelector('input[name="__RequestVerificationToken"]')?.value ??
    "";

  const postForm = async (url, data) => {
    const response = await fetch(url, {
      method: "POST",
      credentials: "same-origin",
      headers: {
        "Content-Type": "application/x-www-form-urlencoded",
        RequestVerificationToken: antiForgeryToken(),
        "X-Requested-With": "XMLHttpRequest",
      },
      body: new URLSearchParams(data),
    });
    let result = {};
    try {
      result = await response.json();
    } catch {
      /* not JSON */
    }
    if (!response.ok || !result.success) {
      throw new Error(
        result.message || "Something went wrong. Please try again.",
      );
    }
    return result;
  };

  // ---------- product modal ----------
  const setField = (id, value) => {
    document.getElementById(id).value = value;
  };

  const closeModal = () => {
    overlay.classList.remove("is-open");
    overlay.setAttribute("aria-hidden", "true");
    form.reset();
    unitSelect.disabled = false;
    document.body.classList.remove("modal-open");
    escapeHandler && document.removeEventListener("keydown", escapeHandler);
    escapeHandler = null;
  };

  const openModal = (product) => {
    const editing = Boolean(product);
    document.getElementById("productModalTitle").textContent = editing
      ? "Edit Product"
      : "Add Product";
    document.getElementById("stockLabel").textContent = editing
      ? "Current Stock"
      : "Starting Stock";
    setField("productId", editing ? product.id : "");
    setField("productName", editing ? product.name : "");
    setField(
      "productCategoryInput",
      editing ? product.category : "Whole Chicken",
    );
    setField("productUnit", editing ? product.units : "pcs");
    setField("startingStock", editing ? product.stock : "");
    setField("reorderThreshold", editing ? product.threshold : "");
    setField("supplier", editing ? product.supplier : "");
    unitSelect.disabled = editing;

    overlay.classList.add("is-open");
    overlay.setAttribute("aria-hidden", "false");
    document.body.classList.add("modal-open");
    escapeHandler = (event) => {
      if (event.key === "Escape") closeModal();
    };
    document.addEventListener("keydown", escapeHandler);
    document.getElementById("productName").focus();
  };

  openButton.addEventListener("click", () => openModal(null));
  document
    .querySelectorAll("#productModalOverlay .modal-close-btn")
    .forEach((button) => button.addEventListener("click", closeModal));
  overlay.addEventListener("click", (event) => {
    if (event.target === overlay) closeModal();
  });
  modalBox.addEventListener("click", (event) => event.stopPropagation());

  form.addEventListener("submit", async (event) => {
    event.preventDefault();
    const id = document.getElementById("productId").value;
    const payload = {
      name: document.getElementById("productName").value.trim(),
      category: document.getElementById("productCategoryInput").value,
      units: unitSelect.value,
      supplier: document.getElementById("supplier").value.trim(),
      currentStock: document.getElementById("startingStock").value || "0",
      reorderThreshold:
        document.getElementById("reorderThreshold").value || "0",
    };
    if (id) payload.id = id;

    saveButton.disabled = true;
    try {
      const result = await postForm(
        id ? "/Products/Update" : "/Products/Create",
        payload,
      );
      finish(result.message);
    } catch (error) {
      showToast(error.message);
      saveButton.disabled = false;
    }
  });

  // ---------- Edit / Delete / Restock buttons in the table ----------
  body.addEventListener("click", async (event) => {
    const edit = event.target.closest(".edit-product-btn");
    if (edit) {
      const d = edit.dataset;
      openModal({
        id: d.id,
        name: d.name,
        category: d.category,
        units: d.units,
        supplier: d.supplier,
        stock: d.stock,
        threshold: d.threshold,
      });
      return;
    }

    const restock = event.target.closest(".restock-product-btn");
    if (restock) {
      openRestockModal({
        id: restock.dataset.id,
        name: restock.dataset.name,
        stock: restock.dataset.stock,
        units: restock.dataset.units,
      });
      return;
    }

    const del = event.target.closest(".delete-product-btn");
    if (del) {
      if (!confirm(`Delete "${del.dataset.name}"? Its sales history is kept.`))
        return;
      try {
        const result = await postForm("/Products/Delete", {
          id: del.dataset.id,
        });
        finish(result.message);
      } catch (error) {
        showToast(error.message);
      }
    }
  });

  // ---------- Restock modal ----------
  const restockOverlay = document.getElementById("restockModalOverlay");
  const restockBox = document.getElementById("restockModalBox");
  const restockForm = document.getElementById("restockForm");
  const restockSaveBtn = restockForm?.querySelector('button[type="submit"]');
  let restockEscapeHandler;

  const closeRestockModal = () => {
    restockOverlay?.classList.remove("is-open");
    restockOverlay?.setAttribute("aria-hidden", "true");
    restockForm?.reset();
    document.body.classList.remove("modal-open");
    restockEscapeHandler &&
      document.removeEventListener("keydown", restockEscapeHandler);
    restockEscapeHandler = null;
  };

  const openRestockModal = (product) => {
    document.getElementById("restockProductId").value = product.id;
    document.getElementById("restockProductName").textContent = product.name;
    document.getElementById("restockCurrentStock").textContent = product.stock;
    document.getElementById("restockUnits").textContent = product.units;
    document.getElementById("restockQuantity").value = "";
    document.getElementById("restockNotes").value = "";

    restockOverlay?.classList.add("is-open");
    restockOverlay?.setAttribute("aria-hidden", "false");
    document.body.classList.add("modal-open");
    restockEscapeHandler = (event) => {
      if (event.key === "Escape") closeRestockModal();
    };
    document.addEventListener("keydown", restockEscapeHandler);
    document.getElementById("restockQuantity").focus();
  };

  document
    .querySelectorAll("#restockModalOverlay .modal-close-btn")
    .forEach((button) => button.addEventListener("click", closeRestockModal));
  restockOverlay?.addEventListener("click", (event) => {
    if (event.target === restockOverlay) closeRestockModal();
  });
  restockBox?.addEventListener("click", (event) => event.stopPropagation());

  restockForm?.addEventListener("submit", async (event) => {
    event.preventDefault();
    const payload = {
      productId: document.getElementById("restockProductId").value,
      quantity: document.getElementById("restockQuantity").value,
      notes: document.getElementById("restockNotes").value.trim(),
    };

    restockSaveBtn.disabled = true;
    try {
      const result = await postForm("/Products/Restock", payload);
      finish(result.message);
    } catch (error) {
      showToast(error.message);
      restockSaveBtn.disabled = false;
    }
  });

  // ---------- Thresholds ----------
  document
    .getElementById("saveThresholds")
    ?.addEventListener("click", async () => {
      const data = {};
      document.querySelectorAll("[data-threshold-id]").forEach((input) => {
        data[`thresholds[${input.dataset.thresholdId}]`] = input.value || "0";
      });
      try {
        const result = await postForm("/Products/UpdateThresholds", data);
        finish(result.message);
      } catch (error) {
        showToast(error.message);
      }
    });

  // ---------- tabs ----------
  document.querySelectorAll(".tab-btn[data-tab]").forEach((button) =>
    button.addEventListener("click", () => {
      document
        .querySelectorAll(".tab-btn[data-tab], .tab-panel")
        .forEach((item) => item.classList.remove("active"));
      button.classList.add("active");
      document.getElementById(button.dataset.tab).classList.add("active");
    }),
  );

  // ---------- search + category filter ----------
  const filter = () => {
    const query = document.getElementById("productSearch").value.toLowerCase();
    const category = document.getElementById("productCategory").value;
    body.querySelectorAll("tr").forEach((row) => {
      if (row.querySelector(".empty-state")) {
        row.remove();
        return;
      }
      const matches =
        (!query || row.dataset.name.toLowerCase().includes(query)) &&
        (category === "All" || row.dataset.category === category);
      const wasHidden = row.hidden;
      row.hidden = !matches;
      if (
        matches &&
        wasHidden &&
        !motionPreference.matches &&
        typeof row.animate === "function"
      ) {
        row.animate([{ opacity: 0.72 }, { opacity: 1 }], {
          duration: 150,
          easing: "ease-out",
        });
      }
    });
    const hasResults = [...body.querySelectorAll("tr")].some(
      (row) => !row.hidden,
    );
    if (!hasResults) {
      const emptyRow = document.createElement("tr");
      const emptyCell = document.createElement("td");
      emptyCell.colSpan = 7;
      emptyCell.className = "empty-state";
      const title = document.createElement("strong");
      title.textContent = "No products match";
      const hint = document.createElement("span");
      hint.textContent = "Try another search or category.";
      emptyCell.append(title, hint);
      emptyRow.append(emptyCell);
      body.append(emptyRow);
    }
  };
  document.getElementById("productSearch")?.addEventListener("input", filter);
  document
    .getElementById("productCategory")
    ?.addEventListener("change", filter);
});
