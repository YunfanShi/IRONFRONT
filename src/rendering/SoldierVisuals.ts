import * as THREE from 'three';
import type {Battle,Soldier} from '../core/Battle';
import {heightAt} from '../core/math';
import type {Team} from '../world/Layout';

type PartKind='rig'|'head'|'armL'|'armR'|'legL'|'legR'|'weapon';
type Part={offset:[number,number,number];kind:PartKind;geometry:THREE.BufferGeometry;material:THREE.Material};
type Army={meshes:THREE.InstancedMesh[];soldiers:Soldier[];parts:Part[]};
type VisualState={x:number;z:number;yaw:number;phase:number;speed:number;deathLean:number};

/**
 * Articulated instanced soldiers. Each limb is one InstancedMesh per faction, so
 * a 32v32 battle keeps draw calls bounded while still showing gait, aiming,
 * crouching, suppression posture and a short non-gory fall animation.
 */
export class SoldierVisuals {
 private armies:Army[]=[];
 private states=new Map<number,VisualState>();
 private object=new THREE.Object3D();
 constructor(private scene:THREE.Scene,battle:Battle){
  const plate=new THREE.MeshStandardMaterial({color:0x263339,roughness:.92,metalness:.05});
  const boots=new THREE.MeshStandardMaterial({color:0x202527,roughness:.99});
  const skin=new THREE.MeshStandardMaterial({color:0x806e5d,roughness:1});
  const visor=new THREE.MeshStandardMaterial({color:0x132830,metalness:.34,roughness:.27});
  const steel=new THREE.MeshStandardMaterial({color:0x20292f,metalness:.74,roughness:.38});
  const geoBox=(w:number,h:number,d:number)=>new THREE.BoxGeometry(w,h,d);
  for(const team of ['blue','red'] as Team[]){
   const suit=new THREE.MeshStandardMaterial({color:team==='blue'?0x365f73:0x806257,roughness:.96});
   const trim=new THREE.MeshStandardMaterial({color:team==='blue'?0x6d9eae:0xaa826b,roughness:.84});
   const parts:Part[]=[
    {geometry:geoBox(.18,.20,.07),material:trim,offset:[-.38,1.78,.27],kind:'rig'},
    {geometry:geoBox(.18,.20,.07),material:trim,offset:[.38,1.78,.27],kind:'rig'},
    {geometry:geoBox(.60,.25,.19),material:plate,offset:[0,1.22,.33],kind:'rig'},
    {geometry:geoBox(.22,.18,.16),material:plate,offset:[-.26,.69,.23],kind:'legL'},
    {geometry:geoBox(.22,.18,.16),material:plate,offset:[.26,.69,.23],kind:'legR'},
    {geometry:geoBox(.78,1.02,.43),material:suit,offset:[0,1.45,0],kind:'rig'},
    {geometry:geoBox(.82,.39,.50),material:plate,offset:[0,1.55,.04],kind:'rig'},
    {geometry:geoBox(.39,.24,.57),material:trim,offset:[0,2.65,0],kind:'head'},
    {geometry:new THREE.SphereGeometry(.26,9,7),material:skin,offset:[0,2.30,0],kind:'head'},
    {geometry:geoBox(.45,.15,.23),material:visor,offset:[0,2.36,.20],kind:'head'},
    {geometry:geoBox(.34,.80,.38),material:suit,offset:[-.60,1.46,.07],kind:'armL'},
    {geometry:geoBox(.34,.80,.38),material:suit,offset:[.60,1.46,.07],kind:'armR'},
    {geometry:geoBox(.40,.99,.40),material:suit,offset:[-.26,.65,0],kind:'legL'},
    {geometry:geoBox(.40,.99,.40),material:suit,offset:[.26,.65,0],kind:'legR'},
    {geometry:geoBox(.45,.23,.60),material:boots,offset:[-.26,.15,.12],kind:'legL'},
    {geometry:geoBox(.45,.23,.60),material:boots,offset:[.26,.15,.12],kind:'legR'},
    {geometry:geoBox(.67,.81,.36),material:plate,offset:[0,1.37,-.39],kind:'rig'},
    {geometry:geoBox(.23,.25,.95),material:steel,offset:[.15,1.52,.59],kind:'weapon'},
    {geometry:geoBox(.31,.27,.36),material:trim,offset:[.40,1.12,.10],kind:'armR'},
    {geometry:geoBox(.13,.16,.43),material:steel,offset:[.15,1.55,1.14],kind:'weapon'},
    {geometry:geoBox(.14,.16,.17),material:trim,offset:[-.40,1.26,.33],kind:'armL'}
   ];
   const soldiers=battle.soldiers.filter(s=>!s.player&&s.team===team);
   const meshes=parts.map(p=>{
    const mesh=new THREE.InstancedMesh(p.geometry,p.material,soldiers.length);
    mesh.instanceMatrix.setUsage(THREE.DynamicDrawUsage);mesh.frustumCulled=false;mesh.castShadow=false;mesh.receiveShadow=true;this.scene.add(mesh);return mesh
   });
   for(const s of soldiers)this.states.set(s.id,{x:s.pos.x,z:s.pos.z,yaw:s.yaw,phase:s.id*.87,speed:0,deathLean:s.id%2?1:-1});
   this.armies.push({meshes,soldiers,parts});
  }
 }
 update(battle:Battle,dt:number){
  const blend=1-Math.exp(-dt*13);
  for(const army of this.armies){
   for(let i=0;i<army.soldiers.length;i++){
    const soldier=army.soldiers[i]!,state=this.states.get(soldier.id)!;
    if(Math.hypot(soldier.pos.x-state.x,soldier.pos.z-state.z)>42){state.x=soldier.pos.x;state.z=soldier.pos.z;state.speed=0}
    const priorX=state.x,priorZ=state.z;
    state.x=THREE.MathUtils.lerp(state.x,soldier.pos.x,blend);state.z=THREE.MathUtils.lerp(state.z,soldier.pos.z,blend);
    const angle=Math.atan2(Math.sin(soldier.yaw-state.yaw),Math.cos(soldier.yaw-state.yaw));state.yaw+=angle*blend;
    const moveSpeed=Math.hypot(state.x-priorX,state.z-priorZ)/Math.max(dt,.001);
    state.speed=THREE.MathUtils.damp(state.speed,Math.min(1,moveSpeed/8.2),8,dt);
    const sprinting=soldier.state==='RETREAT'||soldier.state==='MOVE_TO_OBJECTIVE'||soldier.state==='FOLLOW_SQUAD';
    const fighting=soldier.state==='ENGAGE'||soldier.state==='SEEK_COVER';
    const crouching=soldier.state==='SEEK_COVER'||(soldier.state==='DEFEND'&&soldier.id%3===0);
    state.phase+=dt*((sprinting?3.1:2.0)+state.speed*(sprinting?13.2:10.4));
    const stride=Math.sin(state.phase)*state.speed*(fighting ? .52 : 1),step=Math.abs(Math.cos(state.phase))*state.speed;
    const footY=heightAt(state.x,state.z),bob=step*.052*(crouching ? .45 : 1);
    const sY=Math.sin(state.yaw),cY=Math.cos(state.yaw);
    const deathAge=battle.elapsed-soldier.deathAt,fall=!soldier.alive?Math.min(1,Math.max(0,deathAge/.72)):0;
    const corpseVisible=soldier.vehicleId===null&&(soldier.alive||deathAge<3.7);const crouchDrop=crouching ? .38 : 0;
    for(let j=0;j<army.parts.length;j++){
     const part=army.parts[j]!,[lx,ly,lz]=part.offset;let zz=lz,yy=ly-crouchDrop,rx=0,rz=0;
     if(part.kind==='legL'){rx=stride*.38+(crouching ? .30 : 0);zz+=stride*.21;yy+=Math.max(0,-stride)*.09}
     else if(part.kind==='legR'){rx=-stride*.38+(crouching ? .30 : 0);zz-=stride*.21;yy+=Math.max(0,stride)*.09}
     else if(part.kind==='armL'){rx=fighting?-.78:-stride*.17-.45;zz+=fighting ? .25 : .13;yy-=.035}
     else if(part.kind==='armR'){rx=fighting?-.70:stride*.14-.44;zz+=fighting ? .24 : .12;yy-=.035}
     else if(part.kind==='head'){rx=fighting?-.03:step*.014;rz=Math.sin(state.phase*.47)*.022}
     else if(part.kind==='rig'){yy+=bob;rz=stride*.025}
     else if(part.kind==='weapon'){yy+=bob*.7;rx=fighting?-.03:-.12}
     if(fall>0){yy-=fall*(.45+ly*.18);rz+=state.deathLean*fall*1.15;rx+=fall*.12}
     this.object.position.set(state.x+(cY*lx+sY*zz)*.67,footY+yy*.67,state.z+(-sY*lx+cY*zz)*.67);
     this.object.rotation.set(rx,state.yaw,rz,'YXZ');this.object.scale.setScalar(corpseVisible?.67:0.000001);
     this.object.updateMatrix();army.meshes[j]!.setMatrixAt(i,this.object.matrix);
    }
   }
   for(const mesh of army.meshes)mesh.instanceMatrix.needsUpdate=true;
  }
 }
 dispose(){
  const geometries=new Set<THREE.BufferGeometry>(),materials=new Set<THREE.Material>();
  for(const army of this.armies)for(const m of army.meshes){this.scene.remove(m);geometries.add(m.geometry);materials.add(m.material as THREE.Material)}
  for(const g of geometries)g.dispose();for(const m of materials)m.dispose();
 }
}
