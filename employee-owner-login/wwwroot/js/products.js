	document.addEventListener('DOMContentLoaded', () => {
	const motionPreference = window.matchMedia('(prefers-reduced-motion: reduce)');
	const overlay = document.getElementById('productModalOverlay');
	const modalBox = document.getElementById('productModalBox');
	const form = document.getElementById('productForm');
	const openButton = document.getElementById('openProductModal');
	const toast = document.getElementById('productToast');
	let escapeHandler;

	const showToast = text => {
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
		const label = document.createElement('span');
		label.textContent = text;
		toast.replaceChildren(icon, label);
		toast.classList.add('show');
		setTimeout(() => toast.classList.remove('show'), 2200);
	};

	const closeModal = () => {
		overlay.classList.remove('is-open');
		overlay.setAttribute('aria-hidden', 'true');
		form.reset();
		document.body.classList.remove('modal-open');
		escapeHandler && document.removeEventListener('keydown', escapeHandler);
		escapeHandler = null;
	};

	const openModal = () => {
		overlay.classList.add('is-open');
		overlay.setAttribute('aria-hidden', 'false');
		document.body.classList.add('modal-open');
		escapeHandler = event => { if (event.key === 'Escape') closeModal(); };
		document.addEventListener('keydown', escapeHandler);
		document.getElementById('productName').focus();
	};

	openButton.addEventListener('click', openModal);
	document.querySelectorAll('.modal-close-btn').forEach(button => button.addEventListener('click', closeModal));
	overlay.addEventListener('click', event => { if (event.target === overlay) closeModal(); });
	modalBox.addEventListener('click', event => event.stopPropagation());
	form.addEventListener('submit', event => { event.preventDefault(); showToast('Product saved (preview only)'); closeModal(); });
	document.getElementById('saveThresholds')?.addEventListener('click', () => showToast('Thresholds saved (preview only)'));
	document.querySelectorAll('.tab-btn[data-tab]').forEach(button => button.addEventListener('click', () => { document.querySelectorAll('.tab-btn[data-tab], .tab-panel').forEach(item => item.classList.remove('active')); button.classList.add('active'); document.getElementById(button.dataset.tab).classList.add('active'); }));
	const filter = () => {
		const query = document.getElementById('productSearch').value.toLowerCase();
		const category = document.getElementById('productCategory').value;
		const body = document.getElementById('productsBody');
		body.querySelectorAll('tr').forEach(row => {
			if (row.querySelector('.empty-state')) {
				row.remove();
				return;
			}
			const matches = (!query || row.dataset.name.toLowerCase().includes(query)) &&
				(category === 'All' || row.dataset.category === category);
			const wasHidden = row.hidden;
			row.hidden = !matches;
			if (matches && wasHidden && !motionPreference.matches && typeof row.animate === 'function') {
				row.animate([{ opacity: 0.72 }, { opacity: 1 }], { duration: 150, easing: 'ease-out' });
			}
		});
		const hasResults = [...body.querySelectorAll('tr')].some(row => !row.hidden);
		if (!hasResults) {
			const emptyRow = document.createElement('tr');
			const emptyCell = document.createElement('td');
			emptyCell.colSpan = 7;
			emptyCell.className = 'empty-state';
			const icon = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
			icon.setAttribute('viewBox', '0 0 24 24');
			icon.setAttribute('fill', 'none');
			icon.setAttribute('aria-hidden', 'true');
			const mark = document.createElementNS('http://www.w3.org/2000/svg', 'path');
			mark.setAttribute('d', 'M10.5 4.75a5.75 5.75 0 1 0 3.53 10.29L19 20m-11-9h5');
			mark.setAttribute('stroke', 'currentColor');
			mark.setAttribute('stroke-width', '1.6');
			mark.setAttribute('stroke-linecap', 'round');
			mark.setAttribute('stroke-linejoin', 'round');
			icon.append(mark);
			const title = document.createElement('strong');
			title.textContent = 'No products match';
			const hint = document.createElement('span');
			hint.textContent = 'Try another search or category.';
			emptyCell.append(icon, title, hint);
			emptyRow.append(emptyCell);
			body.append(emptyRow);
			if (!motionPreference.matches && typeof emptyRow.animate === 'function') {
				emptyRow.animate([{ opacity: 0 }, { opacity: 1 }], { duration: 180, easing: 'ease-out' });
			}
		}
	};
	document.getElementById('productSearch')?.addEventListener('input', filter); document.getElementById('productCategory')?.addEventListener('change', filter);
});
