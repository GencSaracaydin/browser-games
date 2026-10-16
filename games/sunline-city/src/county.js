const distance = (a, b) => Math.hypot(a.x - b.x, a.y - b.y);
const clamp = (v, low, high) => Math.max(low, Math.min(high, v));
export const PAINTS = ['#ef987d', '#78b7b5', '#edc86f', '#b4a7c9', '#f3e7cc', '#315e6c'];

// Permanent county progression is deliberately separate from temporary challenges.
export class CountySystem {
  constructor(sim, save = {}) {
    this.sim = sim;
    this.jumps = new Set((Array.isArray(save?.jumps) ? save.jumps : []).filter(id => sim.world.stuntRamps?.some(r => r.id === id)));
    this.bestJump = Number.isFinite(save?.bestJump) ? clamp(save.bestJump, 0, 1000) : 0;
    this.tunes = {};
    for (const type of ['sport', 'sedan', 'van', 'police']) {
      const data = save?.tunes?.[type];
      this.tunes[type] = {engine: clamp(Math.floor(Number(data?.engine) || 0), 0, 2), handling: !!data?.handling,
        paint: PAINTS.includes(data?.paint) ? data.paint : null};
    }
    this.airborne = null;
    this.rampCooldown = 0;
  }

  tuning(car = this.sim.vehicle) { return this.tunes[car?.type] || {engine: 0, handling: false}; }
  workshop() {
    return this.sim.world.pois.find(p => ['garage', 'safehouse'].includes(p.key || p.id) && distance(p, this.sim.pos) < 110);
  }
  shop() { return this.sim.world.pois.find(p => p.key === 'gunshop' && distance(p, this.sim.pos) < 105); }
  applyPaint(car) { const color = this.tuning(car).paint; if (color && car.type !== 'police') car.color = color; }
  purchase(kind, color) {
    const s = this.sim;
    if (!this.workshop() || !s.vehicle || Math.abs(s.vehicle.speed) > 25 || s.wanted) {
      s.toast('Bring a stopped car to a workshop with no police on your tail.'); return false;
    }
    const tune = this.tuning(), price = kind === 'engine' ? 500 + tune.engine * 400 : kind === 'handling' ? 450 : kind === 'repair' ? 100 : 120;
    if (kind === 'engine' && tune.engine >= 2 || kind === 'handling' && tune.handling) return false;
    if (kind === 'paint' && (!PAINTS.includes(color) || s.vehicle.type === 'police')) return false;
    if (!['engine', 'handling', 'repair', 'paint'].includes(kind)) return false;
    if (s.cash < price) { s.toast(`You need $${price} for this upgrade.`); return false; }
    s.cash -= price;
    if (kind === 'engine') tune.engine++;
    if (kind === 'handling') tune.handling = true;
    if (kind === 'paint') { tune.paint = color; this.applyPaint(s.vehicle); }
    if (kind === 'repair') { s.vehicle.health = 100; s.health = 100; }
    s.emit('save'); s.emit('sfx', {name: 'coin'});
    s.toast(kind === 'repair' ? 'Fresh paint, fresh start. Car and health restored.' : 'Looking good. Upgrades saved for this vehicle model.');
    return true;
  }

  update(dt) {
    this.rampCooldown = Math.max(0, this.rampCooldown - dt);
    const s = this.sim, car = s.vehicle;
    if (this.airborne) {
      const jump = this.airborne;
      jump.elapsed += dt;
      if (!car || car.id !== jump.car || jump.elapsed >= jump.duration) {
        const traveled = car ? distance(car, jump.start) : 0;
        const meters = Math.round(traveled / 12);
        const success = car && traveled >= 110 && car.health >= jump.health - 3 && !s.world.isSolid(car.x, car.y, 16);
        this.airborne = null; this.rampCooldown = 2;
        if (success) {
          const first = !this.jumps.has(jump.id);
          this.jumps.add(jump.id); this.bestJump = Math.max(this.bestJump, meters);
          if (first) s.cash += 300;
          s.toast(`STUNT LANDED · ${meters} m${first ? ' · +$300' : ' · already cleared'} · ${this.jumps.size}/${s.world.stuntRamps.length} ramps`);
          s.emit('sfx', {name: 'complete'}); s.emit('save');
        } else s.toast('Rough landing. Hit the ramp straight and keep your speed up.');
      }
      return;
    }
    if (!car || this.rampCooldown || car.speed < 145) return;
    for (const ramp of s.world.stuntRamps || []) {
      const angle = Math.atan2(Math.sin(car.angle - ramp.angle), Math.cos(car.angle - ramp.angle));
      if (distance(car, ramp) < 35 && Math.abs(angle) < .5) {
        this.airborne = {id: ramp.id, car: car.id, start: {x: car.x, y: car.y}, elapsed: 0, duration: 1.05,
          health: car.health, angle: car.angle};
        s.emit('sfx', {name: 'jump'}); s.toast(`${ramp.name || 'STUNT JUMP'} · hold the landing`); break;
      }
    }
  }
  get altitude() { return this.airborne ? Math.sin(clamp(this.airborne.elapsed / this.airborne.duration, 0, 1) * Math.PI) * 42 : 0; }
  serialize() { return {jumps: [...this.jumps], bestJump: this.bestJump, tunes: this.tunes}; }
  state() { return {jumps: this.jumps.size, totalJumps: this.sim.world.stuntRamps?.length || 0, bestJump: this.bestJump,
    airborne: this.airborne ? {name: this.airborne.id, altitude: Math.round(this.altitude)} : null,
    tuning: this.tuning(), workshopNearby: !!this.workshop(), shopNearby: !!this.shop()}; }
}
