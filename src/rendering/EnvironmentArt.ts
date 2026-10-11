import * as THREE from 'three';
import {Random,dist} from '../core/math';
import type {Team} from '../world/Layout';
import {MapContext} from '../world/Maps';

/** Self-contained procedural environment art. Everything is generated locally; no CDN or borrowed assets. */
export class EnvironmentArt {
 private random = new Random(9331);
 private cache:THREE.Material[]=[];
 private batches=new Map<string,{geometry:THREE.BufferGeometry;material:THREE.Material;items:THREE.Matrix4[]}>();
 private dummy=new THREE.Object3D();
 constructor(private scene:THREE.Scene,private high:boolean,private map=new MapContext()){this.addSky();this.addArchitecture();this.addGroundDetails();this.addLandmarks();this.addFieldDetails();if(this.map.definition.theme==='desert')this.addDesertDetails();this.flush()}
 private mat(color:number,roughness=.88,metalness=0){const m=new THREE.MeshStandardMaterial({color,roughness,metalness});this.cache.push(m);return m}
 /** Reuse geometry/material and submit static scene detail in a handful of draw calls. */
 private batch(key:string,geometry:THREE.BufferGeometry,material:THREE.Material,x:number,y:number,z:number,ry=0,sx=1,sy=1,sz=1){
  let b=this.batches.get(key);if(!b){b={geometry,material,items:[]};this.batches.set(key,b)}
  this.dummy.position.set(x,y,z);this.dummy.rotation.set(0,ry,0);this.dummy.scale.set(sx,sy,sz);this.dummy.updateMatrix();b.items.push(this.dummy.matrix.clone());
 }
 private flush(){for(const b of this.batches.values()){
  if(!b.items.length)continue;const mesh=new THREE.InstancedMesh(b.geometry,b.material,b.items.length);
  for(let i=0;i<b.items.length;i++)mesh.setMatrixAt(i,b.items[i]!);
  mesh.instanceMatrix.needsUpdate=true;mesh.castShadow=false;mesh.receiveShadow=true;mesh.computeBoundingSphere();mesh.frustumCulled=true;this.scene.add(mesh)
 }this.batches.clear()}
 private addSky(){
  const sky=new THREE.Mesh(new THREE.SphereGeometry(this.map.size*1.3,32,20),new THREE.ShaderMaterial({
   side:THREE.BackSide,depthWrite:false,uniforms:{},vertexShader:`varying vec3 vDir;void main(){vDir=normalize(position);gl_Position=projectionMatrix*modelViewMatrix*vec4(position,1.);}`,
   fragmentShader:`varying vec3 vDir;void main(){float h=clamp(vDir.y*0.5+0.5,0.,1.);vec3 top=vec3(0.18,0.36,0.54);vec3 mid=vec3(0.45,0.64,0.76);vec3 horizon=vec3(0.88,0.72,0.54);vec3 col=mix(horizon,mid,smoothstep(.43,.61,h));col=mix(col,top,smoothstep(.66,1.,h));vec3 sun=normalize(vec3(.58,.72,-.40));float halo=pow(max(0.,dot(normalize(vDir),sun)),14.);float disk=pow(max(0.,dot(normalize(vDir),sun)),640.);col+=vec3(1.,.64,.36)*halo*.23+vec3(1.,.92,.75)*disk*.67;gl_FragColor=vec4(col,1.);}`
  }));sky.userData.sky=true;sky.renderOrder=-1000;this.scene.add(sky);
  const cloudTexture=this.cloudTexture();const cloudMaterial=new THREE.MeshBasicMaterial({map:cloudTexture,transparent:true,depthWrite:false,side:THREE.DoubleSide,opacity:.45,fog:false});
  const cloudPlane=new THREE.PlaneGeometry(135,48);
  for(let i=0;i<(this.high?23:11);i++){
   const a=this.random.between(-Math.PI,Math.PI),rad=this.random.between(220,560);
   const c=new THREE.Mesh(cloudPlane,cloudMaterial);c.position.set(Math.sin(a)*rad,this.random.between(118,205),Math.cos(a)*rad);
   c.rotation.set(-Math.PI/2,0,this.random.between(-.3,.3));c.scale.set(this.random.between(.7,1.8),this.random.between(.75,1.6),1);c.renderOrder=-10;this.scene.add(c);
  }
 }
 private cloudTexture(){const can=document.createElement('canvas');can.width=256;can.height=128;const ctx=can.getContext('2d')!;
  const gr=ctx.createRadialGradient(128,64,9,128,64,117);gr.addColorStop(0,'rgba(255,250,241,.97)');gr.addColorStop(.3,'rgba(247,245,237,.81)');gr.addColorStop(.68,'rgba(246,243,237,.24)');gr.addColorStop(1,'rgba(245,244,240,0)');ctx.fillStyle=gr;ctx.fillRect(0,0,256,128);return new THREE.CanvasTexture(can)
 }
 private addArchitecture(){
  const glass=this.mat(0x304b52,.24,.25),glassHighlight=this.mat(0x698b92,.27,.15),trim=this.mat(0x343c3b,.91),chalk=this.mat(0x99907d,.95),lamp=this.mat(0xe8c292,.35,.2);
  const pane=new THREE.BoxGeometry(1,1,1),barrelGeo=new THREE.CylinderGeometry(.75,.75,1.8,10),sandbagGeo=new THREE.BoxGeometry(2.25,.6,1),postGeo=new THREE.CylinderGeometry(.13,.13,1,8),pipeGeo=new THREE.CylinderGeometry(.27,.27,1,9);
  const rust=this.mat(0x835646,.9,.2),barrelMat=this.mat(0x58636b,.64,.42),bag=this.mat(0x918570,.97),wire=this.mat(0x252e31,.6,.5);
  for(const b of this.map.blocks){const floor=this.map.heightAt(b.x,b.z);
   if(b.kind==='container'){
    const count=5;for(let k=0;k<count;k++){const px=b.x+(k-2)*b.w/5;
     this.batch('container-ribs',pane,trim,px,floor+b.h*.55,b.z+b.d/2+.14,0,.16,b.h*.94,.16);
    }
   }else if(b.kind==='railcar'){
    for(let j=-1;j<=1;j++)this.batch('railcar-window',pane,glass,b.x+j*4.1,floor+b.h*.66,b.z+b.d/2+.16,0,2.25,1.25,.16);
   }else if(b.kind==='crate'){
    this.batch('crate-band',pane,trim,b.x,floor+b.h*.65,b.z+b.d/2+.07,0,b.w+.08,.2,.1);
   }
  }
  // Loading aprons belong to the buildings rather than random piles around objectives.
  for(const b of this.map.definition.blocks.filter(b=>['factory','house','barracks'].includes(b.kind))){
   const y=this.map.heightAt(b.x,b.z),front=b.z+b.d/2;
   this.batch('entry-pavement',pane,chalk,b.x,y+.06,front+2,0,b.w*.8,.12,3.8);
   // Connect the entrance apron to the nearest public road; thin pavement does not change navigation.
   const entry={x:b.x,z:front+2};let nearest={x:entry.x,z:entry.z},best=Infinity;
   for(const path of this.map.definition.roads)for(let i=1;i<path.length;i++){const a=path[i-1]!,c=path[i]!,dx=c.x-a.x,dz=c.z-a.z,t=Math.max(0,Math.min(1,((entry.x-a.x)*dx+(entry.z-a.z)*dz)/(dx*dx+dz*dz))),q={x:a.x+t*dx,z:a.z+t*dz},distance=dist(entry,q);if(distance<best){best=distance;nearest=q;}}
   if(best<55&&!this.map.lineBlocked(entry,nearest,undefined,1)){const yaw=Math.atan2(nearest.x-entry.x,nearest.z-entry.z);for(let step=0;step<best;step+=2){const x=entry.x+(nearest.x-entry.x)*step/best,z=entry.z+(nearest.z-entry.z)*step/best;this.batch('entry-walkway',pane,chalk,x,this.map.heightAt(x,z)+.08,z,yaw,2.2,.1,2.1);}}
   // A wall-mounted street lamp and utility cabinet stay inside the authoritative solid envelope.
   this.batch('lamp-arm',pane,wire,b.x+b.w*.35,y+4.4,front+.1,0,.2,.2,.3);
   this.batch('street-lamp',pane,lamp,b.x+b.w*.35,y+4.25,front+.18,0,.9,.15,.25);
   this.batch('utility-cabinet',pane,barrelMat,b.x-b.w*.36,y+.9,front+.15,0,1.1,1.8,.28);
  }

 }
 private addGroundDetails(){
  const rockGeo=new THREE.DodecahedronGeometry(1,0),tuftGeo=new THREE.ConeGeometry(.4,1.7,3),shrubGeo=new THREE.IcosahedronGeometry(1,0),cracked=this.mat(this.map.definition.theme==='desert'?0xa88e69:0x737368,.96),grass=this.mat(this.map.definition.theme==='desert'?0xb7a179:0x697959,.99),bush=this.mat(this.map.definition.theme==='desert'?0x7e815c:0x3c5c43,.98),dry=this.mat(0x9e9668,.98);
  const c=this.high?1150:380;
  for(let i=0;i<c;i++){
   const x=this.random.between(-this.map.size*.47,this.map.size*.47),z=this.random.between(-this.map.size*.47,this.map.size*.47);
   if(this.map.collides(x,z,1.0)||this.map.objectives.some(p=>dist(p,{x,z})<15))continue;
   const y=this.map.heightAt(x,z),r=this.random.between(.55,1.3),rot=this.random.between(0,6.28);
   if(i%14===0)this.batch('rocks',rockGeo,cracked,x,y+.36*r,z,rot,.85*r,.43*r,1.2*r);
   else if(i%6===0)this.batch('shrubs',shrubGeo,bush,x,y+.9*r,z,rot,2.7*r,1.15*r,2.8*r);
   else this.batch(i%3===0?'drygrass':'grass',tuftGeo,i%3===0?dry:grass,x,y+.52*r,z,rot,1.2*r,.6*r,1.4*r);
  }

 }
 private addFieldDetails(){
  const box=new THREE.BoxGeometry(1,1,1),asphalt=this.mat(0x454a47,.97),concrete=this.mat(0x9a988b,.95),metal=this.mat(0x586769,.65,.35);
  // Connected road ribbons and curb blocks reuse three static batches.
  for(const path of this.map.definition.roads){
  for(let i=1;i<path.length;i++){const a=path[i-1]!,b=path[i]!,len=dist(a,b),yaw=Math.atan2(b.x-a.x,b.z-a.z);for(let t=0;t<len;t+=8){const x=a.x+(b.x-a.x)*t/len,z=a.z+(b.z-a.z)*t/len;this.batch('road-dash',box,concrete,x,this.map.heightAt(x,z)+.16,z,yaw,.16,.02,2.8);if(this.high)for(const side of [-1,1])this.batch('curb',box,concrete,x+Math.cos(yaw)*side*6,this.map.heightAt(x,z)+.1,z-Math.sin(yaw)*side*6,yaw,.35,.25,7);}}
  }

 }
 private addLandmarks(){
  const steel=this.mat(0x626d72,.65,.55),dark=this.mat(0x282e34,.92),red=this.mat(0xa45b43,.88),tower=new THREE.CylinderGeometry(.27,.43,1,8),long=new THREE.BoxGeometry(1,1,1);
  for(const at of this.map.definition.landmarks) {const p:[number,number]=[at.x,at.z];
   const y=this.map.heightAt(...p);for(let h=0;h<40;h+=4){
    this.batch('tower-main',tower,steel,p[0],y+h+2,p[1],0,1,4,1);
    this.batch('tower-rungs',long,h%8===0?red:dark,p[0],y+h+4,p[1],0,7,.2,.26);
    if(h%8===0)for(const s of [-1,1])this.batch('tower-brace',long,steel,p[0]+s*2.5,y+h+2,p[1],s*.3,.18,4.4,.18)
   }
  }
 }
 private addDesertDetails(){
  const box=new THREE.BoxGeometry(1,1,1),stone=this.mat(0x987a59),steel=this.mat(0x545f5d,.65,.4),sand=this.mat(0xbba16f),dark=this.mat(0x3c4445);
  // Rock escarpments and dry shrubs use a few instanced batches at the outer dunes.
  const rock=new THREE.DodecahedronGeometry(1,0),cactus=new THREE.CylinderGeometry(.18,.24,2.6,5),dry=this.mat(0x747d57);
  for(let i=0;i<180;i++){const x=this.random.between(-440,440),z=this.random.between(-440,440);if(this.map.collides(x,z,3)||this.map.objectives.some(o=>dist(o,{x,z})<30))continue;const y=this.map.heightAt(x,z);if(i%4===0)this.batch('desert-outcrop',rock,stone,x,y+1.4,z,0,2.4,1.5,2.6);else this.batch('dry-cactus',cactus,dry,x,y+1.3,z,0,1,1,1);}
 }
 dispose(){for(const m of this.cache)m.dispose()}
}
