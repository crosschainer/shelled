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
      
      if (!window.shell || typeof window.shell[methodName] !== 'function') {
        throw new Error(`Bridge method ${methodName} not available`);
      }

      return await window.shell[methodName](...args);
    } catch (error) {
      console.error(`Error calling bridge method ${methodName}:`, error);
      throw error;
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
}

// Create and export a singleton instance
export const shellBridge = new ShellBridge();
export default shellBridge;