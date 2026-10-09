import type {Point} from '../core/math';
import type {Team} from '../world/Layout';
export type ReportKind='ENEMY_OBSERVED'|'SOUND_HEARD'|'OBJECTIVE_STATUS'|'NAVIGATION_BLOCKED'|'SQUAD_NEEDS_ASSISTANCE'|'MISSION_COMPLETED';
export interface IntelReport {source:number;squad:number;kind:ReportKind;subject:string;at:Point;time:number;confidence:number;owner?:Team|null;contested?:boolean}
export interface Contact extends IntelReport {confidenceNow:number;status:'current'|'recent'|'last-known'}
/** Only observations cross this boundary. No Battle, enemy list, live entity or lookup callback. */
export class Intelligence {
 private reports=new Map<string,IntelReport>();private sent=new Map<string,number>();
 events=0;
 receive(report:IntelReport):boolean {
  if(!Number.isFinite(report.time)||!Number.isFinite(report.at.x)||!Number.isFinite(report.at.z))return false;
  const key=report.kind+':'+report.subject,gate=report.source+':'+key;
  if(report.time-(this.sent.get(gate)??-Infinity)<1)return false;
  this.sent.set(gate,report.time);this.reports.set(key,{...report,at:{...report.at},confidence:Math.max(0,Math.min(1,report.confidence))});this.events++;
  if(this.reports.size>192)this.reports.delete(this.reports.keys().next().value!);
  if(this.sent.size>768)this.sent.delete(this.sent.keys().next().value!);return true;
 }
 snapshot(now:number):Contact[]{
  const out:Contact[]=[];for(const [key,r] of this.reports){const age=Math.max(0,now-r.time),ttl=r.kind==='OBJECTIVE_STATUS'?60:r.kind==='SOUND_HEARD'?8:24;if(age>ttl){this.reports.delete(key);continue;}
   out.push({...r,at:{...r.at},confidenceNow:r.confidence*Math.max(0,1-age/ttl),status:age<1.5?'current':age<6?'recent':'last-known'});
  }for(const [k,t] of this.sent)if(now-t>60)this.sent.delete(k);return out;
 }
}
