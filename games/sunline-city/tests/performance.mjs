import {chromium} from 'playwright';
const browser=await chromium.launch({headless:true,channel:'chrome'});
const page=await browser.newPage({viewport:{width:1440,height:900},deviceScaleFactor:2});
const errors=[];page.on('pageerror',e=>errors.push(e.message));
await page.goto('http://127.0.0.1:5174');await page.click('#start-btn');
const result=await page.evaluate(async()=>{
 const timings=[],start=performance.now();let last=start;
 await new Promise(resolve=>{const f=t=>{timings.push(t-last);last=t;if(t-start<5000)requestAnimationFrame(f);else resolve();};requestAnimationFrame(f);});timings.sort((a,b)=>a-b);
 const before=window.__SUNLINE__.sim.time;
 const t0=performance.now();window.advanceTime(180000);const cost=performance.now()-t0;
 const sim=window.__SUNLINE__.sim;
 return{frames:timings.length,fps:1000/(timings.reduce((a,b)=>a+b)/timings.length),median:timings[Math.floor(timings.length*.5)],p95:timings[Math.floor(timings.length*.95)],simulation180secMs:cost,timeAdvanced:sim.time-before,entities:{cars:sim.cars.length,people:sim.people.length,police:sim.police.length,particles:sim.particles.length,tracks:sim.tracks.length},stationaryTraffic:sim.cars.filter(c=>c.parked).length};
});console.log(JSON.stringify({result,errors},null,2));await page.screenshot({path:'output/performance.png'});await browser.close();
