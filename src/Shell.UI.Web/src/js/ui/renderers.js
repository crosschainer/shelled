export function createRenderer(selectors) {
  return function renderShell(state) {
    renderStatus(state);
    renderLoading(state);
    renderDesktop(state);
    renderWorkspaces(state);
    renderTaskbarWindows(state);
    renderTrayIcons(state);
    renderLauncher(state);
  };

  function renderStatus(state) {
    if (!selectors.status) return;

    const text = state.statusText;

    // Only show status text when something interesting is happening.
    // For the normal steady state we hide the chip entirely.
    if (text === 'Connected') {
      selectors.status.textContent = '';
      selectors.status.style.display = 'none';
      selectors.status.setAttribute('aria-hidden', 'true');
      return;
    }

    selectors.status.textContent = text;
    selectors.status.style.display = '';
    selectors.status.removeAttribute('aria-hidden');
  }

  function renderLoading(state) {
    if (!selectors.loadingOverlay) return;

    const isVisible = Boolean(state.isBootstrapping);
    selectors.loadingOverlay.style.display = isVisible ? 'flex' : 'none';
    selectors.loadingOverlay.setAttribute('aria-hidden', isVisible ? 'false' : 'true');

    if (isVisible && selectors.status) {
      const text = state.statusText || 'Starting shell...';
      const label = selectors.loadingOverlay.querySelector('.shell-loading__status');
      if (label) {
        label.textContent = text;
      }
    }
  }

  function renderDesktop(state) {
    const container = selectors.desktopIcons ?? selectors.desktopSpace;
    if (!container) return;

    const { desktopItems } = state;
    if (!desktopItems || desktopItems.length === 0) {
      container.replaceChildren();
      return;
    }

    const { positions } = getDesktopPositions(desktopItems, container);
    const selectedSet = new Set(state.selectedDesktopPaths || []);

    const nodes = desktopItems.map((item) => {
      const wrapper = document.createElement('button');
      wrapper.type = 'button';
      wrapper.className =
        'desktop-icon' + (selectedSet.has(item.path) ? ' desktop-icon--selected' : '');
      wrapper.dataset.desktopPath = item.path;
      wrapper.title = item.name;

      const pos = positions[item.path];
      if (pos) {
        wrapper.style.position = 'absolute';
        wrapper.style.left = `${pos.left}px`;
        wrapper.style.top = `${pos.top}px`;
      }

      const iconWrapper = document.createElement('div');
      iconWrapper.className = 'desktop-icon-image-wrapper';

      // Prefer native icon data when available so the Recycle Bin matches the
      // host OS (Windows 11/10 etc.). Fall back to a vector glyph only when
      // no icon data is available for the Recycle Bin.
      if (item.iconData) {
        const img = document.createElement('img');
        img.src = `data:image/png;base64,${item.iconData}`;
        img.alt = '';
        img.className = 'desktop-icon-image';
        iconWrapper.appendChild(img);
      } else if (item.path === 'shell:RecycleBinFolder') {
        const svgNS = 'http://www.w3.org/2000/svg';
        const svg = document.createElementNS(svgNS, 'svg');
        svg.setAttribute('viewBox', '0 0 24 24');
        svg.setAttribute('aria-hidden', 'true');
        svg.setAttribute('class', 'desktop-icon-image');

        const body = document.createElementNS(svgNS, 'path');
        body.setAttribute(
          'd',
          'M7 8h10l-1 10.5A2 2 0 0 1 14 20H10a2 2 0 0 1-2-1.5L7 8z',
        );
        body.setAttribute('fill', 'currentColor');

        const rim = document.createElementNS(svgNS, 'rect');
        rim.setAttribute('x', '6');
        rim.setAttribute('y', '6');
        rim.setAttribute('width', '12');
        rim.setAttribute('height', '2');
        rim.setAttribute('rx', '1');
        rim.setAttribute('fill', 'currentColor');

        const lid = document.createElementNS(svgNS, 'path');
        lid.setAttribute('d', 'M9 5.5 9.8 4h4.4L15 5.5');
        lid.setAttribute('fill', 'currentColor');

        svg.appendChild(body);
        svg.appendChild(rim);
        svg.appendChild(lid);
        iconWrapper.appendChild(svg);
      } else {
        const fallback = document.createElement('span');
        fallback.className = 'desktop-icon-fallback';
        fallback.textContent = item.name?.charAt(0)?.toUpperCase() ?? '•';
        iconWrapper.appendChild(fallback);
      }

      const label = document.createElement('span');
      label.className = 'desktop-icon-label';
      label.textContent = item.name;

      wrapper.appendChild(iconWrapper);
      wrapper.appendChild(label);
      return wrapper;
    });

    container.replaceChildren(...nodes);
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
    const { windows, activeWorkspaceId, focusedWindowHandle, launcherApps } = state;
    const scopedWindows = windows.filter(
      (w) => w.workspaceId === activeWorkspaceId && w.isVisible !== false,
    );
    if (scopedWindows.length === 0) {
      const emptyState = document.createElement('p');
      emptyState.className = 'placeholder-text';
      emptyState.textContent = 'No windows in this workspace yet.';
      selectors.taskbarWindows.replaceChildren(emptyState);
      return;
    }

    const groupsByApp = new Map();

    for (const windowModel of scopedWindows) {
      const hasAppId = Boolean(windowModel.appId);
      const key = hasAppId ? windowModel.appId : windowModel.hwnd;
      let group = groupsByApp.get(key);
      if (!group) {
        const launcherMeta =
          hasAppId && Array.isArray(launcherApps) && launcherApps.length > 0
            ? launcherApps.find((app) => app.id === windowModel.appId)
            : null;

        group = {
          appId: hasAppId ? windowModel.appId : null,
          key,
          windows: [],
          title:
            launcherMeta?.name ||
            windowModel.title ||
            windowModel.appId ||
            windowModel.className ||
            'Window',
          iconData: windowModel.iconData || launcherMeta?.iconData || null,
          isFocused: false,
        };

        groupsByApp.set(key, group);
      }

      group.windows.push(windowModel);
      if (!group.iconData && windowModel.iconData) {
        group.iconData = windowModel.iconData;
      }
      if (windowModel.hwnd === focusedWindowHandle) {
        group.isFocused = true;
      }
    }

    const nodes = Array.from(groupsByApp.values()).map((group) => {
      const button = document.createElement('button');
      button.type = 'button';
      const classes = ['taskbar-item'];
      if (group.isFocused) {
        classes.push('focused');
      }
      button.className = classes.join(' ');
      if (group.appId) {
        button.dataset.appId = group.appId;
      }
      button.dataset.groupKey = group.key;
      button.dataset.groupSize = String(group.windows.length);
      button.title = group.title;
      const ariaLabelBase = group.title || 'Application';
      const ariaLabel =
        group.windows.length > 1
          ? `${ariaLabelBase} (${group.windows.length} windows)`
          : `Focus ${ariaLabelBase}`;
      button.setAttribute('aria-label', ariaLabel);

      const iconWrapper = document.createElement('div');
      iconWrapper.className = 'taskbar-item__icon';

      if (group.iconData) {
        const img = document.createElement('img');
        img.src = `data:image/png;base64,${group.iconData}`;
        img.alt = '';
        img.className = 'taskbar-item__icon-image';
        iconWrapper.appendChild(img);
      } else {
        const fallback = document.createElement('span');
        fallback.className = 'taskbar-item__icon-fallback';
        const initial = group.title?.charAt(0)?.toUpperCase() ?? '?';
        fallback.textContent = initial;
        iconWrapper.appendChild(fallback);
      }

      button.appendChild(iconWrapper);

      if (group.windows.length > 1) {
        const count = document.createElement('span');
        count.className = 'taskbar-item__count';
        count.textContent = String(group.windows.length);
        button.appendChild(count);
      }

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

      const initial = app.name?.charAt(0)?.toUpperCase() ?? '?';
      const iconHtml = app.iconData
        ? `<img class="launcher-card__icon-image" src="data:image/png;base64,${app.iconData}" alt="">`
        : `<span class="launcher-card__icon-fallback">${initial}</span>`;

      card.innerHTML = `
        <div class="launcher-card__icon">
          ${iconHtml}
        </div>
        <div class="launcher-card__title">
          <strong>${app.name}</strong>
          <p>${app.description ?? ''}</p>
        </div>
      `;
      return card;
    });
    selectors.launcherGrid.replaceChildren(...cards);
  }
}
import { getDesktopPositions } from '../utils/desktopLayout.js';
