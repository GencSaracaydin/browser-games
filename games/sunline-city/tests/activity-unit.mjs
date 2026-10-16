import assert from 'node:assert/strict';
import { ActivitySystem, ACTIVITY_CATALOG } from '../src/activities.js';
import { CombatSystem } from '../src/combat.js';

function fixture(saved) {
  const sim={mode:'play',time:0,player:{x:0,y:0},vehicle:null,cars:[],police:[],world:{pois:[],isSolid:()=>false},cash:250,health:100,wanted:0,events:[],job:{index:0,phase:'offer'},customWaypoint:null,
    get pos(){return this.vehicle||this.player;},emit(type,data={}){this.events.push({type,...data});},toast(message){this.emit('toast',{message});},addHeat(n){this.wanted=Math.min(3,this.wanted+n);}};
  sim.combat={enemies:[],spawnEncounter(o){this.clearEncounter(o.id);for(let i=0;i<(o.enemies?.length||o.count);i++)this.enemies.push({id:`${o.id}-${i}`,encounterId:o.id,x:o.center.x,y:o.center.y,health:50,...o.enemies?.[i]});return o.id;},remaining(id){return this.enemies.filter(e=>e.encounterId===id&&e.health>0).length;},clearEncounter(id){this.enemies=this.enemies.filter(e=>e.encounterId!==id);},resupply(){}};
  const activities=new ActivitySystem(sim,saved);
  const at=q=>{const p=sim.pos;p.x=q.x;p.y=q.y;if(sim.vehicle)sim.vehicle.speed=0;};
  const start=id=>{at(activities.startPoint(activities.definition(id)));return activities.start(id);};
  const car=()=>{const c={id:`test-${sim.cars.length}`,x:sim.player.x,y:sim.player.y,health:100,speed:0,type:'sport'};sim.cars.push(c);sim.vehicle=c;return c;};
  const enter=()=>{sim.vehicle=activities.car();sim.vehicle.speed=0;};
  const exit=()=>{sim.player.x=sim.pos.x;sim.player.y=sim.pos.y;sim.vehicle=null;};
  const clear=()=>sim.combat.enemies.forEach(e=>e.health=0);
  return {sim,activities,at,start,car,enter,exit,clear};
}
let passed=0;
function test(name,fn){fn();passed++;process.stdout.write(`PASS ${name}\n`);}

test('remote selection navigates without beginning or teleporting',()=>{
  const {sim,activities}=fixture();assert.equal(activities.start('taxi'),false);assert.equal(activities.active,null);assert.equal(activities.selected,'taxi');assert.equal(sim.player.x,0);assert.ok(sim.customWaypoint);
});
test('story, heat, and one active adventure are enforced',()=>{
  const f=fixture();f.sim.job.phase='deliver';assert.equal(f.start('taxi'),false);f.sim.job.phase='offer';f.sim.wanted=1;assert.equal(f.start('taxi'),false);f.sim.wanted=0;assert.equal(f.start('taxi'),true);assert.equal(f.start('bounty'),false);assert.equal(f.activities.active.id,'taxi');
});
test('all three races award a medal, remember records, and pay only once per finish',()=>{
  for(const def of ACTIVITY_CATALOG.filter(d=>d.type==='race')){
    const f=fixture();f.car();assert.equal(f.start(def.id),true);f.activities.update(3.01);for(const [x,y] of def.route){f.at({x,y});f.activities.update(1);}
    assert.equal(f.activities.active,null);assert.equal(f.activities.records[def.id].medal,'gold');assert.equal(f.activities.records[def.id].completions,1);const cash=f.sim.cash;
    f.activities.update(1);f.activities.interact();assert.equal(f.sim.cash,cash);
    const saved=new ActivitySystem(f.sim,f.activities.serialize());assert.equal(saved.records[def.id].bestTime,def.route.length);
  }
});
test('race replay pays base reward without duplicating medal reward',()=>{
  const f=fixture();f.car();const def=f.activities.definition('race-airport');
  for(let run=0;run<2;run++){f.activities.update(10);f.start(def.id);f.activities.update(3.1);for(const [x,y]of def.route){f.at({x,y});f.activities.update(2);}if(!run)f.firstCash=f.sim.cash;}
  assert.equal(f.sim.cash-f.firstCash,def.reward);assert.equal(f.activities.records[def.id].completions,2);
});
test('taxi requires cab, permits getting back in, completes exactly three fares',()=>{
  const f=fixture();f.start('taxi');assert.equal(f.activities.active.phase,'vehicle');f.enter();f.activities.update(.1);
  f.at(f.activities.waypoint());f.exit();assert.equal(f.activities.interact(),false);f.enter();
  for(let i=0;i<3;i++){f.at(f.activities.waypoint());assert.equal(f.activities.interact(),true);assert.equal(f.activities.active.phase,'dropoff');f.activities.update(10);f.at(f.activities.waypoint());f.activities.interact();}
  assert.equal(f.activities.active,null);assert.ok(f.sim.cash>1000);assert.equal(f.activities.records.taxi.completions,1);
});
test('taxi timeout cancels its unpaid shift and cleans up an abandoned cab',()=>{
  const f=fixture();f.start('taxi');f.enter();f.activities.update(.1);f.at(f.activities.waypoint());f.activities.interact();f.exit();f.activities.update(1000);assert.equal(f.activities.active,null);assert.equal(f.sim.cash,250);assert.equal(f.sim.cars.length,0);
});
test('export requires marked car, clean wanted level, and pays condition',()=>{
  const f=fixture();f.start('export');const target=f.activities.car();f.enter();f.activities.update(.1);assert.equal(f.sim.wanted,2);f.at(f.activities.waypoint());f.activities.interact();assert.ok(f.activities.active);f.sim.wanted=0;target.health=50;f.activities.interact();assert.equal(f.sim.cash,250+775);assert.equal(f.sim.vehicle,null);assert.equal(f.sim.cars.length,0);assert.equal(f.activities.records.export.completions,1);
});
test('survival advances three waves and cleans up all enemies',()=>{
  const f=fixture();f.start('survival');for(let i=1;i<=3;i++){f.activities.update(4.01);assert.equal(f.activities.active.wave,i);assert.equal(f.sim.combat.remaining(f.activities.active.encounter),i+2);f.clear();f.activities.update(.1);}assert.equal(f.activities.active,null);assert.equal(f.sim.cash,1750);assert.equal(f.sim.combat.enemies.length,0);
});
test('heist needs marked van, guards, on-foot case, escape, and handoff',()=>{
  const f=fixture();f.start('heist');f.enter();f.activities.update(.1);assert.equal(f.activities.active.phase,'approach');f.at(f.activities.active.center);f.activities.update(.1);assert.equal(f.activities.active.phase,'guards');f.clear();f.activities.update(.1);assert.equal(f.activities.active.phase,'case');f.at(f.activities.waypoint());assert.equal(f.activities.interact(),false);f.exit();f.activities.interact();assert.equal(f.activities.active.phase,'escape');assert.equal(f.sim.wanted,2);f.sim.wanted=0;f.activities.update(.1);assert.equal(f.activities.active.phase,'deliver');assert.equal(f.activities.interact(),false);f.enter();f.at(f.activities.waypoint());f.activities.interact();assert.equal(f.sim.cash,2850);assert.equal(f.activities.records.heist.completions,1);
});
test('bounty cannot be collected before the encounter is cleared',()=>{
  const f=fixture();f.start('bounty');f.at(f.activities.waypoint());f.activities.update(.1);assert.equal(f.activities.active.phase,'fight');assert.equal(f.activities.interact(),false);f.clear();f.activities.update(.1);assert.equal(f.activities.active.phase,'capture');f.at(f.activities.waypoint());f.activities.interact();assert.equal(f.activities.active.phase,'return');f.at(f.activities.waypoint());f.activities.interact();assert.equal(f.sim.cash,1450);assert.equal(f.sim.combat.enemies.length,0);
});
test('cancellation clears encounters without granting money or deleting occupied cars',()=>{
  const f=fixture();f.start('heist');f.enter();f.activities.update(.1);f.at(f.activities.active.center);f.activities.update(.1);const car=f.sim.vehicle;f.activities.cancel();assert.equal(f.sim.combat.enemies.length,0);assert.equal(f.sim.cash,250);assert.ok(f.sim.cars.includes(car));assert.equal(f.sim.vehicle,car);
});
test('malformed record fields are sanitized and unknown records ignored',()=>{
  const f=fixture({records:{taxi:{completions:-2,earnings:Infinity,bestTime:-99,medal:'diamond'},evil:{completions:999}}});assert.equal(f.activities.records.taxi.completions,0);assert.equal(f.activities.records.taxi.bestTime,null);assert.equal(f.activities.records.taxi.medal,'none');assert.equal(f.activities.records.taxi.earnings,0);assert.equal(f.activities.records.evil,undefined);
});
test('shooting range counts real projectile hits and pays only after all six targets',()=>{
  const f=fixture();f.sim.player.angle=0;f.sim.people=[];f.sim.lastCrime=-100;f.sim.combat=new CombatSystem(f.sim);
  assert.equal(f.start('range'),true);assert.equal(f.sim.combat.enemies.length,6);
  for(const target of [...f.sim.combat.enemies]){
    const aim={x:target.x,y:target.y-11};
    for(let frame=0;frame<60 && target.health>0;frame++){
      f.sim.time+=1/60;f.sim.combat.update(1/60,{fire:true},aim);f.activities.update(1/60);
    }
    assert.equal(target.health,0);
  }
  assert.equal(f.activities.active,null);assert.equal(f.sim.cash,500);assert.equal(f.sim.wanted,0);assert.equal(f.activities.records.range.medal,'gold');assert.equal(f.sim.combat.enemies.length,0);
});
test('shooting range timeout rewards actual hits without pretending full completion',()=>{
  const f=fixture();f.start('range');f.sim.combat.enemies[0].health=0;f.activities.update(45.1);assert.equal(f.activities.active,null);assert.equal(f.sim.cash,300);assert.equal(f.activities.records.range.medal,'none');assert.equal(f.activities.records.range.bestTime,null);
});
process.stdout.write(`${passed} activity scenarios passed.\n`);
