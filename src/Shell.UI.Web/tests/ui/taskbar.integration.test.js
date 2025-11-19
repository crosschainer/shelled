import test from 'node:test';
import assert from 'node:assert/strict';
import { JSDOM } from 'jsdom';

import { createShellStateStore } from '../../src/js/state/store.js';
import { createRenderer } from '../../src/js/ui/renderers.js';
import { wireInteractions } from '../../src/js/ui/interactions.js';
import shellBridge from '../../src/js/bridge/api.js';

function setupDom() {
  const dom = new JSDOM(`<!DOCTYPE html>
    <html lang="en">
      <body>
        <span id="status-text"></span>
        <div id="clock"></div>
        <button id="launcher-toggle"></button>
        <button id="launcher-close"></button>
        <div id="launcher-overlay" class="overlay-backdrop"></div>
        <div id="launcher-grid"></div>
        <div id="workspace-strip"></div>
        <div id="taskbar-windows"></div>
        <div id="tray-icons"></div>
      </body>
    </html>`, { url: 'http://localhost' });

  global.window = dom.window;
  global.document = dom.window.document;
  global.HTMLElement = dom.window.HTMLElement;
  global.Event = dom.window.Event;
  global.CustomEvent = dom.window.CustomEvent;
  global.Node = dom.window.Node;
  global.MouseEvent = dom.window.MouseEvent;

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

  return {
    dom,
    selectors,
    cleanup() {
      dom.window.close();
      delete global.window;
      delete global.document;
      delete global.HTMLElement;
      delete global.Event;
      delete global.CustomEvent;
      delete global.Node;
      delete global.MouseEvent;
    },
  };
}

function createStoreWithRenderer(selectors) {
  const store = createShellStateStore({
    workspaces: [
      { id: 'ws-main', name: 'Main' },
      { id: 'ws-focus', name: 'Focus' },
    ],
    activeWorkspaceId: 'ws-main',
  });
  const renderShell = createRenderer(selectors);
  store.subscribe(renderShell);
  return store;
}

function delay() {
  return new Promise((resolve) => setTimeout(resolve, 0));
}

test('rendering two windows produces two taskbar items', (t) => {
  const { selectors, cleanup } = setupDom();
  t.after(cleanup);

  const store = createStoreWithRenderer(selectors);

  store.upsertWindow({ hwnd: 'hwnd-1', title: 'Alpha', workspaceId: 'ws-main' });
  store.upsertWindow({ hwnd: 'hwnd-2', title: 'Beta', workspaceId: 'ws-main' });

  const items = selectors.taskbarWindows.querySelectorAll('.taskbar-item');
  assert.equal(items.length, 2, 'expected two taskbar items');
  assert.ok(items[0].getAttribute('aria-label')?.includes('Alpha'));
  assert.ok(items[1].getAttribute('aria-label')?.includes('Beta'));
});

test('clicking a taskbar item delegates to shellBridge.focusWindow', async (t) => {
  const { dom, selectors, cleanup } = setupDom();
  t.after(cleanup);

  const store = createStoreWithRenderer(selectors);
  wireInteractions(selectors, store);

  store.upsertWindow({ hwnd: 'hwnd-201', title: 'Docs', workspaceId: 'ws-main', state: 'normal' });

  const button = selectors.taskbarWindows.querySelector('.taskbar-item');
  assert.ok(button, 'taskbar item should be rendered');

  const focusCalls = [];
  const originalFocus = shellBridge.focusWindow;
  const originalRestore = shellBridge.restoreWindow;
  shellBridge.focusWindow = async (hwnd) => {
    focusCalls.push(hwnd);
    return true;
  };
  shellBridge.restoreWindow = async () => true;

  t.after(() => {
    shellBridge.focusWindow = originalFocus;
    shellBridge.restoreWindow = originalRestore;
  });

  button.dispatchEvent(new dom.window.MouseEvent('click', { bubbles: true }));
  await delay();

  assert.deepEqual(focusCalls, ['hwnd-201']);
});
