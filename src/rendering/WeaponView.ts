import * as THREE from 'three';
import {buildWeaponModel,MODEL_PROFILES} from './WeaponModels';
import {WEAPONS,WEAPON_ORDER,type WeaponId} from '../combat/Weapons';

/** Detailed first-person fictional weapons with damped ADS, sprint, reload, swap, sway and recoil. */
export class WeaponView {
 readonly group=new THREE.Group();
 private models:Record<WeaponId,THREE.Group>={} as Record<WeaponId,THREE.Group>;
 private flash:THREE.Sprite;private flashLight:THREE.PointLight;
 private animation=0;private recoil=0;private swayX=0;private swayY=0;private adsBlend=0;private sprintBlend=0;private bob=0;private flashLife=0;
 private throwMotion=0;
 muzzleWorldPosition(){this.camera.updateMatrixWorld(true);this.group.updateWorldMatrix(true,false);return this.group.localToWorld(new THREE.Vector3(0,.02,MODEL_PROFILES[this.current].muzzle));}
 get aimBlend(){return this.adsBlend}
 throw(){this.throwMotion=1;this.adsBlend=0}
 private reloadMotion=0;private lastReloading=false;private swapMotion=0;private current:WeaponId='carbine';
 private shellGroup=new THREE.Group();private shells:{mesh:THREE.Mesh;vel:THREE.Vector3;age:number}[]=[];
 private materials:THREE.Material[]=[];
 constructor(private camera:THREE.PerspectiveCamera,private scene:THREE.Scene){
  const steel=this.mat(0x242b32,.42,.72),matte=this.mat(0x171d20,.78,.25),grip=this.mat(0x353d3b,.98),tan=this.mat(0x877e6a,.84),glove=this.mat(0x3a413b,1),gold=this.mat(0xc49a4f,.4,.85),glass=this.mat(0x13252c,.18,.42);
  for(const id of WEAPON_ORDER){this.models[id]=buildWeaponModel(id,{steel,matte,grip,tan,glove,gold,glass});this.group.add(this.models[id]);this.models[id].visible=id==='carbine';}
  const can=document.createElement('canvas');can.width=96;can.height=96;const ctx=can.getContext('2d')!;
  const gradient=ctx.createRadialGradient(48,48,1,48,48,46);gradient.addColorStop(0,'rgba(255,255,250,1)');gradient.addColorStop(.19,'rgba(255,233,145,.88)');gradient.addColorStop(.47,'rgba(255,161,58,.47)');gradient.addColorStop(1,'rgba(255,122,40,0)');ctx.fillStyle=gradient;ctx.fillRect(0,0,96,96);
  this.flash=new THREE.Sprite(new THREE.SpriteMaterial({map:new THREE.CanvasTexture(can),transparent:true,depthWrite:false,blending:THREE.AdditiveBlending}));this.flash.position.set(0,.02,-1.54);this.flash.scale.set(.66,.66,.66);this.group.add(this.flash);this.flash.visible=false;
  this.flashLight=new THREE.PointLight(0xffb86e,0,8,2);this.flashLight.position.set(0,.025,-1.54);this.group.add(this.flashLight);
  this.group.scale.setScalar(.65);this.group.position.set(.28,-.38,-.4);this.camera.add(this.group);this.scene.add(this.camera);this.camera.add(this.shellGroup);
 }
 private mat(color:number,roughness:number,metalness=0){const m=new THREE.MeshStandardMaterial({color,roughness,metalness});this.materials.push(m);return m}
 setWeapon(id:WeaponId){if(id===this.current)return;this.current=id;for(const key of WEAPON_ORDER)this.models[key].visible=key===id;this.swapMotion=1;this.adsBlend=0;this.recoil=0;const muzzle=MODEL_PROFILES[id].muzzle;this.flash.position.z=muzzle;this.flashLight.position.z=muzzle}
 onMouse(dx:number,dy:number){this.swayX=THREE.MathUtils.clamp(this.swayX-dx*.00011,-.028,.028);this.swayY=THREE.MathUtils.clamp(this.swayY+dy*.00010,-.023,.023)}
 shoot(id:WeaponId){this.setWeapon(id);this.flashLife=.085;this.recoil=Math.min(.42,this.recoil+WEAPONS[id].recoil);this.spawnShell()}
 private spawnShell(){
  if(this.shells.length>=14){const old=this.shells.shift()!;this.shellGroup.remove(old.mesh);old.mesh.geometry.dispose();(old.mesh.material as THREE.Material).dispose()}
  const metal=new THREE.MeshStandardMaterial({color:0xb79b5b,metalness:.9,roughness:.24});const casing=new THREE.Mesh(new THREE.CylinderGeometry(.035,.035,.13,8),metal);
  casing.position.set(.30,-.20,-.72);casing.rotation.z=Math.PI*.3;this.shellGroup.add(casing);this.shells.push({mesh:casing,vel:new THREE.Vector3(2.6+Math.random(),1.6+Math.random()*.5,-.5),age:0});
 }
 render(dt:number,params:{weapon:WeaponId;ads:boolean;sprint:boolean;moving:boolean;speed:number;reload:boolean;alive:boolean;velocitySide:number}){
  dt=Math.min(dt,.065);this.throwMotion=Math.max(0,this.throwMotion-dt*1.8);this.setWeapon(params.weapon);const t=1-Math.exp(-dt*12);this.group.visible=params.alive;
  const targetAim=params.ads&&!params.sprint&&!params.reload&&this.swapMotion<.3&&this.throwMotion===0?1:0;this.adsBlend=THREE.MathUtils.lerp(this.adsBlend,targetAim,t);for(const id of WEAPON_ORDER)this.models[id].visible=id===this.current&&!(WEAPONS[id].zoom>=3&&this.adsBlend>.72);this.sprintBlend=THREE.MathUtils.lerp(this.sprintBlend,params.sprint&&params.moving?1:0,t*.75);
  if(params.reload&&!this.lastReloading)this.reloadMotion=0;if(params.reload)this.reloadMotion=Math.min(1,this.reloadMotion+dt/WEAPONS[params.weapon].reload);else this.reloadMotion=THREE.MathUtils.lerp(this.reloadMotion,0,t*.5);this.lastReloading=params.reload;
  this.swapMotion=Math.max(0,this.swapMotion-dt*2.55);const swapArc=Math.sin(Math.min(1,this.swapMotion)*Math.PI),reloadArc=params.reload?Math.sin(this.reloadMotion*Math.PI):0;
  if(params.moving)this.bob+=dt*(params.sprint?15:params.ads?7.5:11.6)*Math.max(.45,params.speed);this.animation+=dt;const m=params.moving?1:0;
  const bobX=Math.sin(this.bob)*.019*m*(params.sprint?1.5:1)*(1-this.adsBlend*.8),bobY=Math.abs(Math.cos(this.bob))*.018*m*(params.sprint?1.5:1)*(1-this.adsBlend*.8),breathing=Math.sin(this.animation*1.7)*.004;
  this.swayX=THREE.MathUtils.damp(this.swayX,0,6,dt);this.swayY=THREE.MathUtils.damp(this.swayY,0,6,dt);
  this.group.position.x=THREE.MathUtils.lerp(.28,0,this.adsBlend)+bobX+this.swayX+.10*swapArc;
  this.group.position.y=THREE.MathUtils.lerp(-.38,-MODEL_PROFILES[params.weapon].sight*.65,this.adsBlend)+bobY+breathing+this.swayY-this.recoil*.07-reloadArc*.22-swapArc*.34-Math.sin(this.throwMotion*Math.PI)*.4;
  this.group.position.z=THREE.MathUtils.lerp(-.4,-.52,this.adsBlend)+this.recoil*.11;
  this.group.rotation.set(-this.recoil*.21+reloadArc*.62+swapArc*.72-Math.sin(this.throwMotion*Math.PI)*.8-this.sprintBlend*.28,swapArc*.10,this.sprintBlend*.35+reloadArc*.24+params.velocitySide*.015);
  this.recoil=THREE.MathUtils.damp(this.recoil,0,15,dt);this.flashLife=Math.max(0,this.flashLife-dt);this.flash.visible=this.flashLife>0;this.flashLight.intensity=this.flashLife>0?this.flashLife*85:0;
  for(let i=this.shells.length-1;i>=0;i--){const shell=this.shells[i]!;shell.age+=dt;shell.vel.y-=9.5*dt;shell.mesh.position.addScaledVector(shell.vel,dt);shell.mesh.rotation.x+=dt*13;shell.mesh.rotation.z+=dt*17;if(shell.age>1.1){this.shellGroup.remove(shell.mesh);shell.mesh.geometry.dispose();(shell.mesh.material as THREE.Material).dispose();this.shells.splice(i,1)}}
  return this.adsBlend;
 }
 dispose(){
  const geometries=new Set<THREE.BufferGeometry>(),materials=new Set<THREE.Material>(),textures=new Set<THREE.Texture>();
  for(const parent of [this.group,this.shellGroup])parent.traverse((obj:THREE.Object3D)=>{const mesh=obj as THREE.Mesh;if(mesh.geometry instanceof THREE.BufferGeometry)geometries.add(mesh.geometry);if(mesh.material)for(const mat of Array.isArray(mesh.material)?mesh.material:[mesh.material]){materials.add(mat);const mm=mat as THREE.MeshStandardMaterial;if(mm.map)textures.add(mm.map)}});
  for(const g of geometries)g.dispose();for(const m of materials)m.dispose();for(const m of this.materials)m.dispose();for(const t of textures)t.dispose();this.camera.remove(this.group);this.camera.remove(this.shellGroup);
 }
}
