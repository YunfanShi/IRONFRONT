import {dist,type Point} from '../core/math';
import type {Team} from '../world/Layout';
import type {Contact} from './Intelligence';
export type Mission='CAPTURE_OBJECTIVE'|'DEFEND_OBJECTIVE'|'REINFORCE_OBJECTIVE'|'RECON_OBJECTIVE'|'REGROUP'|'WITHDRAW_FROM_AREA';
export interface SquadSummary {squad:number;leader:number;at:Point;members:number;health:number;blocked:boolean;human:boolean}
export interface Assignment {squad:number;objective:string;mission:Mission;score:number;reason:string;since:number}
export interface Recommendation {objective:string;mission:Mission;reason:string;expires:number;status:'offered'|'accepted'|'dismissed'}
/** Restricted value-only input. Hostile live state cannot be queried by this class. */
export class Commander {
 readonly assignments=new Map<number,Assignment>();recommendations=new Map<number,Recommendation>();lastDecision=0;decisions=0;
 constructor(readonly team:Team){}
 evaluate(now:number,map:readonly (Point & {id:string})[],reports:readonly Contact[],squads:readonly SquadSummary[],tickets:number,opposingTickets:number){
  this.lastDecision=now;this.decisions++;const assigned=new Map<string,number>();
  for(const s of squads){const previous=this.assignments.get(s.squad);let best:Assignment|undefined;
   for(const p of map){const known=reports.find(r=>r.kind==='OBJECTIVE_STATUS'&&r.subject===p.id&&r.confidenceNow>.2);const pressure=reports.filter(r=>r.kind==='ENEMY_OBSERVED'&&dist(r.at,p)<60).reduce((n,r)=>n+r.confidenceNow,0);
    const own=known?.owner===this.team;let mission:Mission=!known?'RECON_OBJECTIVE':own?(known.contested?'REINFORCE_OBJECTIVE':'DEFEND_OBJECTIVE'):'CAPTURE_OBJECTIVE';
    const need=!known?3.1:own?(known.contested?7:-1.5):5;let score=need-dist(s.at,p)*.009-(assigned.get(p.id)??0)*2.1+pressure*(own?.6:-.12)+(tickets<opposingTickets&&!own?.5:0);
    if(previous?.objective===p.id)score+=1.2;if(s.blocked&&previous?.objective===p.id)score-=4;
    if(s.members<2||s.health<.35){mission='REGROUP';score=own?7-dist(s.at,p)*.01:score-3;}
    if(!best||score>best.score)best={squad:s.squad,objective:p.id,mission,score,reason:!known?'情报不足，侦察据点':s.blocked?'路线受阻，改换目标':s.members<2?'小队伤亡，重新集结':own?(known.contested?'友军据点受压，请增援':'保持据点与侧翼警戒'):'夺取据点以减缓兵力损失',since:now};
   }
   if(!best)continue;
   // Commitment window except urgent observed pressure or failed navigation.
   if(previous&&now-previous.since<12&&!s.blocked&&s.members>=2&&!reports.some(r=>r.kind==='OBJECTIVE_STATUS'&&r.subject===previous.objective&&r.contested&&r.confidenceNow>.5))best=previous;
   if(previous?.objective===best.objective&&previous.mission===best.mission)best.since=previous.since;
   this.assignments.set(s.squad,best);assigned.set(best.objective,(assigned.get(best.objective)??0)+1);
   if(s.human){const prior=this.recommendations.get(s.squad);if(!prior||now>=prior.expires){this.recommendations.set(s.squad,{objective:best.objective,mission:best.mission,reason:best.reason,expires:now+45,status:'offered'});}else if(prior.status==='accepted'){const p=map.find(p=>p.id===prior.objective);if(p)this.assignments.set(s.squad,{...best,objective:p.id,mission:prior.mission,reason:'玩家接受的任务',since:previous?.since??now});}}
  }
 }
 respond(squad:number,accept:boolean){const r=this.recommendations.get(squad);if(!r||r.status!=='offered')return false;r.status=accept?'accepted':'dismissed';return true;}
}
