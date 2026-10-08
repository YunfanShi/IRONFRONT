import {clamp,dist,type Point} from '../core/math';
import {OBJECTIVES,type Team} from '../world/Layout';
export interface CapturePoint extends Point {id:string;name:string;zh:string;control:number;owner:Team|null;contested:boolean;blueCount:number;redCount:number}
export function createCapturePoints():CapturePoint[]{return OBJECTIVES.map(o=>({...o,control:0,owner:null,contested:false,blueCount:0,redCount:0}))}
/** Returns the new owner only when a flag becomes fully captured or neutralized. */
export function updateCapture(p:CapturePoint,positions:ReadonlyArray<{team:Team;pos:Point;alive:boolean}>,dt:number):{before:Team|null;after:Team|null}|null{
 const before=p.owner;
 let blue=0,red=0;
 for(const s of positions)if(s.alive&&dist(s.pos,p)<25){if(s.team==='blue')blue++;else red++}
 p.blueCount=blue;p.redCount=red;p.contested=blue>0&&red>0;
 if(!p.contested&&(blue||red)){
  const direction=blue?1:-1;const units=blue||red;
  const speed=13*(1+Math.min(1.3,(units-1)*.22));
  p.control=clamp(p.control+dt*speed*direction,-100,100);
  if(p.owner==='red'&&p.control>=0)p.owner=null;
  if(p.owner==='blue'&&p.control<=0)p.owner=null;
  if(p.control>=99.999)p.owner='blue';
  if(p.control<=-99.999)p.owner='red';
 }
 return before!==p.owner?{before,after:p.owner}:null;
}
export function bleedRate(points:readonly CapturePoint[],team:Team):number{
 let friendly=0,enemy=0;for(const p of points)if(p.owner===team)friendly++;else if(p.owner)enemy++;
 return enemy>friendly?(enemy-friendly)*.85:0;
}
