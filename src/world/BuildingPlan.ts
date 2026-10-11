import type {Block} from './Layout';
export type BuildingStyle='residential'|'office'|'factory'|'warehouse'|'military';
export function buildingPlan(b:Block){
 const variant=Math.abs(Math.round(b.x*31+b.z*17))%4;
 const style:BuildingStyle=b.kind==='house'?'residential':b.kind==='barracks'?'military':variant===3?'office':variant===0?'warehouse':'factory';
 const floors=Math.max(1,Math.round(b.h/(['warehouse','factory'].includes(style)?5:3.1))),floorHeight=b.h/floors;
 const setback=(style==='office'||style==='residential'&&variant%2===1)&&floors>1;
 const industrialTower=style==='factory'&&b.h>10;
 const upperWidth=setback?b.w*.78:industrialTower?b.w*.48:b.w,upperDepth=setback?b.d*.84:industrialTower?b.d*.72:b.d;
 const shoulderHeight=setback?b.h-floorHeight:industrialTower?b.h*.68:b.h;
 const volumes=shoulderHeight<b.h?[{x:b.x,z:b.z,w:b.w,d:b.d,h:shoulderHeight},{x:b.x,z:b.z,w:upperWidth,d:upperDepth,h:b.h}]:[{x:b.x,z:b.z,w:b.w,d:b.d,h:b.h}];
 return {style,variant,floors,floorHeight,setback,upperWidth,upperDepth,shoulderHeight,volumes,pitchedRoof:style==='warehouse'||style==='residential'&&variant%2===0,footprint:{x:b.x,z:b.z,w:b.w,d:b.d}};
}
