import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { chromium } from 'playwright';

// Focused regression for the abbey arch hiding the entire traveler.
// Run against Vite: node tests/camera-occlusion.mjs
const baseURL = process.env.BASE_URL || 'http://127.0.0.1:5173';
const output = path.resolve('test-results/qa');
fs.mkdirSync(output, { recursive: true });
const chrome = '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome';
const executablePath = process.env.BROWSER_EXECUTABLE || (fs.existsSync(chrome) ? chrome : undefined);
const browser = await chromium.launch({ executablePath, headless: true, args: [
  '--enable-webgl', '--ignore-gpu-blocklist', '--use-angle=swiftshader', '--enable-unsafe-swiftshader',
] });
const results = [], errors = [];
let baseline;
const page = await browser.newPage({ viewport: { width: 1440, height: 900 }, deviceScaleFactor: 1 });
page.on('pageerror', error => errors.push(error.message));
page.on('console', message => { if (message.type() === 'error') errors.push(message.text()); });

try {
  await page.goto(`${baseURL}/?test=1`, { waitUntil: 'networkidle' });
  await page.waitForFunction(() => window.__stillwater && window.render_game_to_text);
  await page.evaluate(() => window.__stillwater.pauseAuto());
  await page.click('#start-btn');
  // Import the same Three.js API through Vite so geometric verification is
  // independent of the camera's internal collision implementation.
  await page.evaluate(async () => {
    window.cameraTestThree = await import('/node_modules/three/build/three.module.js');
    window.__stillwater.pauseAuto();
  });

  const locations = [
    { name: 'shrine', x: 0, z: -20.3 },
    { name: 'nave', x: 0, z: -16.1 },
    { name: 'west-grove', x: -14, z: -0.3 },
  ];
  const angles = [0, Math.PI / 4, Math.PI / 2, 3 * Math.PI / 4, Math.PI, 5 * Math.PI / 4, 3 * Math.PI / 2, 7 * Math.PI / 4];

  for (const location of locations) for (const angle of angles) {
    const measurement = await page.evaluate(({ location, angle }) => {
      const { Vector3, Raycaster } = window.cameraTestThree;
      const api = window.__stillwater;
      api.teleport(location.x, location.z);
      api.setCamera(angle, 16);
      window.advanceTime(2000);
      api.pauseAuto();
      api.scene.updateMatrixWorld(true);
      api.camera.updateMatrixWorld(true);
      const blockers = api.world.root.children.filter(mesh => /stonework|columns|arch-stones|tree-limbs|meadow-boulders/.test(mesh.name));
      const focus = new Vector3(api.state.x, api.state.y + 1.35, api.state.z);
      const direction = api.camera.position.clone().sub(focus);
      const distance = direction.length();
      const ray = new Raycaster(focus, direction.normalize(), 0.12, distance - 0.05);
      const hits = ray.intersectObjects(blockers, false);
      const body = [0.15, 1.35, 2.1].map(height => {
        const projected = new Vector3(api.state.x, api.state.y + height, api.state.z).project(api.camera);
        return { height, x: projected.x, y: projected.y, z: projected.z };
      });
      const originalCamera = new Vector3(api.state.x + Math.sin(angle) * 16, 16 * Math.sin(0.6) + 2.1, api.state.z - 3.3 + Math.cos(angle) * 16);
      const originalDirection = originalCamera.clone().sub(focus);
      ray.set(focus, originalDirection.clone().normalize());
      ray.far = originalDirection.length();
      const originalHits = ray.intersectObjects(blockers, false);
      return {
        location: location.name, angle: +angle.toFixed(4),
        camera: api.camera.position.toArray(), distance, obstructed: JSON.parse(window.render_game_to_text()).camera.obstructed,
        blockers: hits.map(hit => ({ name: hit.object.name, instance: hit.instanceId, distance: hit.distance })),
        body,
        originalBlocker: originalHits[0] ? { name: originalHits[0].object.name, instance: originalHits[0].instanceId, distance: originalHits[0].distance } : null,
      };
    }, { location, angle });
    const failures = [];
    if (measurement.blockers.length) failures.push('Solid geometry remains between chest and camera');
    if (!measurement.body.every(point => Math.abs(point.x) < 0.98 && Math.abs(point.y) < 0.98 && point.z > -1 && point.z < 1)) failures.push('Character body falls outside the viewport');
    if (!Number.isFinite(measurement.distance) || measurement.distance < 0.59) failures.push('Invalid or excessively close camera distance');
    measurement.status = failures.length ? 'failed' : 'passed';
    measurement.failures = failures;
    results.push(measurement);
    console.log(`${failures.length ? 'FAIL' : 'PASS'} ${location.name} angle=${angle.toFixed(2)} corrected=${measurement.obstructed} distance=${measurement.distance.toFixed(2)}${failures.length ? `: ${failures.join('; ')}` : ''}`);

    if (location.name === 'shrine' && angle === 0) {
      baseline = measurement;
      await page.screenshot({ path: path.join(output, 'camera-shrine-ascii.png'), animations: 'disabled' });
      await page.evaluate(() => window.__stillwater.graphics.renderRaw());
      await page.screenshot({ path: path.join(output, 'camera-shrine-raw.png'), animations: 'disabled' });
      await page.evaluate(() => window.__stillwater.graphics.render(window.__stillwater.state.time));
    }
    if (failures.length) await page.screenshot({ path: path.join(output, `camera-${location.name}-${Math.round(angle * 180 / Math.PI)}-failure.png`), animations: 'disabled' });
  }

  assert.ok(baseline.originalBlocker?.name === 'arch-stones', 'Original shrine camera must reproduce the arch obstruction');
  assert.equal(baseline.obstructed, true, 'Shrine pose engages camera collision correction');
  assert.deepEqual(errors, [], 'Browser console contains no errors');
  assert.equal(results.filter(result => result.status === 'failed').length, 0, 'All camera orbit positions keep the traveler visible');
} catch (error) {
  console.error(error.stack);
  process.exitCode = 1;
} finally {
  fs.writeFileSync(path.join(output, 'camera-occlusion-report.json'), JSON.stringify({ baseURL, results, browserErrors: errors }, null, 2));
  await browser.close();
}
console.log(`${results.filter(result => result.status === 'passed').length}/${results.length} camera poses passed.`);
