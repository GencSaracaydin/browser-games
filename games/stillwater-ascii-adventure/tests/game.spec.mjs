import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { chromium } from 'playwright';

// Run with the Vite server started: npm test
// BASE_URL and BROWSER_EXECUTABLE may point at another server/browser.
const baseURL = process.env.BASE_URL || 'http://127.0.0.1:5173';
const output = path.resolve('test-results/qa');
fs.mkdirSync(output, { recursive: true });
const chrome = '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome';
const executablePath = process.env.BROWSER_EXECUTABLE || (fs.existsSync(chrome) ? chrome : undefined);
const browser = await chromium.launch({ executablePath, headless: true, args: [
  '--enable-webgl', '--ignore-gpu-blocklist', '--use-angle=swiftshader', '--enable-unsafe-swiftshader',
] });
const results = [];
const filter = process.env.TEST_FILTER ? new RegExp(process.env.TEST_FILTER) : null;

const read = page => page.evaluate(() => JSON.parse(window.render_game_to_text()));
const tick = (page, milliseconds = 250) => page.evaluate(ms => {
  window.advanceTime(ms); window.__stillwater.pauseAuto();
}, milliseconds);
async function load(page) {
  await page.goto(`${baseURL}/?test=1`, { waitUntil: 'networkidle' });
  await page.waitForFunction(() => window.__stillwater && window.render_game_to_text);
  await page.evaluate(() => window.__stillwater.pauseAuto());
}
async function start(page) {
  await page.click('#start-btn');
  await page.evaluate(() => { window.__stillwater.setCamera(0, 16); window.__stillwater.pauseAuto(); });
  await tick(page, 500);
  assert.equal((await read(page)).mode, 'playing');
}
async function hold(page, keys, milliseconds) {
  for (const key of keys) await page.keyboard.down(key);
  await tick(page, milliseconds);
  for (const key of keys) await page.keyboard.up(key);
  await tick(page, 16.667);
}
async function teleport(page, x, z) {
  await page.evaluate(([x, z]) => window.__stillwater.teleport(x, z), [x, z]);
  await tick(page, 250);
}
async function visible(page, selector) { assert.ok(await page.locator(selector).isVisible(), `${selector} should be visible`); }

// Test inputs walk the entire quest. The test-only world query supplies a route;
// it never changes inventory or completion, and all gathering uses the E key.
async function walkTo(page, targetX, targetZ) {
  return page.evaluate(({ targetX, targetZ }) => {
    const api = window.__stillwater, grid = 0.5;
    api.setCamera(0, 16);
    const walkable = (x, z) => [[0,0],[0.13,0],[-0.13,0],[0,0.13],[0,-0.13]]
      .every(([dx,dz]) => api.world.canWalk(x + dx, z + dz));
    const key = (x, z) => `${x},${z}`;
    const start = [Math.round(api.state.x / grid), Math.round(api.state.z / grid)];
    const goal = [Math.round(targetX / grid), Math.round(targetZ / grid)];
    const queue = [start], parents = new Map([[key(...start), null]]), nodes = new Map([[key(...start), start]]);
    const dirs = [[1,0],[-1,0],[0,1],[0,-1],[1,1],[1,-1],[-1,1],[-1,-1]];
    for (let index = 0; index < queue.length && !parents.has(key(...goal)); index++) {
      const [x, z] = queue[index];
      for (const [dx, dz] of dirs) {
        const nx = x + dx, nz = z + dz, nextKey = key(nx, nz);
        if (parents.has(nextKey) || !walkable(nx * grid, nz * grid)) continue;
        if (!walkable((x + dx * 0.5) * grid, (z + dz * 0.5) * grid)) continue;
        if (dx && dz && (!walkable((x + dx) * grid, z * grid) || !walkable(x * grid, (z + dz) * grid))) continue;
        parents.set(nextKey, key(x, z)); nodes.set(nextKey, [nx, nz]); queue.push([nx, nz]);
      }
    }
    if (!parents.has(key(...goal))) throw new Error(`No walkable route to ${targetX},${targetZ}`);
    const route = [];
    for (let cursor = key(...goal); cursor; cursor = parents.get(cursor)) route.unshift(nodes.get(cursor));
    const corners = route.filter((point, i) => i === route.length - 1 || (i > 0 && (
      point[0] - route[i-1][0] !== route[i+1][0] - point[0] || point[1] - route[i-1][1] !== route[i+1][1] - point[1])));
    let held = [], ticks = 0, distance = 0;
    function setKeys(next) {
      for (const name of held) if (!next.includes(name)) window.dispatchEvent(new KeyboardEvent('keyup', { key: name }));
      for (const name of next) if (!held.includes(name)) window.dispatchEvent(new KeyboardEvent('keydown', { key: name }));
      held = next;
    }
    const render = api.graphics.render;
    let renderCount = 0;
    // All simulation and controls still run. Only intermediate GPU presentation
    // is throttled on the long walking route; screenshots use the real renderer.
    api.graphics.render = (...args) => { if (++renderCount % 20 === 0) render(...args); };
    try {
      for (const [gx, gz] of corners) {
        const x = gx * grid, z = gz * grid;
        for (let attempts = 0; attempts < 500; attempts++) {
          const dx = x - api.state.x, dz = z - api.state.z, remaining = Math.hypot(dx, dz);
          if (remaining < 0.045) break;
          const keys = [];
          if (Math.abs(dx) > 0.012) keys.push(dx > 0 ? 'd' : 'a');
          if (Math.abs(dz) > 0.012) keys.push(dz > 0 ? 's' : 'w');
          setKeys(keys);
          const previousX = api.state.x, previousZ = api.state.z;
          window.advanceTime(Math.min(110, Math.max(4, remaining / 3 * 1000 * 0.55)));
          distance += Math.hypot(api.state.x - previousX, api.state.z - previousZ);
          if (!api.world.canWalk(api.state.x, api.state.z)) throw new Error('Route entered a collider');
          if (++ticks > 3500 || attempts === 499) throw new Error(`Walk stalled at ${api.state.x},${api.state.z}, heading toward ${x},${z}`);
        }
      }
    } finally {
      setKeys([]); api.graphics.render = render; window.advanceTime(50); api.pauseAuto();
    }
    return { distance, ticks, x: api.state.x, z: api.state.z };
  }, { targetX, targetZ });
}

async function test(name, fn, options = {}) {
  if (filter && !filter.test(name)) return;
  console.log(`RUN  ${name}`);
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 }, deviceScaleFactor: 1, ...options });
  const page = await context.newPage();
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  page.on('console', message => { if (message.type() === 'error') errors.push(message.text()); });
  const started = Date.now();
  try {
    await load(page);
    const details = await fn(page, context);
    assert.deepEqual(errors, [], 'Browser console should contain no errors');
    await tick(page, 2000);
    await page.screenshot({ path: path.join(output, `${name}.png`), animations: 'disabled' });
    results.push({ name, status: 'passed', seconds: +((Date.now()-started)/1000).toFixed(1), details });
    console.log(`PASS ${name}`);
  } catch (error) {
    await page.screenshot({ path: path.join(output, `${name}-failure.png`), animations: 'disabled' }).catch(() => {});
    results.push({ name, status: 'failed', seconds: +((Date.now()-started)/1000).toFixed(1), error: error.stack, browserErrors: errors });
    console.error(`FAIL ${name}: ${error.message}`);
  } finally { await context.close(); }
}

try {
  await test('intro-and-controls', async page => {
    await visible(page, '#intro');
    assert.equal((await read(page)).mode, 'intro');
    await page.keyboard.press('Enter');
    await tick(page, 500);
    assert.equal((await read(page)).mode, 'playing');
    await page.evaluate(() => window.__stillwater.setCamera(0, 16));
    const initial = (await read(page)).player;
    await hold(page, ['ArrowUp'], 500);
    const after = (await read(page)).player;
    assert.ok(initial.z - after.z > 1.3, 'ArrowUp moves the character north');
    assert.ok(Math.abs(initial.x - after.x) < 0.02);
    await teleport(page, 0, 13);
    await hold(page, ['w', 'd'], 500);
    const diagonal = (await read(page)).player;
    assert.ok(Math.abs(Math.hypot(diagonal.x, diagonal.z - 13) - 1.5) < 0.12, 'Diagonal motion is normalized');
    await teleport(page, 0, 13);
    await hold(page, ['w', 'Shift'], 500);
    assert.ok(13 - (await read(page)).player.z > 2.4, 'Running is faster than walking');
    assert.equal((await read(page)).player.moving, false, 'Keys release without sticking');
    await page.keyboard.press('m'); await tick(page);
    assert.equal((await read(page)).mapOpen, true);
    await page.keyboard.press('Escape');
    assert.equal((await read(page)).mapOpen, false);
    assert.equal((await read(page)).mode, 'playing');
  });

  await test('water-and-collision', async page => {
    await start(page); await teleport(page, 0, 4);
    const waterBefore = await read(page);
    assert.equal(waterBefore.player.inWater, true);
    await hold(page, ['w'], 1000);
    const waterAfter = await read(page);
    assert.ok(waterAfter.rippleSteps >= waterBefore.rippleSteps + 2, 'Wading creates multiple footstep ripples');
    assert.ok(waterBefore.player.z - waterAfter.player.z > 2 && waterBefore.player.z - waterAfter.player.z < 2.6, 'Water slows movement');
    await teleport(page, 0, -16.4);
    await hold(page, ['w'], 1700);
    const collision = await read(page);
    assert.ok(collision.player.z > -17.25, 'Echo pedestal stops the player before its center');
    assert.ok(await page.evaluate(() => window.__stillwater.world.canWalk(window.__stillwater.state.x, window.__stillwater.state.z)));
    const boundary = await page.evaluate(() => {
      const { world } = window.__stillwater;
      for (let z = -20; z < 20; z++) if (world.canWalk(world.bounds.maxX - 0.5, z)) return { x: world.bounds.maxX - 0.5, z, maxX: world.bounds.maxX };
    });
    assert.ok(boundary, 'A safe boundary test position exists');
    await teleport(page, boundary.x, boundary.z); await hold(page, ['d'], 2000);
    assert.ok((await read(page)).player.x <= boundary.maxX, 'World bounds stop movement');
  });

  await test('pause-camera-settings', async page => {
    await start(page);
    await page.keyboard.press('Escape'); await tick(page);
    await visible(page, '#settings-modal');
    const paused = await read(page);
    assert.equal(paused.mode, 'paused');
    await hold(page, ['w'], 1000);
    assert.deepEqual((await read(page)).player, paused.player, 'Pause freezes player movement');
    assert.equal((await read(page)).elapsed, paused.elapsed, 'Pause freezes journey time');
    await page.selectOption('#detail-select', 'bold'); await page.selectOption('#palette-select', 'amber');
    await page.check('#motion-setting');
    await page.locator('#zoom-setting').fill('12');
    await page.locator('#zoom-setting').dispatchEvent('input');
    assert.equal((await read(page)).settings.palette, 'amber');
    await page.click('#resume-btn');
    await page.mouse.move(700, 450); await page.mouse.down(); await page.mouse.move(840, 490, { steps: 4 }); await page.mouse.up();
    await tick(page);
    assert.ok(Math.abs((await read(page)).camera.angle) > 0.4, 'Drag orbits camera');
    const distance = (await read(page)).camera.distance;
    await page.mouse.wheel(0, 250); await tick(page);
    assert.ok((await read(page)).camera.distance > distance, 'Wheel zoom changes distance');
    await page.reload({ waitUntil: 'networkidle' }); await page.waitForFunction(() => window.__stillwater);
    const restored = await read(page);
    assert.equal(restored.settings.detail, 'bold'); assert.equal(restored.settings.palette, 'amber'); assert.equal(restored.settings.reducedMotion, true);
    await page.click('#settings-btn');
    await page.locator('#settings-modal .close-btn').focus();
    await page.keyboard.press('Enter');
    assert.equal((await read(page)).mode, 'intro', 'Enter activates the focused close button');
    await page.click('#settings-btn');
    await page.waitForFunction(() => document.querySelector('#settings-modal').contains(document.activeElement));
    await page.keyboard.press('Tab');
    assert.ok(await page.evaluate(() => document.querySelector('#settings-modal').contains(document.activeElement)), 'Modal keyboard focus remains inside dialog');
  });

  await test('full-quest-save-restart', async page => {
    await start(page);
    const targets = await read(page);
    const total = { distance: 0, ticks: 0 };
    // First visit proves the altar cannot complete an empty quest.
    let walked = await walkTo(page, targets.altar.x, targets.altar.z + 1.7);
    total.distance += walked.distance; total.ticks += walked.ticks;
    await page.keyboard.press('e'); await tick(page);
    assert.equal((await read(page)).complete, false);
    assert.match(await page.locator('#toast').textContent(), /3 more echoes/);
    for (const [index, echo] of targets.echoes.entries()) {
      walked = await walkTo(page, echo.x, echo.z + 1.7);
      total.distance += walked.distance; total.ticks += walked.ticks;
      assert.equal((await read(page)).nearby, echo.id, `Prompt appears near ${echo.id}`);
      await page.keyboard.press('e'); await tick(page, 250);
      assert.equal((await read(page)).collected, index + 1);
      await page.keyboard.press('e'); await tick(page);
      assert.equal((await read(page)).collected, index + 1, 'Repeated interaction cannot duplicate an echo');
    }
    await page.keyboard.press('j'); await tick(page);
    assert.equal(await page.locator('.journal-entry.found').count(), 3);
    await tick(page, 2000);
    await page.screenshot({ path: path.join(output, 'full-quest-journal.png'), animations: 'disabled' });
    await page.click('#journal-close');
    walked = await walkTo(page, targets.altar.x, targets.altar.z + 1.7);
    total.distance += walked.distance; total.ticks += walked.ticks;
    await tick(page, 2000);
    await page.screenshot({ path: path.join(output, 'full-quest-shrine.png'), animations: 'disabled' });
    await page.keyboard.press('Enter'); await tick(page);
    assert.equal((await read(page)).complete, true);
    await visible(page, '#ending-modal');
    assert.equal((await read(page)).mode, 'paused');
    await tick(page, 2000);
    await page.screenshot({ path: path.join(output, 'full-quest-ending.png'), animations: 'disabled' });
    await page.click('#continue-btn'); await tick(page);
    assert.equal((await read(page)).mode, 'playing');
    await page.evaluate(() => window.__stillwater.save());
    await page.reload({ waitUntil: 'networkidle' }); await page.waitForFunction(() => window.__stillwater);
    assert.equal((await read(page)).complete, true); assert.equal((await read(page)).collected, 3);
    assert.match(await page.locator('#start-btn').textContent(), /Continue wandering/);
    await page.click('#start-btn'); await page.click('#settings-btn'); await page.click('#restart-btn');
    await page.click('#cancel-restart');
    assert.equal((await read(page)).collected, 3, 'Cancel preserves the completed journey');
    await page.click('#restart-btn'); await page.click('#confirm-restart'); await tick(page);
    const reset = await read(page);
    assert.equal(reset.collected, 0); assert.equal(reset.complete, false); assert.equal(reset.mode, 'playing');
    assert.ok(Math.abs(reset.player.x) < 0.01 && Math.abs(reset.player.z - 13) < 0.01);
    return { walkedMeters: +total.distance.toFixed(1), inputSteps: total.ticks };
  });

  await test('mobile-touch-layout', async page => {
    await visible(page, '#start-btn');
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), 'Intro has no horizontal overflow');
    await start(page); await visible(page, '#mobile-controls');
    await page.click('#map-header-btn'); await tick(page);
    assert.equal((await read(page)).mapOpen, true, 'Header map opens on mobile');
    await page.locator('#map-panel [data-close="map-panel"]').click();
    assert.equal((await read(page)).mapOpen, false, 'Mobile map close returns to exploration');
    const before = (await read(page)).player;
    const box = await page.locator('#joystick').boundingBox();
    await page.mouse.move(box.x + box.width/2, box.y + box.height/2);
    await page.mouse.down(); await page.mouse.move(box.x + box.width/2, box.y + 8);
    await tick(page, 600); await page.mouse.up(); await tick(page);
    assert.ok(before.z - (await read(page)).player.z > 1.3, 'Touch joystick moves the character');
    assert.equal((await read(page)).player.moving, false, 'Joystick release stops movement');
    await page.click('#touch-run');
    assert.ok(await page.locator('#touch-run').evaluate(element => element.classList.contains('active')));
    await teleport(page, -14, -0.4); await page.click('#touch-interact'); await tick(page);
    assert.equal((await read(page)).collected, 1, 'Touch interaction gathers an echo');
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), 'Playing has no horizontal overflow');
    for (const selector of ['#joystick', '#touch-run', '#touch-interact', '#settings-btn']) {
      const bounds = await page.locator(selector).boundingBox();
      assert.ok(bounds && bounds.x >= 0 && bounds.y >= 0 && bounds.x + bounds.width <= 391 && bounds.y + bounds.height <= 845, `${selector} is inside mobile viewport`);
    }
    await page.click('#settings-btn'); await visible(page, '#settings-modal');
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
  }, { viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true });

  await test('invalid-save-and-audio', async page => {
    await page.evaluate(() => {
      localStorage.setItem('stillwater-journey-v1', JSON.stringify({ version: 1, x: 9999, z: -9999, collected: ['made-up'], complete: true, elapsed: -10 }));
    });
    await page.reload({ waitUntil: 'networkidle' }); await page.waitForFunction(() => window.__stillwater);
    const state = await read(page);
    assert.equal(state.collected, 0); assert.equal(state.complete, false); assert.equal(state.elapsed, 0);
    assert.ok(Math.abs(state.player.x) < 0.01 && Math.abs(state.player.z - 13) < 0.01);
    await page.click('#sound-btn');
    assert.equal((await read(page)).settings.sound, true);
    await page.click('#sound-btn');
    assert.equal((await read(page)).settings.sound, false);
    await page.evaluate(() => localStorage.setItem('stillwater-journey-v1', '{broken-json'));
    await page.reload({ waitUntil: 'networkidle' }); await page.waitForFunction(() => window.__stillwater);
    assert.equal((await read(page)).mode, 'intro');
  });

  await test('idle-stability', async page => {
    await start(page); await teleport(page, 0, 4);
    await hold(page, ['w'], 1000);
    const before = await page.evaluate(() => ({ objects: window.__stillwater.scene.children.length, memory: { ...window.__stillwater.graphics.info.memory } }));
    await tick(page, 180000);
    const after = await page.evaluate(() => ({ objects: window.__stillwater.scene.children.length, memory: { ...window.__stillwater.graphics.info.memory } }));
    assert.ok(after.objects <= before.objects + 3, 'Idle world does not accumulate scene objects');
    assert.ok(after.memory.geometries <= before.memory.geometries + 3, 'Idle world does not accumulate geometry');
    const state = await read(page);
    assert.ok(Number.isFinite(state.player.x) && Number.isFinite(state.player.z));
    assert.equal(state.mode, 'playing');
    return { before, after, simulatedSeconds: 180 };
  });
} finally {
  await browser.close();
  fs.writeFileSync(path.join(output, 'report.json'), JSON.stringify({ baseURL, results }, null, 2));
}
const failures = results.filter(result => result.status === 'failed');
console.log(`${results.length - failures.length}/${results.length} scenarios passed. Report: ${path.join(output, 'report.json')}`);
if (failures.length) process.exitCode = 1;
