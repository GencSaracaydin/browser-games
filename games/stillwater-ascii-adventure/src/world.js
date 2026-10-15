import * as THREE from 'three';

// An intentionally small, hand-composed landscape. All decorative scattering is
// seeded, so revisiting the valley preserves the landmarks and the paths.
export function createWorld(scene) {
  const root = new THREE.Group();
  root.name = 'Stillwater valley';
  scene.add(root);
  let seed = 892731;
  const random = () => { seed = (1664525 * seed + 1013904223) >>> 0; return seed / 4294967296; };
  const range = (a, b) => a + random() * (b - a);
  let detailSeed = 72931;
  const detailRandom = () => { detailSeed = (1664525 * detailSeed + 1013904223) >>> 0; return detailSeed / 4294967296; };
  const detailRange = (a, b) => a + detailRandom() * (b - a);
  const clamp = THREE.MathUtils.clamp;
  const smooth = (a, b, x) => { const t = clamp((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t); };
  const waterLevel = 0.035;
  const bounds = { minX: -31, maxX: 31, minZ: -30, maxZ: 30 };
  const spawn = { x: 0, z: 13 };
  const interactables = [];
  const waterMeshes = [];
  const collisions = [];
  const batches = new Map();
  const dummy = new THREE.Object3D();
  const disposables = new Set();
  const animated = [];
  const wind = { value: 0 };
  const mats = {
    stone: new THREE.MeshStandardMaterial({ color: '#a6b6aa', roughness: 0.94 }),
    darkStone: new THREE.MeshStandardMaterial({ color: '#475854', roughness: 1 }),
    grass: new THREE.MeshStandardMaterial({ color: '#87b46c', roughness: 1, side: THREE.DoubleSide, vertexColors: true }),
    leaf: new THREE.MeshStandardMaterial({ color: '#577b5f', roughness: 1, flatShading: true, vertexColors: true }),
    bark: new THREE.MeshStandardMaterial({ color: '#484e42', roughness: 1 }),
    gold: new THREE.MeshStandardMaterial({ color: '#c0a56a', metalness: 0.4, roughness: 0.55 }),
    darkMetal: new THREE.MeshStandardMaterial({ color: '#333f3d', metalness: 0.4, roughness: 0.65 }),
    flower: new THREE.MeshStandardMaterial({ color: '#bdd3b4', emissive: '#46614a', emissiveIntensity: 0.12, roughness: 0.9 }),
    glow: new THREE.MeshBasicMaterial({ color: '#f1df9b' }),
    echo: new THREE.MeshStandardMaterial({ color: '#e3ecc2', emissive: '#bedb83', emissiveIntensity: 2.4, roughness: 0.35, metalness: 0.25 }),
    moss: new THREE.MeshStandardMaterial({ color: '#667c50', roughness: 1 }),
  };
  Object.values(mats).forEach(m => disposables.add(m));
  function textureNoise(repeat, contrast) {
    const size=64, data=new Uint8Array(size*size*4);
    for(let y=0;y<size;y++) for(let x=0;x<size;x++) {
      const coarse=Math.sin(x*.7+Math.sin(y*.39))*Math.cos(y*.53+x*.13);
      const grain=Math.sin(x*17.3+y*29.8)*Math.cos(x*7.2-y*13.6);
      const value=Math.round(255*Math.max(.35,Math.min(1,.84+coarse*contrast+grain*.07)));
      const i=(y*size+x)*4;data[i]=value;data[i+1]=value;data[i+2]=value;data[i+3]=255;
    }
    const texture=new THREE.DataTexture(data,size,size,THREE.RGBAFormat);
    texture.wrapS=texture.wrapT=THREE.RepeatWrapping;
    texture.minFilter=THREE.LinearMipmapLinearFilter;texture.magFilter=THREE.LinearFilter;
    texture.generateMipmaps=true;texture.repeat.set(repeat,repeat);texture.needsUpdate=true;
    disposables.add(texture);return texture;
  }
  mats.stone.map=textureNoise(1.3,.2);
  mats.darkStone.map=textureNoise(1,.2);
  mats.bark.map=textureNoise(1,.25);
  const box = new THREE.BoxGeometry(1, 1, 1);
  const cylinder = new THREE.CylinderGeometry(1, 1, 1, 7);
  const pebble = new THREE.IcosahedronGeometry(1, 1);
  const flowerGeo = new THREE.IcosahedronGeometry(1, 0);
  const stemGeo = new THREE.CylinderGeometry(1, 1, 1, 3);
  const leafGeo = new THREE.IcosahedronGeometry(1, 1);
  const rockPositions = pebble.attributes.position;
  for (let i = 0; i < rockPositions.count; i++) {
    const x = rockPositions.getX(i), y = rockPositions.getY(i), z = rockPositions.getZ(i);
    const rough = 0.88 + 0.15 * Math.sin(x * 8 + z * 4) * Math.cos(y * 7 - x * 3);
    rockPositions.setXYZ(i, x * rough + y * 0.14, Math.max(-0.66, y * rough), z * rough);
  }
  pebble.computeVertexNormals();
  const leafColors = [];
  const leafPositions = leafGeo.attributes.position;
  for (let i = 0; i < leafPositions.count; i++) {
    const x = leafPositions.getX(i), y = leafPositions.getY(i), z = leafPositions.getZ(i);
    const light = 0.7 + 0.25 * Math.sin(x * 9 + z * 6) * Math.cos(y * 8);
    leafColors.push(light * 0.88, light, light * 0.9);
    const rough = 0.93 + 0.16 * Math.sin(x * 11 + z * 5) * Math.cos(y * 9);
    leafPositions.setXYZ(i, x * rough, y * rough, z * rough);
  }
  leafGeo.setAttribute('color', new THREE.Float32BufferAttribute(leafColors, 3));
  leafGeo.computeVertexNormals();
  [box, cylinder, pebble, leafGeo, flowerGeo, stemGeo].forEach(g => disposables.add(g));

  function batch(key, geometry, material, x, y, z, sx, sy, sz, rx = 0, ry = 0, rz = 0, color = null) {
    if (!batches.has(key)) batches.set(key, { geometry, material, transforms: [], colors: [] });
    dummy.position.set(x, y, z);
    dummy.rotation.set(rx, ry, rz);
    dummy.scale.set(sx, sy, sz);
    dummy.updateMatrix();
    const data = batches.get(key);
    data.transforms.push(dummy.matrix.clone());
    data.colors.push(color ? new THREE.Color(color) : new THREE.Color(1, 1, 1));
  }
  function cube(x, y, z, sx, sy, sz, material = mats.stone, rotation = 0, tint = null) {
    const materialName = Object.keys(mats).find(key => mats[key] === material) || 'stone';
    if (!tint && material === mats.stone) tint = new THREE.Color().setHSL(detailRange(0.29, 0.37), detailRange(0.03, 0.09), detailRange(0.65, 0.97));
    batch(`${materialName}-stonework`, box, material, x, y, z, sx, sy, sz, 0, rotation, 0, tint);
  }
  function discCollider(x, z, r) { collisions.push({ x, z, r }); }
  function boxCollider(x, z, w, d) { collisions.push({ x, z, w, d }); }

  function shoreRadius(theta) { return 1 + 0.055 * Math.sin(theta * 3 + 0.5) + 0.038 * Math.cos(theta * 7); }
  function pondDistance(x, z) {
    const theta = Math.atan2(z / 8.2, x / 10.6);
    return Math.hypot(x / 10.6, z / 8.2) / shoreRadius(theta);
  }
  function groundHeight(x, z) {
    const d = pondDistance(x, z);
    const basin = -0.3 * (1 - smooth(0.81, 1.045, d));
    // Three broad steps lead to the sanctuary. Everything else stays level,
    // making the visual ground and movement surface agree exactly.
    if (Math.abs(x) < 4.25 && z < -19.2 && z > -25.5) {
      return z < -20.8 ? 0.45 : z < -20 ? 0.3 : 0.15;
    }
    return basin;
  }
  function isWater(x, z) { return pondDistance(x, z) < 1.015; }
  function canWalk(x, z) {
    if (x < bounds.minX || x > bounds.maxX || z < bounds.minZ || z > bounds.maxZ) return false;
    const clearance = 0.3;
    return !collisions.some(c => c.r !== undefined
      ? Math.hypot(x - c.x, z - c.z) < c.r + clearance
      : Math.abs(x - c.x) < c.w / 2 + clearance && Math.abs(z - c.z) < c.d / 2 + clearance);
  }

  const terrainGeo = new THREE.PlaneGeometry(70, 70, 150, 150);
  terrainGeo.rotateX(-Math.PI / 2);
  const vertices = terrainGeo.attributes.position;
  const colors = [];
  const groundColor = new THREE.Color();
  for (let i = 0; i < vertices.count; i++) {
    const x = vertices.getX(i), z = vertices.getZ(i), d = pondDistance(x, z);
    // Sanctuary steps are separate meshes, avoiding triangles spanning risers.
    vertices.setY(i, Math.min(0, groundHeight(x, z)));
    const pattern = Math.sin(x * 0.72) * Math.cos(z * 0.54) * 0.035 + Math.sin(x * 3.7 + z * 0.3) * Math.cos(z * 4.9) * 0.025 + random() * 0.035;
    if (d < 0.91) groundColor.setHSL(0.44, 0.21, 0.13 + pattern);
    else if (d < 1.14) groundColor.setHSL(0.36, 0.14, 0.155 + pattern);
    else groundColor.setHSL(0.35, 0.25, 0.135 + pattern);
    colors.push(groundColor.r, groundColor.g, groundColor.b);
  }
  terrainGeo.setAttribute('color', new THREE.Float32BufferAttribute(colors, 3));
  terrainGeo.computeVertexNormals();
  const terrainMat = new THREE.MeshStandardMaterial({ vertexColors: true, roughness: 1, map: textureNoise(18,.28) });
  const terrain = new THREE.Mesh(terrainGeo, terrainMat);
  terrain.receiveShadow = true;
  terrain.name = 'Meadow and shallow pond basin';
  root.add(terrain);
  disposables.add(terrainGeo); disposables.add(terrainMat);

  // Shape coordinates use y = -world z. Rotate the water mesh -PI/2 about X.
  const pondShape = new THREE.Shape();
  for (let i = 0; i <= 128; i++) {
    const a = i / 128 * Math.PI * 2, r = shoreRadius(a) * 1.012;
    const x = Math.cos(a) * 10.6 * r, z = Math.sin(a) * 8.2 * r;
    if (i === 0) pondShape.moveTo(x, -z); else pondShape.lineTo(x, -z);
  }
  const waterGeometry = new THREE.ShapeGeometry(pondShape, 128);
  disposables.add(waterGeometry);

  // Forgotten flagstone paths connect all four cardinal landmarks.
  function flagstone(x, z, size = 1) {
    const y = groundHeight(x, z);
    cube(x, y + 0.025, z, range(0.7, 1.2) * size, 0.065, range(0.65, 1.05) * size, mats.darkStone, range(-0.12, 0.12), '#b9c7b4');
    if (random() > 0.5) cube(x + range(-0.2, 0.2), y + 0.062, z, 0.22 * size, 0.016, 0.45 * size, mats.moss, range(-0.3, 0.3));
  }
  for (let z = 10; z < 28; z += 1.25) {
    flagstone(Math.sin(z * 0.3) * 0.45 - 0.5, z, 0.94);
    if (z < 18) flagstone(0.7 + Math.sin(z * 0.3) * 0.35, z + 0.2, 0.85);
  }
  for (let z = -10.3; z > -19; z -= 1.3) {
    flagstone(-0.65, z, 1.12); flagstone(0.62, z - 0.16, 1.05);
  }
  for (let i = 0; i < 19; i++) {
    const a = i / 18 * Math.PI;
    for (const side of [-1, 1]) {
      const x = side * (12.4 + 0.6 * Math.sin(a)), z = 9.6 - i * 1.05;
      if (i % 3 !== 1) flagstone(x, z, 0.8);
    }
  }

  function column(x, z, h = 5.5, r = 0.52, broken = false) {
    cube(x, 0.14, z, r * 3, 0.28, r * 3);
    cube(x, 0.4, z, r * 2.4, 0.25, r * 2.4);
    const courses = Math.max(2, Math.ceil((h - 0.5) / 0.6));
    const courseHeight = (h - 0.5) / courses;
    for (let i = 0; i < courses; i++) {
      batch('columns', cylinder, mats.stone, x + (i === courses - 1 && broken ? 0.035 : 0), 0.5 + (i + 0.5) * courseHeight, z,
        r * detailRange(0.98, 1.025), courseHeight - 0.022, r * detailRange(0.98, 1.025), 0, Math.PI / 8, 0,
        new THREE.Color().setHSL(0.34, 0.04, detailRange(0.68, 0.99)));
    }
    for (let y = 1.15; y < h - 0.4; y += 1.1) batch('column-bands', cylinder, mats.darkStone, x, y, z, r * 1.023, 0.045, r * 1.023);
    if (!broken) {
      cube(x, h, z, r * 2.65, 0.28, r * 2.65);
      cube(x, h + 0.2, z, r * 3, 0.16, r * 2.8);
    } else cube(x + 0.07, h, z - 0.05, r * 1.7, 0.12, r * 1.5, mats.moss, 0.2);
    discCollider(x, z, r * 1.15);
  }
  function arch(x, z, width, spring, rise, thickness = 0.65, depth = 0.9, rotation = 0) {
    // Pointed arches are built as individual voussoirs, preserving their block
    // edges in the ASCII render and allowing plants to hang between stones.
    const segments = 18;
    for (let i = 0; i < segments; i++) {
      const t = (i + 0.5) / segments;
      const localX = (t - 0.5) * width;
      const localY = spring + rise * Math.sin(Math.PI * t);
      const slope = rise * Math.PI / width * Math.cos(Math.PI * t);
      const px = x + localX * Math.cos(rotation), pz = z - localX * Math.sin(rotation);
      batch('arch-stones', box, mats.stone, px, localY, pz,
        width / segments * 1.06 * Math.sqrt(1 + slope * slope), thickness, depth,
        0, rotation, Math.atan(slope), new THREE.Color().setHSL(0.34, 0.05, detailRange(0.62, 0.95)));
    }
    for (const sign of [-1, 1]) column(x + sign * width / 2 * Math.cos(rotation), z - sign * width / 2 * Math.sin(rotation), spring - 0.02, thickness * 0.7);
  }
  function masonryWall(x,z,width,height,depth) {
    const acrossZ=depth>width, length=acrossZ?depth:width;
    const rows=Math.max(1,Math.ceil(height/.57)), rowHeight=height/rows;
    const pitch=length/Math.ceil(length/.95);
    for(let row=0;row<rows;row++) {
      const offset=row%2?pitch*.5:0;
      for(let start=-length/2-offset;start<length/2;start+=pitch) {
        const a=Math.max(-length/2,start),b=Math.min(length/2,start+pitch);
        if(b-a<.06)continue;
        const h=row===rows-1?rowHeight*detailRange(.68,1.05):rowHeight;
        const along=(a+b)/2;
        cube(x+(acrossZ?0:along),row*rowHeight+h/2,z+(acrossZ?along:0),
          acrossZ?width: b-a-.025,h-.022,acrossZ?b-a-.025:depth,mats.stone,detailRange(-.012,.012));
      }
    }
  }

  // The abbey faces the water. Its open nave frames the altar and the moon.
  arch(0, -12.8, 6, 4.3, 2.8, 0.8, 1.1);
  arch(-7.4, -13.2, 3.8, 3.55, 1.8, 0.61, 0.9);
  arch(7.4, -13.2, 3.8, 3.55, 1.8, 0.61, 0.9);
  arch(0, -22.8, 5.4, 5.5, 2.7, 0.75, 0.9);
  arch(-9.25, -18.2, 5.8, 4.25, 2.4, 0.64, 0.8, Math.PI / 2);
  arch(9.25, -18.2, 5.8, 4.25, 2.4, 0.64, 0.8, Math.PI / 2);
  for (const side of [-1, 1]) {
    for (let j = 0; j < 5; j++) {
      const z = -14.4 - j * 1.9;
      const height = j === 1 ? 1.15 : j === 2 ? 1.65 : 2.8 + 0.4 * Math.sin(j * 2 + side);
      masonryWall(side * 9.8,z,0.72,height,1.85);
      cube(side * 9.78, height + 0.08, z - 0.1, 0.95, 0.16, 1.5, mats.moss);
      boxCollider(side * 9.8, z, 0.8, 1.9);
      for (let row = 1; row < height / 0.6; row++) cube(side * 10.18, row * 0.6, z, 0.03, 0.028, 1.76, mats.darkStone);
    }
    for (let i = 0; i < 4; i++) {
      const x = side * (3.8 + i * 1.55), h = 3.1 + 1.5 * random();
      masonryWall(x,-24.2,1.48,h,0.9);
      cube(x, h + 0.08, -24.15, 1.5, 0.15, 1.05, mats.moss);
      boxCollider(x, -24.2, 1.5, 0.9);
    }
  }
  for (const [x,z,h] of [[-5.8,-17,3], [5.8,-17,5.9],[-5.8,-21,4.1],[5.8,-21,5.9],[-7,-9.4,1.9],[7.8,-10,2.7]]) column(x,z,h,0.45,h<5);

  // A single surviving bell tower gives the ruins their asymmetrical skyline.
  const tx = 8, tz = -23;
  for (const dx of [-1.25,1.25]) for (const dz of [-1.25,1.25]) {
    cube(tx + dx, 4.5, tz + dz, 0.75, 9, 0.75);
    boxCollider(tx + dx, tz + dz, 0.75, 0.75);
  }
  for (const y of [3.8,6.9,9]) {
    cube(tx, y, tz, 3.5, 0.35, 3.5);
    if (y === 9) cube(tx, y + 0.22, tz, 3.7, 0.1, 3.6, mats.moss);
  }
  for (const dx of [-1.2,1.2]) cube(tx + dx, 9.55, tz, 0.6, 0.8, 3.1);
  const bell = new THREE.Mesh(new THREE.ConeGeometry(0.63, 0.85, 9, 1, true), mats.gold);
  bell.rotation.z = Math.PI;
  bell.position.set(tx, 8, tz);
  root.add(bell); disposables.add(bell.geometry);
  cube(tx, 8.95, tz, 0.1, 0.5, 0.1, mats.darkMetal);

  // Sanctuary floor and steps match groundHeight, including its walkable edges.
  cube(0, 0.075, -22.35, 8.5, 0.15, 6.3);
  cube(0, 0.225, -22.75, 8.5, 0.15, 5.5);
  cube(0, 0.375, -23.15, 8.5, 0.15, 4.7);
  for (const x of [-4.1,4.1]) cube(x, 0.48, -23.1, 0.08, 0.06, 4.65, mats.gold);
  // Fallen masonry rests beside the nave rather than across its route.
  for (let i = 0; i < 31; i++) {
    const side = i % 2 ? -1 : 1, x = side * range(4.2, 11.8), z = range(-25.5, -10.2);
    const s = range(0.35, 0.75);
    batch('fallen-stones', box, mats.stone, x, s * 0.5, z, s * 1.8, s, s, range(-0.25,0.25),range(-2,2),range(-0.3,0.3),'#9ba997');
  }

  function branch(ax, ay, az, bx, by, bz, radius) {
    const start = new THREE.Vector3(ax, ay, az), end = new THREE.Vector3(bx, by, bz);
    const mid = start.clone().add(end).multiplyScalar(0.5);
    const quaternion = new THREE.Quaternion().setFromUnitVectors(new THREE.Vector3(0,1,0), end.clone().sub(start).normalize());
    dummy.position.copy(mid); dummy.quaternion.copy(quaternion); dummy.scale.set(radius,end.distanceTo(start),radius); dummy.updateMatrix();
    if (!batches.has('tree-limbs')) batches.set('tree-limbs',{ geometry:cylinder,material:mats.bark,transforms:[],colors:[] });
    batches.get('tree-limbs').transforms.push(dummy.matrix.clone()); batches.get('tree-limbs').colors.push(new THREE.Color('#b2b8a3'));
  }
  function tree(x,z,size=1,willow=false) {
    const height = range(5.2,7.5)*size;
    const leanX = range(-0.6,0.6), leanZ=range(-0.45,0.45);
    branch(x,0,z,x+leanX,height*0.78,z+leanZ,0.33*size);
    discCollider(x,z,0.4*size);
    for(let j=0;j<5;j++) {
      const angle=j/5*Math.PI*2+random()*0.4, spread=range(1.6,2.8)*size;
      const bx=x+Math.cos(angle)*spread,bz=z+Math.sin(angle)*spread,by=height*range(0.7,0.95);
      branch(x+leanX*0.6,height*0.45,z+leanZ*0.6,bx,by,bz,0.11*size);
      batch('tree-canopies',leafGeo,mats.leaf,bx,by+0.6*size,bz,range(1.1,1.6)*size,range(0.65,1.1)*size,range(1.1,1.6)*size,range(-0.3,0.3),range(0,6),0,new THREE.Color().setHSL(range(0.29,0.38),range(0.16,0.3),range(0.67,0.91)));
      for(let k=0;k<4;k++) {
        const ta=angle+detailRange(-1.2,1.2), reach=detailRange(.7,1.8)*size;
        const ex=bx+Math.cos(ta)*reach,ez=bz+Math.sin(ta)*reach,ey=by+detailRange(.1,1)*size;
        branch(bx,by,bz,ex,ey,ez,.035*size);
        const leafSize=detailRange(.4,.85)*size;
        batch('tree-fine-foliage',leafGeo,mats.leaf,ex,ey+.15*size,ez,leafSize,leafSize*detailRange(.65,1.2),leafSize*detailRange(.7,1.3),detailRange(-.7,.7),detailRange(0,6),detailRange(-.5,.5),new THREE.Color().setHSL(detailRange(.28,.38),.22,detailRange(.6,.98)));
      }
      if(willow) for(let k=0;k<3;k++) {
        const wx=bx+range(-1.2,1.2)*size,wz=bz+range(-1.2,1.2)*size,wh=range(1.7,3.1)*size;
        batch('willow-curtains',leafGeo,mats.leaf,wx,by-wh*0.36,wz,0.18*size,wh*.7,0.24*size,0,range(0,6),range(-0.2,0.2),'#c0c9a1');
        for(let f=0;f<3;f++)batch('willow-fine-leaves',leafGeo,mats.leaf,wx+detailRange(-.25,.25)*size,by-wh*(.2+f*.22),wz+detailRange(-.25,.25)*size,.22*size,.35*size,.21*size,0,detailRange(0,6),0,'#b9c994');
      }
    }
    // Low exposed roots help anchor trunks to the flat meadow.
    for(let j=0;j<4;j++) { const a=j/4*Math.PI*2; branch(x,0.3,z,x+Math.cos(a)*1.2*size,0.08,z+Math.sin(a)*1.2*size,0.14*size); }
  }
  tree(-15.8,4,1.15,true);
  tree(16.8,-5.7,1.2,true);
  tree(-17.4,-12,1.35);
  tree(19,11,1.15);
  tree(-16.5,16,1.25);
  tree(14.3,-18.5,1.15);
  for(let i=0;i<34;i++) {
    const a=i/34*Math.PI*2, r=range(25,32), x=Math.cos(a)*r,z=Math.sin(a)*r;
    if(Math.abs(x)<5 && z>20)continue;
    tree(x,z,range(0.75,1.2),i%9===0);
  }

  // A blade geometry uses only three triangles; two crossing blades retain
  // their silhouette at every camera angle. Wind is evaluated on the GPU.
  const bladeGeo = new THREE.BufferGeometry();
  bladeGeo.setAttribute('position',new THREE.Float32BufferAttribute([
    -0.09,0,0, 0.09,0,0, 0.045,0.48,0,
    -0.09,0,0, 0.045,0.48,0, -0.025,0.48,0,
    -0.025,0.48,0, 0.045,0.48,0, 0.035,0.9,0,
    0,0,-0.09, 0,0,0.09, 0,0.48,0.045,
    0,0,-0.09, 0,0.48,0.045, 0,0.48,-0.025,
    0,0.48,-0.025, 0,0.48,0.045, 0,0.9,0.035,
  ],3));
  bladeGeo.computeVertexNormals(); disposables.add(bladeGeo);
  const bladeColors=[];
  for(let i=0;i<bladeGeo.attributes.position.count;i++) {
    const y=bladeGeo.attributes.position.getY(i),brightness=.48+y*.58;
    bladeColors.push(brightness*.84,brightness,brightness*.8);
  }
  bladeGeo.setAttribute('color',new THREE.Float32BufferAttribute(bladeColors,3));
  mats.grass.onBeforeCompile = shader => {
    shader.uniforms.uValleyTime=wind;
    shader.vertexShader='uniform float uValleyTime;\n'+shader.vertexShader;
    shader.vertexShader=shader.vertexShader.replace('#include <begin_vertex>',`#include <begin_vertex>
      #ifdef USE_INSTANCING
      float phase = instanceMatrix[3].x * 0.48 + instanceMatrix[3].z * 0.36;
      transformed.x += sin(uValleyTime * 1.2 + phase) * 0.095 * position.y * position.y;
      transformed.z += cos(uValleyTime * 0.8 + phase * 0.7) * 0.05 * position.y;
      #endif`);
  };
  mats.grass.customProgramCacheKey=()=> 'stillwater-grass-v1';
  function nearPath(x,z) {
    if(Math.abs(x)<1.7&&(z>8.7||z<-9))return true;
    if(Math.abs(Math.abs(x)-12.7)<0.8 && z>-10 && z<10)return true;
    if(Math.hypot(x+14,z+2)<1.7||Math.hypot(x-14,z-2)<1.7)return true;
    return false;
  }
  for(let i=0;i<9400;i++) {
    const x=range(-31,31),z=range(-29,30),d=pondDistance(x,z);
    if(d<1.015 || nearPath(x,z) || (Math.abs(x)<8.5&&z<-12&&z>-25.7))continue;
    const y=groundHeight(x,z),height=range(0.42,1.05),density=0.75+0.25*Math.sin(x*0.4)*Math.sin(z*0.3);
    batch('meadow-grass',bladeGeo,mats.grass,x,y,z,range(0.65,1.4),height*density,range(0.65,1.4),0,range(0,Math.PI),0,new THREE.Color().setHSL(range(0.24,0.35),0.24,range(0.5,0.9)));
    if(i%11===0) {
      const stem=range(0.25,0.52);
      batch('flower-stems',stemGeo,mats.grass,x,y+stem/2,z,0.017,stem,0.017);
      batch('meadow-flowers',flowerGeo,mats.flower,x,y+stem,z,0.065,0.045,0.065,0,range(0,6),0,i%3?'#dbe7c9':'#a4bfb3');
    }
  }
  // Dense clumps near the pond create an actual ragged meadow silhouette; the
  // separate detail RNG leaves landmark placement and collision positions fixed.
  for(let i=0;i<24500;i++) {
    const x=detailRange(-22,22),z=detailRange(-17,25),d=pondDistance(x,z);
    if(d<1.025||nearPath(x,z)||(Math.abs(x)<10.3&&z<-12))continue;
    const patch=Math.sin(x*.93+Math.sin(z*.7))*Math.cos(z*.71);
    if(patch<-.35&&detailRandom()<.68)continue;
    const height=detailRange(.4,1.16)*(patch>.3?1.2:.85);
    batch('meadow-grass',bladeGeo,mats.grass,x,groundHeight(x,z),z,detailRange(.7,1.55),height,detailRange(.7,1.55),0,detailRange(0,Math.PI),0,new THREE.Color().setHSL(detailRange(.25,.37),detailRange(.25,.4),detailRange(.45,.9)));
    if(i%48===0) {
      const stem=detailRange(.45,.9),y=groundHeight(x,z);
      batch('flower-stems',stemGeo,mats.grass,x,y+stem/2,z,.018,stem,.018);
      for(let petal=0;petal<4;petal++) {
        const angle=petal*Math.PI/2;
        batch('meadow-flowers',flowerGeo,mats.flower,x+Math.cos(angle)*.055,y+stem,z+Math.sin(angle)*.055,.065,.025,.065,0,angle,0,'#d5e4b7');
      }
    }
  }
  // Reeds define the waterline without surrounding the pond with a solid wall.
  for(let i=0;i<500;i++) {
    const a=range(0,Math.PI*2);
    if(Math.abs(Math.sin(a))>0.92||Math.sin(a*4)>0.7)continue;
    const r=shoreRadius(a)*range(0.94,1.07),x=Math.cos(a)*10.6*r,z=Math.sin(a)*8.2*r;
    batch('pond-reeds',bladeGeo,mats.grass,x,groundHeight(x,z),z,range(0.5,0.85),range(0.5,1.3),range(0.5,0.85),0,range(0,Math.PI),0,'#a5b69a');
  }
  for(let i=0;i<76;i++) {
    const a=range(0,Math.PI*2), r=shoreRadius(a)*range(1.04,1.2),x=Math.cos(a)*10.6*r,z=Math.sin(a)*8.2*r;
    if(nearPath(x,z))continue;
    const s=range(0.14,0.55);
    batch('shore-stones',pebble,mats.darkStone,x,s*0.2,z,s,range(0.35,0.65)*s,s*1.2,range(0,1),range(0,6),0,'#bdc9ba');
  }
  for(let i=0;i<60;i++) {
    const x=range(-29,29),z=range(-28,29),s=range(0.3,1.05);
    if(pondDistance(x,z)<1.2||nearPath(x,z)||(Math.abs(x)<11&&z<-10))continue;
    batch('meadow-boulders',pebble,mats.darkStone,x,s*0.45,z,s,s*0.85,s*1.2,range(-0.2,0.2),range(0,6),range(-0.15,0.15),'#bec7b7');
    if(s>0.7)discCollider(x,z,s*0.65);
    batch('boulder-moss',pebble,mats.moss,x-0.05,s*0.9,z,0.65*s,0.1*s,0.75*s,0,range(0,6),0);
  }

  // Echo pedestals use a distinct warm light, framing the places to explore.
  function addEcho(id,label,hint,x,z) {
    const y=groundHeight(x,z);
    cube(x,y+0.12,z,1.2,0.24,1.2,mats.darkStone,Math.PI/4);
    batch('echo-plinths',cylinder,mats.stone,x,y+0.54,z,0.4,0.75,0.4);
    cube(x,y+0.94,z,0.9,0.14,0.9,mats.stone,Math.PI/4);
    const group=new THREE.Group(); group.position.set(x,y+1.55,z); group.name=label;
    const crystal=new THREE.Mesh(new THREE.OctahedronGeometry(0.21),mats.echo); crystal.scale.y=1.6; group.add(crystal); disposables.add(crystal.geometry);
    const halo=new THREE.Mesh(new THREE.TorusGeometry(0.43,0.015,5,40),mats.gold); halo.rotation.x=Math.PI/2; group.add(halo); disposables.add(halo.geometry);
    const smallHalo=halo.clone();smallHalo.rotation.set(Math.PI/3,0,Math.PI/6);group.add(smallHalo);
    root.add(group);
    animated.push({group,base:y+1.55,seed:random()*6,halo,smallHalo});
    interactables.push({id,type:'echo',position:new THREE.Vector3(x,y,z),mesh:group,label,hint});
    discCollider(x,z,0.28);
    // Low walls and broken uprights turn each echo into a little destination.
    for(const side of [-1,1]) {
      cube(x+side*1.65,y+0.3,z-0.75,0.6,0.6,1.8,mats.stone,side*0.1);
      column(x+side*1.6,z-1.5,range(1.3,2),0.22,true);
    }
  }
  addEcho('west','Echo of Roots','A memory kept beneath the willow.',-14,-2);
  addEcho('east','Echo of Rain','A quiet voice beside the water.',14,2);
  addEcho('north','Echo of Stone','The abbey remembers its song.',0,-17.8);

  const altar=new THREE.Group(); altar.position.set(0,0.45,-22);
  const altarBase=new THREE.Mesh(new THREE.CylinderGeometry(1.15,1.45,0.5,8),mats.darkStone);altarBase.position.y=0.25;altar.add(altarBase);disposables.add(altarBase.geometry);
  const altarTop=new THREE.Mesh(new THREE.CylinderGeometry(1.35,1.2,0.16,8),mats.stone);altarTop.position.y=0.58;altar.add(altarTop);disposables.add(altarTop.geometry);
  const altarRing=new THREE.Mesh(new THREE.TorusGeometry(0.85,0.045,6,48),mats.gold);altarRing.rotation.x=-Math.PI/2;altarRing.position.y=0.68;altar.add(altarRing);disposables.add(altarRing.geometry);
  const altarCrystal=new THREE.Mesh(new THREE.OctahedronGeometry(0.38),mats.echo);altarCrystal.position.y=1.35;altarCrystal.scale.y=1.5;altar.add(altarCrystal);disposables.add(altarCrystal.geometry);
  root.add(altar);
  interactables.push({id:'altar',type:'altar',position:new THREE.Vector3(0,0.45,-22),mesh:altar,label:'The Listening Stone',hint:'Return the three echoes to the abbey.'});
  discCollider(0,-22,0.95);

  // A warm lantern beside the starting stones gives the player a home point.
  const lantern = new THREE.Group();lantern.position.set(2.1,0,13.8);root.add(lantern);
  const lanternFoot=new THREE.Mesh(new THREE.CylinderGeometry(0.23,0.3,0.2,8),mats.darkStone);lanternFoot.position.y=0.1;lantern.add(lanternFoot);disposables.add(lanternFoot.geometry);
  const lanternPole=new THREE.Mesh(new THREE.CylinderGeometry(0.045,0.065,1.7,6),mats.darkMetal);lanternPole.position.y=0.85;lantern.add(lanternPole);disposables.add(lanternPole.geometry);
  for(const y of [1.5,2.05]) {const plate=new THREE.Mesh(box,mats.darkMetal);plate.scale.set(0.42,0.08,0.42);plate.position.y=y;lantern.add(plate);}
  for(const x of [-0.18,0.18]) for(const z of [-0.18,0.18]) { const bar=new THREE.Mesh(box,mats.gold);bar.scale.set(0.035,0.5,0.035);bar.position.set(x,1.77,z);lantern.add(bar); }
  const lanternFlame=new THREE.Mesh(new THREE.SphereGeometry(0.13,8,6),mats.glow);lanternFlame.position.y=1.75;lanternFlame.scale.y=1.7;lantern.add(lanternFlame);disposables.add(lanternFlame.geometry);
  // The owner chooses all actual scene lighting; this mesh supplies the glow.

  // Tangled ivy creeps across the high stonework in small readable clusters.
  for(let i=0;i<84;i++) {
    const side=i%2?-1:1,x=side*range(8.8,10.3),z=range(-23,-13),y=range(0.4,3.3);
    batch('abbey-ivy',leafGeo,mats.moss,x,y,z,range(0.2,0.48),range(0.15,0.32),range(0.25,0.5),0,range(0,6),0,'#a8b393');
  }
  for(const [name,data] of batches) {
    const mesh=new THREE.InstancedMesh(data.geometry,data.material,data.transforms.length);
    data.transforms.forEach((matrix,i)=>{mesh.setMatrixAt(i,matrix);mesh.setColorAt(i,data.colors[i]);});
    mesh.instanceMatrix.needsUpdate=true;
    if(mesh.instanceColor)mesh.instanceColor.needsUpdate=true;
    mesh.name=name;
    mesh.castShadow=!name.includes('grass')&&!name.includes('flowers')&&!name.includes('reeds');
    mesh.receiveShadow=true;
    mesh.computeBoundingSphere();
    root.add(mesh);
  }

  function update(dt,time) {
    wind.value=time;
    for(const item of animated) {
      item.group.position.y=item.base+Math.sin(time*1.3+item.seed)*0.1;
      item.group.children[0].rotation.y=time*0.45+item.seed;
      item.halo.rotation.z=time*0.18;
      item.smallHalo.rotation.y=time*0.25;
    }
    altarCrystal.rotation.y=time*0.2;
    altarCrystal.position.y=1.35+Math.sin(time*1.2)*0.06;
    lanternFlame.scale.y=1.65+Math.sin(time*8)*0.1+Math.sin(time*13.6)*0.07;
  }
  function dispose() {
    scene.remove(root);
    for(const disposable of disposables)disposable.dispose();
  }
  return {root,groundHeight,canWalk,update,interactables,spawn,bounds,waterMeshes,pondShape,waterGeometry,waterLevel,isWater,dispose};
}
