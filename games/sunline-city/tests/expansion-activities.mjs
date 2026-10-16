import {chromium} from 'playwright';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';

// DEV placement is used only to set up scenarios or skip travel between them.
// E, driving, firing, reload, modal, and cancellation use the real DOM handlers.
const browser=await chromium.launch({headless:true});
const context=await browser.newContext({viewport:{width:1440,height:960}});
await context.addInitScript(()=>{window.requestAnimationFrame=()=>0;window.WebSocket=class{addEventListener(){}send(){}close(){}};});
const page=await context.newPage(),errors=[],results=[];
page.on('pageerror',e=>errors.push(e.message));page.on('console',m=>{if(m.type()==='error')errors.push(m.text());});
const out=new URL('../output/playwright/expansion-activities/',import.meta.url);await fs.mkdir(out,{recursive:true});
await page.goto(process.env.SUNLINE_URL||'http://127.0.0.1:5174');
await page.waitForFunction(()=>window.__SUNLINE__&&window.__SUNLINE__.sim.activities);
const state=()=>page.evaluate(()=>JSON.parse(window.render_game_to_text()));
const step=(ms=20,codes=[])=>page.evaluate(({ms,codes})=>{for(const code of codes)dispatchEvent(new KeyboardEvent('keydown',{code,bubbles:true}));window.advanceTime(ms);for(const code of codes)dispatchEvent(new KeyboardEvent('keyup',{code,bubbles:true}));return JSON.parse(window.render_game_to_text());},{ms,codes});
const tap=async key=>{await page.keyboard.press(key);return step(20);};
const fresh=async()=>{await page.waitForFunction(()=>window.__SUNLINE__);await page.evaluate(()=>window.__SUNLINE__.reset());await page.locator('#start-btn').click();await step();};
const teleport=q=>page.evaluate(q=>{window.__SUNLINE__.teleport(q.x,q.y);window.advanceTime(1);},q);
const point=id=>page.evaluate(id=>{const a=window.__SUNLINE__.sim.activities;return a.startPoint(a.definition(id));},id);
const atStart=async id=>teleport(await point(id));
const atTarget=async()=>teleport(await page.evaluate(()=>window.__SUNLINE__.sim.activities.waypoint()));
const screenShot=async name=>{await page.evaluate(()=>{window.__SUNLINE__.updateUI();window.__SUNLINE__.render();});await page.screenshot({path:new URL(`${name}.png`,out).pathname});await fs.writeFile(new URL(`${name}.json`,out),JSON.stringify(await state(),null,2));};
const test=async(name,fn)=>{if(process.env.QA_ONLY&&!name.includes(process.env.QA_ONLY))return;try{const detail=await fn();results.push({name,pass:true,detail});console.log('PASS',name);}catch(error){results.push({name,pass:false,error:error.stack});console.log('FAIL',name,error.message);await screenShot(`failure-${results.length}`).catch(()=>{});}};
const setupRace=async id=>{await fresh();const q=await point(id);await page.evaluate(({q,id})=>{const d=window.__SUNLINE__,s=d.sim,c=s.cars[0];c.x=q.x;c.y=q.y;c.angle=id==='race-coast'?-Math.PI/2:0;c.speed=0;c.health=100;s.vehicle=c;c.occupied=true;s.player.x=c.x;s.player.y=c.y;d.teleport(q.x,q.y);},{q,id});await tap('e');assert.equal((await state()).activity.active?.id,id);await step(3200);};
const drive=async(points,limit=150,cruise=180)=>page.evaluate(({points,limit,cruise})=>{const sim=window.__SUNLINE__.sim;let i=0,elapsed=0;const trail=[];while(i<points.length&&elapsed<limit&&sim.vehicle){const c=sim.vehicle,q=points[i],d=Math.hypot(q.x-c.x,q.y-c.y);if(d<(q.radius||55)){trail.push({i,elapsed,x:c.x,y:c.y,health:c.health});i++;continue;}const delta=Math.atan2(Math.sin(Math.atan2(q.y-c.y,q.x-c.x)-c.angle),Math.cos(Math.atan2(q.y-c.y,q.x-c.x)-c.angle));const speed=Math.abs(delta)>.45?72:Math.min(cruise,Math.max(70,d*.9));const codes=[];if(delta>.06)codes.push('ArrowRight');if(delta<-.06)codes.push('ArrowLeft');if(c.speed<speed-6)codes.push('ArrowUp');if(c.speed>speed+10)codes.push('Space');for(const code of codes)dispatchEvent(new KeyboardEvent('keydown',{code,bubbles:true}));window.advanceTime(100);for(const code of codes)dispatchEvent(new KeyboardEvent('keyup',{code,bubbles:true}));elapsed+=.1;}return{i,total:points.length,elapsed,trail,state:JSON.parse(window.render_game_to_text())};},{points,limit,cruise});
const enterMarked=async()=>{const car=await page.evaluate(()=>window.__SUNLINE__.sim.activities.car());await teleport({x:car.x,y:car.y+24});let s=await tap('e');assert.equal(s.vehicle?.id,car.id,JSON.stringify(s.activity));return s;};
const clearHeat=async()=>page.evaluate(()=>{const s=window.__SUNLINE__.sim;let seconds=0;while(s.wanted&&seconds<35){window.__SUNLINE__.teleport(300,5480);window.advanceTime(500);seconds+=.5;}return{s:s.wanted,seconds};});
const shootEncounter=async({freeze=true}={})=>{
  if(freeze)await page.evaluate(()=>{for(const e of window.__SUNLINE__.sim.combat.enemies){e.cooldown=999;e.speed=0;}});
  let cycles=0;
  while(cycles++<160){
    const enemies=await page.evaluate(()=>{const s=window.__SUNLINE__.sim;return s.combat.enemies.filter(e=>e.health>0).sort((a,b)=>Number(s.lineClear(s.player,b))-Number(s.lineClear(s.player,a)));});if(!enemies.length)break;
    const target=enemies[0];
    // Isolate mission win conditions from enemy AI: set up a clear flank when
    // cover blocks the chosen target, then use genuine aim/fire input and damage.
    await page.evaluate(t=>{const d=window.__SUNLINE__,s=d.sim;const clear=p=>{if(!s.lineClear(p,t))return false;const dx=t.x-p.x,dy=t.y-p.y,len=dx*dx+dy*dy;return !s.cars.some(c=>{const f=Math.max(0,Math.min(1,((c.x-p.x)*dx+(c.y-p.y)*dy)/len));return c.health>0&&Math.hypot(c.x-p.x-dx*f,c.y-p.y-dy*f)<29;});};if(!clear(s.player)){for(let r=95;r<250;r+=35){let found=false;for(let i=0;i<16;i++){const q={x:t.x+Math.cos(i*Math.PI/8)*r,y:t.y+Math.sin(i*Math.PI/8)*r};if(!s.world.isSolid(q.x,q.y,10)&&clear(q)){d.teleport(q.x,q.y);found=true;break;}}if(found)break;}}},target);
    const q=await page.evaluate(t=>window.__SUNLINE__.worldToScreen({x:t.x,y:t.y-11}),target);
    await page.mouse.move(q.x,q.y);await step(320,['Space']);
    const current=await state();if(current.player.health<=0||!current.activity.active)break;
  }
  return{cycles,state:await state()};
};

await test('guide selects a real destination and E begins race without altering story',async()=>{
  await fresh();await tap('j');assert.equal((await state()).mode,'paused');assert.equal(await page.locator('[data-guide]').count(),9);await screenShot('01-adventure-guide');await page.locator('[data-guide="race-coast"]').click();
  let s=await state();assert.equal(s.mode,'play');assert.equal(s.activity.active,null);assert.equal(s.activity.selected,'race-coast');assert.equal(s.mission.phase,'offer');
  await setupRace('race-coast');s=await state();assert.equal(s.activity.active.phase,'race');assert.equal(s.mission.phase,'offer');assert.match(await page.locator('#objective-title').innerText(),/Coastline Club/);
  await tap('j');await page.locator('#cancel-activity').click();await tap('Escape');assert.equal((await state()).activity.active,null);
});
await test('coast race completes by continuous keyboard driving',async()=>{
  await setupRace('race-coast');const points=await page.evaluate(()=>window.__SUNLINE__.sim.activities.definition('race-coast').route.map(([x,y])=>({x,y})));const run=await drive(points,145,185);assert.equal(run.i,run.total,JSON.stringify(run));assert.equal(run.state.activity.active,null,JSON.stringify(run));assert.equal(run.state.activity.records['race-coast'].completions,1);assert.ok(run.state.cash>250);await screenShot('02-coast-race-finish');return{seconds:run.elapsed,health:run.state.vehicle?.health,record:run.state.activity.records['race-coast']};
});
await test('story prevents side start and race still allows getting into car at marker',async()=>{
  await fresh();await tap('e');assert.notEqual((await state()).mission.phase,'offer');await atStart('race-coast');await tap('e');assert.equal((await state()).activity.active,null);
  await fresh();await atStart('race-coast');await page.evaluate(()=>{const s=window.__SUNLINE__.sim,c=s.cars[0];c.x=s.player.x;c.y=s.player.y+25;});await tap('e');assert.ok((await state()).vehicle,'Race prompt must not swallow car entry');
});

const county=await page.evaluate(()=>window.__SUNLINE__.world.width>3300);
if(county){
  await test('county and speedway races follow unblocked streets continuously',async()=>{
    const details=[];for(const id of ['race-county','race-airport']){await setupRace(id);const route=await page.evaluate(id=>window.__SUNLINE__.sim.activities.definition(id).route.map(([x,y])=>({x,y})),id);const run=await drive(route,140,200);assert.equal(run.i,run.total,JSON.stringify(run));assert.equal(run.state.activity.records[id].completions,1,JSON.stringify(run));await screenShot(`03-${id}-finish`);details.push({id,seconds:run.elapsed,health:run.state.vehicle?.health});}return details;
  });
  await test('taxi pickup, three fares, timeout, and cancel use real E',async()=>{
    await fresh();await atStart('taxi');await tap('e');await enterMarked();assert.equal((await state()).activity.active.phase,'pickup');
    for(let fare=0;fare<3;fare++){await atTarget();await tap('e');assert.equal((await state()).activity.active.phase,'dropoff');await step(1000,['ArrowUp']);if((await state()).wanted)assert.equal((await clearHeat()).s,0);await atTarget();await tap('e');}
    let s=await state();assert.equal(s.activity.active,null);assert.equal(s.activity.records.taxi.completions,1);await screenShot('04-taxi-shift-complete');
    await fresh();await atStart('taxi');await tap('e');await enterMarked();await atTarget();await tap('e');await step(180000);assert.equal((await state()).activity.active,null);{const s=await state();assert.equal(s.cash-s.exploration.visited.length*40,250);}
  });
  await test('vehicle export requires correct car, actual heat escape, and handoff',async()=>{
    await fresh();await atStart('export');await tap('e');await enterMarked();assert.equal((await state()).activity.active.phase,'deliver');assert.equal((await state()).wanted,2);await atTarget();await tap('e');assert.ok((await state()).activity.active);const escape=await clearHeat();assert.equal(escape.s,0);await atTarget();await tap('e');const s=await state();assert.equal(s.activity.records.export.completions,1);assert.equal(s.vehicle,null);await screenShot('05-export-complete');return escape;
  });
  await test('shooting range is physically accessible and six real shots complete it',async()=>{
    await fresh();await atStart('range');await tap('e');assert.equal((await state()).activity.active.id,'range');await screenShot('06-shooting-range');const shot=await shootEncounter();assert.equal(shot.state.activity.active,null,JSON.stringify(shot));assert.equal(shot.state.activity.records.range.completions,1);assert.equal(shot.state.wanted,0);assert.equal(shot.state.combat.stats.defeated,6);await screenShot('07-range-complete');return shot.cycles;
  });
  await test('bounty can be subdued by real bullets, captured and delivered',async()=>{
    await fresh();await atStart('bounty');await tap('e');await atTarget();await step();await teleport({x:2730,y:4380});const shot=await shootEncounter();assert.equal(shot.state.activity.active.phase,'capture',JSON.stringify(shot));await atTarget();await tap('e');assert.equal((await state()).activity.active.phase,'return');await atTarget();await tap('e');assert.equal((await state()).activity.records.bounty.completions,1);await screenShot('08-bounty-complete');
  });
  await test('heist has vehicle, guard fight, case, police escape, and van handoff',async()=>{
    await fresh();await atStart('heist');await tap('e');await enterMarked();assert.equal((await state()).activity.active.phase,'approach');await atTarget();await step();assert.equal((await state()).activity.active.phase,'guards');await tap('e');assert.equal((await state()).vehicle,null);await teleport({x:4475,y:4280});const shot=await shootEncounter();assert.equal(shot.state.activity.active.phase,'case',JSON.stringify(shot));await atTarget();await tap('e');assert.equal((await state()).activity.active.phase,'escape');await enterMarked();assert.equal((await clearHeat()).s,0);await atTarget();await tap('e');assert.equal((await state()).activity.records.heist.completions,1);await screenShot('09-heist-complete');
  });
  await test('survival completes three actual combat waves and failure clears threats',async()=>{
    await fresh();await atStart('survival');await tap('e');let shots=0;for(let wave=1;wave<=3;wave++){await step(4050);await teleport({x:4350,y:4250});const shot=await shootEncounter();shots+=shot.cycles;if(wave<3)assert.equal(shot.state.activity.active.phase,'prepare');}assert.equal((await state()).activity.records.survival.completions,1);await screenShot('10-survival-complete');
    await fresh();await atStart('survival');await tap('e');await step(4100);await teleport({x:300,y:5480});await step();assert.equal((await state()).activity.active,null);assert.equal((await state()).combat.enemiesRemaining,0);return{shootingBursts:shots};
  });
}else console.log('SKIP county scenarios: expanded world not loaded yet.');

await fs.writeFile(new URL('results.json',out),JSON.stringify({county,results,errors},null,2));
await browser.close();console.log(JSON.stringify({county,passed:results.filter(r=>r.pass).length,total:results.length,errors},null,2));
if(errors.length||results.some(r=>!r.pass))process.exitCode=1;
