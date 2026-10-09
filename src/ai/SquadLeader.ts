import {dist,type Point} from '../core/math';
export type SquadState='ASSEMBLING'|'TRAVELING'|'ENGAGING'|'DEFENDING'|'CAPTURING'|'REGROUPING'|'WITHDRAWING';
export interface MemberStatus {id:number;pos:Point;hp:number;alive:boolean;player:boolean;vehicleId:number|null;visualTarget:number|null;stuckFor:number}
export interface SquadPlan {state:SquadState;anchor:Point;holdLeader:boolean;reason:string;changedAt:number;cohesion:number}
/** Local autonomy from friendly status and legitimately observed contact flags only. */
export class SquadLeader {
 private plans=new Map<string,SquadPlan>();
 plan(key:string,now:number,members:readonly MemberStatus[],leader:number,objective:Point,defending:boolean):SquadPlan{
  const active=members.filter(s=>s.alive),lead=active.find(s=>s.id===leader)??active[0],old=this.plans.get(key),anchor=lead?.pos??objective;
  const cohesion=active.filter(s=>s.vehicleId!==null||dist(s.pos,anchor)<32).length/Math.max(1,active.length),fighting=active.some(s=>s.visualTarget!==null),health=active.reduce((n,s)=>n+s.hp,0)/Math.max(1,active.length);
  let state:SquadState=active.length<2?'ASSEMBLING':health<35?'WITHDRAWING':fighting?'ENGAGING':dist(anchor,objective)<25?(defending?'DEFENDING':'CAPTURING'):cohesion<.6?'REGROUPING':'TRAVELING';
  // Do not wait forever for a respawned or unreachable straggler.
  if(state==='REGROUPING'&&old?.state==='REGROUPING'&&now-old.changedAt>10)state='TRAVELING';
  const result:SquadPlan={state,anchor:{...anchor},holdLeader:state==='REGROUPING',reason:state==='REGROUPING'?'等待落后队员':state==='WITHDRAWING'?'伤亡过大，脱离并恢复':fighting?'接敌，分批移动与掩护':'执行指挥任务',changedAt:state===old?.state?old.changedAt:now,cohesion};this.plans.set(key,result);return result;
 }
 get(key:string){return this.plans.get(key);}
}
