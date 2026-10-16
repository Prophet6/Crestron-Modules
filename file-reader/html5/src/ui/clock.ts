export function startClock(timeEl: HTMLElement, dateEl: HTMLElement): void {
  const tick = () => {
    const now = new Date();
    timeEl.textContent = new Intl.DateTimeFormat(undefined, {
      hour: 'numeric',
      minute: '2-digit',
    }).format(now);
    dateEl.textContent = new Intl.DateTimeFormat(undefined, {
      weekday: 'short',
      month: 'short',
      day: 'numeric',
    }).format(now);
  };

  tick();
  window.setInterval(tick, 5000);
}
