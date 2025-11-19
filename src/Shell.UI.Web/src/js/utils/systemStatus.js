import shellBridge from '../bridge/api.js';

export function bootstrapSystemStatus(selectors) {
  const {
    clock,
    networkIndicator,
    volumeIndicator,
    volumeSlider,
    volumePercentLabel,
    volumeMuteToggle,
  } = selectors;

  if (!clock && !networkIndicator && !volumeIndicator) {
    return;
  }

  let isStopped = false;

  const applyStatus = (status) => {
    if (!status || typeof status !== 'object') return;

    if (clock && typeof status.localTime === 'string' && status.localTime) {
      clock.textContent = status.localTime;
    }

    if (networkIndicator && status.network) {
      const { kind, isConnected } = status.network;
      const baseClass = 'system-indicator system-indicator--network';
      const variant = kind ? ` system-indicator--network-${kind}` : '';
      networkIndicator.className = baseClass + variant;

      const label = !isConnected
        ? 'No network connection'
        : kind === 'wifi'
          ? 'Wi-Fi connected'
          : kind === 'ethernet'
            ? 'Ethernet connected'
            : 'Network status';

      networkIndicator.setAttribute('aria-label', label);
      networkIndicator.title = label;
    }

    if (status.volume) {
      const { levelPercent, isMuted } = status.volume;
      const baseClass = 'system-indicator system-indicator--volume';
      const variant = isMuted ? ' system-indicator--volume-muted' : ' system-indicator--volume-unmuted';
      const safeLevel =
        typeof levelPercent === 'number' && !Number.isNaN(levelPercent)
          ? Math.max(0, Math.min(100, levelPercent))
          : 0;

      if (volumeIndicator) {
        volumeIndicator.className = baseClass + variant;
        const label = isMuted ? 'Volume muted' : `Volume ${safeLevel}%`;
        volumeIndicator.setAttribute('aria-label', label);
        volumeIndicator.title = label;
      }

      if (volumeSlider) {
        volumeSlider.value = String(safeLevel);
      }

      if (volumePercentLabel) {
        volumePercentLabel.textContent = `${safeLevel}%`;
      }

      if (volumeMuteToggle) {
        volumeMuteToggle.classList.toggle('volume-flyout__mute--muted', !!isMuted);
      }
    }
  };

  const tick = async () => {
    if (isStopped) return;

    try {
      const status = await shellBridge.getSystemStatus();
      applyStatus(status);
    } catch (error) {
      console.error('Error refreshing system status:', error);
    }

    if (!isStopped) {
      setTimeout(tick, 5000);
    }
  };

  tick();

  return () => {
    isStopped = true;
  };
}
