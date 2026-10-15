import assert from 'node:assert/strict';
import fs from 'node:fs';
import { chromium } from 'playwright';

// With Vite running: node tests/audio-audit.mjs
// Uses an isolated browser profile; no screenshots or save-game changes survive.
const baseURL = process.env.BASE_URL || 'http://127.0.0.1:5173';
const chrome = '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome';
const executablePath = process.env.BROWSER_EXECUTABLE || (fs.existsSync(chrome) ? chrome : undefined);
const browser = await chromium.launch({ headless: true, executablePath });
const results = [];

try {
  const page = await browser.newPage({ viewport: { width: 1280, height: 800 } });
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.addInitScript(() => {
    window.audioAudit = { contexts: [], oscillators: 0 };
    const Original = window.AudioContext;
    window.AudioContext = class extends Original {
      constructor(...args) {
        super(...args);
        window.audioAudit.contexts.push(this);
      }
      createOscillator() {
        window.audioAudit.oscillators++;
        return super.createOscillator();
      }
    };
  });
  await page.goto(`${baseURL}/?test=1`, { waitUntil: 'networkidle' });
  await page.waitForFunction(() => window.__stillwater && window.render_game_to_text);
  await page.click('#start-btn');
  await page.evaluate(() => window.__stillwater.pauseAuto());

  await page.focus('#settings-btn');
  await page.keyboard.press('Enter');
  assert.equal(await page.locator('#settings-modal').isVisible(), true);
  await page.keyboard.press('Escape');
  assert.equal(await page.evaluate(() => document.activeElement.id), 'world');
  await page.focus('.wordmark');
  await page.keyboard.press('Enter');
  assert.equal(await page.locator('#settings-modal').isVisible(), true);
  await page.keyboard.press('Escape');
  assert.equal(await page.evaluate(() => document.activeElement.id), 'world');
  await page.focus('#sound-btn');
  await page.keyboard.press('Space');
  await page.waitForFunction(() => JSON.parse(render_game_to_text()).settings.sound);
  assert.equal(await page.evaluate(() => audioAudit.contexts.length), 1);
  results.push({ test: 'Keyboard button/link activation and canvas focus return', passed: true });

  await page.evaluate(() => {
    Object.defineProperty(document, 'hidden', { configurable: true, get: () => true });
    document.dispatchEvent(new Event('visibilitychange'));
  });
  await page.waitForFunction(() => audioAudit.contexts[0].state === 'suspended');
  assert.equal(await page.evaluate(() => JSON.parse(render_game_to_text()).settings.sound), true);
  await page.evaluate(() => {
    Object.defineProperty(document, 'hidden', { configurable: true, get: () => false });
    document.dispatchEvent(new Event('visibilitychange'));
  });
  await page.waitForFunction(() => audioAudit.contexts[0].state === 'running');
  assert.equal(await page.evaluate(() => JSON.parse(render_game_to_text()).settings.sound), true);
  await page.keyboard.press('Escape');
  results.push({ test: 'Background suspension and foreground resume preserve enabled preference', passed: true });

  const before = await page.evaluate(() => audioAudit.oscillators);
  await page.evaluate(() => {
    const api = window.__stillwater;
    // Inventory setup isolates the completion sound from the full quest route,
    // which is covered by game.spec.mjs. The altar interaction itself is real.
    for (const echo of api.world.interactables.filter(item => item.type === 'echo')) {
      api.state.collected.add(echo.id);
    }
    const altar = api.world.interactables.find(item => item.type === 'altar');
    for (let angle = 0; angle < Math.PI * 2; angle += 0.3) {
      const x = altar.position.x + Math.sin(angle) * 1.9;
      const z = altar.position.z + Math.cos(angle) * 1.9;
      if (api.world.canWalk(x, z)) {
        api.teleport(x, z);
        api.interact();
        return;
      }
    }
    throw new Error('No walkable altar approach found');
  });
  const afterInteraction = await page.evaluate(() => audioAudit.oscillators);
  await page.evaluate(() => { window.advanceTime(16); window.__stillwater.pauseAuto(); });
  const afterUpdate = await page.evaluate(() => audioAudit.oscillators);
  assert.equal(await page.evaluate(() => JSON.parse(render_game_to_text()).complete), true);
  assert.equal(afterInteraction - before, 10, 'Five bells should create ten sine oscillators');
  assert.equal(afterUpdate, afterInteraction, 'The following update must not repeat the completion chime');
  results.push({ test: 'Completion chime triggers once', passed: true, oscillatorCounts: [before, afterInteraction, afterUpdate] });
  assert.deepEqual(errors, [], 'No browser errors');
  console.log(JSON.stringify({ passed: results.length, results, browserErrors: errors }, null, 2));
} finally {
  await browser.close();
}
