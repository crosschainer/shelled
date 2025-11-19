/**
 * Bridge API for communicating with the native shell core
 */

class ShellBridge {
  constructor() {
    this.isConnected = false;
    this.eventHandlers = new Map();
    this.connectionPromise = null;

    // Initialize WebView2 message listener
    this.initializeMessageListener();
  }

  initializeMessageListener() {
    if (typeof window === 'undefined') {
      return;
    }

    if (window.chrome && window.chrome.webview) {
      window.chrome.webview.addEventListener('message', (event) => {
        try {
          const message = JSON.parse(event.data);
          this.handleMessage(message);
        } catch (error) {
          console.error('Error parsing bridge message:', error);
        }
      });
    }
  }

  handleMessage(message) {
    const { type, data, timestamp } = message;
    
    if (type === 'connected') {
      this.isConnected = true;
      console.log('Bridge connected:', data);
      this.emit('connected', data);
      return;
    }

    // Emit the event to registered handlers
    this.emit(type, data);
  }

  /**
   * Register an event handler
   */
  on(eventType, handler) {
    if (!this.eventHandlers.has(eventType)) {
      this.eventHandlers.set(eventType, new Set());
    }
    this.eventHandlers.get(eventType).add(handler);
  }

  /**
   * Unregister an event handler
   */
  off(eventType, handler) {
    if (this.eventHandlers.has(eventType)) {
      this.eventHandlers.get(eventType).delete(handler);
    }
  }

  /**
   * Emit an event to all registered handlers
   */
  emit(eventType, data) {
    if (this.eventHandlers.has(eventType)) {
      this.eventHandlers.get(eventType).forEach(handler => {
        try {
          handler(data);
        } catch (error) {
          console.error(`Error in event handler for ${eventType}:`, error);
        }
      });
    }
  }

  /**
   * Wait for the bridge to be connected
   */
  async waitForConnection(timeoutMs = 5000) {
    if (this.isConnected) {
      return Promise.resolve();
    }

    if (this.connectionPromise) {
      return this.connectionPromise;
    }

    this.connectionPromise = new Promise((resolve, reject) => {
      const timeout = setTimeout(() => {
        reject(new Error('Bridge connection timeout'));
      }, timeoutMs);

      const onConnected = () => {
        clearTimeout(timeout);
        this.off('connected', onConnected);
        resolve();
      };

      this.on('connected', onConnected);
    });

    return this.connectionPromise;
  }

  /**
   * Call a bridge method safely
   */
  async callBridgeMethod(methodName, ...args) {
    try {
      await this.waitForConnection();

      if (typeof window === 'undefined') {
        throw new Error('Window object not available');
      }

      const hostObjects = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects;
      const nativeShell = (window.shell ?? null) || (hostObjects && hostObjects.shell);

      if (!nativeShell || typeof nativeShell[methodName] !== 'function') {
        throw new Error(`Bridge method ${methodName} not available`);
      }

      return await nativeShell[methodName](...args);
    } catch (error) {
      console.error(`Error calling bridge method ${methodName}:`, error);
      throw error;
    }
  }

  /**
   * Send a diagnostic log message to the native host.
   * This is best-effort: errors are swallowed on the JS side.
   */
  async log(level, message) {
    try {
      if (!window.shell || typeof window.shell.logMessage !== 'function') {
        return;
      }

      await window.shell.logMessage(level, String(message ?? ''));
    } catch {
      // Ignore logging errors
    }
  }

  // Shell API methods

  /**
   * Get all windows as JSON
   */
  async listWindows() {
    try {
      const json = await this.callBridgeMethod('listWindowsJson');
      return JSON.parse(json);
    } catch (error) {
      console.error('Error listing windows:', error);
      return [];
    }
  }

  /**
   * Get all workspaces as JSON
   */
  async listWorkspaces() {
    try {
      const json = await this.callBridgeMethod('listWorkspacesJson');
      return JSON.parse(json);
    } catch (error) {
      console.error('Error listing workspaces:', error);
      return [];
    }
  }

  /**
   * Get all tray icons as JSON
   */
  async getTrayIcons() {
    try {
      const json = await this.callBridgeMethod('getTrayIconsJson');
      return JSON.parse(json);
    } catch (error) {
      console.error('Error getting tray icons:', error);
      return [];
    }
  }

  /**
   * Get all launcher apps as JSON
   */
  async getLauncherApps() {
    try {
      const json = await this.callBridgeMethod('getLauncherAppsJson');
      return JSON.parse(json);
    } catch (error) {
      console.error('Error getting launcher apps:', error);
      return [];
    }
  }

  /**
   * Get desktop items (user + public desktop).
   */
  async getDesktopItems() {
    try {
      const json = await this.callBridgeMethod('getDesktopItemsJson');
      return JSON.parse(json);
    } catch (error) {
      console.error('Error getting desktop items:', error);
      return [];
    }
  }

  /**
   * Get information about the current desktop background / wallpaper.
   */
  async getDesktopBackgroundInfo() {
    try {
      const json = await this.callBridgeMethod('getDesktopBackgroundInfoJson');
      return JSON.parse(json);
    } catch (error) {
      console.error('Error getting desktop background info:', error);
      return {};
    }
  }

  /**
   * Open the containing folder for a file path in Explorer.
   */
  async openItemLocation(path) {
    try {
      return await this.callBridgeMethod('openItemLocation', path);
    } catch (error) {
      console.error('Error opening item location:', error);
      return false;
    }
  }

  /**
   * Launch an application
   */
  async launchApp(appIdOrPath) {
    try {
      return await this.callBridgeMethod('launchApp', appIdOrPath);
    } catch (error) {
      console.error('Error launching app:', error);
      return false;
    }
  }

  /**
   * Focus a window
   */
  async focusWindow(hwnd) {
    try {
      return await this.callBridgeMethod('focusWindow', hwnd);
    } catch (error) {
      console.error('Error focusing window:', error);
      return false;
    }
  }

  /**
   * Minimize a window
   */
  async minimizeWindow(hwnd) {
    try {
      return await this.callBridgeMethod('minimizeWindow', hwnd);
    } catch (error) {
      console.error('Error minimizing window:', error);
      return false;
    }
  }

  /**
   * Restore a minimized window
   */
  async restoreWindow(hwnd) {
    try {
      return await this.callBridgeMethod('restoreWindow', hwnd);
    } catch (error) {
      console.error('Error restoring window:', error);
      return false;
    }
  }

  /**
   * Close a window
   */
  async closeWindow(hwnd) {
    try {
      return await this.callBridgeMethod('closeWindow', hwnd);
    } catch (error) {
      console.error('Error closing window:', error);
      return false;
    }
  }

  /**
   * Switch to a workspace
   */
  async switchWorkspace(workspaceId) {
    try {
      return await this.callBridgeMethod('switchWorkspace', workspaceId);
    } catch (error) {
      console.error('Error switching workspace:', error);
      return false;
    }
  }

  /**
   * Create a new workspace
   */
  async createWorkspace(workspaceId, name) {
    try {
      return await this.callBridgeMethod('createWorkspace', workspaceId, name);
    } catch (error) {
      console.error('Error creating workspace:', error);
      return false;
    }
  }

  /**
   * Move a window to a different workspace
   */
  async moveWindowToWorkspace(hwnd, workspaceId) {
    try {
      return await this.callBridgeMethod('moveWindowToWorkspace', hwnd, workspaceId);
    } catch (error) {
      console.error('Error moving window to workspace:', error);
      return false;
    }
  }

  /**
   * Get current shell state
   */
  async getShellState() {
    try {
      const json = await this.callBridgeMethod('getShellStateJson');
      return JSON.parse(json);
    } catch (error) {
      console.error('Error getting shell state:', error);
      return {};
    }
  }

  /**
   * Get current system status (time, network, volume)
   */
  async getSystemStatus() {
    try {
      const json = await this.callBridgeMethod('getSystemStatusJson');
      return JSON.parse(json);
    } catch (error) {
      console.error('Error getting system status:', error);
      return {};
    }
  }

  /**
   * Set master system volume as a percentage.
   */
  async setSystemVolume(levelPercent) {
    try {
      return await this.callBridgeMethod('setSystemVolume', levelPercent);
    } catch (error) {
      console.error('Error setting system volume:', error);
      return false;
    }
  }

  /**
   * Toggle master system mute state.
   */
  async toggleSystemMute() {
    try {
      return await this.callBridgeMethod('toggleSystemMute');
    } catch (error) {
      console.error('Error toggling system mute:', error);
      return false;
    }
  }

  /**
   * Open operating system network settings.
   */
  async openNetworkSettings() {
    try {
      return await this.callBridgeMethod('openNetworkSettings');
    } catch (error) {
      console.error('Error opening network settings:', error);
      return false;
    }
  }

  /**
   * Prefer a specific network kind when both Wi-Fi and Ethernet are available.
   */
  async preferNetwork(kind) {
    try {
      return await this.callBridgeMethod('preferNetwork', kind);
    } catch (error) {
      console.error('Error setting preferred network:', error);
      return false;
    }
  }

  /**
   * Handle tray icon click
   */
  async trayIconClick(trayIconId, clickType) {
    try {
      return await this.callBridgeMethod('trayIconClick', trayIconId, clickType);
    } catch (error) {
      console.error('Error handling tray icon click:', error);
      return false;
    }
  }

  /**
   * Restore Explorer as the shell and launch it.
   */
  async restoreExplorerShell() {
    try {
      return await this.callBridgeMethod('restoreExplorerShell');
    } catch (error) {
      console.error('Error restoring Explorer shell:', error);
      return false;
    }
  }
}

// Create and export a singleton instance
export const shellBridge = new ShellBridge();
export default shellBridge;
