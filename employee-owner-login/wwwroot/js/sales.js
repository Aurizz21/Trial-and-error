document.addEventListener('DOMContentLoaded', () => {
  const units = { 'Whole Chicken': 'pcs', 'Chicken Breast': 'kg', 'Chicken Thigh': 'kg', 'Chicken Wings': 'kg', 'Chicken Feet': 'kg' };
  const product = document.getElementById('saleProduct');
  const quantity = document.getElementById('saleQuantity');
  const unit = document.getElementById('saleUnit');
  const date = document.getElementById('saleDate');
  const form = document.getElementById('saleEntryForm');
  const record = document.getElementById('recordSale');
  const toast = document.getElementById('saleToast');
  const quantityError = document.getElementById('quantityError');
  const motionPreference = window.matchMedia('(prefers-reduced-motion: reduce)');
  let isSubmitting = false;
  let toastTimer;
  const localDate = () => {
    const now = new Date();
    return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`;
  };
  const selectedUnit = () => product?.selectedOptions[0]?.dataset.unit || 'pcs';
  const syncUnit = () => {
    const currentUnit = selectedUnit();
    if (unit) unit.textContent = currentUnit;
    if (quantity) {
      quantity.step = currentUnit === 'kg' ? '0.5' : '1';
      quantity.min = currentUnit === 'kg' ? '0.5' : '1';
      if (currentUnit === 'pcs' && quantity.value) quantity.value = Math.round(Number(quantity.value));
    }
    if (record) record.disabled = isSubmitting || !(product?.value && quantity?.value);
  };
  const showToast = (message, success) => {
    if (!toast) return;
    toast.replaceChildren();
    if (success) {
      const icon = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
      icon.setAttribute('viewBox', '0 0 24 24');
      icon.setAttribute('fill', 'none');
      icon.setAttribute('aria-hidden', 'true');
      const mark = document.createElementNS('http://www.w3.org/2000/svg', 'path');
      mark.setAttribute('d', 'm5 12.5 4.2 4.2L19 7');
      mark.setAttribute('stroke', 'currentColor');
      mark.setAttribute('stroke-width', '2');
      mark.setAttribute('stroke-linecap', 'round');
      mark.setAttribute('stroke-linejoin', 'round');
      icon.append(mark);
      toast.append(icon);
    }
    const label = document.createElement('span');
    label.textContent = message;
    toast.append(label);
    toast.style.backgroundColor = success ? 'var(--healthy)' : 'var(--critical)';
    toast.classList.add('show');
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => toast.classList.remove('show'), 3000);
  };
  const setQuantityError = message => {
    if (!quantityError) return;
    quantityError.textContent = message;
    quantityError.hidden = !message;
    quantityError.style.color = 'var(--critical)';
  };
  const appendTextCell = (row, value) => {
    const cell = document.createElement('td');
    cell.textContent = value;
    row.append(cell);
    return cell;
  };
  const renderEntry = entry => {
    const body = document.getElementById('entriesBody');
    if (!body) return;
    if (body.querySelector('.empty-state')) body.replaceChildren();
    const row = document.createElement('tr');
    const productCell = appendTextCell(row, entry.productName);
    const time = document.createElement('small');
    time.textContent = new Date(entry.date).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' });
    productCell.append(time);
    appendTextCell(row, `${entry.quantity} ${entry.unit}`);
    appendTextCell(row, entry.enteredBy);
    appendTextCell(row, entry.notes || '-');
    body.prepend(row);
    if (!motionPreference.matches && typeof row.animate === 'function') {
      row.animate([{ opacity: 0 }, { opacity: 1 }], { duration: 180, easing: 'ease-out' });
    }
  };
  const renderSummary = summary => {
    const total = document.getElementById('todaysTotalUnits');
    const entriesCount = document.getElementById('entryCount');
    const productsAffected = document.getElementById('productsAffected');
    const entriesLabel = document.getElementById('entriesCount');
    if (total) total.textContent = summary.totalUnitsSold;
    if (entriesCount) entriesCount.textContent = String(summary.entriesCount);
    if (productsAffected) productsAffected.textContent = `${summary.productsAffectedCount} of ${summary.totalProducts}`;
    if (entriesLabel) entriesLabel.textContent = `${summary.entriesCount} entries`;
  };
  const renderLowStock = products => {
    const list = document.getElementById('lowStockList');
    if (!list) return;
    list.replaceChildren();
    if (!products.length) {
      const empty = document.createElement('p');
      empty.className = 'empty-state';
      empty.textContent = 'No products currently need attention.';
      list.append(empty);
      return;
    }
    products.forEach(item => {
      const row = document.createElement('div');
      row.className = 'mini-row';
      const details = document.createElement('div');
      const name = document.createElement('strong');
      name.textContent = item.productName;
      const remaining = document.createElement('small');
      remaining.textContent = `Remaining stock: ${item.currentStock} ${item.units}`;
      details.append(name, remaining);
      const status = document.createElement('span');
      status.className = `status-badge status-${item.status.toLowerCase()}`;
      status.textContent = item.status;
      row.append(details, status);
      list.append(row);
    });
  };
  const resetForm = () => {
    form?.reset();
    if (date) date.value = localDate();
    setQuantityError('');
    syncUnit();
  };
  if (date && !date.value) date.value = localDate();
  product?.addEventListener('change', () => { setQuantityError(''); syncUnit(); });
  quantity?.addEventListener('input', () => { setQuantityError(''); syncUnit(); });
  syncUnit();
  form?.addEventListener('submit', async event => {
    event.preventDefault();
    if (isSubmitting || !form.reportValidity()) return;
    isSubmitting = true;
    const originalLabel = record?.textContent || 'Record Sale';
    if (record) record.textContent = 'Recording...';
    record?.setAttribute('aria-busy', 'true');
    setQuantityError('');
    syncUnit();
    try {
      const formData = new FormData(form);
      const token = formData.get('__RequestVerificationToken')?.toString() || '';
      const response = await fetch('/Sales/RecordSale', {
        method: 'POST',
        credentials: 'same-origin',
        headers: {
          'RequestVerificationToken': token,
          'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8'
        },
        body: new URLSearchParams(formData).toString()
      });
      const data = await response.json().catch(() => ({ success: false, message: 'Unable to record the sale.' }));
      if (!response.ok || !data.success) {
        if (data.message?.toLowerCase().includes('exceeds current stock')) setQuantityError(data.message);
        showToast(data.message || 'Sale could not be recorded.', false);
        return;
      }
      renderEntry(data.newEntry);
      renderSummary(data.todaysSummary);
      renderLowStock(data.lowStockProducts);
      const productName = data.newEntry.productName;
      showToast(`Sale recorded: ${data.newEntry.quantity}${data.newEntry.unit} ${productName}`, true);
      resetForm();
    } catch (error) {
      showToast(error.message || 'Sale could not be recorded.', false);
    } finally {
      isSubmitting = false;
      if (record) record.textContent = originalLabel;
      record?.removeAttribute('aria-busy');
      syncUnit();
    }
  });
  document.getElementById('clearSale')?.addEventListener('click', resetForm);

  const historyBody = document.getElementById('historyBody');
  if (!historyBody) return;
  const products = ['Whole Chicken', 'Chicken Breast', 'Chicken Thigh', 'Chicken Wings', 'Chicken Feet'];
  const history = Array.from({ length: 14 }, (_, index) => ({ date: new Date(Date.now() - index * 86400000).toISOString().slice(0, 10), time: `${String(9 + index % 8).padStart(2, '0')}:${index % 2 ? '20' : '45'}`, product: products[index % products.length], quantity: index % 3 ? (index + 2) * 1.5 : index + 8, user: index % 3 ? 'Maria' : 'Owner', notes: index % 4 ? 'Routine sale' : 'Restaurant pickup' }));
  const historyMotionPreference = window.matchMedia('(prefers-reduced-motion: reduce)');
  const renderHistory = (animateChanges = false) => {
    const productFilter = document.getElementById('historyProduct').value;
    const userFilter = document.getElementById('historyUser').value;
    const from = document.getElementById('historyFrom').value;
    const to = document.getElementById('historyTo').value;
    const rows = history.filter(item =>
      (productFilter === 'All products' || item.product === productFilter) &&
      (userFilter === 'Everyone' || item.user === userFilter) &&
      (!from || item.date >= from) && (!to || item.date <= to));
    historyBody.innerHTML = rows.map(item => `<tr><td>${item.date}</td><td>${item.time}</td><td>${item.product}</td><td>${item.quantity} ${units[item.product]}</td><td>${item.user}</td><td>${item.notes}</td></tr>`).join('') || '<tr><td colspan="6" class="empty-state"><svg viewBox="0 0 24 24" fill="none" aria-hidden="true"><circle cx="10.5" cy="10.5" r="5.75" stroke="currentColor" stroke-width="1.6"/><path d="m15 15 4 4M8 10.5h5" stroke="currentColor" stroke-width="1.6" stroke-linecap="round"/></svg><strong>No preview entries match these filters</strong><span>Try a wider date range or clear a filter.</span></td></tr>';
    document.getElementById('historyCount').textContent = `Showing ${rows.length} preview entries`;
    if (animateChanges && !historyMotionPreference.matches && typeof historyBody.animate === 'function') {
      historyBody.animate([{ opacity: 0.55 }, { opacity: 1 }], { duration: 180, easing: 'ease-out' });
    }
  };
  document.getElementById('applyHistory')?.addEventListener('click', () => renderHistory(true));
  document.getElementById('resetHistory')?.addEventListener('click', () => {
    document.querySelectorAll('#historyFrom, #historyTo').forEach(input => input.value = '');
    document.getElementById('historyProduct').value = 'All products';
    document.getElementById('historyUser').value = 'Everyone';
    renderHistory(true);
  });
  renderHistory();
});
