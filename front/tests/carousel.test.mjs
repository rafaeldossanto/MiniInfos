import { test } from 'node:test';
import assert from 'node:assert/strict';
import { swipeStep } from '../src/components/carousel-gesture.ts';

test('a deliberate horizontal gesture opens the next card or returns to the previous one', () => {
  assert.equal(swipeStep(-120, 5, 500), 1);
  assert.equal(swipeStep(120, -5, 500), -1);
});

test('taps, short drags and vertical page scrolling do not switch cards', () => {
  assert.equal(swipeStep(0, 0, 375), 0);
  assert.equal(swipeStep(-18, 3, 375), 0);
  assert.equal(swipeStep(90, 120, 375), 0);
  assert.equal(swipeStep(90, -120, 375), 0);
  assert.equal(swipeStep(-64, 60, 375), 0);
});

test('the swipe distance adapts to a narrow card without requiring a full width drag', () => {
  assert.equal(swipeStep(-50, 3, 260), 1);
  assert.equal(swipeStep(-50, 3, 560), 0);
});
