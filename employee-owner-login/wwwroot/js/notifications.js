(() => {
  const storageKey = 'poultryos.notificationReadState.v1';
  const notifications = [
    { id: 'breast-critical', type: 'critical', title: 'Chicken Breast stock critical', message: '8 kg remaining, reorder 12 kg.', timestamp: new Date(Date.now() - 5 * 60000).toISOString(), isRead: false },
    { id: 'wings-warning', type: 'warning', title: 'Chicken Wings approaching reorder threshold', message: 'Current stock is 18 kg; review the 20 kg reorder threshold.', timestamp: new Date(Date.now() - 42 * 60000).toISOString(), isRead: false },
    { id: 'thigh-sale', type: 'info', title: 'Sale recorded: Chicken Thigh', message: '10 kg Chicken Thigh sold by the owner.', timestamp: new Date(Date.now() - 2 * 3600000).toISOString(), isRead: true },
    { id: 'feet-threshold', type: 'info', title: 'Chicken Feet threshold updated', message: 'The reorder threshold is now 5 kg.', timestamp: new Date(Date.now() - 5 * 3600000).toISOString(), isRead: false },
    { id: 'whole-stock', type: 'warning', title: 'Whole Chicken stock is trending down', message: '130 pcs remain after today\'s sales.', timestamp: new Date(Date.now() - 26 * 3600000).toISOString(), isRead: true },
    { id: 'thigh-forecast', type: 'info', title: 'Chicken Thigh forecast updated', message: 'Projected stock remains healthy for the next 7 days.', timestamp: new Date(Date.now() - 3 * 86400000).toISOString(), isRead: true }
  ];

  const readState = (() => {
    try {
      const saved = JSON.parse(localStorage.getItem(storageKey) || '{}');
      return saved && typeof saved === 'object' ? saved : {};
    } catch {
      return {};
    }
  })();

  notifications.forEach(item => {
    if (typeof readState[item.id] === 'boolean') item.isRead = readState[item.id];
  });

  const persistReadState = () => {
    try {
      localStorage.setItem(storageKey, JSON.stringify(Object.fromEntries(notifications.map(item => [item.id, item.isRead]))));
    } catch {
      // Keep notification state in memory when browser storage is unavailable.
    }
  };

  const unreadCount = () => notifications.filter(item => !item.isRead).length;
  const relativeTime = timestamp => {
    const minutes = Math.max(0, Math.floor((Date.now() - new Date(timestamp).getTime()) / 60000));
    if (minutes < 1) return 'Just now';
    if (minutes < 60) return `${minutes}m ago`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24) return `${hours}h ago`;
    const days = Math.floor(hours / 24);
    return days === 1 ? 'Yesterday' : `${days}d ago`;
  };

  const fullTime = timestamp => new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short'
  }).format(new Date(timestamp));

  const buildNotificationRow = (item, full = false) => {
    const row = document.createElement('button');
    row.type = 'button';
    row.className = `notification-row notification-${item.type}${item.isRead ? ' is-read' : ' is-unread'}`;
    row.dataset.notificationId = item.id;
    row.setAttribute('aria-label', `${item.title}. ${item.message}. ${item.isRead ? 'Read' : 'Unread'}`);

    const indicator = document.createElement('span');
    indicator.className = 'notification-type-dot';
    indicator.setAttribute('aria-hidden', 'true');
    const content = document.createElement('span');
    content.className = 'notification-copy';
    const title = document.createElement('strong');
    title.className = 'notification-title';
    title.textContent = item.title;
    const message = document.createElement('span');
    message.className = 'notification-message';
    message.textContent = item.message;
    const timestamp = document.createElement('time');
    timestamp.className = 'notification-time';
    timestamp.dateTime = item.timestamp;
    timestamp.textContent = full ? fullTime(item.timestamp) : relativeTime(item.timestamp);
    const unread = document.createElement('span');
    unread.className = 'notification-unread-dot';
    unread.setAttribute('aria-hidden', 'true');

    content.append(title, message, timestamp);
    row.append(indicator, content, unread);
    return row;
  };

  const toggle = document.getElementById('notificationToggle');
  const panel = document.getElementById('notificationPanel');
  const badge = document.getElementById('notificationBadge');
  const dropdownList = document.getElementById('notificationDropdownList');
  const markAllButton = document.getElementById('markAllNotificationsRead');
  const fullList = document.getElementById('notificationsPageList');
  const pageEmpty = document.getElementById('notificationsPageEmpty');
  const filterButtons = document.querySelectorAll('[data-notification-filter]');
  let activeFilter = 'all';

  const renderBadge = () => {
    if (!badge) return;
    const count = unreadCount();
    badge.textContent = count > 99 ? '99+' : String(count);
    badge.hidden = count === 0;
  };

  const renderDropdown = () => {
    if (!dropdownList) return;
    dropdownList.replaceChildren();
    if (notifications.length === 0) {
      const empty = document.createElement('p');
      empty.className = 'notification-empty';
      empty.textContent = 'No notifications yet.';
      dropdownList.append(empty);
      return;
    }
    notifications.forEach(item => dropdownList.append(buildNotificationRow(item)));
  };

  const renderFullList = () => {
    if (!fullList) return;
    const filtered = notifications.filter(item => {
      if (activeFilter === 'unread') return !item.isRead;
      return activeFilter === 'all' || item.type === activeFilter;
    });
    fullList.replaceChildren();
    filtered.forEach(item => fullList.append(buildNotificationRow(item, true)));
    if (pageEmpty) pageEmpty.hidden = filtered.length !== 0;
  };

  const render = () => {
    renderBadge();
    renderDropdown();
    renderFullList();
  };

  const markRead = id => {
    const notification = notifications.find(item => item.id === id);
    if (!notification || notification.isRead) return;
    notification.isRead = true;
    persistReadState();
    render();
  };

  const markAllRead = () => {
    notifications.forEach(item => { item.isRead = true; });
    persistReadState();
    render();
  };

  const closePanel = () => {
    if (!panel || !toggle) return;
    panel.hidden = true;
    panel.classList.remove('is-open');
    toggle.setAttribute('aria-expanded', 'false');
  };

  if (toggle && panel) {
    toggle.addEventListener('click', () => {
      const open = panel.hidden;
      panel.hidden = !open;
      panel.classList.toggle('is-open', open);
      toggle.setAttribute('aria-expanded', String(open));
    });
    document.addEventListener('click', event => {
      if (!panel.hidden && !panel.contains(event.target) && !toggle.contains(event.target)) closePanel();
    });
    document.addEventListener('keydown', event => {
      if (event.key === 'Escape' && !panel.hidden) {
        closePanel();
        toggle.focus();
      }
    });
  }

  document.addEventListener('click', event => {
    const row = event.target.closest('[data-notification-id]');
    if (row) markRead(row.dataset.notificationId);
  });
  markAllButton?.addEventListener('click', markAllRead);
  filterButtons.forEach(button => button.addEventListener('click', () => {
    activeFilter = button.dataset.notificationFilter;
    filterButtons.forEach(filter => {
      const active = filter === button;
      filter.classList.toggle('active', active);
      filter.setAttribute('aria-pressed', String(active));
    });
    renderFullList();
  }));

  if (document.body.dataset.notificationsPage === 'true') markAllRead();
  render();

  window.PoultryNotifications = {
    getNotifications: () => notifications.map(item => ({ ...item })),
    markAllRead,
    getUnreadCount: unreadCount
  };
})();