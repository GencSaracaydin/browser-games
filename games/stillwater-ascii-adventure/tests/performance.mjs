import {chromium} from 'playwright';
const browser=await chromium.launch({executablePath:'/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',headless:true});
const page=await browser.newPage({viewport:{width:1440,height:900}});
await page.goto('http://127.0.0.1:5174/?test=1');await page.waitForFunction(()=>window.__stillwater);await page.click('#start-btn');
const result=await page.evaluate(async()=>{
 const samples=[];let last=performance.now();
 for(let i=0;i<60;i++){await new Promise(requestAnimationFrame);const now=performance.now();samples.push(now-last);last=now;}
 const gl=document.querySelector('canvas').getContext('webgl2'),ext=gl.getExtension('WEBGL_debug_renderer_info');
 samples.sort((a,b)=>a-b);
 return {renderer:ext?gl.getParameter(ext.UNMASKED_RENDERER_WEBGL):'unknown',medianFrameMs:samples[30],p95FrameMs:samples[57],fps:1000/(samples.reduce((a,b)=>a+b)/samples.length)};
});
console.log(JSON.stringify(result));await browser.close();
