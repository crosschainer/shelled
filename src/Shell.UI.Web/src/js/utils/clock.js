export function bootstrapClock(element) {
  if (!element) return;

  let timeoutId = null;

  const tick = () => {
    const now = new Date();
    element.textContent = now.toLocaleTimeString([], {
      hour: '2-digit',
      minute: '2-digit',
    });

    const msToNextSecond = 1000 - now.getMilliseconds();
    timeoutId = window.setTimeout(tick, msToNextSecond);
  };

  tick();

  document.addEventListener('visibilitychange', () => {
    if (document.hidden) {
      if (timeoutId !== null) {
        clearTimeout(timeoutId);
        timeoutId = null;
      }
      return;
    }

    // When returning to the desktop, resync the clock to the
    // current system time and re-align to the next whole second.
    tick();
  });
}
