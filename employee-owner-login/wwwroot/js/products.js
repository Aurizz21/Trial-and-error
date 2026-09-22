document.addEventListener('DOMContentLoaded', () => {
	const overlay = document.getElementById('productModalOverlay');
	const modalBox = document.getElementById('productModalBox');
	const form = document.getElementById('productForm');
	const openButton = document.getElementById('openProductModal');
	const toast = document.getElementById('productToast');
	let escapeHandler;

	const showToast = text => {
		toast.textContent = text;
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
	const filter = () => { const query = document.getElementById('productSearch').value.toLowerCase(); const category = document.getElementById('productCategory').value; document.querySelectorAll('#productsBody tr').forEach(row => { row.hidden = !((!query || row.dataset.name.toLowerCase().includes(query)) && (category === 'All' || row.dataset.category === category)); }); };
	document.getElementById('productSearch')?.addEventListener('input', filter); document.getElementById('productCategory')?.addEventListener('change', filter);
});