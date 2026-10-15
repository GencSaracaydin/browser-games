import './style.css';
import * as THREE from 'three';
import { createWorld } from './world.js';
import { createCharacter, createEffects } from './character.js';
import { createRenderer, createWater } from './renderer.js';
import { createAudio } from './audio.js';

const $ = (selector) => document.querySelector(selector);
const show = (selector,visible=true) => $(selector).classList.toggle('hidden',!visible);
const SAVE_KEY='stillwater-journey-v1', SETTINGS_KEY='stillwater-settings-v1';
const readStorage=(key)=>{try{return JSON.parse(localStorage.getItem(key)||'null');}catch{return null;}};
const writeStorage=(key,value)=>{try{localStorage.setItem(key,JSON.stringify(value));}catch{/* Play remains available in private or storage-restricted browsers. */}};
const preferences={detail:'balanced',palette:'sage',reducedMotion:matchMedia('(prefers-reduced-motion: reduce)').matches,zoom:14,...readStorage(SETTINGS_KEY)};
preferences.detail=['fine','balanced','bold'].includes(preferences.detail)?preferences.detail:'balanced';
preferences.palette=['sage','amber','silver'].includes(preferences.palette)?preferences.palette:'sage';
preferences.zoom=Number.isFinite(Number(preferences.zoom))?THREE.MathUtils.clamp(Number(preferences.zoom),9,29):14;
preferences.reducedMotion=Boolean(preferences.reducedMotion);
const saved=readStorage(SAVE_KEY);
const state={mode:'intro',started:false,x:0,z:13,y:0,facing:Math.PI,moving:false,sprinting:false,inWater:false,collected:new Set(),complete:false,elapsed:0,time:0,nearby:null,steps:0};
const keys=new Set();
const joystick={x:0,y:0,active:false,pointer:null};
let touchRun=false,toastTimer=0,saveTimer=0,rippleTimer=0,lastHud=0,modalBefore='playing',manualUntil=0,drag=null;
let cameraAngle=.15,cameraPitch=.60,cameraDistance=Number(preferences.zoom)||14;
const cameraTarget=new THREE.Vector3(0,0,9),cameraGoal=new THREE.Vector3(),playerLabelPosition=new THREE.Vector3();
const scene=new THREE.Scene();scene.background=new THREE.Color(0x13292d);scene.fog=new THREE.FogExp2(0x173236,.017);
scene.add(new THREE.HemisphereLight(0xd6e5cf,0x354744,2.3));
const sun=new THREE.DirectionalLight(0xffe5b1,3.3);sun.position.set(-12,24,-10);scene.add(sun);
const rim=new THREE.DirectionalLight(0x78c3ca,1.5);rim.position.set(10,8,14);scene.add(rim);
const camera=new THREE.PerspectiveCamera(47,1,.1,150);camera.position.set(21,22,31);camera.lookAt(0,0,-3);
let graphics,world,character,effects,water,audio;
try{
  graphics=createRenderer($('#world'),scene,camera);
  world=createWorld(scene);character=createCharacter(scene);character.group.scale.setScalar(1.12);effects=createEffects(scene);water=createWater(scene,world);audio=createAudio();
}catch(error){show('#loading',false);show('#webgl-error');console.error(error);throw error;}
// Keep the traveler visible when the camera would sit behind an abbey arch.
const cameraBlockers=world.root.children.filter(object=>/stonework|columns|arch-stones|tree-limbs|meadow-boulders/.test(object.name));
const cameraRay=new THREE.Raycaster(),cameraFocus=new THREE.Vector3(),cameraRayOrigin=new THREE.Vector3(),cameraDirection=new THREE.Vector3(),cameraHits=[];
let cameraObstructed=false;
scene.updateMatrixWorld(true);
function resolveCamera(position,shoulders=true){
 cameraFocus.set(state.x,state.y+1.35,state.z);
 let ratio=1;
 for(const side of (shoulders?[-.23,0,.23]:[0])){
  cameraRayOrigin.copy(cameraFocus);cameraRayOrigin.x+=Math.cos(cameraAngle)*side;cameraRayOrigin.z-=Math.sin(cameraAngle)*side;
  cameraDirection.subVectors(position,cameraRayOrigin);const length=cameraDirection.length();
  if(length<.1)continue;
  cameraRay.set(cameraRayOrigin,cameraDirection.divideScalar(length));cameraRay.near=.12;cameraRay.far=length;
  cameraHits.length=0;cameraRay.intersectObjects(cameraBlockers,false,cameraHits);
  if(cameraHits.length)ratio=Math.min(ratio,Math.max(.6,cameraHits[0].distance-.65)/length);
 }
 if(ratio<1)position.lerpVectors(cameraFocus,position,ratio);
 return ratio<1;
}
const echoes=world.interactables.filter(item=>item.type==='echo');
const altar=world.interactables.find(item=>item.type==='altar');
const echoLore=[
 {title:'The echo of roots',hint:'West, where the old trees keep watch.',text:'“We planted a tree for everyone who stayed.”'},
 {title:'The echo of rain',hint:'East, beneath a sky the stones remember.',text:'“When the rain came, the whole lake sang.”'},
 {title:'The echo of light',hint:'North, where the abbey meets the morning.',text:'“Keep one light burning. Someone is still coming home.”'},
];
function validPosition(x,z){return Number.isFinite(x)&&Number.isFinite(z)&&world.canWalk(x,z);}
function loadJourney(){
  if(!saved||saved.version!==1)return;
  if(validPosition(saved.x,saved.z)){state.x=saved.x;state.z=saved.z;}
  for(const id of (Array.isArray(saved.collected)?saved.collected:[]))if(echoes.some(e=>e.id===id))state.collected.add(id);
  state.complete=Boolean(saved.complete)&&state.collected.size===echoes.length;
  state.elapsed=Number.isFinite(saved.elapsed)?Math.max(0,saved.elapsed):0;
}
loadJourney();
function syncEchoes(){for(const echo of echoes)echo.mesh.visible=!state.collected.has(echo.id);}
syncEchoes();
function saveJourney(){if(!state.started)return;writeStorage(SAVE_KEY,{version:1,x:state.x,z:state.z,collected:[...state.collected],complete:state.complete,elapsed:state.elapsed});}
function savePreferences(){writeStorage(SETTINGS_KEY,preferences);}
function toast(message,seconds=4){$('#toast').textContent=message;show('#toast');toastTimer=seconds;}
function clearInput(){keys.clear();joystick.x=0;joystick.y=0;joystick.active=false;$('#joystick-knob').style.transform='';state.moving=false;}
function start(){
 state.started=true;state.mode='playing';show('#intro',false);show('#hud');show('#mobile-controls',matchMedia('(pointer: coarse)').matches);closeModal();
 cameraTarget.set(state.x,0,state.z-2);character.setPosition(state.x,world.groundHeight(state.x,state.z),state.z);
 toast(state.complete?'The song is home. The glade is yours to wander.':'Three echoes remain. Let their golden light guide you.',5);saveJourney();
}
function resetJourney(){state.x=world.spawn.x;state.z=world.spawn.z;state.facing=Math.PI;state.collected.clear();state.complete=false;state.elapsed=0;state.nearby=null;touchRun=false;clearInput();syncEchoes();start();updateHUD();}
function openModal(id){
 modalBefore=state.mode==='intro'?'intro':'playing';state.mode='paused';clearInput();
 for(const name of ['settings','journal','ending'])show(`#${name}-modal`,name===id);
 show('#modal-backdrop');show('#restart-confirm',false);if(id==='journal')updateJournal();
 $(id==='ending'?'#continue-btn':`#${id}-modal .close-btn`)?.focus({preventScroll:true});
}
function closeModal(){show('#modal-backdrop',false);for(const name of ['settings','journal','ending'])show(`#${name}-modal`,false);state.mode=state.started?'playing':'intro';clearInput();$(state.started?'#world':'#start-btn').focus({preventScroll:true});}
function toggleMap(){show('#map-panel',$('#map-panel').classList.contains('hidden'));updateMap();}
function nearest(){let best=null,range=2.35;for(const item of world.interactables){if(item.type==='echo'&&state.collected.has(item.id))continue;const distance=Math.hypot(state.x-item.position.x,state.z-item.position.z);if(distance<range){range=distance;best=item;}}return best;}
function interact(){
 if(state.mode!=='playing')return;
 const item=nearest();if(!item)return;
 if(item.type==='echo'){
  const index=echoes.indexOf(item);state.collected.add(item.id);item.mesh.visible=false;
  effects.burst(item.position.x,item.position.y+1,item.position.z,0xe5cf8b);audio.chime('echo');
  toast(`${echoLore[index].title.replace('The echo','Echo')} found. ${echoLore[index].text}`,6);
  if(state.collected.size===echoes.length)setTimeout(()=>{if(!state.complete&&state.mode==='playing')toast('All three echoes are with you. Return to the altar in the northern abbey.',6);},1800);
 }else if(!state.complete){
  if(state.collected.size<echoes.length){audio.chime('hint');toast(`The altar is waiting for ${echoes.length-state.collected.size} more ${echoes.length-state.collected.size===1?'echo':'echoes'}. Follow the gold around the shore.`,4);}
  else{state.complete=true;audio.chime('complete');for(let i=0;i<7;i++)effects.burst(item.position.x+(i-3)*.8,item.position.y+1.8,item.position.z,0xd5d9a4);$('#ending-time').textContent=`${Math.max(1,Math.round(state.elapsed/60))} ${Math.round(state.elapsed/60)<=1?'MINUTE':'MINUTES'} WANDERED`;openModal('ending');}
 }else toast('The song has found its way home. Stay as long as you like.');
 saveJourney();updateHUD();
}
function updateJournal(){
 $('#journal-entries').innerHTML=echoes.map((item,index)=>{const found=state.collected.has(item.id),lore=echoLore[index];return `<div class="journal-entry ${found?'found':''}"><span class="symbol">${found?'✦':'◇'}</span><div><strong>${lore.title}</strong><p>${found?lore.text:lore.hint}</p></div></div>`;}).join('');
}
function updateMap(){
 $('#map-player').setAttribute('cx',state.x);$('#map-player').setAttribute('cy',state.z);
 $('#map-markers').innerHTML=world.interactables.filter(item=>!state.collected.has(item.id)).map(item=>`<text x="${item.position.x}" y="${item.position.z+1}" text-anchor="middle" font-size="3.3" fill="${item.type==='echo'?'#dfc988':'#b8cda9'}">${item.type==='echo'?'◆':'✧'}</text>`).join('');
}
function updateHUD(){
 state.nearby=nearest();
 camera.updateMatrixWorld();playerLabelPosition.set(state.x,state.y+2.2,state.z).project(camera);
 $('#player-label').style.left=`${(playerLabelPosition.x*.5+.5)*100}%`;$('#player-label').style.top=`${(-playerLabelPosition.y*.5+.5)*100}%`;
 $('#echo-count').textContent=`${state.collected.size} / ${echoes.length}`;
 $('#quest-description').textContent=state.complete?'The glade is whole again':state.collected.size===echoes.length?'Return the echoes to the northern altar':'Find the three scattered echoes';
 document.querySelectorAll('.echo-track span').forEach((el,i)=>el.classList.toggle('found',state.collected.has(echoes[i].id)));
 show('#interact-prompt',Boolean(state.nearby)&&state.mode==='playing');
 if(state.nearby)$('#interact-label').textContent=state.nearby.type==='echo'?'Gather the echo':state.complete?'Listen to the lake':'Restore the echoes';
 $('#touch-interact').disabled=!state.nearby;$('#touch-run').classList.toggle('active',touchRun);$('#touch-run').setAttribute('aria-pressed',String(touchRun));
 const area=state.z<-12?'The Silent Abbey':state.x<-11?'The Elder Grove':state.x>11?'The Rain Garden':state.inWater?'The Mirrorwater':'Stillwater Basin';
 $('#area-name').textContent=area;$('#coords').textContent=`${Math.abs(state.x).toFixed(0).padStart(2,'0')}${state.x<0?'W':'E'} · ${Math.abs(state.z).toFixed(0).padStart(2,'0')}${state.z<0?'N':'S'}`;
 const directions=['N','NW','W','SW','S','SE','E','NE'];$('#compass-heading').textContent=directions[((Math.round(cameraAngle/(Math.PI/4))%8)+8)%8];
 $('#weather-label').textContent=state.complete?'LIGHT / 06:21':'MIST / 06:14';
 if(!$('#map-panel').classList.contains('hidden'))updateMap();
}
function applyPreferences(){
 graphics.setDetail(preferences.detail);graphics.setPalette(preferences.palette);document.body.classList.toggle('reduced-motion',preferences.reducedMotion);
 $('#detail-select').value=preferences.detail;$('#palette-select').value=preferences.palette;$('#motion-setting').checked=preferences.reducedMotion;$('#zoom-setting').value=cameraDistance;
}
async function toggleSound(){await audio.setEnabled(!audio.enabled);const enabled=audio.enabled;$('#sound-btn').classList.toggle('active',enabled);$('#sound-btn').setAttribute('aria-label',enabled?'Mute sound':'Enable sound');$('#sound-btn').innerHTML=enabled?'<svg viewBox="0 0 24 24"><path d="M11 5 6 9H3v6h3l5 4V5zM15 8a6 6 0 0 1 0 8m3-11a10 10 0 0 1 0 14"/></svg>':'<svg viewBox="0 0 24 24"><path d="M11 5 6 9H3v6h3l5 4V5zM16 8l5 8m0-8-5 8"/></svg>';$('#audio-setting').textContent=enabled?'On':'Off';$('#audio-setting').setAttribute('aria-pressed',String(enabled));}
async function toggleFullscreen(){try{if(document.fullscreenElement)await document.exitFullscreen();else if($('#app').requestFullscreen)await $('#app').requestFullscreen();else toast('Fullscreen is managed by your browser on this device.');}catch{toast('Fullscreen is unavailable in this browser view.');}}

$('#start-btn').addEventListener('click',start);
if(saved?.version===1){$('#start-btn span:first-child').textContent='Continue wandering';show('#fresh-btn');}
$('#fresh-btn').addEventListener('click',()=>{openModal('settings');show('#restart-confirm');});
$('#settings-btn').addEventListener('click',()=>openModal('settings'));
$('#journal-btn').addEventListener('click',()=>openModal('journal'));
$('#resume-btn').addEventListener('click',closeModal);$('#journal-close').addEventListener('click',closeModal);$('#continue-btn').addEventListener('click',closeModal);
$('#restart-btn').addEventListener('click',()=>show('#restart-confirm'));$('#confirm-restart').addEventListener('click',resetJourney);$('#cancel-restart').addEventListener('click',()=>show('#restart-confirm',false));
$('#ending-new-btn').addEventListener('click',()=>{openModal('settings');show('#restart-confirm');});
$('#map-btn').addEventListener('click',toggleMap);$('#map-header-btn').addEventListener('click',toggleMap);$('#interact-btn').addEventListener('click',interact);$('#touch-interact').addEventListener('click',interact);$('#touch-run').addEventListener('click',()=>{touchRun=!touchRun;updateHUD();});
$('#sound-btn').addEventListener('click',toggleSound);$('#audio-setting').addEventListener('click',toggleSound);$('#fullscreen-btn').addEventListener('click',toggleFullscreen);
$('.wordmark').addEventListener('click',event=>{event.preventDefault();if(state.started)openModal('settings');});
for(const button of document.querySelectorAll('[data-close]'))button.addEventListener('click',()=>{if(button.dataset.close==='modal')closeModal();else show(`#${button.dataset.close}`,false);});
$('#modal-backdrop').addEventListener('click',event=>{if(event.target===$('#modal-backdrop')&&$('#ending-modal').classList.contains('hidden'))closeModal();});
$('#detail-select').addEventListener('change',event=>{preferences.detail=event.target.value;applyPreferences();savePreferences();});
$('#palette-select').addEventListener('change',event=>{preferences.palette=event.target.value;applyPreferences();savePreferences();});
$('#motion-setting').addEventListener('change',event=>{preferences.reducedMotion=event.target.checked;applyPreferences();savePreferences();});
$('#zoom-setting').addEventListener('input',event=>{cameraDistance=Number(event.target.value);preferences.zoom=cameraDistance;savePreferences();});
window.addEventListener('keydown',event=>{
 const key=event.key.toLowerCase();
 if(key==='tab'&&state.mode==='paused'){const activeModal=document.querySelector('.modal:not(.hidden)');const focusable=[...activeModal.querySelectorAll('button,input,select')].filter(el=>el.offsetParent!==null);const first=focusable[0],last=focusable.at(-1);if(event.shiftKey&&document.activeElement===first){event.preventDefault();last.focus();}else if(!event.shiftKey&&document.activeElement===last){event.preventDefault();first.focus();}return;}
 if(['INPUT','SELECT'].includes(document.activeElement.tagName)&&key!=='escape')return;
 if(['BUTTON','A'].includes(document.activeElement.tagName)&&['enter',' '].includes(key))return;
 if(key==='escape'){event.preventDefault();if(state.mode==='paused')closeModal();else if(!$('#map-panel').classList.contains('hidden'))show('#map-panel',false);else openModal('settings');return;}
 if(event.repeat)return;
 if(key==='f'){toggleFullscreen();return;}
 if(state.mode==='intro'){if((key==='enter'||key===' ')&&document.activeElement.tagName!=='BUTTON'){event.preventDefault();start();}return;}
 if(state.mode!=='playing')return;
 if(['arrowup','arrowdown','arrowleft','arrowright',' '].includes(key))event.preventDefault();
 if(key==='j'){openModal('journal');return;}if(key==='m'){toggleMap();return;}
 if(['e','enter',' '].includes(key)){event.preventDefault();interact();return;}
 keys.add(key);
});
window.addEventListener('keyup',event=>keys.delete(event.key.toLowerCase()));
window.addEventListener('blur',()=>{clearInput();saveJourney();});
document.addEventListener('visibilitychange',()=>{clearInput();saveJourney();if(document.hidden&&state.mode==='playing')openModal('settings');audio.setPaused?.(document.hidden);});
window.addEventListener('beforeunload',saveJourney);
const canvas=$('#world');
canvas.addEventListener('pointerdown',event=>{if(state.mode!=='playing')return;drag={x:event.clientX,y:event.clientY,id:event.pointerId};canvas.setPointerCapture(event.pointerId);});
canvas.addEventListener('pointermove',event=>{if(!drag||drag.id!==event.pointerId)return;cameraAngle-=(event.clientX-drag.x)*.005;cameraPitch=THREE.MathUtils.clamp(cameraPitch+(event.clientY-drag.y)*.003,.32,1);drag.x=event.clientX;drag.y=event.clientY;});
function endDrag(){drag=null;}canvas.addEventListener('pointerup',endDrag);canvas.addEventListener('pointercancel',endDrag);canvas.addEventListener('contextmenu',event=>event.preventDefault());
canvas.addEventListener('wheel',event=>{event.preventDefault();cameraDistance=THREE.MathUtils.clamp(cameraDistance+event.deltaY*.012,9,29);preferences.zoom=cameraDistance;$('#zoom-setting').value=cameraDistance;savePreferences();},{passive:false});
const joystickEl=$('#joystick');
function moveJoystick(event){const rect=joystickEl.getBoundingClientRect(),dx=event.clientX-rect.left-rect.width/2,dy=event.clientY-rect.top-rect.height/2;const magnitude=Math.hypot(dx,dy),ratio=Math.min(1,34/Math.max(1,magnitude));joystick.x=dx*ratio/34;joystick.y=dy*ratio/34;$('#joystick-knob').style.transform=`translate(${dx*ratio}px,${dy*ratio}px)`;}
joystickEl.addEventListener('pointerdown',event=>{event.preventDefault();joystick.active=true;joystick.pointer=event.pointerId;joystickEl.setPointerCapture(event.pointerId);moveJoystick(event);});
joystickEl.addEventListener('pointermove',event=>{if(joystick.active&&joystick.pointer===event.pointerId)moveJoystick(event);});
for(const type of ['pointerup','pointercancel','lostpointercapture'])joystickEl.addEventListener(type,()=>{joystick.active=false;joystick.x=0;joystick.y=0;$('#joystick-knob').style.transform='';});
window.addEventListener('resize',()=>graphics.resize());document.addEventListener('fullscreenchange',()=>{graphics.resize();$('#fullscreen-btn').setAttribute('aria-label',document.fullscreenElement?'Exit fullscreen':'Enter fullscreen');});

function update(dt){
 state.time+=dt;
 if(toastTimer>0){toastTimer-=dt;if(toastTimer<=0)show('#toast',false);}
 if(state.mode==='playing'){
  state.elapsed+=dt;saveTimer+=dt;if(saveTimer>4){saveTimer=0;saveJourney();}
  if(keys.has('q'))cameraAngle+=dt*1.3;if(keys.has('r'))cameraAngle-=dt*1.3;
  let sx=(keys.has('d')||keys.has('arrowright')?1:0)-(keys.has('a')||keys.has('arrowleft')?1:0)+joystick.x;
  let sz=(keys.has('s')||keys.has('arrowdown')?1:0)-(keys.has('w')||keys.has('arrowup')?1:0)+joystick.y;
  const length=Math.hypot(sx,sz);if(length>1){sx/=length;sz/=length;}state.moving=length>.06;state.sprinting=(keys.has('shift')||touchRun)&&state.moving;
  let dx=sx*Math.cos(cameraAngle)+sz*Math.sin(cameraAngle),dz=-sx*Math.sin(cameraAngle)+sz*Math.cos(cameraAngle);
  state.inWater=world.isWater(state.x,state.z);const speed=(state.sprinting?5.2:3.0)*(state.inWater?.78:1);
  if(state.moving){
   const nextX=state.x+dx*speed*dt,nextZ=state.z+dz*speed*dt;
   if(world.canWalk(nextX,nextZ)){state.x=nextX;state.z=nextZ;}else if(world.canWalk(nextX,state.z))state.x=nextX;else if(world.canWalk(state.x,nextZ))state.z=nextZ;
   const facing=Math.atan2(dx,dz),difference=Math.atan2(Math.sin(facing-state.facing),Math.cos(facing-state.facing));state.facing+=difference*Math.min(1,dt*13);
   rippleTimer+=dt;if(state.inWater&&rippleTimer>(state.sprinting?.21:.34)){effects.ripple(state.x,state.z);rippleTimer=0;state.steps++;}
  }
  state.inWater=world.isWater(state.x,state.z);state.y=world.groundHeight(state.x,state.z);character.setPosition(state.x,state.y,state.z);
 }else{state.moving=false;state.sprinting=false;}
 character.update(dt,{moving:state.moving,sprinting:state.sprinting,inWater:state.inWater,facing:state.facing,time:preferences.reducedMotion?0:state.time});
 const visualTime=preferences.reducedMotion?0:state.time;
 world.update(dt,visualTime);effects.update(dt,visualTime);water.update(visualTime,state.complete);
 audio.update(dt,{moving:state.moving&&state.mode==='playing',inWater:state.inWater,sprinting:state.sprinting,complete:state.complete});
 if(state.mode==='intro'||(state.mode==='paused'&&!state.started)){
  const orbit=preferences.reducedMotion?0:Math.sin(state.time*.026)*3;
  cameraGoal.set(21+orbit,22,31);camera.position.lerp(cameraGoal,Math.min(1,dt*2));camera.lookAt(0,.5,-3);
 }else{
  cameraTarget.lerp(new THREE.Vector3(state.x,0,state.z-3.3),preferences.reducedMotion?1:1-Math.exp(-dt*5));
  const mobile=camera.aspect<.8?1.13:1;
  cameraGoal.set(cameraTarget.x+Math.sin(cameraAngle)*cameraDistance*mobile,cameraDistance*Math.sin(cameraPitch)+2.1,cameraTarget.z+Math.cos(cameraAngle)*cameraDistance*mobile);
  cameraObstructed=resolveCamera(cameraGoal);
  camera.position.lerp(cameraGoal,cameraObstructed||preferences.reducedMotion?1:1-Math.exp(-dt*4));
  cameraObstructed=resolveCamera(camera.position,false)||cameraObstructed;
  const framing=THREE.MathUtils.smoothstep(camera.position.distanceTo(cameraFocus),5,11);
  camera.lookAt(THREE.MathUtils.lerp(state.x,cameraTarget.x,framing),THREE.MathUtils.lerp(state.y+1,.6,framing),THREE.MathUtils.lerp(state.z,cameraTarget.z,framing));
 }
 lastHud+=dt;if(lastHud>.1){updateHUD();lastHud=0;}
}
applyPreferences();character.setPosition(state.x,world.groundHeight(state.x,state.z),state.z);character.update(0,{facing:state.facing,time:0});updateJournal();updateHUD();
let lastTime=performance.now();
function frame(now){requestAnimationFrame(frame);const dt=Math.min((now-lastTime)/1000,.05);lastTime=now;if(!document.hidden&&now>manualUntil){update(dt);graphics.render(state.time);}}
requestAnimationFrame(frame);graphics.render(0);show('#loading',false);
window.render_game_to_text=()=>JSON.stringify({mode:state.mode,coordinates:'x right/east, y up, z down/south; world positions in meters',player:{x:+state.x.toFixed(3),y:+state.y.toFixed(3),z:+state.z.toFixed(3),facing:+state.facing.toFixed(2),moving:state.moving,sprinting:state.sprinting,inWater:state.inWater},echoes:echoes.map(item=>({id:item.id,x:item.position.x,z:item.position.z,collected:state.collected.has(item.id)})),altar:{x:altar.position.x,z:altar.position.z},nearby:state.nearby?.id??null,collected:state.collected.size,complete:state.complete,elapsed:+state.elapsed.toFixed(2),rippleSteps:state.steps,camera:{angle:+cameraAngle.toFixed(3),distance:+cameraDistance.toFixed(2),obstructed:cameraObstructed},settings:{...preferences,sound:audio.enabled},mapOpen:!$('#map-panel').classList.contains('hidden')});
window.advanceTime=(ms)=>{manualUntil=performance.now()+180;const n=Math.max(1,Math.ceil(ms/(1000/60))),dt=ms/1000/n;for(let i=0;i<n;i++)update(dt);graphics.render(state.time);};
if(new URLSearchParams(location.search).has('test'))window.__stillwater={state,world,scene,camera,graphics,reset:resetJourney,teleport(x,z){if(!validPosition(x,z))throw new Error('Invalid position');state.x=x;state.z=z;state.y=world.groundHeight(x,z);state.inWater=world.isWater(x,z);character.setPosition(x,state.y,z);cameraTarget.set(x,0,z-3.3);updateHUD();},setCamera(angle,distance=14){cameraAngle=angle;cameraDistance=distance;},pauseAuto(){manualUntil=Infinity;},resumeAuto(){manualUntil=0;},interact,save:saveJourney};
