import shellBridge from '../bridge/api.js';

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

  selectors.taskbarWindows.addEventListener('click', async (event) => {
    const target = event.target.closest('[data-hwnd]');
    if (!target) return;
    
    const hwnd = target.dataset.hwnd;
    
    try {
      // Use bridge API to focus window
      const success = await shellBridge.focusWindow(hwnd);
      if (success) {
        // The window focus will be handled by the event system
        console.log('Window focused:', hwnd);
      } else {
        console.error('Failed to focus window:', hwnd);
        // Fallback to local state update
        store.setFocusedWindow(hwnd);
      }
    } catch (error) {
      console.error('Error focusing window:', error);
      // Fallback to local state update
      store.setFocusedWindow(hwnd);
    }
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

  // Add keyboard shortcuts for workspace switching
  document.addEventListener('keydown', (event) => {
    if (event.key === 'Escape') {
      store.toggleLauncher(false);
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
