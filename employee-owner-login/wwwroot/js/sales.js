document.addEventListener('DOMContentLoaded', () => {
  const units = { 'Whole Chicken': 'pcs', 'Chicken Breast': 'kg', 'Chicken Thigh': 'kg', 'Chicken Wings': 'kg', 'Chicken Feet': 'kg' };
  const product = document.getElementById('saleProduct');
  const quantity = document.getElementById('saleQuantity');
  const unit = document.getElementById('saleUnit');
  const date = document.getElementById('saleDate');
  const form = document.getElementById('saleEntryForm');
  const record = document.getElementById('recordSale');
  const toast = document.getElementById('saleToast');
  if (date) date.value = new Date().toISOString().slice(0, 10);
  const syncUnit = () => { const selectedUnit = units[product?.value] || 'pcs'; if (unit) unit.textContent = selectedUnit; if (quantity) { quantity.step = selectedUnit === 'kg' ? '0.5' : '1'; quantity.min = selectedUnit === 'kg' ? '0.5' : '1'; if (selectedUnit === 'pcs' && quantity.value) quantity.value = Math.round(Number(quantity.value)); } if (record) record.disabled = !(product?.value && quantity?.value); };
  product?.addEventListener('change', syncUnit); quantity?.addEventListener('input', syncUnit); syncUnit();
  form?.addEventListener('submit', event => { event.preventDefault(); const selectedUnit = units[product.value]; const row = document.createElement('tr'); row.innerHTML = `<td>${product.value}</td><td>${quantity.value} ${selectedUnit}</td><td>Owner</td><td>${document.getElementById('saleNotes').value || '-'}</td>`; document.getElementById('entriesBody')?.prepend(row); const count = document.getElementById('entryCount'); if (count) count.textContent = String(Number(count.textContent) + 1); toast?.classList.add('show'); setTimeout(() => toast?.classList.remove('show'), 2200); form.reset(); date.value = new Date().toISOString().slice(0, 10); syncUnit(); });
  document.getElementById('clearSale')?.addEventListener('click', () => { form.reset(); date.value = new Date().toISOString().slice(0, 10); syncUnit(); });

  const historyBody = document.getElementById('historyBody');
  if (!historyBody) return;
  const products = ['Whole Chicken', 'Chicken Breast', 'Chicken Thigh', 'Chicken Wings', 'Chicken Feet'];
  const history = Array.from({ length: 14 }, (_, index) => ({ date: new Date(Date.now() - index * 86400000).toISOString().slice(0, 10), time: `${String(9 + index % 8).padStart(2, '0')}:${index % 2 ? '20' : '45'}`, product: products[index % products.length], quantity: index % 3 ? (index + 2) * 1.5 : index + 8, user: index % 3 ? 'Maria' : 'Owner', notes: index % 4 ? 'Routine sale' : 'Restaurant pickup' }));
  const renderHistory = () => { const productFilter = document.getElementById('historyProduct').value; const userFilter = document.getElementById('historyUser').value; const from = document.getElementById('historyFrom').value; const to = document.getElementById('historyTo').value; const rows = history.filter(item => (productFilter === 'All products' || item.product === productFilter) && (userFilter === 'Everyone' || item.user === userFilter) && (!from || item.date >= from) && (!to || item.date <= to)); historyBody.innerHTML = rows.map(item => `<tr><td>${item.date}</td><td>${item.time}</td><td>${item.product}</td><td>${item.quantity} ${units[item.product]}</td><td>${item.user}</td><td>${item.notes}</td></tr>`).join('') || '<tr><td colspan="6" class="empty-state">No preview entries match these filters.</td></tr>'; document.getElementById('historyCount').textContent = `Showing ${rows.length} preview entries`; };
  document.getElementById('applyHistory')?.addEventListener('click', renderHistory); document.getElementById('resetHistory')?.addEventListener('click', () => { document.querySelectorAll('#historyFrom, #historyTo').forEach(input => input.value = ''); document.getElementById('historyProduct').value = 'All products'; document.getElementById('historyUser').value = 'Everyone'; renderHistory(); }); renderHistory();
});