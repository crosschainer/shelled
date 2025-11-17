import { createShellStateStore } from './state/store.js';
import { createRenderer } from './ui/renderers.js';
import { wireInteractions } from './ui/interactions.js';
import { bootstrapClock } from './utils/clock.js';
import { bootstrapMockDataIfNeeded } from './bootstrap/mockData.js';

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
bootstrapMockDataIfNeeded(store);
