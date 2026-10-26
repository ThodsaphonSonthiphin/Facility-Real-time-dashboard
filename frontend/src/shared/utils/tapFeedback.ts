const TAPPED = 'is-tapped';
/** Button and the button-styled links, which carry `data-tap`. */
const PRESSABLE = '[data-tap]';

/**
 * A quick tap is over before `:active` can be seen, so every press of a button-styled element (buttons and
 * button-styled links) plays a short press animation to its end (owner, 2026-10-21). The animation is in index.css.
 * One listener on the document covers every page; main.tsx installs it once. Returns the uninstall.
 */
export function installTapFeedback(root: Document): () => void {
  const play = (event: Event) => {
    const target = event.target instanceof Element ? event.target.closest<HTMLElement>(PRESSABLE) : null;
    if (!target || target.matches(':disabled, [aria-disabled="true"]')) {
      return;
    }
    // Restart the animation when a second tap lands before the first one ends
    target.classList.remove(TAPPED);
    void target.offsetWidth;
    target.classList.add(TAPPED);
  };
  const done = (event: Event) => {
    if (event.target instanceof Element) {
      event.target.classList.remove(TAPPED);
    }
  };
  root.addEventListener('pointerdown', play, true);
  root.addEventListener('animationend', done, true);
  return () => {
    root.removeEventListener('pointerdown', play, true);
    root.removeEventListener('animationend', done, true);
  };
}
