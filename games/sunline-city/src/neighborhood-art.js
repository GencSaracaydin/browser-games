// Place identity is built into the ground, planting and silhouettes, rather than
// a screen-wide colour filter. All additions share Sunline's painted palette.
export const NEIGHBORHOODS = [
  {id:'ocean',name:'Ocean Beach',tagline:'Salt air. Bare feet. Long afternoons.',description:'A cobalt shoreline of surf shacks, beach courts and striped parasols.',color:'#78bfc2',ambience:'surf',x:2250,y:100,w:400,h:2680},
  {id:'havana',name:'Little Havana',tagline:'Citrus, café cubano, and music in the square.',description:'Terracotta rooftops, limewashed arcades and a busy produce market.',color:'#d8a35d',ambience:'market',x:80,y:100,w:1000,h:2050},
  {id:'mirage',name:'Mirage District',tagline:'The neon comes on before the sun goes down.',description:'Lavender deco façades, teal lounges and sculptural garden courts.',color:'#b59bc6',ambience:'nightlife',x:1080,y:1160,w:1170,h:1310},
  {id:'grove',name:'Palm Grove',tagline:'A room by the pool. A whole summer ahead.',description:'The original garden city of pastel motels, palms and quiet villas.',color:'#a5c4a0',ambience:'palms',x:80,y:100,w:2570,h:2680},
  {id:'hills',name:'Palomar Hills',tagline:'The road winds up. The city falls away.',description:'Golden terraces, citrus orchards, vineyards and a breezy scenic overlook.',color:'#b9b387',ambience:'hills',x:80,y:2780,w:1600,h:1560},
  {id:'downtown',name:'Downtown Sunline',tagline:'Big façades. Bigger plans.',description:'Glass towers, a colonnaded bank and a palm-lined cinema boulevard.',color:'#adc8c5',ambience:'city',x:1680,y:2780,w:1500,h:1560},
  {id:'port',name:'Port Azul',tagline:'Salt, steel, and things that arrive after dark.',description:'Stacked freight containers, corrugated sheds, cranes and working quays.',color:'#97aca5',ambience:'harbor',x:3180,y:2780,w:1790,h:1560},
  {id:'cypress',name:'Cypress Park',tagline:'Trade the engine for a little birdsong.',description:'A shaded lakeside boardwalk inside a lush cypress and broadleaf grove.',color:'#6b9c7c',ambience:'forest',x:80,y:4340,w:2240,h:1230},
  {id:'festival',name:'Solstice Fairgrounds',tagline:'One more ride before the lights come on.',description:'A striped midway, food carts, a Ferris wheel and a sun-warmed skate park.',color:'#dfa36f',ambience:'fairground',x:2320,y:4340,w:860,h:1230},
  {id:'speedway',name:'Sunline Speedway',tagline:'Find your line. Keep your nerve.',description:'Banked corners, checkered pit garages and a grandstand under the palms.',color:'#d4c18b',ambience:'motorsport',x:3180,y:4340,w:1790,h:1230},
];

export function decorateNeighborhoods(api){
  const {C,rect,line,poly,ellipse,rounded,label,shrub,drawUmbrella,noise,base,prop:registerProp,palm,buildings,pools,
    drawBaseBuilding,terrainZones}=api;
  const neighborhoods=NEIGHBORHOODS.map(n=>({...n}));
  const neighborhoodAt=(x,y)=>neighborhoods.find(n=>x>=n.x&&x<n.x+n.w&&y>=n.y&&y<n.y+n.h)||null;
  const TAU=Math.PI*2;
  // Trees and landmark structures are static artwork. Cache them only when first
  // visible; repeated foliage strokes should not compete with driving physics.
  const prop=(x,y,w,h,draw)=>{
    const left=Math.floor(x-w-20),top=Math.floor(y-h-24),width=Math.ceil(w*2+40),height=Math.ceil(h+48);
    let sprite=null;
    registerProp(left+20,y,width-40,y-top,c=>{
      if(typeof document==='undefined'){draw(c);return;}
      if(!sprite){sprite=document.createElement('canvas');sprite.width=Math.ceil(width*1.5);sprite.height=Math.ceil(height*1.5);const p=sprite.getContext('2d');p.scale(1.5,1.5);p.translate(-left,-top);draw(p);}
      c.drawImage(sprite,left,top,width,height);
    });
  };
  const artBase=(bounds,draw)=>{draw.bounds=bounds;base(draw);};
  const tree=(x,y,kind='canopy',s=1)=>{
    const h=(kind==='cypress'?74:43)*s;
    prop(x,y,90*s,h+45*s,c=>{
      ellipse(c,x+17*s,y+7*s,31*s,12*s,'rgba(34,62,50,.14)');
      line(c,x,y,x-3*s,y-h*.68,'#817659',4*s);
      if(kind==='cypress'){
        ellipse(c,x,y-h*.62,15*s,h*.53,'#285c46');ellipse(c,x-3*s,y-h*.7,9*s,h*.41,'#467953');
      }else{
        const colors=kind==='jacaranda'?['#927baf','#b29cc9','#9d8aba']:kind==='citrus'?['#427852','#6d995c','#4c8655']:['#387556','#64976a','#4b865f'];
        for(let i=0;i<7;i++){const a=i/7*TAU;ellipse(c,x+Math.cos(a)*19*s,y-h+Math.sin(a)*11*s,23*s,18*s,colors[i%3]);}
        ellipse(c,x-6*s,y-h-5*s,22*s,17*s,colors[1]);
        if(kind==='citrus')for(let i=0;i<11;i++){const a=i*2.4,r=8+noise(x+y+i)*26;ellipse(c,x+Math.cos(a)*r*s,y-h+Math.sin(a)*r*.6*s,2.4*s,2.4*s,'#e4aa50');}
      }
    });
  };
  function havana(c,b){
    const {x,y,w,h,z}=b;
    drawBaseBuilding(c,{...b,name:'',awning:null,style:'villa',color:b.color,roof:'#d49b76'});
    // Clay roof ridges and alternating arched openings make this a low, warm town.
    poly(c,[[x+4,y-z+7],[x+w/2,y-z-13],[x+w-4,y-z+7],[x+w-4,y+h-z-8],[x+w/2,y+h-z-24],[x+4,y+h-z-8]],'#c28a68');
    line(c,x+w/2,y-z-13,x+w/2,y+h-z-24,'#ecc29a',3);
    for(let yy=y-z+14;yy<y+h-z-20;yy+=8){line(c,x+8,yy,x+w/2-3,yy-15,'#dca480',1);line(c,x+w/2+3,yy-15,x+w-8,yy,'#a96d59',1);}
    const count=Math.max(3,Math.floor(w/48)),step=(w-20)/count;
    for(let i=0;i<count;i++){const wx=x+13+i*step;rounded(c,wx,y+h-z+7,step-13,z-12,9,'#477b70');line(c,wx+3,y+h-z+15,wx+step-16,y+h-z+15,'#a3bb92',1);rect(c,wx-2,y+h-9,step-9,3,'#ead3ab');}
    if(b.name){rect(c,x+12,y+h-z-8,w-24,17,'#f1d6a6');label(c,b.name,x+w/2,y+h-z+1,Math.min(10,(w-24)/(b.name.length*.58)), '#8e604b');}
    if(b.awning){for(let i=0;i<Math.floor((w-16)/14);i++)poly(c,[[x+8+i*14,y+h-z+5],[x+22+i*14,y+h-z+5],[x+25+i*14,y+h-z+18],[x+11+i*14,y+h-z+18]],i%2?'#f0d7a9':b.awning);}
    for(let i=0;i<Math.max(1,Math.floor(w/90));i++){const px=x+24+i*83;rect(c,px,y+h-4,21,6,'#b58569');for(let k=0;k<3;k++)ellipse(c,px+4+k*6,y+h-6,5,4,k%2?'#6b925d':'#3f7652');}
  }
  function deco(c,b){
    const {x,y,w,h,z}=b;
    drawBaseBuilding(c,{...b,name:'',awning:null,tower:false,style:'hotel',roof:'#c9bdd0'});
    rect(c,x+8,y-z+8,w-16,8,'#e7d7d7');
    for(let i=0;i<3;i++){rect(c,x-3,y+h-z+5+i*7,w+6,3,i%2?'#70aaa6':'#ebd9cc');}
    const tx=x+w*.49,tw=Math.min(60,w*.2);
    rect(c,tx-tw/2,y-z-19,tw,40,b.color);rect(c,tx-tw*.38,y-z-28,tw*.76,15,'#e7c4c2');rect(c,tx-tw*.2,y-z-34,tw*.4,10,'#d0b6c9');
    for(let i=0;i<3;i++)rect(c,tx-tw*.28+i*tw*.22,y-z-21,tw*.1,29,'#4d8589');
    for(let wx=x+25;wx<x+w-15;wx+=51){ellipse(c,wx,y+h-z+30,9,9,'#f1d4c2');ellipse(c,wx,y+h-z+30,6,6,'#4d838a');}
    if(b.name){const sw=Math.min(w-16,b.name.length*8+20);rounded(c,x+(w-sw)/2,y+h-z-10,sw,23,4,'#4d737c');label(c,b.name,x+w/2,y+h-z+2,11,'#e7b2b6');line(c,x+(w-sw)/2+6,y+h-z+10,x+(w+sw)/2-6,y+h-z+10,'#e3b6bc',1);}
  }
  function downtown(c,b){
    const {x,y,w,h,z}=b;
    drawBaseBuilding(c,{...b,name:b.name==='BANCO DEL SOL'||b.name==='PACIFICA CINEMA'||z>80?'':b.name});
    if(b.name==='BANCO DEL SOL'){
      const fy=y+h-z+6;
      for(let xx=x+14;xx<x+w-8;xx+=30){rect(c,xx,fy,13,z-9,'#eee4c8');rect(c,xx+3,fy+4,4,z-16,'#c8d3bd');rect(c,xx-2,y+h-8,17,6,'#e6d8b9');}
      poly(c,[[x+3,fy-3],[x+w/2,fy-28],[x+w-3,fy-3]],'#e5dfc5');line(c,x+3,fy-3,x+w-3,fy-3,'#fff0d1',3);
      label(c,'BANCO DEL SOL',x+w/2,fy-8,10,'#68877b');
    }else if(b.name==='PACIFICA CINEMA'){
      rounded(c,x+14,y+h-z-3,w-28,32,8,'#d7a0a4');line(c,x+22,y+h-z+1,x+w-22,y+h-z+1,'#f1c891',3);label(c,'P A C I F I C A',x+w/2,y+h-z+11,16,'#f7e5c6');
      rect(c,x+w-49,y-z-24,31,h+20,'#597d83');label(c,'P',x+w-33,y-z-8,18,'#e7b3a4');
    }else if(z>80){
      const floors=Math.max(3,Math.floor(z/28));
      rect(c,x+9,y+h-z+6,w-25,z-10,'#6d9898');
      for(let row=1;row<floors;row++){const yy=y+h-z+row*(z-8)/floors;rect(c,x+6,yy,w-18,3,'#cbd9c6');}
      for(let xx=x+18;xx<x+w-12;xx+=24){rect(c,xx,y+h-z+7,3,z-13,'#bdd3c5');rect(c,xx+6,y+h-z+8,8,z-15,'rgba(227,239,211,.12)');}
      if(b.name)label(c,b.name,x+w/2,y+h-5,9,'#f4e8c9');
    }
  }
  function industrial(c,b){
    const {x,y,w,h,z}=b,container=w<170&&h<100&&x>4100&&y>3950;
    if(container){
      rect(c,x,y-z,w,h,b.color);rect(c,x,y+h-z,w,z,b.color);
      for(let xx=x+5;xx<x+w;xx+=8){line(c,xx,y-z+3,xx,y+h-2,'rgba(51,81,74,.22)',2);line(c,xx+2,y-z+3,xx+2,y+h-2,'rgba(245,230,195,.18)',1);}
      rect(c,x,y-z,w,3,'#ded6b5');rect(c,x+w-5,y-z,5,h+z,'rgba(45,69,64,.18)');
      const cx=x+w*.5;rect(c,cx-22,y+h-z+5,44,14,'#e7dabc');label(c,b.name||'AZUL',cx,y+h-z+12,7,'#677e72');
      line(c,x+5,y+h-z+4,x+5,y+h-3,'#d4cfaf',2);line(c,x+w-10,y+h-z+4,x+w-10,y+h-3,'#d4cfaf',2);
    }else{
      drawBaseBuilding(c,{...b,name:''});
      const rise=Math.min(28,h*.2);
      poly(c,[[x+5,y-z+7],[x+w*.5,y-z-rise],[x+w-5,y-z+7],[x+w-5,y+h-z-7],[x+w*.5,y+h-z-rise-14],[x+5,y+h-z-7]],'#aebfb0');
      for(let xx=x+10;xx<x+w-5;xx+=11)line(c,xx,y-z+5,xx,y+h-z-10,'#8eaa9e',1);
      line(c,x+w*.5,y-z-rise,x+w*.5,y+h-z-rise-14,'#dbe0c3',3);
      rect(c,x+12,y+h-z+7,w-24,z-10,'#527970');
      for(let xx=x+17;xx<x+w-15;xx+=22)line(c,xx,y+h-z+8,xx,y+h-4,'#83a193',2);
      if(b.name){rect(c,x+w*.17,y+h-z-10,w*.66,17,'#d7cfaa');label(c,b.name,x+w/2,y+h-z-1,9,'#55786d');}
      // Clearly industrial hazard edges, small enough to leave doors readable.
      for(let xx=x+10;xx<x+w-10;xx+=22){rect(c,xx,y+h-5,11,4,'#d5b475');rect(c,xx+11,y+h-5,11,4,'#626f60');}
    }
  }

  // Reskin existing footprints so routes, jobs and collision geometry stay exact.
  for(const b of buildings){
    const n=neighborhoodAt(b.x+b.w/2,b.y+b.h/2);b.neighborhood=n?.id;
    if(n?.id==='havana'&&b.y<1280){b.neighborhoodDraw=havana;b.color=['#dda576','#e4bd79','#c9c28e','#d6a17e'][Math.floor(b.x/130+b.y/200)%4];b.awning=b.x<800?'#bc775c':'#8aa176';}
    if(n?.id==='mirage'){b.neighborhoodDraw=deco;b.color=['#b4a1c5','#9bbdc0','#c2a6c3'][Math.floor(b.x/200)%3];b.roof='#d4c5d5';}
    if(n?.id==='downtown')b.neighborhoodDraw=downtown;
    if(n?.id==='port')b.neighborhoodDraw=industrial;
  }

  // LITTLE HAVANA: a tiled market passage, hanging pennants and citrus court.
  artBase({x:570,y:770,w:425,h:150},c=>{
    rect(c,580,780,415,144,'#d9aa7c');
    for(let y=783;y<923;y+=18)for(let x=585;x<995;x+=18){if((Math.floor(x/18)+Math.floor(y/18))%3===0)rect(c,x,y,8,8,'#efc89b');}
    rounded(c,764,809,51,48,8,'#e8ce9f');ellipse(c,790,831,18,14,'#5c9272');ellipse(c,790,831,8,6,'#e6ba7c');
    label(c,'MERCADO DEL SOL',790,878,9,'#91694f');
    for(let x=592;x<990;x+=37){poly(c,[[x,785],[x+15,785],[x+7,797]],['#cb8468','#e4c169','#88a786'][Math.floor(x/37)%3]);}
    line(c,585,784,990,784,'#9b8e68',1.2);
  });
  for(const x of [600,675,920,980])tree(x,900,'citrus',.85);
  tree(790,643,'citrus',.85);tree(790,1040,'citrus',.9);
  // Each market stall has a distinct product and a tilted striped canvas roof.
  for(const [x,y,color,produce]of[[616,839,'#cc815f','citrus'],[706,839,'#d2ad64','flowers'],[933,839,'#7f9b75','fruit']])prop(x,y,65,60,c=>{
    rect(c,x-25,y-13,50,20,'#ba8f63');rect(c,x-27,y-16,54,7,'#e9c195');
    for(const dx of [-25,25])line(c,x+dx,y,x+dx,y-49,'#a68b62',2);
    for(let k=0;k<6;k++)poly(c,[[x-30+k*10,y-52],[x-20+k*10,y-52],[x-15+k*10,y-34],[x-25+k*10,y-34]],k%2?'#f1d4a3':color);
    for(let k=0;k<10;k++){const px=x-20+(k%5)*10,py=y-13+Math.floor(k/5)*7;ellipse(c,px,py,4,3,produce==='flowers'?(k%2?'#d39399':'#e9ba72'):k%2?'#e7ae4e':'#9eaf65');}
  });
  // A domino square and round plant pots soften the mechanical street grid.
  artBase({x:576,y:1024,w:420,h:52},c=>{for(let x=630;x<980;x+=95){ellipse(c,x,1051,16,10,'#d6c4a0');rect(c,x-11,1044,22,12,'#e7cfa6');for(let k=0;k<4;k++)rect(c,x-7+k*4,1048,3,4,'#657a63');}});

  // MIRAGE: fans, lacquered pavers, curved cocktail terraces and a neon sculpture.
  artBase({x:1178,y:1465,w:410,h:280},c=>{
    rect(c,1178,1670,410,74,'#b3a5ba');
    for(let x=1185;x<1585;x+=34){line(c,x,1670,x+30,1743,'#d9c7cc',1);}
    ellipse(c,1375,1715,76,24,'#7b929e');ellipse(c,1375,1712,67,19,'#c7b4c7');
    for(let i=0;i<7;i++){const a=Math.PI+i*Math.PI/6;line(c,1375,1710,1375+Math.cos(a)*27,1698+Math.sin(a)*29,'#e2b8bf',3);}
    for(const x of[1200,1565]){rect(c,x,1680,11,49,'#769aa0');ellipse(c,x+5,1678,12,5,'#bba6bc');}
  });
  for(const [x,y]of[[1184,1285],[1575,1285],[1180,1770],[1580,1770],[1195,2185],[1567,2320]])tree(x,y,'jacaranda',.9);
  // A big deco blade makes the club readable above its flat resort neighbors.
  prop(1575,1645,35,147,c=>{
    rect(c,1565,1507,26,132,'#527c88');rect(c,1568,1509,20,126,'#b093b5');
    line(c,1566,1507,1566,1640,'#efc6bc',2);line(c,1590,1507,1590,1640,'#74b7b0',2);
    for(const [i,ch]of[...'MIRAGE'].entries())label(c,ch,1578,1521+i*18,13,'#f3dac3');
  });

  // OCEAN BEACH: a surfboard rack, shell-coloured volleyball court and parasols.
  artBase({x:2460,y:1380,w:185,h:330},c=>{
    rect(c,2478,1410,145,175,'#e7cf9f');c.strokeStyle='#f8e8c3';c.lineWidth=2;c.strokeRect(2488,1420,125,154);
    line(c,2488,1497,2613,1497,'#f7e7bf',2);ellipse(c,2598,1555,5,4,'#f4e5c3');
    line(c,2482,1496,2619,1496,'#5f9182',2);for(let x=2484;x<2618;x+=8)line(c,x,1482,x,1496,'#efe4c6',.6);line(c,2482,1482,2619,1482,'#f3e7c8',1);
    line(c,2482,1479,2482,1505,'#d0b28a',3);line(c,2619,1479,2619,1505,'#d0b28a',3);
    label(c,'O C E A N   C O U R T S',2550,1658,8,'#ac9972');
  });
  prop(2580,1370,70,73,c=>{
    for(let i=0;i<5;i++){const x=2550+i*13;c.save();c.translate(x,1338);c.rotate((i-2)*.04);ellipse(c,0,0,6,29,['#c98d7d','#e8ce91','#92b5ad','#bea4b3','#eee4be'][i]);line(c,0,-23,0,23,'#f7e9c7',1);c.restore();}
    line(c,2544,1364,2615,1364,'#ae9473',3);line(c,2544,1338,2615,1338,'#bca17b',3);
  });
  for(let y=300;y<1200;y+=210)artBase({x:2470,y:y-30,w:170,h:90},c=>{drawUmbrella(c,2510,y,'#d99283',24);drawUmbrella(c,2600,y+45,'#8cb9b4',21);});

  // PALOMAR: long contour terraces, scattered orchards and a curved lookout trail.
  artBase({x:110,y:3070,w:1450,h:645},c=>{
    for(let i=0;i<5;i++){c.beginPath();c.moveTo(110,3110+i*104);c.bezierCurveTo(215,3050+i*106,315,3140+i*93,395,3100+i*105);c.strokeStyle=i%2?'#abc08c':'#93ab79';c.lineWidth=9;c.stroke();}
    c.beginPath();c.moveTo(1080,3590);c.bezierCurveTo(1090,3655,1160,3680,1280,3650);c.bezierCurveTo(1350,3630,1390,3560,1440,3530);c.strokeStyle='#d6c19a';c.lineWidth=26;c.stroke();
    rounded(c,1164,3580,175,80,20,'#bfa77e');rounded(c,1171,3586,161,68,16,'#dfc6a0');
    for(let x=1178;x<1330;x+=11)line(c,x,3595,x,3650,'#c2a982',1);
    line(c,1180,3654,1325,3654,'#8b8e65',3);for(let x=1185;x<1329;x+=19)line(c,x,3642,x,3656,'#a59770',2);
    label(c,'P A L O M A R   O V E R L O O K',1250,3599,8,'#877b58');
    // Trail markers stay small and rooted in the scenery.
    line(c,1370,3620,1370,3589,'#9f8c66',3);poly(c,[[1360,3591],[1384,3591],[1390,3597],[1384,3603],[1360,3603]],'#d4b48a');
  });
  terrainZones.push({x:1150,y:3580,w:220,h:130,type:'dirt'});
  for(let i=0;i<19;i++){const x=145+(i%4)*65+noise(i)*19,y=3160+Math.floor(i/4)*103+noise(i+31)*17;tree(x,y,'citrus',.7+noise(i+47)*.3);}
  for(const [x,y]of[[599,3250],[612,3380],[1380,3290],[1375,3630],[1105,3690],[612,4050],[1510,3940],[1490,4160]])tree(x,y,'cypress',1.05);
  for(const [x,y]of[[620,3190],[1030,3175],[1125,3620],[1385,3680]])tree(x,y,'jacaranda',.85);
  artBase({x:1010,y:3850,w:450,h:330},c=>{for(let i=0;i<5;i++){const y=3880+i*49;line(c,1040,y,1435,y+31,'#d8c59a',3);for(let x=1050;x<1430;x+=25){ellipse(c,x,y+(x-1040)/395*31,9,6,'#557b49');ellipse(c,x+2,y+(x-1040)/395*31-2,5,3,'#7c985a');}}});

  // CYPRESS: irregular natural water and a shaded forest canopy ring.
  const lake=pools.find(p=>p.kind==='lake');
  if(lake){lake.outline=[];for(let i=0;i<48;i++){const a=i/48*TAU,r=1+Math.sin(a*3)*.065+Math.sin(a*7)*.035;lake.outline.push([lake.x+lake.w/2+Math.cos(a)*(lake.w/2-4)*r, lake.y+lake.h/2+Math.sin(a)*(lake.h/2-4)*r]);}}
  artBase({x:580,y:4420,w:1080,h:960},c=>{
    rect(c,865,4635,610,400,'#7caa73');ellipse(c,1170,4840,318,217,'#88aa77');
    c.beginPath();c.moveTo(675,4760);c.bezierCurveTo(710,4560,1390,4490,1540,4720);c.strokeStyle='#527d57';c.lineWidth=15;c.stroke();
    // The eastern boardwalk is a timber platform, not a vehicle road.
    rect(c,1430,4780,133,89,'#bfaa83');for(let x=1433;x<1562;x+=9)line(c,x,4782,x,4866,'#d9c29c',2);
    line(c,1435,4781,1559,4781,'#8f9973',3);for(let x=1440;x<1562;x+=22)line(c,x,4768,x,4784,'#b2a27e',2);
    label(c,'CYPRESS BOARDWALK',1513,4887,8,'#6b815d');
  });
  for(let i=0;i<54;i++){
    const a=i/54*TAU,rx=500+noise(i+300)*55,ry=300+noise(i+389)*65;
    const x=1170+Math.cos(a)*rx,y=4850+Math.sin(a)*ry;
    if(Math.abs(x-1680)<95||Math.hypot(x-730,y-5320)<135||Math.abs(y-4860)<40&&x>1450)continue;
    tree(x,y,i%3?'canopy':'cypress',.85+noise(i+18)*.4);
  }
  for(const [x,y]of[[1780,4770],[1850,4790],[1920,4815],[2050,4820],[2130,4750],[2135,4940],[1750,5150],[2170,5310]])tree(x,y,'canopy',1.1);

  // SOLSTICE: the wheel gives the midway a skyline no other district has.
  prop(2960,4800,170,214,c=>{
    ellipse(c,2969,4805,70,15,'rgba(57,73,54,.13)');
    const cx=2960,cy=4683,r=82;
    line(c,cx-45,cy+112,cx,cy,'#ba9472',8);line(c,cx+45,cy+112,cx,cy,'#c6a27b',8);
    c.beginPath();c.arc(cx,cy,r,0,TAU);c.strokeStyle='#eacb9b';c.lineWidth=5;c.stroke();
    c.beginPath();c.arc(cx,cy,r-10,0,TAU);c.strokeStyle='#bc9572';c.lineWidth=2;c.stroke();
    for(let i=0;i<12;i++){const a=i/12*TAU,x=cx+Math.cos(a)*r,y=cy+Math.sin(a)*r;line(c,cx,cy,x,y,'#dfba89',2);line(c,x,y,x,y+8,'#a88668',1.5);rounded(c,x-9,y+7,18,13,4,i%2?'#c8826d':'#96b2a0');rect(c,x-6,y+8,12,4,'#e8d2aa');}
    ellipse(c,cx,cy,9,9,'#f0d6a7');ellipse(c,cx,cy,4,4,'#c68d70');
  });
  artBase({x:2445,y:4470,w:580,h:310},c=>{
    for(let x=2475;x<3020;x+=28){poly(c,[[x,4610],[x+16,4610],[x+8,4626]],Math.floor(x/28)%2?'#dc9d71':'#b1bc8a');}
    line(c,2465,4610,3020,4610,'#a18e69',1);
  });
  // Half-pipes and rails frame, rather than block, the existing stunt runway.
  for(const [x,y]of[[2500,5280],[2890,5280]])prop(x,y,78,58,c=>{
    poly(c,[[x-35,y],[x+35,y],[x+35,y-42],[x+17,y-42],[x+9,y-13],[x-9,y-13],[x-17,y-42],[x-35,y-42]],'#c6a27e');
    line(c,x-35,y-42,x-17,y-42,'#e8c89c',4);line(c,x+17,y-42,x+35,y-42,'#e8c89c',4);
    line(c,x-30,y-40,x-30,y-58,'#8d8e73',2);line(c,x-30,y-58,x-17,y-58,'#8d8e73',2);
  });

  // PORT AZUL: towering storage tanks and a high, teal cargo gantry.
  for(const [x,y,r]of[[3375,3595,32],[3455,3595,27]])prop(x,y,r*2,110,c=>{
    ellipse(c,x+15,y+8,r+8,16,'rgba(44,70,59,.17)');rect(c,x-r,y-66,r*2,66,'#b3c4b4');ellipse(c,x,y,r,12,'#9eafa1');ellipse(c,x,y-66,r,12,'#d5dcc0');ellipse(c,x,y-68,r-6,8,'#b7c7b3');line(c,x-r+8,y-61,x-r+8,y-3,'#e0debf',3);for(let yy=y-59;yy<y-3;yy+=8)line(c,x+r-9,yy,x+r-3,yy,'#798f7e',1);
  });
  prop(4655,3575,150,280,c=>{
    poly(c,[[4630,3575],[4642,3575],[4660,3373],[4646,3373]],'#71998d');poly(c,[[4694,3575],[4706,3575],[4674,3373],[4660,3373]],'#507f77');
    rect(c,4607,3355,118,19,'#7ba194');rect(c,4627,3343,53,15,'#a8bba6');rect(c,4633,3347,36,8,'#416e69');
    line(c,4660,3355,4807,3320,'#c9bea0',8);line(c,4660,3330,4807,3320,'#779487',2);line(c,4799,3320,4799,3450,'#678477',1.5);poly(c,[[4794,3450],[4804,3450],[4804,3461],[4794,3461]],'#9e9c7e');
  });
  artBase({x:4800,y:3780,w:175,h:470},c=>{
    rect(c,4810,3790,150,430,'#b6b79f');
    for(let yy=3800;yy<4220;yy+=34){rect(c,4812,yy,11,17,'#5d7c6c');rect(c,4823,yy,11,17,'#d8ba7f');}
    for(let yy=3840;yy<4210;yy+=78){ellipse(c,4947,yy,7,5,'#667c6e');ellipse(c,4947,yy-2,4,3,'#a3af91');}
    label(c,'PORT AZUL · PIER 09',4890,4175,9,'#6b8071');
  });

  // Food carts and bus shelters match activity coordinates, with open foot space.
  const food=[{x:865,y:885,name:'CAFÉ SOL',color:'#c88b62'},{x:1520,y:1715,name:'MIRAGE BAR',color:'#a18caf'},{x:2540,y:1640,name:'SURF SODA',color:'#78a9a3'},{x:2830,y:4705,name:'LUNA TACOS',color:'#cc9470'}];
  for(const f of food)prop(f.x,f.y-28,62,69,c=>{
    const x=f.x,y=f.y-28;ellipse(c,x+8,y+4,28,7,'rgba(52,71,56,.15)');rect(c,x-24,y-25,48,23,f.color);rect(c,x-27,y-28,54,5,'#efd3a5');
    for(const dx of[-22,22])line(c,x+dx,y-26,x+dx,y-52,'#b49e76',2);
    for(let i=0;i<6;i++)poly(c,[[x-30+i*10,y-58],[x-20+i*10,y-58],[x-16+i*10,y-43],[x-26+i*10,y-43]],i%2?'#edd4ad':f.color);
    label(c,f.name,x,y-16,6,'#f6e1bd');ellipse(c,x-17,y,4,4,'#607663');ellipse(c,x+17,y,4,4,'#607663');
  });
  const stops=[['ocean',2404,1500],['havana',996,875],['mirage',1596,1715],['grove',2236,1085],['hills',1596,3600],['downtown',3100,3650],['port',4104,3710],['cypress',1764,4860],['festival',2404,4850],['speedway',4656,4500]];
  for(const [id,x,y]of stops)prop(x+14,y-16,45,58,c=>{
    const n=neighborhoods.find(n=>n.id===id);rect(c,x+18,y-56,4,39,'#8a9c83');rounded(c,x+9,y-69,22,17,3,'#ede4c7');rect(c,x+11,y-67,18,11,n.color);label(c,'S',x+20,y-61,8,'#f9ebcd');
    rect(c,x-23,y-24,25,5,'#bbaa82');line(c,x-20,y-19,x-20,y-13,'#84937b',2);line(c,x-2,y-19,x-2,y-13,'#84937b',2);
  });
  return {neighborhoods,neighborhoodAt};
}
