import assert from 'node:assert/strict';
import {createWorld} from '../src/world.js';
import {Simulation} from '../src/simulation.js';
const world=createWorld();
let passed=0;
function test(name,fn){fn();passed++;console.log(`PASS ${name}`);}
const postcards=[['havana',790,885],['mirage',1375,1715],['ocean',2520,1600],['hills',1250,3640],['cypress',1515,4825],['festival',2750,4705],['downtown',2540,3535],['port',4840,4140],['speedway',3440,4550]];
const food=[['havana',865,885],['mirage',1520,1715],['ocean',2540,1640],['festival',2830,4705]];
const stops=[['ocean',2404,1500],['havana',996,875],['mirage',1596,1715],['grove',2236,1085],['hills',1596,3600],['downtown',3100,3650],['port',4104,3710],['cypress',1764,4860],['festival',2404,4850],['speedway',4656,4500]];

test('Every neighborhood has a stable identity and complete atmosphere metadata',()=>{
  const ids=['ocean','havana','mirage','grove','hills','downtown','port','cypress','festival','speedway'];
  assert.deepEqual(world.neighborhoods.map(n=>n.id),ids);
  assert.equal(new Set(world.neighborhoods.map(n=>n.color)).size,10);
  for(const n of world.neighborhoods){for(const field of['name','tagline','description','color','ambience'])assert(n[field]?.length>2,`${n.id}.${field}`);assert(n.w>0&&n.h>0);}
  assert.equal(world.neighborhoodAt(-100,-100),null);
});
test('Postcard landmarks resolve to the intended place and remain walkable',()=>{
  for(const[id,x,y]of postcards){assert.equal(world.neighborhoodAt(x,y)?.id,id);assert(!world.isSolid(x,y,20),`${id} postcard blocked`);}
});
test('All food carts and transit stops retain open interaction space',()=>{
  for(const[id,x,y]of[...food,...stops]){assert.equal(world.neighborhoodAt(x,y)?.id,id);assert(!world.isSolid(x,y,20),`${id} interaction point blocked`);}
});
test('Scenery changes preserve every story and activity destination for vehicles',()=>{
  assert.equal(world.pois.length,18);
  for(const poi of world.pois)assert(!world.isSolid(poi.x,poi.y,35),`${poi.key} lost vehicle clearance`);
});
test('Terrain classifications match visible streets, courts, beach and hills',()=>{
  for(const[x,y,type]of[[2320,3020,'road'],[4470,4900,'road'],[1950,950,'paved'],[1800,580,'grass'],[2550,1410,'sand'],[1250,3640,'dirt'],[750,4900,'grass'],[2830,4705,'paved'],[3770,3500,'paved']])assert.equal(world.terrainAt(x,y),type,`${x},${y}`);
});
test('Natural lake collision follows the shore and leaves its boardwalk accessible',()=>{
  const lake=world.pools.find(p=>p.kind==='lake');assert(lake.outline.length>=32);
  assert(world.isSolid(lake.x+lake.w/2,lake.y+lake.h/2,8));
  assert(!world.isSolid(1515,4825,20));
  const cx=lake.x+lake.w/2,cy=lake.y+lake.h/2;
  for(const[x,y]of lake.outline){assert(world.isSolid(cx+(x-cx)*.9,cy+(y-cy)*.9,3));assert(!world.isSolid(cx+(x-cx)*1.15,cy+(y-cy)*1.15,3));}
});
test('Distinct architectural skins stay within culled, visible scenery',()=>{
  for(const id of['havana','mirage','downtown','port'])assert(world.buildings.filter(b=>b.neighborhood===id&&b.neighborhoodDraw).length>=3,id);
  const total=world.getDrawables().length;
  for(const[,x,y]of postcards)assert(world.getDrawables({x:x-450,y:y-300,w:900,h:600}).length<total/3);
});
test('Short county traffic run is unaffected by decorative landmarks',()=>{
  const sim=new Simulation(world);const cars=sim.cars.filter(c=>c.route?.some(p=>p.y>2800));
  for(let i=0;i<1800;i++)sim.updateTraffic(1/30);
  for(const car of cars){assert(!car.parked,`${car.id} became stuck`);assert(!world.isSolid(car.x,car.y,14),`${car.id} entered a building`);}
});
console.log(`${passed}/${passed} neighborhood world checks passed.`);
