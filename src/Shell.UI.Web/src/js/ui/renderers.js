export function createRenderer(selectors) {
  return function renderShell(state) {
    renderStatus(state);
    renderWorkspaces(state);
    renderTaskbarWindows(state);
    renderTrayIcons(state);
    renderLauncher(state);
  };

  function renderStatus(state) {
    selectors.status.textContent = state.statusText;
  }

  function renderWorkspaces(state) {
    const { workspaces, activeWorkspaceId, windows } = state;
    selectors.workspaceStrip.replaceChildren(
      ...workspaces.map((workspace) => {
        const indicator = document.createElement('button');
        indicator.type = 'button';
        indicator.className =
          'workspace-indicator' + (workspace.id === activeWorkspaceId ? ' active' : '');
        indicator.dataset.workspaceId = workspace.id;
        indicator.title = `Switch to ${workspace.name}`;
        const isActive = workspace.id === activeWorkspaceId;
        indicator.setAttribute('aria-pressed', String(isActive));
        indicator.setAttribute('aria-current', isActive ? 'true' : 'false');
        const count = windows.filter((w) => w.workspaceId === workspace.id).length;
        indicator.innerHTML = `
          <strong>${workspace.name}</strong>
          <span class="count">${count} window${count === 1 ? '' : 's'}</span>
        `;
        return indicator;
      }),
    );
  }

  function renderTaskbarWindows(state) {
    const { windows, activeWorkspaceId, focusedWindowHandle } = state;
    const scopedWindows = windows.filter((w) => w.workspaceId === activeWorkspaceId);
    if (scopedWindows.length === 0) {
      const emptyState = document.createElement('p');
      emptyState.className = 'placeholder-text';
      emptyState.textContent = 'No windows in this workspace yet.';
      selectors.taskbarWindows.replaceChildren(emptyState);
      return;
    }

    const nodes = scopedWindows.map((windowModel) => {
      const button = document.createElement('button');
      button.type = 'button';
      const classes = ['taskbar-item'];
      const isFocused = windowModel.hwnd === focusedWindowHandle;
      if (isFocused) {
        classes.push('focused');
      }
      button.className = classes.join(' ');
      button.dataset.hwnd = windowModel.hwnd;
      button.title = windowModel.title ?? windowModel.appId ?? 'Window';
      button.setAttribute('aria-pressed', String(isFocused));
      button.setAttribute('aria-label', `Focus ${windowModel.title ?? windowModel.appId ?? 'window'}`);
      button.innerHTML = `
        <span class="title">${windowModel.title ?? windowModel.appId ?? 'Window'}</span>
        <span class="badge">${windowModel.state ?? ''}</span>
      `;
      return button;
    });
    selectors.taskbarWindows.replaceChildren(...nodes);
  }

  function renderTrayIcons(state) {
    if (state.trayIcons.length === 0) {
      selectors.trayIcons.replaceChildren();
      return;
    }

    const nodes = state.trayIcons.map((icon) => {
      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'tray-button btn-circle btn-icon';
      button.dataset.trayId = icon.id;
      button.title = icon.tooltip ?? icon.title ?? icon.id;
      button.setAttribute('aria-label', icon.tooltip ?? icon.title ?? icon.id);
      
      // Handle different icon types
      if (icon.iconData) {
        // If we have icon data (base64), create an image
        const img = document.createElement('img');
        img.src = `data:image/png;base64,${icon.iconData}`;
        img.alt = icon.tooltip ?? icon.title ?? icon.id;
        img.className = 'tray-icon-image';
        button.appendChild(img);
      } else if (icon.emoji) {
        // Use emoji if available
        button.textContent = icon.emoji;
      } else {
        // Fallback to first letter or generic icon
        button.textContent = icon.title?.charAt(0)?.toUpperCase() ?? '•';
      }
      
      // Add visual state indicators
      if (!icon.isVisible) {
        button.classList.add('hidden-tray-icon');
      }
      
      return button;
    });
    selectors.trayIcons.replaceChildren(...nodes);
  }

  function renderLauncher(state) {
    selectors.launcherOverlay.classList.toggle('open', state.isLauncherOpen);
    selectors.launcherOverlay.setAttribute('aria-hidden', state.isLauncherOpen ? 'false' : 'true');
    selectors.launcherToggle.setAttribute('aria-expanded', state.isLauncherOpen ? 'true' : 'false');

    if (!state.isLauncherOpen) {
      return;
    }

    const cards = state.launcherApps.map((app) => {
      const card = document.createElement('button');
      card.type = 'button';
      card.className = 'launcher-card card card--app';
      card.dataset.appId = app.id;
      card.setAttribute('aria-label', `Launch ${app.name}`);
      card.innerHTML = `
        <strong>${app.name}</strong>
        <p>${app.description ?? ''}</p>
      `;
      return card;
    });
    selectors.launcherGrid.replaceChildren(...cards);
  }
}
