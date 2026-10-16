import {chromium} from 'playwright';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';

// Isolated setup places the player at contacts; all actions under test use the
// actual mobile buttons. This catches differences from keyboard-only scenarios.
const out=new URL('../output/playwright/release-regression/',import.meta.url);
await fs.mkdir(out,{recursive:true});
const browser=await chromium.launch({headless:true,channel:'chrome'});
const errors=[],results=[];
const context=await browser.newContext({viewport:{width:390,height:844},isMobile:true,hasTouch:true});
await context.addInitScript(()=>{window.requestAnimationFrame=()=>0;window.WebSocket=class{addEventListener(){}send(){}close(){}};});
const page=await context.newPage();
page.on('pageerror',e=>errors.push(e.message));
page.on('console',m=>{if(m.type()==='error')errors.push(m.text());});
await page.goto(process.env.SUNLINE_URL||'http://127.0.0.1:5174');
await page.waitForFunction(()=>window.__SUNLINE__);
const step=(ms=20)=>page.evaluate(ms=>window.advanceTime(ms),ms);
const state=()=>page.evaluate(()=>JSON.parse(window.render_game_to_text()));
const fresh=async()=>{await page.evaluate(()=>window.__SUNLINE__.reset());await page.locator('#start-btn').tap();await step();};
const shot=async name=>{await page.evaluate(()=>{window.__SUNLINE__.updateUI();window.__SUNLINE__.render();});await page.screenshot({path:new URL(`${name}.png`,out).pathname});};
const test=async(name,fn)=>{try{await fn();results.push({name,pass:true});console.log('PASS',name);}catch(e){results.push({name,pass:false,error:e.stack});console.log('FAIL',name,e.message);await shot(`failure-${results.length}`).catch(()=>{});}};

await test('touch E completes Sol Auto handoff and starts Sol race without opening workshop',async()=>{
  await fresh();
  await page.evaluate(()=>{const d=window.__SUNLINE__,s=d.sim,c=s.cars[0];s.vehicle=c;c.occupied=true;c.speed=0;s.completed=1;s.job={index:1,phase:'escape',timer:0,checkpoint:0};d.teleport(750,1770);d.updateUI();});
  assert.equal(await page.evaluate(()=>window.__SUNLINE__.sim.context().kind),'job');
  await page.locator('#touch-e').tap();await step();
  assert.equal((await state()).completed,2);assert.equal((await state()).mode,'play');
  assert.equal(await page.locator('#modal').isVisible(),false);
  await page.locator('#touch-e').tap();await step();
  assert.equal((await state()).mission.phase,'race');assert.equal((await state()).mode,'play');
  await shot('01-mobile-story-race');
});

await test('touch E enters and exits car at weapon shop while separate G opens shop',async()=>{
  await fresh();
  await page.evaluate(()=>{const d=window.__SUNLINE__,s=d.sim,p=d.world.pois.find(p=>p.key==='gunshop'),c=s.cars[0];c.x=p.x;c.y=p.y+30;c.speed=0;d.teleport(p.x,p.y);d.updateUI();});
  assert.equal(await page.locator('#touch-service').isVisible(),true);
  await page.locator('#touch-e').tap();await step();assert.ok((await state()).vehicle);
  await page.locator('#touch-e').tap();await step();assert.equal((await state()).vehicle,null);
  assert.equal(await page.locator('#modal').isVisible(),false);
  await page.locator('#touch-service').tap();assert.ok(await page.locator('[data-buy="smg"]').isVisible());
  await page.locator('#modal-close').tap();assert.equal((await state()).mode,'play');
  await shot('02-mobile-shop-controls');
});

await test('touch G opens workshop and paused modal freezes simulation',async()=>{
  await fresh();
  await page.evaluate(()=>{const d=window.__SUNLINE__,s=d.sim,c=s.cars[0];s.vehicle=c;c.occupied=true;s.cash=2000;d.teleport(750,1770);d.updateUI();});
  await page.locator('#touch-service').tap();assert.equal(await page.locator('#modal-title').innerText(),'Make it your ride.');
  const before=await state();await step(3000);assert.equal((await state()).time,before.time);
  await page.locator('[data-upgrade="engine"]').tap();assert.equal(await page.evaluate(()=>window.__SUNLINE__.sim.county.tuning().engine),1);
  await page.locator('#modal-close').tap();assert.equal((await state()).mode,'play');
});

await test('portrait and landscape combat plus service controls do not overlap',async()=>{
  await fresh();
  await page.evaluate(()=>{const d=window.__SUNLINE__,p=d.world.pois.find(p=>p.key==='gunshop');d.teleport(p.x,p.y);d.updateUI();});
  for(const [label,viewport]of [['portrait',{width:390,height:844}],['landscape',{width:844,height:390}]]){
    await page.setViewportSize(viewport);await step();
    const report=await page.evaluate(()=>{
      const nodes=[...document.querySelectorAll('#touch-controls button,#touch-extra button')].filter(e=>e.getBoundingClientRect().width>0);
      const boxes=nodes.map(e=>({name:e.id||e.dataset.key,r:e.getBoundingClientRect().toJSON()}));
      const overlaps=[];
      for(let i=0;i<boxes.length;i++)for(let j=i+1;j<boxes.length;j++){const a=boxes[i],b=boxes[j];if(a.r.left<b.r.right&&a.r.right>b.r.left&&a.r.top<b.r.bottom&&a.r.bottom>b.r.top)overlaps.push([a.name,b.name]);}
      return {overlaps,outside:boxes.filter(({r})=>r.left<0||r.right>innerWidth||r.top<0||r.bottom>innerHeight),count:boxes.length};
    });
    assert.deepEqual(report.overlaps,[]);assert.deepEqual(report.outside,[]);assert.equal(report.count,11);await shot(`03-${label}-controls`);
  }
  await page.setViewportSize({width:390,height:844});
});

await test('reloading during an adventure preserves purchased weapons, ammo, tuning and previous records',async()=>{
  await fresh();
  await page.evaluate(()=>{const d=window.__SUNLINE__,s=d.sim,p=d.world.pois.find(p=>p.key==='gunshop');s.cash=5000;d.teleport(p.x,p.y);d.updateUI();});
  await page.locator('#touch-service').tap();await page.locator('[data-buy="smg"]').tap();await page.locator('#modal-close').tap();
  await page.evaluate(()=>{const d=window.__SUNLINE__,s=d.sim,c=s.cars[0];s.vehicle=c;c.occupied=true;d.teleport(750,1770);d.updateUI();});
  await page.locator('#touch-service').tap();await page.locator('[data-upgrade="engine"]').tap();await page.locator('#modal-close').tap();
  await page.evaluate(()=>{const d=window.__SUNLINE__,s=d.sim;s.vehicle.occupied=false;s.vehicle=null;const p=d.world.pois.find(p=>p.key==='gunrange');d.teleport(p.x,p.y);s.activities.records.taxi={completions:2,bestTime:null,medal:'none',earnings:1700};d.updateUI();});
  await page.locator('#touch-e').tap();await step();assert.equal((await state()).activity.active.id,'range');
  const fire=await page.locator('[data-key="Space"]').boundingBox(),cdp=await context.newCDPSession(page);
  await cdp.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[{x:fire.x+fire.width/2,y:fire.y+fire.height/2}]});
  await step(400);await cdp.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]});await cdp.detach();
  const before=await state();assert.ok(before.combat.ammo<30);
  await page.reload();await page.waitForFunction(()=>window.__SUNLINE__);await page.locator('#start-btn').tap();await step();
  const after=await state();assert.equal(after.activity.active,null);assert.equal(after.combat.enemiesRemaining,0);assert.equal(after.combat.selected,'smg');assert.equal(after.combat.ammo,before.combat.ammo);assert.equal(after.combat.reserve,before.combat.reserve);assert.equal(after.cash,before.cash);assert.equal(after.activity.records.taxi.completions,2);assert.equal(await page.evaluate(()=>window.__SUNLINE__.sim.county.tunes.sport.engine),1);
});

await fs.writeFile(new URL('results.json',out),JSON.stringify({results,errors},null,2));
await browser.close();console.log(JSON.stringify({passed:results.filter(r=>r.pass).length,total:results.length,errors}));
if(errors.length||results.some(r=>!r.pass))process.exitCode=1;
