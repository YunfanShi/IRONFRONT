import type {VehicleKind} from '../vehicles/Vehicle';
import {heightAt as industrialHeight,type Point} from '../core/math';
import {MAP_SIZE,BASES,OBJECTIVES,BLOCKS,OPEN_BUILDINGS,COVER_POINTS,collides,lineBlocked,type Block,type Team,type ObjectiveDefinition} from './Layout';
export type MapId='industrial-frontier'|'dust-horizon';
export interface MapDefinition {
 id:MapId;name:string;zh:string;description:string;size:number;seed:number;
 bases:Record<Team,Point>;objectives:readonly ObjectiveDefinition[];blocks:readonly Block[];
 openBuildings:readonly {x:number;z:number;w:number;d:number;h:number}[];
 roads:readonly (readonly Point[])[];parking:Record<Team,Point>;theme:'industrial'|'desert';
 heightAt:(x:number,z:number)=>number;supportedModes:readonly ('conquest'|'breakthrough')[];
 allowedVehicles:readonly VehicleKind[];navigationStep:number;preview:string;landmarks:readonly Point[];
}
const point=(x:number,z:number):Point=>({x,z});
const dustObjectives:ObjectiveDefinition[]=[
 {id:'A',name:'Canyon Gate',zh:'峡谷关口',x:-270,z:-205},
 {id:'B',name:'Industrial Ruins',zh:'工业遗址',x:-120,z:-100},
 {id:'C',name:'Old Settlement',zh:'沙漠聚落',x:0,z:30},
 {id:'D',name:'Road Junction',zh:'道路枢纽',x:180,z:120},
 {id:'E',name:'Energy Facility',zh:'能源设施',x:300,z:265}
];
const dustBlocks:Block[]=[];
const add=(x:number,z:number,w:number,d:number,h:number,kind:Block['kind'])=>dustBlocks.push({x,z,w,d,h,kind});
// Each landmark has open capture ground, two approaches and bounded solid cover.
for(const z of [-270,-170])for(const x of [-330,-235])add(x,z,34,62,34,'wall');
for(const [x,z] of [[-175,-145],[-75,-145],[-175,-55],[-65,-50]])add(x!,z!,30,24,12,'factory');
for(const [x,z] of [[-65,-8],[58,-10],[-63,73],[60,73]])add(x!,z!,23,20,8,'house');
for(const [x,z] of [[135,80],[230,80],[130,175],[240,175]])add(x!,z!,18,14,7,'barracks');
for(const [x,z] of [[260,220],[350,225],[250,315],[355,320]])add(x!,z!,30,24,15,'factory');
for(const o of dustObjectives)for(const side of [-1,1]){add(o.x+side*29,o.z+19,7,4,2.8,'crate');add(o.x+side*38,o.z-27,12,6,4,'container');}
const dustRoads=[ [point(-400,-395),...dustObjectives,point(400,395)],
 [point(-400,-395),point(-350,-100),point(-190,20),point(-110,160),point(125,270),point(300,265),point(400,395)],
 [point(-270,-205),point(-40,-245),point(130,-120),point(300,10),point(400,395)] ];
const dustHeight=(x:number,z:number)=>{
 const hill=(a:number,b:number,r:number,h:number)=>h*Math.exp(-((x-a)**2+(z-b)**2)/(r*r));
 return hill(-355,-230,44,35)+hill(-220,-250,42,30)+hill(-400,-170,75,24)+hill(-170,-330,90,20)+hill(100,-340,100,14)+hill(-250,320,150,18)+hill(420,80,90,21)+hill(80,400,95,12);
};
export const MAPS:Record<MapId,MapDefinition>={
 'industrial-frontier':{id:'industrial-frontier',name:'Industrial Frontier',zh:'工业前线',description:'工业区、铁路与村庄之间的联合兵种战场',size:MAP_SIZE,seed:44809,bases:BASES,objectives:OBJECTIVES,blocks:BLOCKS,openBuildings:OPEN_BUILDINGS,parking:{blue:point(-304,-302),red:point(304,302)},heightAt:industrialHeight,theme:'industrial',allowedVehicles:['scout','ifv','tank','transport','aa','helicopter','jet','motorcycle'],navigationStep:16,supportedModes:['conquest','breakthrough'],preview:'/maps/industrial-frontier.png',landmarks:[point(-177,246),point(289,-235),point(155,253)],roads:[[point(-320,-296),point(-185,-210),point(-58,-180),point(37,-62),point(140,65),point(301,296)],[point(-291,160),point(-171,116),point(-26,80),point(140,113),point(302,154)],[OBJECTIVES[0]!,OBJECTIVES[1]!,OBJECTIVES[2]!,OBJECTIVES[4]!]]},
 'dust-horizon':{id:'dust-horizon',name:'Dust Horizon',zh:'沙尘地平线',description:'穿越峡谷与荒漠聚落，争夺道路枢纽和能源设施',size:950,seed:190505,bases:{blue:point(-400,-395),red:point(400,395)},objectives:dustObjectives,blocks:dustBlocks,openBuildings:[],parking:{blue:point(-390,-370),red:point(390,370)},heightAt:dustHeight,theme:'desert',allowedVehicles:['scout','ifv','tank','transport','aa','helicopter','jet','motorcycle'],navigationStep:16,supportedModes:['conquest','breakthrough'],preview:'/maps/dust-horizon.png',roads:dustRoads,landmarks:[point(-330,-250),point(-175,-145),point(350,225)]}
};
export function isMapId(value:unknown):value is MapId{return typeof value==='string'&&Object.hasOwn(MAPS,value);}
/** Per-match immutable geometry, never a mutable global active map. */
export class MapContext {
 readonly definition:MapDefinition;readonly cover:readonly Point[];
 constructor(id:MapId='industrial-frontier'){
  if(!isMapId(id))throw new Error('Unsupported map: '+String(id));this.definition=MAPS[id];
  this.cover=id==='industrial-frontier'?COVER_POINTS:this.blocks.flatMap(b=>[{x:b.x-b.w/2-2.4,z:b.z},{x:b.x+b.w/2+2.4,z:b.z},{x:b.x,z:b.z-b.d/2-2.4},{x:b.x,z:b.z+b.d/2+2.4}]).filter(p=>!this.collides(p.x,p.z,1.05));
 }
 get size(){return this.definition.size;}get limit(){return this.size/2-10;}get bases(){return this.definition.bases;}get blocks(){return this.definition.blocks;}get objectives(){return this.definition.objectives;}
 heightAt=(x:number,z:number)=>this.definition.heightAt(x,z);
 collides=(x:number,z:number,r:number)=>collides(x,z,r,this.blocks,this.size);
 lineBlocked=(a:Point,b:Point,blocks:readonly Block[]=this.blocks,pad=0)=>lineBlocked(a,b,blocks,pad);
 project(p:Point,width:number){return {x:(p.x+this.size/2)/this.size*width,y:(p.z+this.size/2)/this.size*width};}
}
