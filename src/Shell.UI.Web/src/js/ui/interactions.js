export function wireInteractions(selectors, store) {
  selectors.launcherToggle.addEventListener('click', () => store.toggleLauncher());
  selectors.launcherClose.addEventListener('click', () => store.toggleLauncher(false));
  selectors.launcherOverlay.addEventListener('click', (event) => {
    if (event.target === selectors.launcherOverlay) {
      store.toggleLauncher(false);
    }
  });

  document.addEventListener('keydown', (event) => {
    if (event.key === 'Escape') {
      store.toggleLauncher(false);
    }
  });

  selectors.workspaceStrip.addEventListener('click', (event) => {
    const target = event.target.closest('[data-workspace-id]');
    if (!target) return;
    store.setActiveWorkspace(target.dataset.workspaceId);
  });

  selectors.taskbarWindows.addEventListener('click', (event) => {
    const target = event.target.closest('[data-hwnd]');
    if (!target) return;
    store.setFocusedWindow(target.dataset.hwnd);
  });

  selectors.trayIcons.addEventListener('click', (event) => {
    const target = event.target.closest('[data-tray-id]');
    if (!target) return;
    console.log('Tray icon clicked:', target.dataset.trayId);
  });

  selectors.launcherGrid.addEventListener('click', (event) => {
    const target = event.target.closest('[data-app-id]');
    if (!target) return;
    console.log('Launcher app selected:', target.dataset.appId);
  });
}
