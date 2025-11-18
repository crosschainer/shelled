import shellBridge from './api.js';

/**
 * Handles initial synchronization with the shell core
 */
export class ShellSync {
  constructor(store) {
    this.store = store;
    this.isInitialized = false;
    this.syncPromise = null;
  }

  /**
   * Initialize synchronization with the shell core
   */
  async initialize() {
    if (this.isInitialized) {
      return;
    }

    if (this.syncPromise) {
      return this.syncPromise;
    }

    this.syncPromise = this.performInitialSync();
    return this.syncPromise;
  }

  async performInitialSync() {
    try {
      console.log('Starting initial sync with shell core...');
      
      // Wait for bridge connection
      await shellBridge.waitForConnection();
      
      // Update status to indicate we're syncing
      this.store.setStatusText('Syncing...');

      // Sync all data in parallel
      const [windows, workspaces, trayIcons, launcherApps, shellState] = await Promise.all([
        shellBridge.listWindows(),
        shellBridge.listWorkspaces(),
        shellBridge.getTrayIcons(),
        shellBridge.getLauncherApps(),
        shellBridge.getShellState()
      ]);

      // Update store with synced data
      this.store.setWindows(windows);
      this.store.setWorkspaces(workspaces);
      this.store.setTrayIcons(trayIcons);
      this.store.setLauncherApps(launcherApps);

      // Set active workspace from shell state
      if (shellState.activeWorkspaceId) {
        this.store.setActiveWorkspace(shellState.activeWorkspaceId);
      }

      // Set focused window from shell state
      if (shellState.focusedWindowHandle && shellState.focusedWindowHandle !== '0') {
        this.store.setFocusedWindow(shellState.focusedWindowHandle);
      }

      // Update status to connected
      this.store.setStatusText('Connected');
      
      console.log('Initial sync completed successfully', {
        windows: windows.length,
        workspaces: workspaces.length,
        trayIcons: trayIcons.length,
        activeWorkspace: shellState.activeWorkspaceId
      });

      this.isInitialized = true;
      
    } catch (error) {
      console.error('Error during initial sync:', error);
      this.store.setStatusText('Sync failed');
      throw error;
    }
  }

  /**
   * Force a resync of all data
   */
  async resync() {
    this.isInitialized = false;
    this.syncPromise = null;
    return this.initialize();
  }

  /**
   * Sync only windows
   */
  async syncWindows() {
    try {
      const windows = await shellBridge.listWindows();
      this.store.setWindows(windows);
      return windows;
    } catch (error) {
      console.error('Error syncing windows:', error);
      return [];
    }
  }

  /**
   * Sync only workspaces
   */
  async syncWorkspaces() {
    try {
      const workspaces = await shellBridge.listWorkspaces();
      this.store.setWorkspaces(workspaces);
      return workspaces;
    } catch (error) {
      console.error('Error syncing workspaces:', error);
      return [];
    }
  }

  /**
   * Sync only tray icons
   */
  async syncTrayIcons() {
    try {
      const trayIcons = await shellBridge.getTrayIcons();
      this.store.setTrayIcons(trayIcons);
      return trayIcons;
    } catch (error) {
      console.error('Error syncing tray icons:', error);
      return [];
    }
  }
}

export default ShellSync;