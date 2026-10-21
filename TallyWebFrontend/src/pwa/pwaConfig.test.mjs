import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const manifestUrl = new URL('../../public/manifest.webmanifest', import.meta.url);
const publicUrl = new URL('../../public/', import.meta.url);

test('PWA manifest declares the Tally Web standalone application and required icons', async () => {
  const manifest = JSON.parse(await readFile(manifestUrl, 'utf8'));
  assert.equal(manifest.name, 'Tally Web');
  assert.equal(manifest.short_name, 'Tally Web');
  assert.equal(manifest.display, 'standalone');
  assert.equal(manifest.start_url, '/dashboard');
  assert.equal(manifest.theme_color, '#b97824');
  for (const size of [192, 512]) {
    const icon = manifest.icons.find((entry) => entry.sizes === `${size}x${size}`);
    assert.equal(icon?.type, 'image/png');
    const png = await readFile(new URL(icon.src.slice(1), publicUrl));
    assert.deepEqual([...png.subarray(0, 8)], [137, 80, 78, 71, 13, 10, 26, 10]);
    assert.equal(png.readUInt32BE(16), size);
    assert.equal(png.readUInt32BE(20), size);
  }
});

test('service worker is network only and the client registers it with cache bypass for updates', async () => {
  const worker = await readFile(new URL('../../public/sw.js', import.meta.url), 'utf8');
  const registration = await readFile(new URL('./registerServiceWorker.js', import.meta.url), 'utf8');
  assert.match(worker, /fetch\(event\.request, \{ cache: 'no-store' \}\)/);
  assert.doesNotMatch(worker, /caches\.(?:open|match)|\.put\(/);
  assert.match(worker, /SW_BUILD_ID = '__BUILD_ID__'/);
  assert.match(registration, /register\('\/sw\.js', \{ scope: '\/', updateViaCache: 'none' \}\)/);
  assert.match(registration, /registration\.update\(\)/);
});
