document.addEventListener("DOMContentLoaded", () => {
  const input = document.getElementById("globalSearchInput");
  const trigger = document.querySelector(".global-search-trigger");
  const panel = document.getElementById("globalSearchResults");
  const status = panel?.querySelector(".global-search-status");
  const groups = panel?.querySelector(".global-search-groups");
  const suggestions = panel?.querySelector(".global-search-suggestions");
  const fromInput = document.getElementById("globalSearchFrom");
  const toInput = document.getElementById("globalSearchTo");
  if (!input || !panel || !status || !groups) return;

  const minimumLength = 2;
  let debounceTimer;
  let requestController;
  let requestNumber = 0;

  const openPanel = () => {
    panel.hidden = false;
    if (suggestions) suggestions.hidden = input.value.trim().length >= minimumLength;
    input.setAttribute("aria-expanded", "true");
  };

  const closePanel = () => {
    panel.hidden = true;
    input.setAttribute("aria-expanded", "false");
  };

  const renderResults = (data) => {
    groups.replaceChildren();
    const resultGroups = [
      ["Products", data.products || []],
      ["Stock alerts", data.alerts || []],
      ["Sales", data.sales || []],
    ];
    let resultCount = 0;

    for (const [heading, results] of resultGroups) {
      if (!results.length) continue;
      resultCount += results.length;
      const section = document.createElement("section");
      section.className = "global-search-group";
      const title = document.createElement("h2");
      title.textContent = heading;
      const list = document.createElement("ul");
      for (const result of results) {
        const item = document.createElement("li");
        const link = document.createElement("a");
        link.className = "global-search-result";
        link.href = result.href;
        const name = document.createElement("strong");
        name.textContent = result.name;
        const detail = document.createElement("span");
        if (heading === "Products") {
          detail.textContent = `${result.category} · ${Number(result.currentStock).toLocaleString()} ${result.units} in stock`;
        } else if (heading === "Stock alerts") {
          detail.textContent = `${result.status} · ${Number(result.currentStock).toLocaleString()} ${result.units} remaining`;
        } else {
          const soldDate = new Date(result.soldAt).toLocaleDateString();
          detail.textContent = `${soldDate} · ${Number(result.quantity).toLocaleString()} ${result.units}`;
        }
        link.append(name, detail);
        item.append(link);
        list.append(item);
      }
      section.append(title, list);
      groups.append(section);
    }

    status.textContent = resultCount ? "" : "No results found.";
  };

  const runSearch = async () => {
    requestController?.abort();
    const thisRequest = ++requestNumber;
    const term = input.value.trim();
    if (term.length < minimumLength) {
      groups.replaceChildren();
      status.textContent = "";
      openPanel();
      return;
    }

    if (fromInput?.value && toInput?.value && toInput.value < fromInput.value) {
      groups.replaceChildren();
      status.textContent = "The end date must be on or after the start date.";
      openPanel();
      return;
    }

    requestController = new AbortController();
    const params = new URLSearchParams({ term });
    if (fromInput?.value) params.set("from", fromInput.value);
    if (toInput?.value) params.set("to", toInput.value);
    status.textContent = "Searching…";
    groups.replaceChildren();
    openPanel();

    try {
      const response = await fetch(`/Search/Query?${params}`, {
        signal: requestController.signal,
        headers: { Accept: "application/json" },
      });
      if (!response.ok) throw new Error("Search request failed");
      const data = await response.json();
      if (thisRequest !== requestNumber) return;
      renderResults(data);
    } catch (error) {
      if (error.name === "AbortError" || thisRequest !== requestNumber) return;
      status.textContent = "Search is unavailable right now. Please try again.";
      groups.replaceChildren();
      openPanel();
    }
  };

  input.addEventListener("input", () => {
    clearTimeout(debounceTimer);
    requestController?.abort();
    requestNumber += 1;
    debounceTimer = setTimeout(runSearch, 180);
  });

  input.addEventListener("focus", () => {
    if (input.value.trim().length >= minimumLength && panel.hidden) runSearch();
  });

  trigger?.addEventListener("click", () => {
    input.focus();
    if (!panel.hidden) return;
    if (input.value.trim().length >= minimumLength) {
      runSearch();
    } else {
      openPanel();
    }
  });

  panel.addEventListener("click", (event) => {
    const chip = event.target.closest("[data-search-term]");
    if (!chip) return;
    input.value = chip.dataset.searchTerm;
    input.focus();
    clearTimeout(debounceTimer);
    runSearch();
  });

  fromInput?.addEventListener("change", runSearch);
  toInput?.addEventListener("change", runSearch);

  input.addEventListener("keydown", (event) => {
    if (event.key === "Escape") closePanel();
  });

  document.addEventListener("click", (event) => {
    if (!event.target.closest("#globalSearch")) closePanel();
  });
});
