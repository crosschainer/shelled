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

const createShellStubForWorkspaces = (switchCalls) => ({
  async listWindowsJson() {
    return JSON.stringify([]);
  },
  async listWorkspacesJson() {
    return JSON.stringify([
      { id: 'default', name: 'Default', windowHandles: [], isActive: true },
      { id: 'dev', name: 'Dev', windowHandles: [], isActive: false }
    ]);
  },
  async getTrayIconsJson() {
    return JSON.stringify([]);
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
  async switchWorkspace(id) {
    switchCalls.push(id);
    return true;
  },
  async moveWindowToWorkspace() {
    return true;
  },
  async trayIconClick() {
    return true;
  },
  async minimizeWindow() {
    return true;
  }
});

test('TEST-E2E-02: workspace strip switches active workspace and highlights selection', async (t) => {
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
  const switchCalls = [];
  dom.window.shell = createShellStubForWorkspaces(switchCalls);

  const { shellBridge } = await import('../../src/js/bridge/api.js');
  shellBridge.isConnected = true;

  await import('../../src/js/main.js');

  await waitFor(
    () => dom.window.document.getElementById('status-text').textContent === 'Connected'
  );

  const workspaceStrip = dom.window.document.getElementById('workspace-strip');
  const workspaceButtons = workspaceStrip.querySelectorAll('[data-workspace-id]');
  assert.equal(workspaceButtons.length >= 2, true);

  const defaultButton = workspaceStrip.querySelector('[data-workspace-id="default"]');
  const devButton = workspaceStrip.querySelector('[data-workspace-id="dev"]');
  assert.ok(defaultButton);
  assert.ok(devButton);

  // Initial state: default is active
  assert.equal(defaultButton.classList.contains('active'), true);
  assert.equal(devButton.classList.contains('active'), false);

  // Click dev workspace
  devButton.dispatchEvent(new dom.window.MouseEvent('click', { bubbles: true }));
  await nextTick();

  // Bridge should have been asked to switch
  assert.deepEqual(switchCalls, ['dev']);

  // Simulate Core event to confirm highlighting logic
  const { shellBridge: bridgeApi } = await import('../../src/js/bridge/api.js');
  bridgeApi.emit('workspaceSwitched', {
    previousWorkspaceId: 'default',
    newWorkspaceId: 'dev'
  });
  await nextTick();

  const refreshedDefault = workspaceStrip.querySelector('[data-workspace-id="default"]');
  const refreshedDev = workspaceStrip.querySelector('[data-workspace-id="dev"]');
  assert.equal(refreshedDefault.classList.contains('active'), false);
  assert.equal(refreshedDev.classList.contains('active'), true);
});

