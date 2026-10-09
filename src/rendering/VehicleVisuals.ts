import * as THREE from 'three';
import {mergeGeometries} from 'three/addons/utils/BufferGeometryUtils.js';
import type {Battle} from '../core/Battle';
import {heightAt} from '../core/math';
import type {ArmoredVehicle} from '../vehicles/Vehicle';

type Visual={vehicle:ArmoredVehicle;root:THREE.Group;turret:THREE.Group;mg:THREE.Group;gun:THREE.Group;parachute:THREE.Group;wheels:THREE.Mesh[];materials:THREE.Material[];badge:THREE.Sprite;smoke:THREE.Sprite[];canvas:HTMLCanvasElement;hp:number};

/** Detailed but lightweight fictional armored ground vehicles for the combined-arms phase. */
export class VehicleVisuals {
 private visuals:Visual[]=[];
 constructor(private scene:THREE.Scene,private battle:Battle){for(const vehicle of battle.vehicles)this.visuals.push(this.build(vehicle))}
 private build(vehicle:ArmoredVehicle):Visual{
  const root=new THREE.Group();root.userData.vehicleId=vehicle.id;const materials:THREE.Material[]=[];
  const mat=(color:number,roughness=.78,metalness=.28)=>{const m=new THREE.MeshStandardMaterial({color,roughness,metalness});materials.push(m);return m};
  const armor=mat(vehicle.team==='blue'?0x506b70:0x755a52,.82,.33),armorDark=mat(0x273033,.72,.5),trim=mat(vehicle.team==='blue'?0x79a5a8:0xa9796b,.68,.3),rubber=mat(0x15191a,.98,.02),glass=mat(0x18323b,.23,.45),steel=mat(0x303a3e,.55,.72),lamp=mat(0xf1d5a0,.28,.25);
  const box=(w:number,h:number,d:number,m:THREE.Material,x:number,y:number,z:number,parent:THREE.Object3D=root)=>{const mesh=new THREE.Mesh(new THREE.BoxGeometry(w,h,d),m);mesh.position.set(x,y,z);mesh.castShadow=true;mesh.receiveShadow=true;parent.add(mesh);return mesh};
  // Lower chassis and layered armor.
  box(4.7,.75,7.4,armorDark,0,.74,0);box(4.35,1.05,6.45,armor,0,1.32,.05);const nose=box(4.16,.78,2.15,armor,0,1.78,2.52);nose.rotation.x=-.20;
  box(4.58,.24,5.7,trim,0,1.91,-.20);box(4.64,.45,.35,armorDark,0,1.30,-3.42);box(4.48,.36,.32,trim,0,1.76,3.15);
  // Side skirts and detail rails.
  for(const x of [-2.40,2.40]){box(.22,.78,6.5,armor,x,1.00,0);box(.10,.18,5.8,trim,x*1.015,1.58,.05);for(const z of [-2.65,-.92,.92,2.65])box(.12,.50,.16,steel,x*1.025,1.17,z)}
  const wheels:THREE.Mesh[]=[];
  for(const x of [-2.20,2.20])for(const z of [-2.55,-.87,.87,2.55]){const wheel=new THREE.Mesh(new THREE.CylinderGeometry(.66,.66,.46,14),rubber);wheel.rotation.z=Math.PI/2;wheel.position.set(x,.68,z);wheel.castShadow=true;root.add(wheel);wheels.push(wheel);const hub=new THREE.Mesh(new THREE.CylinderGeometry(.27,.27,.49,12),steel);hub.rotation.z=Math.PI/2;hub.position.copy(wheel.position);root.add(hub)}
  // Turret can rotate independently from hull.
  const turret=new THREE.Group();turret.position.set(0,2.10,-.10);root.add(turret);
  box(3.05,.78,2.65,armor,0,.35,0,turret);const turretFront=box(2.72,.64,1.15,trim,0,.39,1.55,turret);turretFront.rotation.x=-.14;
  box(1.22,.46,1.05,armorDark,-.58,.92,-.30,turret);box(.88,.28,.72,glass,.58,.88,.40,turret);
  const gun=new THREE.Group();gun.position.y=.5;turret.add(gun);
  const barrel=new THREE.Mesh(new THREE.CylinderGeometry(.14,.19,4.75,12),steel);barrel.rotation.x=Math.PI/2;barrel.position.set(0,0,3.35);barrel.castShadow=true;gun.add(barrel);
  const muzzle=new THREE.Mesh(new THREE.CylinderGeometry(.25,.25,.55,12),armorDark);muzzle.rotation.x=Math.PI/2;muzzle.position.set(0,0,5.65);gun.add(muzzle);
  if(['tank','ifv'].includes(vehicle.kind))box(.10,.10,2,steel,.45,0,2.35,gun);
  const antenna=new THREE.Mesh(new THREE.CylinderGeometry(.025,.035,2.6,7),steel);antenna.position.set(-.96,2.00,-.78);turret.add(antenna);
  const mg=new THREE.Group();mg.position.set(.8,3.4,-.25);root.add(mg);box(.24,.22,1.65,steel,0,.15,.7,mg);box(.35,.4,.45,armorDark,0,0,0,mg);
  // Lamps, hatches and faction markings.
  for(const x of [-1.45,1.45]){const light=box(.42,.30,.14,lamp,x,1.58,3.43);light.castShadow=false;box(.55,.10,.30,armorDark,x,1.88,2.72)}
  const mark=box(1.12,.12,.70,trim,0,1.96,-1.72);mark.rotation.y=.03;
  if(vehicle.kind==='tank')for(const x of [-2.25,2.25])box(.65,1.05,7.4,rubber,x,.63,0);
  if(vehicle.kind==='transport'){root.scale.set(.6,.6,.7);turret.scale.set(.5,.6,.2);box(3,.9,2.3,armorDark,0,2,-1.5);box(.12,1.15,2.6,steel,-1.5,2.25,-1.4);box(.12,1.15,2.6,steel,1.5,2.25,-1.4);}
  else if(vehicle.kind==='scout'){root.scale.set(.65,.75,.66);turret.scale.set(.65,.6,.45)}
  else root.scale.setScalar(vehicle.kind==='tank'?1.08:.96);
  if(vehicle.kind==='aa'){gun.clear();for(const x of [-.55,.55])for(const y of [-.15,.15]){const barrel=new THREE.Mesh(new THREE.CylinderGeometry(.06,.06,5.2,8),steel);barrel.rotation.x=Math.PI/2;barrel.position.set(x,y,3.325);gun.add(barrel);}const radar=box(1.9,.12,1.1,glass,0,1.7,-.5,turret);radar.rotation.x=.8;}
  if(vehicle.kind==='helicopter'||vehicle.kind==='jet'){
   root.clear();root.scale.setScalar(.96);root.add(turret,mg);turret.clear();turret.add(gun);turret.position.set(0,2.1,-.1);gun.clear();box(2.3,2.1,7,armor,0,1.8,0);box(1.9,1.1,2.2,glass,0,2.3,2.1);box(1,.7,5,armorDark,0,1.6,-4.5);box(3.2,.16,1.6,trim,0,2,-5);box(.16,2,1.6,trim,0,2.7,-5);const barrel=new THREE.Mesh(new THREE.CylinderGeometry(.08,.08,3,10),steel);barrel.rotation.x=Math.PI/2;barrel.position.set(0,0,4.425);gun.add(barrel);
   if(vehicle.kind==='helicopter'){const rotor=new THREE.Group();rotor.name='rotor';rotor.position.y=3.4;root.add(rotor);box(14,.1,.24,steel,0,0,0,rotor);box(.24,.1,14,steel,0,0,0,rotor);for(const x of [-1.5,1.5])box(.15,.15,5,steel,x,.25,0);}
   else {const wing=box(13,.18,3.6,armor,0,1.4,-.8);wing.rotation.y=.1;box(1.4,.65,1.8,armorDark,0,1.5,-4);}
  }
  // Batch static pieces per material, retaining independent turret and wheel motion.
  for(const parent of [root,turret,mg,gun]){const groups=new Map<THREE.Material,THREE.Mesh[]>();for(const child of [...parent.children])if(child instanceof THREE.Mesh&&!wheels.includes(child)&&!Array.isArray(child.material)){const list=groups.get(child.material)??[];list.push(child);groups.set(child.material,list);}
   for(const [material,parts] of groups){if(parts.length<2)continue;const geometries=parts.map(mesh=>{mesh.updateMatrix();return mesh.geometry.clone().applyMatrix4(mesh.matrix)}),merged=mergeGeometries(geometries,false);for(const geometry of geometries)geometry.dispose();if(!merged)continue;for(const mesh of parts){parent.remove(mesh);mesh.geometry.dispose();}const batch=new THREE.Mesh(merged,material);batch.castShadow=true;batch.receiveShadow=true;parent.add(batch);}}
  const parachute=new THREE.Group();const fabric=new THREE.MeshStandardMaterial({color:0xb2ac86,side:THREE.DoubleSide,roughness:1});materials.push(fabric);const canopy=new THREE.Mesh(new THREE.SphereGeometry(10,16,8,0,Math.PI*2,0,Math.PI/2),fabric);canopy.scale.set(1,.45,1);canopy.position.y=24;parachute.add(canopy);const cord=new THREE.LineBasicMaterial({color:0xd5cba5});materials.push(cord);for(const x of [-1,1])for(const z of [-1,1])parachute.add(new THREE.Line(new THREE.BufferGeometry().setFromPoints([new THREE.Vector3(x*2,2,z*2),new THREE.Vector3(x*6.5,24,z*6.5)]),cord));parachute.visible=false;root.add(parachute);
  const canvas=document.createElement('canvas');canvas.width=128;canvas.height=32;const badge=new THREE.Sprite(new THREE.SpriteMaterial({map:new THREE.CanvasTexture(canvas),transparent:true,depthTest:true,depthWrite:false}));badge.position.y=5;badge.scale.set(5,1.25,1);root.add(badge);
  const tex=document.createElement('canvas');tex.width=64;tex.height=64;const c=tex.getContext('2d')!,gradient=c.createRadialGradient(32,32,0,32,32,30);gradient.addColorStop(0,'rgba(20,22,22,.7)');gradient.addColorStop(1,'rgba(20,22,22,0)');c.fillStyle=gradient;c.fillRect(0,0,64,64);const texture=new THREE.CanvasTexture(tex),smoke:THREE.Sprite[]=[];for(let i=0;i<4;i++){const m=new THREE.SpriteMaterial({map:texture,transparent:true,opacity:.6,depthWrite:false});materials.push(m);const sprite=new THREE.Sprite(m);root.add(sprite);smoke.push(sprite);}materials.push(badge.material);
  this.scene.add(root);return {vehicle,root,turret,mg,gun,parachute,wheels,materials,badge,smoke,canvas,hp:-1};
 }
 private removeVisual(v:Visual){this.scene.remove(v.root);v.root.traverse(o=>{const m=o as THREE.Mesh;m.geometry?.dispose()});for(const m of v.materials){const texture=(m as THREE.SpriteMaterial).map;if(texture)texture.dispose();m.dispose();}}
 update(dt:number,firstPerson=false,network=false){
  for(let i=this.visuals.length-1;i>=0;i--){const v=this.visuals[i]!;if(this.battle.vehicles[v.vehicle.id]!==v.vehicle){this.removeVisual(v);this.visuals.splice(i,1)}}
  for(const v of this.battle.vehicles)if(!this.visuals.some(item=>item.vehicle===v))this.visuals.push(this.build(v));
  for(const v of this.visuals){const item=v.vehicle,root=v.root;const smooth=network&&root.position.distanceTo(new THREE.Vector3(item.pos.x,root.position.y,item.pos.z))<40;root.position.set(smooth?THREE.MathUtils.damp(root.position.x,item.pos.x,14,dt):item.pos.x,heightAt(item.pos.x,item.pos.z)+item.altitude+.05,smooth?THREE.MathUtils.damp(root.position.z,item.pos.z,14,dt):item.pos.z);if(item.airborne)root.position.y+=Math.max(2,(8-(this.battle.elapsed-item.dropStarted))*10);v.parachute.visible=item.airborne;v.gun.rotation.x=-item.turretPitch;root.rotation.y=item.yaw;const rel=Math.atan2(Math.sin(item.turretYaw-item.yaw),Math.cos(item.turretYaw-item.yaw));v.mg.rotation.x=-item.turretPitch;v.mg.rotation.y=Math.atan2(Math.sin(item.mgYaw-item.yaw),Math.cos(item.mgYaw-item.yaw));v.turret.rotation.y=rel;for(const wheel of v.wheels)wheel.rotation.x+=item.speed*dt*.42;
   const disabled=!item.alive;root.rotation.z=THREE.MathUtils.damp(root.rotation.z,disabled?(item.id%2 ? .10 : -.10):0,3,dt);root.position.y-=disabled ? .10 : 0;root.visible=(!disabled||this.battle.elapsed-item.disabledAt<12)&&!(this.battle.playerVehicle===item&&firstPerson);root.scale.setScalar((item.kind==='tank'?1.08:item.kind==='scout'?.66:item.kind==='transport'?.7:.96)*(disabled?.92:1));root.traverse(o=>{if(o instanceof THREE.Mesh&&!Array.isArray(o.material)&&o.material instanceof THREE.MeshStandardMaterial){if(!o.material.userData.originalColor)o.material.userData.originalColor=o.material.color.getHex();o.material.color.setHex(disabled?0x272725:o.material.userData.originalColor);}});v.turret.position.y=disabled?1.4:2.1;v.gun.rotation.z=disabled?.18:0;
   v.badge.visible=item.alive&&item.team===this.battle.player.team&&this.battle.playerVehicle!==item&&this.battle.canIdentify(item.pos,120);if(v.hp!==Math.ceil(item.hp)){v.hp=Math.ceil(item.hp);const c=v.canvas.getContext('2d')!;c.clearRect(0,0,128,32);c.fillStyle='#0a161acc';c.fillRect(0,0,128,32);c.fillStyle='#76d7e3';c.fillRect(4,22,120*item.hp/item.maxHp,5);c.font='bold 13px Arial';c.fillText(`${item.kind.toUpperCase()} ${v.hp}`,4,15);(v.badge.material.map as THREE.CanvasTexture).needsUpdate=true;}
   for(const [i,smoke] of v.smoke.entries()){smoke.visible=(!item.alive&&this.battle.elapsed-item.disabledAt<12)||(item.alive&&item.hp/item.maxHp<.45);const phase=(this.battle.elapsed*.3+i*.25)%1;smoke.position.set(Math.sin(this.battle.elapsed+i)*phase,3+phase*10,-1-phase*2);smoke.scale.setScalar(2+phase*6);smoke.material.opacity=(1-phase)*.65;}const rotor=root.getObjectByName('rotor');if(rotor)rotor.rotation.y+=dt*34*item.enginePower;
  }}
 dispose(){const geometries=new Set<THREE.BufferGeometry>(),materials=new Set<THREE.Material>();for(const v of this.visuals){this.scene.remove(v.root);v.root.traverse((o:THREE.Object3D)=>{const m=o as THREE.Mesh;if(m.geometry instanceof THREE.BufferGeometry)geometries.add(m.geometry);if(m.material){for(const x of Array.isArray(m.material)?m.material:[m.material])materials.add(x)}})}for(const g of geometries)g.dispose();for(const m of materials){const texture=(m as THREE.SpriteMaterial).map;if(texture)texture.dispose();m.dispose();}}
}
