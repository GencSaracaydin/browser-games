import {chromium} from 'playwright';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const out=new URL('../output/playwright/neighborhoods/',import.meta.url);await fs.mkdir(out,{recursive:true});
const browser=await chromium.launch({headless:true,channel:'chrome'}),errors=[],results=[];
const context=await browser.newContext({viewport:{width:1440,height:900}});await context.addInitScript(()=>{window.requestAnimationFrame=()=>0;window.WebSocket=class{addEventListener(){}send(){}close(){}};});
const page=await context.newPage();page.on('pageerror',e=>errors.push(e.message));
await page.goto('http://127.0.0.1:5174');await page.waitForFunction(()=>window.__SUNLINE__);
const step=(ms=30)=>page.evaluate(ms=>window.advanceTime(ms),ms);
const fresh=async()=>{await page.evaluate(()=>window.__SUNLINE__.reset());await page.click('#start-btn');await step();};
const shot=async name=>{await page.evaluate(()=>{window.__SUNLINE__.updateUI();window.__SUNLINE__.render();});await page.screenshot({path:new URL(name+'.png',out).pathname});};
const test=async(name,fn)=>{try{await fn();results.push({name,pass:true});console.log('PASS',name);}catch(e){results.push({name,pass:false,error:e.stack});console.log('FAIL',name,e.message);await shot('failure-'+results.length);}};
await test('photo keyboard captures actual canvas thumbnail, journal map selects, and album persists',async()=>{
 await fresh();await page.evaluate(()=>{const d=window.__SUNLINE__,p=d.sim.exploration.landmarks[0];d.teleport(p.x,p.y);});await step(1500);
 await page.keyboard.press('c');await step();
 let info=await page.evaluate(()=>{const e=window.__SUNLINE__.sim.exploration;return{photos:[...e.photos],album:e.album,save:JSON.parse(localStorage.getItem('sunline-city-v1')).exploration};});
 assert.ok(info.photos.includes('mercado'));assert.match(info.album.mercado,/^data:image\/jpeg;base64,/);assert.equal(info.save.album.mercado,info.album.mercado);
 await page.keyboard.press('p');assert.equal(await page.locator('.postcard').count(),9);assert.equal(await page.locator('.postcard img').count(),1);await shot('01-journal');
 await page.locator('[data-explore-guide="transit:mirage"]').first().click();assert.equal(await page.evaluate(()=>window.__SUNLINE__.sim.customWaypoint.x),1596);
 await page.reload();await page.waitForFunction(()=>window.__SUNLINE__);await page.click('#start-btn');await step();assert.ok(await page.evaluate(()=>window.__SUNLINE__.sim.exploration.album.mercado));
});
await test('journal transit uses discovered route, charges fare and returns to play',async()=>{
 await fresh();await page.evaluate(()=>{const d=window.__SUNLINE__,e=d.sim.exploration;e.visited.add('havana');e.visited.add('mirage');d.teleport(996,875);d.sim.cash=500;});await step();
 await page.keyboard.press('g');assert.ok(await page.locator('[data-travel="mirage"]').isEnabled());await page.locator('[data-travel="mirage"]').click();await step();
 const result=await page.evaluate(()=>({mode:window.__SUNLINE__.sim.mode,cash:window.__SUNLINE__.sim.cash,pos:window.__SUNLINE__.sim.pos,trips:window.__SUNLINE__.sim.exploration.trips}));assert.equal(result.mode,'play');assert.equal(result.cash,440);assert.equal(result.trips,1);assert.equal(await page.locator('#arrival h2').innerText(),'Mirage District');assert.ok(Math.hypot(result.pos.x-1596,result.pos.y-1715)<60);
});
await test('exploration map filter and lighting controls work',async()=>{
 await page.keyboard.press('m');await page.selectOption('#map-filter','explore');assert.equal(await page.locator('#full-map [data-explore]').count(),23);assert.equal(await page.locator('#full-map [data-activity]').count(),0);await shot('02-exploration-map');await page.selectOption('#map-neighborhood','cypress');assert.match(await page.locator('#full-map svg').getAttribute('viewBox'),/-20 4240 2440 1430/);await shot('02-cypress-closeup');await page.locator('[data-explore="photo:cypress"]').click();assert.equal(await page.evaluate(()=>window.__SUNLINE__.sim.customWaypoint.name),'Cypress Boardwalk');
 await page.keyboard.press('p');await page.selectOption('#light-select','golden');await page.keyboard.press('p');assert.equal(await page.evaluate(()=>window.__SUNLINE__.settings.light),'golden');
});
await test('boost input drains meter, release recharges and on foot Shift remains sprint',async()=>{
 await fresh();await page.evaluate(()=>{const d=window.__SUNLINE__,s=d.sim,c=s.cars[0];s.vehicle=c;c.occupied=true;c.owned=true;c.angle=0;d.teleport(1800,3020);});
 await page.keyboard.down('Shift');await page.keyboard.down('w');await step(1800);let d=await page.evaluate(()=>window.__SUNLINE__.sim.driving.state());assert.ok(d.boosting);assert.ok(d.charge<2.5);assert.equal(await page.locator('#nitro-status').innerText(),'BOOSTING');await shot('03-nitro');
 await page.keyboard.up('Shift');await page.keyboard.up('w');await step(5000);assert.ok((await page.evaluate(()=>window.__SUNLINE__.sim.driving.state())).charge>d.charge);
});
await test('all neighborhood views render distinctive architecture and scenery',async()=>{
 await fresh();for(const [name,x,y]of[['havana',790,885],['mirage',1375,1715],['ocean',2520,1600],['hills',1250,3640],['cypress',1515,4825],['festival',2750,4705],['downtown',2540,3535],['port',4500,4080],['speedway',3520,4670]]){await page.evaluate(({x,y})=>window.__SUNLINE__.teleport(x,y),{x,y});await step(1700);await shot('district-'+name);}
});
await test('mobile photo and journal controls fit, thumbnail capture and nitro button work',async()=>{
 const mobile=await browser.newContext({viewport:{width:390,height:844},isMobile:true,hasTouch:true});await mobile.addInitScript(()=>{window.requestAnimationFrame=()=>0;window.WebSocket=class{addEventListener(){}send(){}close(){}};});const p=await mobile.newPage();p.on('pageerror',e=>errors.push(e.message));await p.goto('http://127.0.0.1:5174');await p.click('#start-btn');await p.evaluate(()=>{const d=window.__SUNLINE__;d.teleport(790,885);window.advanceTime(1500);});await p.locator('#touch-photo').tap();assert.ok(await p.evaluate(()=>window.__SUNLINE__.sim.exploration.photos.has('mercado')));await p.locator('#journal-btn').tap();assert.equal(await p.locator('.postcard').count(),9);await p.screenshot({path:new URL('04-mobile-journal.png',out).pathname});await p.locator('#modal-close').tap();
 for(const viewport of[{width:390,height:844},{width:844,height:390}]){await p.setViewportSize(viewport);await p.evaluate(()=>window.advanceTime(20));const outside=await p.evaluate(()=>[...document.querySelectorAll('.hud-tools button,#touch-controls button,#touch-extra button')].map(e=>e.getBoundingClientRect()).filter(r=>r.width&&(r.left<0||r.right>innerWidth||r.bottom>innerHeight)));assert.deepEqual(outside,[]);await p.screenshot({path:new URL(`05-mobile-${viewport.width}.png`,out).pathname});}
 await p.evaluate(()=>{const d=window.__SUNLINE__,s=d.sim,c=s.cars[0];s.vehicle=c;c.owned=true;c.occupied=true;c.angle=0;d.teleport(1800,3020);d.updateUI();});assert.equal(await p.locator('[data-key="ShiftLeft"]').innerText(),'BOOST');const box=await p.locator('[data-key="ShiftLeft"]').boundingBox(),cdp=await mobile.newCDPSession(p);await cdp.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[{x:box.x+box.width/2,y:box.y+box.height/2}]});await p.evaluate(()=>window.advanceTime(1000));assert.ok(await p.evaluate(()=>window.__SUNLINE__.sim.driving.state().boosting));await cdp.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]});await mobile.close();
});
await fs.writeFile(new URL('results.json',out),JSON.stringify({results,errors},null,2));await browser.close();console.log(JSON.stringify({passed:results.filter(r=>r.pass).length,total:results.length,errors}));if(errors.length||results.some(r=>!r.pass))process.exitCode=1;
