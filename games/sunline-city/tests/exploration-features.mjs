import assert from 'node:assert/strict';
import {createWorld} from '../src/world.js';
import {ExplorationSystem,LANDMARKS,FOOD_STANDS,TRANSIT_STOPS,POSTCARD_REWARD,COLLECTION_REWARD,DISCOVERY_REWARD,TRANSIT_FARE} from '../src/exploration.js';

const results=[];
function test(name,fn){try{const detail=fn();results.push({name,pass:true,detail});console.log('PASS',name);}catch(error){results.push({name,pass:false,error:error.stack});console.log('FAIL',name,error.message);}}
function setup(save=null,world=createWorld()){
  const sim={world,mode:'play',cash:250,health:100,wanted:0,player:{x:2160,y:1085,angle:0},vehicle:null,cars:[],police:[],activities:{active:null},job:{index:0,phase:'offer'},events:[],customWaypoint:null,
    get pos(){return this.vehicle||this.player;},emit(type,data={}){this.events.push({type,...data});},toast(message){this.emit('toast',{message});}};
  const exploration=new ExplorationSystem(sim,save);sim.exploration=exploration;
  const at=p=>{sim.player.x=p.x;sim.player.y=p.y;};
  return{sim,exploration,at};
}
const sampleJPEG='data:image/jpeg;base64,/9j/2Q==';

test('all nine camera points, four food stands and ten shuttle stops are walkable',()=>{
  const world=createWorld();for(const p of [...LANDMARKS,...FOOD_STANDS,...TRANSIT_STOPS])assert.equal(world.isSolid(p.x,p.y,20),false,`${p.id} blocked at ${p.x},${p.y}`);
  assert.equal(LANDMARKS.length,9);assert.equal(FOOD_STANDS.length,4);assert.equal(TRANSIT_STOPS.length,10);
});
test('neighborhood discovery waits for entry, pays once, and freezes while paused',()=>{
  const f=setup();f.exploration.update(.6);assert.equal(f.sim.cash,250);f.sim.mode='paused';f.exploration.update(5);assert.equal(f.sim.cash,250);f.sim.mode='play';f.exploration.update(.61);assert.equal(f.sim.cash,250+DISCOVERY_REWARD);f.exploration.update(9);assert.equal(f.sim.cash,290);assert.equal(f.exploration.visited.size,1);assert.equal(f.sim.events.filter(e=>e.type==='neighborhoodDiscovered').length,1);
});
test('a short border crossing does not repeatedly pay discovery',()=>{
  const f=setup();f.at(TRANSIT_STOPS.find(p=>p.id==='havana'));f.exploration.update(.8);f.at(TRANSIT_STOPS.find(p=>p.id==='ocean'));f.exploration.update(.8);assert.equal(f.exploration.visited.size,0);f.exploration.update(.5);assert.equal(f.exploration.visited.size,1);f.at(TRANSIT_STOPS.find(p=>p.id==='havana'));f.exploration.update(1.3);assert.equal(f.exploration.visited.size,2);f.at(TRANSIT_STOPS.find(p=>p.id==='ocean'));f.exploration.update(2);assert.equal(f.sim.cash,250+DISCOVERY_REWARD*2);
});
test('photo requires the actual landmark, on foot, no wanted level, and play mode',()=>{
  const f=setup(),p=LANDMARKS[0];assert.equal(f.exploration.photo(p.id),false);f.at(p);f.sim.vehicle={...p};assert.equal(f.exploration.photo(p.id),false);f.sim.vehicle=null;f.sim.wanted=1;assert.equal(f.exploration.photo(p.id),false);f.sim.wanted=0;f.sim.mode='paused';assert.equal(f.exploration.photo(p.id),false);assert.equal(f.sim.cash,250);f.sim.mode='play';assert.equal(f.exploration.snapshot(p.id),true);assert.equal(f.sim.cash,250+POSTCARD_REWARD+DISCOVERY_REWARD);assert.equal(f.exploration.photos.size,1);
});
test('first postcard and full collection pay exactly once across save reload',()=>{
  const f=setup();for(const p of LANDMARKS){f.at(p);assert.equal(f.exploration.photo(p.id),true);const cash=f.sim.cash;assert.equal(f.exploration.photo(p.id),false);assert.equal(f.sim.cash,cash);}
  const uniqueRegions=new Set(LANDMARKS.map(p=>p.neighborhoodId)).size;
  assert.equal(f.sim.cash,250+LANDMARKS.length*POSTCARD_REWARD+COLLECTION_REWARD+uniqueRegions*DISCOVERY_REWARD);
  assert.equal(f.sim.events.filter(e=>e.type==='postcard'&&e.collectionBonus===500).length,1);
  const loaded=setup(f.exploration.serialize());loaded.at(LANDMARKS[0]);assert.equal(loaded.exploration.photo(),false);assert.equal(loaded.sim.cash,250);assert.equal(loaded.exploration.collectionPaid,true);
});
test('album accepts bounded JPEGs only for earned postcards and text state omits pixels',()=>{
  const f=setup();assert.equal(f.exploration.attachPhoto(LANDMARKS[0].id,sampleJPEG),false);f.at(LANDMARKS[0]);f.exploration.photo();assert.equal(f.exploration.attachPhoto(LANDMARKS[0].id,sampleJPEG),true);assert.equal(f.exploration.attachPhoto(LANDMARKS[0].id,'data:image/png;base64,AAA'),false);assert.equal(f.exploration.attachPhoto(LANDMARKS[0].id,'data:image/jpeg;base64,'+'A'.repeat(120001)),false);assert.equal(f.exploration.serialize().album.mercado,sampleJPEG);assert.equal(JSON.stringify(f.exploration.state()).includes('base64'),false);
});
test('legacy and malformed saves sanitize records without rewarding or crashing',()=>{
  const f=setup({visited:['havana','havana','unknown'],photos:['mercado','mercado','unknown'],album:{mercado:'https://bad.example/picture',unknown:sampleJPEG},meals:-1,trips:Infinity});assert.deepEqual([...f.exploration.visited],['havana']);assert.deepEqual([...f.exploration.photos],['mercado']);assert.deepEqual(f.exploration.album,{});assert.equal(f.exploration.meals,0);assert.equal(f.exploration.trips,0);assert.equal(f.sim.cash,250);assert.equal(setup(null).exploration.photos.size,0);assert.equal(setup({photos:'mercado',visited:{}}).exploration.visited.size,0);
});
test('food heals, charges menu price, caps health and blocks full-health waste',()=>{
  const f=setup(),food=FOOD_STANDS[0];f.at(food);assert.equal(f.exploration.buyFood(),false);assert.equal(f.sim.cash,250);f.sim.health=85;assert.equal(f.exploration.interact(),true);assert.equal(f.sim.health,100);assert.equal(f.sim.cash,250-food.price);assert.equal(f.exploration.meals,1);assert.equal(f.exploration.buyFood(),false);assert.equal(f.sim.cash,220);
});
test('food enforces affordability, location, on-foot, heat and a meal cooldown',()=>{
  const f=setup(),food=FOOD_STANDS[1];f.sim.health=10;assert.equal(f.exploration.buyFood(food.id),false);f.at(food);f.sim.cash=0;assert.equal(f.exploration.buyFood(food.id),false);f.sim.cash=250;f.sim.wanted=1;assert.equal(f.exploration.buyFood(food.id),false);f.sim.wanted=0;f.sim.vehicle={...food};assert.equal(f.exploration.buyFood(food.id),false);f.sim.vehicle=null;assert.equal(f.exploration.buyFood(food.id),true);assert.equal(f.exploration.buyFood(food.id),false);assert.equal(f.sim.health,50);f.exploration.update(8.1);assert.equal(f.exploration.buyFood(food.id),true);assert.equal(f.sim.health,90);
});
test('shuttle requires nearby origin and a previously visited destination',()=>{
  const f=setup(),origin=TRANSIT_STOPS[1],dest=TRANSIT_STOPS[0];f.at({x:2000,y:1300});f.exploration.visited.add(dest.neighborhoodId);assert.equal(f.exploration.travel(dest.id),false);f.at(origin);f.exploration.visited.delete(dest.neighborhoodId);assert.equal(f.exploration.travel(dest.id),false);f.exploration.visited.add(dest.neighborhoodId);assert.equal(f.exploration.travel(dest.id),true);assert.equal(f.sim.cash,250-TRANSIT_FARE);assert.equal(f.exploration.trips,1);assert.ok(Math.hypot(f.sim.player.x-dest.x,f.sim.player.y-dest.y)<=58);assert.equal(f.sim.events.filter(e=>e.type==='travel').length,1);assert.equal(f.exploration.travel(dest.id),false);assert.equal(f.sim.cash,190);
});
test('shuttle cannot bypass wanted level, story, adventure or vehicle restrictions',()=>{
  const f=setup(),origin=TRANSIT_STOPS[1],dest=TRANSIT_STOPS[0];f.at(origin);f.exploration.visited.add(dest.neighborhoodId);
  for(const [setup,clear] of [[()=>f.sim.wanted=1,()=>f.sim.wanted=0],[()=>f.sim.job.phase='deliver',()=>f.sim.job.phase='offer'],[()=>f.sim.activities.active={id:'heist'},()=>f.sim.activities.active=null],[()=>f.sim.vehicle={...origin},()=>f.sim.vehicle=null]]){setup();assert.equal(f.exploration.travel(dest.id),false);assert.equal(f.sim.cash,250);assert.equal(f.sim.player.x,origin.x);clear();}
});
test('journal can preview travel while paused but only an explicit boarding action moves',()=>{
  const f=setup(),origin=TRANSIT_STOPS[1],dest=TRANSIT_STOPS[0];f.at(origin);f.exploration.visited.add(dest.neighborhoodId);f.sim.mode='paused';assert.equal(f.exploration.travelStatus(dest.id).canTravel,true);assert.equal(f.exploration.travel(dest.id),false);f.sim.mode='play';f.sim.cash=59;assert.equal(f.exploration.travel(dest.id),false);assert.equal(f.sim.player.x,origin.x);
});
test('blocked shuttle arrival is atomic and does not charge the player',()=>{
  const f=setup(),origin=TRANSIT_STOPS[1],dest=TRANSIT_STOPS[0];f.at(origin);f.exploration.visited.add(dest.neighborhoodId);f.sim.world={...f.sim.world,isSolid:()=>true};assert.equal(f.exploration.travel(dest.id),false);assert.equal(f.sim.cash,250);assert.equal(f.sim.player.x,origin.x);assert.equal(f.exploration.trips,0);
});
test('photo and transit IDs select their own actual destinations without teleporting',()=>{
  const f=setup();f.exploration.select('photo:ocean');assert.equal(f.sim.customWaypoint.x,2520);f.exploration.select('transit:ocean');assert.equal(f.sim.customWaypoint.x,2404);f.exploration.select('rosa','food');assert.equal(f.sim.customWaypoint.x,865);assert.equal(f.sim.player.x,2160);assert.equal(f.sim.cash,250);assert.equal(f.exploration.select('missing'),false);
});

if(process.argv.includes('--browser')){
  const {chromium}=await import('playwright');const fs=await import('node:fs/promises');
  const browser=await chromium.launch({headless:true}),errors=[];
  const ctx=await browser.newContext({viewport:{width:1440,height:960}});
  await ctx.addInitScript(()=>{window.requestAnimationFrame=()=>0;window.WebSocket=class{addEventListener(){}send(){}close(){}};});
  const page=await ctx.newPage();page.on('pageerror',e=>errors.push(e.message));page.on('console',m=>{if(m.type()==='error')errors.push(m.text());});
  const out=new URL('../output/playwright/exploration/',import.meta.url);await fs.mkdir(out,{recursive:true});
  await page.goto('http://127.0.0.1:5174');await page.waitForFunction(()=>window.__SUNLINE__?.sim.exploration);
  const step=(ms=20,codes=[])=>page.evaluate(({ms,codes})=>{for(const code of codes)dispatchEvent(new KeyboardEvent('keydown',{code,bubbles:true}));window.advanceTime(ms);for(const code of codes)dispatchEvent(new KeyboardEvent('keyup',{code,bubbles:true}));return JSON.parse(window.render_game_to_text());},{ms,codes});
  const state=()=>page.evaluate(()=>JSON.parse(window.render_game_to_text()));
  const tap=async key=>{await page.keyboard.press(key);return step();};
  const fresh=async()=>{await page.evaluate(()=>window.__SUNLINE__.reset());await page.locator('#start-btn').click();await step(1400);};
  const at=async p=>{await page.evaluate(p=>{window.__SUNLINE__.teleport(p.x,p.y);window.advanceTime(1400);},p);};
  const shot=async name=>{await page.evaluate(()=>{window.__SUNLINE__.updateUI();window.__SUNLINE__.render();});await page.screenshot({path:new URL(`${name}.png`,out).pathname});await fs.writeFile(new URL(`${name}.json`,out),JSON.stringify(await state(),null,2));};
  const check=async(name,fn)=>{try{const detail=await fn();results.push({name,pass:true,detail});console.log('PASS',name);}catch(error){results.push({name,pass:false,error:error.stack});console.log('FAIL',name,error.message);await shot(`failure-${results.length}`).catch(()=>{});}};
  await check('browser: C earns real postcard, P shows actual saved photograph',async()=>{
    await fresh();await at(LANDMARKS[0]);const before=(await state()).cash;let s=await tap('c');assert.ok(s.exploration.photos.includes('mercado'));assert.equal(s.cash,before+150);const image=await page.evaluate(()=>window.__SUNLINE__.sim.exploration.album.mercado);assert.match(image,/^data:image\/jpeg;base64,/);await fs.writeFile(new URL('mercado-postcard.jpg',out),Buffer.from(image.split(',')[1],'base64'));await tap('c');assert.equal((await state()).cash,before+150);await tap('p');assert.equal((await state()).mode,'paused');assert.equal(await page.locator('.neighborhood-card').count(),10);assert.equal(await page.locator('.postcard').count(),9);await page.locator('.postcard img').scrollIntoViewIfNeeded();await page.waitForFunction(()=>document.querySelector('.postcard img')?.naturalWidth===600);await shot('01-journal-photograph');await tap('p');assert.equal((await state()).mode,'play');return{thumbnailCharacters:image.length};
  });
  await check('browser: walk from camera to nearby café, E buys food without taking photo',async()=>{
    await at(LANDMARKS[0]);await page.evaluate(()=>window.__SUNLINE__.sim.health=65);const before=(await state()).cash;await step(640,['KeyD']);let s=await state();assert.ok(Math.abs(s.player.x-FOOD_STANDS[0].x)<15);assert.match(s.context,/Café con leche/);s=await tap('e');assert.equal(s.player.health,100);assert.equal(s.cash,before-30);assert.equal(s.exploration.photos.length,1);await shot('02-market-coffee');
  });
  await check('browser: thumbnail, discoveries and meal count survive reload',async()=>{
    const previous=await page.evaluate(()=>window.__SUNLINE__.sim.exploration.serialize());await page.reload();await page.waitForFunction(()=>window.__SUNLINE__);await page.locator('#start-btn').click();await step(20);const loaded=await page.evaluate(()=>window.__SUNLINE__.sim.exploration.serialize());assert.deepEqual(loaded.photos,previous.photos);assert.equal(loaded.album.mercado,previous.album.mercado);assert.equal(loaded.meals,1);assert.deepEqual(loaded.visited,previous.visited);
  });
  await check('browser: G opens local shuttle, journal ride charges fare and arrives',async()=>{
    const ocean=TRANSIT_STOPS.find(p=>p.id==='ocean'),havana=TRANSIT_STOPS.find(p=>p.id==='havana');await at(ocean);await at(havana);const before=(await state()).cash;await tap('g');assert.equal((await state()).mode,'paused');const ride=page.locator('[data-travel="ocean"]');assert.equal(await ride.isEnabled(),true);await ride.click();const s=await state();assert.equal(s.mode,'play');assert.equal(s.cash,before-60);assert.ok(Math.hypot(s.player.x-ocean.x,s.player.y-ocean.y)<60);assert.equal(s.exploration.trips,1);await shot('03-shuttle-arrival');
  });
  await check('browser: active story disables every shuttle ride without charging',async()=>{
    await fresh();await tap('e');assert.notEqual((await state()).mission.phase,'offer');await at(TRANSIT_STOPS.find(p=>p.id==='havana'));await tap('g');assert.equal(await page.locator('[data-travel]:not([disabled])').count(),0);await tap('p');
  });
  await check('browser: all nine real captures earn collection bonus once and stay bounded',async()=>{
    await fresh();let lastBefore=0;for(const landmark of LANDMARKS){await at(landmark);lastBefore=(await state()).cash;await tap('c');}
    let s=await state();assert.equal(s.exploration.photos.length,9);assert.equal(s.cash,lastBefore+650);const before=s.cash;await tap('c');assert.equal((await state()).cash,before);const album=await page.evaluate(()=>window.__SUNLINE__.sim.exploration.album);assert.equal(Object.keys(album).length,9);const size=JSON.stringify(album).length;assert.ok(size<1080000);await tap('p');await page.locator('.postcard img').first().scrollIntoViewIfNeeded();await shot('04-complete-postcard-album');await tap('p');return{albumCharacters:size};
  });
  await check('browser: prior version-one save migrates without losing existing progress',async()=>{
    await page.addInitScript(()=>localStorage.setItem('sunline-city-v1',JSON.stringify({version:1,cash:1234,completed:3,collected:[1,4],stats:{distance:1200,jobs:3,escapes:2},player:{x:2160,y:1085}})));await page.reload();await page.waitForFunction(()=>window.__SUNLINE__);let s=await state();assert.equal(s.cash,1234);assert.equal(s.completed,3);assert.equal(s.exploration.photos.length,0);assert.equal(s.exploration.visited.length,0);await page.locator('#start-btn').click();await step(1400);s=await state();assert.equal(s.cash,1274);assert.equal(s.completed,3);assert.equal(s.tapes.collected,2);
  });
  results.push({name:'browser: no console or page errors',pass:errors.length===0,detail:errors});
  await fs.writeFile(new URL('results.json',out),JSON.stringify({results,errors},null,2));await browser.close();
}
console.log(JSON.stringify({passed:results.filter(r=>r.pass).length,total:results.length},null,2));
if(results.some(r=>!r.pass))process.exitCode=1;
