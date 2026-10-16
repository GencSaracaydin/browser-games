import { chromium } from 'playwright';
import fs from 'node:fs/promises';
const browser=await chromium.launch({headless:true});
const page=await browser.newPage({viewport:{width:1440,height:900},deviceScaleFactor:1});
const errors=[];page.on('pageerror',e=>errors.push(e.message));page.on('console',e=>{if(e.type()==='error')errors.push(e.text())});
await page.goto('http://127.0.0.1:5174');await page.waitForTimeout(600);await fs.mkdir('output',{recursive:true});await page.screenshot({path:'output/intro.png'});await page.click('#start-btn');await page.waitForTimeout(200);await page.screenshot({path:'output/gameplay.png'});console.log(await page.evaluate(()=>window.render_game_to_text()));console.log({errors});await browser.close();
