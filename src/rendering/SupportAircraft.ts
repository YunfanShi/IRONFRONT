import * as THREE from 'three';
import type {Battle} from '../core/Battle';
/** Server-clock aircraft and ballistic bombs. Four bombers release over the selected target corridor. */
export class SupportAircraft {
 private units=new Map<string,{plane:THREE.Group;bomb:THREE.Mesh}>();
 private body=new THREE.CylinderGeometry(.9,.65,13,10);
 private wing=new THREE.BoxGeometry(1,1,1);
 private bombGeometry=new THREE.CapsuleGeometry(.24,.8,3,6);
 private steel=new THREE.MeshStandardMaterial({color:0x687477,roughness:.68,metalness:.4});
 private dark=new THREE.MeshStandardMaterial({color:0x263235,roughness:.78});
 constructor(private scene:THREE.Scene){}
 dispose(){for(const u of this.units.values())this.scene.remove(u.plane,u.bomb);this.units.clear();this.body.dispose();this.wing.dispose();this.bombGeometry.dispose();this.steel.dispose();this.dark.dispose();}
 update(b:Battle){const active=new Set<string>();
  for(const f of b.supportFlights)for(let index=0;index<(f.kind==='artillery'?4:1);index++){
   const key=f.id+':'+index,age=b.elapsed-f.starts-index*3;if(age<0||age>12)continue;active.add(key);
   let unit=this.units.get(key);if(!unit){const plane=new THREE.Group(),hull=new THREE.Mesh(this.body,this.steel);hull.rotation.x=Math.PI/2;plane.add(hull);
    for(const [x,y,z,w,h,d] of [[0,0,0,19,.22,3],[0,1,-5,6,.2,2],[0,1.8,-5,.2,3,2],[0,.65,3,1.2,.8,2]]){const mesh=new THREE.Mesh(this.wing,this.steel);mesh.position.set(x!,y!,z!);mesh.scale.set(w!,h!,d!);plane.add(mesh);}
    const bomb=new THREE.Mesh(this.bombGeometry,this.dark);this.scene.add(plane,bomb);unit={plane,bomb};this.units.set(key,unit);}
   const target=f.kind==='artillery'?f.targets[index]!:f.at,ground=b.map.heightAt(target.x,target.z),height=f.kind==='artillery'?74:95;
   unit.plane.position.set(target.x+(age-5)*80,ground+height,target.z);unit.plane.rotation.y=Math.PI/2;
   const fallTime=Math.sqrt(2*height/9.81),release=5-fallTime,t=age-release;
   unit.bomb.visible=f.kind==='artillery'&&t>=0&&age<5;
   if(unit.bomb.visible){unit.bomb.position.set(target.x+(age-5)*80,ground+height-4.905*t*t,target.z);unit.bomb.rotation.z=-Math.atan2(80,9.81*t);}
  }
  for(const [key,u] of this.units)if(!active.has(key)){this.scene.remove(u.plane,u.bomb);this.units.delete(key);}
 }
}
