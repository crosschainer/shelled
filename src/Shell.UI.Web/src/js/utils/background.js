/**
 * Apply a Windows-style desktop background to the shell root element.
 * The info object comes from shellBridge.getDesktopBackgroundInfo().
 */
export function applyDesktopBackground(info, desktopElement) {
  if (!info || typeof info !== 'object') return;

  const desktop = desktopElement || document.getElementById('desktop');
  if (!desktop) return;

  const { wallpaperUrl, wallpaperStyle, backgroundColor, hasWallpaper } = info;

  // Always set a fallback background color
  if (backgroundColor) {
    desktop.style.backgroundColor = backgroundColor;
  }

  if (!hasWallpaper || !wallpaperUrl) {
    // Keep the default gradient background
    return;
  }

  let size = 'cover';
  let repeat = 'no-repeat';
  let position = 'center center';

  switch (wallpaperStyle) {
    case 'tile':
      size = 'auto';
      repeat = 'repeat';
      break;
    case 'fit':
      size = 'contain';
      repeat = 'no-repeat';
      break;
    case 'stretch':
      size = '100% 100%';
      repeat = 'no-repeat';
      break;
    case 'center':
      size = 'auto';
      repeat = 'no-repeat';
      break;
    case 'span':
      size = 'cover';
      repeat = 'no-repeat';
      break;
    default:
      size = 'cover';
      repeat = 'no-repeat';
      break;
  }

  desktop.style.backgroundImage = `url('${wallpaperUrl}')`;
  desktop.style.backgroundSize = size;
  desktop.style.backgroundRepeat = repeat;
  desktop.style.backgroundPosition = position;
}

