import {drawPerson} from './sprites.js';

export function drawActivityGround(ctx,sim) {
  const a=sim.activities.active;
  if(!a || a.type!=='race')return;
  const def=sim.activities.definition(a.id),p=sim.activities.waypoint();
  if(!p)return;
  const previous=a.checkpoint>0?{x:def.route[a.checkpoint-1][0],y:def.route[a.checkpoint-1][1]}:sim.activities.startPoint(def);
  const angle=Math.atan2(p.y-previous.y,p.x-previous.x);
  ctx.save();ctx.translate(p.x,p.y);ctx.rotate(angle);
  ctx.fillStyle='#f8d47b36';ctx.fillRect(-15,-78,30,156);
  for(let y=-76;y<76;y+=12){ctx.fillStyle=(y+76)%24?'#f4e8c5cc':'#476b66cc';ctx.fillRect(-4,y,8,12);}
  for(const y of [-81,81]){ctx.fillStyle='#243f47';ctx.fillRect(-3,y-5,6,13);ctx.fillStyle='#f7ce78';ctx.beginPath();ctx.moveTo(0,y-5);ctx.lineTo(-17,y-5);ctx.lineTo(-10,y-13);ctx.lineTo(0,y-13);ctx.fill();}
  ctx.restore();
}

export function activityDrawables(ctx,sim) {
  const a=sim.activities.active;
  if(!a)return [];
  const list=[];
  if(a.type==='taxi'&&a.phase==='pickup') {
    const p=a.pickup;
    list.push({y:p.y+8,draw:()=>{
      drawPerson(ctx,{x:p.x-15,y:p.y+8,angle:0,shirt:['#e7ba70','#b9a3c5','#69aaa3'][a.fare],skin:'#bd8868'},sim.time);
      ctx.save();ctx.translate(p.x-23,p.y+10);ctx.fillStyle='#a77658';ctx.fillRect(-3,-2,6,7);ctx.strokeStyle='#715f4b';ctx.lineWidth=1;ctx.strokeRect(-2,-5,4,3);ctx.restore();
    }});
  }
  if(a.type==='heist'&&['approach','guards','case'].includes(a.phase)) {
    const p=sim.activities.poi('heist');
    list.push({y:p.y,draw:()=>{
      ctx.save();ctx.translate(p.x,p.y);ctx.fillStyle='#173b4a33';ctx.beginPath();ctx.ellipse(4,3,17,6,0,0,Math.PI*2);ctx.fill();
      ctx.fillStyle='#bc8c6d';ctx.fillRect(-12,-10,24,15);ctx.fillStyle='#d9ad82';ctx.fillRect(-12,-10,24,4);ctx.strokeStyle='#755e50';ctx.lineWidth=2;ctx.strokeRect(-4,-15,8,5);ctx.fillStyle='#f2d79f';ctx.fillRect(-7,-6,3,5);ctx.fillRect(5,-6,3,5);ctx.restore();
    }});
  }
  return list;
}
