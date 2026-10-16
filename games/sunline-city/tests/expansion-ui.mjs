import {chromium} from 'playwright';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const out=new URL('../output/expansion-ui/',import.meta.url);await fs.mkdir(out,{recursive:true});
const browser=await chromium.launch({headless:true,channel:'chrome'}),errors=[],results=[];
async function pageFor(viewport,touch=false){const c=await browser.newContext({viewport,hasTouch:touch,isMobile:touch});await c.addInitScript(()=>{window.requestAnimationFrame=()=>0;window.WebSocket=class{addEventListener(){} send(){} close(){}};});const p=await c.newPage();p.on('pageerror',e=>errors.push(e.message));await p.goto('http://127.0.0.1:5174');await p.waitForFunction(()=>window.__SUNLINE__);return p;}
const page=await pageFor({width:1440,height:900});
const step=(ms=20)=>page.evaluate(ms=>window.advanceTime(ms),ms);
const tap=async key=>{await page.keyboard.press(key);await step();};
const fresh=async()=>{await page.evaluate(()=>window.__SUNLINE__.reset());await page.locator('#start-btn').click();await step();};
const shot=async(name,p=page)=>{await p.evaluate(()=>{window.__SUNLINE__.updateUI();window.__SUNLINE__.render();});await p.screenshot({path:new URL(`${name}.png`,out).pathname});};
const check=async(name,fn)=>{try{await fn();results.push({name,pass:true});console.log('PASS',name);}catch(e){results.push({name,pass:false,error:e.message});console.log('FAIL',name,e.message);await shot('failure-'+results.length);}};

await check('county dimensions, clear POIs, connected roads and valid new car types',async()=>{
 const info=await page.evaluate(()=>{const{world,sim}=window.__SUNLINE__;return {width:world.width,height:world.height,blocked:world.pois.filter(p=>world.isSolid(p.x,p.y,30)),models:[...new Set(sim.cars.map(c=>c.type))],buildings:world.buildings.length,ramps:world.stuntRamps.length};});
 assert.equal(info.width,5200);assert.equal(info.height,5700);assert.deepEqual(info.blocked,[]);assert.ok(info.buildings>=65);assert.ok(info.models.every(t=>['sport','sedan','van','police'].includes(t)));assert.equal(info.ramps,6);
});
await check('adventure guide opens, labels9activities, navigates and returns focus',async()=>{
 await fresh();await tap('j');assert.equal(await page.locator('.activity-card').count(),9);await shot('01-adventures');
 await page.locator('[data-guide="taxi"]').click();await step();
 assert.equal(await page.evaluate(()=>window.__SUNLINE__.sim.customWaypoint.activityId),'taxi');assert.equal(await page.evaluate(()=>window.__SUNLINE__.sim.mode),'play');assert.equal(await page.evaluate(()=>document.activeElement.id),'game');
});
await check('map region and filter controls work; marker click sets exact destination',async()=>{
 await tap('m');await shot('02-whole-county');
 await page.locator('[data-region="south"]').click();assert.match(await page.locator('#full-map svg').getAttribute('viewBox'),/2770/);await shot('03-south-map');
 await page.locator('#map-filter').selectOption('services');assert.equal(await page.locator('#full-map [data-activity]').count(),0);
 await page.locator('#full-map [data-service="gunshop"]').click();await step();assert.equal(await page.evaluate(()=>window.__SUNLINE__.sim.customWaypoint.name),await page.evaluate(()=>window.__SUNLINE__.world.pois.find(p=>p.key==='gunshop').name));
});
await check('workshop transactions improve driving and persist by model',async()=>{
 await fresh();await page.evaluate(()=>{const d=window.__SUNLINE__,s=d.sim,c=s.cars[0];s.vehicle=c;c.occupied=true;c.owned=true;s.cash=5000;d.teleport(750,1770);c.health=25;});
 await tap('g');assert.match(await page.locator('#modal-panel').innerText(),/Make it your ride/);await shot('04-workshop');
 await page.locator('[data-upgrade="engine"]').click();await page.locator('[data-upgrade="handling"]').click();await page.locator('[data-paint="#78b7b5"]').click();await page.locator('[data-upgrade="repair"]').click();
 let result=await page.evaluate(()=>({cash:window.__SUNLINE__.sim.cash,tune:window.__SUNLINE__.sim.county.tuning(),health:window.__SUNLINE__.sim.vehicle.health,color:window.__SUNLINE__.sim.vehicle.color,save:JSON.parse(localStorage.getItem('sunline-city-v1'))}));
 assert.equal(result.cash,3830);assert.equal(result.tune.engine,1);assert.equal(result.tune.handling,true);assert.equal(result.color,'#78b7b5');assert.equal(result.health,100);assert.equal(result.save.county.tunes.sport.engine,1);
 await page.reload();await page.waitForFunction(()=>window.__SUNLINE__);assert.equal(await page.evaluate(()=>window.__SUNLINE__.sim.county.tunes.sport.engine),1);
});
await check('all six stunt jumps use real acceleration and reward once',async()=>{
 await fresh();const ramps=await page.evaluate(()=>window.__SUNLINE__.world.stuntRamps);
 for(const r of ramps){
  const result=await page.evaluate(r=>{const d=window.__SUNLINE__,s=d.sim,c=s.cars[0];s.vehicle=c;c.owned=true;c.occupied=true;c.health=100;s.wanted=0;s.police=[];s.county.rampCooldown=0;c.angle=r.angle;d.teleport(r.x-Math.cos(r.angle)*120,r.y-Math.sin(r.angle)*120);c.speed=180;const before=s.cash,visits=s.exploration.visited.size,tapes=s.collected.size;
   window.dispatchEvent(new KeyboardEvent('keydown',{code:'ArrowUp'}));window.advanceTime(1900);window.dispatchEvent(new KeyboardEvent('keyup',{code:'ArrowUp'}));
   return {id:r.id,done:s.county.jumps.has(r.id),cash:s.cash-before-(s.exploration.visited.size-visits)*40-(s.collected.size-tapes)*100,health:c.health,x:c.x,y:c.y};},r);
  assert.ok(result.done,JSON.stringify(result));assert.equal(result.cash,300);
 }
 assert.equal(await page.evaluate(()=>window.__SUNLINE__.sim.county.jumps.size),6);await shot('05-stunts');
 const repeat=await page.evaluate(r=>{const d=window.__SUNLINE__,s=d.sim,c=s.vehicle;s.county.rampCooldown=0;c.angle=r.angle;d.teleport(r.x-Math.cos(r.angle)*120,r.y-Math.sin(r.angle)*120);c.speed=180;const before=s.cash,visits=s.exploration.visited.size,tapes=s.collected.size;window.dispatchEvent(new KeyboardEvent('keydown',{code:'ArrowUp'}));window.advanceTime(1900);window.dispatchEvent(new KeyboardEvent('keyup',{code:'ArrowUp'}));return s.cash-before;},ramps[0]);assert.equal(repeat,0);
});
await check('new districts render with no page errors',async()=>{
 await fresh();for(const [name,x,y]of[['downtown',2500,3500],['docks',4350,4100],['park',1720,4860],['speedway',3920,4750],['hills',850,3490]]){await page.evaluate(({x,y})=>window.__SUNLINE__.teleport(x,y),{x,y});await step(200);await shot('district-'+name);}
});
await check('mobile guide, arsenal, and county map fit portrait and landscape',async()=>{
 for(const [label,viewport]of[['portrait',{width:390,height:844}],['landscape',{width:844,height:390}]]){
  const p=await pageFor(viewport,true);await p.locator('#start-btn').tap();await p.evaluate(()=>window.advanceTime(30));await p.locator('#adventures-btn').tap();assert.equal(await p.locator('.activity-card').count(),9);await shot(`mobile-${label}-guide`,p);
  assert.equal(await p.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false);await p.locator('#modal-close').tap();await p.locator('#map-btn').tap();await shot(`mobile-${label}-map`,p);await p.locator('#modal-close').tap();await p.evaluate(()=>window.__SUNLINE__.openModal('arsenal'));await shot(`mobile-${label}-arsenal`,p);await p.context().close();
 }
});
await fs.writeFile(new URL('results.json',out),JSON.stringify({results,errors},null,2));console.log(JSON.stringify({passed:results.filter(r=>r.pass).length,total:results.length,errors}));await browser.close();if(results.some(r=>!r.pass)||errors.length)process.exitCode=1;
