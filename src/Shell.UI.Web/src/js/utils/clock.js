export function bootstrapClock(element) {
  if (!element) return;
  const update = () => {
    const now = new Date();
    element.textContent = now.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  };
  update();
  setInterval(update, 1000);
}
