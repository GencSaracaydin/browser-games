/** Weapon, projectile and encounter simulation. Distances use the city's world pixels. */
export const WEAPONS = Object.freeze([
  {id:'pistol',name:'Service Pistol',shortName:'PISTOL',magazine:12,reserve:60,maxReserve:120,price:0,refillPrice:45,damage:28,pellets:1,spread:.018,range:570,speed:760,interval:.29,reloadTime:1.05,color:'#ffe7a1',description:'Precise, dependable, and easy to control.'},
  {id:'smg',name:'Coastline SMG',shortName:'SMG',magazine:30,reserve:120,maxReserve:240,price:900,refillPrice:80,damage:13,pellets:1,spread:.075,range:410,speed:800,interval:.085,reloadTime:1.65,color:'#f7bf73',description:'Rapid fire. Keep bursts short to stay accurate.'},
  {id:'shotgun',name:'Boardwalk Pump',shortName:'SHOTGUN',magazine:6,reserve:30,maxReserve:60,price:1200,refillPrice:70,damage:11,pellets:7,spread:.19,range:270,speed:700,interval:.78,reloadTime:1.95,color:'#ffa980',description:'Seven pellets. Big impact at close range.'},
  {id:'carbine',name:'Ranger Carbine',shortName:'CARBINE',magazine:20,reserve:80,maxReserve:160,price:1800,refillPrice:100,damage:36,pellets:1,spread:.012,range:790,speed:980,interval:.22,reloadTime:1.6,color:'#bcebe0',description:'Long reach and a steady, deliberate rhythm.'},
].map(w=>Object.freeze(w)));
const clamp=(v,a,b)=>Math.max(a,Math.min(b,v));
const finite=(v,fallback)=>Number.isFinite(v)?v:fallback;
const distance=(a,b)=>Math.hypot(a.x-b.x,a.y-b.y);
const seed=n=>{const v=Math.sin(n*127.1+311.7)*43758.5453;return v-Math.floor(v)};
const alive=e=>e.health>0&&!e.defeated;

// Return the first contact along a segment, including a segment starting inside.
export function segmentCircle(ax,ay,bx,by,cx,cy,radius){
 const dx=bx-ax,dy=by-ay,fx=ax-cx,fy=ay-cy,c=fx*fx+fy*fy-radius*radius;
 if(c<=0)return 0;const a=dx*dx+dy*dy;if(a<1e-8)return null;
 const b=2*(fx*dx+fy*dy),discriminant=b*b-4*a*c;if(discriminant<0)return null;
 const t=(-b-Math.sqrt(discriminant))/(2*a);return t>=0&&t<=1?t:null;
}

export class CombatSystem {
 constructor(sim,save=null){
  this.sim=sim;this.inventory=Object.create(null);
  for(const w of WEAPONS){const saved=save?.inventory?.[w.id];this.inventory[w.id]={owned:w.id==='pistol'||saved?.owned===true,ammo:clamp(Math.floor(finite(saved?.ammo,w.magazine)),0,w.magazine),reserve:clamp(Math.floor(finite(saved?.reserve,w.reserve)),0,w.maxReserve)};}
  this.selected=WEAPONS.some(w=>w.id===save?.selected&&this.inventory[w.id].owned)?save.selected:'pistol';
  this.cooldown=0;this.reloadRemaining=0;this.reloadDuration=0;this.recoil=0;this.hitMarker=0;this.hitKill=false;this.damageFlash=0;this.muzzle=0;this.lastAim=null;this.lastShot=-100;this.dryCooldown=0;this.dodgeTime=0;this.dodgeCooldown=0;this.dodgeAngle=0;this.qHeld=false;this.shotSequence=0;this.enemySequence=0;
  this.bullets=[];this.effects=[];this.enemies=[];this.encounters=new Map();this.safeGrace=[];
  this.stats={shots:Math.max(0,Math.floor(finite(save?.stats?.shots,0))),hits:Math.max(0,Math.floor(finite(save?.stats?.hits,0))),defeated:Math.max(0,Math.floor(finite(save?.stats?.defeated,0)))};
  this.sync();
 }
 get weapon(){return WEAPONS.find(w=>w.id===this.selected)}
 get ammo(){return this.inventory[this.selected].ammo}
 get reserve(){return this.inventory[this.selected].reserve}
 get enemiesRemaining(){return this.enemies.filter(alive).length}
 get reloadProgress(){return this.reloadRemaining?1-this.reloadRemaining/this.reloadDuration:0}
 get armed(){return !this.sim.vehicle&&(this.sim.time-this.lastShot<1.6||this.reloadRemaining>0||!!this.lastAim)}
 sync(){this.sim.ammo=this.ammo;this.sim.reload=this.reloadRemaining;this.sim.shootCooldown=this.cooldown;}
 emit(type,data={}){this.sim.emit?.(type,data)}
 sound(name){this.emit('sfx',{name})}
 toast(message){this.sim.toast?.(message)}
 serialize(){return{version:1,selected:this.selected,inventory:Object.fromEntries(WEAPONS.map(w=>[w.id,{...this.inventory[w.id]}])),stats:{...this.stats}};}
 state(){return{selected:this.selected,name:this.weapon.name,ammo:this.ammo,reserve:this.reserve,magazine:this.weapon.magazine,reloading:this.reloadRemaining>0,reloadProgress:+this.reloadProgress.toFixed(2),cooldown:+this.cooldown.toFixed(2),dodgeReady:this.dodgeCooldown<=0,dodging:this.dodgeTime>0,hitMarker:this.hitMarker>0,unlocked:WEAPONS.filter(w=>this.inventory[w.id].owned).map(w=>w.id),weapons:WEAPONS.map(w=>({id:w.id,name:w.name,owned:this.inventory[w.id].owned,ammo:this.inventory[w.id].ammo,reserve:this.inventory[w.id].reserve,magazine:w.magazine,price:w.price,refillPrice:w.refillPrice})),enemiesRemaining:this.enemiesRemaining,enemies:this.enemies.filter(e=>alive(e)||distance(e,this.sim.pos)<600).map(e=>({id:e.id,encounterId:e.encounterId,tag:e.tag,x:Math.round(e.x),y:Math.round(e.y),health:Math.ceil(e.health),maxHealth:e.maxHealth,hostile:e.hostile,defeated:e.defeated,telegraph:e.windup>0})),stats:{...this.stats}};}
 select(value){
  const w=typeof value==='number'?WEAPONS[value]:WEAPONS.find(w=>w.id===value);
  if(!w)return false;if(!this.inventory[w.id].owned){this.toast(`${w.name} is available at the gun shop.`);return false;}
  if(this.selected===w.id)return true;
  this.selected=w.id;this.reloadRemaining=0;this.reloadDuration=0;this.cooldown=.16;this.recoil=0;this.sound('weaponSwitch');this.sync();this.emit('save');return true;
 }
 cycle(delta=1){const owned=WEAPONS.filter(w=>this.inventory[w.id].owned);const i=owned.findIndex(w=>w.id===this.selected);return this.select(owned[(i+(delta<0?-1:1)+owned.length)%owned.length].id);}
 purchase(id){
  const w=WEAPONS.find(w=>w.id===id);if(!w)return false;
  if(this.inventory[id].owned){this.toast('You already own this weapon. Refill it below.');return false;}
  if(this.sim.cash<w.price){this.toast(`You need $${w.price.toLocaleString()} for the ${w.name}.`);return false;}
  this.sim.cash-=w.price;this.inventory[id]={owned:true,ammo:w.magazine,reserve:w.reserve};this.select(id);this.sound('coin');this.toast(`${w.name} unlocked. Ammunition included.`);this.emit('save');return true;
 }
 refill(id=this.selected,cost){
  const w=WEAPONS.find(w=>w.id===id);if(!w||!this.inventory[id].owned)return false;
  const item=this.inventory[id],price=Number.isFinite(cost)?Math.max(0,cost):w.refillPrice;
  if(item.ammo===w.magazine&&item.reserve===w.maxReserve){this.toast('Fully stocked.');return false;}
  if(this.sim.cash<price){this.toast(`Ammunition costs $${price}.`);return false;}
  this.sim.cash-=price;item.ammo=w.magazine;item.reserve=w.maxReserve;if(this.selected===id){this.reloadRemaining=0;this.reloadDuration=0;}this.sync();this.sound('coin');this.toast(`${w.name} ammunition refilled.`);this.emit('save');return true;
 }
 refillAll(free=false){let paid=0;for(const w of WEAPONS){const item=this.inventory[w.id];if(!item.owned)continue;const cost=free?0:w.refillPrice;if(this.sim.cash<cost)continue;if(item.ammo===w.magazine&&item.reserve===w.maxReserve)continue;this.sim.cash-=cost;paid+=cost;item.ammo=w.magazine;item.reserve=w.maxReserve;}this.reloadRemaining=0;this.sync();this.emit('save');return paid;}
 reloadWeapon(){
  if(this.sim.mode!=='play'||this.sim.vehicle||this.reloadRemaining>0||this.dodgeTime>0)return false;
  const item=this.inventory[this.selected],w=this.weapon;if(item.ammo>=w.magazine)return false;
  if(item.reserve<=0){if(this.dryCooldown<=0){this.toast('Out of ammunition. Visit the gun shop or rest at The Palms.');this.sound('empty');this.dryCooldown=1.4;}return false;}
  this.reloadDuration=w.reloadTime;this.reloadRemaining=w.reloadTime;this.sound('reload');this.sync();return true;
 }
 resetAfterBusted(){this.bullets=[];this.clearEncounter();this.safeGrace=[];this.reloadRemaining=0;this.cooldown=0;this.dodgeTime=0;this.dodgeCooldown=0;this.damageFlash=0;const starter=this.inventory.pistol;starter.ammo=12;starter.reserve=Math.max(starter.reserve,36);this.select('pistol');this.sync();}
 resupply(id=this.selected,magazines=2){const w=WEAPONS.find(w=>w.id===id),item=this.inventory[id];if(!w||!item?.owned)return false;item.reserve=Math.min(w.maxReserve,item.reserve+Math.max(0,Math.floor(finite(magazines,2)))*w.magazine);this.sync();this.emit('save');return true;}
 restockStarter(){const item=this.inventory.pistol;item.ammo=12;item.reserve=Math.max(60,item.reserve);this.reloadRemaining=0;this.sync();}
 resolveAim(aim){if(!aim)return null;const p=this.sim.player;const target=this.enemies.filter(e=>alive(e)&&Math.hypot(aim.x-e.x,aim.y-(e.y-e.hitOffsetY))<21&&this.lineClear(p,e)).sort((a,b)=>Math.hypot(aim.x-a.x,aim.y-a.y)-Math.hypot(aim.x-b.x,aim.y-b.y))[0];return target?{x:target.x,y:target.y}:aim;}
 isSafeShot(aim){const p=this.sim.player,range=this.sim.world.pois?.find(q=>q.key==='gunrange'||q.id==='gunrange');const endpoint=aim||{x:p.x+Math.cos(p.angle)*200,y:p.y+Math.sin(p.angle)*200};if(range&&distance(p,range)<=500&&distance(endpoint,range)<=500)return true;this.safeGrace=this.safeGrace.filter(z=>z.expires>this.sim.time);for(const encounter of [...this.encounters.values(),...this.safeGrace])if(encounter.safe&&distance(p,encounter.center)<=encounter.safeRadius){if(aim&&distance(aim,encounter.center)<=encounter.safeRadius)return true;const a=aim?Math.atan2(aim.y-p.y,aim.x-p.x):p.angle;const end={x:p.x+Math.cos(a)*this.weapon.range,y:p.y+Math.sin(a)*this.weapon.range};if(this.enemies.some(e=>e.encounterId===encounter.id&&!e.hostile&&alive(e)&&segmentCircle(p.x,p.y,end.x,end.y,e.x,e.y,24)!==null))return true;}return false;}
 fire(aim=null){
  if(this.sim.mode!=='play'||this.sim.vehicle||this.cooldown>0||this.reloadRemaining>0||this.dodgeTime>0)return false;
  const item=this.inventory[this.selected];if(item.ammo<=0){this.reloadWeapon();return false;}
  const p=this.sim.player,w=this.weapon,resolved=this.resolveAim(aim),a=resolved&&Number.isFinite(resolved.x)&&Number.isFinite(resolved.y)?Math.atan2(resolved.y-p.y,resolved.x-p.x):p.angle;
  const safe=this.isSafeShot(aim);p.angle=a;this.lastAim=aim?{...aim}:null;this.lastShot=this.sim.time;this.shotSequence++;item.ammo--;this.cooldown=w.interval;this.muzzle=.065;this.recoil=Math.min(1,this.recoil+(w.id==='shotgun'?.8:.24));this.stats.shots++;
  const bloom=w.spread*(1+(p.moving?.6:0)+this.recoil*(w.id==='smg'?2:.35));
  for(let i=0;i<w.pellets;i++){const offset=w.pellets===1?(seed(this.shotSequence*7)-.5)*bloom*2:((i/(w.pellets-1)-.5)*2+(.2*seed(i+this.shotSequence)-.1))*bloom;const angle=a+offset;
   this.bullets.push({x:p.x+Math.cos(a)*13,y:p.y+Math.sin(a)*13,prevX:p.x,prevY:p.y,vx:Math.cos(angle)*w.speed,vy:Math.sin(angle)*w.speed,life:w.range/w.speed,damage:w.damage,owner:'player',color:w.color,shot:this.shotSequence,weapon:w.id,safe});
  }
  this.sound(w.id==='pistol'?'shoot':`shoot_${w.id}`);
  if(!safe){if(this.sim.time-this.sim.lastCrime>3||this.sim.wanted===0)this.sim.addHeat?.(1);this.sim.lastCrime=this.sim.time;for(const n of this.sim.people||[])if(distance(p,n)<300)n.panic=4;}
  for(const encounter of this.encounters.values())if(encounter.safe&&distance(p,encounter.center)<=encounter.safeRadius)encounter.shots++;
  this.sync();return true;
 }
 dodge(direction=null){
  if(this.sim.mode!=='play'||this.sim.vehicle||this.dodgeCooldown>0)return false;
  this.dodgeAngle=typeof direction==='number'?direction:direction&&Math.hypot(direction.x,direction.y)>.1?Math.atan2(direction.y,direction.x):this.sim.player.angle;
  this.dodgeTime=.28;this.dodgeCooldown=1.15;this.reloadRemaining=0;this.sound('dodge');return true;
 }
 update(dt,keys={},aim=null){
  if(this.sim.mode!=='play')return;
  dt=clamp(finite(dt,0),0,.25);this.cooldown=Math.max(0,this.cooldown-dt);this.dryCooldown=Math.max(0,this.dryCooldown-dt);this.muzzle=Math.max(0,this.muzzle-dt);this.hitMarker=Math.max(0,this.hitMarker-dt);this.damageFlash=Math.max(0,this.damageFlash-dt);this.recoil=Math.max(0,this.recoil-dt*1.4);this.dodgeCooldown=Math.max(0,this.dodgeCooldown-dt);
  if(aim&&Number.isFinite(aim.x)&&Number.isFinite(aim.y))this.lastAim={...aim};
  if(keys.KeyQ&&!this.qHeld)this.dodge({x:(keys.KeyD||keys.ArrowRight?1:0)-(keys.KeyA||keys.ArrowLeft?1:0),y:(keys.KeyS||keys.ArrowDown?1:0)-(keys.KeyW||keys.ArrowUp?1:0)});this.qHeld=!!keys.KeyQ;
  if(this.dodgeTime>0){const step=Math.min(dt,this.dodgeTime);this.dodgeTime=Math.max(0,this.dodgeTime-dt);if(!this.sim.vehicle){const p=this.sim.player;this.effects.push({kind:'dodge',x:p.x,y:p.y,life:.22,maxLife:.22,color:'#fff4cc'});this.move(p,Math.cos(this.dodgeAngle)*330*step,Math.sin(this.dodgeAngle)*330*step,8);}}
  if(this.reloadRemaining>0){this.reloadRemaining=Math.max(0,this.reloadRemaining-dt);if(this.reloadRemaining<=0){const item=this.inventory[this.selected],amount=Math.min(this.weapon.magazine-item.ammo,item.reserve);item.ammo+=amount;item.reserve-=amount;this.sound('reloadDone');this.emit('save');}}
  if(!this.sim.vehicle&&(keys.Space||keys.Mouse0||keys.fire))this.fire(aim);
  if(!this.sim.vehicle&&aim&&this.dodgeTime<=0){const target=this.resolveAim(aim);this.sim.player.angle=Math.atan2(target.y-this.sim.player.y,target.x-this.sim.player.x);}
  this.updateEnemies(dt);this.updateBullets(dt);
  for(const effect of this.effects)effect.life-=dt;this.effects=this.effects.filter(e=>e.life>0).slice(-180);
  this.sync();
 }
 move(entity,dx,dy,radius=8){if(this.sim.move)return this.sim.move(entity,dx,dy,radius);const solid=this.sim.world.isSolid;let hit=false;if(!solid(entity.x+dx,entity.y,radius))entity.x+=dx;else hit=true;if(!solid(entity.x,entity.y+dy,radius))entity.y+=dy;else hit=true;return hit;}
 wallHit(ax,ay,bx,by,radius=2){const length=Math.hypot(bx-ax,by-ay),steps=Math.max(1,Math.ceil(length/4));for(let i=0;i<=steps;i++){const t=i/steps;if(this.sim.world.isSolid(ax+(bx-ax)*t,ay+(by-ay)*t,radius))return t;}return null;}
 lineClear(a,b){return this.wallHit(a.x,a.y,b.x,b.y,2)===null;}
 spawnEncounter(spec={}){
  const id=String(spec.id||`encounter-${this.enemySequence++}`);this.clearEncounter(id);
  const center={x:finite(spec.center?.x,finite(spec.x,this.sim.player.x+220)),y:finite(spec.center?.y,finite(spec.y,this.sim.player.y))},spread=Math.max(20,finite(spec.spread,120)),difficulty=clamp(finite(spec.difficulty,1),.5,3),safe=!!spec.safe;
  const encounter={id,center,safe,safeRadius:Math.max(60,finite(spec.safeRadius,spread+170)),shots:0,hits:0,defeated:0,total:0};this.encounters.set(id,encounter);
  const definitions=Array.isArray(spec.enemies)?spec.enemies:Array.from({length:clamp(Math.floor(finite(spec.count,3)),1,24)},(_,i)=>{const a=(i/Math.max(1,spec.count||3))*Math.PI*2+.4;return{x:center.x+Math.cos(a)*spread,y:center.y+Math.sin(a)*spread}});
  for(let i=0;i<definitions.length;i++){const def=definitions[i],hostile=def.hostile??(spec.kind!=='target');const point=this.findOpen({x:finite(def.x,center.x),y:finite(def.y,center.y)});if(!point)continue;
   const maxHealth=clamp(finite(def.health,hostile?48+difficulty*8:28),1,350);
   this.enemies.push({id:def.id||`${id}-${i}`,encounterId:id,tag:def.tag||spec.tag||null,x:point.x,y:point.y,homeX:point.x,homeY:point.y,angle:0,health:maxHealth,maxHealth,hostile,defeated:false,kind:hostile?(spec.kind||'guard'):'target',skin:def.skin||'#bb8268',shirt:def.shirt||['#be746a','#6a7f91','#bf9e65'][i%3],moving:false,phase:i,hitFlash:0,windup:0,windupDuration:.84,lockedAim:null,cooldown:1.15+i*.35,sequence:0,difficulty,speed:33+difficulty*8,radius:hostile?13:18,hitOffsetY:hostile?8:11,sightTimer:0,canSee:false,defeatedTime:0,shotHits:new Set()});encounter.total++;
  }
  return id;
 }
 findOpen(point){if(!this.sim.world.isSolid(point.x,point.y,11))return point;for(let r=20;r<=180;r+=20)for(let i=0;i<12;i++){const p={x:point.x+Math.cos(i*Math.PI/6)*r,y:point.y+Math.sin(i*Math.PI/6)*r};if(!this.sim.world.isSolid(p.x,p.y,11))return p;}return null;}
 remaining(id){return this.enemies.filter(e=>e.encounterId===id&&alive(e)).length;}
 encounterState(id){const encounter=this.encounters.get(id);return encounter?{...encounter,center:{...encounter.center},remaining:this.remaining(id)}:null;}
 clearEncounter(id){for(const e of this.encounters.values())if(e.safe&&(id===undefined||e.id===id))this.safeGrace.push({...e,expires:this.sim.time+.8});this.safeGrace=this.safeGrace.filter(e=>e.expires>this.sim.time).slice(-12);if(id===undefined){this.enemies=[];this.encounters.clear();this.bullets=this.bullets.filter(b=>b.owner==='player');return;}
  this.enemies=this.enemies.filter(e=>e.encounterId!==id);this.encounters.delete(id);this.bullets=this.bullets.filter(b=>b.encounterId!==id);
 }
 updateEnemies(dt){
  const p=this.sim.pos;
  for(const e of this.enemies){e.hitFlash=Math.max(0,e.hitFlash-dt);e.moving=false;
   if(!alive(e)){e.defeatedTime+=dt;continue;}if(!e.hostile)continue;
   const d=distance(e,p);if(d>700){e.windup=0;continue;}
   e.phase+=dt*7;e.cooldown=Math.max(0,e.cooldown-dt);
   e.sightTimer-=dt;if(e.sightTimer<=0){e.canSee=this.lineClear(e,p);e.sightTimer=.12;}const visible=e.canSee;
   if(e.windup>0){e.windup=Math.max(0,e.windup-dt);e.angle=Math.atan2(e.lockedAim.y-e.y,e.lockedAim.x-e.x);
    if(e.windup===0){if(visible&&d<540){const a=e.angle;this.bullets.push({x:e.x+Math.cos(a)*14,y:e.y+Math.sin(a)*14,prevX:e.x,prevY:e.y,vx:Math.cos(a)*280,vy:Math.sin(a)*280,life:1.9,damage:13+e.difficulty*2,owner:'enemy',enemyId:e.id,encounterId:e.encounterId,color:'#f47f6e'});this.effects.push({kind:'muzzle',x:e.x+Math.cos(a)*16,y:e.y+Math.sin(a)*16,angle:a,life:.08,maxLife:.08,color:'#ffc58e'});this.sound('enemyShoot');}e.cooldown=1.4+seed(++e.sequence+e.x)*.8;}
    continue;
   }
   e.angle=Math.atan2(p.y-e.y,p.x-e.x);
   if(visible&&d<490&&e.cooldown<=0){e.windupDuration=.84;e.windup=e.windupDuration;e.lockedAim={x:p.x,y:p.y};continue;}
   // Guardians stay in their district; small flanking movement gives room to dodge.
   if(d>120&&distance(e,{x:e.homeX,y:e.homeY})<280){const approach=d>260?1:0;const strafe=d<280&&visible?.45*Math.sign(Math.sin(e.x+this.sim.time*.45)):0;const a=e.angle;const dx=(Math.cos(a)*approach-Math.sin(a)*strafe)*e.speed*dt,dy=(Math.sin(a)*approach+Math.cos(a)*strafe)*e.speed*dt;this.move(e,dx,dy,8);e.moving=Math.hypot(dx,dy)>.01;}
  }
 }
 impact(b,kind,target){
  this.effects.push({kind:kind==='enemy'?'hit':'spark',x:b.x,y:b.y,life:.24,maxLife:.24,color:kind==='enemy'?'#fff2c4':b.color});
  if(kind==='enemy'){
   const e=target;if(!alive(e))return;e.health=Math.max(0,e.health-b.damage);e.hitFlash=.14;
   if(!e.shotHits.has(b.shot)){e.shotHits.add(b.shot);this.stats.hits++;const encounter=this.encounters.get(e.encounterId);if(encounter)encounter.hits++;}
   this.hitMarker=.18;this.hitKill=e.health<=0;
   if(e.health<=0){e.defeated=true;e.windup=0;e.moving=false;this.stats.defeated++;const encounter=this.encounters.get(e.encounterId);if(encounter)encounter.defeated++;this.emit('enemyDefeated',{id:e.id,encounterId:e.encounterId,tag:e.tag,x:e.x,y:e.y,hostile:e.hostile});this.sound(e.hostile?'enemyDown':'targetHit');}else this.sound('hit');
  }else if(kind==='player'){
   if(this.dodgeTime>0)return;
   this.sim.health=Math.max(0,this.sim.health-b.damage);this.damageFlash=.32;this.sound('hurt');
  }else if(kind==='car'){
   target.health=Math.max(0,target.health-b.damage*.45);if(target!==this.sim.vehicle){target.parked=true;target.speed*=.4;}else if(b.owner==='enemy'){this.sim.health=Math.max(0,this.sim.health-b.damage*.12);this.damageFlash=.16;}
  }
 }
 updateBullets(dt){
  const cars=[...(this.sim.cars||[]),...(this.sim.police||[])];
  for(const b of this.bullets){if(b.life<=0)continue;const step=Math.min(dt,b.life),ax=b.x,ay=b.y,bx=ax+b.vx*step,by=ay+b.vy*step;let first={t:2,kind:null,target:null};
   const wall=this.wallHit(ax,ay,bx,by);if(wall!==null)first={t:wall,kind:'wall',target:null};
   const check=(target,radius,kind,offsetY=0)=>{const t=segmentCircle(ax,ay,bx,by,target.x,target.y-offsetY,radius);if(t!==null&&t<first.t)first={t,kind,target};};
   if(b.owner==='player'){for(const e of this.enemies)if(alive(e))check(e,e.radius,'enemy',e.hitOffsetY);}
   else if(!this.sim.vehicle)check(this.sim.player,9,'player');
   for(const car of cars){if(car.health<=0)continue;check(car,18,'car');}
   b.prevX=ax;b.prevY=ay;b.x=first.t<=1?ax+(bx-ax)*first.t:bx;b.y=first.t<=1?ay+(by-ay)*first.t:by;b.life-=dt;
   if(first.t<=1){this.impact(b,first.kind,first.target);b.life=0;}
  }
  this.bullets=this.bullets.filter(b=>b.life>0).slice(-240);
 }
}
