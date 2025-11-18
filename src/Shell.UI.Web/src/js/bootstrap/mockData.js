export function bootstrapMockDataIfNeeded(store) {
  const shellApi = window.shell ?? null;
  if (shellApi) {
    store.setStatusText('Connected to Shell Core');
    return;
  }

  store.setStatusText('Mock data mode');
  const workspaces = [
    { id: 'ws-1', name: 'Workspace 1' },
    { id: 'ws-2', name: 'Workspace 2' },
    { id: 'ws-3', name: 'Workspace 3' },
  ];
  const windows = [
    { hwnd: '0x1', title: 'Code Editor', state: 'Maximized', workspaceId: 'ws-1' },
    { hwnd: '0x2', title: 'Documentation', state: 'Normal', workspaceId: 'ws-1' },
    { hwnd: '0x3', title: 'Music Player', state: 'Minimized', workspaceId: 'ws-2' },
  ];
  const trayIcons = [
    { id: 'network', title: 'Network', emoji: '🌐' },
    { id: 'battery', title: 'Battery', emoji: '🔋' },
    { id: 'sound', title: 'Volume', emoji: '🔊' },
  ];
  const apps = [
    { id: 'notepad', name: 'Notepad', description: 'Simple text editor' },
    { id: 'terminal', name: 'Terminal', description: 'Command line' },
    { id: 'browser', name: 'Browser', description: 'Surf the web' },
    { id: 'music', name: 'Music', description: 'Play your tunes' },
  ];

  store.setWorkspaces(workspaces);
  store.setActiveWorkspace('ws-1');
  store.setWindows(windows);
  store.setFocusedWindow('0x1');
  store.setTrayIcons(trayIcons);
  // Only set launcher apps in mock mode as fallback
  store.setLauncherApps(apps);
}
