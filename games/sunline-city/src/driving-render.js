/** Stateless world-space exhaust and tire haze; nothing accumulates between frames. */
export function drawDrivingEffects(ctx,sim,{reducedMotion=false}={}){
 const car=sim.vehicle,driving=sim.driving;if(!car||!driving)return;
 if(driving.boosting){
  ctx.save();ctx.translate(car.x,car.y);ctx.rotate(car.angle);const length=reducedMotion?15:17+Math.sin(sim.time*37)*3;
  for(const y of [-5,5]){ctx.fillStyle='#7fe8efbb';ctx.beginPath();ctx.moveTo(-21,y-3);ctx.lineTo(-26-length,y);ctx.lineTo(-21,y+3);ctx.closePath();ctx.fill();ctx.fillStyle='#fff3c9';ctx.beginPath();ctx.moveTo(-21,y-1.4);ctx.lineTo(-25-length*.62,y);ctx.lineTo(-21,y+1.4);ctx.closePath();ctx.fill();}
  if(!reducedMotion){ctx.strokeStyle='#d5f5ed70';ctx.lineWidth=1.2;for(let i=0;i<3;i++){const progress=(sim.time*2.7+i/3)%1,x=-38-progress*48,y=(i-1)*15;ctx.globalAlpha=(1-progress)*.6;ctx.beginPath();ctx.moveTo(x,y);ctx.lineTo(x-13,y);ctx.stroke();}}
  ctx.restore();
 }
 if(driving.drifting){ctx.save();for(let i=0;i<3;i++){const progress=reducedMotion?(i+1)/4:(sim.time*2+i/3)%1,back=18+progress*26,x=car.x-Math.cos(car.motionAngle)*back,y=car.y-Math.sin(car.motionAngle)*back;ctx.globalAlpha=(1-progress)*.2;ctx.fillStyle=driving.terrain==='sand'?'#e7d5ad':'#e9e6d3';ctx.beginPath();ctx.ellipse(x,y,4+progress*7,3+progress*5,car.motionAngle,0,Math.PI*2);ctx.fill();}ctx.restore();}
}
