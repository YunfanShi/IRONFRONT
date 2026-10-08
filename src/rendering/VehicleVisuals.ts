import * as THREE from 'three';
import {mergeGeometries} from 'three/addons/utils/BufferGeometryUtils.js';
import type {Battle} from '../core/Battle';
import {heightAt} from '../core/math';
import type {ArmoredVehicle} from '../vehicles/Vehicle';

type Visual={vehicle:ArmoredVehicle;root:THREE.Group;turret:THREE.Group;mg:THREE.Group;gun:THREE.Group;parachute:THREE.Group;wheels:THREE.Mesh[];materials:THREE.Material[]};

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
  const antenna=new THREE.Mesh(new THREE.CylinderGeometry(.025,.035,2.6,7),steel);antenna.position.set(-.96,2.00,-.78);turret.add(antenna);
  const mg=new THREE.Group();mg.position.set(.8,3.4,-.25);root.add(mg);box(.24,.22,1.65,steel,0,.15,.7,mg);box(.35,.4,.45,armorDark,0,0,0,mg);
  // Lamps, hatches and faction markings.
  for(const x of [-1.45,1.45]){const light=box(.42,.30,.14,lamp,x,1.58,3.43);light.castShadow=false;box(.55,.10,.30,armorDark,x,1.88,2.72)}
  const mark=box(1.12,.12,.70,trim,0,1.96,-1.72);mark.rotation.y=.03;
  if(vehicle.kind==='tank')for(const x of [-2.25,2.25])box(.65,1.05,7.4,rubber,x,.63,0);
  if(vehicle.kind==='transport'){root.scale.set(.6,.6,.7);turret.scale.set(.5,.6,.2);box(3,.9,2.3,armorDark,0,2,-1.5);box(.12,1.15,2.6,steel,-1.5,2.25,-1.4);box(.12,1.15,2.6,steel,1.5,2.25,-1.4);}
  else if(vehicle.kind==='scout'){root.scale.set(.65,.75,.66);turret.scale.set(.65,.6,.45)}
  else root.scale.setScalar(vehicle.kind==='tank'?1.08:.96);
  // Batch static pieces per material, retaining independent turret and wheel motion.
  for(const parent of [root,turret,mg,gun]){const groups=new Map<THREE.Material,THREE.Mesh[]>();for(const child of [...parent.children])if(child instanceof THREE.Mesh&&!wheels.includes(child)&&!Array.isArray(child.material)){const list=groups.get(child.material)??[];list.push(child);groups.set(child.material,list);}
   for(const [material,parts] of groups){if(parts.length<2)continue;const geometries=parts.map(mesh=>{mesh.updateMatrix();return mesh.geometry.clone().applyMatrix4(mesh.matrix)}),merged=mergeGeometries(geometries,false);for(const geometry of geometries)geometry.dispose();if(!merged)continue;for(const mesh of parts){parent.remove(mesh);mesh.geometry.dispose();}const batch=new THREE.Mesh(merged,material);batch.castShadow=true;batch.receiveShadow=true;parent.add(batch);}}
  const parachute=new THREE.Group();const fabric=new THREE.MeshStandardMaterial({color:0xb2ac86,side:THREE.DoubleSide,roughness:1});materials.push(fabric);const canopy=new THREE.Mesh(new THREE.SphereGeometry(10,16,8,0,Math.PI*2,0,Math.PI/2),fabric);canopy.scale.set(1,.45,1);canopy.position.y=24;parachute.add(canopy);const cord=new THREE.LineBasicMaterial({color:0xd5cba5});materials.push(cord);for(const x of [-1,1])for(const z of [-1,1])parachute.add(new THREE.Line(new THREE.BufferGeometry().setFromPoints([new THREE.Vector3(x*2,2,z*2),new THREE.Vector3(x*6.5,24,z*6.5)]),cord));parachute.visible=false;root.add(parachute);
  this.scene.add(root);return {vehicle,root,turret,mg,gun,parachute,wheels,materials};
 }
 private removeVisual(v:Visual){this.scene.remove(v.root);v.root.traverse(o=>{const m=o as THREE.Mesh;m.geometry?.dispose()});for(const m of v.materials)m.dispose()}
 update(dt:number){
  for(let i=this.visuals.length-1;i>=0;i--){const v=this.visuals[i]!;if(this.battle.vehicles[v.vehicle.id]!==v.vehicle){this.removeVisual(v);this.visuals.splice(i,1)}}
  for(const v of this.battle.vehicles)if(!this.visuals.some(item=>item.vehicle===v))this.visuals.push(this.build(v));
  for(const v of this.visuals){const item=v.vehicle,root=v.root;root.position.set(item.pos.x,heightAt(item.pos.x,item.pos.z)+.05,item.pos.z);if(item.airborne)root.position.y+=Math.max(2,(8-(this.battle.elapsed-item.dropStarted))*10);v.parachute.visible=item.airborne;v.gun.rotation.x=-item.turretPitch;root.rotation.y=item.yaw;const rel=Math.atan2(Math.sin(item.turretYaw-item.yaw),Math.cos(item.turretYaw-item.yaw));v.mg.rotation.y=Math.atan2(Math.sin(item.mgYaw-item.yaw),Math.cos(item.mgYaw-item.yaw));v.turret.rotation.y=rel;for(const wheel of v.wheels)wheel.rotation.x+=item.speed*dt*.42;
   const disabled=!item.alive;root.rotation.z=THREE.MathUtils.damp(root.rotation.z,disabled?(item.id%2 ? .10 : -.10):0,3,dt);root.position.y-=disabled ? .10 : 0;root.visible=!(this.battle.playerVehicle===item&&this.battle.playerSeat===1);
  }}
 dispose(){const geometries=new Set<THREE.BufferGeometry>(),materials=new Set<THREE.Material>();for(const v of this.visuals){this.scene.remove(v.root);v.root.traverse((o:THREE.Object3D)=>{const m=o as THREE.Mesh;if(m.geometry instanceof THREE.BufferGeometry)geometries.add(m.geometry);if(m.material){for(const x of Array.isArray(m.material)?m.material:[m.material])materials.add(x)}})}for(const g of geometries)g.dispose();for(const m of materials)m.dispose()}
}
