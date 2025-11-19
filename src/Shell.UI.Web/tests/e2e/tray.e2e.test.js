import test from 'node:test';
import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import fs from 'node:fs/promises';
import { JSDOM } from 'jsdom';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const uiRoot = path.resolve(__dirname, '..', '..');
const htmlPath = path.join(uiRoot, 'src', 'index.html');

const nextTick = () => new Promise((resolve) => setTimeout(resolve, 0));

const waitFor = async (predicate, { timeout = 2000, interval = 10 } = {}) => {
  const start = Date.now();
  return new Promise((resolve, reject) => {
    const check = () => {
      try {
        if (predicate()) {
          resolve(true);
          return;
        }
      } catch (error) {
        reject(error);
        return;
      }

      if (Date.now() - start > timeout) {
        reject(new Error('Timed out waiting for condition.'));
        return;
      }

      setTimeout(check, interval);
    };

    check();
  });
};

const assignGlobals = (window) => {
  global.window = window;
  global.document = window.document;
  global.HTMLElement = window.HTMLElement;
  global.CustomEvent = window.CustomEvent;
  global.Event = window.Event;
  global.Node = window.Node;
  global.navigator = window.navigator;
  global.DOMParser = window.DOMParser;
  global.requestAnimationFrame = window.requestAnimationFrame = (cb) => setTimeout(cb, 0);
  global.cancelAnimationFrame = window.cancelAnimationFrame;
};

const resetGlobals = () => {
  delete global.window;
  delete global.document;
  delete global.HTMLElement;
  delete global.CustomEvent;
  delete global.Event;
  delete global.Node;
  delete global.navigator;
  delete global.DOMParser;
  delete global.requestAnimationFrame;
  delete global.cancelAnimationFrame;
};

const createShellStubForTray = (clickCalls) => ({
  async listWindowsJson() {
    return JSON.stringify([]);
  },
  async listWorkspacesJson() {
    return JSON.stringify([
      { id: 'default', name: 'Default', windowHandles: [], isActive: true }
    ]);
  },
  async getTrayIconsJson() {
    return JSON.stringify([
      {
        id: 'tray-app-1',
        tooltip: 'Tray App 1',
        processId: 2001,
        isVisible: true,
        iconData: null
      }
    ]);
  },
  async getLauncherAppsJson() {
    return JSON.stringify([]);
  },
  async getShellStateJson() {
    return JSON.stringify({ activeWorkspaceId: 'default', focusedWindowHandle: '0' });
  },
  async launchApp() {
    return true;
  },
  async focusWindow() {
    return true;
  },
  async restoreWindow() {
    return true;
  },
  async switchWorkspace() {
    return true;
  },
  async moveWindowToWorkspace() {
    return true;
  },
  async trayIconClick(id, action) {
    clickCalls.push({ id, action });
    return true;
  },
  async minimizeWindow() {
    return true;
  }
});

test('TEST-E2E-03: tray icon is rendered and click forwards to shellApi', async (t) => {
  const html = await fs.readFile(htmlPath, 'utf8');
  const dom = new JSDOM(html, {
    url: 'http://localhost',
    pretendToBeVisual: true
  });

  assignGlobals(dom.window);
  t.after(() => {
    dom.window.close();
    resetGlobals();
  });

  const activeTimers = new Set();
  const nativeSetInterval = global.setInterval;
  const nativeClearInterval = global.clearInterval;
  global.setInterval = (...args) => {
    const id = nativeSetInterval(...args);
    activeTimers.add(id);
    return id;
  };
  global.clearInterval = (id) => {
    activeTimers.delete(id);
    nativeClearInterval(id);
  };
  dom.window.setInterval = global.setInterval;
  dom.window.clearInterval = global.clearInterval;
  t.after(() => {
    activeTimers.forEach((id) => nativeClearInterval(id));
    activeTimers.clear();
    global.setInterval = nativeSetInterval;
    global.clearInterval = nativeClearInterval;
  });

  dom.window.chrome = { webview: { addEventListener: () => {} } };
  const clickCalls = [];
  dom.window.shell = createShellStubForTray(clickCalls);

  const { shellBridge } = await import('../../src/js/bridge/api.js');
  shellBridge.isConnected = true;

  await import('../../src/js/main.js');

  await waitFor(
    () => dom.window.document.getElementById('status-text').textContent === 'Connected'
  );

  const trayContainer = dom.window.document.getElementById('tray-icons');
  await waitFor(() => trayContainer.querySelectorAll('[data-tray-id]').length > 0);

  const trayItems = trayContainer.querySelectorAll('[data-tray-id]');
  assert.equal(trayItems.length, 1);
  const trayItem = trayItems[0];
  assert.equal(trayItem.dataset.trayId, 'tray-app-1');

  // Click tray icon (left-click)
  trayItem.dispatchEvent(new dom.window.MouseEvent('click', { bubbles: true }));
  await nextTick();

  assert.deepEqual(clickCalls, [{ id: 'tray-app-1', action: 'left' }]);
});

