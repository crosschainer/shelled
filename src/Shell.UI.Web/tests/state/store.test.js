import test from 'node:test';
import assert from 'node:assert/strict';
import { createShellStateStore } from '../../src/js/state/store.js';

test('upsertWindow adds and updates windows while notifying subscribers', () => {
  const store = createShellStateStore();
  let lastState;
  store.subscribe((snapshot) => {
    lastState = snapshot;
  });

  store.upsertWindow({ hwnd: 100, title: 'Alpha', workspaceId: 'ws-1' });
  assert.equal(lastState.windows.length, 1);
  assert.deepEqual(lastState.windows[0], {
    hwnd: 100,
    title: 'Alpha',
    workspaceId: 'ws-1',
  });

  store.upsertWindow({ hwnd: 100, title: 'Alpha Updated' });
  assert.equal(lastState.windows.length, 1);
  assert.equal(lastState.windows[0].title, 'Alpha Updated');
});

test('removeWindow deletes a window and leaves others intact', () => {
  const store = createShellStateStore();
  let lastState;
  store.subscribe((snapshot) => {
    lastState = snapshot;
  });

  store.upsertWindow({ hwnd: 100, title: 'Alpha' });
  store.upsertWindow({ hwnd: 200, title: 'Beta' });
  store.removeWindow(100);

  assert.equal(lastState.windows.length, 1);
  assert.equal(lastState.windows[0].hwnd, 200);
});

test('workspaces and active workspace are managed consistently', () => {
  const store = createShellStateStore();
  let lastState;
  store.subscribe((snapshot) => {
    lastState = snapshot;
  });

  store.setWorkspaces([
    { id: 'ws-1', name: 'Main' },
    { id: 'ws-2', name: 'Focus' },
  ]);
  assert.equal(lastState.workspaces.length, 2);
  assert.equal(lastState.activeWorkspaceId, 'ws-1');

  store.setActiveWorkspace('ws-2');
  assert.equal(lastState.activeWorkspaceId, 'ws-2');
});

test('tray icons and focus updates clone state safely', () => {
  const store = createShellStateStore();
  let snapshots = [];
  store.subscribe((snapshot) => {
    snapshots.push(snapshot);
  });

  store.setTrayIcons([{ id: 'net', title: 'Network' }]);
  store.setFocusedWindow(1234);

  const latest = snapshots.at(-1);
  assert.equal(latest.trayIcons.length, 1);
  assert.equal(latest.trayIcons[0].id, 'net');
  assert.equal(latest.focusedWindowHandle, 1234);

  // mutate snapshot to ensure store state is immutable from outside consumers
  latest.trayIcons[0].id = 'modified';
  const freshState = store.getState();
  assert.equal(freshState.trayIcons[0].id, 'net');
});

test('toggleLauncher flips the flag or respects forced state', () => {
  const store = createShellStateStore();
  store.toggleLauncher();
  assert.equal(store.getState().isLauncherOpen, true);
  store.toggleLauncher(false);
  assert.equal(store.getState().isLauncherOpen, false);
});
