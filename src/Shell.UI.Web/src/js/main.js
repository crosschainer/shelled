import { createShellStateStore } from './state/store.js';
import { createRenderer } from './ui/renderers.js';
import { wireInteractions } from './ui/interactions.js';
import { bootstrapClock } from './utils/clock.js';
import { bootstrapSystemStatus } from './utils/systemStatus.js';
import { applyDesktopBackground } from './utils/background.js';
import { bootstrapMockDataIfNeeded } from './bootstrap/mockData.js';
import shellBridge from './bridge/api.js';
import ShellSync from './bridge/sync.js';
import ShellEventHandler from './bridge/events.js';

const selectors = {
  desktop: document.getElementById('desktop'),
  status: document.getElementById('status-text'),
  clock: document.getElementById('clock'),
  topPanel: document.getElementById('top-panel'),
  launcherToggle: document.getElementById('launcher-toggle'),
  launcherClose: document.getElementById('launcher-close'),
  launcherOverlay: document.getElementById('launcher-overlay'),
  launcherGrid: document.getElementById('launcher-grid'),
  desktopSpace: document.getElementById('desktop-space'),
  desktopIcons: document.getElementById('desktop-icons'),
  workspaceStrip: document.getElementById('workspace-strip'),
  taskbarWindows: document.getElementById('taskbar-windows'),
  trayIcons: document.getElementById('tray-icons'),
  taskbar: document.getElementById('taskbar'),
  exitShellButton: document.getElementById('exit-shell-button'),
  networkIndicator: document.getElementById('network-indicator'),
  volumeIndicator: document.getElementById('volume-indicator'),
  volumeFlyout: document.getElementById('volume-flyout'),
  volumeSlider: document.getElementById('volume-slider'),
  volumeMuteToggle: document.getElementById('volume-mute-toggle'),
  volumePercentLabel: document.getElementById('volume-percent-label'),
  loadingOverlay: document.getElementById('shell-loading-overlay'),
};

const store = createShellStateStore();
const renderShell = createRenderer(selectors);
store.subscribe(renderShell);
bootstrapClock(selectors.clock);
bootstrapSystemStatus(selectors);
wireInteractions(selectors, store);

// Initialize bridge integration
const shellSync = new ShellSync(store);
const eventHandler = new ShellEventHandler(store);

// Start listening for events
eventHandler.startListening();

// Initialize shell integration
async function initializeShell() {
  try {
    console.log('Initializing shell integration...');
    
    // Try to sync with shell core
    await shellSync.initialize();

     // Sync desktop background with Windows wallpaper when available
    try {
      const backgroundInfo = await shellBridge.getDesktopBackgroundInfo();
      applyDesktopBackground(backgroundInfo, selectors.desktop);
    } catch (error) {
      console.warn('Failed to apply desktop background from shell:', error);
    }
    
    console.log('Shell integration initialized successfully');
  } catch (error) {
    console.warn('Shell integration failed, falling back to mock data:', error);
    
    // Fall back to mock data if shell integration fails
    bootstrapMockDataIfNeeded(store);
  }
}

// Start initialization
initializeShell();

// Export for debugging
window.shellDebug = {
  store,
  shellBridge,
  shellSync,
  eventHandler
};
