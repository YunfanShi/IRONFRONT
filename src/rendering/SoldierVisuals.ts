import * as THREE from 'three';
import type {Battle,Soldier} from '../core/Battle';

import type {Team} from '../world/Layout';

type PartKind='rig'|'head'|'armL'|'armR'|'legL'|'legR'|'weapon'|'gadget';
type Part={offset:[number,number,number];kind:PartKind;geometry:THREE.BufferGeometry;material:THREE.Material|THREE.Material[];humanOnly?:boolean;gadgetType?:Soldier['equipmentKind']};
type Army={team:Team;meshes:THREE.InstancedMesh[];soldiers:Soldier[];parts:Part[];counts:Uint16Array};
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
  const plate=new THREE.MeshStandardMaterial({color:0x3b4648,roughness:.94,metalness:.04});
  const boots=new THREE.MeshStandardMaterial({color:0x202527,roughness:.99});
  const gloves=new THREE.MeshStandardMaterial({color:0x303635,roughness:.96});
  const badge=new THREE.MeshStandardMaterial({color:0xf0d46c,roughness:.8});
  const skin=new THREE.MeshStandardMaterial({color:0xb18b70,roughness:.94});
  const visor=new THREE.MeshStandardMaterial({color:0x17272a,metalness:.12,roughness:.47});
  const steel=new THREE.MeshStandardMaterial({color:0x20292f,metalness:.74,roughness:.38});
  const geoBox=(w:number,h:number,d:number)=>new THREE.BoxGeometry(w,h,d);
  const rounded=(w:number,h:number,d:number)=>new THREE.SphereGeometry(1,12,8).scale(w/2,h/2,d/2);
  for(const team of ['blue','red'] as Team[]){
   const suit=new THREE.MeshStandardMaterial({color:0xffffff,roughness:.98});
   const cloth=document.createElement('canvas');cloth.width=cloth.height=32;const cc=cloth.getContext('2d')!;cc.fillStyle=team==='blue'?'#3f5059':'#5e524d';cc.fillRect(0,0,32,32);cc.fillStyle=team==='blue'?'#37464e':'#504843';for(let i=0;i<48;i++)cc.fillRect((i*17)%32,(i*11)%32,2,2);const clothMap=new THREE.CanvasTexture(cloth);clothMap.magFilter=THREE.LinearFilter;clothMap.minFilter=THREE.LinearMipmapLinearFilter;clothMap.colorSpace=THREE.SRGBColorSpace;suit.map=clothMap;
   const trim=new THREE.MeshStandardMaterial({color:team==='blue'?0x47535a:0x604f48,roughness:.9});
   const helmet=new THREE.MeshStandardMaterial({color:team==='blue'?0x344753:0x554b47,roughness:.86,metalness:.08});
   const parts:Part[]=[
    {geometry:new THREE.CylinderGeometry(.37,.46,1.2,10),material:suit,offset:[0,1.45,0],kind:'rig'},
    {geometry:rounded(.54,.66,.52),material:skin,offset:[0,2.39,0],kind:'head'},
    {geometry:new THREE.SphereGeometry(.34,12,8,0,Math.PI*2,0,Math.PI*.65),material:helmet,offset:[0,2.53,-.015],kind:'head'},
    {geometry:rounded(.45,.12,.12),material:visor,offset:[0,2.48,.27],kind:'head'},
    {geometry:rounded(.46,.19,.12),material:trim,offset:[0,2.30,.26],kind:'head'},
    {geometry:rounded(.14,.21,.21),material:helmet,offset:[-.31,2.43,0],kind:'head'},
    {geometry:rounded(.14,.21,.21),material:helmet,offset:[.31,2.43,0],kind:'head'},
    {geometry:geoBox(.24,.18,.02),material:badge,offset:[0,1.68,.215],kind:'rig',humanOnly:true},
    {geometry:new THREE.CylinderGeometry(.17,.21,1.2,8),material:suit,offset:[-.58,1.45,0],kind:'armL'},
    {geometry:new THREE.CylinderGeometry(.17,.21,1.2,8),material:suit,offset:[.58,1.45,0],kind:'armR'},
    {geometry:rounded(.39,.28,.36),material:suit,offset:[-.53,1.93,0],kind:'armL'},
    {geometry:rounded(.39,.28,.36),material:suit,offset:[.53,1.93,0],kind:'armR'},
    {geometry:geoBox(.34,.27,.36),material:gloves,offset:[-.6,1.0,0],kind:'armL'},
    {geometry:geoBox(.34,.27,.36),material:gloves,offset:[.6,1.0,0],kind:'armR'},
    {geometry:new THREE.CylinderGeometry(.18,.22,1.2,8),material:trim,offset:[-.2,.65,0],kind:'legL'},
    {geometry:new THREE.CylinderGeometry(.18,.22,1.2,8),material:trim,offset:[.2,.65,0],kind:'legR'},
    {geometry:rounded(.31,.24,.25),material:plate,offset:[-.2,.48,.14],kind:'legL'},
    {geometry:rounded(.31,.24,.25),material:plate,offset:[.2,.48,.14],kind:'legR'},
    {geometry:geoBox(.41,.22,.44),material:boots,offset:[-.2,.15,.015],kind:'legL'},
    {geometry:geoBox(.41,.22,.44),material:boots,offset:[.2,.15,.015],kind:'legR'},
    {geometry:new THREE.CylinderGeometry(.40,.45,.78,10),material:plate,offset:[0,1.52,0],kind:'rig'},
    {geometry:geoBox(.68,.68,.22),material:plate,offset:[0,1.48,-.35],kind:'rig'},
    {geometry:geoBox(.69,.64,.14),material:plate,offset:[0,1.49,.36],kind:'rig'},
    {geometry:geoBox(.86,.13,.43),material:gloves,offset:[0,1.03,0],kind:'rig'},
    {geometry:geoBox(.18,.25,.17),material:trim,offset:[-.26,1.32,.45],kind:'rig'},
    {geometry:geoBox(.18,.25,.17),material:trim,offset:[0,1.32,.45],kind:'rig'},
    {geometry:geoBox(.18,.25,.17),material:trim,offset:[.26,1.32,.45],kind:'rig'},
    {geometry:geoBox(.25,.31,.15),material:trim,offset:[-.51,1.18,0],kind:'rig'},
    {geometry:geoBox(.25,.31,.15),material:trim,offset:[.51,1.18,0],kind:'rig'},
    {geometry:geoBox(.23,.25,.95),material:steel,offset:[.15,1.52,.59],kind:'weapon'},
    {geometry:geoBox(.31,.27,.36),material:trim,offset:[.40,1.12,.10],kind:'armR'},
    {geometry:geoBox(.13,.16,.43),material:steel,offset:[.15,1.55,1.14],kind:'weapon'},
    {geometry:geoBox(.4,.3,.3),material:trim,offset:[.15,1.35,.6],kind:'gadget',gadgetType:'medical'},
    {geometry:geoBox(.13,.28,.45),material:steel,offset:[.15,1.35,.6],kind:'gadget',gadgetType:'repair'},
    {geometry:geoBox(.4,.32,.4),material:plate,offset:[.15,1.35,.6],kind:'gadget',gadgetType:'ammo'},
    {geometry:new THREE.CylinderGeometry(.1,.1,1.5,8).rotateX(Math.PI/2),material:plate,offset:[.15,1.6,.6],kind:'gadget',gadgetType:'rocket'},
    {geometry:new THREE.CylinderGeometry(.1,.1,.3,8),material:trim,offset:[.15,1.35,.6],kind:'gadget',gadgetType:'smoke'},
    {geometry:geoBox(.14,.16,.17),material:trim,offset:[-.40,1.26,.33],kind:'armL'}
   ];
   const soldiers=battle.soldiers.filter(s=>s.team===team);
   const meshes=parts.map(p=>{
    const mesh=new THREE.InstancedMesh(p.geometry,p.material,battle.soldiers.length);
    mesh.instanceMatrix.setUsage(THREE.DynamicDrawUsage);mesh.frustumCulled=false;mesh.castShadow=false;mesh.receiveShadow=true;this.scene.add(mesh);return mesh
   });
   for(const s of soldiers)this.states.set(s.id,{x:s.pos.x,z:s.pos.z,yaw:s.yaw,phase:s.id*.87,speed:0,deathLean:s.id%2?1:-1});
   this.armies.push({team,meshes,soldiers,parts,counts:new Uint16Array(parts.length)});
  }
 }
 update(battle:Battle,dt:number,showSelf=false){
  const blend=1-Math.exp(-dt*13);
  for(const army of this.armies){
   army.soldiers.length=0;for(const soldier of battle.soldiers)if(soldier.team===army.team)army.soldiers.push(soldier);army.counts.fill(0);
   for(let i=0;i<army.soldiers.length;i++){
    const soldier=army.soldiers[i]!,state=this.states.get(soldier.id)!;
    if(Math.hypot(soldier.pos.x-state.x,soldier.pos.z-state.z)>42){state.x=soldier.pos.x;state.z=soldier.pos.z;state.speed=0}
    const priorX=state.x,priorZ=state.z;
    state.x=THREE.MathUtils.lerp(state.x,soldier.pos.x,blend);state.z=THREE.MathUtils.lerp(state.z,soldier.pos.z,blend);
    const angle=Math.atan2(Math.sin(soldier.yaw-state.yaw),Math.cos(soldier.yaw-state.yaw));state.yaw+=angle*blend;
    const moveSpeed=Math.hypot(state.x-priorX,state.z-priorZ)/Math.max(dt,.001);
    state.speed=THREE.MathUtils.damp(state.speed,Math.min(1,moveSpeed/8.2),8,dt);
    const sprinting=soldier.state==='RETREAT'||soldier.state==='MOVE_TO_OBJECTIVE'||soldier.state==='FOLLOW_SQUAD'||soldier.state==='BOARD_VEHICLE';
    const fighting=soldier.state==='ENGAGE'||soldier.state==='SEEK_COVER';
    const interacting=soldier.state==='REVIVE';
    const observing=soldier.state==='SEARCH'||soldier.state==='DEFEND';
    const crouching=soldier.state==='SEEK_COVER'||interacting||(soldier.state==='DEFEND'&&soldier.id%3===0);
    state.phase+=dt*((sprinting?3.1:2.0)+state.speed*(sprinting?13.2:10.4));
    const stride=Math.sin(state.phase)*state.speed*(fighting ? .52 : 1),step=Math.abs(Math.cos(state.phase))*state.speed;
    const mount=soldier.vehicleId===null?null:battle.vehicles.find(v=>v.id===soldier.vehicleId&&v.kind==='motorcycle'&&v.alive),riding=!!mount;if(mount){const seat=mount.occupants.indexOf(soldier.id),offset=seat===0?.18:-.65;state.x=mount.pos.x+Math.sin(mount.yaw)*offset;state.z=mount.pos.z+Math.cos(mount.yaw)*offset;state.yaw=mount.yaw;}const footY=battle.map.heightAt(state.x,state.z)+soldier.altitude+(riding?.43:0),bob=step*.052*(crouching ? .45 : 1);
    const sY=Math.sin(state.yaw),cY=Math.cos(state.yaw);
    const deathAge=battle.elapsed-soldier.deathAt,fall=!soldier.alive?Math.min(1,Math.max(0,deathAge/.72)):0;
    const equipmentActive=!soldier.player&&(soldier.equipmentUntil??0)>battle.elapsed;const weaponId=soldier.id===battle.player.id?battle.playerWeapon:soldier.weaponId;const weaponScale=weaponId==='sniper'?1.45:weaponId==='lmg'?1.18:weaponId==='smg'?.72:1;
    const downed=soldier.downedUntil>battle.elapsed;const corpseVisible=(soldier.id!==battle.player.id||showSelf)&&(soldier.vehicleId===null||riding)&&(soldier.alive||downed||deathAge<3.7);const crouchDrop=crouching ? .38 : 0;
    for(let j=0;j<army.parts.length;j++){
     const part=army.parts[j]!;
     // Compact active instances: hidden bodies/equipment never compose or upload matrices.
     if(!corpseVisible||part.humanOnly&&!soldier.player||part.kind==='weapon'&&(riding||equipmentActive)||part.kind==='gadget'&&(!equipmentActive||part.gadgetType!==soldier.equipmentKind))continue;
     const [lx,ly,lz]=part.offset;let zz=lz,yy=ly-crouchDrop,rx=0,rz=0;
     if(part.kind==='legL'){rx=stride*.38+(crouching ? .30 : 0);zz+=stride*.21;yy+=Math.max(0,-stride)*.09}
     else if(part.kind==='legR'){rx=-stride*.38+(crouching ? .30 : 0);zz-=stride*.21;yy+=Math.max(0,stride)*.09}
     else if(part.kind==='armL'){rx=interacting?-.95:fighting?-.78:-stride*.17-.45;zz+=interacting?.31:fighting?.25:.13;yy-=.035}
     else if(part.kind==='armR'){rx=interacting?-1.05:fighting?-.70:stride*.14-.44;zz+=interacting?.35:fighting?.24:.12;yy-=.035}
     else if(part.kind==='head'){rx=fighting?-.03:step*.014;rz=observing&&state.speed<.15?Math.sin(battle.elapsed*1.2+soldier.id)*.055:Math.sin(state.phase*.47)*.022}
     else if(part.kind==='rig'){yy+=bob;rz=stride*.025}
     else if(part.kind==='weapon'){yy+=bob*.7;rx=fighting?-.03:-.12}
     if(riding){rx=part.kind==='legL'||part.kind==='legR'?1.1:part.kind==='armL'||part.kind==='armR'?-.95:0;rz=0;yy=ly;zz=lz;if(part.kind==='legL'||part.kind==='legR'){yy-=.15;zz+=.25;}if(part.kind==='armL'||part.kind==='armR'){zz+=.23;yy-=.08;}}
     if(fall>0){yy-=fall*(.45+ly*.18);rz+=state.deathLean*fall*1.15;rx+=fall*.12}
     this.object.position.set(state.x+(cY*lx+sY*zz)*.67,footY+yy*.67,state.z+(-sY*lx+cY*zz)*.67);
     this.object.rotation.set(rx,state.yaw,rz,'YXZ');this.object.scale.setScalar(.67);
     if(part.kind==='weapon')this.object.scale.z*=weaponScale;
     this.object.updateMatrix();army.meshes[j]!.setMatrixAt(army.counts[j]!,this.object.matrix);army.counts[j]=army.counts[j]!+1;
    }
   }
   for(let j=0;j<army.meshes.length;j++){const mesh=army.meshes[j]!,count=army.counts[j]!;mesh.count=count;mesh.visible=count>0;if(count){mesh.instanceMatrix.clearUpdateRanges();mesh.instanceMatrix.addUpdateRange(0,count*16);mesh.instanceMatrix.needsUpdate=true;}}
  }
 }
 dispose(){
  const geometries=new Set<THREE.BufferGeometry>(),materials=new Set<THREE.Material>();
  for(const army of this.armies)for(const m of army.meshes){this.scene.remove(m);geometries.add(m.geometry);for(const mat of Array.isArray(m.material)?m.material:[m.material])materials.add(mat)}
  for(const g of geometries)g.dispose();for(const m of materials){(m as THREE.MeshStandardMaterial).map?.dispose();m.dispose();}
 }
}
