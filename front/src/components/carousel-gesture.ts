/** Short and predominantly vertical gestures belong to page scrolling, not card navigation. */
export function swipeStep(deltaX: number, deltaY: number, width: number): number {
  const threshold = Math.min(64, width * 0.18);
  return Math.abs(deltaX) >= threshold && Math.abs(deltaX) > Math.abs(deltaY) * 1.2
    ? -Math.sign(deltaX)
    : 0;
}
