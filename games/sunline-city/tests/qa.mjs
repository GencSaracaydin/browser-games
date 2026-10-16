import { chromium } from 'playwright';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';

// Independent interaction QA. Input always travels through the game's DOM input
// handlers; DEV teleports/state edits are limited to isolated scenario setup.
const base = process.env.SUNLINE_URL || 'http://127.0.0.1:5174';
const out = new URL('../output/independent-qa/', import.meta.url);
await fs.mkdir(out, { recursive: true });
const browser = await chromium.launch({ headless: true });
const results = [];
const errors = [];
const context = await browser.newContext({ viewport: { width: 1440, height: 960 } });
await context.addInitScript(() => { window.requestAnimationFrame = () => 0; window.WebSocket = class { addEventListener(){} send(){} close(){} }; });
const page = await context.newPage();
const watch = p => {
  p.on('pageerror', e => errors.push({ page: p.url(), message: e.message }));
  p.on('console', m => { if (m.type() === 'error') errors.push({ page: p.url(), message: m.text() }); });
};
watch(page);
await page.goto(base);
await page.waitForFunction(() => window.__SUNLINE__ && window.render_game_to_text);
const earnedCash = s => s.cash - (s.exploration?.visited.length||0)*40;
const state = p => p.evaluate(() => JSON.parse(window.render_game_to_text()));
const step = (p, ms = 16, codes = []) => p.evaluate(({ ms, codes }) => {
  for (const code of codes) window.dispatchEvent(new KeyboardEvent('keydown', { code, bubbles: true }));
  window.advanceTime(ms);
  for (const code of codes) window.dispatchEvent(new KeyboardEvent('keyup', { code, bubbles: true }));
  return JSON.parse(window.render_game_to_text());
}, { ms, codes });
const tap = async (p, key) => { await p.keyboard.press(key); return step(p, 16); };
const teleport = (p, x, y) => p.evaluate(({ x, y }) => { window.__SUNLINE__.teleport(x,y); window.advanceTime(1); }, { x, y });
const fresh = async p => {
  await p.evaluate(() => { window.__SUNLINE__.reset(); });
  await p.locator('#start-btn').click();
  await step(p, 20);
};
const screenshot = async (p, name) => {
  await p.evaluate(() => { window.__SUNLINE__.updateUI(); window.__SUNLINE__.render(); });
  await p.screenshot({ path: new URL(`${name}.png`, out).pathname });
  await fs.writeFile(new URL(`${name}.json`, out), JSON.stringify(await state(p), null, 2));
};
const test = async (name, fn) => {
  if(process.env.QA_ONLY && !name.includes(process.env.QA_ONLY)) return;
  try { const detail = await fn(); results.push({ name, pass: true, detail }); console.log(`PASS ${name}`); }
  catch (e) { results.push({ name, pass: false, error: e.stack }); console.log(`FAIL ${name}: ${e.message}`); await screenshot(page, `failure-${results.length}`).catch(()=>{}); }
};
const getCar = async p => {
  const car = await p.evaluate(() => window.__SUNLINE__.sim.cars.find(c => c.id === 'car-0'));
  await teleport(p, car.x, car.y + 20);
  let s = await tap(p, 'e');
  // The starter now parks beside Luca; his nearby offer has interaction priority.
  if (!s.vehicle && s.mission.phase === 'vehicle') s = await tap(p, 'e');
  assert.equal(s.vehicle?.id, 'car-0');
  return s;
};
const clearHeat = async p => {
  // Move behind distant blocks to verify the real line-of-sight and 14s escape logic.
  return p.evaluate(() => {
    const dev = window.__SUNLINE__, sim = dev.sim;
    const candidates = [{x:220,y:230},{x:2510,y:260},{x:220,y:2650},{x:2500,y:2650},{x:1150,y:1300},{x:1830,y:2140}].filter(p=>!dev.world.isSolid(p.x,p.y,16));
    let elapsed=0, hidden=0;
    while(sim.wanted && elapsed<45){
      const score=p=>sim.police.reduce((min,c)=>Math.min(min, Math.hypot(c.x-p.x,c.y-p.y)+(sim.lineClear(c,p)?0:250)), 9999);
      candidates.sort((a,b)=>score(b)-score(a));
      const q=candidates[0];dev.teleport(q.x,q.y);
      window.advanceTime(500);elapsed+=.5;if(!sim.heat)hidden+=.5;
    }
    return {wanted:sim.wanted,escapes:sim.stats.escapes,elapsed,hidden};
  });
};
const drive = async (p, points, limit = 160, cruise = 132) => p.evaluate(({ points, limit, cruise }) => {
  const sim=window.__SUNLINE__.sim; let next=0, elapsed=0, collisions=0;const trail=[];
  let lastHealth=sim.vehicle?.health ?? 0;
  while(next<points.length && elapsed<limit && sim.vehicle){
    const car=sim.vehicle,target=points[next];const dx=target.x-car.x,dy=target.y-car.y,d=Math.hypot(dx,dy);
    if(d<(target.radius||38)){trail.push({next,elapsed,x:car.x,y:car.y,health:car.health,wanted:sim.wanted,checkpoint:sim.job.checkpoint});next++;continue;}
    const difference=Math.atan2(Math.sin(Math.atan2(dy,dx)-car.angle),Math.cos(Math.atan2(dy,dx)-car.angle));
    const targetSpeed=Math.abs(difference)>.45?72:Math.min(cruise, Math.max(65,d*.8));
    const codes=[];if(difference>.075)codes.push('ArrowRight');if(difference<-.075)codes.push('ArrowLeft');
    if(car.speed<targetSpeed-5)codes.push('ArrowUp');if(car.speed>targetSpeed+12)codes.push('Space');
    for(const code of codes)window.dispatchEvent(new KeyboardEvent('keydown',{code,bubbles:true}));
    window.advanceTime(100);
    for(const code of codes)window.dispatchEvent(new KeyboardEvent('keyup',{code,bubbles:true}));
    elapsed+=.1;if(sim.vehicle&&sim.vehicle.health<lastHealth)collisions++;lastHealth=sim.vehicle?.health??0;
  }
  window.dispatchEvent(new KeyboardEvent('keydown',{code:'Space',bubbles:true}));window.advanceTime(1000);window.dispatchEvent(new KeyboardEvent('keyup',{code:'Space',bubbles:true}));
  return {next,total:points.length,elapsed,collisions,trail,state:JSON.parse(window.render_game_to_text())};
}, { points, limit, cruise });

await test('intro and keyboard controls dialog', async()=>{
  assert.equal((await state(page)).mode,'intro');
  await screenshot(page,'01-desktop-intro');
  await page.locator('#intro-controls').click();
  assert.equal((await state(page)).mode,'paused');
  assert.match(await page.locator('#modal-panel').innerText(),/Aim & fire/);
  await tap(page,'Escape');assert.equal((await state(page)).mode,'intro');
  await page.locator('#start-btn').focus();await tap(page,'Enter');assert.equal((await state(page)).mode,'play');
  assert.equal(await page.evaluate(()=>document.activeElement.id),'game');
});

await test('walk, accept first job, enter starter, real drive to first delivery', async()=>{
  await fresh(page);
  const before=await state(page);const walking=await step(page,200,['ArrowDown']);
  assert.ok(walking.player.y>before.player.y+15);
  await step(page,200,['ArrowUp']);
  let s=await tap(page,'e');assert.equal(s.mission.phase,'vehicle');
  await step(page,200,['ArrowDown']);s=await tap(page,'e');assert.equal(s.vehicle?.id,'car-0');
  const moving=await step(page,400,['ArrowUp']);assert.ok(moving.vehicle.speed>40);
  await tap(page,'e');assert.ok((await state(page)).vehicle,'Cannot exit a fast car');
  await step(page,600,['Space']);
  const route=await drive(page,[{x:2280,y:1132,radius:35},{x:2320,y:1240,radius:55},{x:2320,y:1780,radius:55},{x:2320,y:2090,radius:35},{x:2460,y:2090,radius:28}]);
  assert.equal(route.next,route.total,JSON.stringify(route));
  await step(page,1000,['Space']);
  s=await tap(page,'e');assert.equal(s.completed,1,JSON.stringify(s));assert.equal(earnedCash(s),650);
  await screenshot(page,'02-first-job-driven');
  return {routeSeconds:route.elapsed,collisions:route.collisions,health:s.vehicle.health};
});

await test('pause/map freeze, focus trap, map destination, settings persistence',async()=>{
  await fresh(page);await step(page,200,['ArrowDown']);
  let s=await tap(page,'Escape');assert.equal(s.mode,'paused');const time=s.time;
  await step(page,5000,['ArrowDown']);s=await state(page);assert.equal(s.time,time);
  await page.locator('#new-game').focus();await tap(page,'Tab');assert.equal(await page.evaluate(()=>document.activeElement.id),'modal-close');
  await page.locator('#zoom-select').selectOption('0.8');await page.locator('#motion-select').selectOption('reduced');
  await tap(page,'Escape');assert.equal((await state(page)).mode,'play');
  assert.equal(await page.evaluate(()=>document.activeElement.id),'game');
  s=await tap(page,'m');assert.equal(s.mode,'map');const mapTime=s.time;await step(page,1000);assert.equal((await state(page)).time,mapTime);
  await screenshot(page,'03-city-map');
  const point=await page.evaluate(()=>{const svg=document.querySelector('#full-map svg'),p=svg.createSVGPoint();p.x=2400;p.y=1160;const q=p.matrixTransform(svg.getScreenCTM());return{x:q.x,y:q.y};});
  await page.mouse.click(point.x,point.y);assert.equal((await state(page)).mode,'play');
  const destination=await page.evaluate(()=>window.__SUNLINE__.sim.customWaypoint);assert.equal(destination.name,'Your destination');assert.ok(Math.hypot(destination.x-2400,destination.y-1160)<14);
  assert.equal(await page.evaluate(()=>JSON.parse(localStorage.getItem('sunline-settings-v1')).zoom),.8);
});

await test('shoot, car hit, empty magazine, reload, stolen car heat and escape',async()=>{
  await fresh(page);const targetCar=await page.evaluate(()=>window.__SUNLINE__.sim.cars[2]);await teleport(page,targetCar.x-75,targetCar.y);
  await page.evaluate(()=>window.__SUNLINE__.sim.player.angle=0);
  const before=await page.evaluate(()=>window.__SUNLINE__.sim.cars[2].health);
  for(let i=0;i<12;i++){await step(page,20,['Space']);await step(page,320);}
  let s=await state(page);assert.equal(s.player.ammo,0);assert.ok(s.wanted>0);
  assert.ok(await page.evaluate(()=>window.__SUNLINE__.sim.cars[2].health)<before);
  s=await tap(page,'r');assert.equal(s.player.reloading,true);s=await step(page,1200);assert.equal(s.player.ammo,12);assert.equal(s.player.reloading,false);
  await screenshot(page,'04-trouble-on-foot');
  await fresh(page);const parkedCar=await page.evaluate(()=>window.__SUNLINE__.sim.cars[2]);await teleport(page,parkedCar.x-20,parkedCar.y);s=await tap(page,'e');assert.equal(s.vehicle?.id,'car-2');assert.equal(s.wanted,1);
  await step(page,500);assert.ok((await state(page)).police.length>0);
  const escape=await clearHeat(page);assert.equal(escape.wanted,0);assert.equal(escape.escapes,1);return escape;
});

await test('race expires and can be accepted again',async()=>{
  await fresh(page);await getCar(page);
  await page.evaluate(()=>{const s=window.__SUNLINE__.sim;s.completed=2;s.job={index:2,phase:'offer',timer:0,checkpoint:0};});
  await teleport(page,750,1770);let s=await tap(page,'e');assert.equal(s.mission.phase,'race');
  s=await step(page,101000);assert.equal(s.mission.phase,'offer');assert.equal(s.completed,2);
  s=await tap(page,'e');assert.equal(s.mission.phase,'race');assert.ok(s.mission.timer>99);
  return {timer:s.mission.timer,completed:s.completed};
});

await test('complete coastal race by continuous steering and acceleration',async()=>{
  await fresh(page);await getCar(page);
  await page.evaluate(()=>{const s=window.__SUNLINE__.sim;s.completed=2;s.job={index:2,phase:'offer',timer:0,checkpoint:0};});
  await teleport(page,750,1770);let s=await tap(page,'e');assert.equal(s.mission.phase,'race');
  const route=await drive(page,[{x:1000,y:1840,radius:45},{x:1080,y:1840,radius:45},{x:1080,y:1240,radius:50},{x:1080,y:1160,radius:45},{x:1680,y:1160,radius:55},{x:2260,y:1160,radius:45},{x:2320,y:1160,radius:45},{x:2320,y:560,radius:45},{x:2320,y:480,radius:45},{x:1680,y:480,radius:65}],100,88);
  assert.equal(route.next,route.total,JSON.stringify(route));s=await state(page);assert.equal(s.completed,3,JSON.stringify(route));
  await screenshot(page,'10-race-driven');return{elapsed:route.elapsed,collisions:route.collisions,health:s.vehicle?.health,wanted:s.wanted};
});

await test('solid building collision and garage repair in car',async()=>{
  await fresh(page);await getCar(page);
  const wall=await page.evaluate(()=>window.__SUNLINE__.world.buildings.find(b=>b.w>100&&b.h>100&&b.x>160&&!window.__SUNLINE__.world.isSolid(b.x-90,b.y+b.h/2,16)));
  await teleport(page,wall.x-90,wall.y+wall.h/2);await page.evaluate(()=>window.__SUNLINE__.sim.vehicle.angle=0);
  let s=await step(page,1600,['ArrowUp']);assert.ok(s.player.x<=wall.x-14);assert.ok(s.vehicle.health<100,'Wall impact damages the car');
  await page.evaluate(()=>{window.__SUNLINE__.sim.health=37;window.__SUNLINE__.sim.vehicle.health=25;});
  await teleport(page,750,1770);s=await tap(page,'r');assert.equal(s.vehicle.health,100);assert.equal(s.player.health,100);assert.equal(earnedCash(s),150);
  return{cash:s.cash,carHealth:s.vehicle.health};
});

await test('all five jobs and race checkpoints complete in order',async()=>{
  await fresh(page);await tap(page,'e');await step(page,200,['ArrowDown']);await tap(page,'e');
  await teleport(page,2450,2040);let s=await tap(page,'e');assert.equal(s.completed,1);assert.equal(earnedCash(s),650);
  await teleport(page,1380,1770);s=await tap(page,'e');assert.equal(s.mission.phase,'deliver');
  await teleport(page,1390,410);s=await tap(page,'e');assert.equal(s.mission.phase,'escape');assert.equal(s.wanted,2);
  await teleport(page,750,1770);s=await tap(page,'e');assert.equal(s.completed,1,'Wanted handoff must be rejected');
  assert.equal((await clearHeat(page)).wanted,0);await teleport(page,750,1770);s=await tap(page,'e');assert.equal(s.completed,2);
  s=await tap(page,'e');assert.equal(s.mission.phase,'race');
  const checkpoints=await page.evaluate(()=>window.__SUNLINE__.sim.checkpoints);
  for(let i=0;i<checkpoints.length;i++){
    // Place 100px from each gate and drive through its real trigger with acceleration.
    const cp=checkpoints[i];await teleport(page,cp.x-105,cp.y);await page.evaluate(()=>window.__SUNLINE__.sim.vehicle.angle=0);
    s=await step(page,1100,['ArrowUp']);if(i<checkpoints.length-1)assert.equal(s.mission.checkpoint,i+1);else assert.equal(s.completed,3);
  }
  await teleport(page,2450,2040);s=await tap(page,'e');assert.equal(s.mission.phase,'deliver');assert.ok(s.wanted>=2);
  assert.equal((await clearHeat(page)).wanted,0);await teleport(page,390,2440);s=await tap(page,'e');assert.equal(s.completed,4);
  await teleport(page,2180,1050);s=await tap(page,'e');assert.equal(s.mission.phase,'deliver');
  await teleport(page,2510,700);s=await tap(page,'e');assert.equal(s.completed,5);assert.equal(earnedCash(s),6650);
  assert.equal(s.mission.name,'Free roam');assert.match(await page.locator('#mission-banner').innerText(),/THE CITY IS YOURS/);
  await screenshot(page,'05-story-finished');const finished={completed:s.completed,cash:s.cash};await page.reload();await page.waitForFunction(()=>window.__SUNLINE__);s=await state(page);assert.equal(s.completed,5);assert.equal(earnedCash(s),6650);return finished;
});

await test('arrest penalty, failed job reset, recovery and car exit',async()=>{
  await fresh(page);await tap(page,'e');
  await page.evaluate(()=>{const s=window.__SUNLINE__.sim;s.addHeat(1);s.police=[{id:'qa-police',x:s.pos.x+15,y:s.pos.y,angle:Math.PI,speed:0,color:'#fff5df',type:'police',health:100,pursuing:true,pathTimer:0}];s.lastPoliceSpawn=s.time;});
  let s=await step(page,6000);assert.equal(s.wanted,0);assert.equal(earnedCash(s),100);assert.equal(s.mission.phase,'offer');assert.equal(s.player.health,100);assert.equal(s.player.x,2160);assert.match(await page.locator('#mission-banner').innerText(),/BUSTED/);
  await getCar(page);await teleport(page,2320,1160);s=await tap(page,'e');assert.equal(s.player.onFoot,true);assert.equal(s.vehicle,null);return {cash:s.cash,health:s.player.health};
});

await test('collectibles persist and reload restores completed jobs and valid position',async()=>{
  await fresh(page);const tape=await page.evaluate(()=>window.__SUNLINE__.sim.tapes[0]);await teleport(page,tape.x,tape.y);let s=await step(page,20);assert.equal(s.tapes.collected,1);assert.equal(earnedCash(s),350);
  const saved=await page.evaluate(()=>JSON.parse(localStorage.getItem('sunline-city-v1')));assert.deepEqual(saved.collected,[0]);
  await page.reload();await page.waitForFunction(()=>window.__SUNLINE__);s=await state(page);assert.equal(s.tapes.collected,1);assert.equal(earnedCash(s),350);assert.equal(s.player.x,tape.x);assert.equal(s.mode,'intro');
  await page.locator('#start-btn').click();await step(page,20);assert.equal((await state(page)).cash,350);return saved;
});

await test('all twelve tapes are reachable, reward once, and save',async()=>{
  await fresh(page);const tapes=await page.evaluate(()=>window.__SUNLINE__.sim.tapes.map(t=>({...t,solid:window.__SUNLINE__.world.isSolid(t.x,t.y,8)})));
  assert.equal(tapes.length,12);assert.ok(tapes.every(t=>!t.solid));
  for(const tape of tapes)await teleport(page,tape.x,tape.y);
  let s=await state(page);assert.equal(s.tapes.collected,12);assert.equal(earnedCash(s),1450);assert.match(await page.locator('#toast').innerText(),/All 12 mixtapes found/);
  await teleport(page,tapes[0].x,tapes[0].y);s=await state(page);assert.equal(earnedCash(s),1450);
  assert.equal(await page.evaluate(()=>JSON.parse(localStorage.getItem('sunline-city-v1')).collected.length),12);return{collected:s.tapes.collected,cash:s.cash};
});

await test('malformed saves fail safely; valid hostile fields are sanitized',async()=>{
  for(const raw of ['{bad json','null','[]',JSON.stringify({version:1,cash:-10,completed:2,collected:[]}),JSON.stringify({version:1,cash:100,completed:2.5,collected:[]})]){
    await page.evaluate(raw=>{window.__SUNLINE__.sim.mode='intro';localStorage.setItem('sunline-city-v1',raw);},raw);
    await page.reload();await page.waitForFunction(()=>window.__SUNLINE__);const s=await state(page);assert.equal(earnedCash(s),250);assert.equal(s.completed,0);
  }
  await page.evaluate(()=>{window.__SUNLINE__.sim.mode='intro';localStorage.setItem('sunline-city-v1',JSON.stringify({version:1,cash:300,completed:1,collected:[0,0,99,-1,'3',null,{}],stats:{distance:-9,escapes:'wrong'},player:{x:999999,y:0}}));});
  await page.reload();await page.waitForFunction(()=>window.__SUNLINE__);const s=await state(page);assert.equal(earnedCash(s),300);assert.equal(s.completed,1);assert.equal(s.tapes.collected,1);assert.equal(s.player.x,2160);return s;
});

await test('mobile 390×844 touch walking, interaction, car control, map and pause',async()=>{
  const mobile=await browser.newContext({viewport:{width:390,height:844},isMobile:true,hasTouch:true,deviceScaleFactor:1});
  await mobile.addInitScript(()=>{window.requestAnimationFrame=()=>0;window.WebSocket=class{addEventListener(){} send(){} close(){}};});const p=await mobile.newPage();watch(p);await p.goto(base);await p.waitForFunction(()=>window.__SUNLINE__);
  await screenshot(p,'06-mobile-intro');await p.locator('#start-btn').tap();await step(p,20);
  assert.equal(await p.locator('#touch-controls').isVisible(),true);
  const touchHold=async(selector,ms)=>{const b=p.locator(selector);await b.dispatchEvent('pointerdown',{pointerId:1,pointerType:'touch',bubbles:true});await step(p,ms);await b.dispatchEvent('pointerup',{pointerId:1,pointerType:'touch',bubbles:true});};
  await p.locator('#touch-e').tap();assert.equal((await state(p)).mission.phase,'vehicle');
  const y=(await state(p)).player.y;await touchHold('[data-key="ArrowDown"]',200);assert.ok((await state(p)).player.y>y+15);
  await p.locator('#touch-e').tap();assert.equal((await state(p)).vehicle?.id,'car-0');
  await touchHold('[data-key="ArrowUp"]',500);assert.ok((await state(p)).vehicle.speed>50);
  await touchHold('[data-key="Space"]',800);assert.ok((await state(p)).vehicle.speed<20);
  await screenshot(p,'07-mobile-gameplay');
  await p.locator('#map-btn').tap();assert.equal((await state(p)).mode,'map');await screenshot(p,'08-mobile-map');await p.locator('#modal-close').tap();
  await p.locator('#pause-btn').tap();assert.equal((await state(p)).mode,'paused');await screenshot(p,'09-mobile-pause');
  const overflow=await p.evaluate(()=>document.documentElement.scrollWidth>innerWidth);assert.equal(overflow,false);
  await mobile.close();
});

await test('mobile landscape retains usable touch controls',async()=>{
  const mobile=await browser.newContext({viewport:{width:844,height:390},isMobile:true,hasTouch:true,deviceScaleFactor:1});
  await mobile.addInitScript(()=>{window.requestAnimationFrame=()=>0;window.WebSocket=class{addEventListener(){} send(){} close(){}};});const p=await mobile.newPage();watch(p);await p.goto(base);await p.waitForFunction(()=>window.__SUNLINE__);
  await p.locator('#start-btn').tap();await step(p,20);await screenshot(p,'11-mobile-landscape');
  assert.equal(await p.locator('#touch-controls').isVisible(),true,'Movement buttons must remain visible on a rotated phone');
  const bounds=await p.locator('#touch-controls').boundingBox();assert.ok(bounds.x>=0&&bounds.y>=0&&bounds.x+bounds.width<=844&&bounds.y+bounds.height<=390);
  await mobile.close();
});

await fs.writeFile(new URL('results.json',out),JSON.stringify({results,errors},null,2));
console.log(JSON.stringify({passed:results.filter(x=>x.pass).length,total:results.length,errors},null,2));
await browser.close();
if(results.some(x=>!x.pass)||errors.some(x=>!x.message.includes('fonts.googleapis.com')))process.exitCode=1;
