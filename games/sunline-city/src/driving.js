/** Arcade handling with finite nitro and a drift chain grounded in actual distance. */
const clamp=(v,lo,hi)=>Math.max(lo,Math.min(hi,v));
const diff=(a,b)=>Math.atan2(Math.sin(a-b),Math.cos(a-b));
const finite=(value,fallback=0)=>Number.isFinite(value)?value:fallback;
export const DRIVING_TUNING=Object.freeze({maxCharge:4,rechargeTime:7,rechargeDelay:.45,acceleration:220,boostAcceleration:365,boostMultiplier:1.5,topSpeeds:Object.freeze({sport:410,sedan:350,van:280,police:365}),reverseSpeed:110});
const SURFACES={road:{speed:1,grip:1,acceleration:1},paved:{speed:.94,grip:1,acceleration:.96},dirt:{cap:265,grip:.82,acceleration:.86},grass:{cap:235,grip:.75,acceleration:.8},sand:{cap:195,grip:.65,acceleration:.68}};
export class DrivingSystem{
 constructor(sim,save=null){
  this.sim=sim;this.maxCharge=DRIVING_TUNING.maxCharge;this.charge=clamp(finite(save?.charge,this.maxCharge),0,this.maxCharge);this.boosting=false;this.recharging=false;this.exhausted=false;this.rechargeDelay=0;this.lastCar=null;this.terrain='road';this.drifting=false;this.chain=null;this.lastDrift=0;this.lastReward=0;this.lastDriftTimer=0;this.rewardCooldown=clamp(finite(save?.rewardCooldown),0,30);
  this.bestDrift=clamp(Math.floor(finite(save?.bestDrift)),0,10000000);this.totalDriftScore=clamp(Math.floor(finite(save?.totalDriftScore)),0,1000000000);this.driftsCompleted=clamp(Math.floor(finite(save?.driftsCompleted)),0,1000000);
 }
 serialize(){return{charge:this.charge,bestDrift:this.bestDrift,totalDriftScore:this.totalDriftScore,driftsCompleted:this.driftsCompleted,rewardCooldown:this.rewardCooldown};}
 state(){return{charge:+this.charge.toFixed(2),maxCharge:this.maxCharge,boosting:this.boosting,recharging:this.recharging,exhausted:this.exhausted,terrain:this.terrain,drifting:this.drifting,driftScore:Math.floor(this.chain?.score||0),driftMultiplier:this.chain?.multiplier||1,bestDrift:this.bestDrift,lastDrift:this.lastDriftTimer>0?this.lastDrift:0,lastReward:this.lastDriftTimer>0?this.lastReward:0,driftsCompleted:this.driftsCompleted};}
 surface(car){
  if(this.sim.isRoad(car))return{type:'road',...SURFACES.road};
  const value=this.sim.world.terrainAt?.(car.x,car.y);const type=typeof value==='string'?value:value?.type||value?.terrain;
  if(SURFACES[type])return{type,...SURFACES[type],...(value&&typeof value==='object'?{grip:clamp(finite(value.grip,SURFACES[type].grip),.45,1.2)}:{})};
  // Original coast has a broad beach strip. Other open lots keep useful traction.
  const fallback=car.x>2460&&car.x<2720&&car.y<2850?'sand':'grass';return{type:fallback,...SURFACES[fallback]};
 }
 update(dt,keys={}){
  if(this.sim.mode!=='play')return;dt=clamp(finite(dt),0,.25);this.rewardCooldown=Math.max(0,this.rewardCooldown-dt);this.lastDriftTimer=Math.max(0,this.lastDriftTimer-dt);
  const sim=this.sim,car=sim.vehicle,boostHeld=!!(keys.ShiftLeft||keys.ShiftRight||keys.Boost);
  if(!boostHeld&&this.charge>=.6)this.exhausted=false;
  if(!car){if(this.chain)this.settleDrift();this.boosting=false;this.drifting=false;this.lastCar=null;this.recharge(dt);return;}
  if(this.lastCar!==car.id){if(this.chain)this.settleDrift();car.motionAngle=car.angle;this.lastCar=car.id;}
  const tune=sim.county?.tuning(car)||{engine:0,handling:false},surface=this.surface(car);this.terrain=surface.type;
  const left=!!(keys.KeyA||keys.ArrowLeft),right=!!(keys.KeyD||keys.ArrowRight),up=!!(keys.KeyW||keys.ArrowUp),down=!!(keys.KeyS||keys.ArrowDown),steer=(right?1:0)-(left?1:0);let throttle=(up?1:0)-(down?1:0);
  car.braking=!!keys.Space;const wasBoosting=this.boosting;
  this.boosting=boostHeld&&!this.exhausted&&this.charge>0&&!car.braking&&throttle>=0&&car.speed>=0&&car.health>0;
  if(this.boosting){const available=Math.min(dt,this.charge);this.charge=Math.max(0,this.charge-available);this.rechargeDelay=DRIVING_TUNING.rechargeDelay;this.recharging=false;throttle=1;if(!wasBoosting)sim.emit('sfx',{name:'boost'});if(this.charge<=0)this.exhausted=true;}else this.recharge(dt);
  car.boosting=this.boosting;
  const base=(DRIVING_TUNING.topSpeeds[car.type]||350)*(1+tune.engine*.08),terrainTop=surface.cap?Math.min(base,surface.cap*(1+tune.engine*.05)):base*surface.speed;
  const top=terrainTop*(this.boosting?DRIVING_TUNING.boostMultiplier:1),opposite=throttle&&Math.sign(throttle)!==Math.sign(car.speed)&&Math.abs(car.speed)>12;
  const acceleration=opposite?560:DRIVING_TUNING.acceleration*(1+tune.engine*.12)*surface.acceleration+(this.boosting?DRIVING_TUNING.boostAcceleration:0);
  if(throttle<0||car.speed<top)car.speed+=throttle*acceleration*dt;
  if(!throttle)car.speed*=Math.exp(-.52*dt);
  const handbrakeTurn=car.braking&&steer&&car.speed>105;
  if(car.braking)car.speed*=Math.exp(-(handbrakeTurn?(tune.handling?.95:1.12):(tune.handling?4.3:3.8))*dt);
  // Overspeed settles smoothly when nitro ends or the car leaves pavement.
  if(car.speed>top)car.speed=Math.max(top,car.speed-225*dt);
  car.speed=Math.max(-DRIVING_TUNING.reverseSpeed,car.speed);if(Math.abs(car.speed)<1)car.speed=0;
  const highSpeedStability=clamp(1-(Math.abs(car.speed)-245)/1050,.6,1),turnRate=(tune.handling?2.4:2.12)*(handbrakeTurn?1.4:1)*surface.grip*highSpeedStability;
  car.angle+=steer*turnRate*clamp(Math.abs(car.speed)/65,0,1)*Math.sign(car.speed||1)*dt;
  car.motionAngle=finite(car.motionAngle,car.angle);const grip=(handbrakeTurn?4.2:13.5)*surface.grip*(tune.handling?1.15:1);
  car.motionAngle+=diff(car.angle,car.motionAngle)*Math.min(1,grip*dt);
  if(Math.abs(car.speed)<50)car.motionAngle=car.angle;
  const before={x:car.x,y:car.y},contact=this.moveCar(car,Math.cos(car.motionAngle)*car.speed*dt,Math.sin(car.motionAngle)*car.speed*dt);
  if(contact.wall){if(Math.abs(car.speed)>65&&sim.collisionCooldown<=0){car.health-=Math.abs(car.speed)*.055;sim.health-=Math.abs(car.speed)*.012;sim.emit('sfx',{name:'crash'});sim.sparks(car);sim.collisionCooldown=.5;}car.speed*=-.18;car.motionAngle=car.angle;this.crash();}
  const traveled=Math.hypot(car.x-before.x,car.y-before.y),slip=Math.abs(diff(car.angle,car.motionAngle));
  this.drifting=!!(handbrakeTurn&&car.speed>105&&slip>.08&&traveled>0&&!contact.wall&&!contact.car&&!sim.county?.airborne);
  if(this.drifting){if(!this.chain)this.chain={score:0,distance:0,duration:0,multiplier:1,idle:0,origin:before,maxDisplacement:0};const chain=this.chain;chain.duration+=dt;chain.distance+=traveled;chain.multiplier=Math.min(4,1+Math.floor(chain.duration/1.5));chain.score+=traveled*(.3+Math.min(.8,slip))*chain.multiplier;chain.idle=0;chain.maxDisplacement=Math.max(chain.maxDisplacement,Math.hypot(car.x-chain.origin.x,car.y-chain.origin.y));}
  else if(this.chain){this.chain.idle+=dt;if(this.chain.idle>=1.2)this.settleDrift();}
  if((this.drifting||car.braking)&&Math.abs(car.speed)>80){sim.tracks.push({x:car.x,y:car.y,angle:car.motionAngle,life:8});if(sim.tracks.length>160)sim.tracks.shift();}
  sim.player.x=car.x;sim.player.y=car.y;sim.player.angle=car.angle;
 }
 recharge(dt){this.rechargeDelay=Math.max(0,this.rechargeDelay-dt);this.recharging=this.charge<this.maxCharge&&this.rechargeDelay<=0;if(this.recharging)this.charge=Math.min(this.maxCharge,this.charge+this.maxCharge/DRIVING_TUNING.rechargeTime*dt);}
 moveCar(car,dx,dy){
  const steps=Math.max(1,Math.ceil(Math.hypot(dx,dy)/8));
  for(let i=0;i<steps;i++){
   if(this.sim.move(car,dx/steps,dy/steps,15))return{wall:true};
   // Stop within the existing traffic collision radius, even on a long frame.
   const other=[...this.sim.cars,...this.sim.police].find(c=>c!==car&&c.health>0&&Math.hypot(c.x-car.x,c.y-car.y)<30);
   if(other)return{wall:false,car:other};
  }
  return{wall:false};
 }
 settleDrift(){
  const chain=this.chain;this.chain=null;this.drifting=false;if(!chain||chain.distance<150||chain.maxDisplacement<95||chain.duration<.8||chain.score<65)return false;
  const score=Math.floor(chain.score);this.bestDrift=Math.max(this.bestDrift,score);this.totalDriftScore+=score;this.driftsCompleted++;this.lastDrift=score;this.lastDriftTimer=4;this.lastReward=0;
  if(this.rewardCooldown<=0){this.lastReward=Math.min(60,Math.floor(score/35));this.sim.cash+=this.lastReward;this.rewardCooldown=30;}
  this.sim.toast(`DRIFT BANKED · ${score.toLocaleString()} points${this.lastReward?` · +$${this.lastReward}`:''}`);this.sim.emit('sfx',{name:'driftBank'});this.sim.emit('save');return true;
 }
 crash(){if(this.chain){this.chain=null;this.lastDrift=0;this.lastReward=0;this.lastDriftTimer=0;}this.drifting=false;}
 reset(){this.crash();this.boosting=false;this.lastCar=null;}
}
