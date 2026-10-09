import {dist,type Point} from '../core/math';
import type {BotState} from '../core/Battle';

export type AIProfile='regular'|'elite';
export type AIIntentKind='MOVE_WITH_SQUAD'|'HOLD_POSITION'|'OBSERVE'|'INTERACT_WITH_OBJECTIVE'|'ASSIST_TEAMMATE'|'ENGAGE_IN_GAME_COMBAT'|'TAKE_COVER'|'WITHDRAW'|'REGROUP'|'RECOVER_FROM_NAVIGATION_FAILURE';
export interface AIIntent {
 kind:AIIntentKind;state:BotState;destination:Point;goal:string;targetId:number|null;
 speed:number;reason:string;since:number;commitUntil:number;
}
export type AIIntentProposal=Omit<AIIntent,'since'|'commitUntil'>;

const priority:Record<AIIntentKind,number>={MOVE_WITH_SQUAD:1,HOLD_POSITION:1,OBSERVE:1,INTERACT_WITH_OBJECTIVE:2,REGROUP:2,ENGAGE_IN_GAME_COMBAT:3,TAKE_COVER:4,ASSIST_TEAMMATE:4,WITHDRAW:5,RECOVER_FROM_NAVIGATION_FAILURE:5};

/** Keeps a useful decision until it finishes or a real change warrants interruption. */
export function resolveIntent(now:number,at:Point,current:AIIntent|null,proposal:AIIntentProposal,profile:AIProfile,urgent=false):{intent:AIIntent;changed:boolean}{
 if(current){
  const completed=current.speed>0&&dist(at,current.destination)<2.8;
  const interrupted=urgent||current.goal!==proposal.goal||current.targetId!==proposal.targetId||priority[proposal.kind]>priority[current.kind];
  const drift=dist(current.destination,proposal.destination);
  if(!interrupted){
   if(profile==='elite'&&current.kind==='REGROUP'&&proposal.kind==='REGROUP'&&current.state===proposal.state&&drift<32){
    if(drift<3.5)return {intent:current,changed:false};
    return {intent:{...current,destination:{...proposal.destination},speed:proposal.speed,reason:proposal.reason},changed:false};
   }
   if(current.kind===proposal.kind&&current.state===proposal.state&&drift<3.5)return {intent:current,changed:false};
   if(completed&&now-current.since>1.5)return {intent:{...proposal,destination:{...proposal.destination},since:now,commitUntil:now+(profile==='elite'?4.5:3)},changed:true};
   if(now<current.commitUntil&&(current.kind!==proposal.kind||drift<(profile==='elite'?12:8)))return {intent:current,changed:false};
  }
 }
 const duration=proposal.kind==='HOLD_POSITION'||proposal.kind==='OBSERVE'?2:proposal.kind==='ASSIST_TEAMMATE'?2.5:profile==='elite'?4.5:3;
 return {intent:{...proposal,destination:{...proposal.destination},since:now,commitUntil:now+duration},changed:true};
}
