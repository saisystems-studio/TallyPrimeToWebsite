import test from 'node:test';
import assert from 'node:assert/strict';
import { getInstallInstructions } from './installInstructions.js';

test('install fallback provides browser specific Chrome, Edge, and Firefox guidance', () => {
  assert.match(getInstallInstructions('Mozilla/5.0 Chrome/130.0.0.0 Safari/537.36'), /Google Chrome/);
  assert.match(getInstallInstructions('Mozilla/5.0 Edg/130.0.0.0'), /Microsoft Edge/);
  assert.match(getInstallInstructions('Mozilla/5.0 Firefox/130.0'), /Firefox on Windows/);
});

test('other browsers receive a Chrome and Edge installation path', () => {
  const instructions = getInstallInstructions('Unknown Browser');
  assert.match(instructions, /Microsoft Edge/);
  assert.match(instructions, /Google Chrome/);
});
