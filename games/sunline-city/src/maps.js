export const escapeXML = value => String(value ?? '').replace(/[<>&"']/g, c => ({'<':'&lt;','>':'&gt;','&':'&amp;','"':'&quot;',"'":'&#39;'}[c]));
export const ACTIVITY_COLORS = {race:'#aa7846',taxi:'#b48c35',export:'#507c87',survival:'#a55d56',heist:'#8d657e',bounty:'#ad6950',range:'#648775'};
export const ACTIVITY_SYMBOLS = {race:'R',taxi:'T',export:'E',survival:'S',heist:'H',bounty:'B',range:'◎'};

export function activityMarkers(sim) {
  return (sim.activities?.markers() || []).map(m => ({...m,
    color:m.color || ACTIVITY_COLORS[m.type] || '#5b8f81',
    symbol:m.symbol || ACTIVITY_SYMBOLS[m.type] || '•'}));
}

export function mapSVG(world, sim, {full = false, region = 'all', filter = 'all'} = {}) {
  const p = sim.pos, story = sim.waypoint(), wp = sim.customWaypoint || story;
  const neighborhood=world.neighborhoods?.find(n=>n.id===region);
  const view = full && neighborhood ? [neighborhood.x-100,neighborhood.y-100,neighborhood.w+200,neighborhood.h+200] : full ? region === 'north' ? [20,40,3280,2800] : region === 'south' ? [250,2770,4850,2900] : [-20,0,world.width+40,world.height] : [p.x-550,p.y-410,1100,820];
  const [vx,vy,vw,vh] = view;
  const inView = (x,y,w=0,h=0) => x+w>vx-100 && x<vx+vw+100 && y+h>vy-100 && y<vy+vh+100;
  const polygons = world.mapLand || [[[0,0],[2660,0],[2660,2850],[0,2850]]];
  let svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="${view.join(' ')}" preserveAspectRatio="xMidYMid meet" role="img" aria-label="Sunline County activity map"><rect x="-4000" y="-4000" width="16000" height="16000" fill="#206d93"/>`;
  for (const points of polygons) svg += `<polygon points="${points.map(q=>q.join(',')).join(' ')}" fill="#abc39c" stroke="#e4d5ae" stroke-width="34"/>`;
  if(world.neighborhoods?.length){
    svg+=`<defs><clipPath id="land-clip">${polygons.map(points=>`<polygon points="${points.map(q=>q.join(',')).join(' ')}"/>`).join('')}</clipPath></defs><g clip-path="url(#land-clip)">`;
    for(const n of [...world.neighborhoods].sort((a,b)=>(a.id==='grove'?-1:b.id==='grove'?1:0)))svg+=`<rect x="${n.x}" y="${n.y}" width="${n.w}" height="${n.h}" fill="${n.color}" opacity=".4"/>`;
    svg+='</g>';
  }
  const roads=world.roadSegments || [...world.roads.vertical.map(x=>({x1:x,y1:60,x2:x,y2:2790})),...world.roads.horizontal.map(y=>({x1:65,y1:y,x2:2430,y2:y}))];
  for (const r of roads) {
    if (!inView(Math.min(r.x1,r.x2),Math.min(r.y1,r.y2),Math.abs(r.x2-r.x1),Math.abs(r.y2-r.y1))) continue;
    svg+=`<path d="M${r.x1} ${r.y1}L${r.x2} ${r.y2}" stroke="#f5edda" stroke-width="${(r.width||116)+28}"/><path d="M${r.x1} ${r.y1}L${r.x2} ${r.y2}" stroke="#9ba9a0" stroke-width="${(r.width||116)-8}"/>`;
  }
  if(world.raceTrack){const points=world.raceTrack.points;const path=points.map((p,i)=>`${i?'L':'M'}${p.x} ${p.y}`).join(' ')+'Z';svg+=`<path d="${path}" fill="none" stroke="#f5edda" stroke-width="${world.raceTrack.width+22}"/><path d="${path}" fill="none" stroke="#859b91" stroke-width="${world.raceTrack.width}"/>`;}
  for (const b of world.buildings) if(inView(b.x,b.y,b.w,b.h)) svg+=`<rect x="${b.x}" y="${b.y}" width="${b.w}" height="${b.h}" rx="8" fill="#6d9891"/>`;
  for (const pool of world.pools) if(inView(pool.x,pool.y,pool.w,pool.h)) svg+=pool.outline?`<polygon points="${pool.outline.map(p=>p.join(',')).join(' ')}" fill="#5d9d91"/>`:`<rect x="${pool.x}" y="${pool.y}" width="${pool.w}" height="${pool.h}" fill="#3caaac"/>`;
  if (full) {
    for (const d of world.neighborhoods || world.districts || []) svg+=`<text x="${d.x+d.w/2}" y="${d.y+d.h/2}" text-anchor="middle" fill="#315b59" opacity=".5" font-family="sans-serif" font-weight="700" font-size="${region==='all'?36:32}" letter-spacing="4">${escapeXML(d.name.toUpperCase())}</text>`;
    svg+=`<text x="3010" y="1450" transform="rotate(90,3010,1450)" fill="#b9d7cd" font-family="Georgia,serif" font-style="italic" font-size="72" letter-spacing="12">Atlantic Ocean</text>`;
  }
  if (filter === 'all' || filter === 'stunts') for (const r of world.stuntRamps || []) if(full||inView(r.x,r.y)) {
    const done=sim.county.jumps.has(r.id);
    svg+=`<path d="M${r.x-26} ${r.y+24}L${r.x+30} ${r.y+24}L${r.x+30} ${r.y-24}Z" fill="${done?'#78a980':'#bf8d67'}" stroke="#fff5d7" stroke-width="5"/>`;
  }
  if (filter === 'all' || filter === 'activities') for (const m of activityMarkers(sim)) if(full||inView(m.x,m.y)) {
    const r=full?(region==='all'?39:29):24;
    svg+=`<g data-activity="${escapeXML(m.id)}" style="cursor:pointer"><circle cx="${m.x}" cy="${m.y}" r="${r}" fill="${m.color}" stroke="#fff3d7" stroke-width="6"/><text x="${m.x}" y="${m.y+12}" fill="#fff6df" text-anchor="middle" font-family="sans-serif" font-size="${r}" font-weight="700">${escapeXML(m.symbol)}</text><title>${escapeXML(m.name)}</title></g>`;
  }
  if (filter === 'all' || filter === 'services') for (const po of world.pois.filter(po=>['garage','gunshop','safehouse','gunrange'].includes(po.key))) if(full||inView(po.x,po.y)) {
    const r=full?33:23, name=po.key==='gunshop'?'W':po.key==='gunrange'?'◎':'G';
    svg+=`<g data-service="${escapeXML(po.key)}"><rect x="${po.x-r}" y="${po.y-r}" width="${r*2}" height="${r*2}" rx="8" fill="#365d60" stroke="#fff1d1" stroke-width="6"/><text x="${po.x}" y="${po.y+11}" text-anchor="middle" fill="#fff6df" font-family="sans-serif" font-size="${r}" font-weight="700">${name}</text><title>${escapeXML(po.name)}</title></g>`;
  }
  if(filter==='explore'||filter==='all')for(const m of sim.exploration?.markers()||[]){
    if(!inView(m.x,m.y)||(!full&&Math.hypot(m.x-p.x,m.y-p.y)>340))continue;
    const r=full?(region==='all'?48:region==='north'||region==='south'?32:18):16,collected=m.type==='photo'&&m.collected,color=collected?'#668967':m.color||'#bb8b6b';
    svg+=`<g data-explore="${m.type}:${escapeXML(m.id)}" style="cursor:pointer"><rect x="${m.x-r}" y="${m.y-r}" width="${r*2}" height="${r*2}" rx="${m.type==='transit'?r:5}" fill="${color}" stroke="#fff5d8" stroke-width="4"/><text x="${m.x}" y="${m.y+r*.4}" font-size="${r*1.2}" font-family="sans-serif" font-weight="700" text-anchor="middle" fill="#fff6df">${collected?'✓':m.type==='transit'?'↔':m.type==='food'?'+':'C'}</text><title>${escapeXML(m.name)}</title></g>`;
  }
  if(wp) svg+=`<path d="M${p.x} ${p.y}L${wp.x} ${wp.y}" stroke="#f5cb6b" stroke-width="${full?12:8}" stroke-dasharray="20 18" opacity=".8"/><circle cx="${wp.x}" cy="${wp.y}" r="${full?35:27}" fill="#efbd62" stroke="#fff4d3" stroke-width="7"/>`;
  if(sim.customWaypoint&&story&&story!==sim.customWaypoint) svg+=`<circle cx="${story.x}" cy="${story.y}" r="24" fill="#f1bf62" stroke="#fff4d3" stroke-width="7"/>`;
  if(full && region!=='all') for (const po of world.pois.filter(po=>!['race','taxi','bounty','docks'].includes(po.key))) {
    svg+=`<text x="${po.x}" y="${po.y-52}" text-anchor="middle" fill="#244f51" stroke="#f3eed3" stroke-width="5" paint-order="stroke" font-family="sans-serif" font-size="40" font-weight="600">${escapeXML(po.name)}</text>`;
  }
  for(const c of sim.police) if(full||inView(c.x,c.y)) svg+=`<circle cx="${c.x}" cy="${c.y}" r="${full?23:20}" fill="#d87362" stroke="#fff6df" stroke-width="5"/>`;
  svg+=`<circle cx="${p.x}" cy="${p.y}" r="${full?32:27}" fill="#173e45" stroke="#fff8df" stroke-width="9"/><path transform="translate(${p.x} ${p.y}) rotate(${p.angle*180/Math.PI})" d="M55 0L35 -13L35 13Z" fill="#fff8df"/>`;
  return svg+'</svg>';
}

export function drawCountyMarkers(ctx, sim, visible) {
  for(const m of activityMarkers(sim)) {
    if(!visible(m)) continue;
    ctx.save();ctx.translate(m.x,m.y);
    ctx.strokeStyle=m.color;ctx.lineWidth=2;ctx.setLineDash([5,6]);ctx.beginPath();ctx.ellipse(0,0,25,14,0,0,Math.PI*2);ctx.stroke();ctx.setLineDash([]);
    ctx.fillStyle=m.color;ctx.beginPath();ctx.roundRect(-12,-43,24,24,4);ctx.fill();ctx.strokeStyle='#fff2d5';ctx.lineWidth=1.5;ctx.stroke();
    ctx.fillStyle='#fff6df';ctx.font='bold 13px sans-serif';ctx.textAlign='center';ctx.textBaseline='middle';ctx.fillText(m.symbol,0,-30);
    if(Math.hypot(sim.pos.x-m.x,sim.pos.y-m.y)<160){ctx.fillStyle='#183a41ed';ctx.font='600 10px sans-serif';const w=ctx.measureText(m.name).width+18;ctx.fillRect(-w/2,-66,w,18);ctx.fillStyle='#fff6df';ctx.fillText(m.name,0,-57);}
    ctx.restore();
  }
  for(const po of sim.world.pois.filter(p=>['gunshop','safehouse','garage','gunrange'].includes(p.key))) {
    if(!visible(po))continue;ctx.save();ctx.translate(po.x,po.y);ctx.fillStyle='#315e63';ctx.fillRect(-13,-43,26,25);ctx.strokeStyle='#fff1d1';ctx.strokeRect(-13,-43,26,25);ctx.fillStyle='#fff1d1';ctx.font='bold 13px sans-serif';ctx.textAlign='center';ctx.fillText(po.key==='gunshop'?'W':po.key==='gunrange'?'◎':'G',0,-25);ctx.restore();
  }
}
