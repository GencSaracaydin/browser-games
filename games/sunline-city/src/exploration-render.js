// World-space markers. The county art supplies physical carts and shuttle stops.
const TAU=Math.PI*2;
const near=(a,b,r)=>Math.hypot(a.x-b.x,a.y-b.y)<r;
function camera(ctx,collected){
  ctx.strokeStyle=collected?'#c7d9bd':'#fff4d7';ctx.fillStyle=ctx.strokeStyle;ctx.lineWidth=1.5;
  ctx.strokeRect(-7,-4,14,9);ctx.fillRect(-4,-7,5,3);ctx.beginPath();ctx.arc(0,.5,2.6,0,TAU);ctx.stroke();
}
function bus(ctx){
  ctx.strokeStyle='#fff4d7';ctx.fillStyle='#fff4d7';ctx.lineWidth=1.4;ctx.strokeRect(-6,-7,12,12);ctx.strokeRect(-4,-5,8,5);ctx.fillRect(-4,2,2,2);ctx.fillRect(2,2,2,2);ctx.fillRect(-5,5,2,2);ctx.fillRect(3,5,2,2);
}
function food(ctx){
  ctx.strokeStyle='#fff4d7';ctx.lineWidth=1.5;ctx.beginPath();ctx.moveTo(-4,-7);ctx.lineTo(-4,7);ctx.moveTo(-7,-7);ctx.lineTo(-7,-2);ctx.lineTo(-1,-2);ctx.lineTo(-1,-7);ctx.moveTo(4,7);ctx.lineTo(4,-7);ctx.quadraticCurveTo(9,-4,4,0);ctx.stroke();
}
export function drawExploration(ctx,sim,bounds=null,visible=null){
  if(!sim.exploration||sim.mode==='intro')return;
  if(typeof bounds==='function'){visible=bounds;bounds=null;}
  const inView=visible||((p)=>!bounds||p.x>bounds.x-80&&p.y>bounds.y-80&&p.x<bounds.x+bounds.w+80&&p.y<bounds.y+bounds.h+80);
  for(const marker of sim.exploration.markers()){
    if(!inView(marker))continue;
    const collected=marker.type==='photo'&&marker.collected;
    const close=near(sim.pos,marker,145);
    ctx.save();ctx.translate(marker.x,marker.y);
    ctx.globalAlpha=collected?.67:marker.type==='transit'&&!marker.visited?.7:1;
    ctx.strokeStyle=collected?'#789d7c':marker.color||'#5a8381';ctx.lineWidth=1.5;
    if(close){ctx.setLineDash([3,5]);ctx.beginPath();ctx.ellipse(0,0,20,10,0,0,TAU);ctx.stroke();ctx.setLineDash([]);}
    ctx.fillStyle=collected?'#5b816c':marker.color||'#5a8381';ctx.beginPath();ctx.roundRect(-12,-42,24,24,4);ctx.fill();ctx.strokeStyle='#fff3d4';ctx.lineWidth=1.2;ctx.stroke();
    ctx.save();ctx.translate(0,-30);if(marker.type==='photo')camera(ctx,collected);else if(marker.type==='transit')bus(ctx);else food(ctx);ctx.restore();
    if(collected){ctx.fillStyle='#f7efd4';ctx.font='bold 9px sans-serif';ctx.textAlign='center';ctx.fillText('✓',14,-16);}
    if(close){
      const text=marker.type==='photo'?`${collected?'✓':'C'} · ${marker.name}`:marker.type==='food'?`${marker.name} · $${marker.price}`:`${marker.name} shuttle`;
      ctx.globalAlpha=1;ctx.font='600 9px sans-serif';ctx.textAlign='center';ctx.textBaseline='middle';const width=ctx.measureText(text).width+14;
      // Food labels sit below the point; camera and shuttle labels stay above it.
      const y=marker.type==='food'?21:-56;ctx.fillStyle='#24474bea';ctx.beginPath();ctx.roundRect(-width/2,y-8,width,16,3);ctx.fill();ctx.fillStyle='#fff4d8';ctx.fillText(text,0,y);
    }
    ctx.restore();
  }
}
export const drawExplorationMarkers=drawExploration;
