// A slower way to know the county: neighborhoods, postcards, local food and buses.
const distance=(a,b)=>Math.hypot(a.x-b.x,a.y-b.y);
const clamp=(v,lo,hi)=>Math.max(lo,Math.min(hi,v));
const safeCount=v=>Number.isFinite(v)?clamp(Math.floor(v),0,999999):0;
const copyPoint=p=>({x:p.x,y:p.y});
export const POSTCARD_REWARD=150;
export const COLLECTION_REWARD=500;
export const DISCOVERY_REWARD=40;
export const TRANSIT_FARE=60;

export const LANDMARKS=Object.freeze([
  {id:'mercado',neighborhoodId:'havana',name:'Mercado del Sol',x:790,y:885,color:'#c98961',caption:'Striped awnings, a café on the corner, and the whole morning ahead.'},
  {id:'mirage',neighborhoodId:'mirage',name:'Club Mirage',x:1375,y:1715,color:'#b87e98',caption:'Pastel walls and a dance floor waiting for the sun to go down.'},
  {id:'ocean',neighborhoodId:'ocean',name:'Ocean Courts',x:2520,y:1600,color:'#65aeb7',caption:'A sea breeze through the palms. The next point can wait.'},
  {id:'palomar',neighborhoodId:'hills',name:'Palomar Overlook',x:1250,y:3640,color:'#8d9e71',caption:'The quiet roads above the city, where every turn is a scenic route.'},
  {id:'cypress',neighborhoodId:'cypress',name:'Cypress Boardwalk',x:1515,y:4825,color:'#6e9a81',caption:'Still water, wooden planks, and a little room to breathe.'},
  {id:'solstice',neighborhoodId:'festival',name:'Solstice Midway',x:2750,y:4705,color:'#d1a05f',caption:'Bright tents, warm pavement, and the unmistakable promise of summer.'},
  {id:'fountain',neighborhoodId:'downtown',name:'Banco Fountain',x:2540,y:3535,color:'#b78f77',caption:'Meet me by the fountain, where the city slows for a moment.'},
  {id:'cranes',neighborhoodId:'port',name:'Pier Nine Cranes',x:4840,y:4140,color:'#7498a2',caption:'Steel silhouettes over blue water. Somebody is always coming or going.'},
  {id:'speedway',neighborhoodId:'speedway',name:'Sunline Speedway',x:3440,y:4550,color:'#a19a6c',caption:'A quiet pit lane before the engines turn the afternoon into a blur.'},
].map(Object.freeze));

export const FOOD_STANDS=Object.freeze([
  {id:'rosa',type:'food',neighborhoodId:'havana',name:'Rosa’s Café',item:'Café con leche',x:865,y:885,price:30,health:35,color:'#bb855d',description:'A small cup, a long conversation. Little Havana runs on Rosa’s coffee.'},
  {id:'mirage-juice',type:'food',neighborhoodId:'mirage',name:'Mirage Juice Bar',item:'Passionfruit cooler',x:1520,y:1715,price:35,health:40,color:'#b97f9c',description:'Something cold and bright before another lap around the boulevard.'},
  {id:'ocean-ice',type:'food',neighborhoodId:'ocean',name:'Ocean Ice',item:'Mango shaved ice',x:2540,y:1640,price:25,health:30,color:'#6aa6af',description:'A paper cup of mango ice, best enjoyed with sand under your feet.'},
  {id:'solstice-tacos',type:'food',neighborhoodId:'festival',name:'Solstice Tacos',item:'Street taco plate',x:2830,y:4705,price:45,health:55,color:'#c69a58',description:'The best seat at the fair is beside the taco cart.'},
].map(Object.freeze));

export const TRANSIT_STOPS=Object.freeze([
  {id:'ocean',type:'transit',neighborhoodId:'ocean',name:'Ocean Beach',x:2404,y:1500},
  {id:'havana',type:'transit',neighborhoodId:'havana',name:'Little Havana',x:996,y:875},
  {id:'mirage',type:'transit',neighborhoodId:'mirage',name:'Mirage District',x:1596,y:1715},
  {id:'grove',type:'transit',neighborhoodId:'grove',name:'Palm Grove',x:2236,y:1085},
  {id:'hills',type:'transit',neighborhoodId:'hills',name:'Palomar Hills',x:1596,y:3600},
  {id:'downtown',type:'transit',neighborhoodId:'downtown',name:'Downtown Sunline',x:3100,y:3650},
  {id:'port',type:'transit',neighborhoodId:'port',name:'Port Azul',x:4104,y:3710},
  {id:'cypress',type:'transit',neighborhoodId:'cypress',name:'Cypress Park',x:1764,y:4860},
  {id:'festival',type:'transit',neighborhoodId:'festival',name:'Solstice Fairgrounds',x:2404,y:4850},
  {id:'speedway',type:'transit',neighborhoodId:'speedway',name:'Sunline Speedway',x:4656,y:4500},
].map(Object.freeze));

const FALLBACK_NEIGHBORHOODS=[
  ['ocean','Ocean Beach',2250,100,400,2680],['havana','Little Havana',80,100,1000,2050],
  ['mirage','Mirage District',1080,1160,1170,1310],['grove','Palm Grove',80,100,2570,2680],
  ['hills','Palomar Hills',80,2780,1600,1560],['downtown','Downtown Sunline',1680,2780,1500,1560],
  ['port','Port Azul',3180,2780,1790,1560],['cypress','Cypress Park',80,4340,2240,1230],
  ['festival','Solstice Fairgrounds',2320,4340,860,1230],['speedway','Sunline Speedway',3180,4340,1790,1230],
].map(([id,name,x,y,w,h])=>({id,name,x,y,w,h,color:'#739b89',tagline:'Another corner of summer.',description:''}));

export class ExplorationSystem{
  constructor(sim,save=null){
    this.sim=sim;
    this.landmarks=LANDMARKS;
    this.foodStands=FOOD_STANDS;
    this.transitStops=TRANSIT_STOPS;
    this.neighborhoods=sim.world.neighborhoods?.length?sim.world.neighborhoods:FALLBACK_NEIGHBORHOODS;
    const known=new Set(this.neighborhoods.map(n=>n.id)),photos=new Set(this.landmarks.map(p=>p.id));
    this.visited=new Set((Array.isArray(save?.visited)?save.visited:[]).filter(id=>known.has(id)));
    this.photos=new Set((Array.isArray(save?.photos)?save.photos:[]).filter(id=>photos.has(id)));
    this.album={};
    for(const id of this.photos){const thumb=save?.album?.[id];if(this.validThumbnail(thumb))this.album[id]=thumb;}
    this.collectionPaid=save?.collectionPaid===true||this.photos.size===this.landmarks.length;
    this.meals=safeCount(save?.meals);this.trips=safeCount(save?.trips);
    this.foodCooldown=0;this.travelCooldown=0;this.photoFlash=0;
    this.pendingNeighborhood=null;this.discoveryTime=0;this.current=null;
  }
  neighborhood(){
    const p=this.sim.pos;
    return this.sim.world.neighborhoodAt?.(p.x,p.y)||this.neighborhoods.find(n=>p.x>=n.x&&p.y>=n.y&&p.x<n.x+n.w&&p.y<n.y+n.h)||null;
  }
  update(dt){
    if(this.sim.mode!=='play')return;
    this.foodCooldown=Math.max(0,this.foodCooldown-dt);this.travelCooldown=Math.max(0,this.travelCooldown-dt);this.photoFlash=Math.max(0,this.photoFlash-dt);
    const n=this.neighborhood();this.current=n?.id||null;
    if(!n||this.visited.has(n.id)){this.pendingNeighborhood=null;this.discoveryTime=0;return;}
    if(this.pendingNeighborhood!==n.id){this.pendingNeighborhood=n.id;this.discoveryTime=0;}
    this.discoveryTime+=dt;
    if(this.discoveryTime>=1.2){
      this.discover(n.id);this.pendingNeighborhood=null;this.discoveryTime=0;
    }
  }
  discover(id){
    const n=this.neighborhoods.find(n=>n.id===id);
    if(!n||this.visited.has(id))return false;
    this.visited.add(id);this.sim.cash+=DISCOVERY_REWARD;
    this.sim.emit('neighborhoodDiscovered',{id:n.id,name:n.name,tagline:n.tagline||'',description:n.description||'',color:n.color,reward:DISCOVERY_REWARD});
    this.sim.emit('sfx',{name:'coin'});this.sim.emit('save');return true;
  }
  nearest(items,radius){
    return items.map(p=>({...p,distance:distance(this.sim.pos,p)})).filter(p=>p.distance<=radius).sort((a,b)=>a.distance-b.distance)[0]||null;
  }
  nearbyLandmark(radius=65){return this.nearest(this.landmarks,radius);}
  nearbyFood(radius=58){return this.nearest(this.foodStands,radius);}
  nearbyStop(radius=78){return this.nearest(this.transitStops,radius);}
  listLandmarks(){return this.landmarks.map(p=>({...p,type:'photo',collected:this.photos.has(p.id),visited:this.visited.has(p.neighborhoodId),distance:Math.round(distance(this.sim.pos,p)),thumbnail:this.album[p.id]||null,reward:POSTCARD_REWARD}));}
  listNeighborhoods(){return this.neighborhoods.map(n=>({...n,visited:this.visited.has(n.id),current:n.id===this.current,photos:this.landmarks.filter(p=>p.neighborhoodId===n.id&&this.photos.has(p.id)).length}));}
  listStops(){return this.transitStops.map(p=>({...p,...this.travelStatus(p.id),visited:this.visited.has(p.neighborhoodId),nearby:distance(this.sim.pos,p)<=78,distance:Math.round(distance(this.sim.pos,p)),fee:TRANSIT_FARE}));}
  context(){
    if(this.sim.mode!=='play'||this.sim.vehicle)return null;
    const food=this.nearbyFood(),photo=this.nearbyLandmark(),stop=this.nearbyStop();
    if(food&&this.sim.health<100)return{kind:'food',key:'E',id:food.id,label:`${food.item} · +${food.health} health · $${food.price}`};
    if(photo&&!this.photos.has(photo.id))return{kind:'photo',key:'C',id:photo.id,label:`Postcard · ${photo.name}`};
    if(stop)return{kind:'transit',key:'G',id:stop.id,label:'Sunline Shuttle · choose a visited stop'};
    if(food)return{kind:'food',key:'E',id:food.id,label:`${food.name} · ${food.item} · $${food.price}`};
    if(photo)return{kind:'photo',key:'P',id:photo.id,label:'Postcard saved · open your journal'};
    return null;
  }
  service(){const stop=this.nearbyStop();return !this.sim.vehicle&&stop?{kind:'transit',key:'G',id:stop.id,name:stop.name,label:'Sunline Shuttle'}:null;}
  interact(){if(this.sim.mode!=='play'||this.sim.vehicle)return false;const food=this.nearbyFood();if(!food)return false;this.buyFood(food.id);return true;}
  photo(id){
    const s=this.sim,p=id?this.landmarks.find(p=>p.id===id):this.nearbyLandmark();
    if(s.mode!=='play')return false;
    if(s.vehicle){s.toast('Park, step out, and find your angle. Postcards are taken on foot.');return false;}
    if(s.wanted){s.toast('Lose the patrols before stopping for a postcard.');return false;}
    if(!p||distance(s.pos,p)>65){s.toast('Look for a camera marker. Get close and press C to take a postcard.');return false;}
    if(this.photos.has(p.id)){s.toast('That view is already in your journal. Press P to see your postcards.');return false;}
    this.photos.add(p.id);this.photoFlash=.35;
    // Photographs also establish a visit if the player reaches a camera point
    // before the normal neighborhood dwell time has finished.
    this.discover(p.neighborhoodId);
    let bonus=0;if(this.photos.size===this.landmarks.length&&!this.collectionPaid){bonus=COLLECTION_REWARD;this.collectionPaid=true;}
    s.cash+=POSTCARD_REWARD+bonus;
    s.emit('postcard',{id:p.id,name:p.name,caption:p.caption,neighborhoodId:p.neighborhoodId,color:p.color,reward:POSTCARD_REWARD,collectionBonus:bonus,count:this.photos.size,total:this.landmarks.length});
    s.emit('sfx',{name:bonus?'complete':'coin'});s.emit('save');
    s.toast(bonus?`The whole county, in nine postcards. +$${POSTCARD_REWARD+bonus}`:`Postcard ${this.photos.size} / ${this.landmarks.length} · ${p.name} · +$${POSTCARD_REWARD}`);
    return true;
  }
  snapshot(id){return this.photo(id);}
  validThumbnail(value){return typeof value==='string'&&value.length<=120000&&/^data:image\/jpeg;base64,[A-Za-z0-9+/=]+$/.test(value);}
  attachPhoto(id,dataURL){if(!this.photos.has(id)||!this.validThumbnail(dataURL))return false;this.album[id]=dataURL;this.sim.emit('save');return true;}
  buyFood(id){
    const s=this.sim,food=id?this.foodStands.find(p=>p.id===id):this.nearbyFood();
    if(s.mode!=='play')return false;
    if(s.vehicle){s.toast('Step out of your car to visit the food stand.');return false;}
    if(!food||distance(s.pos,food)>58){s.toast('Visit the food stand to order.');return false;}
    if(s.wanted){s.toast('Lose the patrols before taking a lunch break.');return false;}
    if(s.health>=100){s.toast('Feeling good already. Save your appetite for later.');return false;}
    if(this.foodCooldown>0){s.toast('Take a moment to enjoy it before ordering again.');return false;}
    if(s.cash<food.price){s.toast(`${food.item} costs $${food.price}.`);return false;}
    const recovered=Math.min(food.health,100-s.health);s.cash-=food.price;s.health+=recovered;this.meals++;this.foodCooldown=8;
    s.emit('sfx',{name:'coin'});s.emit('save');s.toast(`${food.name} · ${food.item} · +${Math.round(recovered)} health`);return true;
  }
  travelStatus(id){
    const s=this.sim,target=this.transitStops.find(p=>p.id===id),origin=this.nearbyStop();
    const reject=reason=>({canTravel:false,reason,origin:origin?.id||null});
    if(!target)return reject('Choose a Sunline Shuttle stop.');
    if(s.mode!=='play'&&s.mode!=='paused')return reject('Return to the city before boarding.');
    if(s.vehicle)return reject('Park and step out to use the shuttle.');
    if(s.wanted)return reject('The shuttle will wait until you lose the patrols.');
    if(s.activities?.active)return reject('Finish or abandon your adventure before boarding.');
    if(s.job&&s.job.index<5&&s.job.phase!=='offer')return reject('Finish your story job before boarding.');
    if(!origin)return reject('Visit a marked bus stop to board the shuttle.');
    if(!this.visited.has(target.neighborhoodId))return reject('Discover this neighborhood before using its shuttle stop.');
    if(origin.id===target.id)return reject('You are already at this stop.');
    if(this.travelCooldown>0)return reject('The next shuttle will be along in a moment.');
    if(s.cash<TRANSIT_FARE)return reject(`A shuttle ticket costs $${TRANSIT_FARE}.`);
    return{canTravel:true,reason:null,origin:origin.id};
  }
  arrivalPoint(stop){
    const s=this.sim;
    for(const r of [0,22,40,58])for(let i=0;i<(r?12:1);i++){
      const q={x:stop.x+Math.cos(i*Math.PI/6)*r,y:stop.y+Math.sin(i*Math.PI/6)*r};
      if(!s.world.isSolid(q.x,q.y,10)&&![...(s.cars||[]),...(s.police||[])].some(c=>c.health>0&&distance(c,q)<34))return q;
    }
    return null;
  }
  travel(id){
    if(this.sim.mode!=='play'){this.sim.toast('Close the journal before boarding the shuttle.');return false;}
    const status=this.travelStatus(id);
    if(!status.canTravel){this.sim.toast(status.reason);return false;}
    const stop=this.transitStops.find(p=>p.id===id),arrival=this.arrivalPoint(stop),s=this.sim;
    if(!arrival){s.toast('That stop is blocked. Try another destination for now.');return false;}
    s.cash-=TRANSIT_FARE;s.player.x=arrival.x;s.player.y=arrival.y;s.player.angle=Math.PI/2;s.player.moving=false;
    s.customWaypoint=null;this.travelCooldown=2;this.trips++;
    s.emit('travel',{id:stop.id,name:stop.name,...arrival,fee:TRANSIT_FARE});s.emit('sfx',{name:'enter'});s.emit('save');
    s.toast(`${stop.name} · shuttle fare $${TRANSIT_FARE}. Welcome back.`);return true;
  }
  select(id,kind=null){
    if(typeof id!=='string')return false;
    if(id.includes(':'))[kind,id]=id.split(':',2);
    const groups=kind==='photo'?[this.landmarks]:kind==='food'?[this.foodStands]:kind==='transit'?[this.transitStops]:[this.landmarks,this.foodStands,this.transitStops];
    const p=groups.flat().find(p=>p.id===id);
    if(!p)return false;
    this.sim.customWaypoint={...copyPoint(p),name:p.name,explorationId:id};this.sim.toast(`${p.name} marked on your map.`);return true;
  }
  markers(){return[...this.landmarks.map(p=>({...p,type:'photo',collected:this.photos.has(p.id),symbol:'C'})),...this.foodStands.map(p=>({...p,symbol:'+'})),...this.transitStops.map(p=>({...p,color:'#527e82',visited:this.visited.has(p.neighborhoodId),symbol:'S'}))];}
  serialize(){return{version:1,visited:[...this.visited],photos:[...this.photos],album:{...this.album},collectionPaid:this.collectionPaid,meals:this.meals,trips:this.trips};}
  state(){return{neighborhood:this.neighborhood()?.id||null,visited:[...this.visited],neighborhoodsTotal:this.neighborhoods.length,photos:[...this.photos],photosTotal:this.landmarks.length,collectionComplete:this.collectionPaid,meals:this.meals,trips:this.trips,nearbyLandmark:this.nearbyLandmark()?.id||null,nearbyFood:this.nearbyFood()?.id||null,nearbyTransit:this.nearbyStop()?.id||null};}
}
