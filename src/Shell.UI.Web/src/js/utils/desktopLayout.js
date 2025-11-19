const STORAGE_KEY = 'shelled.desktopLayout.v2';

// Base grid metrics. These roughly match the desktop icon card size + spacing.
const BASE_CELL_WIDTH = 96;
const BASE_CELL_HEIGHT = 110;
const CELL_GAP_X = 16;
const CELL_GAP_Y = 16;

function canUseStorage() {
  try {
    return typeof window !== 'undefined' && !!window.localStorage;
  } catch {
    return false;
  }
}

function loadRawLayout() {
  if (!canUseStorage()) {
    return {};
  }

  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (!raw) return {};
    const parsed = JSON.parse(raw);
    if (!parsed || typeof parsed !== 'object') return {};
    const layout = {};
    for (const [path, cell] of Object.entries(parsed)) {
      if (!cell || typeof cell !== 'object') continue;
      const col = Number(cell.col);
      const row = Number(cell.row);
      if (!Number.isFinite(col) || !Number.isFinite(row)) continue;
      layout[path] = { col, row };
    }
    return layout;
  } catch {
    return {};
  }
}

function saveRawLayout(layout) {
  if (!canUseStorage()) {
    return;
  }

  try {
    const payload = {};
    if (layout && typeof layout === 'object') {
      for (const [path, cell] of Object.entries(layout)) {
        if (!cell || typeof cell !== 'object') continue;
        const col = Number(cell.col);
        const row = Number(cell.row);
        if (!Number.isFinite(col) || !Number.isFinite(row)) continue;
        payload[path] = { col, row };
      }
    }
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(payload));
  } catch {
    // Best-effort persistence; ignore errors.
  }
}

function getGridMetrics(container) {
  const rect = container && container.getBoundingClientRect
    ? container.getBoundingClientRect()
    : { width: 0, height: 0 };

  const width = rect.width && Number.isFinite(rect.width) && rect.width > 0
    ? rect.width
    : 960;

  const cellWidth = BASE_CELL_WIDTH + CELL_GAP_X;
  const cellHeight = BASE_CELL_HEIGHT + CELL_GAP_Y;
  const columns = Math.max(1, Math.floor(width / cellWidth));

  return {
    cellWidth,
    cellHeight,
    columns,
    originLeft: 8,
    originTop: 8,
  };
}

function keyForCell(col, row) {
  return `${col},${row}`;
}

function ensureLayout(desktopItems, container) {
  const metrics = getGridMetrics(container);
  const layout = loadRawLayout();

  if (!Array.isArray(desktopItems) || desktopItems.length === 0) {
    return { layout: {}, metrics };
  }

  const paths = new Set();
  for (const item of desktopItems) {
    if (item && typeof item.path === 'string') {
      paths.add(item.path);
    }
  }

  // Remove entries for items that no longer exist.
  for (const existingPath of Object.keys(layout)) {
    if (!paths.has(existingPath)) {
      delete layout[existingPath];
    }
  }

  const occupied = new Set();
  for (const cell of Object.values(layout)) {
    if (!cell || typeof cell !== 'object') continue;
    const col = Number(cell.col);
    const row = Number(cell.row);
    if (!Number.isFinite(col) || !Number.isFinite(row)) continue;
    occupied.add(keyForCell(col, row));
  }

  function nextFreeCell() {
    let row = 0;
    let col = 0;

    while (true) {
      const key = keyForCell(col, row);
      if (!occupied.has(key)) {
        occupied.add(key);
        return { col, row };
      }
      col += 1;
      if (col >= metrics.columns) {
        col = 0;
        row += 1;
      }
    }
  }

  for (const item of desktopItems) {
    const path = item && item.path;
    if (typeof path !== 'string') continue;
    if (!layout[path]) {
      layout[path] = nextFreeCell();
    }
  }

  saveRawLayout(layout);
  return { layout, metrics };
}

export function getDesktopPositions(desktopItems, container) {
  const { layout, metrics } = ensureLayout(desktopItems, container);

  const positions = {};
  for (const item of desktopItems || []) {
    if (!item || typeof item.path !== 'string') continue;
    const cell = layout[item.path];
    if (!cell) continue;
    const left = metrics.originLeft + cell.col * metrics.cellWidth;
    const top = metrics.originTop + cell.row * metrics.cellHeight;
    positions[item.path] = { left, top };
  }

  return { layout, metrics, positions };
}

export function moveDesktopSelection(desktopItems, draggedPaths, deltaX, deltaY, container, baseLayout, baseMetrics) {
  if (!Array.isArray(desktopItems) || desktopItems.length === 0) {
    return null;
  }

  const draggedSet = new Set(
    Array.isArray(draggedPaths) ? draggedPaths.filter((p) => typeof p === 'string') : [],
  );
  if (draggedSet.size === 0) {
    return null;
  }

  const { layout: initialLayout, metrics } =
    baseLayout && baseMetrics
      ? { layout: { ...baseLayout }, metrics: baseMetrics }
      : ensureLayout(desktopItems, container);

  const colOffset = Math.round(deltaX / metrics.cellWidth);
  const rowOffset = Math.round(deltaY / metrics.cellHeight);

  if (colOffset === 0 && rowOffset === 0) {
    return { layout: initialLayout, metrics };
  }

  const newLayout = { ...initialLayout };

  const occupied = new Set();
  for (const [path, cell] of Object.entries(initialLayout)) {
    if (!cell || typeof cell !== 'object') continue;
    if (draggedSet.has(path)) continue;
    const col = Number(cell.col);
    const row = Number(cell.row);
    if (!Number.isFinite(col) || !Number.isFinite(row)) continue;
    occupied.add(keyForCell(col, row));
  }

  const draggedEntries = Object.entries(initialLayout)
    .filter(([path]) => draggedSet.has(path))
    .map(([path, cell]) => ({
      path,
      col: Number(cell.col) || 0,
      row: Number(cell.row) || 0,
    }))
    .sort((a, b) => {
      if (a.row !== b.row) return a.row - b.row;
      if (a.col !== b.col) return a.col - b.col;
      return a.path.localeCompare(b.path);
    });

  for (const entry of draggedEntries) {
    let targetCol = entry.col + colOffset;
    let targetRow = entry.row + rowOffset;

    if (targetCol < 0) targetCol = 0;
    if (targetRow < 0) targetRow = 0;

    let key = keyForCell(targetCol, targetRow);
    while (occupied.has(key)) {
      targetRow += 1;
      key = keyForCell(targetCol, targetRow);
    }

    newLayout[entry.path] = { col: targetCol, row: targetRow };
    occupied.add(key);
  }

  saveRawLayout(newLayout);
  return { layout: newLayout, metrics };
}

export function sortItemsByLayout(desktopItems) {
  if (!Array.isArray(desktopItems) || desktopItems.length === 0) {
    return [];
  }

  const layout = loadRawLayout();

  return [...desktopItems].sort((a, b) => {
    const cellA = layout[a.path] || { col: 0, row: 0 };
    const cellB = layout[b.path] || { col: 0, row: 0 };

    if (cellA.row !== cellB.row) return cellA.row - cellB.row;
    if (cellA.col !== cellB.col) return cellA.col - cellB.col;

    const nameA = a.name || a.path || '';
    const nameB = b.name || b.path || '';
    return String(nameA).localeCompare(String(nameB));
  });
}
