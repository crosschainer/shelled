import test from 'node:test';
import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import fs from 'node:fs/promises';
import { JSDOM } from 'jsdom';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const uiRoot = path.resolve(__dirname, '..', '..');
const htmlPath = path.join(uiRoot, 'src', 'index.html');

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

const nextTick = () => new Promise((resolve) => setTimeout(resolve, 0));

const createShellStub = (launchCalls) => ({
  async listWindowsJson() {
    return JSON.stringify([]);
  },
  async listWorkspacesJson() {
    return JSON.stringify([
      { id: 'default', name: 'Default', windowHandles: [], isActive: true }
    ]);
  },
  async getTrayIconsJson() {
    return JSON.stringify([]);
  },
  async getLauncherAppsJson() {
    return JSON.stringify([
      { id: 'notepad', name: 'Notepad', description: 'Simple text editor', category: 'Productivity' },
      { id: 'calc', name: 'Calculator', description: 'Windows calculator', category: 'Utilities' }
    ]);
  },
  async getShellStateJson() {
    return JSON.stringify({ activeWorkspaceId: 'default', focusedWindowHandle: '0' });
  },
  async launchApp(appId) {
    launchCalls.push(appId);
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
  async trayIconClick() {
    return true;
  },
  async minimizeWindow() {
    return true;
  }
});

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

test('TEST-E2E-01: launcher opens Notepad and taskbar reflects the new window', async (t) => {
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
  const launchCalls = [];
  dom.window.shell = createShellStub(launchCalls);

  const { shellBridge } = await import('../../src/js/bridge/api.js');
  shellBridge.isConnected = true;

  await import('../../src/js/main.js');

  await waitFor(() => dom.window.document.getElementById('status-text').textContent === 'Connected');

  const launcherToggle = dom.window.document.getElementById('launcher-toggle');
  launcherToggle.dispatchEvent(new dom.window.MouseEvent('click', { bubbles: true }));
  await nextTick();

  const overlay = dom.window.document.getElementById('launcher-overlay');
  assert.equal(overlay.classList.contains('open'), true);

  const notepadCard = overlay.querySelector('[data-app-id="notepad"]');
  assert.ok(notepadCard, 'expected notepad card to be rendered');

  notepadCard.dispatchEvent(new dom.window.MouseEvent('click', { bubbles: true }));
  await nextTick();

  assert.deepEqual(launchCalls, ['notepad']);
  assert.equal(overlay.classList.contains('open'), false);

  shellBridge.emit('windowCreated', {
    hwnd: '200',
    title: 'Untitled - Notepad',
    processId: 4242,
    workspaceId: 'default',
    state: 'normal',
    isVisible: true,
    appId: 'notepad',
    className: 'Notepad'
  });
  await nextTick();

  const taskbarItems = dom.window.document.querySelectorAll('#taskbar-windows .taskbar-item');
  assert.equal(taskbarItems.length, 1);
  assert.match(taskbarItems[0].textContent, /Notepad/);

  shellBridge.emit('windowFocusChanged', { previousHwnd: '0', currentHwnd: '200' });
  await nextTick();

  const refreshedItems = dom.window.document.querySelectorAll('#taskbar-windows .taskbar-item');
  assert.equal(refreshedItems[0].classList.contains('focused'), true);
});
