import {chromium} from 'playwright';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';

const out=new URL('../output/playwright/neighborhood-release/',import.meta.url);
await fs.mkdir(out,{recursive:true});
const browser=await chromium.launch({headless:true,channel:'chrome'}),errors=[],results=[];
const context=await browser.newContext({viewport:{width:390,height:844},isMobile:true,hasTouch:true});
await context.addInitScript(()=>{window.requestAnimationFrame=()=>0;window.WebSocket=class{addEventListener(){}send(){}close(){}};});
const page=await context.newPage();
page.on('pageerror',e=>errors.push(e.message));page.on('console',m=>{if(m.type()==='error')errors.push(m.text());});
await page.goto(process.env.SUNLINE_URL||'http://127.0.0.1:5174');await page.waitForFunction(()=>window.__SUNLINE__);
const step=(ms=20)=>page.evaluate(ms=>window.advanceTime(ms),ms);
const state=()=>page.evaluate(()=>JSON.parse(window.render_game_to_text()));
const fresh=async()=>{await page.evaluate(()=>window.__SUNLINE__.reset());await page.locator('#start-btn').tap();await step();};
const shot=async name=>{await page.evaluate(()=>{window.__SUNLINE__.updateUI();window.__SUNLINE__.render();});await page.screenshot({path:new URL(name+'.png',out).pathname});};
const test=async(name,fn)=>{try{await fn();results.push({name,pass:true});console.log('PASS',name);}catch(e){results.push({name,pass:false,error:e.stack});console.log('FAIL',name,e.message);await shot('failure-'+results.length).catch(()=>{});}};

await test('touch E re-enters a car parked on each food marker, including injured players',async()=>{
 for(let i=0;i<4;i++){
  await fresh();await page.evaluate(i=>{const d=window.__SUNLINE__,s=d.sim,f=s.exploration.foodStands[i],c=s.cars[0];s.vehicle=c;c.owned=true;c.occupied=true;c.speed=0;c.angle=0;d.teleport(f.x,f.y);d.updateUI();},i);
  await page.locator('#touch-e').tap();await step();assert.equal((await state()).vehicle,null);
  await page.evaluate(()=>{window.__SUNLINE__.sim.health=70;window.__SUNLINE__.updateUI();});
  assert.equal(await page.evaluate(()=>window.__SUNLINE__.sim.context().kind),'enter');
  await page.locator('#touch-e').tap();await step();assert.ok((await state()).vehicle);assert.equal((await state()).player.health,70);assert.equal((await state()).exploration.meals,0);
 }
});

await test('walking toward the food stand chooses the meal, then walking toward the car chooses entry',async()=>{
 await fresh();await page.evaluate(()=>{const d=window.__SUNLINE__,s=d.sim,f=s.exploration.foodStands[0],c=s.cars[0];c.x=f.x+50;c.y=f.y;c.speed=0;c.owned=true;c.parked=true;s.health=65;d.teleport(f.x,f.y);d.updateUI();});
 assert.equal(await page.evaluate(()=>window.__SUNLINE__.sim.context().kind),'food');
 await page.locator('#touch-e').tap();await step();assert.equal((await state()).player.health,100);assert.equal((await state()).exploration.meals,1);assert.equal((await state()).cash,220);
 await page.keyboard.down('d');await step(300);await page.keyboard.up('d');assert.equal(await page.evaluate(()=>window.__SUNLINE__.sim.context().kind),'enter');
 await page.locator('#touch-e').tap();await step();assert.ok((await state()).vehicle);await shot('01-food-car');
});

await test('all nine canvas postcards and ten discoveries persist without duplicate payouts',async()=>{
 await fresh();await page.setViewportSize({width:1440,height:900});
 const marks=await page.evaluate(()=>window.__SUNLINE__.sim.exploration.landmarks);
 for(const mark of marks){await page.evaluate(p=>window.__SUNLINE__.teleport(p.x,p.y),mark);await step(100);await page.keyboard.press('c');}
 const stops=await page.evaluate(()=>window.__SUNLINE__.sim.exploration.transitStops);
 for(const stop of stops){await page.evaluate(p=>window.__SUNLINE__.teleport(p.x,p.y),stop);await step(1300);}
 const before=await page.evaluate(()=>({cash:window.__SUNLINE__.sim.cash,exploration:window.__SUNLINE__.sim.exploration.serialize()}));
 assert.equal(before.exploration.photos.length,9);assert.equal(before.exploration.visited.length,10);assert.equal(Object.keys(before.exploration.album).length,9);assert.equal(before.exploration.collectionPaid,true);
 await page.reload();await page.waitForFunction(()=>window.__SUNLINE__);await page.click('#start-btn');await step(30);
 const restored=await page.evaluate(()=>({cash:window.__SUNLINE__.sim.cash,exploration:window.__SUNLINE__.sim.exploration.serialize()}));assert.deepEqual(restored,before);
 await page.evaluate(p=>window.__SUNLINE__.teleport(p.x,p.y),marks[0]);await step(1300);await page.keyboard.press('c');assert.equal((await state()).cash,before.cash);
 await page.keyboard.press('p');assert.equal(await page.locator('.postcard img').count(),9);await page.locator('.postcard img').first().scrollIntoViewIfNeeded();await shot('02-album');
});

await test('journal pause freezes driving and closing it clears held boost input',async()=>{
 await fresh();await page.evaluate(()=>{const d=window.__SUNLINE__,s=d.sim,c=s.cars[0];s.vehicle=c;c.owned=true;c.occupied=true;c.angle=0;d.teleport(1800,3020);});
 await page.keyboard.down('Shift');await page.keyboard.down('w');await step(700);assert.equal((await state()).driving.boosting,true);
 await page.keyboard.press('p');const before=await state();await step(5000);const after=await state();assert.equal(after.time,before.time);assert.deepEqual(after.driving,before.driving);assert.equal(after.vehicle.speed,before.vehicle.speed);
 await page.keyboard.up('Shift');await page.keyboard.up('w');await page.keyboard.press('p');await step(600);assert.equal((await state()).mode,'play');assert.equal((await state()).driving.boosting,false);assert.ok((await state()).driving.charge>before.driving.charge);
});

await test('map selection survives regional filtering and lighting survives reload',async()=>{
 await fresh();await page.keyboard.press('m');await page.selectOption('#map-filter','explore');assert.equal(await page.locator('#full-map [data-explore]').count(),23);
 await page.locator('[data-region="south"]').click();assert.equal(await page.locator('#map-filter').inputValue(),'explore');await page.locator('[data-explore="photo:cypress"]').click();assert.equal((await state()).mode,'play');assert.equal(await page.evaluate(()=>window.__SUNLINE__.sim.customWaypoint.name),'Cypress Boardwalk');
 await page.keyboard.press('p');await page.selectOption('#light-select','blue');await page.keyboard.press('p');await shot('03-blue-hour');await page.reload();await page.waitForFunction(()=>window.__SUNLINE__);assert.equal(await page.evaluate(()=>window.__SUNLINE__.settings.light),'blue');
});

await test('portrait and landscape photo, service and boost controls remain separated and reachable',async()=>{
 await fresh();
 for(const viewport of [{width:390,height:844},{width:844,height:390}]){
  await page.setViewportSize(viewport);
  for(const [name,x,y,car] of [['photo',2520,1600,false],['shuttle',1596,1715,false],['boost',1800,3020,true]]){
   await page.evaluate(({x,y,car})=>{const d=window.__SUNLINE__,s=d.sim;if(s.vehicle)s.vehicle.occupied=false;s.vehicle=car?s.cars[0]:null;if(car){s.vehicle.occupied=true;s.vehicle.owned=true;s.vehicle.angle=0;}d.teleport(x,y);window.advanceTime(20);d.updateUI();},{x,y,car});
   const report=await page.evaluate(()=>{const boxes=[...document.querySelectorAll('.hud-tools button,#touch-controls button,#touch-extra button')].map(e=>({id:e.id||e.dataset.key,r:e.getBoundingClientRect().toJSON()})).filter(b=>b.r.width);const overlaps=[];for(let i=0;i<boxes.length;i++)for(let j=i+1;j<boxes.length;j++){const a=boxes[i],b=boxes[j];if(a.r.left<b.r.right&&a.r.right>b.r.left&&a.r.top<b.r.bottom&&a.r.bottom>b.r.top)overlaps.push([a.id,b.id]);}return{overlaps,outside:boxes.filter(({r})=>r.left<0||r.top<0||r.right>innerWidth||r.bottom>innerHeight)};});
   assert.deepEqual(report.outside,[]);assert.deepEqual(report.overlaps,[]);await shot(`04-${name}-${viewport.width}`);
  }
 }
});
await fs.writeFile(new URL('results.json',out),JSON.stringify({results,errors},null,2));await browser.close();console.log(JSON.stringify({passed:results.filter(r=>r.pass).length,total:results.length,errors}));if(errors.length||results.some(r=>!r.pass))process.exitCode=1;
