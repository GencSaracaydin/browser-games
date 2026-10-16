// Replayable county adventures. Each run owns its objectives and temporary cast;
// story progression and the player's position are never changed by starting one.
const distance = (a, b) => Math.hypot(a.x - b.x, a.y - b.y);
const clamp = (v, lo, hi) => Math.max(lo, Math.min(hi, v));
const finite = (v, fallback = 0) => Number.isFinite(v) ? v : fallback;
const point = (x, y, name) => ({ x, y, name });
const MEDALS = ['none', 'bronze', 'silver', 'gold'];

export const ACTIVITY_CATALOG = [
  { id: 'race-coast', type: 'race', name: 'Coastline Club', category: 'STREET RACE', color: '#ecb65e', icon: 'flag', start: point(2320, 1160, 'Coastline Club'), reward: 250, description: 'Seven checkpoints through the old coast. Beat your personal best.', medals: { gold: 49, silver: 67, bronze: 95 }, route: [[2320,480],[1680,480],[1680,1160],[1080,1160],[1080,1840],[2320,1840],[2320,1160]] },
  { id: 'race-county', type: 'race', name: 'County Run', category: 'STREET RACE', color: '#ecb65e', icon: 'flag', start: point(2320, 3020, 'County Run'), reward: 350, description: 'Open the throttle on a long loop around the new county.', medals: { gold: 43, silver: 61, bronze: 90 }, route: [[4740,3020],[4740,4340],[2320,4340],[2320,3020]] },
  { id: 'race-airport', type: 'race', name: 'Sunset Speedway', category: 'CIRCUIT RACE', color: '#ecb65e', icon: 'flag', poi: 'race', reward: 250, description: 'A tight four-corner circuit. Fast exits win the gold.', medals: { gold: 25, silver: 36, bronze: 52 }, route: [[4480,4670],[4480,5190],[3430,5190],[3430,4670]] },
  { id: 'taxi', type: 'taxi', name: 'Golden Cab Shift', category: 'TAXI SHIFT', color: '#f2c463', icon: 'taxi', poi: 'taxi', reward: 850, description: 'Three fares, one yellow cab. Arrive quickly and keep it clean.' },
  { id: 'export', type: 'export', name: 'Special Order', category: 'VEHICLE EXPORT', color: '#7dc7be', icon: 'car', poi: 'export', reward: 1100, description: 'Find the silver Sunset GT, lose the heat, and bring it to the docks.' },
  { id: 'survival', type: 'survival', name: 'Dockyard Defense', category: 'SURVIVAL', color: '#e98b7c', icon: 'shield', poi: 'docks', reward: 1500, description: 'Three waves in the freight yard. Use cover and watch the flanks.' },
  { id: 'heist', type: 'heist', name: 'The Coral Exchange', category: 'HEIST', color: '#ce8fa4', icon: 'case', poi: 'heist', reward: 2600, description: 'Take the marked van, recover a guarded case, and disappear into the county.' },
  { id: 'bounty', type: 'bounty', name: 'Dead Calm', category: 'BOUNTY', color: '#98b7cf', icon: 'target', poi: 'bounty', reward: 1200, description: 'Find a fugitive and their crew. Subdue them and bring the target back.' },
  { id: 'range', type: 'range', name: 'Six in the Sun', category: 'TARGET PRACTICE', color: '#9bbf9f', icon: 'target', poi: 'gunrange', reward: 250, description: 'Six targets, 45 seconds. Learn the weapons and chase a gold medal.' },
];

const FALLBACK_POIS = {
  race: point(3430,4670,'Sunset Speedway'), taxi: point(3030,3500,'Golden Cab Co.'),
  export: point(4240,3910,'Azul Vehicle Exchange'), docks: point(4350,4100,'Freight Yard'),
  heist: point(4550,4140,'The Coral Exchange'), bounty: point(4200,3100,'Port Dispatch'),
  safehouse: point(850,3490,'Palomar Hideaway'), garage: point(750,1770,'Sol Auto'),
  marina: point(2450,2040,'Bluewater Marina'),
  gunrange: point(3770,3500,'Port Azul Range'),
};
const SYMBOLS = {race:'R',taxi:'T',export:'E',survival:'S',heist:'H',bounty:'B',range:'◎'};

export class ActivitySystem {
  constructor(sim, save = null) {
    this.sim = sim;
    this.catalog = ACTIVITY_CATALOG;
    this.active = null;
    this.selected = null;
    this.serial = 0;
    this.cooldown = 0;
    this.records = {};
    for (const def of this.catalog) {
      const saved = save?.records?.[def.id];
      this.records[def.id] = {
        completions: clamp(Math.floor(finite(saved?.completions)), 0, 999999),
        bestTime: Number.isFinite(saved?.bestTime) && saved.bestTime > 0 ? saved.bestTime : null,
        medal: MEDALS.includes(saved?.medal) ? saved.medal : 'none',
        earnings: clamp(Math.floor(finite(saved?.earnings)), 0, 100000000),
      };
    }
  }

  poi(key) { return this.sim.world.pois.find(p => p.key === key || p.id === key) || FALLBACK_POIS[key]; }
  definition(id) { return this.catalog.find(def => def.id === id); }
  startPoint(def) { return def.start || this.poi(def.poi); }
  storyBusy() { return this.sim.job && this.sim.job.index < 5 && this.sim.job.phase !== 'offer'; }
  status(id) {
    const def = this.definition(id);
    if (!def) return null;
    const start = this.startPoint(def);
    return { ...this.records[id], active: this.active?.id === id, selected: this.selected === id, available: !this.active && !this.storyBusy(), distance: Math.round(distance(this.sim.pos, start)) };
  }
  list() { return this.catalog.map(def => ({ ...def, start: this.startPoint(def), ...this.status(def.id) })); }
  select(id) {
    const def = this.definition(id);
    if (!def) return false;
    if (this.active) { this.sim.toast('Finish or cancel your current adventure first.'); return false; }
    this.selected = id;
    this.sim.customWaypoint = { ...this.startPoint(def), name: def.name, activityId: id };
    this.sim.toast(`${def.name} marked on your map. Press E at the marker to begin.`);
    return true;
  }
  nearby(radius = 90) {
    if (this.active || this.storyBusy()) return null;
    const close = this.catalog.map(def => ({ ...def, start: this.startPoint(def), distance: distance(this.sim.pos, this.startPoint(def)) })).filter(def => def.distance < radius);
    close.sort((a,b) => (b.id === this.selected) - (a.id === this.selected) || a.distance - b.distance);
    return close[0] || null;
  }
  context() {
    const a = this.active, s = this.sim;
    if (!a) {
      const def = this.nearby();
      if (def && ((def.type==='race'&&!s.vehicle)||(def.type==='range'&&s.vehicle))) return null;
      return def ? { kind: 'activity', key: 'E', id: def.id, label: `${def.name} · start ${def.type === 'race' ? 'race' : 'adventure'}` } : null;
    }
    const target = this.waypoint();
    if (!target || distance(s.pos, target) > 80) return null;
    if (a.type === 'taxi' && ['pickup', 'dropoff'].includes(a.phase) && s.vehicle?.id === a.carId) return { kind: 'activity', key: 'E', label: a.phase === 'pickup' ? 'Pick up your passenger' : 'Drop off your passenger' };
    if (a.type === 'export' && a.phase === 'deliver' && s.vehicle?.id === a.carId) return { kind: 'activity', key: 'E', label: s.wanted ? 'Lose the patrols first' : 'Deliver the silver Sunset GT' };
    if (a.type === 'heist' && a.phase === 'case') return { kind: 'activity', key: 'E', label: s.vehicle ? 'Get out to collect the case' : 'Collect the case' };
    if (a.type === 'heist' && a.phase === 'deliver' && s.vehicle?.id === a.carId) return { kind: 'activity', key: 'E', label: 'Deliver the case in the marked van' };
    if (a.type === 'bounty' && a.phase === 'capture') return { kind: 'activity', key: 'E', label: s.vehicle ? 'Get out to secure the target' : 'Secure the fugitive' };
    if (a.type === 'bounty' && a.phase === 'return') return { kind: 'activity', key: 'E', label: s.wanted ? 'Lose the patrols first' : 'Turn in the fugitive' };
    return null;
  }
  start(id) {
    const def = this.definition(id), s = this.sim;
    if (!def || s.mode !== 'play') return false;
    if (this.active) { s.toast('Finish or cancel your current adventure first.'); return false; }
    if (this.storyBusy()) { s.toast('Finish your current story job before taking a side adventure.'); return false; }
    if (distance(s.pos, this.startPoint(def)) > 90) { this.select(id); return false; }
    if (s.wanted) { s.toast('Lose the patrols before starting an adventure.'); return false; }
    if (this.cooldown > 0) { s.toast('Catch your breath. The next run is ready in a moment.'); return false; }
    if (def.type === 'race' && !s.vehicle) { s.toast('Bring a car to the starting line.'); return false; }
    if (['survival','heist','bounty','range'].includes(def.type) && !s.combat?.spawnEncounter) { s.toast('This adventure is temporarily unavailable.'); return false; }
    if (def.type==='range' && s.vehicle) { s.toast('Park your car and approach the range on foot.'); return false; }
    this.cleanupCars();
    const a = this.active = { id, type: def.type, phase: 'start', elapsed: 0, timer: 0, run: ++this.serial, encounter: null, carId: null };
    this.selected = null;
    s.customWaypoint = null;
    if (def.type === 'race') {
      a.phase = 'countdown'; a.countdown = 3; a.checkpoint = 0; a.timer = def.medals.bronze * 1.6;
      a.carId = s.vehicle.id; s.vehicle.speed = 0;
      s.toast(`${def.name} · 3… 2… 1…`);
    } else if (def.type === 'taxi') {
      const cab = this.spawnCar(point(this.poi('taxi').x, this.poi('taxi').y + 60), 'sedan', '#efc862', 'SUNLINE CAB');
      a.carId = cab.id; a.phase = 'vehicle'; a.fare = 0; a.fareEarnings = 0;
      a.pickup = { ...this.poi('taxi') }; a.destinations = ['safehouse','garage','marina'];
      s.toast('Your yellow cab is waiting. Three fares, and a bonus for finishing the shift.');
    } else if (def.type === 'export') {
      const q = this.poi('race');
      const car = this.spawnCar(point(q.x - 80, q.y + 50), 'sport', '#d5e1d7', 'SILVER SUNSET GT');
      car.owned = false; a.carId = car.id; a.phase = 'acquire';
      s.toast('Azul Vehicle Exchange wants the silver Sunset GT at the speedway. Bring it back in one piece.');
    } else if (def.type === 'survival') {
      a.phase = 'prepare'; a.wave = 0; a.timer = 4; a.center = { ...this.poi('docks') };
      s.toast('Dockyard Defense · leave your car and get behind cover. Wave one in 4 seconds.');
    } else if (def.type === 'heist') {
      const q = this.poi('export');
      const car = this.spawnCar(point(q.x - 65, q.y + 40), 'van', '#cc927f', 'CORAL COURIER VAN');
      a.carId = car.id; a.phase = 'acquire'; a.center = point(4510,4130,'Guarded case');
      s.toast('First, collect the coral van at Azul Vehicle Exchange. Then come back for the case.');
    } else if (def.type === 'bounty') {
      a.phase = 'search'; a.center = point(2730,4170,'Fugitive at County Plaza');
      s.toast('The fugitive is laying low near County Plaza. Find the crew and bring them in.');
    } else if (def.type === 'range') {
      a.phase='targets'; a.center={...this.poi('gunrange')}; a.timer=45; a.total=6; a.encounter=`range-${a.run}`;
      const enemies=[[-120,-120],[0,-170],[120,-120],[-80,-60],[0,-100],[80,-60]].map(([dx,dy])=>({...this.openPoint(point(a.center.x+dx,a.center.y+dy),9),health:1,hostile:false}));
      s.combat.restockStarter?.();
      s.combat.spawnEncounter({id:a.encounter,center:a.center,count:6,kind:'target',spread:160,safe:true,safeRadius:650,enemies});
      s.toast('Six targets. Aim with the mouse and hold to fire. R reloads. No police heat here.');
    }
    s.emit('sfx', { name: 'mission' });
    return true;
  }

  interact() {
    const a = this.active, s = this.sim;
    if (!a) {
      const def = this.nearby();
      if (!def || (def.type==='race'&&!s.vehicle) || (def.type==='range'&&s.vehicle)) return false;
      return this.start(def.id), true;
    }
    const target = this.waypoint();
    if (!target || distance(s.pos, target) > 80) return false;
    if (a.type === 'taxi' && ['pickup','dropoff'].includes(a.phase)) {
      if (s.vehicle?.id !== a.carId) return false;
      if (Math.abs(s.vehicle.speed) > 25) { s.toast('Stop the cab for your passenger.'); return true; }
      if (s.wanted) { s.toast('Your passenger would prefer to lose the patrols first.'); return true; }
      if (a.phase === 'pickup') {
        a.phase = 'dropoff'; a.startHealth = s.vehicle.health;
        const destination = this.poi(a.destinations[a.fare]);
        a.timer = a.fareDuration = Math.max(65, (Math.abs(s.pos.x-destination.x)+Math.abs(s.pos.y-destination.y))/105 + 55);
        s.toast(`Fare ${a.fare + 1} / 3 · ${destination.name}. A smooth ride earns a better tip.`);
      } else {
        const damage = Math.max(0, a.startHealth-s.vehicle.health);
        const tip = Math.max(0, Math.round(150 * a.timer/a.fareDuration - damage*4));
        a.fareEarnings += 150 + tip;
        a.fare++;
        if (a.fare === 3) this.complete(250 + a.fareEarnings, 'Three happy passengers. A good day behind the wheel.');
        else { a.pickup = { ...target }; a.phase = 'pickup'; s.toast(`Fare complete · $${150+tip} toward your shift. Press E for the next passenger.`); s.emit('sfx',{name:'coin'}); }
      }
      return true;
    }
    if (a.type === 'export' && a.phase === 'deliver') {
      if (s.vehicle?.id !== a.carId) return false;
      if (s.wanted) { s.toast('Lose the patrols before making the delivery.'); return true; }
      if (Math.abs(s.vehicle.speed) > 30) { s.toast('Park the car for the inspection.'); return true; }
      const payment = Math.round(450 + 650 * clamp(s.vehicle.health,0,100)/100);
      this.releaseDeliveredCar(); this.complete(payment, 'The car is on the manifest. So is your payment.'); return true;
    }
    if (a.type === 'heist' && a.phase === 'case') {
      if (s.vehicle) return false;
      a.phase = 'escape'; this.clearEncounter(); if (s.wanted<2) s.addHeat(2-s.wanted);
      s.toast('Case secured. Get back to the marked van and lose the patrols.'); return true;
    }
    if (a.type === 'heist' && a.phase === 'deliver') {
      if (s.wanted) { s.toast('Lose the patrols before approaching the hideaway.'); return true; }
      if (s.vehicle?.id !== a.carId) return false;
      if (Math.abs(s.vehicle.speed) > 30) { s.toast('Stop the van to hand over the case.'); return true; }
      this.complete(2600, 'A quiet exchange. A very loud payday.'); return true;
    }
    if (a.type === 'bounty' && a.phase === 'capture') {
      if (s.vehicle) return false;
      a.phase = 'return'; this.clearEncounter(); s.toast('Fugitive secured. Return to the Port Dispatch.'); return true;
    }
    if (a.type === 'bounty' && a.phase === 'return') {
      if (s.wanted) { s.toast('Lose the patrols before returning to the bail office.'); return true; }
      if (s.vehicle && Math.abs(s.vehicle.speed)>30) { s.toast('Stop here to turn in the fugitive.'); return true; }
      this.complete(1200, 'The county can take it from here. Bounty paid.'); return true;
    }
    return false;
  }

  update(dt) {
    if (this.sim.mode !== 'play') return;
    this.cooldown = Math.max(0, this.cooldown - dt);
    const a = this.active, s = this.sim;
    if (!a) return;
    a.elapsed += dt;
    const car = this.car();
    if (a.carId && (!car || car.health <= 0)) { this.fail('The mission vehicle was lost.'); return; }
    if (s.health <= 0) { this.fail('You need a little time to recover.'); return; }
    if (a.type === 'race') {
      const def = this.definition(a.id);
      if (a.phase === 'countdown') {
        if (s.vehicle?.id !== a.carId) { this.fail('You left the starting car.'); return; }
        s.vehicle.speed = 0; a.countdown -= dt;
        if (a.countdown <= 0) { a.phase = 'race'; a.elapsed = 0; s.toast('GO! Hit every checkpoint.'); s.emit('sfx',{name:'coin'}); }
        return;
      }
      a.timer -= dt;
      const target = this.waypoint();
      if (s.vehicle?.id === a.carId && target && distance(s.pos,target)<92) {
        a.checkpoint++; s.emit('sfx',{name:'coin'});
        if (a.checkpoint >= def.route.length) {
          const time = a.elapsed, record = this.records[a.id];
          const medal = time <= def.medals.gold ? 'gold' : time <= def.medals.silver ? 'silver' : time <= def.medals.bronze ? 'bronze' : 'none';
          const bonus = Math.max(0,MEDALS.indexOf(medal)-MEDALS.indexOf(record.medal))*175;
          const best = record.bestTime === null || time < record.bestTime;
          if (best) record.bestTime = time;
          if (MEDALS.indexOf(medal)>MEDALS.indexOf(record.medal)) record.medal = medal;
          a.medal=medal;
          this.complete(def.reward+bonus, `${time.toFixed(1)}s${medal==='none'?'':` · ${medal.toUpperCase()}`}${best?' · PERSONAL BEST':''}`); return;
        }
        s.toast(`Checkpoint ${a.checkpoint} / ${def.route.length}`);
      }
      if (a.timer <= 0) this.fail('Time ran out. The starting line is waiting for another try.');
    } else if (a.type === 'taxi') {
      if (a.phase === 'vehicle' && s.vehicle?.id === a.carId) { a.phase = 'pickup'; s.toast('Pull up to Golden Cab Co. and press E to collect your first passenger.'); }
      if (a.phase === 'dropoff') { a.timer -= dt; if (a.timer <= 0) this.fail('Your passenger found another ride. Shift ended.'); }
    } else if (a.type === 'export') {
      if (a.phase === 'acquire' && s.vehicle?.id === a.carId) { a.phase = 'deliver'; if (s.wanted<2) s.addHeat(2-s.wanted); s.toast('That is the one. Lose the heat and take it to Azul Vehicle Exchange.'); }
    } else if (a.type === 'survival') {
      if (distance(s.pos,a.center)>800) { this.fail('You left the freight yard.'); return; }
      if (a.phase === 'prepare') { a.timer -= dt; if (a.timer<=0) this.spawnWave(); }
      else if (a.phase === 'wave' && s.combat.remaining(a.encounter) === 0) {
        if (a.wave===3) { this.complete(1500,'Three waves down. The freight yard is yours.'); return; }
        this.clearEncounter(); a.phase='prepare'; a.timer=4; s.health=Math.min(100,s.health+22);
        this.restock(); s.toast(`Wave ${a.wave} clear. Breathe, reload, find cover.`);
      }
    } else if (a.type === 'heist') {
      if (a.phase === 'acquire' && s.vehicle?.id === a.carId) { a.phase='approach'; s.toast('Take the van back to the exchange. The case has company.'); }
      if (a.phase === 'approach' && distance(s.pos,a.center)<200) {
        a.phase='guards'; a.encounter=`heist-${a.run}`;
        s.combat.spawnEncounter({id:a.encounter,center:a.center,count:4,difficulty:1.1,spread:105,safe:true,safeRadius:600});
        s.toast('Four guards. Leave the van, use cover, and secure the case.');
      }
      if (a.phase === 'guards' && s.combat.remaining(a.encounter) === 0) { a.phase='case'; s.toast('The case is clear. Approach it on foot and press E.'); }
      if (a.phase === 'escape' && !s.wanted) { a.phase='deliver'; s.toast('The coast is clear. Take the coral van to the Palomar Hideaway.'); }
    } else if (a.type === 'bounty') {
      if (a.phase === 'search' && distance(s.pos,a.center)<450) {
        a.phase='fight'; a.encounter=`bounty-${a.run}`;
        const q=this.openPoint(a.center,9);
        s.combat.spawnEncounter({id:a.encounter,center:a.center,count:3,difficulty:1,spread:120,safe:true,safeRadius:650,enemies:[{...q,health:90,tag:'bounty',shirt:'#b56783'},{...this.openPoint(point(q.x+115,q.y-60),9),health:50},{...this.openPoint(point(q.x-110,q.y+65),9),health:50}]});
        s.toast('Target located. Subdue the crew, then approach the fugitive on foot.');
      }
      if (a.phase === 'fight' && s.combat.remaining(a.encounter)===0) {
        const target=s.combat.enemies.find(e=>e.encounterId===a.encounter&&e.tag==='bounty');
        a.capture=target?point(target.x,target.y,'Subdued fugitive'):{...a.center};
        a.phase='capture'; s.toast('The fugitive is down. Press E nearby to secure them.');
      }
    } else if (a.type === 'range') {
      if (distance(s.pos,a.center)>600) { this.fail('You left the shooting range.'); return; }
      a.timer-=dt;
      const hits=a.total-s.combat.remaining(a.encounter);
      if (hits===a.total || a.timer<=0) {
        if (!hits) { this.fail('No targets hit. Try aiming with the mouse or moving closer.'); return; }
        const record=this.records[a.id];
        const medal=hits===a.total?(a.elapsed<=18?'gold':a.elapsed<=30?'silver':'bronze'):'none';
        if (hits===a.total && (record.bestTime===null || a.elapsed<record.bestTime)) record.bestTime=a.elapsed;
        if (MEDALS.indexOf(medal)>MEDALS.indexOf(record.medal)) record.medal=medal;
        a.medal=medal;
        this.complete(10+hits*40,`${hits} / ${a.total} targets · ${a.elapsed.toFixed(1)}s${medal==='none'?'':` · ${medal.toUpperCase()}`}`);
      }
    }
  }

  spawnWave() {
    const a=this.active;
    if (!a || a.type!=='survival') return;
    a.wave++; a.phase='wave'; a.encounter=`survival-${a.run}-${a.wave}`;
    this.sim.combat.spawnEncounter({id:a.encounter,center:a.center,count:a.wave+2,difficulty:.8+a.wave*.12,spread:150,safe:true,safeRadius:650});
    this.restock(); this.sim.toast(`Wave ${a.wave} / 3 · ${a.wave+2} intruders. Stay moving.`);
  }
  restock() {
    // Combat owns ammunition; this optional hook supplies a small arena refill.
    this.sim.combat?.resupply?.(undefined,2);
  }
  car() { return this.active?.carId ? this.sim.cars.find(car=>car.id===this.active.carId) || this.sim.police?.find(car=>car.id===this.active.carId) : null; }
  openPoint(origin,radius=17) {
    if (!this.sim.world.isSolid(origin.x,origin.y,radius)) return {...origin};
    for (let r=25;r<=325;r+=25) for (let i=0;i<16;i++) {
      const q=point(origin.x+Math.cos(i*Math.PI/8)*r,origin.y+Math.sin(i*Math.PI/8)*r);
      if (!this.sim.world.isSolid(q.x,q.y,radius)) return q;
    }
    return {...origin};
  }
  spawnCar(origin,type,color,name) {
    const q=this.openPoint(origin,18);
    const car={...q,id:`activity-car-${this.active.run}-${this.active.type}`,activityVehicle:true,activityName:name,name,type,color,angle:0,speed:0,health:100,parked:true,owned:true,occupied:false,route:null};
    this.sim.cars.push(car); return car;
  }
  cleanupCars() { this.sim.cars=this.sim.cars.filter(car=>!car.activityVehicle||car===this.sim.vehicle); }
  clearEncounter() {
    if (this.active?.encounter) this.sim.combat?.clearEncounter(this.active.encounter);
    if (this.active) this.active.encounter=null;
  }
  releaseDeliveredCar() {
    const s=this.sim, car=this.car();
    if (car && s.vehicle===car) {
      const exit=this.openPoint(point(car.x,car.y+42),9);
      s.player.x=exit.x; s.player.y=exit.y; s.player.moving=false; s.vehicle=null; car.occupied=false;
    }
    if (car) s.cars=s.cars.filter(c=>c!==car);
  }
  complete(reward,message) {
    const a=this.active;
    if (!a) return false;
    const def=this.definition(a.id),record=this.records[a.id];
    reward=Math.max(0,Math.round(reward)); record.completions++; record.earnings+=reward;
    this.clearEncounter(); this.active=null; this.cooldown=6;
    this.cleanupCars(); this.sim.cash+=reward; this.sim.health=Math.min(100,this.sim.health+20);
    this.sim.emit('activityComplete',{title:def.name,reward,message,medal:a.medal&&a.medal!=='none'?a.medal:null});
    this.sim.emit('sfx',{name:'complete'}); this.sim.emit('save'); return true;
  }
  cancel(reason='Adventure cancelled. You can try it again at the marker.') {
    if (!this.active) return false;
    this.clearEncounter(); this.active=null; this.cooldown=1; this.cleanupCars();
    this.sim.toast(reason); return true;
  }
  fail(reason='Adventure interrupted.') { return this.cancel(`${reason} No reward lost from earlier runs.`); }
  waypoint() {
    const a=this.active;
    if (!a) return null;
    const def=this.definition(a.id);
    if (a.type==='race') {
      if (a.phase==='countdown') return this.startPoint(def);
      const q=def.route[a.checkpoint]; return q?point(q[0],q[1],`Checkpoint ${a.checkpoint+1} / ${def.route.length}`):null;
    }
    if (a.type==='taxi') return a.phase==='vehicle'?this.car():a.phase==='pickup'?{...a.pickup,name:`Passenger ${a.fare+1}`} : this.poi(a.destinations[a.fare]);
    if (a.type==='export') return a.phase==='acquire'?this.car():this.poi('export');
    if (a.type==='survival') return {...a.center,name:`Wave ${a.wave||1} / 3`};
    if (a.type==='heist') {
      if (a.phase==='acquire') return this.car();
      if (a.phase==='deliver') return this.sim.vehicle?.id===a.carId?this.poi('safehouse'):this.car();
      if (a.phase==='escape') return this.sim.vehicle?.id===a.carId?this.poi('safehouse'):this.car();
      return this.poi('heist');
    }
    if (a.type==='bounty') return a.phase==='return'?this.poi('bounty'):a.phase==='capture'?a.capture:a.center;
    if (a.type==='range') return this.sim.combat.enemies.find(e=>e.encounterId===a.encounter&&e.health>0)||a.center;
    return null;
  }
  objective() {
    const a=this.active;
    if (!a) return null;
    const def=this.definition(a.id),remaining=this.sim.combat?.remaining?.(a.encounter)??0;
    let detail='',extra=`$${def.reward.toLocaleString()} REWARD`;
    if (a.type==='race') {
      detail=a.phase==='countdown'?`On your marks… ${Math.max(1,Math.ceil(a.countdown))}`:`Checkpoint ${a.checkpoint+1} / ${def.route.length}. Follow the gold gates.`;
      extra=`${a.elapsed.toFixed(1)}s · GOLD ${def.medals.gold}s · ${Math.ceil(a.timer)}s LEFT`;
    } else if (a.type==='taxi') {
      detail=a.phase==='vehicle'?'Take the yellow Sunline cab.':a.phase==='pickup'?`Collect passenger ${a.fare+1} of 3. Stop close and press E.`:`Take your passenger to ${this.poi(a.destinations[a.fare]).name}. Stop and press E.`;
      extra=a.phase==='dropoff'?`${Math.ceil(a.timer)}s LEFT · FARE ${a.fare+1} / 3 · KEEP THE CAR CLEAN`:`THREE FARES · $${a.fareEarnings} ACCRUED · $250 SHIFT BONUS`;
    } else if (a.type==='export') detail=a.phase==='acquire'?'Take the marked silver Sunset GT at the speedway.':this.sim.wanted?'Lose the patrols. Protect the car: damage lowers your payout.':'Bring the silver Sunset GT to Azul Vehicle Exchange. Stop and press E.';
    else if (a.type==='survival') { detail=a.phase==='prepare'?`Next wave in ${Math.ceil(a.timer)}s. Reload and find cover.`:`Wave ${a.wave} / 3. Clear the freight yard.`; extra=`${remaining} HOSTILES · STAY WITHIN THE FREIGHT YARD`; }
    else if (a.type==='heist') detail=({acquire:'Take the marked coral van near Azul Vehicle Exchange.',approach:'Drive the van to the guarded exchange.',guards:`Leave the van and clear ${remaining} guards around the case.`,case:'Approach the case on foot and press E.',escape:'Get back to the coral van and lose the patrols.',deliver:'Bring the case to the Palomar Hideaway in the marked van.'})[a.phase];
    else if (a.type==='bounty') detail=({search:'Find the fugitive hiding near County Plaza.',fight:`Subdue the fugitive and crew. ${remaining} still standing.`,capture:'Approach the subdued fugitive on foot and press E.',return:'Bring the fugitive to the Port Dispatch. Press E to hand over.'})[a.phase];
    else if (a.type==='range') { detail='Aim and shoot the six gold targets. R reloads; number keys change weapons.'; extra=`${a.total-remaining} / ${a.total} TARGETS · ${Math.ceil(a.timer)}s LEFT · GOLD UNDER 18s`; }
    return {category:def.category,title:def.name,detail,extra};
  }
  markers() {
    if (!this.active) return this.catalog.map(def=>({...this.startPoint(def),id:def.id,name:def.name,type:def.type,color:def.color,symbol:SYMBOLS[def.type],selected:this.selected===def.id}));
    const target=this.waypoint(),def=this.definition(this.active.id);
    return target?[{...target,id:this.active.id,type:this.active.type,color:def.color,symbol:SYMBOLS[def.type],active:true}]:[];
  }
  serialize() { return { records: Object.fromEntries(Object.entries(this.records).map(([id,record])=>[id,{...record}])) }; }
  state() {
    const a=this.active;
    return {selected:this.selected,active:a?{id:a.id,type:a.type,phase:a.phase,elapsed:+a.elapsed.toFixed(1),timer:+a.timer.toFixed(1),checkpoint:a.checkpoint??null,wave:a.wave??null,fare:a.fare??null,carId:a.carId,remaining:a.encounter?this.sim.combat?.remaining(a.encounter)??0:0}:null,records:this.serialize().records};
  }
}
