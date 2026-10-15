import { chromium } from 'playwright';
const browser=await chromium.launch({headless:true,args:['--enable-unsafe-swiftshader']});
const page=await browser.newPage({viewport:{width:1440,height:900},deviceScaleFactor:1});
page.on('pageerror',e=>console.log(e.message));page.on('console',m=>{if(m.type()==='error')console.log(m.text());});
await page.goto('http://127.0.0.1:5174/?test=1');await page.waitForFunction(()=>window.__stillwater);await page.click('#start-btn');
await page.evaluate(()=>{window.advanceTime(3000);window.__stillwater.pauseAuto();});await page.screenshot({path:'test-results/play-settled.png'});
await page.evaluate(()=>{window.__stillwater.teleport(0,3);window.__stillwater.setCamera(.12,9);window.advanceTime(2000);window.__stillwater.pauseAuto();});await page.screenshot({path:'test-results/water-close.png'});
await page.evaluate(()=>{window.__stillwater.graphics.renderRaw();});await page.screenshot({path:'test-results/raw.png'});
await browser.close();
