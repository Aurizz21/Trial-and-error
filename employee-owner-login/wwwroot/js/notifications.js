(() => {
  const currentRole = document.body.dataset.role || 'Owner';
  const notifications = [];

  const getAntiForgeryToken = () => {
    const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
    return tokenInput ? tokenInput.value : '';
  };

  const requestJson = async (url, method = 'GET', body) => {
    const headers = { 'X-Requested-With': 'XMLHttpRequest' };

    if (method !== 'GET') {
      headers['Content-Type'] = 'application/json';
      headers['RequestVerificationToken'] = getAntiForgeryToken();
    }

    const response = await fetch(url, {
      method,
      headers,
      credentials: 'same-origin',
      body: body ? JSON.stringify(body) : undefined
    });

    if (!response.ok) {
      throw new Error(`Request failed: ${response.status}`);
    }

    return response.json();
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

  const fetchNotifications = async () => {
    try {
      const response = await requestJson('/Notifications/List');
      notifications.splice(0, notifications.length, ...response.map(item => ({ ...item, type: (item.type || 'info').toLowerCase() })));
      render();
    } catch (error) {
      console.error('Unable to load notifications from the server.', error);
    }
  };

  const markRead = async id => {
    const notification = notifications.find(item => item.id === id);
    if (!notification || notification.isRead) return;

    try {
      await requestJson('/Notifications/MarkRead', 'POST', { notificationId: id });
      notification.isRead = true;
      render();
    } catch (error) {
      console.error('Unable to mark notification as read.', error);
    }
  };

  const markAllRead = async () => {
    try {
      await requestJson('/Notifications/MarkAllRead', 'POST', {});
      notifications.forEach(item => { item.isRead = true; });
      render();
    } catch (error) {
      console.error('Unable to mark notifications as read.', error);
    }
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
  markAllButton?.addEventListener('click', () => markAllRead());
  filterButtons.forEach(button => button.addEventListener('click', () => {
    activeFilter = button.dataset.notificationFilter;
    filterButtons.forEach(filter => {
      const active = filter === button;
      filter.classList.toggle('active', active);
      filter.setAttribute('aria-pressed', String(active));
    });
    renderFullList();
  }));

  if (document.body.dataset.notificationsPage === 'true') {
    markAllRead();
  }

  fetchNotifications();

  window.PoultryNotifications = {
    getNotifications: () => notifications.map(item => ({ ...item })),
    markAllRead,
    getUnreadCount: unreadCount
  };
})();