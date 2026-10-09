import {dist,type Point} from '../core/math';
export type SquadState='ASSEMBLING'|'TRAVELING'|'ENGAGING'|'DEFENDING'|'CAPTURING'|'REGROUPING'|'WITHDRAWING';
export interface MemberStatus {id:number;pos:Point;hp:number;alive:boolean;player:boolean;vehicleId:number|null;visualTarget:number|null;stuckFor:number}
export interface SquadPlan {state:SquadState;anchor:Point;destination:Point;memberDestinations:Record<number,Point>;heading:number;advanceParity:0|1;holdLeader:boolean;regroupTimedOut:boolean;reason:string;changedAt:number;cohesion:number}
/** Local autonomy from friendly status and legitimately observed contact flags only. */
export class SquadLeader {
 private plans=new Map<string,SquadPlan>();
 plan(key:string,now:number,members:readonly MemberStatus[],leader:number,objective:Point,defending:boolean):SquadPlan{
  const active=members.filter(s=>s.alive),lead=active.find(s=>s.id===leader)??active[0],old=this.plans.get(key),anchor=lead?.pos??objective;
  const desiredHeading=Math.atan2(objective.x-anchor.x,objective.z-anchor.z),delta=old?Math.atan2(Math.sin(desiredHeading-old.heading),Math.cos(desiredHeading-old.heading)):0;
  // Formation faces the mission, not the leader's rapidly changing combat yaw.
  const heading=old?old.heading+Math.max(-.35,Math.min(.35,delta)):desiredHeading;
  const memberDestinations:Record<number,Point>={};const infantry=active.filter(s=>s.vehicleId===null&&s.id!==lead?.id).sort((a,b)=>a.id-b.id);
  if(lead)memberDestinations[lead.id]={...objective};
  infantry.forEach((s,index)=>{const row=Math.floor(index/2)+1,side=index%2===0?-1:1,lateral=side*(3.5+row*.35),back=row*5.5;memberDestinations[s.id]={x:anchor.x+Math.cos(heading)*lateral-Math.sin(heading)*back,z:anchor.z-Math.sin(heading)*lateral-Math.cos(heading)*back};});
  const cohesion=active.filter(s=>s.vehicleId!==null||s.id===lead?.id||dist(s.pos,memberDestinations[s.id]??anchor)<13).length/Math.max(1,active.length),fighting=active.some(s=>s.visualTarget!==null),health=active.reduce((n,s)=>n+s.hp,0)/Math.max(1,active.length);
  let state:SquadState=active.length<2?'ASSEMBLING':health<35?'WITHDRAWING':fighting?'ENGAGING':dist(anchor,objective)<25?(defending?'DEFENDING':'CAPTURING'):cohesion<.6?'REGROUPING':'TRAVELING';
  // A missing or unreachable member must not repeatedly freeze the leader.
  const regroupTimedOut=cohesion<.6&&(old?.regroupTimedOut===true||state==='REGROUPING'&&old?.state==='REGROUPING'&&now-old.changedAt>8);
  if(state==='REGROUPING'&&regroupTimedOut)state='TRAVELING';
  const result:SquadPlan={state,anchor:{...anchor},destination:{...objective},memberDestinations,heading,advanceParity:Math.floor(now/7)%2 as 0|1,holdLeader:state==='REGROUPING',regroupTimedOut,reason:state==='REGROUPING'?'等待落后队员':state==='WITHDRAWING'?'伤亡过大，脱离并恢复':fighting?'接敌，分批移动与掩护':'执行指挥任务',changedAt:state===old?.state?old.changedAt:now,cohesion};this.plans.set(key,result);return result;
 }
 get(key:string){return this.plans.get(key);}
}
