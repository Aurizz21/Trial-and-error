document.addEventListener('DOMContentLoaded', () => {
  const data = {
    'Whole Chicken': { unit: 'pcs', avg: 12, date: 'Oct 02', reorder: 80, accuracy: '96%', critical: false },
    'Chicken Breast': { unit: 'kg', avg: 15, date: 'Sep 24', reorder: 25, accuracy: '92%', critical: true },
    'Chicken Thigh': { unit: 'kg', avg: 7, date: 'Oct 08', reorder: 45, accuracy: '95%', critical: false },
    'Chicken Wings': { unit: 'kg', avg: 8.7, date: 'Sep 27', reorder: 20, accuracy: '91%', critical: false },
    'Chicken Feet': { unit: 'kg', avg: 0, date: 'Insufficient Data', reorder: 30, accuracy: '-', critical: false }
  };
  const product = document.getElementById('forecastProduct'); if (!product) return;
  const draw = () => { const item = data[product.value]; document.getElementById('avgSales').textContent = item.avg ? `${item.avg} ${item.unit}` : 'Insufficient Data'; document.getElementById('depletionDate').textContent = item.date; document.getElementById('reorderQty').textContent = `${item.reorder} ${item.unit}`; document.getElementById('accuracyText').textContent = item.accuracy; const alert = document.getElementById('forecastWarning'); alert.textContent = item.critical ? 'Stock depletion in ~1 day. Reorder 25 kg immediately.' : ''; alert.classList.toggle('hidden', !item.critical); };
  product.addEventListener('change', draw); draw();
});