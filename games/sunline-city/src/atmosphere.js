// Small, deterministic environmental movement; no entities or timers accumulate.
export function drawAtmosphere(ctx,sim,bounds,reducedMotion=false){
 const id=sim.world.neighborhoodAt?.(sim.pos.x,sim.pos.y)?.id;
 if(reducedMotion||!['ocean','cypress','hills','port'].includes(id))return;
 const t=sim.time;
 ctx.save();ctx.lineWidth=1.5;
 for(let i=0;i<7;i++){
  const x=bounds.x+((i*173+t*(id==='cypress'?4:10))%bounds.w),y=bounds.y+((i*139+Math.sin(t*.4+i)*15)%bounds.h);
  if(id==='cypress'||id==='hills'){ctx.globalAlpha=.45;ctx.fillStyle=id==='hills'?'#fff0b5':'#d0e7b5';ctx.beginPath();ctx.ellipse(x,y,2.5,1.2,Math.sin(t+i),0,Math.PI*2);ctx.fill();}
  else{ctx.globalAlpha=.7;ctx.strokeStyle='#fff7dd';ctx.beginPath();const wing=3+Math.sin(t*2+i)*2;ctx.moveTo(x-5,y-wing);ctx.quadraticCurveTo(x-2,y-3,x,y);ctx.quadraticCurveTo(x+2,y-3,x+5,y-wing);ctx.stroke();}
 }
 ctx.restore();
}
export function drawLighting(ctx,W,H,mood){
 if(mood==='afternoon'||!mood)return;
 ctx.save();ctx.fillStyle=mood==='golden'?'rgba(242,151,65,.10)':'rgba(34,49,112,.19)';ctx.fillRect(0,0,W,H);
 const glow=ctx.createLinearGradient(0,0,W,H);glow.addColorStop(0,mood==='golden'?'rgba(255,222,150,.12)':'rgba(202,151,208,.09)');glow.addColorStop(1,'rgba(0,0,0,0)');ctx.fillStyle=glow;ctx.fillRect(0,0,W,H);ctx.restore();
}
