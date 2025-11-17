import { createShellStateStore } from './state/store.js';
import { createRenderer } from './ui/renderers.js';
import { wireInteractions } from './ui/interactions.js';
import { bootstrapClock } from './utils/clock.js';
import { bootstrapMockDataIfNeeded } from './bootstrap/mockData.js';
import shellBridge from './bridge/api.js';
import ShellSync from './bridge/sync.js';
import ShellEventHandler from './bridge/events.js';

const selectors = {
  status: document.getElementById('status-text'),
  clock: document.getElementById('clock'),
  launcherToggle: document.getElementById('launcher-toggle'),
  launcherClose: document.getElementById('launcher-close'),
  launcherOverlay: document.getElementById('launcher-overlay'),
  launcherGrid: document.getElementById('launcher-grid'),
  workspaceStrip: document.getElementById('workspace-strip'),
  taskbarWindows: document.getElementById('taskbar-windows'),
  trayIcons: document.getElementById('tray-icons'),
};

const store = createShellStateStore();
const renderShell = createRenderer(selectors);
store.subscribe(renderShell);
bootstrapClock(selectors.clock);
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
