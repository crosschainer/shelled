import shellBridge from '../bridge/api.js';
import { getDesktopPositions, moveDesktopSelection, sortItemsByLayout } from '../utils/desktopLayout.js';

export function wireInteractions(selectors, store) {
  selectors.launcherToggle.addEventListener('click', () => store.toggleLauncher());
  selectors.launcherClose.addEventListener('click', () => store.toggleLauncher(false));
  selectors.launcherOverlay.addEventListener('click', (event) => {
    if (event.target === selectors.launcherOverlay) {
      store.toggleLauncher(false);
    }
  });

  selectors.workspaceStrip.addEventListener('click', async (event) => {
    const target = event.target.closest('[data-workspace-id]');
    if (!target) return;
    
    const workspaceId = target.dataset.workspaceId;
    
    try {
      // Use bridge API to switch workspace
      const success = await shellBridge.switchWorkspace(workspaceId);
      if (success) {
        // The workspace switch will be handled by the event system
        console.log('Workspace switched to:', workspaceId);
      } else {
        console.error('Failed to switch workspace:', workspaceId);
        // Fallback to local state update
        store.setActiveWorkspace(workspaceId);
      }
    } catch (error) {
      console.error('Error switching workspace:', error);
      // Fallback to local state update
      store.setActiveWorkspace(workspaceId);
    }
  });

  async function focusWindowByHandle(hwnd) {
    const state = store.getState();
    const window = state.windows.find((w) => w.hwnd === hwnd);

    if (!window) return;

    try {
      if (window.state === 'minimized') {
        await shellBridge.restoreWindow(hwnd);
      }

      const success = await shellBridge.focusWindow(hwnd);
      if (success) {
        console.log('Window focused:', hwnd);
      } else {
        console.error('Failed to focus window:', hwnd);
        store.setFocusedWindow(hwnd);
      }
    } catch (error) {
      console.error('Error focusing window:', error);
      store.setFocusedWindow(hwnd);
    }
  }

  let taskbarPreviewMenu = null;
  let taskbarPreviewAnchor = null;
  let taskbarPreviewHideTimeoutId = null;

  function removeTaskbarPreviewMenu() {
    if (taskbarPreviewMenu) {
      taskbarPreviewMenu.remove();
      taskbarPreviewMenu = null;
      taskbarPreviewAnchor = null;
    }
    if (taskbarPreviewHideTimeoutId) {
      clearTimeout(taskbarPreviewHideTimeoutId);
      taskbarPreviewHideTimeoutId = null;
    }
  }

  function scheduleTaskbarPreviewHide() {
    if (taskbarPreviewHideTimeoutId) {
      clearTimeout(taskbarPreviewHideTimeoutId);
    }
    taskbarPreviewHideTimeoutId = setTimeout(() => {
      removeTaskbarPreviewMenu();
    }, 150);
  }

  function showTaskbarPreviewMenuForApp(anchorElement, appId) {
    const state = store.getState();
    const { windows, activeWorkspaceId } = state;
    const groupKey = anchorElement.dataset.groupKey;

    const groupWindows = windows.filter((w) => {
      if (w.workspaceId !== activeWorkspaceId || w.isVisible === false) {
        return false;
      }
      if (appId && w.appId === appId) {
        return true;
      }
      if (!appId && groupKey && w.hwnd === groupKey) {
        return true;
      }
      return false;
    });

    if (groupWindows.length <= 1) {
      removeTaskbarPreviewMenu();
      return;
    }

    removeTaskbarPreviewMenu();

    const menu = document.createElement('div');
    menu.className = 'taskbar-preview-menu';

    const list = document.createElement('div');
    list.className = 'taskbar-preview-list';

    groupWindows.forEach((windowModel) => {
      const itemButton = document.createElement('button');
      itemButton.type = 'button';
      itemButton.className = 'taskbar-preview-item';
      itemButton.dataset.hwnd = windowModel.hwnd;
      const title = windowModel.title || windowModel.appId || 'Window';
      const stateLabel = windowModel.state || '';

      itemButton.innerHTML = `
        <span class="taskbar-preview-item__title">${title}</span>
        <span class="taskbar-preview-item__meta">${stateLabel}</span>
      `;

      itemButton.addEventListener('click', async () => {
        removeTaskbarPreviewMenu();
        await focusWindowByHandle(windowModel.hwnd);
      });

      list.appendChild(itemButton);
    });

    menu.appendChild(list);
    document.body.appendChild(menu);

    const anchorRect = anchorElement.getBoundingClientRect();
    const menuRect = menu.getBoundingClientRect();

    const viewportWidth = window.innerWidth;
    const viewportHeight = window.innerHeight;

    let left = anchorRect.left + anchorRect.width / 2 - menuRect.width / 2;
    if (left < 8) {
      left = 8;
    }
    if (left + menuRect.width > viewportWidth - 8) {
      left = viewportWidth - menuRect.width - 8;
    }

    menu.style.position = 'fixed';
    menu.style.left = `${left}px`;

    const taskbarRect =
      selectors.taskbar && selectors.taskbar.getBoundingClientRect
        ? selectors.taskbar.getBoundingClientRect()
        : { top: viewportHeight, height: 0 };

    let bottom = viewportHeight - taskbarRect.top + 8;
    if (bottom < 8) {
      bottom = 8;
    }

    menu.style.bottom = `${bottom}px`;
    menu.style.top = '';

    menu.addEventListener('mouseenter', () => {
      if (taskbarPreviewHideTimeoutId) {
        clearTimeout(taskbarPreviewHideTimeoutId);
        taskbarPreviewHideTimeoutId = null;
      }
    });

    menu.addEventListener('mouseleave', () => {
      scheduleTaskbarPreviewHide();
    });

    taskbarPreviewMenu = menu;
    taskbarPreviewAnchor = anchorElement;
  }

  selectors.taskbarWindows.addEventListener('click', async (event) => {
    const button = event.target.closest('.taskbar-item');
    if (!button) return;

    removeTaskbarPreviewMenu();

    const appId = button.dataset.appId;
    const groupKey = button.dataset.groupKey;
    const state = store.getState();
    const { windows, activeWorkspaceId, focusedWindowHandle } = state;

    const groupWindows = windows.filter((w) => {
      if (w.workspaceId !== activeWorkspaceId || w.isVisible === false) {
        return false;
      }
      if (appId && w.appId === appId) {
        return true;
      }
      if (!appId && groupKey && w.hwnd === groupKey) {
        return true;
      }
      return false;
    });

    if (groupWindows.length === 0) {
      return;
    }

    let targetWindow = groupWindows.find((w) => w.hwnd === focusedWindowHandle);
    if (!targetWindow) {
      targetWindow = groupWindows[0];
    }

    await focusWindowByHandle(targetWindow.hwnd);
  });

  selectors.taskbarWindows.addEventListener('contextmenu', (event) => {
    const button = event.target.closest('.taskbar-item');
    if (!button) return;

    event.preventDefault();
    const state = store.getState();
    const { windows, activeWorkspaceId, focusedWindowHandle } = state;
    const appId = button.dataset.appId;
    const groupKey = button.dataset.groupKey;

    const groupWindows = windows.filter((w) => {
      if (w.workspaceId !== activeWorkspaceId || w.isVisible === false) {
        return false;
      }
      if (appId && w.appId === appId) {
        return true;
      }
      if (!appId && groupKey && w.hwnd === groupKey) {
        return true;
      }
      return false;
    });

    if (groupWindows.length === 0) {
      return;
    }

    let targetWindow = groupWindows.find((w) => w.hwnd === focusedWindowHandle);
    if (!targetWindow) {
      targetWindow = groupWindows[0];
    }

    showTaskbarContextMenu(event, targetWindow.hwnd, targetWindow, store);
  });

  selectors.taskbarWindows.addEventListener('mouseover', (event) => {
    const button = event.target.closest('.taskbar-item');
    if (!button || !selectors.taskbarWindows.contains(button)) {
      return;
    }

    if (taskbarPreviewAnchor === button) {
      return;
    }

    const appId = button.dataset.appId;
    showTaskbarPreviewMenuForApp(button, appId);
  });

  selectors.taskbarWindows.addEventListener('mouseout', (event) => {
    if (!taskbarPreviewMenu || !taskbarPreviewAnchor) {
      return;
    }

    const related = event.relatedTarget;
    if (
      related &&
      (taskbarPreviewAnchor.contains(related) || taskbarPreviewMenu.contains(related))
    ) {
      return;
    }

    scheduleTaskbarPreviewHide();
  });

  selectors.trayIcons.addEventListener('click', async (event) => {
    const target = event.target.closest('[data-tray-id]');
    if (!target) return;
    
    const trayId = target.dataset.trayId;
    
    try {
      // Use bridge API to handle tray icon click
      const success = await shellBridge.trayIconClick(trayId, 'left');
      if (success) {
        console.log('Tray icon clicked:', trayId);
      } else {
        console.error('Failed to handle tray icon click:', trayId);
      }
    } catch (error) {
      console.error('Error handling tray icon click:', error);
    }
  });

  // Add right-click support for tray icons
  selectors.trayIcons.addEventListener('contextmenu', async (event) => {
    const target = event.target.closest('[data-tray-id]');
    if (!target) return;
    
    event.preventDefault();
    const trayId = target.dataset.trayId;
    
    try {
      // Use bridge API to handle tray icon right-click
      const success = await shellBridge.trayIconClick(trayId, 'right');
      if (success) {
        console.log('Tray icon right-clicked:', trayId);
      } else {
        console.error('Failed to handle tray icon right-click:', trayId);
      }
    } catch (error) {
      console.error('Error handling tray icon right-click:', error);
    }
  });

  const volumeIndicator = selectors.volumeIndicator;
  const volumeFlyout = selectors.volumeFlyout;
  const volumeSlider = selectors.volumeSlider;
  const volumeMuteToggle = selectors.volumeMuteToggle;
  const volumePercentLabel = selectors.volumePercentLabel;

  let isVolumeFlyoutOpen = false;

  const closeVolumeFlyout = () => {
    if (!volumeFlyout || !isVolumeFlyoutOpen) {
      return;
    }

    volumeFlyout.classList.remove('open');
    volumeFlyout.setAttribute('aria-hidden', 'true');
    isVolumeFlyoutOpen = false;
  };

  const openVolumeFlyout = () => {
    if (!volumeFlyout) {
      return;
    }

    volumeFlyout.classList.add('open');
    volumeFlyout.setAttribute('aria-hidden', 'false');
    isVolumeFlyoutOpen = true;
  };

  if (volumeIndicator && volumeFlyout) {
    volumeIndicator.addEventListener('click', (event) => {
      event.preventDefault();
      event.stopPropagation();

      if (isVolumeFlyoutOpen) {
        closeVolumeFlyout();
      } else {
        openVolumeFlyout();
      }
    });

    document.addEventListener('click', (event) => {
      if (!isVolumeFlyoutOpen) {
        return;
      }

      const target = event.target;
      if (
        (volumeIndicator && volumeIndicator.contains(target)) ||
        (volumeFlyout && volumeFlyout.contains(target))
      ) {
        return;
      }

      closeVolumeFlyout();
    });
  }

  if (volumeSlider) {
    volumeSlider.addEventListener('input', (event) => {
      const value = Number(event.target.value);
      if (Number.isNaN(value)) {
        return;
      }

      const clamped = Math.max(0, Math.min(100, value));

      if (volumePercentLabel) {
        volumePercentLabel.textContent = `${clamped}%`;
      }

      shellBridge
        .setSystemVolume(clamped)
        .catch((error) =>
          console.error('Error setting system volume from slider:', error),
        );
    });
  }

  if (volumeMuteToggle) {
    volumeMuteToggle.addEventListener('click', async (event) => {
      event.preventDefault();

      try {
        const ok = await shellBridge.toggleSystemMute();
        if (!ok) {
          return;
        }

        const status = await shellBridge.getSystemStatus();
        if (!status || !status.volume) {
          return;
        }

        const { levelPercent, isMuted } = status.volume;
        const safeLevel =
          typeof levelPercent === 'number' && !Number.isNaN(levelPercent)
            ? Math.max(0, Math.min(100, levelPercent))
            : 0;

        if (volumeSlider) {
          volumeSlider.value = String(safeLevel);
        }

        if (volumePercentLabel) {
          volumePercentLabel.textContent = `${safeLevel}%`;
        }

        if (selectors.volumeIndicator) {
          const baseClass = 'system-indicator system-indicator--volume';
          const variant = isMuted
            ? ' system-indicator--volume-muted'
            : ' system-indicator--volume-unmuted';
          selectors.volumeIndicator.className = baseClass + variant;

          const label = isMuted ? 'Volume muted' : `Volume ${safeLevel}%`;
          selectors.volumeIndicator.setAttribute('aria-label', label);
          selectors.volumeIndicator.title = label;
        }

        if (volumeMuteToggle) {
          volumeMuteToggle.classList.toggle('volume-flyout__mute--muted', !!isMuted);
        }
      } catch (error) {
        console.error('Error toggling system mute from volume flyout:', error);
      }
    });
  }

  if (selectors.networkIndicator) {
    selectors.networkIndicator.addEventListener('click', async (event) => {
      event.preventDefault();

      let status = null;
      try {
        status = await shellBridge.getSystemStatus();
      } catch (error) {
        console.error('Error getting system status for network menu:', error);
      }

      const networkStatus = status && status.network ? status.network : null;
      const menuItems = [];

      if (networkStatus) {
        const kind =
          typeof networkStatus.kind === 'string'
            ? networkStatus.kind.toLowerCase()
            : '';
        const isConnected = !!networkStatus.isConnected;

        const label = !isConnected
          ? 'No network connection'
          : kind === 'wifi'
            ? 'Connected via Wi-Fi'
            : kind === 'ethernet'
              ? 'Connected via Ethernet'
              : 'Network status';

        menuItems.push({
          label,
          action: async () => {},
          disabled: true,
        });

        const hasWifiAdapter =
          typeof networkStatus.hasWifiAdapter === 'boolean'
            ? networkStatus.hasWifiAdapter
            : false;
        const hasEthernetAdapter =
          typeof networkStatus.hasEthernetAdapter === 'boolean'
            ? networkStatus.hasEthernetAdapter
            : false;

        if (hasWifiAdapter && kind !== 'wifi') {
          menuItems.push({
            label: 'Switch to Wi-Fi',
            action: () => shellBridge.preferNetwork('wifi'),
          });
        }

        if (hasEthernetAdapter && kind !== 'ethernet') {
          menuItems.push({
            label: 'Switch to Ethernet',
            action: () => shellBridge.preferNetwork('ethernet'),
          });
        }
      }

      menuItems.push({
        label: 'Open Network settings',
        action: () => shellBridge.openNetworkSettings(),
      });

      if (menuItems.length === 0) {
        return;
      }

      showContextMenu(event, menuItems, 'network-context-menu');
    });
  }

  selectors.launcherGrid.addEventListener('click', async (event) => {
    const target = event.target.closest('[data-app-id]');
    if (!target) return;
    
    const appId = target.dataset.appId;
    
    try {
      // Use bridge API to launch app
      const success = await shellBridge.launchApp(appId);
      if (success) {
        console.log('App launched:', appId);
        // Close launcher after successful launch
        store.toggleLauncher(false);
      } else {
        console.error('Failed to launch app:', appId);
      }
    } catch (error) {
      console.error('Error launching app:', error);
    }
  });

  // Desktop icon interactions: selection, drag-move, click-to-open, context menu.
  const desktopContainer = selectors.desktopIcons || selectors.desktopSpace;
  if (desktopContainer) {
    let lastSelectedDesktopPath = null;
    let suppressNextDesktopClickOpen = false;

    const DRAG_START_THRESHOLD_SQUARED = 16; // 4px movement

    let dragState = null;

    function getCurrentDesktopSelection() {
      const state = store.getState();
      return Array.isArray(state.selectedDesktopPaths)
        ? [...state.selectedDesktopPaths]
        : [];
    }

    function setCurrentDesktopSelection(paths) {
      store.setDesktopSelection(paths);
    }

    function buildSelectionForClick(path, event) {
      const state = store.getState();
      const items = Array.isArray(state.desktopItems)
        ? sortItemsByLayout(state.desktopItems)
        : [];
      const currentSelection = new Set(
        Array.isArray(state.selectedDesktopPaths) ? state.selectedDesktopPaths : [],
      );

      if (!path) {
        return [];
      }

      let nextSelection = new Set(currentSelection);

      if (event.shiftKey && lastSelectedDesktopPath) {
        const anchor = lastSelectedDesktopPath;
        const anchorIndex = items.findIndex((i) => i.path === anchor);
        const targetIndex = items.findIndex((i) => i.path === path);
        if (anchorIndex >= 0 && targetIndex >= 0) {
          const start = Math.min(anchorIndex, targetIndex);
          const end = Math.max(anchorIndex, targetIndex);
          nextSelection = new Set(
            items.slice(start, end + 1).map((item) => item.path),
          );
        } else {
          nextSelection = new Set([path]);
        }
      } else if (event.ctrlKey || event.metaKey) {
        if (nextSelection.has(path)) {
          nextSelection.delete(path);
        } else {
          nextSelection.add(path);
        }
      } else {
        nextSelection = new Set([path]);
      }

      lastSelectedDesktopPath = path;
      return Array.from(nextSelection);
    }

    desktopContainer.addEventListener('pointerdown', (event) => {
      if (event.button !== 0) {
        return;
      }

      const icon = event.target.closest('[data-desktop-path]');
      const path = icon?.dataset.desktopPath;

      if (!icon || !path) {
        // Clicked on empty desktop space: clear selection.
        setCurrentDesktopSelection([]);
        lastSelectedDesktopPath = null;
        dragState = null;
        return;
      }

      event.preventDefault();

      // Update selection immediately on pointer-down so drag operations act on
      // the correct group of icons.
      const nextSelection = buildSelectionForClick(path, event);
      setCurrentDesktopSelection(nextSelection);

      const state = store.getState();
      const selectedPaths = new Set(
        Array.isArray(state.selectedDesktopPaths) ? state.selectedDesktopPaths : [path],
      );

      const iconsInSelection = [];
      const allIcons = desktopContainer.querySelectorAll('[data-desktop-path]');
      allIcons.forEach((node) => {
        const nodePath = node.dataset.desktopPath;
        if (selectedPaths.has(nodePath)) {
          iconsInSelection.push(node);
        }
      });

      const { layout, metrics } = getDesktopPositions(state.desktopItems || [], desktopContainer);

      dragState = {
        pointerId: event.pointerId,
        startX: event.clientX,
        startY: event.clientY,
        hasMoved: false,
        draggedPaths: Array.from(selectedPaths),
        draggedElements: iconsInSelection,
        baseLayout: layout,
        baseMetrics: metrics,
      };

      try {
        icon.setPointerCapture(event.pointerId);
      } catch {
        // Ignore errors from pointer capture (older platforms).
      }
    });

    desktopContainer.addEventListener('pointermove', (event) => {
      if (!dragState || event.pointerId !== dragState.pointerId) {
        return;
      }

      const dx = event.clientX - dragState.startX;
      const dy = event.clientY - dragState.startY;
      const distanceSquared = dx * dx + dy * dy;

      if (!dragState.hasMoved) {
        if (distanceSquared < DRAG_START_THRESHOLD_SQUARED) {
          return;
        }
        dragState.hasMoved = true;
      }

      dragState.draggedElements.forEach((el) => {
        el.classList.add('desktop-icon--dragging');
        el.style.transform = `translate(${dx}px, ${dy}px)`;
      });
    });

    function clearDragState() {
      if (!dragState) return;
      dragState.draggedElements.forEach((el) => {
        el.classList.remove('desktop-icon--dragging');
        el.style.transform = '';
      });
      dragState = null;
    }

    desktopContainer.addEventListener('pointerup', (event) => {
      if (!dragState || event.pointerId !== dragState.pointerId) {
        return;
      }

      const wasDragging = dragState.hasMoved;
      if (wasDragging) {
        const dx = event.clientX - dragState.startX;
        const dy = event.clientY - dragState.startY;

        const state = store.getState();
        const items = Array.isArray(state.desktopItems) ? state.desktopItems : [];

        const result = moveDesktopSelection(
          items,
          dragState.draggedPaths,
          dx,
          dy,
          desktopContainer,
          dragState.baseLayout,
          dragState.baseMetrics,
        );

        if (result) {
          // Force a re-render so the renderer can apply the updated layout.
          store.setDesktopItems(items);
          suppressNextDesktopClickOpen = true;
        }
      }

      clearDragState();
    });

    desktopContainer.addEventListener('lostpointercapture', () => {
      clearDragState();
    });

    desktopContainer.addEventListener('click', async (event) => {
      const target = event.target.closest('[data-desktop-path]');
      if (!target) {
        return;
      }

      const path = target.dataset.desktopPath;
      if (!path) {
        return;
      }

      if (suppressNextDesktopClickOpen) {
        suppressNextDesktopClickOpen = false;
        return;
      }

      // Modifier clicks are used only for selection; do not open items.
      if (event.ctrlKey || event.metaKey || event.shiftKey) {
        return;
      }

      try {
        const success = await shellBridge.launchApp(path);
        if (!success) {
          console.warn('Failed to open desktop item:', path);
        }
      } catch (error) {
        console.error('Error opening desktop item:', error);
      }
    });

    // Desktop icon right-click: show context menu, aligning selection with the target.
    desktopContainer.addEventListener('contextmenu', (event) => {
      const target = event.target.closest('[data-desktop-path]');
      if (!target) return;

      event.preventDefault();

      const path = target.dataset.desktopPath;
      if (!path) return;

      const currentSelection = new Set(getCurrentDesktopSelection());
      if (!currentSelection.has(path)) {
        setCurrentDesktopSelection([path]);
        lastSelectedDesktopPath = path;
      }

      const state = store.getState();
      const item = state.desktopItems.find((i) => i.path === path) ?? { path };

      showDesktopContextMenu(event, item);
    });
  }

  // Exit shell / restore Explorer button (no-op in dev/test mode)
  if (selectors.exitShellButton) {
    selectors.exitShellButton.addEventListener('click', async () => {
      try {
        const confirmed = window.confirm('Restore Explorer as the desktop shell and launch it?');
        if (!confirmed) return;

        const success = await shellBridge.restoreExplorerShell();
        if (!success) {
          console.warn('RestoreExplorerShell did not complete successfully (dev/test mode or error).');
        }
      } catch (error) {
        console.error('Error restoring Explorer shell from UI:', error);
      }
    });
  }

  // Add keyboard shortcuts for workspace switching
  document.addEventListener('keydown', (event) => {
    if (event.key === 'Escape') {
      store.toggleLauncher(false);
      if (isVolumeFlyoutOpen) {
        closeVolumeFlyout();
      }
      return;
    }

    // Ctrl+1-9 for workspace switching
    if (event.ctrlKey && event.key >= '1' && event.key <= '9') {
      event.preventDefault();
      const workspaceIndex = parseInt(event.key) - 1;
      const workspaces = store.getState().workspaces;
      
      if (workspaceIndex < workspaces.length) {
        const workspaceId = workspaces[workspaceIndex].id;
        shellBridge.switchWorkspace(workspaceId).catch(error => {
          console.error('Error switching workspace via keyboard:', error);
          store.setActiveWorkspace(workspaceId);
        });
      }
    }

    // Alt+Tab for window switching (basic implementation)
    if (event.altKey && event.key === 'Tab') {
      event.preventDefault();
      const windows = store.getState().windows.filter(w => w.isVisible);
      const currentFocused = store.getState().focusedWindow;
      
      if (windows.length > 1) {
        const currentIndex = windows.findIndex(w => w.hwnd === currentFocused);
        const nextIndex = (currentIndex + 1) % windows.length;
        const nextWindow = windows[nextIndex];
        
        shellBridge.focusWindow(nextWindow.hwnd).catch(error => {
          console.error('Error switching window via keyboard:', error);
          store.setFocusedWindow(nextWindow.hwnd);
        });
      }
    }
  });
}

/**
 * Show context menu for taskbar items
 */
function showTaskbarContextMenu(event, hwnd, window, store) {
  const menuItems = [];

  // Restore/Minimize based on current state
  if (window.state === 'minimized') {
    menuItems.push({
      label: 'Restore',
      action: () => shellBridge.restoreWindow(hwnd)
    });
  } else {
    menuItems.push({
      label: 'Minimize',
      action: () => shellBridge.minimizeWindow(hwnd)
    });
  }

  // Always show focus option
  menuItems.push({
    label: 'Focus',
    action: () => shellBridge.focusWindow(hwnd)
  });

  // Move window to another workspace (if any)
  const state = store.getState();
  const workspaces = state.workspaces || [];
  const targetWorkspaces = workspaces.filter(
    (ws) => ws.id && ws.id !== window.workspaceId,
  );

  targetWorkspaces.forEach((ws) => {
    menuItems.push({
      label: `Move to ${ws.name}`,
      action: () => shellBridge.moveWindowToWorkspace(hwnd, ws.id),
    });
  });

  // Close option
  menuItems.push({
    label: 'Close',
    action: () => shellBridge.closeWindow(hwnd),
    disabled: false
  });

  showContextMenu(event, menuItems, 'taskbar-context-menu');
}

/**
 * Show context menu for desktop items
 */
function showDesktopContextMenu(event, desktopItem) {
  const menuItems = [];

  if (desktopItem && desktopItem.path) {
    menuItems.push({
      label: 'Open',
      action: () => shellBridge.launchApp(desktopItem.path),
    });

    if (!desktopItem.path.startsWith('shell:')) {
      menuItems.push({
        label: 'Open file location',
        action: () => shellBridge.openItemLocation(desktopItem.path),
      });
    }
  }

  if (menuItems.length === 0) {
    return;
  }

  showContextMenu(event, menuItems, 'desktop-context-menu');
}

/**
 * Generic context menu renderer used by both taskbar and desktop menus.
 */
function showContextMenu(event, menuItems, extraClassName) {
  // Remove any existing context menu
  const existingMenu = document.querySelector('.taskbar-context-menu');
  if (existingMenu) {
    existingMenu.remove();
  }

  // Create context menu
  const menu = document.createElement('div');
  menu.className = ['taskbar-context-menu', extraClassName].filter(Boolean).join(' ');
  menu.style.position = 'fixed';
  menu.style.zIndex = '10000';

  // Create menu HTML
  menu.innerHTML = `
    <div class="context-menu-content">
      ${menuItems.map(item => `
        <button type="button" class="context-menu-item" 
                ${item.disabled ? 'disabled' : ''}>
          ${item.label}
        </button>
      `).join('')}
    </div>
  `;

  document.body.appendChild(menu);

  // Position the menu, clamping to the viewport so it stays fully visible.
  const viewportWidth = window.innerWidth;
  const viewportHeight = window.innerHeight;
  const rect = menu.getBoundingClientRect();

  let left = event.clientX;
  let top = event.clientY;

  if (left + rect.width > viewportWidth) {
    left = Math.max(0, viewportWidth - rect.width - 8);
  }
  if (top + rect.height > viewportHeight) {
    top = Math.max(0, viewportHeight - rect.height - 8);
  }

  menu.style.left = `${left}px`;
  menu.style.top = `${top}px`;

  // Add event listeners
  const buttons = menu.querySelectorAll('.context-menu-item');
  buttons.forEach((button, index) => {
    const item = menuItems[index];
    if (!item || item.disabled) {
      return;
    }

    button.addEventListener('click', async () => {
      try {
        await item.action();
      } catch (error) {
        console.error('Error executing context menu action:', error);
      }
      menu.remove();
    });
  });

  // Close menu when clicking outside
  const closeMenu = (e) => {
    if (!menu.contains(e.target)) {
      menu.remove();
      document.removeEventListener('click', closeMenu);
    }
  };

  // Add close handler after a brief delay to prevent immediate closure
  setTimeout(() => {
    document.addEventListener('click', closeMenu);
  }, 10);

  // Close on escape key
  const handleEscape = (e) => {
    if (e.key === 'Escape') {
      menu.remove();
      document.removeEventListener('keydown', handleEscape);
    }
  };
  document.addEventListener('keydown', handleEscape);
}
