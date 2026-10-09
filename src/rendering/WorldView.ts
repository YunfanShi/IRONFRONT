import {VehicleInterior} from './VehicleInterior';
import * as THREE from 'three';
import {Random,heightAt,dist} from '../core/math';
import {MAP_SIZE,BLOCKS,OBJECTIVES,BASES,OPEN_BUILDINGS,collides,type Team} from '../world/Layout';
import type {Battle,BattleEvent} from '../core/Battle';
import {EnvironmentArt} from './EnvironmentArt';
import {SoldierVisuals} from './SoldierVisuals';
import {WeaponView} from './WeaponView';
import {WEAPONS,type WeaponId} from '../combat/Weapons';
import {VEHICLE_TYPES,vehicleMuzzle} from '../vehicles/Vehicle';
import {EquipmentView,type EquipmentItem} from './EquipmentView';
import {FieldVisuals} from './FieldVisuals';
import {VehicleVisuals} from './VehicleVisuals';

const material=(hex:number,roughness=.9,metalness=.0)=>new THREE.MeshStandardMaterial({color:hex,roughness,metalness});
const amber=material(0xa38c62),concrete=material(0x777b75),tin=material(0x596f77,.62,.37),roof=material(0x303e41,.72,.21),house=material(0xb1a188),olive=material(0x57654f);
const metal=material(0x47565b,.52,.55),dark=material(0x272c2c),blue=0x66c4fd,red=0xff806c;
const cube=(w:number,h:number,d:number,mat:THREE.Material)=>new THREE.Mesh(new THREE.BoxGeometry(w,h,d),mat);
function makeFlag(label:string):THREE.Sprite {
 const can=document.createElement('canvas');can.width=128;can.height=128;const c=can.getContext('2d')!;
 c.fillStyle='rgba(10,26,33,.90)';c.beginPath();c.arc(64,64,47,0,Math.PI*2);c.fill();
 c.strokeStyle='#c2dfdf';c.lineWidth=3;c.stroke();c.fillStyle='white';c.font='bold 70px Arial';c.textAlign='center';c.textBaseline='middle';c.fillText(label,64,67);
 const sprite=new THREE.Sprite(new THREE.SpriteMaterial({map:new THREE.CanvasTexture(can),transparent:true,depthTest:false}));sprite.scale.set(14,14,1);return sprite;
}
type FX={position:THREE.Vector3;velocity:THREE.Vector3;color:THREE.Color;life:number;max:number};
export interface LookState {yaw:number;pitch:number;height:number;ads:boolean;moving:boolean;sprint:boolean;reload:boolean;speed:number;side:number;paused?:boolean;vehicleFirstPerson?:boolean;infantryFirstPerson?:boolean;introAge?:number;resultAge?:number;network?:boolean;equipment?:EquipmentItem|null;equipmentAge?:number;deathAge?:number}
export class WorldView {
 readonly scene=new THREE.Scene();readonly renderer:THREE.WebGLRenderer;
 readonly camera:THREE.PerspectiveCamera;readonly weapon:WeaponView;readonly equipment:EquipmentView;readonly interior:VehicleInterior;
 private soldierRender:SoldierVisuals;private vehicleRender:VehicleVisuals;private art:EnvironmentArt;
 private markers:{ring:THREE.Mesh;label:THREE.Sprite;beam:THREE.Mesh}[]=[];
 readonly field=new FieldVisuals(this.scene);
 private projectileMeshes=new Map<number,THREE.Mesh>();
 private tracer:{line:THREE.Line;life:number;followMuzzle:boolean;equipment:boolean;fresh:boolean}[]=[];
 private blastFx:{mesh:THREE.Mesh;life:number;max:number;radius:number}[]=[];
 private particles:FX[]=[];private particleCloud:THREE.Points;
 private positionData=new Float32Array(1200*3);private colorData=new Float32Array(1200*3);
 private t=0;private high:boolean;private medium:boolean;private sun:THREE.DirectionalLight;private shadowEnabled:boolean;
 private iff:{id:number;sprite:THREE.Sprite}[]=[];
 private supportSmoke=new Map<number,THREE.Sprite[]>();private supportSmokeTexture:THREE.Texture|null=null;
 private armorFlash=0;private armorShake=0;private hitFlash=0;private hitRoll=0;private cameraKick=0;private vignette:HTMLDivElement|null=null;private smoke:{sprite:THREE.Sprite;baseX:number;baseY:number;baseScale:number;phase:number}[]=[];
 constructor(private canvas:HTMLCanvasElement,battle:Battle,quality:'low'|'medium'|'high',private baseFov=79,private renderScale=1){
  this.high=quality==='high';this.medium=quality==='medium';this.shadowEnabled=quality!=='low';
  this.renderer=new THREE.WebGLRenderer({canvas,antialias:quality!=='low',powerPreference:'high-performance',alpha:false});
  const ratio=this.high?1.75:this.medium?1.35:1;this.renderer.setPixelRatio(Math.min(window.devicePixelRatio||1,ratio)*this.renderScale);
  this.renderer.outputColorSpace=THREE.SRGBColorSpace;this.renderer.toneMapping=THREE.ACESFilmicToneMapping;this.renderer.toneMappingExposure=1.34;
  this.renderer.shadowMap.enabled=this.shadowEnabled;this.renderer.shadowMap.type=THREE.PCFSoftShadowMap;
  this.camera=new THREE.PerspectiveCamera(this.baseFov,1,.08,960);
  this.scene.background=new THREE.Color(0x97b6c5);this.scene.fog=new THREE.FogExp2(0xa7afb0,this.high ? .00255 : this.medium ? .00315 : .00415);
  this.scene.add(new THREE.AmbientLight(0xb7c9d2,.55));
  this.scene.add(new THREE.HemisphereLight(0xcdeaff,0x595b4b,this.high?1.85:1.65));
  this.sun=new THREE.DirectionalLight(0xffe3bd,this.high?3.25:this.medium?2.85:2.35);this.sun.position.set(160,240,-125);
  if(this.shadowEnabled){this.sun.castShadow=true;const map=this.high?2048:1024;this.sun.shadow.mapSize.set(map,map);const span=this.high?110:86;this.sun.shadow.camera.left=-span;this.sun.shadow.camera.right=span;this.sun.shadow.camera.top=span;this.sun.shadow.camera.bottom=-span;this.sun.shadow.camera.near=1;this.sun.shadow.camera.far=550;this.sun.shadow.bias=-.00035;this.sun.shadow.normalBias=.035}
  this.scene.add(this.sun,this.sun.target);
  this.buildTerrain();this.buildEnvironment();this.buildFoliage(this.high?520:this.medium?310:155);this.art=new EnvironmentArt(this.scene,this.high||this.medium);this.buildAmbientSmoke();
  this.buildMarkers();this.soldierRender=new SoldierVisuals(this.scene,battle);this.vehicleRender=new VehicleVisuals(this.scene,battle);
  this.weapon=new WeaponView(this.camera,this.scene);this.equipment=new EquipmentView(this.camera);this.interior=new VehicleInterior(this.camera);
  for(const s of battle.soldiers.filter(s=>s.id!==battle.player.id)){const can=document.createElement('canvas');can.width=128;can.height=48;const ctx=can.getContext('2d')!;ctx.fillStyle=s.team==='blue'?'#82d7ff':'#ffb19f';ctx.font='bold 25px sans-serif';ctx.textAlign='center';ctx.fillText(s.team==='blue'?'◆ BLU':'▲ RED',64,32);const sprite=new THREE.Sprite(new THREE.SpriteMaterial({map:new THREE.CanvasTexture(can),transparent:true,depthTest:true,depthWrite:false}));sprite.scale.set(2.5,.9,1);sprite.visible=false;this.scene.add(sprite);this.iff.push({id:s.id,sprite})}

  const particles=new THREE.BufferGeometry();particles.setAttribute('position',new THREE.BufferAttribute(this.positionData,3));particles.setAttribute('color',new THREE.BufferAttribute(this.colorData,3));particles.setDrawRange(0,0);
  const dot=this.particleTexture();
  this.particleCloud=new THREE.Points(particles,new THREE.PointsMaterial({map:dot,size:this.high ? .82 : this.medium ? .68 : .54,transparent:true,vertexColors:true,depthWrite:false,blending:THREE.AdditiveBlending,sizeAttenuation:true}));
  this.particleCloud.frustumCulled=false;this.scene.add(this.particleCloud);
  this.vignette=document.querySelector('#damage-vignette');
  this.resize();window.addEventListener('resize',this.resize);
 }
 resize=()=>{const w=Math.max(1,this.canvas.clientWidth),h=Math.max(1,this.canvas.clientHeight);this.camera.aspect=w/h;this.camera.updateProjectionMatrix();this.renderer.setSize(w,h,false)};
 private particleTexture(){const can=document.createElement('canvas');can.width=64;can.height=64;const c=can.getContext('2d')!;
  const g=c.createRadialGradient(32,32,0,32,32,30);g.addColorStop(0,'rgba(255,255,255,1)');g.addColorStop(.2,'rgba(255,255,255,.94)');g.addColorStop(1,'rgba(255,255,255,0)');c.fillStyle=g;c.fillRect(0,0,64,64);return new THREE.CanvasTexture(can)
 }
 private addBlock(x:number,y:number,z:number,w:number,h:number,d:number,mat:THREE.Material,casts=true){
  const m=cube(w,h,d,mat);m.position.set(x,y+h/2,z);m.castShadow=this.shadowEnabled&&casts;m.receiveShadow=true;this.scene.add(m);return m;
 }
 private groundTexture(){const can=document.createElement('canvas');can.width=256;can.height=256;const c=can.getContext('2d')!;let seed=12222;
  const rng=()=>{seed=(Math.imul(seed,1664525)+1013904223)|0;return (seed>>>0)/4294967296};
  c.fillStyle='#a2ad8d';c.fillRect(0,0,256,256);
  for(let i=0;i<12000;i++){const l=50+rng()*43;const a=.025+rng()*.12;c.fillStyle=`rgba(${l},${l+12},${l-5},${a})`;
   const x=rng()*256,y=rng()*256;c.fillRect(x,y,1+rng()*3,1+rng()*5)
  }const tex=new THREE.CanvasTexture(can);tex.wrapS=tex.wrapT=THREE.RepeatWrapping;tex.repeat.set(35,35);tex.anisotropy=8;tex.colorSpace=THREE.SRGBColorSpace;return tex;
 }
 private buildTerrain(){
  const g=new THREE.PlaneGeometry(MAP_SIZE,MAP_SIZE,120,120);g.rotateX(-Math.PI/2);
  const pos=g.attributes.position as THREE.BufferAttribute,colors:number[]=[];
  const low=new THREE.Color(0x75866a),high=new THREE.Color(0xa7a28a),rand=new Random(44809);
  for(let i=0;i<pos.count;i++){
   const x=pos.getX(i),z=pos.getZ(i),y=heightAt(x,z);pos.setY(i,y);
   const c=low.clone().lerp(high,Math.min(.75,y/28));c.multiplyScalar(.86+rand.between(0,.22));colors.push(c.r,c.g,c.b);
  }
  g.setAttribute('color',new THREE.Float32BufferAttribute(colors,3));g.computeVertexNormals();
  const ground=new THREE.Mesh(g,new THREE.MeshStandardMaterial({map:this.groundTexture(),vertexColors:true,roughness:1,metalness:0,side:THREE.DoubleSide}));
  ground.receiveShadow=this.shadowEnabled;this.scene.add(ground);
  const roads=[[[ -320,-296],[-185,-210],[-58,-180],[37,-62],[140,65],[301,296]], [[-291,160],[-171,116],[-26,80],[140,113],[302,154]], [[-210,-144],[-15,-92],[17,80],[-186,185]]];
  for(const points of roads)for(let j=0;j<points.length-1;j++)this.road({x:points[j]![0]!,z:points[j]![1]!},{x:points[j+1]![0]!,z:points[j+1]![1]!},11);
  // Railroad: ballast, sleepers, rails, raised steel edges.
  for(const z of [47,132]){
   this.addBlock(8,.02,z,590,.28,10.4,material(0x77756d),false);
   for(const side of [-2.6,2.6])this.addBlock(8,.30,z+side,590,.19,.31,metal,false);
   for(let x=-286;x<295;x+=7.6)this.addBlock(x,.05,z,1.1,.28,9.4,dark,false);
  }
 }
 private road(a:{x:number;z:number},b:{x:number;z:number},width:number){
  const len=Math.hypot(a.x-b.x,a.z-b.z),n=Math.ceil(len/4);const vertices:number[]=[],indices:number[]=[];
  const nx=-(b.z-a.z)/len*width/2,nz=(b.x-a.x)/len*width/2;
  for(let i=0;i<=n;i++){const t=i/n,x=a.x+(b.x-a.x)*t,z=a.z+(b.z-a.z)*t;
   for(const v of [-1,1])vertices.push(x+nx*v,heightAt(x+nx*v,z+nz*v)+.13,z+nz*v);
   if(i<n){const k=i*2;indices.push(k,k+1,k+2,k+1,k+3,k+2)}
  }const g=new THREE.BufferGeometry();g.setAttribute('position',new THREE.Float32BufferAttribute(vertices,3));g.setIndex(indices);g.computeVertexNormals();
  const m=new THREE.Mesh(g,material(0x8d8779,1));m.receiveShadow=this.shadowEnabled;this.scene.add(m);
 }
 private buildEnvironment(){
  const windows=material(0x1e363b,.24,.28),rim=material(0x9a9181);
  for(const b of BLOCKS){const y=heightAt(b.x,b.z);
   let mat=concrete;if(b.kind==='house')mat=house;else if(b.kind==='barracks')mat=olive;
   else if(b.kind==='container')mat=tin;else if(b.kind==='railcar')mat=metal;else if(b.kind==='crate')mat=amber;
   this.addBlock(b.x,y,b.z,b.w,b.h,b.d,mat);
   if(['factory','house','barracks'].includes(b.kind)){
    this.addBlock(b.x,y+b.h,b.z,b.w+1.6,.7,b.d+1.6,roof);
    // Proper entrance with realistic trim, recessed door glass and visible lintel.
    this.addBlock(b.x,y,b.z+b.d/2+.19,4.3,4.85,.28,metal,false);
    this.addBlock(b.x,y+4.7,b.z+b.d/2+.35,5.15,.27,.4,rim,false);
    this.addBlock(b.x,y+2.75,b.z+b.d/2+.39,1.35,1.1,.08,windows,false);
    for(const s of [-1,1])this.addBlock(b.x+s*2.4,y,b.z+b.d/2+.32,.21,5,.25,rim,false);
   }
   if(b.kind==='railcar')for(const side of [-1,1]){
    const wheel=new THREE.Mesh(new THREE.CylinderGeometry(1.5,1.5,.6,12),dark);wheel.rotation.x=Math.PI/2;wheel.position.set(b.x+side*5,y+1,b.z+b.d/2);this.scene.add(wheel)
   }
  }
  for(const g of OPEN_BUILDINGS){const y=heightAt(g.x,g.z);
   this.addBlock(g.x,y+g.h,g.z,g.w+1,1.0,g.d+1,roof);
   this.addBlock(g.x,y,g.z,g.w-.8,.2,g.d-.8,concrete);
  }
  const mast=new THREE.Mesh(new THREE.CylinderGeometry(.45,1.7,69,8),metal);mast.position.set(-185,heightAt(-185,244)+34.5,244);mast.castShadow=this.shadowEnabled;this.scene.add(mast);
  for(let i=10;i<70;i+=13){this.addBlock(-185,heightAt(-185,244)+i,244,18,.45,.55,dark,false);this.addBlock(-185,heightAt(-185,244)+i,244,.55,.45,18,dark,false)}
  const light=new THREE.PointLight(0xff8574,9,55);light.position.set(-185,heightAt(-185,244)+70,244);this.scene.add(light);
  for(const [x,z] of [[-29,189],[96,-201]] as [number,number][])this.addBlock(x,heightAt(x,z)+7,z,31,1.1,24,tin);
  for(const team of ['blue','red'] as Team[]){const p=BASES[team];const paint=team==='blue'?0x37718a:0x8b534b;
   this.addBlock(p.x,heightAt(p.x,p.z),p.z-21,40,.6,16,material(paint));
   const pole=new THREE.Mesh(new THREE.CylinderGeometry(.16,.16,14,8),metal);pole.position.set(p.x,heightAt(p.x,p.z)+7,p.z);this.scene.add(pole)
  }
 }
 private buildFoliage(count:number){
  const rand=new Random(22305);
  const trunkGeo=new THREE.CylinderGeometry(.38,.71,5.0,7),leafGeo=new THREE.ConeGeometry(4.2,10.8,8);
  const trunks=new THREE.InstancedMesh(trunkGeo,material(0x66533c),count),foliage=new THREE.InstancedMesh(leafGeo,material(0x3a624e),count*2);
  const d=new THREE.Object3D();let added=0;
  while(added<count){const x=rand.between(-345,345),z=rand.between(-345,345);
   if(dist({x,z},BASES.blue)<32||dist({x,z},BASES.red)<32||collides(x,z,5)||OBJECTIVES.some(o=>dist({x,z},o)<31)||Math.abs(z-47)<12||Math.abs(z-132)<12)continue;
   const h=heightAt(x,z),s=rand.between(.65,1.55),yaw=rand.between(0,6.28);
   d.scale.setScalar(s);d.rotation.y=yaw;d.position.set(x,h+2.4*s,z);d.updateMatrix();trunks.setMatrixAt(added,d.matrix);
   d.position.y=h+8.4*s;d.scale.setScalar(s);d.updateMatrix();foliage.setMatrixAt(added*2,d.matrix);
   d.position.y=h+13.2*s;d.scale.setScalar(s*.71);d.updateMatrix();foliage.setMatrixAt(added*2+1,d.matrix);added++;
  }trunks.instanceMatrix.needsUpdate=true;foliage.instanceMatrix.needsUpdate=true;
  foliage.castShadow=this.shadowEnabled&&this.high;trunks.castShadow=this.shadowEnabled&&this.high;this.scene.add(trunks,foliage)
 }
 private smokeTexture(){const can=document.createElement('canvas');can.width=128;can.height=128;const c=can.getContext('2d')!;const g=c.createRadialGradient(64,64,6,64,64,60);g.addColorStop(0,'rgba(78,84,82,.56)');g.addColorStop(.45,'rgba(94,98,92,.30)');g.addColorStop(1,'rgba(95,101,99,0)');c.fillStyle=g;c.fillRect(0,0,128,128);return new THREE.CanvasTexture(can)}
 private buildAmbientSmoke(){if(!this.medium&&!this.high)return;const tex=this.smokeTexture(),spots=[[-269,-170],[-163,-191],[82,127],[-246,229],[247,207]] as [number,number][];let i=0;for(const [x,z] of spots){for(let j=0;j<(this.high?3:2);j++){const mat=new THREE.SpriteMaterial({map:tex,transparent:true,depthWrite:false,opacity:.16,fog:true,color:0xa3a6a1});const sprite=new THREE.Sprite(mat);const baseY=heightAt(x,z)+18+j*8;sprite.position.set(x+(j-1)*2.3,baseY,z);const scale=15+j*6;sprite.scale.set(scale,scale*.85,1);this.scene.add(sprite);this.smoke.push({sprite,baseX:x+(j-1)*2.3,baseY,baseScale:scale,phase:i++*.91+j*.37})}}}
 private updateAmbientSmoke(dt:number){for(const s of this.smoke){s.phase+=dt*.18;s.sprite.position.y=s.baseY+Math.sin(s.phase)*2.1;s.sprite.position.x=s.baseX+Math.sin(s.phase*.53)*2.6;const pulse=.88+Math.sin(s.phase*.7)*.12;s.sprite.scale.set(s.baseScale*pulse,s.baseScale*.85*pulse,1);const m=s.sprite.material as THREE.SpriteMaterial;m.opacity=.11+(Math.sin(s.phase)+1)*.035}}
 private buildMarkers(){
  for(const o of OBJECTIVES){const y=heightAt(o.x,o.z);
   const ringGeo=new THREE.RingGeometry(21.1,22.5,64);ringGeo.rotateX(-Math.PI/2);
   const ring=new THREE.Mesh(ringGeo,new THREE.MeshBasicMaterial({color:0xd9f0eb,transparent:true,opacity:.4,depthWrite:false,side:THREE.DoubleSide}));ring.position.set(o.x,y+.3,o.z);this.scene.add(ring);
   const label=makeFlag(o.id);label.position.set(o.x,y+25,o.z);this.scene.add(label);
   const beam=new THREE.Mesh(new THREE.CylinderGeometry(7,11,45,24,1,true),new THREE.MeshBasicMaterial({color:0x6abde8,transparent:true,opacity:.042,depthWrite:false,side:THREE.DoubleSide,blending:THREE.AdditiveBlending}));beam.position.set(o.x,y+22.5,o.z);this.scene.add(beam);
   this.markers.push({ring,label,beam});
  }
 }
 playerMuzzle(battle:Battle,firstPerson=true){if(firstPerson)return this.weapon.muzzleWorldPosition();const a=battle.player.yaw,p=battle.player.pos;return {x:p.x+Math.sin(a)*.8,y:heightAt(p.x,p.z)+1.09,z:p.z+Math.cos(a)*.8};}
 onMouse(dx:number,dy:number){this.weapon.onMouse(dx,dy)}
 playerShot(weapon:WeaponId){this.weapon.shoot(weapon);/* Ballistic recoil is applied by the controller to both camera and aim ray. */}
 private spawnSpark(x:number,y:number,z:number,color:number,count:number){
  for(let n=0;n<count&&this.particles.length<950;n++){
   const rnd=Math.random(),direction=new THREE.Vector3(Math.random()-.5,Math.random()*1.3,Math.random()-.5).normalize();
   this.particles.push({position:new THREE.Vector3(x,y,z),velocity:direction.multiplyScalar(2+rnd*6),color:new THREE.Color(color),life:.24+rnd*.43,max:.24+rnd*.43})
  }
 }
 showEvents(events:BattleEvent[],player:{x:number;z:number}){
  for(const e of events){
   if((e.type==='shot'||(e.type==='vehicleShot'&&e.weapon==='mg'))&&dist(e.from,player)<220){
    const vehicle=e.type==='vehicleShot',ownMG=vehicle&&e.player===true&&this.equipment.root.visible;const ownMuzzle=ownMG?this.equipment.muzzleWorldPosition():this.weapon.muzzleWorldPosition();const start=(e.type==='shot'&&e.player&&this.weapon.group.visible)||ownMG?new THREE.Vector3(ownMuzzle.x,ownMuzzle.y,ownMuzzle.z):e.muzzle?new THREE.Vector3(e.muzzle.x,e.muzzle.y,e.muzzle.z):new THREE.Vector3(e.from.x,heightAt(e.from.x,e.from.z)+(vehicle?2.75:1.4),e.from.z);
    const end=e.type==='shot'&&e.end?new THREE.Vector3(e.end.x,e.end.y,e.end.z):new THREE.Vector3(e.to.x,heightAt(e.to.x,e.to.z)+(vehicle ? .85 : 1.35),e.to.z);
    // Visible short-lived tracer plus a luminous projectile head; no range truncation.
    const line=new THREE.Line(new THREE.BufferGeometry().setFromPoints([start,end]),new THREE.LineDashedMaterial({dashSize:2.5,gapSize:9,color:0xffd5a0,transparent:true,opacity:vehicle ? .85 : .5,depthWrite:false,blending:THREE.AdditiveBlending}));
    const flight=end.clone().sub(start).normalize().multiplyScalar(170);this.particles.push({position:start.clone(),velocity:flight,color:new THREE.Color(0xffce85),life:Math.min(.18,start.distanceTo(end)/170),max:.18});line.computeLineDistances();this.scene.add(line);this.tracer.push({line,life:vehicle?.18:.09,fresh:true,followMuzzle:(e.type==='shot'&&e.player===true&&this.weapon.group.visible)||ownMG,equipment:ownMG});
    if(vehicle||Math.random()<.7)this.spawnSpark(end.x,end.y,end.z,e.hit?0xf7c18a:0xdcc6a2,vehicle?20:e.hit?9:4);
   }else if(e.type==='vehicleShot'){const dir=new THREE.Vector3(e.to.x-e.from.x,0,e.to.z-e.from.z).normalize(),y=e.muzzle?.y??heightAt(e.from.x,e.from.z)+3.1;this.spawnSpark(e.from.x,y,e.from.z,0xffcd83,18);if(e.player)this.cameraKick=Math.min(.085,this.cameraKick+.04);
   }else if(e.type==='hitConfirmed'){this.spawnSpark(e.at.x,heightAt(e.at.x,e.at.z)+(e.vehicle?2:1.3),e.at.z,e.vehicle?0xffd38f:0xd1c2a4,e.killed?18:8);
   }else if(e.type==='playerHit'){this.hitFlash=Math.min(1,this.hitFlash+.65+e.amount*.006);this.hitRoll=(Math.random()-.5)*.07;this.cameraKick=Math.min(.085,this.cameraKick+.018+e.amount*.00045);
   }else if(e.type==='vehicleHit'){const v=this.lastBattle?.vehicles.find(v=>v.id===e.id);if(v){this.spawnSpark(v.pos.x,heightAt(v.pos.x,v.pos.z)+v.altitude+2,v.pos.z,0xffbc72,16);if(v===this.lastBattle?.playerVehicle&&e.amount>0){const strength=THREE.MathUtils.clamp(e.amount/v.maxHp,.025,.3);this.armorFlash=Math.min(1,this.armorFlash+.5+strength*1.5);this.armorShake=Math.min(.22,this.armorShake+.04+strength*.5);this.hitRoll=(Math.random()<.5?-1:1)*(.018+strength*.12);this.cameraKick=Math.min(.14,this.cameraKick+.035+strength*.2);}}}else if(e.type==='vehicleDisabled'){this.spawnSpark(e.at.x,heightAt(e.at.x,e.at.z)+1.7,e.at.z,0xffaa69,42);
   }else if(e.type==='artilleryImpact'||e.type==='projectileImpact'){const y=heightAt(e.at.x,e.at.z)+1.0;this.spawnSpark(e.at.x,y,e.at.z,0xffb063,this.high?90:58);const mesh=new THREE.Mesh(new THREE.SphereGeometry(1,16,10),new THREE.MeshBasicMaterial({color:0xffa35b,transparent:true,opacity:.34,depthWrite:false,blending:THREE.AdditiveBlending}));mesh.position.set(e.at.x,y,e.at.z);this.scene.add(mesh);this.blastFx.push({mesh,life:.58,max:.58,radius:e.radius*.72});const d=dist(e.at,player);if(d<105)this.cameraKick=Math.min(.42,this.cameraKick+(1-d/105)*.26);
   }else if(e.type==='death'){
    // No gore; a brief dust/debris puff marks an actual soldier elimination.
    const victim=this.lastBattle?.soldiers[e.victim];if(victim&&dist(victim.pos,player)<120)this.spawnSpark(victim.pos.x,heightAt(victim.pos.x,victim.pos.z)+.7,victim.pos.z,0xada18b,12)
   }
  }
 }
 private lastBattle:Battle|null=null;
 private updateBlastFx(dt:number){for(let i=this.blastFx.length-1;i>=0;i--){const b=this.blastFx[i]!;b.life-=dt;if(b.life<=0){this.scene.remove(b.mesh);b.mesh.geometry.dispose();(b.mesh.material as THREE.Material).dispose();this.blastFx.splice(i,1);continue}const t=1-b.life/b.max,scale=.8+b.radius*t;b.mesh.scale.setScalar(scale);(b.mesh.material as THREE.MeshBasicMaterial).opacity=(1-t)*.34}}
 private updateParticles(dt:number){
  let count=0;
  for(let i=this.particles.length-1;i>=0;i--){const p=this.particles[i]!;p.life-=dt;if(p.life<=0){this.particles.splice(i,1);continue}
   p.velocity.y-=dt*12;p.velocity.multiplyScalar(Math.max(0,1-dt*1.7));p.position.addScaledVector(p.velocity,dt);
   this.positionData[count*3]=p.position.x;this.positionData[count*3+1]=p.position.y;this.positionData[count*3+2]=p.position.z;
   const fade=p.life/p.max;this.colorData[count*3]=p.color.r*fade;this.colorData[count*3+1]=p.color.g*fade;this.colorData[count*3+2]=p.color.b*fade;count++;
  }
  this.particleCloud.geometry.setDrawRange(0,count);
  (this.particleCloud.geometry.attributes.position as THREE.BufferAttribute).needsUpdate=true;
  (this.particleCloud.geometry.attributes.color as THREE.BufferAttribute).needsUpdate=true;
 }
 private updateSupportSmoke(battle:Battle){
  const active=battle.supports.filter(e=>e.kind==='smoke');
  for(const [id,sprites] of this.supportSmoke)if(!active.some(e=>e.id===id)){for(const sprite of sprites){this.scene.remove(sprite);sprite.material.dispose()}this.supportSmoke.delete(id)}
  for(const e of active){if(!this.supportSmoke.has(e.id)){this.supportSmokeTexture??=this.smokeTexture();const sprites:THREE.Sprite[]=[];for(let i=0;i<(this.high?14:this.medium?10:7);i++){const m=new THREE.SpriteMaterial({map:this.supportSmokeTexture,transparent:true,depthWrite:false,opacity:.9,color:0xd3d8d3});const sprite=new THREE.Sprite(m);const a=i*2.4,r=(i%3)*7;sprite.position.set(e.at.x+Math.cos(a)*r,heightAt(e.at.x,e.at.z)+5+(i%2)*3,e.at.z+Math.sin(a)*r);sprite.scale.set(28,20,1);this.scene.add(sprite);sprites.push(sprite)}this.supportSmoke.set(e.id,sprites)}const life=Math.min(1,(battle.elapsed-e.starts)/1.2,(e.ends-battle.elapsed)/2);for(const sprite of this.supportSmoke.get(e.id)!){sprite.material.opacity=Math.max(0,life)*.95;}}
 }
 vehicleAim(battle:Battle):{x:number;y:number;z:number}{
  const v=battle.playerVehicle;if(!v)return {x:0,y:0,z:1};
  const ray=new THREE.Raycaster();ray.setFromCamera(new THREE.Vector2(0,0),this.camera);ray.far=VEHICLE_TYPES[v.kind].weaponRange;
  const hits=ray.intersectObjects(this.scene.children,true).filter(hit=>{if(!(hit.object instanceof THREE.Mesh))return false;let o:THREE.Object3D|null=hit.object;while(o){if(o.userData.vehicleId===v.id||o.userData.projectile)return false;o=o.parent;}return true;});
  const target=hits[0]?.point??ray.ray.at(ray.far,new THREE.Vector3());
  const direction=target.clone().sub(new THREE.Vector3(vehicleMuzzle(v,battle.playerSeat===1||v.kind==='scout').x,heightAt(v.pos.x,v.pos.z)+vehicleMuzzle(v,battle.playerSeat===1||v.kind==='scout').y,vehicleMuzzle(v,battle.playerSeat===1||v.kind==='scout').z)).normalize();return {x:direction.x,y:direction.y,z:direction.z};
 }
 private updateProjectileMeshes(battle:Battle){
  for(const [id,mesh] of this.projectileMeshes)if(!battle.projectiles.some(p=>p.id===id)){this.scene.remove(mesh);mesh.geometry.dispose();(mesh.material as THREE.Material).dispose();this.projectileMeshes.delete(id);}
  for(const p of battle.projectiles){let mesh=this.projectileMeshes.get(p.id);if(!mesh){mesh=new THREE.Mesh(new THREE.SphereGeometry(p.kind==='rocket'?.18:.14,8,6),new THREE.MeshBasicMaterial({color:0xffd58a}));mesh.scale.set(1,1,2.5);mesh.userData.projectile=true;this.scene.add(mesh);this.projectileMeshes.set(p.id,mesh);}mesh.position.set(p.pos.x,p.pos.y,p.pos.z);mesh.lookAt(p.pos.x+p.velocity.x,p.pos.y+p.velocity.y,p.pos.z+p.velocity.z);if(this.particles.length<900)this.particles.push({position:mesh.position.clone(),velocity:new THREE.Vector3(0,.5,0),color:new THREE.Color(0xd1c3a7),life:.3,max:.3});}
 }
 render(battle:Battle,dt:number,look:LookState){
  this.lastBattle=battle;this.t+=dt;if(this.scene.fog instanceof THREE.FogExp2)this.scene.fog.density=(this.high?.00255:this.medium?.00315:.00415)*(1-.88*THREE.MathUtils.smoothstep(look.deathAge??0,2,4.8));
  const p=battle.player,vehicle=battle.playerVehicle;
  if(vehicle){
   const gunner=look.vehicleFirstPerson||look.ads,distance=gunner?0:10;
   let fraction=1;const seatHeight=battle.playerSeat===1&&vehicle.kind!=='motorcycle'?3.35:({motorcycle:1.65,scout:1.9,transport:1.85,tank:3.15,ifv:2.65,aa:2.8,helicopter:2.45,jet:2.35}[vehicle.kind])+(battle.playerSeat>=2?-.25:0);const pivotY=heightAt(vehicle.pos.x,vehicle.pos.z)+vehicle.altitude+(gunner?seatHeight:3.5);
   if(!gunner)for(let t=.05;t<=1;t+=.05){const x=vehicle.pos.x+Math.sin(look.yaw)*distance*t,z=vehicle.pos.z+Math.cos(look.yaw)*distance*t,y=pivotY+2.15*t;if(BLOCKS.some(b=>Math.abs(x-b.x)<b.w/2+.5&&Math.abs(z-b.z)<b.d/2+.5&&y<heightAt(x,z)+b.h+.5)){fraction=Math.max(0,t-.08);break;}}
   const x=vehicle.pos.x+Math.sin(look.yaw)*distance*fraction,z=vehicle.pos.z+Math.cos(look.yaw)*distance*fraction;
   const side=gunner?(battle.playerSeat===1?.55:battle.playerSeat>=2?(battle.playerSeat===2?-.55:.55):['scout','transport','helicopter'].includes(vehicle.kind)?-.3:0):0,forward=gunner?(battle.playerSeat>=2||vehicle.kind==='motorcycle'&&battle.playerSeat===1?-.55:.35):0;this.camera.position.set(x+Math.cos(vehicle.yaw)*side+Math.sin(vehicle.yaw)*forward,Math.max(heightAt(x,z)+1.2,pivotY+(gunner?0:2.15*fraction)),z-Math.sin(vehicle.yaw)*side+Math.cos(vehicle.yaw)*forward);
  }else {this.camera.position.set(p.pos.x,heightAt(p.pos.x,p.pos.z)+look.height,p.pos.z);if(look.infantryFirstPerson===false&&!look.ads){let fraction=1;for(let t=.1;t<=1;t+=.1){const x=p.pos.x+Math.sin(look.yaw)*4*t,z=p.pos.z+Math.cos(look.yaw)*4*t,y=heightAt(p.pos.x,p.pos.z)+look.height+.6*t;if(BLOCKS.some(b=>Math.abs(x-b.x)<b.w/2+.3&&Math.abs(z-b.z)<b.d/2+.3&&y<heightAt(x,z)+b.h)){fraction=Math.max(0,t-.1);break;}}this.camera.position.x+=Math.sin(look.yaw)*4*fraction;this.camera.position.z+=Math.cos(look.yaw)*4*fraction;this.camera.position.y+=.6*fraction;}}
  for(const object of this.scene.children)if(object.userData.sky)object.position.copy(this.camera.position);
  this.camera.rotation.order='YXZ';this.camera.rotation.y=look.yaw;this.camera.rotation.x=look.pitch-this.cameraKick;
  this.camera.rotation.z=this.hitRoll;this.hitRoll=THREE.MathUtils.damp(this.hitRoll,0,10,dt);this.hitFlash=Math.max(0,this.hitFlash-dt*1.7);
  if(p.downedUntil>battle.elapsed){this.camera.position.y=heightAt(p.pos.x,p.pos.z)+.35;this.camera.rotation.z=.35;}
  if(look.deathAge!==undefined){const age=look.deathAge,fall=THREE.MathUtils.smoothstep(age,0,1.15),rise=THREE.MathUtils.smoothstep(age,2,4.8);this.camera.position.set(p.pos.x*(1-rise),heightAt(p.pos.x,p.pos.z)+THREE.MathUtils.lerp(look.height,.28,fall)+rise*525,p.pos.z*(1-rise));this.camera.rotation.x=THREE.MathUtils.lerp(look.pitch-.25*fall,-Math.PI/2+.001,rise);this.camera.rotation.y=look.yaw*(1-rise);this.camera.rotation.z=.75*fall*(1-rise);}
  if(look.introAge!==undefined){const t=THREE.MathUtils.smoothstep(look.introAge,0,3);this.camera.position.y+=18*(1-t);this.camera.rotation.x-=.45*(1-t);}
  if(look.resultAge!==undefined){const t=Math.min(1,look.resultAge/4),a=look.yaw+t*.6;this.camera.position.set(p.pos.x+Math.sin(a)*10, heightAt(p.pos.x,p.pos.z)+5+t*8,p.pos.z+Math.cos(a)*10);this.camera.lookAt(p.pos.x,heightAt(p.pos.x,p.pos.z)+1.5,p.pos.z);}
  if(vehicle&&this.armorShake>0){this.camera.position.x+=Math.sin(this.t*79)*this.armorShake;this.camera.position.y+=Math.sin(this.t*97)*this.armorShake*.65;}this.armorShake=THREE.MathUtils.damp(this.armorShake,0,9,dt);this.armorFlash=Math.max(0,this.armorFlash-dt*2.2);this.cameraKick=THREE.MathUtils.damp(this.cameraKick,0,17,dt);
  const aim=this.weapon.render(dt,{weapon:battle.playerWeapon,ads:look.ads&&!vehicle,sprint:look.sprint&&!vehicle,moving:look.moving,speed:look.speed,reload:look.reload,alive:p.alive&&!vehicle&&!look.equipment&&(look.infantryFirstPerson!==false||look.ads),velocitySide:look.side});
  this.equipment.render(p.alive&&(look.infantryFirstPerson!==false||look.ads||!!vehicle)?(vehicle?(vehicle.kind!=='motorcycle'&&(look.vehicleFirstPerson||look.ads)?(battle.playerSeat===1?'mountedMG':null):null):look.equipment??null):null,look.equipmentAge??0,look.ads);
  if(vehicle&&(battle.playerSeat===1||vehicle.kind==='scout')){this.equipment.root.rotation.y=Math.atan2(Math.sin(vehicle.mgYaw-look.yaw-Math.PI),Math.cos(vehicle.mgYaw-look.yaw-Math.PI));this.equipment.root.rotation.x=vehicle.turretPitch-look.pitch;}
  const sway=look.moving?Math.sin(this.t*(look.sprint?15:11))*.011*(look.sprint?1.5:1):0;
  this.camera.position.y+=sway*(1-aim*.65);
  this.camera.rotation.z+=look.side*.013;
  const vehicleZoom=vehicle&&look.ads?3:1;const adsFov=THREE.MathUtils.radToDeg(2*Math.atan(Math.tan(THREE.MathUtils.degToRad(this.baseFov/2))/battle.activeWeapon.zoom));this.camera.fov=THREE.MathUtils.damp(this.camera.fov,(vehicle?this.baseFov/vehicleZoom:THREE.MathUtils.lerp(this.baseFov,adsFov,aim))+(look.sprint?3:0),13,dt);this.camera.updateProjectionMatrix();
  for(const item of this.iff){const s=battle.soldiers[item.id]!;item.sprite.visible=s.alive&&s.vehicleId===null&&battle.canIdentify(s.pos,s.team===battle.player.team?60:35);item.sprite.position.set(s.pos.x,heightAt(s.pos.x,s.pos.z)+2.35,s.pos.z);const width=Math.max(.2,dist(p.pos,s.pos)*Math.tan(THREE.MathUtils.degToRad(this.camera.fov/2))*.07);item.sprite.scale.set(width,width*.375,1)}
  this.updateSupportSmoke(battle);
  this.interior.render(p.alive&&look.resultAge===undefined&&look.introAge===undefined?vehicle:null,battle.playerSeat,!!look.vehicleFirstPerson||look.ads,look.ads,dt);this.field.update(battle,dt,this.camera);this.updateProjectileMeshes(battle);this.soldierRender.update(battle,dt,vehicle?!look.vehicleFirstPerson&&!look.ads:look.infantryFirstPerson===false&&!look.ads);this.vehicleRender.update(dt,!!look.vehicleFirstPerson||look.ads,!!look.network);
  for(let i=0;i<this.markers.length;i++){
   const owner=battle.points[i]!.owner,color=owner==='blue'?blue:owner==='red'?red:0xdbddd2;
   (this.markers[i]!.ring.material as THREE.MeshBasicMaterial).color.setHex(color);
   this.markers[i]!.label.material.color.setHex(color);
   (this.markers[i]!.beam.material as THREE.MeshBasicMaterial).color.setHex(color);
   (this.markers[i]!.beam.material as THREE.MeshBasicMaterial).opacity=.028+(Math.sin(this.t*2+i)+1)*.012;
  }
  // Camera-centred shadow coverage: nearby soldiers/buildings remain readable.
  if(this.shadowEnabled){this.sun.position.set(p.pos.x+160,240,p.pos.z-125);this.sun.target.position.set(p.pos.x,0,p.pos.z);this.sun.target.updateMatrixWorld()}
  this.updateParticles(dt);this.updateBlastFx(dt);this.updateAmbientSmoke(dt);
  for(let i=this.tracer.length-1;i>=0;i--){const t=this.tracer[i]!;if(t.followMuzzle){const m=t.equipment?this.equipment.muzzleWorldPosition():this.weapon.muzzleWorldPosition(),positions=t.line.geometry.getAttribute('position') as THREE.BufferAttribute;positions.setXYZ(0,m.x,m.y,m.z);positions.needsUpdate=true;t.line.geometry.computeBoundingSphere();t.line.computeLineDistances();}if(t.fresh)t.fresh=false;else t.life-=dt;if(t.life<=0){this.scene.remove(t.line);t.line.geometry.dispose();(t.line.material as THREE.Material).dispose();this.tracer.splice(i,1)}}
  if(this.vignette){this.vignette.classList.toggle('armor-hit',!!vehicle);this.vignette.style.opacity=String(vehicle?this.armorFlash:Math.max(0,(p.alive?(1-p.hp/100)*.8:0)+this.hitFlash*.85));}
  this.renderer.render(this.scene,this.camera);
 }
 dispose(){
  window.removeEventListener('resize',this.resize);this.field.dispose();this.weapon.dispose();this.equipment.dispose();this.interior.dispose();this.soldierRender.dispose();this.vehicleRender.dispose();this.art.dispose();
  // THREE.Scene.clear does not free GPU resources: release all generated assets on restart.
  const geometries=new Set<THREE.BufferGeometry>(),materials=new Set<THREE.Material>(),textures=new Set<THREE.Texture>();
  this.scene.traverse((o:THREE.Object3D)=>{
   const mesh=o as THREE.Mesh;
   if(mesh.geometry instanceof THREE.BufferGeometry)geometries.add(mesh.geometry);
   if(mesh.material){const list=Array.isArray(mesh.material)?mesh.material:[mesh.material];
    for(const m of list)if(m instanceof THREE.Material){materials.add(m);const mm=m as THREE.MeshStandardMaterial & {map?:THREE.Texture};if(mm.map)textures.add(mm.map)}
   }
  });
  for(const g of geometries)g.dispose();for(const t of textures)t.dispose();for(const m of materials)m.dispose();
  this.renderer.dispose();this.scene.clear();
 }
}
