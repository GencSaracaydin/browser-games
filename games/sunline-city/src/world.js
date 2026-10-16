import {buildCounty} from './world-expansion.js';
import {decorateNeighborhoods} from './neighborhood-art.js';
// Sunline City: an entirely drawn, sun-warmed miniature coastal city.
// All coordinates describe the ground plane; façades and palms rise toward -Y.
const C = {
  grass: '#4d966b', lawn: '#4e9e65', darkGrass: '#287a54', hedge: '#185647',
  cream: '#f4ead5', pavement: '#e4dfcf', curb: '#f6eddc', road: '#687d83',
  roadEdge: '#586c72', stripe: '#e9e1ba', pink: '#efb5ab', peach: '#eac9ae',
  mint: '#b4d3bd', blue: '#b8d2db', ink: '#173a48', shadow: 'rgba(15,43,67,.28)',
  pool: '#0eb9c5', sea: '#135eaa', frond: '#124d41', frondLight: '#3a8867',
};
const TAU = Math.PI * 2;
const rect = (c,x,y,w,h,color) => { c.fillStyle=color; c.fillRect(x,y,w,h); };
const line = (c,x,y,x2,y2,color,width=1) => { c.beginPath();c.moveTo(x,y);c.lineTo(x2,y2);c.strokeStyle=color;c.lineWidth=width;c.stroke(); };
const poly = (c,p,color) => { c.beginPath();p.forEach(([x,y],i)=>i?c.lineTo(x,y):c.moveTo(x,y));c.closePath();c.fillStyle=color;c.fill(); };
const ellipse = (c,x,y,rx,ry,color) => { c.beginPath();c.ellipse(x,y,rx,ry,0,0,TAU);c.fillStyle=color;c.fill(); };
const rounded = (c,x,y,w,h,r,color) => { c.beginPath();c.roundRect(x,y,w,h,r);c.fillStyle=color;c.fill(); };
const label = (c,str,x,y,size=10,color=C.ink,align='center',font='sans-serif') => { c.fillStyle=color;c.font=`600 ${size}px ${font}`;c.textAlign=align;c.textBaseline='middle';c.fillText(str,x,y); };
const intersects = (a,b,pad=0) => a.x+a.w+pad>b.x&&a.x-pad<b.x+b.w&&a.y+a.h+pad>b.y&&a.y-pad<b.y+b.h;
const noise = (n) => { const r=Math.sin(n*127.1+311.7)*43758.5453123;return r-Math.floor(r); };

function drawPalm(c,p,shadow=false) {
  const {x,y,h,scale:s=1,seed=0}=p;
  c.save();c.translate(x,y);
  if(shadow) { c.transform(1,0,-.72,-.3,0,0);c.globalAlpha=.25; }
  const lean=(noise(seed)*14-7)*s, crownY=-h;
  c.lineCap='round';
  c.beginPath();c.moveTo(0,0);c.quadraticCurveTo(lean*.2,-h*.45,lean,crownY);
  c.strokeStyle=shadow?'#12364b':'#74694e';c.lineWidth=5*s;c.stroke();
  if(!shadow) { c.strokeStyle='#b7a582';c.lineWidth=1.3*s;c.stroke();
    for(let k=8;k<h-8;k+=5){ line(c,lean*k/h-2*s,-k,lean*k/h+2*s,-k-1,'#81745a',.8*s); }
  }
  const count=11;
  for(let i=0;i<count;i++){
    const angle=i/count*TAU+noise(seed+4)*.8;
    const len=(24+noise(seed+i+90)*15)*s;
    const dx=Math.cos(angle)*len,dy=Math.sin(angle)*len*.56;
    const startX=lean,startY=crownY;
    const endX=startX+dx,endY=startY+dy+6*s;
    const bendX=startX+dx*.58,bendY=startY+dy*.9-7*s;
    c.beginPath();c.moveTo(startX,startY);c.quadraticCurveTo(bendX,bendY,endX,endY);
    c.strokeStyle=shadow?'#12364b':i%3===0?'#154e42':'#123f37';c.lineWidth=3.4*s;c.stroke();
    for(let k=2;k<10;k++){
      const t=k/10, mt=1-t;
      const bx=mt*mt*startX+2*mt*t*bendX+t*t*endX;
      const by=mt*mt*startY+2*mt*t*bendY+t*t*endY;
      const leaflet=(1-t)*10*s+1;
      const nx=-Math.sin(angle),ny=Math.cos(angle)*.7;
      const backwardX=-Math.cos(angle)*leaflet*.6,backwardY=-Math.sin(angle)*leaflet*.3;
      for(const side of [-1,1]) {
        line(c,bx,by,bx+nx*leaflet*side+backwardX,by+ny*leaflet*side+backwardY+3*s,
          shadow?'#12364b':(i+k)%4===0?C.frondLight:C.frond,1.6*s);
      }
    }
  }
  if(!shadow){ellipse(c,lean-2*s,crownY+3*s,3*s,2*s,'#9e8857');ellipse(c,lean+2*s,crownY+4*s,2.5*s,2*s,'#61754a');}
  c.restore();
}

function shrub(c,x,y,r=14,flowers=false) {
  ellipse(c,x+8,y+6,r,r*.47,'rgba(25,59,52,.15)');
  ellipse(c,x,y,r,r*.7,C.hedge);
  for(let i=0;i<10;i++){
    const a=i*2.4,rr=r*(.3+noise(x+y+i)*.65);
    ellipse(c,x+Math.cos(a)*rr,y+Math.sin(a)*rr*.55,3.8,2.8,i%3?'#337b58':'#639e64');
    if(flowers&&i%3===0)ellipse(c,x+Math.cos(a)*rr,y+Math.sin(a)*rr*.55,1.8,1.6,'#ec989a');
  }
}

function drawBuilding(c,b) {
  if(b.neighborhoodDraw){b.neighborhoodDraw(c,b);return;}
  const {x,y,w,h,z=32,color=C.pink,roof='#eee4cd',style='hotel'}=b;
  // Roof slab, light north wall and shaded front wall read as miniature architecture.
  rect(c,x,y-z,w,h,roof);
  rect(c,x,y+h-z,w,z,color);
  rect(c,x+w-7,y-z,7,h+z,'rgba(61,79,77,.13)');
  rect(c,x+4,y-z+4,w-8,h-8,b.roofColor||'#d9d4bf');
  rect(c,x+8,y-z+8,w-16,h-16,b.roofColor||roof);
  rect(c,x,y-z,w,3,'#fff5df');
  rect(c,x,y+h-z-3,w,5,'#f7ecd8');
  rect(c,x,y+h-3,w,3,'rgba(48,79,77,.2)');
  if(style==='villa') {
    rect(c,x+8,y-z+8,w-16,h-16,'#d6a28f');
    for(let sy=y-z+12;sy<y+h-z-8;sy+=7)line(c,x+10,sy,x+w-10,sy,'#c28d7c',1);
    for(let sx=x+15;sx<x+w-8;sx+=11)line(c,sx,y-z+9,sx,y+h-z-9,'rgba(246,216,181,.34)',1);
    line(c,x+8,y+h/2-z,x+w-8,y+h/2-z,'#f5d5bb',4);
    const n=Math.max(2,Math.floor(w/44));
    for(let i=0;i<n;i++){
      const wx=x+18+i*(w-32)/n;
      rounded(c,wx,y+h-z+9,20,z-15,9,'#3e6f73');
      rect(c,wx-3,y+h-z+9,3,z-13,'#f8ead6');rect(c,wx+20,y+h-z+9,3,z-13,'#f8ead6');
      line(c,wx+10,y+h-z+11,wx+10,y+h-8,'#c2c8ac',1);
    }
  } else if(style==='garage'||style==='hangar'){
    for(let sx=x+8;sx<x+w-8;sx+=9)line(c,sx,y-z+8,sx,y+h-z-9,'rgba(99,115,110,.18)',1);
    const n=style==='hangar'?1:3;
    for(let i=0;i<n;i++){
      const gw=(w-24)/n-8,gx=x+12+i*(w-24)/n;
      rect(c,gx,y+h-z+8,gw,z-9,'#48676e');
      for(let yy=y+h-z+12;yy<y+h-2;yy+=5)line(c,gx,yy,gx+gw,yy,'#7e9291',1);
      rect(c,gx+3,y+h-z+12,gw-6,5,'#b7cbbf');
    }
  } else {
    // Recessed windows and breezeways run along the façade.
    const n=Math.max(2,Math.floor(w/31));
    const ww=(w-20)/n;
    for(let i=0;i<n;i++){
      const wx=x+11+i*ww;
      rect(c,wx,y+h-z+10,ww-10,z-18,'#547a80');
      rect(c,wx+2,y+h-z+11,ww-14,4,'#87b9bd');
      rect(c,wx,y+h-z+8,ww-7,2,'#fff3dc');
      if(i%3===0)rect(c,wx+ww*.45,y+h-z+10,2,z-18,'#e7c6b0');
      if(style==='hotel'){
        rect(c,wx-2,y+h-9,ww-6,2,'#fbebd1');
        for(let q=0;q<4;q++)line(c,wx+q*(ww-8)/3,y+h-8,wx+q*(ww-8)/3,y+h-3,'#d5b39f',.7);
      }
    }
  }
  // Rooftop utilities, skylights, vent shadows, and tiny solar panels.
  if(w>100&&h>50){
    const ux=x+w-46,uy=y-z+15;
    rect(c,ux+7,uy+7,26,18,'rgba(44,73,73,.14)');
    rect(c,ux,uy,26,16,'#a7b4ab');rect(c,ux,uy,26,3,'#d1d8c7');
    ellipse(c,ux+8,uy+9,5,4,'#6f8983');ellipse(c,ux+19,uy+9,4,3,'#758b86');
    for(let a=0;a<4;a++)line(c,ux+3,uy+5+a*3,ux+13,uy+5+a*3,'#9eafa5',.6);
    if(w>220){rect(c,x+19,y-z+15,55,23,'#7a9c9b');for(let s=0;s<5;s++)line(c,x+21+s*11,y-z+16,x+21+s*11,y-z+36,'#bad0c0',1);}
    if(h>110&&style!=='villa') {
      // An enclosed stairwell and a little rooftop planting break up deep roofs.
      rect(c,x+19,y+h-z-44,39,23,'rgba(45,74,68,.12)');
      rect(c,x+14,y+h-z-49,36,21,color);rect(c,x+14,y+h-z-49,36,5,'#f6ead2');
      rect(c,x+36,y+h-z-39,7,11,'#7a9b91');
      rect(c,x+w-43,y+h-z-29,24,12,'#c4b197');
      for(let k=0;k<4;k++)ellipse(c,x+w-39+k*5,y+h-z-26,4,4,k%2?'#668c65':'#8da279');
    }
  }
  if(b.name){
    const signW=Math.min(w-18,b.name.length*7+22),sy=y+h-z-18;
    rect(c,x+(w-signW)/2+3,sy+3,signW,17,'rgba(31,61,65,.12)');
    rect(c,x+(w-signW)/2,sy,signW,17,b.signColor||'#f3ead5');
    label(c,b.name,x+w/2,sy+9,b.name.length>15?8.5:10,b.ink||'#426b69');
  }
  if(b.awning){
    const ay=y+h-z+4;
    for(let q=0;q<Math.floor((w-16)/12);q++)rect(c,x+8+q*12,ay,12,10,q%2?'#f4e9d5':b.awning);
    rect(c,x+8,ay+10,Math.floor((w-16)/12)*12,3,'rgba(40,71,68,.18)');
  }
  if(b.tower){
    const tx=x+w/2-25,ty=y-z-19;
    rect(c,tx+5,ty+7,50,30,'rgba(46,69,70,.15)');rect(c,tx,ty,50,29,b.color);
    rect(c,tx+3,ty+3,44,4,'#f8edd7');rect(c,tx+8,ty+10,34,9,'#607c83');
    label(c,'S',tx+25,ty+15,12,'#f3e2c5');
  }
}

function drawUmbrella(c,x,y,color='#f5e4c2',r=15) {
  ellipse(c,x+13,y+11,r,r*.6,'rgba(34,69,66,.15)');
  line(c,x,y,x,y+13,'#bbaa87',2);
  for(let i=0;i<8;i++){
    const a=i/8*TAU,b=(i+1)/8*TAU;
    poly(c,[[x,y-8],[x+Math.cos(a)*r,y+Math.sin(a)*r*.65-5],[x+Math.cos(b)*r,y+Math.sin(b)*r*.65-5]],i%2?color:'#fff1d4');
  }
  ellipse(c,x,y-8,2,2,'#fff5dc');
}
function lounge(c,x,y,angle=0,color='#f5ebd9'){
  c.save();c.translate(x,y);c.rotate(angle);rect(c,5,4,10,24,'rgba(39,73,69,.13)');
  rect(c,0,0,10,25,color);rect(c,1,1,8,8,'#d3d9c4');
  for(let i=3;i<24;i+=4)line(c,1,i,9,i,'#b8c7b7',.6);c.restore();
}
function drawBoat(c,x,y,s=1,color='#f7ead4',angle=0){
  c.save();c.translate(x,y);c.rotate(angle);c.scale(s,s);
  ellipse(c,5,7,13,31,'rgba(7,48,91,.25)');
  c.beginPath();c.moveTo(0,-34);c.bezierCurveTo(17,-17,16,14,10,30);c.lineTo(-10,30);c.bezierCurveTo(-16,14,-17,-17,0,-34);c.fillStyle=color;c.fill();
  poly(c,[[-8,-17],[0,-27],[8,-17],[9,18],[-9,18]],'#c4d6cf');
  rounded(c,-7,-8,14,22,3,'#f7eee0');rect(c,-6,-5,12,6,'#537d90');
  rect(c,-7,19,14,6,'#bcab8d');line(c,0,-23,0,-12,'#f7f4db',1.4);c.restore();
}

export function createWorld(){
  const width=5200,height=5700;
  const roads={vertical:[480,1080,1680,2320],horizontal:[480,1160,1840,2520],width:116};
  const buildings=[],pools=[],palms=[],props=[],ground=[],terrainZones=[];
  const pois=[
    {id:'motel',key:'motel',x:2180,y:1050,name:'The Palms'},
    {id:'marina',key:'marina',x:2450,y:2040,name:'Bluewater Marina'},
    {id:'club',key:'club',x:1380,y:1770,name:'Club Mirage'},
    {id:'garage',key:'garage',x:750,y:1770,name:'Sol Auto'},
    {id:'villa',key:'villa',x:1390,y:410,name:'Casa Azul'},
    {id:'airstrip',key:'airstrip',x:390,y:2440,name:'Sunset Airfield'},
    {id:'lookout',key:'lookout',x:2510,y:700,name:'Ocean Lookout'},
  ];
  const addB=(x,y,w,h,z,color,name='',extra={})=>{const b={x,y,w,h,z,color,name,...extra};buildings.push(b);return b;};
  const palm=(x,y,h=64,s=1)=>palms.push({x,y,h,scale:s,seed:x*3+y});
  const pool=(x,y,w,h,kind='pool')=>{pools.push({x,y,w,h,kind});};
  const base=(fn)=>ground.push(fn);
  const prop=(x,y,w,h,draw)=>props.push({x,y:y-h,w,h,sortY:y,draw});
  const grassLot=(x,y,w,h,color=C.lawn)=>{terrainZones.push({x,y,w,h,type:'grass'});return base(c=>{
    rect(c,x,y,w,h,color);
    for(let sy=y;sy<y+h;sy+=18)rect(c,x,sy,w,8,'rgba(208,228,153,.045)');
  });};
  const paving=(x,y,w,h,color=C.cream)=>{terrainZones.push({x,y,w,h,type:'paved'});return base(c=>{rect(c,x,y,w,h,color);for(let k=x+24;k<x+w;k+=48)line(c,k,y,k,y+h,'rgba(90,115,103,.055)',1);});};
  const hedge=(x,y,w,h)=>base(c=>{rect(c,x,y,w,h,C.hedge);for(let k=0;k<w;k+=17)shrub(c,x+k+7,y+h/2,8,true);});
  const shrubs=(x,y,count,step=28)=>base(c=>{for(let i=0;i<count;i++)shrub(c,x+i*step,y,13,i%3===0);});
  const parking=(x,y,w,h,vertical=false)=>{terrainZones.push({x,y,w,h,type:'paved'});return base(c=>{
    rect(c,x,y,w,h,'#a0aaa3');
    if(vertical){for(let py=y+5;py<y+h-28;py+=35){line(c,x+4,py,x+31,py,'#e8e5cb',1);line(c,x+w-31,py,x+w-4,py,'#e8e5cb',1);}}
    else for(let px=x+5;px<x+w-23;px+=27){line(c,px,y+3,px,y+34,'#ebe8cf',1.2);if(h>70)line(c,px,y+h-34,px,y+h-3,'#ebe8cf',1.2);}
  });};

  // THE PALMS. A generous courtyard gives the first scene its postcard composition.
  grassLot(1765,574,474,503,'#539f69');
  paving(1834,691,370,359);
  addB(1860,730,320,100,40,C.pink,'THE PALMS',{tower:true,roof:'#f2e5ce'});
  addB(1808,870,85,154,28,'#f2cbb9','SUITES',{roof:'#f5e8d0'});
  pool(2000,910,160,90);
  paving(1983,892,194,125,'#f9edcf');
  base(c=>{
    rect(c,1950,1036,258,6,'#ecdfc7');
    label(c,'P A L M S   M O T E L',2017,1058,9,'#5e8672');
    for(let i=0;i<4;i++){lounge(c,2020+i*37,872,0);lounge(c,2020+i*37,1025,Math.PI);}
    drawUmbrella(c,1970,930,'#e9b5a0',16);drawUmbrella(c,2185,962,'#e9b5a0',16);
    lounge(c,1966,951,0);lounge(c,2194,905,0);
  });
  hedge(1838,1040,203,9);hedge(1930,668,260,9);
  [ [1815,704,75], [2214,718,83], [1921,875,76], [2220,1002,72], [1952,1046,77], [1801,1052,65], [2216,845,70] ].forEach(p=>palm(...p));
  parking(1798,589,415,77);
  prop(2216,1082,36,80,c=>{
    line(c,2216,1082,2216,1018,'#c9ad88',4);
    rect(c,2200,1007,32,39,'#e6b0a0');rect(c,2203,1010,26,33,'#f4e8c8');
    label(c,'THE',2216,1017,5,'#577c70');label(c,'PALMS',2216,1026,6,'#577c70');label(c,'MOTEL',2216,1035,5,'#ab816a');
  });

  // Ocean-facing art deco resorts and a morning tennis club.
  paving(1776,155,454,238);
  addB(1798,210,222,106,44,'#c1d5bd','CORAL HOUSE',{awning:'#82b9ad'});
  addB(2078,201,137,138,30,'#f2c7ad','THE BREEZE',{roof:'#f3ddbb'});
  pool(1845,348,110,36);
  shrubs(1800,141,15);palm(1779,362,76);palm(2053,365,69);palm(2223,377,82);
  paving(1776,1270,452,463,'#eee5ce');
  addB(1805,1310,180,75,30,'#e7b9a9','SUNLINE RACQUET CLUB',{roof:'#f1ddbd'});
  base(c=>{
    rect(c,1805,1430,180,261,'#d89384');rect(c,1818,1445,154,231,'#57a5a3');
    c.strokeStyle='#f9e9d2';c.lineWidth=2;c.strokeRect(1827,1453,136,215);c.strokeRect(1840,1453,110,215);
    c.strokeRect(1840,1506,110,111);line(c,1895,1506,1895,1617,'#f9e9d2',2);
    line(c,1815,1560,1975,1560,'#254f51',3);for(let x=1817;x<1975;x+=5)line(c,x,1550,x,1560,'#eff0d6',.6);
    line(c,1815,1548,1975,1548,'#eff0d6',1.4);line(c,1815,1547,1815,1562,'#edf1d8',3);line(c,1975,1547,1975,1562,'#edf1d8',3);
    for(let i=0;i<4;i++){lounge(c,2008,1458+i*55,Math.PI/2);drawUmbrella(c,2054,1469+i*55,'#e8c19b',13);}
  });
  addB(2105,1363,99,213,43,'#c1d3cd','SUNDIAL',{roof:'#ece7d1'});
  pool(2090,1635,110,69);
  palm(1798,1413,75);palm(2086,1420,68);palm(2054,1708,80);palm(2214,1703,75);
  shrubs(1796,1731,16);
  addB(1810,1990,172,114,34,'#c2d3c4','SEABIRD MARKET',{awning:'#799f95'});
  addB(2060,1990,147,107,30,'#e5b6ab','LIDO CAFÉ',{awning:'#da927d'});
  parking(1790,2140,422,115);paving(1790,2280,422,137);grassLot(1830,2314,343,75);
  shrubs(1810,2304,14);palm(1800,2078,72);palm(2008,2084,81);palm(2221,2388,75);
  addB(1786,2662,202,81,26,'#f0c3a7','COAST RECORDS',{awning:'#e5ab8b'});
  addB(2055,2662,162,81,31,'#c0cfcc','POST OFFICE');

  // CASA AZUL: villas, gardens, pools, and shaded private lanes.
  grassLot(1162,120,433,274,'#56975f');paving(1190,332,382,52);
  addB(1220,198,270,100,42,'#d9d8b6','CASA AZUL',{style:'villa'});
  pool(1480,323,80,49);palm(1187,228,72);palm(1517,221,79);palm(1559,376,65);
  shrubs(1190,133,14);hedge(1180,375,174,8);hedge(1430,375,130,8);
  grassLot(1175,571,412,502,'#51966a');
  addB(1204,663,140,91,29,'#ead3b4','',{style:'villa'});addB(1419,681,138,95,30,'#bfcfc2','',{style:'villa'});
  addB(1210,912,155,92,31,'#e7b5a9','',{style:'villa'});addB(1430,921,129,87,29,'#c1d3c5','',{style:'villa'});
  pool(1212,800,109,44);pool(1440,819,98,46);
  paving(1377,590,22,460,'#dacfb6');paving(1192,874,376,22,'#dacfb6');
  for(const p of [[1188,785],[1366,801],[1567,826],[1180,1043],[1387,1050],[1560,1040],[1377,613]])palm(...p,62,.9);
  // Club Mirage surrounds a sculptural circular courtyard.
  paving(1178,1278,410,465,'#e8ddc9');grassLot(1200,1300,368,173);
  pool(1279,1329,203,96);
  addB(1205,1525,341,134,51,'#e0adb4','CLUB MIRAGE',{roof:'#ded4cd',tower:true,signColor:'#f0cbb9',ink:'#745e78'});
  base(c=>{
    ellipse(c,1375,1717,80,25,'#d4c6b8');ellipse(c,1375,1714,66,19,'#ede4ce');
    for(let i=0;i<9;i++){const a=i/9*TAU;ellipse(c,1375+Math.cos(a)*57,1714+Math.sin(a)*14,2,1.6,'#c5b29e');}
    line(c,1368,1714,1380,1685,'#b18c89',4);line(c,1380,1685,1389,1714,'#c5a192',4);
    label(c,'M I R A G E',1375,1684,9,'#9e7587');
    for(let i=0;i<5;i++) { lounge(c,1254+i*50,1285);drawUmbrella(c,1254+i*50,1460,'#d7b4b4',12); }
  });
  palm(1188,1468,78);palm(1568,1471,83);palm(1197,1726,76);palm(1570,1723,77);
  shrubs(1211,1743,6);shrubs(1430,1743,6);
  // Civic square with geometric lawns and reflecting basin.
  paving(1181,1945,402,459);grassLot(1200,1960,363,95);grassLot(1200,2300,363,86);
  addB(1265,2040,232,95,51,'#d8d3bc','SUNLINE GALLERY',{roof:'#f2ead6'});
  base(c=>{
    ellipse(c,1380,2226,66,37,'#c8bfa6');ellipse(c,1380,2223,59,32,'#f7ebd1');ellipse(c,1380,2223,49,25,'#449f9c');
    ellipse(c,1380,2223,10,6,'#e3e1be');line(c,1380,2221,1380,2195,'#b4d6c5',3);ellipse(c,1380,2193,6,3,'#f1edcd');
    for(const x of [1225,1535]){rect(c,x,2170,6,55,'#b79f7d');rect(c,x-2,2170,10,55,'#c8b88e');}
  });
  for(const x of [1210,1560])for(const y of [2040,2180,2370])palm(x,y,70);
  addB(1195,2663,174,74,28,'#d4b3b1','FLEUR',{awning:'#be8090'});addB(1423,2663,153,74,30,'#edd1b3','NORTH STAR');

  // SOL DISTRICT: small shops, apartment terraces, and the workshop.
  paving(579,166,416,227);
  addB(590,201,169,112,35,'#c4ccbe','PALMA BOOKS',{awning:'#8baaa0'});addB(817,208,162,105,39,'#debeb4','GOLDEN HOUR');
  parking(590,337,388,52);palm(579,363,72);palm(990,359,77);shrubs(609,150,13);
  paving(581,588,410,480);
  addB(590,637,176,133,44,'#e5c3a8','LAS FLORES',{roof:'#eadbc0'});addB(817,638,164,135,36,'#adcac4','PACIFIC LAUNDRY',{awning:'#79a8a2'});
  parking(594,792,385,70);addB(593,930,172,99,38,'#e1b3a9','SUNSET DINER',{awning:'#d88979'});addB(820,931,162,99,44,'#c7ceaf','SOL APARTMENTS');
  palm(785,745,77);palm(785,1025,72);palm(590,1065,62);palm(983,1064,66);
  paving(579,1272,414,470,'#dfd8c5');
  addB(590,1325,166,101,39,'#d9c6aa','NO. 07 STORAGE',{style:'garage'});addB(809,1325,167,101,35,'#c4d1bb','SOL SUPPLY',{awning:'#86a594'});
  parking(590,1460,388,79);addB(603,1636,348,94,36,'#e6bd99','SOL AUTO',{style:'garage',roof:'#dbd8c4',signColor:'#f4d3a8',ink:'#8c7256'});
  base(c=>{
    rect(c,608,1738,341,10,'#d2c6ae');label(c,'SERVICE  ·  REPAIRS  ·  GOOD COMPANY',778,1750,7,'#7e8b77');
    for(let q=0;q<3;q++){ellipse(c,967+q*7,1671,5,8,'#3e5151');ellipse(c,967+q*7,1670,2,4,'#8c9c8c');}
    for(let q=0;q<3;q++)rect(c,585,1660+q*18,11,13,'#729f89');
  });
  palm(582,1557,66);palm(985,1570,71);palm(972,1744,64);
  grassLot(578,1941,415,484,'#57965f');paving(614,1957,333,427);
  addB(618,2012,323,102,43,'#bfcab8','SUNLINE STUDIOS',{roof:'#e0dbbf'});
  pool(657,2192,237,90);shrubs(621,2320,12);palm(587,2132,75);palm(967,2162,84);palm(587,2392,68);palm(974,2400,73);
  addB(588,2650,164,87,30,'#b6cfc3','SURF SERVICE',{awning:'#76a99f'});addB(809,2650,168,87,33,'#d5b8a7','PALOMA');

  // Western neighborhood and the compact private airfield.
  for(const y of [193,663,942,1334,1580]){
    paving(110,y-34,281,147);
    addB(126,y,243,91,30,y===1580?'#bdd0b6':'#e4c7aa',y===193?'WESTSIDE':y===1334?'RADIO SUNLINE':'',{style:y===663||y===942?'villa':'hotel'});
    palm(111,y+105,63,.95);palm(387,y+95,69,.95);
  }
  grassLot(105,1938,296,494,'#57966e');
  addB(108,1980,284,81,32,'#c8d0be','SUNSET AIRFIELD',{style:'hangar',roof:'#dad8c6'});
  base(c=>{
    rect(c,179,2123,123,299,'#8b9690');rect(c,189,2129,103,289,'#84908d');
    c.setLineDash([19,15]);line(c,240,2142,240,2395,'#e9e5cc',3);c.setLineDash([]);
    for(const yy of [2140,2388])for(let xx=195;xx<290;xx+=14)rect(c,xx,yy,7,17,'#e3dfc8');
    label(c,'09',240,2183,17,'#eae5c9');label(c,'27',240,2362,17,'#eae5c9');
    // A cream and coral light aircraft parked beside the runway.
    c.save();c.translate(340,2200);c.rotate(.18);
    poly(c,[[4,-45],[10,-10],[47,10],[46,18],[7,7],[6,35],[18,44],[17,49],[-17,49],[-18,44],[-6,35],[-7,7],[-46,18],[-47,10],[-10,-10],[-4,-45]],'rgba(33,66,66,.15)');
    c.translate(-7,-6);poly(c,[[0,-45],[6,-33],[7,-8],[43,9],[42,15],[6,6],[5,33],[16,42],[16,46],[-16,46],[-16,42],[-5,33],[-6,6],[-42,15],[-43,9],[-7,-8],[-6,-33]],'#f4e8cb');
    rect(c,-3,-24,6,21,'#cc857b');rect(c,-4,-18,8,8,'#5d8f9b');line(c,-21,-40,21,-40,'#3e6a6b',2);c.restore();
  });
  palm(113,2349,71);palm(388,2357,72);
  addB(117,2661,265,80,27,'#e4d5b0','WEST COAST WAREHOUSE',{style:'garage'});

  // Beach promenade, a lookout pavilion, and marina piers.
  base(c=>{
    rect(c,2410,85,46,2700,'#e9dfc6');line(c,2454,85,2454,2785,'#faf0d8',3);
    for(let y=100;y<2790;y+=32)line(c,2412,y,2453,y,'#cfcbb4',.7);
    rect(c,2458,649,170,97,'#f0e2c3');
    for(let x=2466;x<2628;x+=10)line(c,x,651,x,745,'#dfd1b3',1);
    line(c,2627,650,2627,746,'#f8edcc',5);line(c,2459,650,2627,650,'#f8edcc',4);
    for(let y=657;y<745;y+=14)rect(c,2625,y,5,5,'#b5b79e');
    for(let y=850;y<1720;y+=195){drawUmbrella(c,2584,y,'#dfac99',20);lounge(c,2563,y+21,0);lounge(c,2593,y+21,0);drawUmbrella(c,2511,y+84,'#b1c5b1',17);lounge(c,2507,y+106,0);}
    // Wooden boardwalk and finger docks remain scenery beyond the beach's walkable boundary.
    rect(c,2457,1970,270,160,'#d6c7a7');
    for(let yy=1975;yy<2126;yy+=9)line(c,2459,yy,2727,yy,'#bdad90',1);
    rect(c,2690,1940,42,530,'#c4b498');
    for(let yy=1943;yy<2470;yy+=9)line(c,2691,yy,2731,yy,'#ae9d86',1);
    for(let yy=1980;yy<2450;yy+=116){rect(c,2730,yy,150,20,'#d7c5a3');for(let xx=2733;xx<2880;xx+=9)line(c,xx,yy+1,xx,yy+19,'#baa78c',1);for(let xx=2737;xx<2880;xx+=42)ellipse(c,xx,yy+3,3,3,'#6a7a71');}
    label(c,'BLUEWATER',2534,1995,11,'#688574');label(c,'M A R I N A',2534,2011,8,'#879783');
    for(let yy=2024;yy<2470;yy+=116){drawBoat(c,2770,yy,1,'#faf0d9');drawBoat(c,2837,yy,1.05,'#e9dad2');}
    drawBoat(c,3002,1130,1.5,'#f1e7d6',.25);drawBoat(c,2910,470,.8,'#f3e6cc',-.4);
    // Lifeguard house and pennant.
    rect(c,2544,1724,35,24,'#cd9d85');rect(c,2540,1708,43,18,'#e7bca2');poly(c,[[2535,1708],[2562,1696],[2588,1708]],'#d68b80');
    for(let x=2545;x<2580;x+=11)line(c,x,1726,x,1753,'#e5d1af',3);line(c,2590,1737,2590,1689,'#c2b79b',2);poly(c,[[2591,1689],[2608,1693],[2591,1700]],'#dc927d');
  });
  addB(2470,2180,157,71,26,'#d5bea6','HARBOR OFFICE',{awning:'#7aa8a3'});
  for(let y=182;y<2760;y+=144){
    if(Math.abs(y-700)<80||Math.abs(y-2040)<120)continue;
    palm(2426,y,70+noise(y)*13,1.03);
  }
  palm(2475,646,80);palm(2616,772,77);palm(2472,1940,78);palm(2608,2146,73);
  // Every street gets an alternating rhythm of palm trees and small urban furniture.
  for(const x of [480,1080,1680])for(let y=224;y<2760;y+=218){
    if(roads.horizontal.some(ry=>Math.abs(y-ry)<118))continue;
    palm(x-83,y,55+noise(y+x)*18,.9);
    if(x>500)palm(x+83,y+46,59+noise(y+x+10)*15,.92);
  }
  for(const y of roads.horizontal)for(let x=220;x<2240;x+=290){
    if(roads.vertical.some(rx=>Math.abs(x-rx)<130))continue;
    palm(x,y-83,57+noise(x+y)*16,.9);
  }
  // Slender streetlights, benches, newsstands and bicycle racks.
  for(const x of roads.vertical)for(let y=300;y<2700;y+=340){
    if(roads.horizontal.some(ry=>Math.abs(y-ry)<102))continue;
    prop(x+71,y,16,51,c=>{line(c,x+71,y,x+71,y-43,'#7e9184',2);line(c,x+71,y-43,x+62,y-47,'#7e9184',2);ellipse(c,x+60,y-47,6,2.4,'#efe5c9');});
  }
  base(c=>{
    for(const [x,y] of [[1795,1093],[1971,1093],[2207,1093],[2397,909],[2397,1490],[2400,2390],[1248,1755],[1515,1755],[1218,2233],[1543,2233]]){
      rect(c,x+4,y+3,31,8,'rgba(37,62,58,.13)');rect(c,x,y,29,6,'#bca882');line(c,x+1,y+2,x+27,y+2,'#e0c6a0',.7);rect(c,x+3,y+6,2,4,'#798b7b');rect(c,x+24,y+6,2,4,'#798b7b');
    }
    for(const [x,y] of [[2030,1086],[1448,1756],[961,1051],[2467,2090]]){
      ellipse(c,x,y,7,6,'#536f68');ellipse(c,x+21,y,7,6,'#536f68');ellipse(c,x,y,5.7,4.7,'#e4debf');ellipse(c,x+21,y,5.7,4.7,'#e4debf');
      line(c,x,y,x+10,y-10,'#b6806e',1.8);line(c,x+10,y-10,x+21,y,'#b6806e',1.8);line(c,x+21,y,x+8,y,'#b6806e',1.5);line(c,x+8,y,x+16,y-9,'#b6806e',1.5);line(c,x+15,y-9,x+20,y-11,'#65766b',1.5);
    }
    for(const [x,y] of [[2232,1085],[1786,1751],[994,1750],[1583,2420],[2393,1980]]) {rounded(c,x,y,8,13,2,'#608878');rect(c,x-1,y,10,3,'#527668');}
  });

  const county=buildCounty({C,rect,line,poly,ellipse,rounded,label,shrub,drawUmbrella,lounge,drawBoat,addB,palm,pool,base,prop,grassLot,paving,hedge,shrubs,parking,noise});
  pois.push(...county.pois);
  const {mapLand,roadSegments,districts,extraCarSpawns,extraTrafficRoutes,stuntRamps,raceTrack}=county;

  const {neighborhoods,neighborhoodAt}=decorateNeighborhoods({C,rect,line,poly,ellipse,rounded,label,shrub,drawUmbrella,noise,base,prop,palm,buildings,pools,terrainZones,drawBaseBuilding:(c,b)=>drawBuilding(c,{...b,neighborhoodDraw:null})});

  const drawables=[];
  // Palm fronds contain hundreds of strokes. Cache each at 2× resolution so
  // busy intersections remain smooth without sacrificing the fine foliage.
  const cachedPalm=(p)=>{
    let sprite=null;const x=Math.floor(p.x-48),y=Math.floor(p.y-p.h-43),w=96,h=Math.ceil(p.h+48);
    return c=>{
      if(typeof document==='undefined'){drawPalm(c,p);return;}
      if(!sprite){sprite=document.createElement('canvas');sprite.width=w*2;sprite.height=h*2;const s=sprite.getContext('2d');s.scale(2,2);s.translate(-x,-y);drawPalm(s,p);}
      c.drawImage(sprite,x,y,w,h);
    };
  };
  for(const b of buildings)drawables.push({x:b.x,y:b.y+b.h,w:b.w,h:b.h+b.z+45,top:b.y-b.z-45,draw:(c)=>drawBuilding(c,b)});
  for(const p of palms)drawables.push({x:p.x-48,y:p.y,w:96,h:p.h+43,top:p.y-p.h-43,draw:cachedPalm(p)});
  for(const p of props)drawables.push({x:p.x-20,y:p.sortY,w:p.w+40,h:p.h,top:p.y,draw:p.draw});

  function drawRoads(c){
    const rw=roads.width;
    for(const x of roads.vertical){rect(c,x-rw/2-20,65,rw+40,2715,C.pavement);rect(c,x-rw/2-3,65,rw+6,2715,C.curb);rect(c,x-rw/2,65,rw,2715,C.road);}
    for(const y of roads.horizontal){rect(c,70,y-rw/2-20,2340,rw+40,C.pavement);rect(c,70,y-rw/2-3,2340,rw+6,C.curb);rect(c,70,y-rw/2,2340,rw,C.road);}
    c.lineCap='butt';
    for(const x of roads.vertical){
      for(let y=88;y<2770;y+=34){if(roads.horizontal.some(ry=>Math.abs(y-ry)<78))continue;line(c,x-2,y,x-2,y+19,C.stripe,1.2);line(c,x+2,y,x+2,y+19,C.stripe,1.2);}
      for(let y=74;y<2775;y+=21){if(roads.horizontal.some(ry=>Math.abs(y-ry)<76))continue;line(c,x-55,y,x-55,y+15,'#adb2a1',1);line(c,x+55,y,x+55,y+15,'#adb2a1',1);}
    }
    for(const y of roads.horizontal){
      for(let x=85;x<2390;x+=34){if(roads.vertical.some(rx=>Math.abs(x-rx)<78))continue;line(c,x,y-2,x+19,y-2,C.stripe,1.2);line(c,x,y+2,x+19,y+2,C.stripe,1.2);}
      for(let x=74;x<2395;x+=21){if(roads.vertical.some(rx=>Math.abs(x-rx)<76))continue;line(c,x,y-55,x+15,y-55,'#adb2a1',1);line(c,x,y+55,x+15,y+55,'#adb2a1',1);}
    }
    // Crosswalks and stop lines are slightly faded in the sun.
    for(const x of roads.vertical)for(const y of roads.horizontal){
      for(let k=-45;k<=45;k+=12){rect(c,x+k,y-81,6,15,'#d5d8c6');rect(c,x+k,y+66,6,15,'#d5d8c6');rect(c,x-81,y+k,15,6,'#d5d8c6');rect(c,x+66,y+k,15,6,'#d5d8c6');}
      line(c,x+6,y+91,x+51,y+91,'#dfe1ce',2);line(c,x-6,y-91,x-51,y-91,'#dfe1ce',2);
      line(c,x+91,y-6,x+91,y-51,'#dfe1ce',2);line(c,x-91,y+6,x-91,y+51,'#dfe1ce',2);
      for(const [dx,dy] of [[-67,-66],[67,66]]){ellipse(c,x+dx,y+dy,3,2,'#8d9d8b');rect(c,x+dx-1,y+dy-14,2,14,'#879787');rect(c,x+dx-3,y+dy-20,6,9,'#506b68');rect(c,x+dx-1,y+dy-18,2,2,'#d3bb7d');}
    }
    const names=['PALM AVENUE','SUNSET BOULEVARD','MIRAGE AVENUE','HARBOR ROAD'];
    roads.horizontal.forEach((y,i)=>{label(c,names[i],1930,y-38,7,'#c6cec0');label(c,names[i],825,y-38,7,'#c6cec0');});
    c.save();c.translate(2358,1433);c.rotate(-Math.PI/2);label(c,'O C E A N   D R I V E',0,0,8,'#c6cec0');c.restore();
    // Road end markings provide a gentle visual edge without invisible city blocks.
    for(const y of roads.horizontal)for(let x=82;x<110;x+=9)rect(c,x,y-37,4,74,'#b8bcaa');
  }
  // Lazy 768px ground tiles avoid a 119MB county-sized canvas. The LRU limit
  // keeps long drives bounded while preserving the finely drawn original art.
  const tileSize=768,tileCache=new Map(),tileLimit=24;
  function drawCountyRoads(c){
    const segments=roadSegments.slice(8);
    c.lineCap='butt';c.lineJoin='round';
    for(const [extra,color] of [[40,C.pavement],[6,C.curb],[0,C.road]])for(const s of segments)line(c,s.x1,s.y1,s.x2,s.y2,color,s.width+extra);
    for(const s of segments){
      const dx=s.x2-s.x1,dy=s.y2-s.y1,len=Math.hypot(dx,dy),nx=-dy/len,ny=dx/len;
      for(let d=24;d<len-20;d+=34){
        const x=s.x1+dx*d/len,y=s.y1+dy*d/len;
        if(segments.some(q=>q!==s&&((Math.abs(q.x1-q.x2)<1&&Math.abs(x-q.x1)<75&&y>=Math.min(q.y1,q.y2)-75&&y<=Math.max(q.y1,q.y2)+75)||(Math.abs(q.y1-q.y2)<1&&Math.abs(y-q.y1)<75&&x>=Math.min(q.x1,q.x2)-75&&x<=Math.max(q.x1,q.x2)+75))))continue;
        line(c,x+nx*2,y+ny*2,x+dx/len*18+nx*2,y+dy/len*18+ny*2,C.stripe,1.2);
        line(c,x-nx*2,y-ny*2,x+dx/len*18-nx*2,y+dy/len*18-ny*2,C.stripe,1.2);
      }
      if(Math.abs(dy)<1&&len>700){label(c,s.name.toUpperCase(),s.x1+len*.30,s.y1-38,8,'#d3d8c2');label(c,s.name.toUpperCase(),s.x1+len*.72,s.y1-38,8,'#d3d8c2');}
    }
    for(const x of [480,1680,2320,3180,4020,4740])for(const y of [3020,3740,4340,5480]){
      if(x===4020&&y===5480)continue;
      for(let k=-42;k<=42;k+=12){rect(c,x+k,y-79,6,14,'#d6d8c3');rect(c,x+k,y+65,6,14,'#d6d8c3');rect(c,x-79,y+k,14,6,'#d6d8c3');rect(c,x+65,y+k,14,6,'#d6d8c3');}
    }
  }
  function bakeTile(tx,ty){
    if(typeof document==='undefined')return null;
    const tile={x:tx*tileSize,y:ty*tileSize,w:Math.min(tileSize,width-tx*tileSize),h:Math.min(tileSize,height-ty*tileSize)};
    const canvas=document.createElement('canvas');canvas.width=tile.w+4;canvas.height=tile.h+4;const c=canvas.getContext('2d');
    // Two world pixels of overlap prevent subpixel camera/zoom sampling seams.
    c.translate(2-tile.x,2-tile.y);
    rect(c,0,0,width,height,C.sea);
    rect(c,0,0,2685,2850,C.grass);
    // Fine horizontal ocean bands retain the tranquil, painted graphic quality.
    const seaGradient=c.createLinearGradient(2630,0,3300,0);seaGradient.addColorStop(0,'#279ca9');seaGradient.addColorStop(.16,'#176eaf');seaGradient.addColorStop(1,'#124d99');
    rect(c,2635,0,width-2635,height,seaGradient);
    for(let y=Math.floor(tile.y/13)*13;y<tile.y+tile.h;y+=13)rect(c,2700+noise(y)*80,y,width-2700,1,'rgba(209,228,209,.04)');
    poly(c,[[2454,0],[2672,0],[2655,400],[2685,850],[2646,1320],[2674,1765],[2650,2200],[2673,2850],[2454,2850]],'#e7d8b6');
    poly(c,[[2644,0],[2672,0],[2655,400],[2685,850],[2646,1320],[2674,1765],[2650,2200],[2673,2850],[2640,2850],[2625,2200],[2648,1765],[2624,1320],[2660,850],[2630,400]],'#ede0bf');
    for(let y=10;y<2850;y+=37)for(let x=2471;x<2643;x+=41)ellipse(c,x+noise(x+y)*15,y+noise(x-y)*20,.9,.6,'rgba(148,131,93,.18)');
    // City block sidewalks form quiet, cream rectangles under the detailed lots.
    const cols=[[96,303],[562,436],[1162,436],[1762,476]],rows=[[102,296],[562,516],[1242,516],[1922,516],[2602,150]];
    for(const [x,w]of cols)for(const [y,h]of rows){rect(c,x,y,w,h,'#c3c8ab');rect(c,x+8,y+8,w-16,h-16,'#659c6c');}
    county.drawTerrain(c);
    drawRoads(c);
    for(const fn of ground)if(!fn.bounds||intersects(fn.bounds,tile,40))fn(c);
    drawCountyRoads(c);
    // Pools are drawn into their surrounding deck and then animated very subtly.
    for(const p of pools){
      if(!intersects(p,tile,15))continue;
      if(p.kind==='lake'){if(p.outline){c.beginPath();p.outline.forEach(([x,y],i)=>i?c.lineTo(x,y):c.moveTo(x,y));c.closePath();c.strokeStyle='#bec8a0';c.lineWidth=14;c.stroke();c.fillStyle='#529c91';c.fill();c.save();c.clip();}else{rounded(c,p.x-7,p.y-7,p.w+14,p.h+14,78,'#c2c9a4');rounded(c,p.x,p.y,p.w,p.h,72,'#55a5a0');}for(let i=0;i<120;i++){const px=p.x+15+noise(i+41)*(p.w-30),py=p.y+15+noise(i+85)*(p.h-30);line(c,px,py,px+12,py,'rgba(215,235,197,.19)',1);}if(p.outline)c.restore();continue;}
      rect(c,p.x-6,p.y-6,p.w+12,p.h+12,'#f7edcf');rect(c,p.x-2,p.y-2,p.w+4,p.h+4,'#648e87');
      rect(c,p.x,p.y,p.w,p.h,C.pool);rect(c,p.x,p.y,p.w,5,'#229ba8');rect(c,p.x,p.y,5,p.h,'#2195a1');
      rect(c,p.x+5,p.y+p.h-3,p.w-5,3,'#9ed4c9');
      for(let x=p.x+9;x<p.x+p.w-7;x+=13)for(let y=p.y+11;y<p.y+p.h-5;y+=13){line(c,x,y,x+7,y-2,'rgba(204,241,235,.15)',1);}
      // A chrome ladder makes every pool read as water, even from a distance.
      for(const lx of [p.x+18,p.x+27]){line(c,lx,p.y-6,lx,p.y+14,'#ecedd3',1.5);line(c,lx,p.y-6,lx+3,p.y-10,'#ecedd3',1.5);}
      for(let ly=p.y+3;ly<p.y+14;ly+=5)line(c,p.x+18,ly,p.x+27,ly,'#c7e4cf',1);
    }
    // Long afternoon shadows are painted on the ground before the buildings.
    for(const b of buildings){if(!intersects({x:b.x,y:b.y-b.z,w:b.w+b.z,h:b.h+b.z*1.5},tile))continue;const d=b.z*.9;poly(c,[[b.x,b.y+b.h],[b.x+b.w,b.y+b.h],[b.x+b.w+d,b.y+b.h+d*.46],[b.x+d,b.y+b.h+d*.46]],C.shadow);poly(c,[[b.x+b.w,b.y-b.z],[b.x+b.w,b.y+b.h],[b.x+b.w+d,b.y+b.h+d*.46],[b.x+b.w+d,b.y-b.z+d*.46]],'rgba(20,46,66,.19)');}
    for(const p of palms)if(intersects({x:p.x-70,y:p.y-10,w:p.h+140,h:p.h*.6+60},tile))drawPalm(c,p,true);
    // Quiet paper-grain speckling, deterministic and baked only once.
    for(let i=0;i<1600;i++){const seed=i+tx*1713+ty*8197,x=tile.x+noise(seed+21)*tile.w,y=tile.y+noise(seed+927)*tile.h;rect(c,x,y,1,1,i%2?'rgba(255,244,213,.045)':'rgba(27,65,56,.025)');}
    return canvas;
  }
  function drawBase(c,bounds,time=0){
    const b=bounds||{x:0,y:0,w:width,h:height};
    rect(c,b.x,b.y,b.w,b.h,C.sea);
    const minX=Math.max(0,Math.floor(b.x/tileSize)),minY=Math.max(0,Math.floor(b.y/tileSize));
    const maxX=Math.min(Math.ceil(width/tileSize)-1,Math.floor((b.x+b.w)/tileSize)),maxY=Math.min(Math.ceil(height/tileSize)-1,Math.floor((b.y+b.h)/tileSize));
    for(let ty=minY;ty<=maxY;ty++)for(let tx=minX;tx<=maxX;tx++){
      const key=`${tx}:${ty}`;let tile=tileCache.get(key);
      if(tile){tileCache.delete(key);tileCache.set(key,tile);}else{tile=bakeTile(tx,ty);if(tile){tileCache.set(key,tile);while(tileCache.size>tileLimit)tileCache.delete(tileCache.keys().next().value);}}
      if(tile)c.drawImage(tile,tx*tileSize-2,ty*tileSize-2);
    }
    // Glints drift very slowly; the coast otherwise stays serene.
    if(b.x+b.w>2615){
      c.save();c.lineCap='round';
      for(let i=0;i<80;i++){
        const x=2675+noise(i+40)*(width-2675),y=(noise(i+130)*height+Math.sin(time*.3+i)*3)%height;
        if(x<b.x||x>b.x+b.w||y<b.y||y>b.y+b.h||insideLand(x,y))continue;
        const a=.08+.09*(.5+.5*Math.sin(time*1.1+i));line(c,x,y,x+4+noise(i)*18,y,`rgba(221,238,215,${a})`,1);
      }
      for(let y=Math.max(0,Math.floor(b.y/40)*40);y<Math.min(2780,b.y+b.h);y+=40){const x=2655+Math.sin(y*.006)*14+Math.sin(time*.4+y*.02)*4;line(c,x,y,x+2,y+22,'rgba(245,242,213,.36)',2);}
      c.restore();
    }
    for(const p of pools){if(!intersects(p,b))continue;
      c.save();c.beginPath();if(p.outline){p.outline.forEach(([x,y],i)=>i?c.lineTo(x,y):c.moveTo(x,y));c.closePath();}else c.rect(p.x+4,p.y+5,p.w-8,p.h-9);c.clip();
      for(let i=0;i<9;i++){const px=p.x+noise(i+p.x)*p.w,py=p.y+(noise(i+p.y)*p.h+time*1.3)%p.h;line(c,px,py,px+5+Math.sin(time+i)*2,py,'rgba(236,255,247,.74)',1.1);}
      c.restore();
    }
  }
  function insideLand(x,y){
    const boundary=mapLand[0];let inside=false;
    for(let i=0,j=boundary.length-1;i<boundary.length;j=i++){
      const [xi,yi]=boundary[i],[xj,yj]=boundary[j];
      if((yi>y)!==(yj>y)&&x<(xj-xi)*(y-yi)/(yj-yi)+xi)inside=!inside;
    }
    return inside;
  }
  function isSolid(x,y,r=8){
    if(!Number.isFinite(x)||!Number.isFinite(y))return true;
    if(!insideLand(x,y))return true;
    // Exact circle-to-coast checks retain a visible, consistent world boundary.
    const boundary=mapLand[0];
    for(let i=0,j=boundary.length-1;i<boundary.length;j=i++){
      const [ax,ay]=boundary[j],[bx,by]=boundary[i],dx=bx-ax,dy=by-ay,t=Math.max(0,Math.min(1,((x-ax)*dx+(y-ay)*dy)/(dx*dx+dy*dy)));
      if(Math.hypot(x-ax-dx*t,y-ay-dy*t)<r)return true;
    }
    return buildings.some(b=>x+r>b.x&&x-r<b.x+b.w&&y+r>b.y&&y-r<b.y+b.h)||pools.some(b=>{if(b.kind!=='lake')return x+r>b.x&&x-r<b.x+b.w&&y+r>b.y&&y-r<b.y+b.h;if(b.outline){let inside=false;for(let i=0,j=b.outline.length-1;i<b.outline.length;j=i++){const[ax,ay]=b.outline[j],[bx,by]=b.outline[i],dx=bx-ax,dy=by-ay,t=Math.max(0,Math.min(1,((x-ax)*dx+(y-ay)*dy)/(dx*dx+dy*dy)));if(Math.hypot(x-ax-dx*t,y-ay-dy*t)<r)return true;if((by>y)!==(ay>y)&&x<(ax-bx)*(y-by)/(ay-by)+bx)inside=!inside;}return inside;}const radius=72,dx=Math.max(0,Math.abs(x-b.x-b.w/2)-(b.w/2-radius)),dy=Math.max(0,Math.abs(y-b.y-b.h/2)-(b.h/2-radius));return Math.hypot(dx,dy)<radius+r;});
  }
  function terrainAt(x,y){
    const onSegment=(a,b,width)=>{const dx=b.x-a.x,dy=b.y-a.y,t=Math.max(0,Math.min(1,((x-a.x)*dx+(y-a.y)*dy)/(dx*dx+dy*dy||1)));return Math.hypot(x-a.x-dx*t,y-a.y-dy*t)<width/2;};
    if(roadSegments.some(r=>onSegment({x:r.x1,y:r.y1},{x:r.x2,y:r.y2},r.width)))return 'road';
    if(raceTrack.points.some((p,i)=>onSegment(p,raceTrack.points[(i+1)%raceTrack.points.length],raceTrack.width)))return 'road';
    for(let i=terrainZones.length-1;i>=0;i--){const z=terrainZones[i];if(x>=z.x&&x<=z.x+z.w&&y>=z.y&&y<=z.y+z.h)return z.type;}
    if(x>2454&&y<2780||x>4880&&y>2870||y>5500)return 'sand';
    const n=neighborhoodAt(x,y);if(n?.id==='hills')return 'dirt';
    return 'grass';
  }
  function getDrawables(bounds){
    if(!bounds)return drawables;
    return drawables.filter(d=>d.x+d.w>bounds.x-20&&d.x<bounds.x+bounds.w+20&&d.y>bounds.y-20&&d.top<bounds.y+bounds.h+20);
  }
  return {width,height,roads,roadSegments,mapLand,districts,neighborhoods,neighborhoodAt,terrainAt,extraCarSpawns,extraTrafficRoutes,stuntRamps,raceTrack,buildings,pools,pois,spawn:{x:2160,y:1085},isSolid,drawBase,getDrawables};
}
