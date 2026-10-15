import * as THREE from 'three';
import { Reflector } from 'three/addons/objects/Reflector.js';

export function createRenderer(canvas,scene,camera){
  const renderer=new THREE.WebGLRenderer({canvas,antialias:false,alpha:false,powerPreference:'high-performance',preserveDrawingBuffer:true});
  renderer.setPixelRatio(1);
  renderer.outputColorSpace=THREE.SRGBColorSpace;
  renderer.toneMapping=THREE.ACESFilmicToneMapping;
  renderer.toneMappingExposure=1.25;
  const target=new THREE.WebGLRenderTarget(1,1,{minFilter:THREE.LinearFilter,magFilter:THREE.LinearFilter});
  const glyphRows=[" .,:;-=+*ox%#@MW", " .,'`/\\|vY!i*#@W", "  .,_-~:;=~+*o#@"];
  const atlas=document.createElement('canvas');atlas.width=32*16;atlas.height=48*3;
  const ctx=atlas.getContext('2d');ctx.fillStyle='#000';ctx.fillRect(0,0,atlas.width,atlas.height);ctx.fillStyle='#fff';ctx.font='bold 37px monospace';ctx.textAlign='center';ctx.textBaseline='middle';
  glyphRows.forEach((row,r)=>[...row].forEach((glyph,i)=>ctx.fillText(glyph,i*32+16,r*48+24)));
  const texture=new THREE.CanvasTexture(atlas);texture.minFilter=THREE.LinearFilter;texture.magFilter=THREE.LinearFilter;texture.generateMipmaps=false;
  const uniforms={tScene:{value:target.texture},tGlyph:{value:texture},resolution:{value:new THREE.Vector2()},cell:{value:new THREE.Vector2(6,9)},palette:{value:0},time:{value:0}};
  const material=new THREE.ShaderMaterial({uniforms,depthTest:false,depthWrite:false,toneMapped:false,
    vertexShader:`varying vec2 vUv; void main(){vUv=uv;gl_Position=vec4(position,1.);}`,
    fragmentShader:`precision highp float; varying vec2 vUv; uniform sampler2D tScene,tGlyph; uniform vec2 resolution,cell; uniform float palette,time;
    float hash(vec2 p){return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453);}
    void main(){
      vec2 grid=resolution/cell;vec2 cellId=floor(vUv*grid);vec2 sampleUV=(cellId+.5)/grid;
      vec4 scenePixel=texture2D(tScene,sampleUV);vec3 source=scenePixel.rgb;
      vec3 c=pow(max(source,vec3(0.)),vec3(.4545));
      float l=dot(c,vec3(.2126,.7152,.0722));float h=hash(cellId);
      bool grass=c.g>c.r*1.17&&c.g>c.b*1.25;
      bool water=scenePixel.a<.8;
      float row=0.;float index=clamp(floor(l*17.+(h-.5)*4.),0.,15.);
      if(grass){row=1.;index=l>.2?floor(5.+h*8.):floor(l*20.);}
      if(water){row=2.;index=clamp(floor(l*23.+h*3.),0.,15.);if(l<.22&&h>.2+l)index=0.;if(l>.29){row=0.;index=clamp(floor(l*23.+(h-.5)*3.),0.,15.);}}
      vec2 local=fract(vUv*grid);
      float glyph=texture2D(tGlyph,vec2((index+local.x)/16.,(2.-row+local.y)/3.)).r;
      vec3 background=vec3(.018,.042,.048);
      vec3 ink=c*1.4+vec3(.025,.035,.028);
      if(palette>1.5){ink=mix(vec3(.23,.32,.35),vec3(.96,.98,.87),l);background=vec3(.026,.039,.049);}
      else if(palette>.5){ink=mix(ink,vec3(l*1.45,l*1.08,l*.70),.7);background=vec3(.052,.035,.025);}
      vec3 underpainting=c*(water?.28:.43);
      vec3 outColor=background+underpainting+ink*glyph*1.05;
      gl_FragColor=vec4(outColor,1.);
    }`
  });
  const postScene=new THREE.Scene();postScene.add(new THREE.Mesh(new THREE.PlaneGeometry(2,2),material));
  const postCamera=new THREE.Camera();
  let width=1,height=1,detail='balanced';
  function setDetail(value){detail=value;const scale=value==='fine'?.78:value==='bold'?1.4:1;const factor=width<600?.85:1;uniforms.cell.value.set(6*scale*factor,9*scale*factor);}
  function resize(){width=canvas.clientWidth;height=canvas.clientHeight;renderer.setSize(width,height,false);target.setSize(Math.ceil(Math.min(width,1800)*.75),Math.ceil(Math.min(height,1200)*.75));uniforms.resolution.value.set(width,height);camera.aspect=width/height;camera.updateProjectionMatrix();setDetail(detail);}
  function render(time=0){uniforms.time.value=time;renderer.setRenderTarget(target);renderer.render(scene,camera);renderer.setRenderTarget(null);renderer.render(postScene,postCamera);}
  resize();
  return {renderer,resize,render,renderRaw(){renderer.setRenderTarget(null);renderer.render(scene,camera);},setDetail,setPalette(name){uniforms.palette.value={sage:0,amber:1,silver:2}[name]??0;},get info(){return renderer.info;}};
}

export function createWater(scene,world){
  const water=new Reflector(world.waterGeometry,{color:0x7ca9a6,textureWidth:512,textureHeight:512,clipBias:.002,multisample:0});
  water.rotation.x=-Math.PI/2;water.position.y=world.waterLevel;
  const shader=water.material;
  shader.uniforms.time={value:0};shader.uniforms.restored={value:0};
  shader.fragmentShader=shader.fragmentShader.replace('uniform vec3 color;','uniform vec3 color; uniform float time; uniform float restored;')
    .replace('vec4 base = texture2DProj( tDiffuse, vUv );',`vec4 uv2=vUv; vec2 wave=vec2(sin(vUv.x*54.+time*.55)+sin(vUv.y*43.-time*.36),cos(vUv.x*35.+time*.47));uv2.xy+=wave*.0014*vUv.w;vec4 base=texture2DProj(tDiffuse,uv2);`)
    .replace('gl_FragColor = vec4( blendOverlay( base.rgb, color ), 1.0 );',`vec3 tint=mix(vec3(.025,.10,.12),vec3(.07,.14,.12),restored); gl_FragColor=vec4(mix(base.rgb*.90,tint,.15),.42);`);
  scene.add(water);world.waterMeshes.push(water);
  return {mesh:water,update(time,restored){shader.uniforms.time.value=time;shader.uniforms.restored.value=restored?1:0;}};
}
