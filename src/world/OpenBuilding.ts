import type {Block} from './Layout';
export interface OpenBuilding {x:number;z:number;w:number;d:number;h:number}
/** Ground-floor entrance and two rooms share this collision contract with the visual shell. */
export function openBuildingWalls(b:OpenBuilding):Block[]{const t=1.2,door=8;
 return [
 {x:b.x,z:b.z-b.d/2,w:b.w,d:t,h:b.h,kind:'wall'},
 ...[-1,1].map(side=>({x:b.x+side*b.w/2,z:b.z,w:t,d:b.d,h:b.h,kind:'wall' as const})),
 ...[-1,1].map(side=>({x:b.x+side*(b.w+door)/4,z:b.z+b.d/2,w:(b.w-door)/2,d:t,h:b.h,kind:'wall' as const})),
 {x:b.x,z:b.z-b.d*.27,w:t,d:b.d*.42,h:3,kind:'wall'}];
}
