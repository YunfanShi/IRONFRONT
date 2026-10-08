import type {Point} from '../core/math';
export type SupportId='recon'|'smoke'|'artillery'|'reinforce'|'scout'|'ifv'|'tank';
export const SUPPORTS:Record<SupportId,{name:string;zh:string;cost:number;cooldown:number;description:string}>={
 recon:{name:'Recon scan',zh:'侦察扫描',cost:80,cooldown:25,description:'Reveal approximate enemy contacts around a sector for 12s'},
 smoke:{name:'Smoke barrage',zh:'烟幕弹幕',cost:100,cooldown:30,description:'Block sight and targeting across a sector for 22s'},
 artillery:{name:'Artillery',zh:'炮击支援',cost:250,cooldown:65,description:'5s warning, four damaging and suppressing strikes'},
 reinforce:{name:'Rapid reinforcements',zh:'快速增援',cost:180,cooldown:50,description:'Shorten allied AI respawn timers and boost morale'},
 scout:{name:'R4 Scout',zh:'R4 侦察车',cost:160,cooldown:40,description:'Spawn a fast light vehicle at the main base'},
 ifv:{name:'V12 Lancer IFV',zh:'V12 步战车',cost:300,cooldown:65,description:'Spawn an autocannon IFV at the main base'},
 tank:{name:'T90 Bastion MBT',zh:'T90 主战坦克',cost:450,cooldown:90,description:'Spawn heavy armor and a high damage cannon at the main base'}
};
export interface SupportEffect {id:number;kind:'recon'|'smoke'|'artillery'|'reinforce';at:Point;starts:number;ends:number;nextPulse:number;pulses:number}
export const RP_REWARDS={kill:60,capture:120,assist:25,defense:30,vehicle:150,objectiveTick:5} as const;
