(async () => {
  // ---------- fetch notifications from the server ----------
  const fetchNotifications = async () => {
    try {
      const response = await fetch("/Notifications/Recent", {
        credentials: "same-origin",
        headers: { "X-Requested-With": "XMLHttpRequest" },
      });
      if (!response.ok) return { notifications: [], unreadCount: 0 };
      const data = await response.json();
      return {
        notifications: Array.isArray(data.notifications)
          ? data.notifications
          : [],
        unreadCount:
          typeof data.unreadCount === "number" ? data.unreadCount : 0,
      };
    } catch {
      return { notifications: [], unreadCount: 0 };
    }
  };

  const { notifications, unreadCount } = await fetchNotifications();

  // ---------- helpers ----------
  const relativeTime = (timestamp) => {
    const minutes = Math.max(
      0,
      Math.floor((Date.now() - new Date(timestamp).getTime()) / 60000),
    );
    if (minutes < 1) return "Just now";
    if (minutes < 60) return `${minutes}m ago`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24) return `${hours}h ago`;
    const days = Math.floor(hours / 24);
    return days === 1 ? "Yesterday" : `${days}d ago`;
  };

  const fullTime = (timestamp) =>
    new Intl.DateTimeFormat(undefined, {
      dateStyle: "medium",
      timeStyle: "short",
    }).format(new Date(timestamp));

  const buildNotificationRow = (item, full = false) => {
    const row = document.createElement("a");
    row.href = "/Notifications";
    row.className = `notification-row notification-${item.type}${item.isRead ? " is-read" : " is-unread"}`;
    row.dataset.notificationId = item.id;

    const indicator = document.createElement("span");
    indicator.className = "notification-type-dot";
    indicator.setAttribute("aria-hidden", "true");

    const content = document.createElement("span");
    content.className = "notification-copy";

    const title = document.createElement("strong");
    title.className = "notification-title";
    title.textContent = item.title;

    const message = document.createElement("span");
    message.className = "notification-message";
    message.textContent = item.message;

    const timestamp = document.createElement("time");
    timestamp.className = "notification-time";
    timestamp.dateTime = item.timestamp;
    timestamp.textContent = full
      ? fullTime(item.timestamp)
      : relativeTime(item.timestamp);

    const unread = document.createElement("span");
    unread.className = "notification-unread-dot";
    unread.setAttribute("aria-hidden", "true");

    content.append(title, message, timestamp);
    row.append(indicator, content, unread);
    return row;
  };

  // ---------- elements ----------
  const toggle = document.getElementById("notificationToggle");
  const panel = document.getElementById("notificationPanel");
  const badge = document.getElementById("notificationBadge");
  const dropdownList = document.getElementById("notificationDropdownList");

  // ---------- render ----------
  const renderBadge = () => {
    if (!badge) return;
    badge.textContent = unreadCount > 99 ? "99+" : String(unreadCount);
    badge.hidden = unreadCount === 0;
  };

  const renderDropdown = () => {
    if (!dropdownList) return;
    dropdownList.replaceChildren();

    if (notifications.length === 0) {
      const empty = document.createElement("p");
      empty.className = "notification-empty";
      empty.textContent = "No notifications yet.";
      dropdownList.append(empty);
      return;
    }

    notifications
      .slice(0, 5)
      .forEach((item) => dropdownList.append(buildNotificationRow(item)));
  };

  const render = () => {
    renderBadge();
    renderDropdown();
  };

  // ---------- open/close the dropdown ----------
  const closePanel = () => {
    if (!panel || !toggle) return;
    panel.hidden = true;
    panel.classList.remove("is-open");
    toggle.setAttribute("aria-expanded", "false");
  };

  if (toggle && panel) {
    toggle.addEventListener("click", () => {
      const open = panel.hidden;
      panel.hidden = !open;
      panel.classList.toggle("is-open", open);
      toggle.setAttribute("aria-expanded", String(open));
    });
    document.addEventListener("click", (event) => {
      if (
        !panel.hidden &&
        !panel.contains(event.target) &&
        !toggle.contains(event.target)
      )
        closePanel();
    });
    document.addEventListener("keydown", (event) => {
      if (event.key === "Escape" && !panel.hidden) {
        closePanel();
        toggle.focus();
      }
    });
  }

  render();
})();
