import {drawPerson} from './sprites.js';
const TAU=Math.PI*2;
function circle(ctx,x,y,r,color){ctx.beginPath();ctx.arc(x,y,r,0,TAU);ctx.fillStyle=color;ctx.fill();}

/** Draw before people/buildings. Telegraphs lock to the position an enemy is aiming at. */
export function drawCombatGround(ctx,combat){
 ctx.save();
 for(const e of combat.enemies){
  if(e.health<=0||!e.hostile||e.windup<=0||!e.lockedAim)continue;
  const progress=1-e.windup/e.windupDuration;
  ctx.strokeStyle=`rgba(224,94,77,${.2+progress*.35})`;ctx.lineWidth=1.5;ctx.setLineDash([5,5]);ctx.beginPath();ctx.moveTo(e.x,e.y);ctx.lineTo(e.lockedAim.x,e.lockedAim.y);ctx.stroke();ctx.setLineDash([]);
  ctx.strokeStyle='#f6a18b';ctx.lineWidth=2;ctx.beginPath();ctx.arc(e.lockedAim.x,e.lockedAim.y,14-progress*7,0,TAU);ctx.stroke();
  ctx.globalAlpha=.12+progress*.1;circle(ctx,e.x,e.y,15,'#e98671');ctx.globalAlpha=1;
 }
 for(const effect of combat.effects){if(effect.kind!=='dodge')continue;ctx.globalAlpha=effect.life/effect.maxLife*.28;ctx.fillStyle=effect.color;ctx.beginPath();ctx.ellipse(effect.x,effect.y,7,11,0,0,TAU);ctx.fill();}
 ctx.restore();
}

function drawTarget(ctx,e){
 ctx.save();ctx.translate(e.x,e.y);
 ctx.fillStyle='#3c575b33';ctx.beginPath();ctx.ellipse(3,4,15,5,0,0,TAU);ctx.fill();
 ctx.fillStyle='#bc9874';ctx.fillRect(-2,-18,4,22);ctx.fillRect(-9,2,18,3);
 ctx.fillStyle='#f0d7ac';ctx.strokeStyle='#a77558';ctx.lineWidth=1.3;ctx.beginPath();ctx.roundRect(-11,-31,22,26,5);ctx.fill();ctx.stroke();
 for(const [r,color]of [[8,'#d88976'],[5,'#f8eccb'],[2,'#d88976']])circle(ctx,0,-18,r,color);
 if(e.hitFlash>0){ctx.globalAlpha=.75;circle(ctx,0,-18,10,'#fff8df');}
 ctx.restore();
}
function drawCombatant(ctx,e,time){
 ctx.save();
 if(e.health<=0){
  // Defeated people retreat; a small marker remains for capture objectives.
  ctx.globalAlpha=e.tag==='bounty'?.9:Math.max(.2,1-e.defeatedTime/3);
  ctx.strokeStyle='#f9e3a5';ctx.lineWidth=1.8;ctx.beginPath();ctx.ellipse(e.x,e.y,14,7,0,0,TAU);ctx.stroke();
  ctx.beginPath();ctx.moveTo(e.x-4,e.y-3);ctx.lineTo(e.x-1,e.y);ctx.lineTo(e.x+5,e.y-7);ctx.stroke();ctx.restore();return;
 }
 if(e.kind==='target')drawTarget(ctx,e);
 else{
  drawPerson(ctx,{...e,armed:true,shirt:e.hitFlash>0?'#fff4d3':e.shirt},time);
  const warning=e.windup>0;ctx.fillStyle=warning?'#ffe4a6':'#ca7167';ctx.strokeStyle='#fdf1d0';ctx.lineWidth=.7;
  ctx.beginPath();ctx.moveTo(e.x,e.y-18);ctx.lineTo(e.x-4,e.y-24);ctx.lineTo(e.x+4,e.y-24);ctx.closePath();ctx.fill();ctx.stroke();
  if(warning){circle(ctx,e.x+9,e.y-16,4,'#c9685c');ctx.fillStyle='#fff3d2';ctx.font='bold 7px sans-serif';ctx.textAlign='center';ctx.fillText('!',e.x+9,e.y-13.5);}
 }
 if(e.health<e.maxHealth||e.hostile){const y=e.y-(e.kind==='target'?37:30);ctx.fillStyle='#244a50bb';ctx.fillRect(e.x-13,y,26,3);ctx.fillStyle=e.hostile?'#edb08a':'#a4dbcf';ctx.fillRect(e.x-13,y,26*Math.max(0,e.health/e.maxHealth),3);}
 ctx.restore();
}

/** Append these to the main painter-sorted list, then sort by y as usual. */
export function combatDrawables(ctx,combat,time=combat.sim.time,bounds=null){
 return combat.enemies.filter(e=>!bounds||(e.x>bounds.x-40&&e.x<bounds.x+bounds.w+40&&e.y>bounds.y-40&&e.y<bounds.y+bounds.h+40)).map(e=>({y:e.y,draw:()=>drawCombatant(ctx,e,time)}));
}

/** World-space projectiles, muzzle flashes, reload ring and aiming reticle. */
export function drawCombatEffects(ctx,combat,aim=null,{reducedMotion=false}={}){
 const sim=combat.sim;ctx.save();
 for(const b of combat.bullets){ctx.strokeStyle=b.color;ctx.lineWidth=b.owner==='enemy'?2.8:2;ctx.lineCap='round';ctx.beginPath();ctx.moveTo(b.x,b.y);ctx.lineTo(b.x-b.vx*.018,b.y-b.vy*.018);ctx.stroke();circle(ctx,b.x,b.y,b.owner==='enemy'?2.8:1.5,b.owner==='enemy'?'#fff0ce':'#fff7da');}
 for(const e of combat.effects){if(e.kind==='dodge')continue;ctx.globalAlpha=Math.max(0,e.life/e.maxLife);ctx.strokeStyle=e.color;ctx.lineWidth=1.6;
  if(e.kind==='muzzle'){circle(ctx,e.x,e.y,5,e.color);continue;}
  const expand=(1-e.life/e.maxLife)*7;for(let i=0;i<4;i++){const a=i*Math.PI/2+.5;ctx.beginPath();ctx.moveTo(e.x+Math.cos(a)*(2+expand),e.y+Math.sin(a)*(2+expand));ctx.lineTo(e.x+Math.cos(a)*(5+expand),e.y+Math.sin(a)*(5+expand));ctx.stroke();}
 }
 ctx.globalAlpha=1;
 if(!sim.vehicle&&combat.muzzle>0){const p=sim.player,a=p.angle,x=p.x+Math.cos(a)*19,y=p.y+Math.sin(a)*19;ctx.save();ctx.translate(x,y-6);ctx.rotate(a);ctx.fillStyle=combat.weapon.color;ctx.beginPath();ctx.moveTo(9,0);ctx.lineTo(1,3);ctx.lineTo(-2,6);ctx.lineTo(-3,1);ctx.lineTo(-2,-5);ctx.lineTo(2,-2);ctx.closePath();ctx.fill();ctx.restore();}
 if(!sim.vehicle&&combat.reloadRemaining>0){const p=sim.player;ctx.lineWidth=2.2;ctx.strokeStyle='#264f5980';ctx.beginPath();ctx.arc(p.x,p.y,18,0,TAU);ctx.stroke();ctx.strokeStyle='#fff2bd';ctx.beginPath();ctx.arc(p.x,p.y,18,-Math.PI/2,-Math.PI/2+TAU*combat.reloadProgress);ctx.stroke();}
 if(combat.damageFlash>0){ctx.strokeStyle='#df7667';ctx.globalAlpha=combat.damageFlash/.32;ctx.lineWidth=2;ctx.beginPath();ctx.arc(sim.pos.x,sim.pos.y,24,0,TAU);ctx.stroke();ctx.globalAlpha=1;}
 if(!sim.vehicle&&aim&&sim.mode==='play'){
  const target=combat.enemies.find(e=>e.health>0&&Math.hypot(e.x-aim.x,e.y-aim.y)<20),spread=6+(reducedMotion?0:combat.recoil*9),color=target?'#f6b295':'#fff3cc';ctx.lineCap='round';
  for(const [width,stroke]of [[3.5,'#214955cc'],[1.5,color]]){ctx.lineWidth=width;ctx.strokeStyle=stroke;for(let i=0;i<4;i++){const a=i*Math.PI/2;ctx.beginPath();ctx.moveTo(aim.x+Math.cos(a)*spread,aim.y+Math.sin(a)*spread);ctx.lineTo(aim.x+Math.cos(a)*(spread+5),aim.y+Math.sin(a)*(spread+5));ctx.stroke();}}
  circle(ctx,aim.x,aim.y,1.3,color);
  if(combat.hitMarker>0){ctx.strokeStyle=combat.hitKill?'#f7cb79':'#fff5d4';ctx.lineWidth=2;for(let i=0;i<4;i++){const a=Math.PI/4+i*Math.PI/2;ctx.beginPath();ctx.moveTo(aim.x+Math.cos(a)*4,aim.y+Math.sin(a)*4);ctx.lineTo(aim.x+Math.cos(a)*9,aim.y+Math.sin(a)*9);ctx.stroke();}}
 }
 ctx.restore();
}
