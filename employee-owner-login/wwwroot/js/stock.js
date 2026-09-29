document.addEventListener('DOMContentLoaded', () => {
  const motionPreference = window.matchMedia('(prefers-reduced-motion: reduce)');
  const search = document.getElementById('stockSearch');
  const category = document.getElementById('stockCategory');
  const rows = [...document.querySelectorAll('#stockRows tr')];
  const chips = [...document.querySelectorAll('.status-filters .chip')];
  let status = 'All';

  const filter = () => {
    const query = search.value.toLowerCase();
    rows.forEach(row => {
      const visible = (!query || row.dataset.name.toLowerCase().includes(query)) &&
        (category.value === 'All' || row.dataset.category === category.value) &&
        (status === 'All' || row.dataset.status === status);
      const wasHidden = row.hidden;
      row.hidden = !visible;
      if (visible && wasHidden && !motionPreference.matches && typeof row.animate === 'function') {
        row.animate([{ opacity: 0.72 }, { opacity: 1 }], { duration: 150, easing: 'ease-out' });
      }
    });
  };

  search?.addEventListener('input', filter);
  category?.addEventListener('change', filter);
  chips.forEach(chip => chip.addEventListener('click', () => {
    status = chip.dataset.status;
    chips.forEach(item => item.classList.toggle('active', item === chip));
    filter();
  }));
});
