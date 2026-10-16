// A second postcard: the county below Sunline's original resort district.
// Ground and architecture use the same tiny painted-world vocabulary as the city.
export function buildCounty(api) {
  const {C,rect,line,poly,ellipse,rounded,label,shrub,drawUmbrella,lounge,drawBoat,
    addB,palm,pool,base,prop,grassLot,paving,hedge,shrubs,parking,noise}=api;
  const land = [[80,100],[2650,100],[2650,2775],[2880,2870],[4770,2870],[4970,3040],[4970,5360],[4810,5570],[80,5570]];
  const roadSegments = [];
  const road=(x1,y1,x2,y2,name,width=116)=>roadSegments.push({x1,y1,x2,y2,width,name});
  for(const x of [480,1080,1680,2320])road(x,100,x,2780,'Sunline City');
  for(const y of [480,1160,1840,2520])road(100,y,2410,y,'Sunline City');
  for(const x of [480,1680,2320])road(x,2780,x,5480,x===480?'Cypress Parkway':x===1680?'Palm County Road':'Ocean Expressway');
  for(const x of [3180,4020,4740])road(x,3020,x,x===4020?4340:5480,x===3180?'Grand Avenue':x===4020?'Port Boulevard':'Seabreeze Drive');
  for(const [y,name] of [[3020,'County Line'],[3740,'Grand Boulevard'],[4340,'Cypress Way'],[5480,'South Coast Road']])road(480,y,4740,y,name);
  road(1080,2780,1080,3020,'Sol Connector');
  road(480,3550,750,3550,'Vista Lane',72);
  road(750,3550,1150,3550,'Vista Lane',72);
  road(1150,3550,1450,3410,'Vista Lane',72);
  road(1450,3410,1450,3020,'Vista Lane',72);
  road(2320,4880,3180,4880,'Festival Way',100);
  road(3180,4670,3430,4670,'Speedway Entrance',100);
  const districts=[
    {id:'sunline',name:'SUNLINE CITY',x:80,y:100,w:2570,h:2680,color:'#eac5af'},
    {id:'hills',name:'PALOMAR HILLS',x:80,y:2870,w:1600,h:1470,color:'#b3cba4'},
    {id:'downtown',name:'DOWNTOWN SUNLINE',x:1680,y:2870,w:1500,h:1470,color:'#e9bdac'},
    {id:'port',name:'PORT AZUL',x:3180,y:2870,w:1790,h:1470,color:'#a6c4c1'},
    {id:'cypress',name:'CYPRESS PARK',x:80,y:4340,w:2240,h:1230,color:'#a4c5a0'},
    {id:'festival',name:'SOLSTICE FAIRGROUNDS',x:2320,y:4340,w:860,h:1230,color:'#e6cba8'},
    {id:'raceway',name:'SUNLINE SPEEDWAY',x:3180,y:4340,w:1790,h:1230,color:'#c7c9ae'},
  ];
  const pois=[
    {id:'gunshop',key:'gunshop',x:1990,y:3500,name:'Sunline Sporting Arms'},
    {id:'gunrange',key:'gunrange',x:3770,y:3500,name:'Port Azul Range'},
    {id:'docks',key:'docks',x:4350,y:4100,name:'Port Azul Docks'},
    {id:'race',key:'race',x:3430,y:4670,name:'Sunline Speedway'},
    {id:'taxi',key:'taxi',x:3030,y:3500,name:'Golden Cab Co.'},
    {id:'bounty',key:'bounty',x:4200,y:3100,name:'Port Dispatch'},
    {id:'safehouse',key:'safehouse',x:850,y:3490,name:'Palomar Hideaway'},
    {id:'stunt',key:'stunt',x:2850,y:5060,name:'Solstice Stunt Grounds'},
    {id:'export',key:'export',x:4240,y:3910,name:'Azul Vehicle Exchange'},
    {id:'heist',key:'heist',x:4550,y:4140,name:'Pier Nine'},
    {id:'park',key:'park',x:1720,y:4860,name:'Cypress Lake'},
  ];
  const extraCarSpawns=[
    {x:925,y:3490,angle:0,color:'#e9cf9b',model:'coupe'},
    {x:2050,y:3540,angle:0,color:'#baaaa6',model:'sedan'},
    {x:2910,y:3500,angle:0,color:'#f2cb6d',model:'sedan'},
    {x:2970,y:3500,angle:0,color:'#f2cb6d',model:'sedan'},
    {x:4240,y:3935,angle:0,color:'#b6ccbc',model:'coupe'},
    {x:4370,y:3940,angle:0,color:'#e7b09c',model:'van'},
    {x:3500,y:4470,angle:Math.PI/2,color:'#e77c72',model:'sport'},
    {x:3600,y:4470,angle:Math.PI/2,color:'#eac469',model:'sport'},
    {x:2760,y:4910,angle:0,color:'#b9d0c8',model:'sport'},
    {x:1820,y:5270,angle:0,color:'#c6c6a2',model:'sedan'},
  ];
  const extraTrafficRoutes=[
    [{x:1707,y:3047},{x:3153,y:3047},{x:3153,y:4313},{x:1707,y:4313}],
    [{x:2347,y:3767},{x:4713,y:3767},{x:4713,y:5453},{x:2347,y:5453}],
    [{x:507,y:3767},{x:1653,y:3767},{x:1653,y:5453},{x:507,y:5453}],
    [{x:4047,y:3047},{x:4713,y:3047},{x:4713,y:4313},{x:4047,y:4313}],
    // This loop crosses County Line so north and south share moving traffic.
    [{x:1707,y:1867},{x:2293,y:1867},{x:2293,y:4313},{x:1707,y:4313}],
  ];
  const racePoints=[];
  for(const [cx,cy,start]of[[4365,4745,-Math.PI/2],[4365,5105,0],[3525,5105,Math.PI/2],[3525,4745,Math.PI]]){
    for(let i=0;i<=8;i++){const a=start+i/8*Math.PI/2;racePoints.push({x:cx+Math.cos(a)*105,y:cy+Math.sin(a)*105});}
  }
  const raceTrack={points:racePoints,width:116};
  const stuntRamps=[
    {id:'solstice-launch',name:'Solstice Leap',x:2850,y:5060,w:62,h:92,angle:0},
    {id:'port-launch',name:'Pier Nine Gap',x:4530,y:3550,w:62,h:92,angle:0},
    {id:'cypress-launch',name:'Cypress Flyer',x:730,y:5320,w:62,h:92,angle:0},
    {id:'airfield-launch',name:'Runway Hop',x:340,y:2300,w:62,h:92,angle:Math.PI/2},
    {id:'ocean-launch',name:'Ocean Air',x:2530,y:1250,w:62,h:92,angle:Math.PI/2},
    {id:'grove-launch',name:'The Grove Leap',x:250,y:5200,w:62,h:92,angle:Math.PI/2},
  ];

  // County terrain is painted in broad, irregular panels, never a repeated tile.
  const drawTerrain=c=>{
    c.save();c.beginPath();land.forEach(([x,y],i)=>i?c.lineTo(x,y):c.moveTo(x,y));c.closePath();c.clip();
    rect(c,80,2780,4890,2790,'#71a076');
    poly(c,[[80,2780],[2635,2780],[2840,2880],[4770,2880],[4955,3050],[4955,5360],[4795,5555],[80,5555]],'#729d70');
    poly(c,[[80,2940],[350,2875],[1000,2940],[1510,3070],[1580,3820],[1170,4100],[500,4210],[80,4100]],'#72946a');
    poly(c,[[80,3060],[280,2920],[480,3100],[730,2960],[1030,3140],[1280,3070],[1480,3260],[1450,3470],[80,3460]],'#648763');
    poly(c,[[80,3240],[290,3070],[490,3280],[660,3115],[860,3330],[1040,3200],[1320,3410],[1430,3540],[80,3620]],'#84a478');
    for(let i=0;i<17;i++){const x=120+noise(i+640)*1390,y=3080+noise(i+831)*1050;ellipse(c,x,y,60+noise(i)*70,30+noise(i+5)*30,'rgba(43,100,66,.08)');}
    rect(c,1760,3100,1330,1160,'#cfceb5');
    rect(c,3260,3100,1400,1160,'#bdc4b5');
    rect(c,3280,4420,1380,980,'#c7c6ac');
    // Broad sand ribbon gives Port Azul its own rugged, southern shoreline.
    poly(c,[[4770,2880],[4970,3040],[4970,5360],[4810,5570],[80,5570],[80,5535],[4770,5535],[4900,5330],[4900,3090],[4720,2940],[2880,2940],[2630,2820]],'#dfd0ac');
    for(let y=3120;y<5350;y+=37){line(c,4920,y,4952,y+8,'rgba(249,238,204,.48)',2);}
    c.restore();
  };

  // PALOMAR HILLS: a winding residential drive, a hideaway, winery and observatory.
  paving(630,3280,710,220,'#d5cfb1');
  addB(690,3310,280,103,37,'#e1b39e','PALOMAR HIDEAWAY',{style:'villa'});
  pool(1070,3310,170,78);paving(1056,3296,198,106);
  addB(1120,3180,212,86,34,'#d9c6a1','',{style:'villa'});
  hedge(660,3260,370,10);shrubs(1000,3425,10);
  palm(625,3450,82);palm(1000,3450,86);palm(1345,3425,76);palm(1072,3250,72);
  base(c=>{for(let i=0;i<4;i++)lounge(c,1087+i*40,3413,Math.PI);drawUmbrella(c,1268,3330,'#e5bd9f',19);label(c,'V I S T A   L A N E',900,3531,8,'#e1dec6');});
  // An intimate west-country vineyard, visible between the bends in the road.
  grassLot(650,3850,800,370,'#87a271');
  addB(730,3870,240,96,40,'#e0c8a4','SOL VINEYARDS',{style:'villa'});
  base(c=>{
    for(let y=4010;y<4190;y+=34)for(let x=680;x<1440;x+=28){ellipse(c,x,y,8,6,'#39704f');line(c,x,y+6,x,y+11,'#a48e66',1);}
    rect(c,655,3980,790,12,'#d5c3a0');
    label(c,'P A L O M A R   H I L L S',1130,4145,14,'#e7dfc1');
  });
  for(const [x,y] of [[640,3950],[1010,3970],[1460,4200],[620,4200]])palm(x,y,66);
  paving(140,3850,220,250,'#d6d2b7');
  addB(164,3890,170,110,42,'#d9d0b5','PALOMAR OBSERVATORY',{roof:'#eee5ce'});
  prop(250,3970,110,130,c=>{ellipse(c,250,3864,58,27,'#ece2c7');rect(c,194,3864,112,23,'#e2d5b7');ellipse(c,250,3864,53,22,'#f4e9ce');line(c,239,3843,252,3880,'#879b90',4);});

  // DOWNTOWN: varied civic towers, covered arcades, rooftop terraces and plazas.
  paving(1775,3140,1340,480,'#dfd7c1');
  addB(1830,3200,300,145,76,'#e3b6a2','SUNLINE SPORTING ARMS',{awning:'#b98e77',roof:'#ead9be'});
  addB(2180,3220,80,162,96,'#b8ccbd','SOL 01',{roof:'#e8e3c8'});
  parking(1795,3470,420,122);
  // County bank and its open marble forecourt.
  addB(2420,3190,260,168,116,'#c0d1c5','BANCO DEL SOL',{roof:'#ece7cf',tower:true});
  base(c=>{
    for(let x=2427;x<2674;x+=31){rect(c,x,3319,12,46,'#f5e5c9');rect(c,x+3,3320,4,40,'#dfcbb0');}
    ellipse(c,2540,3502,75,27,'#c0bea8');ellipse(c,2540,3497,67,24,'#eae0c7');ellipse(c,2540,3497,48,16,'#55a49a');
    line(c,2540,3496,2540,3467,'#e2d8b7',7);ellipse(c,2540,3465,18,8,'#e9dcb8');
  });
  addB(2780,3210,255,146,61,'#e9cb9d','GOLDEN CAB CO.',{style:'garage',signColor:'#f3d88c',ink:'#756747'});
  parking(2780,3405,330,188);palm(2740,3505,82);palm(3098,3380,85);
  for(const x of [1785,2150,2715,3100])palm(x,3622,82);
  for(const [x,y] of [[1790,3160],[2310,3155],[2745,3170],[3095,3165]])palm(x,y,72);
  paving(1775,3850,1340,410,'#e3dac2');
  addB(1790,3900,195,180,133,'#cebbb5','THE ROSEWOOD',{roof:'#e4dace'});
  addB(2040,3905,200,120,86,'#d9c6a4','SUNLINE ARCADE',{awning:'#bc9c76'});
  addB(2410,3910,180,184,148,'#bad1ca','SOL TOWER',{roof:'#d8e0ce',tower:true});
  addB(2700,3900,330,128,71,'#e1bdb0','PACIFICA CINEMA',{awning:'#b58183'});
  base(c=>{
    for(let i=0;i<3;i++){const x=2730+i*95;rect(c,x,3960,66,63,['#567f85','#cb907a','#87a384'][i]);label(c,['HIGH TIDE','SOLSTICE','OPEN ROAD'][i],x+33,3995,7,'#f9e6c6');}
    for(let i=0;i<4;i++){drawUmbrella(c,2085+i*41,4130,'#dbaa97',15);ellipse(c,2085+i*41,4136,8,4,'#e8d7b5');}
    rounded(c,2700,4125,330,74,12,'#c6d0b2');
    label(c,'D O W N T O W N   S U N L I N E',2520,4223,12,'#617c6f');
  });
  shrubs(1790,4220,11);shrubs(2730,4218,12);
  for(const [x,y] of [[1775,4080],[2270,4170],[2640,4110],[3090,4150]])palm(x,y,81);

  // PORT AZUL: shooting range, customs sheds, container yards and a working pier.
  paving(3290,3160,610,480,'#d5d1ba');
  addB(3315,3200,170,120,40,'#c0cbbb','PORT AZUL RANGE',{style:'garage'});
  base(c=>{
    rect(c,3520,3195,345,224,'#c0b69a');rect(c,3520,3195,345,18,'#9da893');
    for(let x=3530;x<3860;x+=65){line(c,x,3220,x,3410,'#e7d9b6',2);rect(c,x+12,3235,28,23,'#dbd3b5');label(c,String(1+Math.round((x-3530)/65)).padStart(2,'0'),x+26,3247,11,'#7b8872');}
    rect(c,3520,3425,345,14,'#a29f85');
    label(c,'E Y E S   O N   T A R G E T',3690,3450,8,'#7f8066');
    for(let x=3530;x<3850;x+=70){rect(c,x,3470,42,5,'#c3b08f');}
  });
  parking(3320,3480,170,125);palm(3300,3570,74);palm(3920,3605,68);
  paving(4110,3100,550,550,'#d1d0bb');
  addB(4190,3210,380,170,46,'#abc1b8','PORT AUTHORITY',{style:'hangar',roof:'#d5d5bf'});
  addB(4110,3420,205,110,36,'#d1c0a1','CUSTOMS 04',{style:'garage'});
  parking(4330,3410,320,130);
  base(c=>{label(c,'PORT DISPATCH',4200,3137,12,'#74887c');for(let y=3108;y<3170;y+=14)line(c,4605,y,4646,y,'#b8ad8c',4);});
  for(const p of [[4120,3165],[4650,3165],[4105,3600]])palm(...p,67);
  // Flat roofs and stacked, sun-bleached shipping containers form readable cover.
  paving(3270,3830,1410,420,'#c8c8b3');
  addB(3310,3890,480,215,53,'#b9c7b7','AZUL FREIGHT TERMINAL',{style:'hangar',roof:'#d5d3bc'});
  parking(3300,4145,500,75);
  addB(4110,3810,470,60,38,'#d6c3a6','AZUL VEHICLE EXCHANGE',{style:'garage'});
  const container=(x,y,w,h,color,name='')=>addB(x,y,w,h,26,color,name,{style:'hangar',roof:color,roofColor:color});
  container(4140,4005,125,52,'#ba8777','AZUL');container(4410,3995,155,50,'#8fa99a','SLC');
  container(4140,4170,130,52,'#b5bca0','09');container(4410,4200,155,44,'#c5aa83','CARGO');
  container(4600,4020,68,83,'#8fa5a1','');
  base(c=>{
    for(let y=3940;y<4240;y+=37){line(c,4100,y,4125,y,'#e2dbb7',3);line(c,4675,y,4698,y,'#e2dbb7',3);}
    label(c,'P I E R   N I N E',4490,4110,13,'#8b937d');
    rect(c,4800,3940,170,225,'#b5b69f');for(let y=3943;y<4165;y+=12)line(c,4800,y,4970,y,'#979d88',1);
    drawBoat(c,5050,3980,1.75,'#ede3c7',0);drawBoat(c,5060,4250,2.1,'#d9d7bf',Math.PI);
  });
  // Two slender dock cranes have raised booms, drawn above the yard with clear feet.
  for(const [x,y] of [[4825,3870],[4825,4260]])prop(x,y,140,155,c=>{
    poly(c,[[x-9,y],[x+6,y],[x+19,y-118],[x+7,y-118]],'#a19772');
    line(c,x+12,y-118,x+135,y-136,'#d9c49a',7);line(c,x+12,y-118,x-26,y-134,'#d9c49a',6);
    line(c,x+10,y-152,x+135,y-136,'#667e75',1);line(c,x+10,y-152,x-25,y-134,'#667e75',1);line(c,x+10,y-152,x+12,y-118,'#bfad86',3);
    line(c,x+125,y-137,x+125,y-64,'#5e746d',1);line(c,x+125,y-64,x+132,y-61,'#5e746d',3);
    rect(c,x-3,y-115,28,19,'#c8b68f');rect(c,x+6,y-111,15,8,'#648583');
  });

  // CYPRESS PARK: a lake ring, botanic conservatory, picnic lawns and a bandstand.
  grassLot(590,4440,1620,950,'#77a16b');
  base(c=>{
    ellipse(c,1200,4870,585,345,'#d3c5a6');ellipse(c,1200,4870,557,320,'#7caa73');
    rect(c,550,4848,1590,30,'#d8c9a8');rect(c,1660,4445,30,910,'#d8c9a8');
    for(let x=780;x<1540;x+=100){drawUmbrella(c,x,4475,'#dbb797',18);ellipse(c,x,4483,10,5,'#c9b28b');}
    for(let i=0;i<24;i++){const a=i/24*Math.PI*2,x=1200+Math.cos(a)*540,y=4870+Math.sin(a)*303;shrub(c,x,y,14,i%4===0);}
  });
  pool(900,4670,540,330,'lake');
  base(c=>{
    rounded(c,870,4640,600,390,95,'#8bac77');rounded(c,888,4658,564,354,86,'#c3caa5');
    label(c,'C Y P R E S S   L A K E',1170,5060,13,'#4d775f');
    rect(c,1440,4808,86,42,'#c6b391');for(let x=1444;x<1524;x+=8)line(c,x,4810,x,4848,'#ac9c80',1);
    drawBoat(c,1478,4883,.5,'#e9d9b6',Math.PI/2);
  });
  addB(1770,4510,340,140,48,'#bed0b8','CYPRESS CONSERVATORY',{roof:'#9fbbad',roofColor:'#b4d2c2'});
  base(c=>{for(let x=1790;x<2090;x+=24)line(c,x,4475,x,4593,'rgba(244,232,196,.7)',2);for(let y=4490;y<4590;y+=25)line(c,1784,y,2095,y,'rgba(244,232,196,.7)',2);});
  paving(1790,5010,300,260,'#d7cdaf');
  addB(1820,5050,240,80,30,'#e5c7a2','CYPRESS CAFÉ',{awning:'#a5b99a'});
  base(c=>{for(let y=5160;y<5250;y+=55)for(let x=1835;x<2070;x+=65){drawUmbrella(c,x,y,'#e2bda1',18);ellipse(c,x,y+7,9,5,'#c0ab87');}});
  parking(1780,5280,330,85);
  for(const p of [[630,4515],[800,4570],[1520,4560],[1570,5160],[780,5180],[1650,5270],[1750,4720],[2160,4750],[2170,5220]])palm(...p,77,1.05);
  // Western meadow with an open amphitheatre and landmark cypresses.
  base(c=>{
    ellipse(c,250,4730,140,170,'#d8ccad');ellipse(c,250,4730,110,140,'#7d9c66');
    for(let i=0;i<4;i++){c.beginPath();c.ellipse(250,4730,85+i*18,106+i*17,0,-1.1,1.1);c.strokeStyle='#c5ba95';c.lineWidth=7;c.stroke();}
    rect(c,185,4700,70,66,'#cfc2a0');label(c,'THE GROVE',220,4787,9,'#5d7f61');
  });
  for(let i=0;i<18;i++){const x=125+noise(i+450)*260,y=4450+noise(i+600)*900;prop(x,y,26,63,c=>{rect(c,x-2,y-37,4,37,'#827856');ellipse(c,x,y-34,14,31,'#32694c');ellipse(c,x-3,y-40,8,24,'#4b8056');});}

  // SOLSTICE: food trucks, striped tents, an open stunt runway, and a skate bowl.
  grassLot(2420,4440,650,940,'#a1ad77');
  paving(2460,4470,580,320,'#d7caac');
  for(let i=0;i<3;i++){
    const x=2480+i*183;
    addB(x,4510,140,65,30,['#e3b29c','#c8d3af','#b6ccc1'][i],['MANGO CLUB','SOL SODA','LUNA TACOS'][i],{awning:['#c98772','#8caa77','#74a49a'][i],style:'garage'});
  }
  base(c=>{
    for(let x=2500;x<3040;x+=92){drawUmbrella(c,x,4660,'#dfb994',23);ellipse(c,x,4670,13,7,'#c3ad87');}
    label(c,'S O L S T I C E',2750,4735,25,'#a18368');label(c,'THE COUNTY NEVER SLEEPS',2750,4770,9,'#95866a');
    rounded(c,2470,5200,530,140,52,'#c6bc9f');rounded(c,2484,5214,502,112,45,'#a8b4a0');rounded(c,2500,5230,470,80,34,'#8fa597');
    for(let i=0;i<5;i++){line(c,2490+i*98,5233,2510+i*98,5297,'#d9d1b4',3);}
    // The stunt apron remains completely open across the landing corridor.
    rect(c,2430,4970,670,176,'#d2c3a2');
    for(let x=2450;x<3090;x+=35)rect(c,x,5058,18,3,'#eee0be');
    label(c,'S O L S T I C E   A I R',2680,5122,10,'#9a886c');
  });
  palm(2440,4790,77);palm(3080,4780,82);palm(2460,5180,69);palm(3085,5370,73);

  // SPEEDWAY: a banked oval, paddock and terraced grandstand. The entire track is driveable.
  base(c=>{
    rounded(c,3320,4530,1250,800,160,'#ddd0ae');
    rounded(c,3360,4580,1170,690,138,'#748580');
    rounded(c,3480,4700,930,450,92,'#9eaf7d');
    c.beginPath();c.roundRect(3420,4640,1050,570,105);c.strokeStyle='#ddd6b4';c.lineWidth=2;c.setLineDash([17,15]);c.stroke();c.setLineDash([]);
    c.beginPath();c.roundRect(3370,4590,1150,670,133);c.strokeStyle='#e9d8b4';c.lineWidth=4;c.stroke();
    c.beginPath();c.roundRect(3473,4693,944,464,98);c.strokeStyle='#e9d8b4';c.lineWidth=4;c.stroke();
    for(let y=4625;y<4720;y+=12)for(let x=3410;x<3450;x+=12)rect(c,x,y,12,12,((x-3410)/12+(y-4625)/12)%2<1?'#e9e2c9':'#526e69');
    label(c,'S U N L I N E',3945,4880,47,'#e3ddba');label(c,'S P E E D W A Y',3945,4930,26,'#d6d4ad');
    ellipse(c,3950,5015,89,25,'#b5ba8a');label(c,'EST. 1987',3950,5015,12,'#78956a');
    for(let i=0;i<6;i++){rect(c,3540,5350+i*8,800,5,i%2?'#dbcaa6':'#c2b893');}
    label(c,'GRANDSTAND',3940,5405,10,'#8c8b71');
  });
  addB(3660,4440,500,70,35,'#c6ceac','SUNLINE MOTOR CLUB',{style:'garage',roof:'#ded8bb'});
  parking(3320,4410,300,105);parking(4220,4410,390,105);
  for(const p of [[3320,4515],[4610,4515],[4630,5270],[3280,5280]])palm(...p,83);
  // A quiet boardwalk frames the distant horizon, with a small lighthouse at the point.
  base(c=>{rect(c,4820,4440,55,890,'#dfd0ad');for(let y=4440;y<5330;y+=13)line(c,4822,y,4873,y,'#c4b794',1);for(let y=4490;y<5290;y+=200){drawUmbrella(c,4890,y,'#d9b29b',22);lounge(c,4900,y+27,0);}});
  prop(4800,5430,80,125,c=>{
    poly(c,[[4775,5430],[4825,5430],[4815,5335],[4785,5335]],'#efe5c8');
    poly(c,[[4779,5405],[4821,5405],[4818,5383],[4782,5383]],'#c98174');
    rect(c,4780,5324,40,22,'#628f8c');rect(c,4775,5320,50,5,'#f2e7c9');poly(c,[[4772,5320],[4800,5306],[4828,5320]],'#bd8574');
    for(let x=4785;x<4820;x+=9)line(c,x,5325,x,5344,'#ede1c4',2);ellipse(c,4800,5430,24,7,'#d5ccb0');
  });

  // Roads sit above terrain and below every lot, added via drawCountyRoads at bake time.
  // Ramps are unmistakable cream/coral wedges with an unobstructed runway ahead.
  for(const r of stuntRamps)base(c=>{
    c.save();c.translate(r.x,r.y);c.rotate(r.angle);
    rect(c,-60,-60,120,120,'#c7b795');
    poly(c,[[-r.w/2,-r.h/2],[r.w/2,-r.h/2-15],[r.w/2,r.h/2-15],[-r.w/2,r.h/2]],'#efce9e');
    poly(c,[[r.w/2,-r.h/2-15],[r.w/2+8,-r.h/2],[r.w/2+8,r.h/2],[r.w/2,r.h/2-15]],'#b58b6a');
    for(let y=-r.h/2+4;y<r.h/2;y+=17){poly(c,[[-22,y],[-5,y-5],[17,y-5],[0,y]],'#c67f6e');}
    poly(c,[[-5,-10],[10,0],[-5,10]],'#fff2cf');
    c.restore();
  });
  return {drawTerrain,mapLand:[land],roadSegments,districts,pois,extraCarSpawns,extraTrafficRoutes,stuntRamps,raceTrack};
}
