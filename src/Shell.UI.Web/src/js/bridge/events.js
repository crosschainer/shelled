import shellBridge from './api.js';

/**
 * Handles events from the shell core and updates the UI state
 */
export class ShellEventHandler {
  constructor(store) {
    this.store = store;
    this.isListening = false;
  }

  /**
   * Start listening to shell core events
   */
  startListening() {
    if (this.isListening) {
      return;
    }

    console.log('Starting to listen for shell core events...');

    // Window events
    shellBridge.on('windowCreated', this.handleWindowCreated.bind(this));
    shellBridge.on('windowDestroyed', this.handleWindowDestroyed.bind(this));
    shellBridge.on('windowUpdated', this.handleWindowUpdated.bind(this));
    shellBridge.on('windowFocusChanged', this.handleWindowFocusChanged.bind(this));

    // Workspace events
    shellBridge.on('workspaceSwitched', this.handleWorkspaceSwitched.bind(this));
    shellBridge.on('workspaceCreated', this.handleWorkspaceCreated.bind(this));
    shellBridge.on('windowMovedToWorkspace', this.handleWindowMovedToWorkspace.bind(this));

    // Tray events
    shellBridge.on('trayIconAdded', this.handleTrayIconAdded.bind(this));
    shellBridge.on('trayIconUpdated', this.handleTrayIconUpdated.bind(this));
    shellBridge.on('trayIconRemoved', this.handleTrayIconRemoved.bind(this));

    // Hotkey events
    shellBridge.on('hotkeyPressed', this.handleHotkeyPressed.bind(this));

    // Connection events
    shellBridge.on('connected', this.handleConnected.bind(this));

    this.isListening = true;
  }

  /**
   * Stop listening to shell core events
   */
  stopListening() {
    if (!this.isListening) {
      return;
    }

    console.log('Stopping shell core event listening...');

    // Remove all event listeners
    shellBridge.off('windowCreated', this.handleWindowCreated);
    shellBridge.off('windowDestroyed', this.handleWindowDestroyed);
    shellBridge.off('windowUpdated', this.handleWindowUpdated);
    shellBridge.off('windowFocusChanged', this.handleWindowFocusChanged);
    shellBridge.off('workspaceSwitched', this.handleWorkspaceSwitched);
    shellBridge.off('workspaceCreated', this.handleWorkspaceCreated);
    shellBridge.off('windowMovedToWorkspace', this.handleWindowMovedToWorkspace);
    shellBridge.off('trayIconAdded', this.handleTrayIconAdded);
    shellBridge.off('trayIconUpdated', this.handleTrayIconUpdated);
    shellBridge.off('trayIconRemoved', this.handleTrayIconRemoved);
    shellBridge.off('hotkeyPressed', this.handleHotkeyPressed);
    shellBridge.off('connected', this.handleConnected);

    this.isListening = false;
  }

  // Event handlers

  handleWindowCreated(data) {
    console.log('Window created:', data);
    const window = {
      hwnd: data.hwnd,
      title: data.title,
      processId: data.processId,
      workspaceId: data.workspaceId,
      state: data.state,
      isVisible: data.isVisible,
      appId: data.appId,
      className: data.className,
      iconData: data.iconData
    };
    this.store.upsertWindow(window);
  }

  handleWindowDestroyed(data) {
    console.log('Window destroyed:', data);
    this.store.removeWindow(data.hwnd);
  }

  handleWindowUpdated(data) {
    console.log('Window updated:', data);
    const window = {
      hwnd: data.hwnd,
      title: data.title,
      state: data.state,
      isVisible: data.isVisible
    };
    this.store.upsertWindow(window);
  }

  handleWindowFocusChanged(data) {
    console.log('Window focus changed:', data);
    this.store.setFocusedWindow(data.currentHwnd);
  }

  handleWorkspaceSwitched(data) {
    console.log('Workspace switched:', data);
    this.store.setActiveWorkspace(data.currentWorkspaceId);
  }

  handleWorkspaceCreated(data) {
    console.log('Workspace created:', data);
    // Refresh workspaces to get the updated list
    this.refreshWorkspaces();
  }

  handleWindowMovedToWorkspace(data) {
    console.log('Window moved to workspace:', data);
    // Update the window's workspace ID
    const window = {
      hwnd: data.hwnd,
      workspaceId: data.newWorkspaceId
    };
    this.store.upsertWindow(window);
  }

  handleTrayIconAdded(data) {
    console.log('Tray icon added:', data);
    // Refresh tray icons to get the updated list
    this.refreshTrayIcons();
  }

  handleTrayIconUpdated(data) {
    console.log('Tray icon updated:', data);
    // Refresh tray icons to get the updated list
    this.refreshTrayIcons();
  }

  handleTrayIconRemoved(data) {
    console.log('Tray icon removed:', data);
    // Remove from tray icons list
    const currentTrayIcons = this.store.getState().trayIcons;
    const updatedTrayIcons = currentTrayIcons.filter(icon => icon.id !== data.id);
    this.store.setTrayIcons(updatedTrayIcons);
  }

  handleHotkeyPressed(data) {
    console.log('Hotkey pressed:', data);
    
    // Handle specific hotkeys
    switch (data.hotkeyId) {
      case 'launcher-toggle':
        // Toggle launcher visibility
        this.toggleLauncher();
        break;
      default:
        console.log('Unhandled hotkey:', data.hotkeyId);
    }
  }

  toggleLauncher() {
    // Get launcher elements
    const launcherOverlay = document.getElementById('launcher-overlay');
    if (launcherOverlay) {
      const isVisible = launcherOverlay.style.display !== 'none';
      launcherOverlay.style.display = isVisible ? 'none' : 'flex';
      
      // Focus the launcher grid when showing
      if (!isVisible) {
        const launcherGrid = document.getElementById('launcher-grid');
        if (launcherGrid) {
          launcherGrid.focus();
        }
      }
    }
  }

  handleConnected(data) {
    console.log('Bridge connected:', data);
    this.store.setStatusText('Connected');
  }

  // Helper methods for refreshing data

  async refreshWorkspaces() {
    try {
      const workspaces = await shellBridge.listWorkspaces();
      this.store.setWorkspaces(workspaces);
    } catch (error) {
      console.error('Error refreshing workspaces:', error);
    }
  }

  async refreshTrayIcons() {
    try {
      const trayIcons = await shellBridge.getTrayIcons();
      this.store.setTrayIcons(trayIcons);
    } catch (error) {
      console.error('Error refreshing tray icons:', error);
    }
  }

  async refreshWindows() {
    try {
      const windows = await shellBridge.listWindows();
      this.store.setWindows(windows);
    } catch (error) {
      console.error('Error refreshing windows:', error);
    }
  }
}

export default ShellEventHandler;
