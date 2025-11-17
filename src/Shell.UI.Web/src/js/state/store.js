export function createShellStateStore(initialState = {}) {
  const defaultState = {
    windows: [],
    workspaces: [],
    trayIcons: [],
    launcherApps: [],
    activeWorkspaceId: null,
    focusedWindowHandle: null,
    statusText: 'Disconnected',
    isLauncherOpen: false,
  };

  const state = { ...defaultState, ...initialState };
  const listeners = new Set();

  const cloneState = () => ({
    ...state,
    windows: state.windows.map((w) => ({ ...w })),
    workspaces: state.workspaces.map((w) => ({ ...w })),
    trayIcons: state.trayIcons.map((i) => ({ ...i })),
    launcherApps: state.launcherApps.map((a) => ({ ...a })),
  });

  const notify = () => {
    const snapshot = cloneState();
    listeners.forEach((listener) => listener(snapshot));
  };

  return {
    getState: cloneState,
    subscribe(listener) {
      listeners.add(listener);
      listener(cloneState());
      return () => listeners.delete(listener);
    },
    setState(patch) {
      Object.assign(state, patch);
      notify();
    },
    setWindows(windows) {
      state.windows = [...windows];
      notify();
    },
    upsertWindow(windowModel) {
      const index = state.windows.findIndex((w) => w.hwnd === windowModel.hwnd);
      if (index >= 0) {
        state.windows[index] = { ...state.windows[index], ...windowModel };
      } else {
        state.windows.push({ ...windowModel });
      }
      notify();
    },
    removeWindow(hwnd) {
      state.windows = state.windows.filter((w) => `${w.hwnd}` !== `${hwnd}`);
      notify();
    },
    setWorkspaces(workspaces) {
      state.workspaces = [...workspaces];
      if (!state.activeWorkspaceId && state.workspaces.length > 0) {
        state.activeWorkspaceId = state.workspaces[0].id;
      }
      notify();
    },
    setActiveWorkspace(id) {
      state.activeWorkspaceId = id;
      notify();
    },
    setTrayIcons(trayIcons) {
      state.trayIcons = [...trayIcons];
      notify();
    },
    setFocusedWindow(hwnd) {
      state.focusedWindowHandle = hwnd;
      notify();
    },
    setStatusText(text) {
      state.statusText = text;
      notify();
    },
    setLauncherApps(apps) {
      state.launcherApps = [...apps];
      notify();
    },
    toggleLauncher(forceState) {
      const next = typeof forceState === 'boolean' ? forceState : !state.isLauncherOpen;
      state.isLauncherOpen = next;
      notify();
    },
  };
}
