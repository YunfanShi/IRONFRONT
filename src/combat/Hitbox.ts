export interface Vector3 {x:number;y:number;z:number}
/** Vertical standing capsule, including the head and airborne offset. Returns ray entry. */
export function soldierRayHit(origin:Vector3,d:Vector3,at:Vector3):number|null {
 let best=Infinity;
 for(const h of [.5,1,1.5,1.9]){const dx=at.x-origin.x,dy=at.y+h-origin.y,dz=at.z-origin.z,t=dx*d.x+dy*d.y+dz*d.z,miss=dx*dx+dy*dy+dz*dz-t*t;if(t<0||miss>.52*.52)continue;best=Math.min(best,Math.max(0,t-Math.sqrt(.52*.52-miss)));}
 return Number.isFinite(best)?best:null;
}
