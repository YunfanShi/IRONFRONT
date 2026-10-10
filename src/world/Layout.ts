import type {Point} from '../core/math';
export type Team='blue'|'red';
export interface Block extends Point {w:number;d:number;h:number;kind:'factory'|'house'|'barracks'|'container'|'crate'|'wall'|'shedpost'|'railcar'|'tower';}
export interface ObjectiveDefinition extends Point {id:string;name:string;zh:string;}
export const MAP_SIZE=720;
export const BASES:{blue:Point;red:Point}={blue:{x:-311,z:-302},red:{x:311,z:302}};
export const OBJECTIVES:ObjectiveDefinition[]=[
 {id:'A',name:'Industrial Complex',zh:'工业设施',x:-201,z:-143},
 {id:'B',name:'Central Village',zh:'中央村庄',x:-20,z:-93},
 {id:'C',name:'Railway Station',zh:'铁路车站',x:17,z:81},
 {id:'D',name:'Military Base',zh:'军事基地',x:199,z:157},
 {id:'E',name:'Communications Facility',zh:'通信设施',x:-189,z:185}
];
export const BLOCKS:Block[]=[];
export const OPEN_BUILDINGS=[{x:-7,z:-16,w:29,d:24,h:7.6},{x:-40,z:146,w:29,d:23,h:7.3},{x:116,z:-160,w:32,d:24,h:7.6}];
function add(x:number,z:number,w:number,d:number,h:number,kind:Block['kind']){BLOCKS.push({x,z,w,d,h,kind})}
// Industrial works — buildings with a navigable courtyard and flanking lanes.
add(-269,-170,37,30,15,'factory');add(-250,-102,48,25,10,'factory');add(-163,-191,22,50,16,'factory');
for(let i=0;i<6;i++)add(-292+i*18,-215,13,7,4,'container');
// Village — multiple streets through modest houses.
for(const [x,z] of [[-83,-142],[13,-150],[65,-121],[-100,-55],[50,-38]] as [number,number][])add(x,z,22,20,9,'house');
// Railway yard — cars and platforms, not continuous impassable barricades.
for(let i=0;i<4;i++){add(95+i*28,53,18,9,6,'railcar');add(-104+i*28,130,18,9,6,'railcar')}
add(82,127,29,22,11,'factory');
// Enemy forward base.
add(247,207,43,19,9,'barracks');add(254,114,38,20,9,'barracks');add(151,209,28,22,10,'barracks');
// Radio installation, cover without sealing off objectives.
add(-246,229,29,22,9,'factory');add(-130,232,34,20,8,'barracks');
// Shelter pillars: walk-through areas under roofs.
for(const [x,z] of [[-29,189],[96,-201]] as [number,number][]) {
 for(const dx of [-13,13])for(const dz of [-10,10])add(x+dx,z+dz,2.8,2.8,7,'shedpost');
}
// Open-sided buildings: the roof is decorative, while three separate walls
// leave a genuine, walkable entrance on the +z side.
for(const g of OPEN_BUILDINGS){
 add(g.x,g.z-g.d/2, g.w,2.3,g.h,'wall');
 add(g.x-g.w/2,g.z,2.3,g.d,g.h,'wall');
 add(g.x+g.w/2,g.z,2.3,g.d,g.h,'wall');
}
// Scattered cover outside spawn zones and capture-point centers.
for(const [x,z] of [[-193,-212],[-160,-106],[-132,-159],[-45,-170],[34,-70],[8,-192],[-68,3],[41,21],[-65,68],[55,166],[113,181],[170,100],[229,95],[204,222],[-225,118],[-143,126],[-232,277],[-101,209],[132,-119],[-4,240],[108,12],[-285,-30],[275,-46],[260,-147]] as [number,number][])add(x,z,10,5,3,'crate');
export function collides(x:number,z:number,r:number,blocks:readonly Block[]=BLOCKS,size=MAP_SIZE):boolean {
 if(Math.abs(x)>size/2-r||Math.abs(z)>size/2-r)return true;
 for(const b of blocks){const cx=Math.max(b.x-b.w/2,Math.min(x,b.x+b.w/2));const cz=Math.max(b.z-b.d/2,Math.min(z,b.z+b.d/2));if((x-cx)**2+(z-cz)**2<r*r)return true;}
 return false;
}

export const COVER_POINTS:Point[]=[];
for(const b of BLOCKS){
 const margin=2.4;
 const candidates:Point[]=[
  {x:b.x-b.w/2-margin,z:b.z},{x:b.x+b.w/2+margin,z:b.z},
  {x:b.x,z:b.z-b.d/2-margin},{x:b.x,z:b.z+b.d/2+margin}
 ];
 for(const c of candidates)if(Math.abs(c.x)<MAP_SIZE/2-3&&Math.abs(c.z)<MAP_SIZE/2-3&&!collides(c.x,c.z,1.05))COVER_POINTS.push(c);
}
export function lineBlocked(a:Point,b:Point,blocks:readonly Block[]=BLOCKS,pad=0):boolean{
 // Segment against expanded 2-D building AABBs; ignore the departure/end collision.
 const dx=b.x-a.x,dz=b.z-a.z;
 for(const o of blocks){let tmin=0,tmax=1;const minx=o.x-o.w/2-pad,maxx=o.x+o.w/2+pad,minz=o.z-o.d/2-pad,maxz=o.z+o.d/2+pad;
  // Unrolled slabs avoid temporary axis arrays for every block in every LOS query.
  if(Math.abs(dx)<1e-8){if(a.x<minx||a.x>maxx)continue;}else{let t1=(minx-a.x)/dx,t2=(maxx-a.x)/dx;if(t1>t2){const swap=t1;t1=t2;t2=swap;}tmin=Math.max(tmin,t1);tmax=Math.min(tmax,t2);if(tmin>tmax)continue;}
  if(Math.abs(dz)<1e-8){if(a.z<minz||a.z>maxz)continue;}else{let t1=(minz-a.z)/dz,t2=(maxz-a.z)/dz;if(t1>t2){const swap=t1;t1=t2;t2=swap;}tmin=Math.max(tmin,t1);tmax=Math.min(tmax,t2);if(tmin>tmax)continue;}
  if(tmin<=tmax&&tmax>0.02&&tmin<0.98)return true;
 }
 return false;
}
