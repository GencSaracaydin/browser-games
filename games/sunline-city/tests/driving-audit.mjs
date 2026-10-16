import { chromium } from 'playwright';
import { mkdir, writeFile } from 'node:fs/promises';
const out = 'tests/outputs/driving-audit';
await mkdir(out,{recursive:true});
const browser = await chromium.launch({headless:true});
const page = await browser.newPage({viewport:{width:1440,height:900}});
const errors=[];page.on('pageerror',e=>errors.push(e.message));
await page.goto('http://127.0.0.1:5174');
await page.click('#start-btn');
const held=new Set();
async function keys(list){for(const k of [...held])if(!list.includes(k)){await page.keyboard.up(k);held.delete(k)}for(const k of list)if(!held.has(k)){await page.keyboard.down(k);held.add(k)}}
async function state(){return JSON.parse(await page.evaluate(()=>window.render_game_to_text()))}
async function advance(ms=100){return JSON.parse(await page.evaluate(ms=>window.advanceTime(ms),ms))}
const trace=[];
async function checkpoint(label){const s=await state();trace.push({label,...s});console.log(label,JSON.stringify({time:s.time,p:s.player,v:s.vehicle,wanted:s.wanted,escape:s.escapeSeconds,mission:s.mission}));await page.screenshot({path:`${out}/${label}.png`});return s;}
async function drive(route,{maxSteps=2000,maxSpeed=180,turnSpeed=90,stopAtEnd=true,avoidTraffic=false}={}){
 let index=0,s=await state(),passing=null;
 for(let step=0;step<maxSteps;step++){
  if(!s.vehicle) return {ok:false,reason:'no car',index,state:s};
  if(step>0 && s.wanted===0 && stopAtEnd==='escape'){await keys(['Space']);s=await advance(1200);await keys([]);return {ok:true,escaped:true,index,state:s};}
  const p=s.player,target=route[index];let d=Math.hypot(p.x-target.x,p.y-target.y);
  const threshold=index===route.length-1?25:45;
  if(d<threshold){index++;if(index===route.length)break;}
  const dest=route[index];let aim={...dest};
  const routeAngle=Math.atan2(dest.y-p.y,dest.x-p.x),aligned=Math.abs(Math.atan2(Math.sin(routeAngle-p.angle),Math.cos(routeAngle-p.angle)))<.35;
  if(avoidTraffic&&aligned&&Math.hypot(dest.x-p.x,dest.y-p.y)>180){
    const horiz=Math.abs(Math.cos(routeAngle))>.8;
    const vert=Math.abs(Math.sin(routeAngle))>.8;
    if(horiz||vert){
      if(passing){const car=s.nearbyCars.find(c=>c.id===passing.id);if(!car||(car.x-p.x)*Math.cos(routeAngle)+(car.y-p.y)*Math.sin(routeAngle)<-50)passing=null;}
      if(!passing){const candidates=s.nearbyCars.filter(c=>c.id!==s.vehicle.id&&c.type!=='police').map(c=>({...c,forward:(c.x-p.x)*Math.cos(routeAngle)+(c.y-p.y)*Math.sin(routeAngle),side:Math.abs(-(c.x-p.x)*Math.sin(routeAngle)+(c.y-p.y)*Math.cos(routeAngle))})).filter(c=>c.forward>0&&c.forward<190&&c.side<34).sort((a,b)=>a.forward-b.forward);if(candidates.length){const car=candidates[0];const centers=horiz?[480,1160,1840,2520]:[480,1080,1680,2320];const coordinate=horiz?car.y:car.x;const center=centers.reduce((a,b)=>Math.abs(a-coordinate)<Math.abs(b-coordinate)?a:b);passing={id:car.id,lateral:center+(coordinate>center?-30:30),horiz};}}
      if(passing){if(passing.horiz)aim={x:p.x+Math.cos(routeAngle)*150,y:passing.lateral};else aim={x:passing.lateral,y:p.y+Math.sin(routeAngle)*150};}
    }
  }
  const delta=Math.atan2(aim.y-p.y,aim.x-p.x)-p.angle;const angle=Math.atan2(Math.sin(delta),Math.cos(delta));
  let want=maxSpeed;
  if(Math.abs(angle)>.3)want=turnSpeed;
  if(Math.abs(angle)>.8)want=55;
  if(index===route.length-1)want=Math.min(want,Math.max(40,Math.hypot(dest.x-p.x,dest.y-p.y)*.9));
  if(index<route.length-1&&Math.hypot(dest.x-p.x,dest.y-p.y)<105)want=Math.min(want,turnSpeed);
  const controls=[];
  if(angle>.045)controls.push('d');else if(angle<-.045)controls.push('a');
  if(s.vehicle.speed<want-7)controls.push('w');else if(s.vehicle.speed>want+10)controls.push('s');
  await keys(controls);s=await advance(100);
  if(step%20===0)trace.push({routeIndex:index,step,...s});
 }
 await keys(stopAtEnd?['Space']:[]);
 if(stopAtEnd)s=await advance(1300);
 await keys([]);
 return {ok:index===route.length,index,state:s};
}
await page.keyboard.press('e');await page.keyboard.press('e');await advance(20);
await checkpoint('01-in-starter');
const firstDrive=await drive([{x:2300,y:1132},{x:2340,y:1200},{x:2340,y:2045},{x:2430,y:2085}],{maxSteps:500,maxSpeed:170});
console.log('firstDrive',JSON.stringify(firstDrive));await checkpoint('02-marina-arrival');
await page.keyboard.press('e');await advance(20);await checkpoint('03-handoff');
// Fresh scenario: real gunfire creates heat. Movement and vehicle interaction use keyboard input only.
await page.goto('http://127.0.0.1:5174');
await page.evaluate(()=>localStorage.clear());await page.reload();await page.click('#start-btn');
await page.keyboard.press('e');await page.keyboard.down('Space');await advance(20);await page.keyboard.up('Space');await page.keyboard.press('e');
for(let i=0;i<8;i++){const s=await advance(500);if(s.policeSeeingPlayer)break;}
const pursuitStart=await checkpoint('04-live-pursuit');
const pursuitRoute=[{x:2270,y:1132},{x:2348,y:1050},{x:2348,y:600},{x:2250,y:452},{x:600,y:452},{x:452,y:550},{x:452,y:2390},{x:560,y:2550},{x:2230,y:2550},{x:2370,y:2410},{x:2370,y:600},{x:2250,y:452},{x:600,y:452},{x:452,y:550},{x:452,y:1750},{x:550,y:1868},{x:1600,y:1868},{x:1708,y:1760},{x:1708,y:1250},{x:1800,y:1188}];
const pursuitDrive=await drive(pursuitRoute,{maxSteps:1400,maxSpeed:320,turnSpeed:110,stopAtEnd:'escape',avoidTraffic:true});
const pursuitEnd=await checkpoint('05-pursuit-end');
const traffic=await page.evaluate(()=>{
 const sim=window.__SUNLINE__.sim;const overlaps=[];
 for(let i=0;i<sim.cars.length;i++)for(let j=i+1;j<sim.cars.length;j++){const a=sim.cars[i],b=sim.cars[j],d=Math.hypot(a.x-b.x,a.y-b.y);if(d<30)overlaps.push({a:a.id,b:b.id,d,aParked:a.parked,bParked:b.parked,aSpeed:a.speed,bSpeed:b.speed})}
 return {moving:sim.cars.filter(c=>!c.parked).length,overlaps,police:sim.police.map(c=>({x:c.x,y:c.y,path:c.path,speed:c.speed}))};
});
await page.goto('http://127.0.0.1:5174');await page.evaluate(()=>localStorage.clear());await page.reload();await page.click('#start-btn');
const sustainedTraffic=process.env.SKIP_TRAFFIC?null:await page.evaluate(()=>{
 const samples=[],overlaps=[],perPair=new Map();let minimumGap=Infinity;
 for(let i=0;i<720;i++){
  window.advanceTime(250);const sim=window.__SUNLINE__.sim;
  for(let a=0;a<sim.cars.length;a++)for(let b=a+1;b<sim.cars.length;b++){
   const ca=sim.cars[a],cb=sim.cars[b];if(ca.parked&&cb.parked)continue;
   const gap=Math.hypot(ca.x-cb.x,ca.y-cb.y);minimumGap=Math.min(minimumGap,gap);
   if(gap<29){const key=ca.id+'/'+cb.id;perPair.set(key,(perPair.get(key)||0)+1);if(overlaps.length<30)overlaps.push({time:sim.time,gap,a:ca.id,b:cb.id,x:ca.x,y:ca.y,speedA:ca.speed,speedB:cb.speed});}
  }
  if(i%40===0)samples.push({time:sim.time,moving:sim.cars.filter(c=>!c.parked).length,flowing:sim.cars.filter(c=>!c.parked&&c.speed>10).length});
 }
 return{seconds:180,minimumGap,samples,overlaps,persistentPairs:[...perPair.entries()]};
});
if(sustainedTraffic){console.log('180sec traffic',JSON.stringify(sustainedTraffic));await page.screenshot({path:`${out}/06-traffic-180sec.png`});}else console.log('180sec traffic skipped; use default run for sustained flow audit.');
await writeFile(`${out}/result-starter-escape.json`,JSON.stringify({firstDrive,pursuitStart,pursuitDrive,pursuitEnd,traffic,sustainedTraffic,trace,errors},null,2));
await browser.close();
const passed=firstDrive.ok&&trace.some(v=>v.label==='03-handoff'&&v.completed===1)&&pursuitDrive.escaped&&errors.length===0&&(!sustainedTraffic||sustainedTraffic.samples.every(s=>s.flowing>=12)&&sustainedTraffic.minimumGap>25);
if(!passed)process.exitCode=1;
