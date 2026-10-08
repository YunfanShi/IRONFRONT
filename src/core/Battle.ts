import {clamp,dist,norm,Random,heightAt,type Point} from './math';
import {BASES,COVER_POINTS,collides,lineBlocked,type Team} from '../world/Layout';
import {Navigation} from '../ai/Navigation';
import {bleedRate,createCapturePoints,updateCapture,type CapturePoint} from '../gameplay/Conquest';
import {WEAPONS,WEAPON_ORDER,DEFAULT_LOADOUT,validateLoadout,type Loadout,type WeaponId} from '../combat/Weapons';
import {createArmoredVehicles,VEHICLE,VEHICLE_TYPES,createVehicle,type ArmoredVehicle} from '../vehicles/Vehicle';

import {SUPPORTS,RP_REWARDS,type SupportId,type SupportEffect} from '../gameplay/Requisition';

export type BotState='SPAWN'|'IDLE'|'FOLLOW_SQUAD'|'MOVE_TO_OBJECTIVE'|'SEARCH'|'ENGAGE'|'SEEK_COVER'|'DEFEND'|'RETREAT'|'RESPAWN';
export type SquadIntent='ATTACK'|'DEFEND'|'FLANK'|'REINFORCE';
export type MajorEventKind='ARTILLERY'|'COUNTER_OFFENSIVE'|'ARMORED_PUSH';
export interface ActiveMajorEvent {id:number;kind:MajorEventKind;team:Team;objective:string;startedAt:number;endsAt:number;warningUntil:number;nextPulse:number;pulsesRemaining:number}

export interface Soldier {
 id:number;team:Team;player:boolean;squad:number;squadLeader:boolean;pos:Point;yaw:number;
 velocity:Point;visualTarget:number|null;reactionUntil:number;burstLeft:number;
 hp:number;alive:boolean;respawnAt:number;deathAt:number;spawnGraceUntil:number;kills:number;deaths:number;
 state:BotState;goal:string;target:number|null;nextShot:number;route:Point[];nextPath:number;
 alertUntil:number;lastSeen:Point|null;lastSeenAt:number;suppression:number;morale:number;
 cover:Point|null;nextAiAt:number;lastAiAt:number;stuckFor:number;lastProgress:Point;
}

interface SquadOrder {team:Team;squad:number;objective:string;leaderId:number;sharedEnemyId:number|null;sharedEnemyUntil:number;intent:SquadIntent;approach:Point}

export type BattleEvent =
 |{type:'shot';team:Team;from:Point;to:Point;hit:boolean;player:boolean;weapon?:WeaponId}
 |{type:'death';victim:number;killer:number|null;team:Team}
 |{type:'capture';id:string;owner:Team|null}
 |{type:'respawn';id:number}
 |{type:'end';winner:Team}
 |{type:'vehicleShot';team:Team;from:Point;to:Point;hit:boolean;player:boolean}
 |{type:'vehicleDisabled';team:Team;id:number;at:Point}
 |{type:'vehicleRespawn';team:Team;id:number}
 |{type:'vehicleEnter'|'vehicleExit';id:number}
 |{type:'majorEvent';action:'warning'|'start'|'end';kind:MajorEventKind;team:Team;objective:string}
 |{type:'rp';amount:number;reason:string}
 |{type:'support';kind:SupportId;at:Point}
 |{type:'grenade';at:Point;radius:number}
 |{type:'artilleryImpact';team:Team;at:Point;radius:number};

export interface BattleSettings {
 size:8|16|32|64;
 difficulty:'easy'|'normal'|'hard';
 tickets:number;
 killTicketPenalty?:number;
 seed?:number;
}

const OPP=(t:Team):Team=>t==='blue'?'red':'blue';
const SQUAD_KEY=(team:Team,squad:number)=>`${team}-${squad}`;

export class Battle {
 readonly soldiers:Soldier[]=[];
 readonly points:CapturePoint[]=createCapturePoints();
 readonly tickets:{blue:number;red:number};
 readonly settings:BattleSettings;
 readonly vehicles:ArmoredVehicle[]=createArmoredVehicles();
 readonly nav=new Navigation();
 readonly random:Random;
 elapsed=0;winner:Team|null=null;finished=false;
 playerWeapon:WeaponId='carbine';playerReloadUntil=0;playerNextShot=0;playerAiming=false;
 loadout:Loadout={...DEFAULT_LOADOUT};
 requisitionPoints=0;readonly supports:SupportEffect[]=[];
 readonly supportCooldowns:Partial<Record<SupportId,number>>={};
 grenadeCount=2;gadgetCharges=2;playerThrowUntil=0;playerGadgetUntil=0;
 private supportSequence=1;private nextObjectiveReward=0;
 private contributions=new Map<number,{damage:number;at:number}>();
 private grenades:{at:Point;detonate:number}[]=[];
 private ammo=Object.fromEntries(WEAPON_ORDER.map(id=>[id,WEAPONS[id].magazine])) as Record<WeaponId,number>;
 private reserve=Object.fromEntries(WEAPON_ORDER.map(id=>[id,WEAPONS[id].reserve])) as Record<WeaponId,number>;
 private reloadingWeapon:WeaponId|null=null;
 private commanderAcc=100;private squadOrders=new Map<string,SquadOrder>();private eventQueue:BattleEvent[]=[];
 private navigationFailures=0;
 private playerVehicleId:number|null=null;
 private nextMajorEventAt=64;private majorEventSequence=0;private majorEventOffset=0;private majorEventId=1;private activeMajorEvent:ActiveMajorEvent|null=null;

 constructor(settings:BattleSettings){
  this.settings={...settings,killTicketPenalty:settings.killTicketPenalty??1};
  this.random=new Random(settings.seed??12731);this.tickets={blue:settings.tickets,red:settings.tickets};
  let id=0;
  for(const team of ['blue','red'] as const)for(let i=0;i<settings.size;i++){
   const player=team==='blue'&&i===0;
   const soldier:Soldier={id:id++,team,player,squad:Math.floor(i/4),squadLeader:false,pos:{...BASES[team]},yaw:team==='blue'?0:Math.PI,
    velocity:{x:0,z:0},visualTarget:null,reactionUntil:0,burstLeft:0,hp:100,alive:true,respawnAt:0,deathAt:-999,spawnGraceUntil:1.2,kills:0,deaths:0,state:'SPAWN',goal:'C',target:null,nextShot:0,
    route:[],nextPath:0,alertUntil:0,lastSeen:null,lastSeenAt:-999,suppression:0,morale:1,cover:null,nextAiAt:0,lastAiAt:0,
    stuckFor:0,lastProgress:{...BASES[team]}};
   this.soldiers.push(soldier);this.placeAtSpawn(soldier,true);
  }
  this.majorEventOffset=this.random.int(3);this.nextMajorEventAt=this.random.between(62,78);
  this.assignSquads();
 }

 get player():Soldier{return this.soldiers[0]!}
 get ongoing():boolean{return !this.finished}
 get playerAmmo():number{return this.ammo[this.playerWeapon]}
 get playerReserveAmmo():number{return this.reserve[this.playerWeapon]}
 get activeWeapon(){return WEAPONS[this.playerWeapon]}
 get playerVehicle():ArmoredVehicle|null{return this.playerVehicleId===null?null:(this.vehicles[this.playerVehicleId]??null)}
 get inVehicle():boolean{return !!this.playerVehicle?.alive}
 events():BattleEvent[]{const out=this.eventQueue;this.eventQueue=[];return out}
 private emit(e:BattleEvent){if(this.eventQueue.length>=320)this.eventQueue.shift();this.eventQueue.push(e);if(e.type==='shot'||e.type==='vehicleShot')this.reactToGunfire(e.team,e.from,e.to,e.type==='vehicleShot'?105:72)}
 private reactToGunfire(team:Team,from:Point,to:Point,radius:number){for(const s of this.soldiers){if(!s.alive||s.team===team)continue;const near=Math.min(dist(s.pos,from),dist(s.pos,to));if(near>radius)continue;if(this.random.next()>.58)continue;s.alertUntil=Math.max(s.alertUntil,this.elapsed+2.2);s.lastSeen={x:from.x+this.random.between(-8,8),z:from.z+this.random.between(-8,8)};s.lastSeenAt=this.elapsed-.25;s.suppression=clamp(s.suppression+(near<30?.11:.045),0,1)}}

 private placeAtSpawn(s:Soldier,home=false,pointId?:string){
  let center:Point=BASES[s.team];
  const point=this.points.find(x=>x.id===pointId&&x.owner===s.team);
  if(point)center=point;
  else if(!home){const own=this.points.filter(x=>x.owner===s.team);if(own.length&&this.random.next()<.68)center=own[this.random.int(own.length)]!}
  let final={...center};
  if(!(s.player&&home))for(let n=0;n<50;n++){
   const theta=this.random.between(0,Math.PI*2),radius=this.random.between(8,28);
   const candidate={x:center.x+Math.cos(theta)*radius,z:center.z+Math.sin(theta)*radius};
   if(!collides(candidate.x,candidate.z,1.3)){final=candidate;break}
  }
  s.pos=final;s.lastProgress={...final};s.yaw=s.team==='blue'?0:Math.PI;s.hp=100;s.alive=true;s.target=null;s.route=[];s.nextPath=0;
  s.state='SPAWN';s.spawnGraceUntil=this.elapsed+1.15;s.lastSeen=null;s.lastSeenAt=-999;s.suppression=0;s.morale=1;s.cover=null;
  s.velocity={x:0,z:0};s.visualTarget=null;s.burstLeft=0;s.reactionUntil=this.elapsed+.6;
  s.lastAiAt=this.elapsed;s.nextAiAt=this.elapsed+this.random.between(.02,.16);s.stuckFor=0;
 }

 respawnPlayer(pointId?:string):boolean{
  const s=this.player;if(s.alive||this.finished||this.elapsed<s.respawnAt)return false;
  if(pointId&&pointId!=='BASE'&&!this.points.some(p=>p.id===pointId&&p.owner==='blue'))return false;
  this.placeAtSpawn(s,pointId==='BASE',pointId);this.resetPlayerLoadout();this.emit({type:'respawn',id:s.id});return true;
 }
 setLoadout(value:unknown):boolean {
  if(this.elapsed>0&&this.player.alive)return false;
  this.loadout=validateLoadout(value);this.resetPlayerLoadout();return true;
 }
 private resetPlayerLoadout(){
  this.playerWeapon=this.loadout.primary;
  for(const id of WEAPON_ORDER){this.ammo[id]=WEAPONS[id].magazine;this.reserve[id]=WEAPONS[id].reserve}
  this.playerReloadUntil=0;this.reloadingWeapon=null;this.playerNextShot=0;this.grenadeCount=2;this.gadgetCharges=2;this.playerThrowUntil=0;this.playerGadgetUntil=0;
 }
 switchPlayerWeapon(id:WeaponId):boolean{
  if(this.finished||!this.player.alive||!([this.loadout.primary,this.loadout.secondary] as WeaponId[]).includes(id)||id===this.playerWeapon)return false;
  this.playerWeapon=id;this.playerReloadUntil=0;this.reloadingWeapon=null;this.playerNextShot=Math.max(this.playerNextShot,this.elapsed+.22);return true;
 }
 movePlayer(x:number,z:number){
  const s=this.player;if(!s.alive||this.finished)return;
  if(!collides(s.pos.x+x,s.pos.z,1.05))s.pos.x+=x;
  if(!collides(s.pos.x,s.pos.z+z,1.05))s.pos.z+=z;
 }

 togglePlayerVehicle():boolean{
  const p=this.player;if(!p.alive||this.finished)return false;
  const current=this.playerVehicle;
  if(current){
   const offsets=[{x:5,z:0},{x:-5,z:0},{x:0,z:5},{x:0,z:-5}];
   const out=offsets.map(o=>({x:current.pos.x+o.x,z:current.pos.z+o.z})).find(q=>!collides(q.x,q.z,1.05));
   if(!out)return false;
   p.pos={...out};current.driver='ai';this.playerVehicleId=null;this.emit({type:'vehicleExit',id:current.id});return true;
  }
  let best:ArmoredVehicle|null=null,bestD=7.2;
  for(const v of this.vehicles){if(!v.alive||v.team!=='blue'||v.driver==='player')continue;const d=dist(p.pos,v.pos);if(d<bestD){bestD=d;best=v}}
  if(!best)return false;
  best.driver='player';best.speed=0;this.playerVehicleId=best.id;p.pos={...best.pos};p.route=[];this.emit({type:'vehicleEnter',id:best.id});return true;
 }

 drivePlayerVehicle(throttle:number,steer:number,dt:number){
  const v=this.playerVehicle;if(!v||!v.alive||this.finished)return;
  const target=throttle>=0?throttle*VEHICLE_TYPES[v.kind].maxForward:throttle*VEHICLE_TYPES[v.kind].maxReverse;
  const rate=Math.abs(target)<Math.abs(v.speed)?VEHICLE_TYPES[v.kind].braking:VEHICLE_TYPES[v.kind].acceleration;
  v.speed+=clamp(target-v.speed,-rate*dt,rate*dt);
  if(Math.abs(throttle)<.01)v.speed*=Math.max(0,1-dt*3.1);
  const steerScale=.35+.65*Math.min(1,Math.abs(v.speed)/5);v.yaw+=steer*VEHICLE_TYPES[v.kind].turnRate*steerScale*dt*(v.speed<-.1?-1:1);
  const dx=Math.sin(v.yaw)*v.speed*dt,dz=Math.cos(v.yaw)*v.speed*dt;
  let moved=false;if(!collides(v.pos.x+dx,v.pos.z,VEHICLE_TYPES[v.kind].radius)){v.pos.x+=dx;moved=true}
  if(!collides(v.pos.x,v.pos.z+dz,VEHICLE_TYPES[v.kind].radius)){v.pos.z+=dz;moved=true}
  if(!moved)v.speed*=.18;
  this.player.pos={...v.pos};
 }

 aimPlayerVehicle(yaw:number){const v=this.playerVehicle;if(v)v.turretYaw=yaw+Math.PI}

 shootPlayerVehicle(direction:{x:number;y:number;z:number}):boolean{
  const v=this.playerVehicle;if(!v||!v.alive||this.finished||this.elapsed<v.nextShot)return false;
  v.nextShot=this.elapsed+VEHICLE_TYPES[v.kind].weaponInterval;const origin={...v.pos};v.turretYaw=Math.atan2(direction.x,direction.z);
  let soldier:Soldier|null=null,best:number=VEHICLE_TYPES[v.kind].weaponRange;
  for(const s of this.soldiers){if(!s.alive||s.team===v.team)continue;const along=(s.pos.x-origin.x)*direction.x+(s.pos.z-origin.z)*direction.z;if(along<0||along>best)continue;const lateral=Math.abs((s.pos.x-origin.x)*direction.z-(s.pos.z-origin.z)*direction.x);if(lateral<2.0&&!this.sightBlocked(origin,s.pos)){best=along;soldier=s}}
  let vehicle:ArmoredVehicle|null=null;
  for(const e of this.vehicles){if(!e.alive||e.team===v.team)continue;const along=(e.pos.x-origin.x)*direction.x+(e.pos.z-origin.z)*direction.z;if(along<0||along>best)continue;const lateral=Math.abs((e.pos.x-origin.x)*direction.z-(e.pos.z-origin.z)*direction.x);if(lateral<3.5&&!this.sightBlocked(origin,e.pos,undefined,1.4)){best=along;vehicle=e;soldier=null}}
  const target=vehicle?.pos??soldier?.pos??{x:origin.x+direction.x*VEHICLE_TYPES[v.kind].weaponRange,z:origin.z+direction.z*VEHICLE_TYPES[v.kind].weaponRange};
  this.emit({type:'vehicleShot',team:v.team,from:{...origin},to:{...target},hit:!!vehicle||!!soldier,player:true});
  if(vehicle)this.damageVehicle(vehicle,VEHICLE_TYPES[v.kind].weaponDamage,v.team,true);else if(soldier)this.damage(soldier,VEHICLE_TYPES[v.kind].weaponDamage,this.player);
  return true;
 }
 startReload(){
  const def=this.activeWeapon,weapon=this.playerWeapon;
  if(!this.player.alive||this.ammo[weapon]>=def.magazine||this.reserve[weapon]<=0||this.playerReloadUntil>this.elapsed)return;
  this.reloadingWeapon=weapon;this.playerReloadUntil=this.elapsed+def.reload;
 }
 isReloading(){return this.playerReloadUntil>this.elapsed&&this.reloadingWeapon===this.playerWeapon}

 shootPlayer(direction:{x:number;y:number;z:number},eyeHeight=1.78):boolean{
  const p=this.player,def=this.activeWeapon,weapon=this.playerWeapon;
  if(!p.alive||this.inVehicle||this.finished||this.elapsed<this.playerThrowUntil||this.isReloading()||this.elapsed<this.playerNextShot)return false;
  if(this.ammo[weapon]<=0){this.startReload();return false}
  this.playerNextShot=this.elapsed+def.fireInterval;this.ammo[weapon]--;
  const origin={x:p.pos.x,y:heightAt(p.pos.x,p.pos.z)+eyeHeight,z:p.pos.z};
  const spread=this.playerAiming?def.adsSpread:def.hipSpread;
  const jitter={x:this.random.between(-spread,spread),y:this.random.between(-spread,spread),z:this.random.between(-spread,spread)};
  const length=Math.hypot(direction.x+jitter.x,direction.y+jitter.y,direction.z+jitter.z)||1;
  const d={x:(direction.x+jitter.x)/length,y:(direction.y+jitter.y)/length,z:(direction.z+jitter.z)/length};
  let found:Soldier|null=null,best=def.maxRange;
  for(const s of this.soldiers){if(!s.alive||s.team===p.team)continue;
   const center={x:s.pos.x,y:heightAt(s.pos.x,s.pos.z)+1.15,z:s.pos.z};
   const proj=(center.x-origin.x)*d.x+(center.y-origin.y)*d.y+(center.z-origin.z)*d.z;
   if(proj<0||proj>best)continue;
   const distanceSq=(origin.x+d.x*proj-center.x)**2+(origin.y+d.y*proj-center.y)**2+(origin.z+d.z*proj-center.z)**2;
   if(distanceSq<.82*.82&&!this.sightBlocked(p.pos,s.pos)){best=proj;found=s}
  }
  const endpoint=found?{...found.pos}:{x:origin.x+d.x*def.maxRange,z:origin.z+d.z*def.maxRange};
  this.emit({type:'shot',team:'blue',from:{...p.pos},to:endpoint,hit:!!found,player:true,weapon});
  if(found){const falloff=clamp(1-(best-def.effectiveRange)/(def.maxRange-def.effectiveRange+1),.64,1);this.damage(found,def.damage*falloff,p)}
  if(this.ammo[weapon]===0)this.startReload();return true;
 }

 private respawnDelay(team:Team){return this.eventForTeam(team,'COUNTER_OFFENSIVE')?4.4:7}
 private eliminate(victim:Soldier,killer:Soldier|null){
  const contribution=this.contributions.get(victim.id);this.contributions.delete(victim.id);
  if(killer?.player){this.awardRP(RP_REWARDS.kill,'kill');if(this.points.some(p=>p.owner===killer.team&&dist(p,victim.pos)<25))this.awardRP(RP_REWARDS.defense,'defense')}
  else if(contribution&&contribution.damage>=20&&this.elapsed-contribution.at<10)this.awardRP(RP_REWARDS.assist,'assist');
  victim.alive=false;victim.state='RESPAWN';victim.deaths++;victim.deathAt=this.elapsed;victim.respawnAt=this.elapsed+(victim.player?4.5:this.respawnDelay(victim.team));if(killer)killer.kills++;
  this.tickets[victim.team]=Math.max(0,this.tickets[victim.team]-(this.settings.killTicketPenalty??1));this.emit({type:'death',victim:victim.id,killer:killer?.id??null,team:victim.team});this.checkFinish();
 }
 private damage(victim:Soldier,amount:number,attacker:Soldier){
  if(!victim.alive)return;if(attacker.player&&victim.team!==attacker.team){const old=this.contributions.get(victim.id);this.contributions.set(victim.id,{damage:(old?.damage??0)+amount,at:this.elapsed})}victim.hp=Math.max(0,victim.hp-amount);victim.alertUntil=this.elapsed+4;victim.target=attacker.id;victim.lastSeen={...attacker.pos};victim.lastSeenAt=this.elapsed;
  victim.suppression=clamp(victim.suppression+.28,0,1);victim.morale=clamp(victim.morale-.12,0,1);if(victim.hp<=0)this.eliminate(victim,attacker)
 }
 private damageFromEvent(victim:Soldier,amount:number,attackerTeam:Team,origin:Point){
  if(!victim.alive||victim.team===attackerTeam)return;victim.hp=Math.max(0,victim.hp-amount);victim.alertUntil=this.elapsed+5;victim.target=null;victim.lastSeen={...origin};victim.lastSeenAt=this.elapsed;victim.suppression=clamp(victim.suppression+.55,0,1);victim.morale=clamp(victim.morale-.22,0,1);if(victim.hp<=0)this.eliminate(victim,null)
 }

 private localStrength(team:Team,at:Point,radius=34):number{
  let value=0;for(const s of this.soldiers)if(s.alive&&s.team===team&&dist(s.pos,at)<radius)value+=1;
  for(const v of this.vehicles)if(v.alive&&v.team===team&&dist(v.pos,at)<radius*1.3)value+=3;return value;
 }
 private objectiveApproach(team:Team,squad:number,p:CapturePoint,intent:SquadIntent):Point{
  if(intent==='DEFEND'||intent==='REINFORCE'){
   let best:Point|null=null,bestD=Infinity;for(const c of COVER_POINTS){const d=dist(c,p);if(d<8||d>29||collides(c.x,c.z,1.2))continue;if(d<bestD){bestD=d;best=c}}
   if(best)return {...best};
  }
  const base=BASES[team],toward=norm({x:p.x-base.x,z:p.z-base.z}),side={x:toward.z,z:-toward.x};
  const sign=squad%2===0?1:-1;const lateral=intent==='FLANK'?(32+(squad%3)*8)*sign:(squad%3-1)*8;
  const back=intent==='FLANK'?14:6;
  const q={x:p.x-side.x*lateral-toward.x*back,z:p.z-side.z*lateral-toward.z*back};
  return collides(q.x,q.z,2)?{x:p.x-side.x*lateral*.55,z:p.z-side.z*lateral*.55}:q;
 }
 private eventForTeam(team:Team,kind?:MajorEventKind){const e=this.activeMajorEvent;return e&&e.team===team&&(!kind||e.kind===kind)?e:null}
 private assignSquads(){
  for(const s of this.soldiers)s.squadLeader=false;
  for(const team of ['blue','red'] as const){
   const assigned:Record<string,number>={},squads=Math.ceil(this.settings.size/4),major=this.activeMajorEvent?.team===team?this.activeMajorEvent:null;
   for(let squad=0;squad<squads;squad++){
    const all=this.soldiers.filter(s=>s.team===team&&s.squad===squad&&!s.player);
    const alive=all.filter(s=>s.alive);const leader=(alive[0]??all[0]);if(!leader)continue;leader.squadLeader=true;
    const previous=this.squadOrders.get(SQUAD_KEY(team,squad));const from=leader.pos;let best=this.points[0]!,highest=-Infinity;
    const forceEvent=!!major&&(major.kind==='COUNTER_OFFENSIVE'?squad<Math.ceil(squads*.7):major.kind==='ARMORED_PUSH'?squad<Math.min(3,squads):false);
    if(forceEvent)best=this.points.find(p=>p.id===major!.objective)??best;else for(const p of this.points){
     const friendlyNear=this.localStrength(team,p,54),enemyNear=this.localStrength(OPP(team),p,54);
     let score=p.owner===team?-.55:p.owner===null?4.25:3.75;
     if(p.contested&&p.owner===team)score=6.3;if(p.contested&&p.owner!==team)score=5.9;
     score+=(enemyNear-friendlyNear)*.13;score-=dist(from,p)*.0085;score-=(assigned[p.id]??0)*1.85;
     if(this.tickets[team]<this.tickets[OPP(team)]&&p.owner!==team)score+=.65;
     if(enemyNear>friendlyNear*1.7&&p.owner!==team)score-=.45;
     score+=this.random.between(0,.34);if(score>highest){highest=score;best=p}
    }
    let intent:SquadIntent;
    if(best.owner===team)intent=best.contested?'REINFORCE':'DEFEND';
    else if(forceEvent&&major?.kind==='COUNTER_OFFENSIVE')intent=squad%3===1?'FLANK':'ATTACK';
    else if(forceEvent&&major?.kind==='ARMORED_PUSH')intent=squad===1?'FLANK':'REINFORCE';
    else intent=squad%4===1?'FLANK':'ATTACK';
    const order:SquadOrder={team,squad,objective:best.id,leaderId:leader.id,sharedEnemyId:previous?.sharedEnemyId??null,sharedEnemyUntil:previous?.sharedEnemyUntil??0,intent,approach:this.objectiveApproach(team,squad,best,intent)};
    this.squadOrders.set(SQUAD_KEY(team,squad),order);assigned[best.id]=(assigned[best.id]??0)+1;
   }
  }
  for(const s of this.soldiers){if(s.player)continue;const goal=this.squadOrders.get(SQUAD_KEY(s.team,s.squad))?.objective??'C';if(s.goal!==goal){s.goal=goal;s.route=[];s.nextPath=0}}
 }

 /** Smoke is a gameplay volume, independent of graphics quality. */
 sightBlocked(from:Point,to:Point,_blocks?:undefined,eye=1.5):boolean {
  if(lineBlocked(from,to,undefined,eye))return true;
  const dx=to.x-from.x,dz=to.z-from.z,len=dx*dx+dz*dz;
  return this.supports.some(e=>e.kind==='smoke'&&e.starts<=this.elapsed&&e.ends>this.elapsed&&(()=>{const t=clamp(((e.at.x-from.x)*dx+(e.at.z-from.z)*dz)/(len||1),0,1);return Math.hypot(from.x+dx*t-e.at.x,from.z+dz*t-e.at.z)<23})());
 }
 hasReconContact(at:Point):boolean{return this.supports.some(e=>e.kind==='recon'&&e.ends>this.elapsed&&dist(e.at,at)<105)}
 canIdentify(at:Point,range=55):boolean{return this.player.alive&&dist(this.player.pos,at)<range&&!this.sightBlocked(this.player.pos,at)}
 awardRP(amount:number,reason:string){if(!Number.isFinite(amount)||amount<=0||this.finished)return;this.requisitionPoints=Math.min(9999,this.requisitionPoints+Math.floor(amount));this.emit({type:'rp',amount:Math.floor(amount),reason})}
 requestSupport(id:SupportId,objective:string):{ok:boolean;reason:string} {
  const def=SUPPORTS[id],point=this.points.find(p=>p.id===objective);
  if(!Object.hasOwn(SUPPORTS,id)||!def||!point)return {ok:false,reason:'Invalid support or target / 支援或目标无效'};
  if(this.finished||!this.player.alive)return {ok:false,reason:'Deploy first / 请先部署'};
  if(this.elapsed<(this.supportCooldowns[id]??0))return {ok:false,reason:'Cooling down / 冷却中'};
  if(this.requisitionPoints<def.cost)return {ok:false,reason:'Insufficient RP / 积分不足'};
  const isVehicle=id==='scout'||id==='ifv'||id==='tank';
  let vehicle:ArmoredVehicle|null=null;
  if(isVehicle){
   if(this.vehicles.filter(v=>v.requisitioned&&v.alive).length>=3)return {ok:false,reason:'Vehicle limit / 征用载具上限 3'};
   const cfg=VEHICLE_TYPES[id],base=BASES.blue;
   let spawn:Point|null=null;
   for(let i=0;i<20;i++){const q={x:base.x+12+(i%5)*10,z:base.z+12+Math.floor(i/5)*11};if(!collides(q.x,q.z,cfg.radius)&&!this.vehicles.some(v=>v.alive&&dist(v.pos,q)<cfg.radius+VEHICLE_TYPES[v.kind].radius+1)){spawn=q;break}}
   if(!spawn)return {ok:false,reason:'Parking obstructed / 停车区受阻'};
   const reusable=this.vehicles.find(v=>v.requisitioned&&!v.alive),index=reusable?.id??this.vehicles.length;
   vehicle=createVehicle(index,'blue',id,spawn,true);vehicle.nextDecision=this.elapsed+45;
   if(reusable)this.vehicles[index]=vehicle;else this.vehicles.push(vehicle);
  }
  this.requisitionPoints-=def.cost;this.supportCooldowns[id]=this.elapsed+def.cooldown;
  if(!isVehicle){const kind=id as SupportEffect['kind'];this.supports.push({id:this.supportSequence++,kind,at:{x:point.x,z:point.z},starts:this.elapsed,ends:this.elapsed+(kind==='recon'?12:kind==='smoke'?22:kind==='artillery'?19:18),nextPulse:this.elapsed+5,pulses:kind==='artillery'?4:0});}
  this.emit({type:'support',kind:id,at:vehicle?{...vehicle.pos}:{x:point.x,z:point.z}});
  return {ok:true,reason:isVehicle?'Vehicle ready at base / 载具已停放主基地':'Support active / 支援已呼叫'};
 }
 throwGrenade(direction:Point):boolean {
  if(!this.player.alive||this.inVehicle||this.finished||this.isReloading()||!this.grenadeCount||this.elapsed<this.playerThrowUntil)return false;
  const d=norm(direction);if(!d.x&&!d.z)return false;
  this.grenadeCount--;this.playerThrowUntil=this.elapsed+.8;const from=this.player.pos;let at={...from};
  for(let range=2;range<=28;range+=2){const q={x:from.x+d.x*range,z:from.z+d.z*range};if(collides(q.x,q.z,.2)||lineBlocked(from,q))break;at=q;}
  if(this.loadout.throwable==='smoke')this.supports.push({id:this.supportSequence++,kind:'smoke',at,starts:this.elapsed,ends:this.elapsed+16,nextPulse:0,pulses:0});
  else this.grenades.push({at,detonate:this.elapsed+1.5});return true;
 }
 useGadget():boolean {
  if(!this.player.alive||this.finished||!this.gadgetCharges||this.elapsed<this.playerGadgetUntil)return false;
  if(this.loadout.gadget==='medkit'){if(this.player.hp>=100)return false;this.player.hp=Math.min(100,this.player.hp+45);this.player.suppression=Math.max(0,this.player.suppression-.4)}
  else {const v=this.vehicles.find(v=>v.team==='blue'&&v.alive&&v.hp<v.maxHp&&dist(v.pos,this.player.pos)<8);if(!v)return false;v.hp=Math.min(v.maxHp,v.hp+130)}
  this.gadgetCharges--;this.playerGadgetUntil=this.elapsed+10;return true;
 }
 private updateSupports(){
  for(const e of this.supports){
   if(e.ends<=this.elapsed)continue;
   if(e.kind==='reinforce')for(const s of this.soldiers)if(s.team==='blue'&&!s.player){s.morale=Math.max(s.morale,.8);if(!s.alive)s.respawnAt=Math.min(s.respawnAt,this.elapsed+1)}
   if(e.kind==='artillery'&&e.pulses>0&&this.elapsed>=e.nextPulse){const a=this.random.between(0,6.28),r=this.random.between(0,17),at={x:e.at.x+Math.cos(a)*r,z:e.at.z+Math.sin(a)*r};this.emit({type:'artilleryImpact',team:'blue',at,radius:22});for(const s of this.soldiers)if(s.alive&&s.team==='red'&&dist(s.pos,at)<22)this.damage(s,100*(1-dist(s.pos,at)/22)+12,this.player);for(const v of this.vehicles)if(v.alive&&v.team==='red'&&dist(v.pos,at)<27)this.damageVehicle(v,120*(1-dist(v.pos,at)/27)+15,'blue',true);e.pulses--;e.nextPulse=this.elapsed+3;}
  }
  for(let i=this.supports.length-1;i>=0;i--)if(this.supports[i]!.ends<=this.elapsed)this.supports.splice(i,1);
  for(let i=this.grenades.length-1;i>=0;i--){const g=this.grenades[i]!;if(this.elapsed<g.detonate)continue;this.emit({type:'grenade',at:g.at,radius:12});for(const s of this.soldiers)if(s.alive&&s.team==='red'&&dist(s.pos,g.at)<12&&!lineBlocked(g.at,s.pos))this.damage(s,130*(1-dist(s.pos,g.at)/12),this.player);for(const v of this.vehicles)if(v.alive&&v.team==='red'&&dist(v.pos,g.at)<15)this.damageVehicle(v,75*(1-dist(v.pos,g.at)/15),'blue',true);this.grenades.splice(i,1);}
 }
 private findVisibleEnemy(s:Soldier):Soldier|null{
  let candidate:Soldier|null=null,best={easy:96,normal:112,hard:128}[this.settings.difficulty];
  for(const e of this.soldiers){if(!e.alive||e.team===s.team||(e.player&&this.inVehicle))continue;const d=dist(s.pos,e.pos);if(d>best)continue;
   const angle=Math.atan2(e.pos.x-s.pos.x,e.pos.z-s.pos.z),facing=Math.cos(angle-s.yaw),remembered=s.target===e.id||this.elapsed<s.alertUntil;
   if(!remembered&&(d>38&&facing<.12))continue;if(this.sightBlocked(s.pos,e.pos))continue;best=d;candidate=e;
  }
  return candidate;
 }
 private findAudibleEnemy(s:Soldier):Soldier|null{const range={easy:18,normal:23,hard:28}[this.settings.difficulty];let heard:Soldier|null=null,best=range;for(const e of this.soldiers){if(!e.alive||e.team===s.team||(e.player&&this.inVehicle))continue;const d=dist(s.pos,e.pos);if(d<best){best=d;heard=e}}return heard}
 private shareEnemy(s:Soldier,e:Soldier){
  const order=this.squadOrders.get(SQUAD_KEY(s.team,s.squad));if(!order)return;
  order.sharedEnemyId=e.id;order.sharedEnemyUntil=this.elapsed+({easy:1.9,normal:2.7,hard:3.6}[this.settings.difficulty]);
 }
 private squadLeader(s:Soldier):Soldier|null{
  const id=this.squadOrders.get(SQUAD_KEY(s.team,s.squad))?.leaderId;return id===undefined?null:(this.soldiers[id]??null);
 }
 private formationDestination(s:Soldier,leader:Soldier):Point{
  const members=this.soldiers.filter(m=>m.team===s.team&&m.squad===s.squad&&!m.player).sort((a,b)=>a.id-b.id);
  const idx=Math.max(0,members.findIndex(m=>m.id===s.id));const row=Math.floor(idx/2)+1,side=idx%2===0?-1:1;
  const forward=-4.7*row,lateral=3.2*side,sy=Math.sin(leader.yaw),cy=Math.cos(leader.yaw);
  return {x:leader.pos.x+cy*lateral+sy*forward,z:leader.pos.z-sy*lateral+cy*forward};
 }
 private findCover(s:Soldier,enemy:Point):Point|null{
  let best:Point|null=null,bestScore=Infinity;
  for(const c of COVER_POINTS){const d=dist(s.pos,c);if(d>44||d<3.5)continue;if(!this.sightBlocked(c,enemy,undefined,.25))continue;
   const enemyDistance=dist(c,enemy);if(enemyDistance<12)continue;const score=d-enemyDistance*.035+this.random.between(0,1.2);if(score<bestScore){bestScore=score;best=c}
  }
  return best?{...best}:null;
 }
 private safeDestination(s:Soldier,enemy:Point):Point{
  const options:Point[]=[BASES[s.team],...this.points.filter(p=>p.owner===s.team)];let best=options[0]!,score=-Infinity;
  for(const p of options){const value=dist(p,enemy)-dist(s.pos,p)*.28;if(value>score&&!this.sightBlocked(s.pos,p,undefined,1.5)){score=value;best=p}}
  return {...best};
 }
 private moveBot(s:Soldier,destination:Point,dt:number,speed:number){
  if(!s.route.length||s.nextPath<=this.elapsed){s.route=this.nav.find(s.pos,destination);s.nextPath=this.elapsed+this.random.between(1.25,3.1)}
  while(s.route.length&&dist(s.pos,s.route[0]!)<3.8)s.route.shift();
  const next=s.route[0]??destination;let velocity=norm({x:next.x-s.pos.x,z:next.z-s.pos.z});
  let avoidX=0,avoidZ=0;
  for(const mate of this.soldiers){if(mate.id===s.id||!mate.alive||mate.team!==s.team)continue;const d=dist(s.pos,mate.pos);if(d>0&&d<2.7){avoidX+=(s.pos.x-mate.pos.x)/d*(2.7-d);avoidZ+=(s.pos.z-mate.pos.z)/d*(2.7-d)}}
  velocity=norm({x:velocity.x+avoidX*.46,z:velocity.z+avoidZ*.46});const move=Math.min(4.2,dt*speed);s.yaw=Math.atan2(velocity.x,velocity.z);
  let moved=false;if(!collides(s.pos.x+velocity.x*move,s.pos.z,1.12)){s.pos.x+=velocity.x*move;moved=true}
  if(!collides(s.pos.x,s.pos.z+velocity.z*move,1.12)){s.pos.z+=velocity.z*move;moved=true}
  const progress=dist(s.pos,s.lastProgress);
  if(moved&&progress>.8){s.lastProgress={...s.pos};s.stuckFor=0}else s.stuckFor+=dt;
  if(!moved){s.route=[];s.nextPath=0}
  if(s.stuckFor>1.65){let escape:Point|null=null,best=Infinity;const seed=(s.id%8)*Math.PI/4;for(let i=0;i<8;i++){const a=seed+i*Math.PI/4,r=4.8+(i%2)*1.8,c={x:s.pos.x+Math.cos(a)*r,z:s.pos.z+Math.sin(a)*r};if(collides(c.x,c.z,1.12))continue;let crowd=0;for(const m of this.soldiers)if(m.id!==s.id&&m.alive&&m.team===s.team&&dist(c,m.pos)<3.2)crowd++;const score=dist(c,destination)+crowd*8;if(score<best){best=score;escape=c}}if(escape){s.route=[escape];s.nextPath=this.elapsed+.8;s.lastProgress={...s.pos}}else{this.navigationFailures++;s.route=[];s.nextPath=0}s.stuckFor=0}
 }

 private combatDestination(s:Soldier,enemy:Soldier):Point{
  const d=dist(s.pos,enemy.pos),dx=enemy.pos.x-s.pos.x,dz=enemy.pos.z-s.pos.z,len=Math.hypot(dx,dz)||1,fx=dx/len,fz=dz/len,side=(Math.floor(this.elapsed/2.25)+s.id)%2===0?1:-1;
  const lateral=side*(d<18?5.5:8.5),forward=d<14?-7:d>48?9:0;const q={x:s.pos.x+fz*lateral+fx*forward,z:s.pos.z-fx*lateral+fz*forward};return collides(q.x,q.z,1.3)?{...s.pos}:q;
 }
 private botAction(s:Soldier,dt:number){
  if(!s.alive||s.player)return;s.suppression=Math.max(0,s.suppression-dt*.17);s.morale=clamp(s.morale+dt*.025,0,1);
  const surge=!!this.eventForTeam(s.team,'COUNTER_OFFENSIVE');if(surge)s.morale=Math.max(s.morale,.56);
  const visible=this.findVisibleEnemy(s);if(visible?.id!==s.visualTarget){s.visualTarget=visible?.id??null;s.reactionUntil=this.elapsed+({easy:.8,normal:.5,hard:.32}[this.settings.difficulty])+this.random.between(.05,.22);s.burstLeft=0}if(visible){s.target=visible.id;s.alertUntil=this.elapsed+2.8;s.lastSeen={...visible.pos};s.lastSeenAt=this.elapsed;this.shareEnemy(s,visible)}else{const audible=this.findAudibleEnemy(s);if(audible&&this.random.next()<.58){const error={easy:7,normal:4.5,hard:2.8}[this.settings.difficulty];s.target=audible.id;s.alertUntil=this.elapsed+1.6;s.lastSeen={x:audible.pos.x+this.random.between(-error,error),z:audible.pos.z+this.random.between(-error,error)};s.lastSeenAt=this.elapsed-.15}}
  const order=this.squadOrders.get(SQUAD_KEY(s.team,s.squad));
  if(!visible&&order?.sharedEnemyId!==null&&order&&this.elapsed<order.sharedEnemyUntil){const shared=this.soldiers[order.sharedEnemyId];if(shared?.alive&&dist(s.pos,shared.pos)<125){s.target=shared.id;s.lastSeen={...shared.pos};s.lastSeenAt=this.elapsed-.4}}
  const tracked=s.target===null?null:this.soldiers[s.target]??null;
  const enemy=tracked?.alive&&dist(s.pos,tracked.pos)<112&&this.elapsed<s.alertUntil+1&&!this.sightBlocked(s.pos,tracked.pos)?tracked:null;
  const objective=this.points.find(p=>p.id===s.goal)??this.points[2]!,approach=order?.approach??objective,leader=this.squadLeader(s);
  const friendlyForce=this.localStrength(s.team,s.pos,30),enemyForce=this.localStrength(OPP(s.team),s.pos,30);let destination:Point=approach,speed=9.3*(surge?1.12:1);
  const barrage=this.activeMajorEvent?.kind==='ARTILLERY'&&this.activeMajorEvent.team!==s.team&&this.elapsed<this.activeMajorEvent.warningUntil?this.activeMajorEvent:null;
  const barragePoint=barrage?this.points.find(p=>p.id===barrage.objective)??null:null;

  if(this.elapsed<s.spawnGraceUntil){s.state='SPAWN';destination=leader&&leader.id!==s.id?this.formationDestination(s,leader):approach;speed=8.2}
  else if(s.team==='red'&&this.supports.some(e=>e.kind==='artillery'&&this.elapsed<e.starts+5&&dist(s.pos,e.at)<48)){s.state='SEEK_COVER';destination=this.safeDestination(s,this.supports.find(e=>e.kind==='artillery')!.at);speed=11.6}
  else if(barragePoint&&dist(s.pos,barragePoint)<48){s.state='SEEK_COVER';destination=this.safeDestination(s,barragePoint);speed=11.6;s.cover=null}
  else if(enemy&&(s.hp<24||(enemyForce>friendlyForce*1.55&&(s.hp<68||s.morale<.55)))){s.state='RETREAT';destination=this.safeDestination(s,enemy.pos);speed=11.3;s.cover=null}
  else if(enemy&&(s.hp<55||s.suppression>.64)){
   if(!s.cover||dist(s.pos,s.cover)<2.2||!this.sightBlocked(s.cover,enemy.pos,undefined,.2))s.cover=this.findCover(s,enemy.pos);
   if(s.cover){s.state='SEEK_COVER';destination=s.cover;speed=10.4}else{s.state='ENGAGE';destination=this.combatDestination(s,enemy);speed=7.2}
  }
  else if(enemy){s.state='ENGAGE';destination=this.combatDestination(s,enemy);speed=dist(s.pos,enemy.pos)>62?7.8:dist(destination,s.pos)>2?5.4:0}
  else if(s.lastSeen&&this.elapsed-s.lastSeenAt<4.8){s.state='SEARCH';destination=s.lastSeen;speed=8.7}
  else if(leader&&leader.id!==s.id&&leader.alive&&dist(s.pos,leader.pos)>17&&dist(s.pos,objective)>28){s.state='FOLLOW_SQUAD';destination=this.formationDestination(s,leader);speed=10}
  else if(dist(s.pos,approach)>8&&dist(s.pos,objective)>17){s.state='MOVE_TO_OBJECTIVE';destination=approach;speed=10.2*(surge?1.08:1)}
  else if(dist(s.pos,objective)<27){s.state=objective.owner===s.team&&!objective.contested&&dist(s.pos,approach)<5?'IDLE':'DEFEND';destination=order?.intent==='DEFEND'?approach:objective;speed=s.state==='IDLE'?0:5.8}
  else {s.state='MOVE_TO_OBJECTIVE';destination=objective;speed=10.1}

  if(speed>0&&dist(s.pos,destination)>2.5)this.moveBot(s,destination,dt,speed);else if(enemy)s.yaw=Math.atan2(enemy.pos.x-s.pos.x,enemy.pos.z-s.pos.z);

  if(enemy&&visible?.id===enemy.id&&this.elapsed>=s.reactionUntil){const d=dist(s.pos,enemy.pos);if(d<103&&this.elapsed>=s.nextShot){
   const cfg=WEAPONS.carbine;
   if(s.burstLeft<=0)s.burstLeft=d>65?2:d>30?3:5;
   s.burstLeft--;s.nextShot=this.elapsed+(s.burstLeft>0?cfg.fireInterval:this.random.between(.45,.9));
   const lead={easy:.25,normal:.65,hard:.85}[this.settings.difficulty],flight=d/230;
   const error=({easy:.045,normal:.024,hard:.015}[this.settings.difficulty]*d+.5)*(1+s.suppression*2.8)*(speed>0?1.3:1)*(s.morale<.5?1.3:1);
   const impact={x:enemy.pos.x+enemy.velocity.x*flight*lead+this.random.between(-error,error),z:enemy.pos.z+enemy.velocity.z*flight*lead+this.random.between(-error,error)};
   // A geometric ray test replaces the old chance-to-damage roll.
   const aim=norm({x:impact.x-s.pos.x,z:impact.z-s.pos.z}),dx=enemy.pos.x+enemy.velocity.x*flight-s.pos.x,dz=enemy.pos.z+enemy.velocity.z*flight-s.pos.z;
   const hit=Math.abs(dx*aim.z-dz*aim.x)<.82&&dx*aim.x+dz*aim.z>0&&!this.sightBlocked(s.pos,enemy.pos);
   this.emit({type:'shot',team:s.team,from:{...s.pos},to:impact,hit,player:false,weapon:'carbine'});
   enemy.suppression=clamp(enemy.suppression+(hit?.18:.04),0,1);
   if(hit)this.damage(enemy,cfg.damage*.72*clamp(1-d/180,.45,1),s);
  }}else if(this.elapsed-s.lastSeenAt>5){s.target=null;s.lastSeen=null;s.cover=null}
 }

 private eventTarget(team:Team):CapturePoint{
  let best=this.points[2]!,bestScore=-Infinity;for(const p of this.points){const friendly=this.localStrength(team,p,62),enemy=this.localStrength(OPP(team),p,62);let score=(p.owner===team?-2:p.owner===null?2.5:4.2)+(p.contested?3.5:0)+(enemy-friendly)*.16-dist(BASES[team],p)*.002;score+=this.random.between(0,.35);if(score>bestScore){bestScore=score;best=p}}return best;
 }
 private chooseEventTeam():Team{
  const diff=this.tickets.blue-this.tickets.red;if(Math.abs(diff)>45)return diff<0?'blue':'red';const blueHeld=this.points.filter(p=>p.owner==='blue').length,redHeld=this.points.filter(p=>p.owner==='red').length;if(blueHeld!==redHeld)return blueHeld<redHeld?'blue':'red';return this.majorEventSequence%2===0?'red':'blue';
 }
 private startMajorEvent(){
  const kinds:MajorEventKind[]=['ARTILLERY','COUNTER_OFFENSIVE','ARMORED_PUSH'],kind=kinds[(this.majorEventSequence+this.majorEventOffset)%kinds.length]!,team=this.chooseEventTeam(),target=this.eventTarget(team);this.majorEventSequence++;
  const warning=kind==='ARTILLERY'?5:0,duration=kind==='ARTILLERY'?18:kind==='COUNTER_OFFENSIVE'?44:52;
  this.activeMajorEvent={id:this.majorEventId++,kind,team,objective:target.id,startedAt:this.elapsed,endsAt:this.elapsed+duration,warningUntil:this.elapsed+warning,nextPulse:this.elapsed+warning,pulsesRemaining:kind==='ARTILLERY'?4:0};
  if(kind==='COUNTER_OFFENSIVE'){for(const s of this.soldiers)if(s.team===team&&!s.player){s.morale=Math.max(s.morale,.72);if(!s.alive)s.respawnAt=Math.min(s.respawnAt,this.elapsed+2.8)}}
  if(kind==='ARMORED_PUSH'){const armor=this.vehicles.find(v=>v.team===team);if(armor){if(!armor.alive)armor.respawnAt=Math.min(armor.respawnAt,this.elapsed+5.5);else if(armor.driver!=='player'){armor.driver='ai';armor.nextDecision=this.elapsed;armor.route=[]}}}
  this.nextMajorEventAt=this.elapsed+duration+this.random.between(56,82);this.emit({type:'majorEvent',action:warning?'warning':'start',kind,team,objective:target.id});this.assignSquads();
 }
 private artilleryImpact(e:ActiveMajorEvent){
  const target=this.points.find(p=>p.id===e.objective)??this.points[2]!,angle=this.random.between(0,Math.PI*2),spread=this.random.between(3,28),at={x:target.x+Math.cos(angle)*spread,z:target.z+Math.sin(angle)*spread},radius=this.random.between(15,20);
  this.emit({type:'artilleryImpact',team:e.team,at,radius});for(const s of this.soldiers){if(!s.alive||s.team===e.team)continue;const d=dist(s.pos,at);if(d>radius)continue;const amount=(1-d/radius)*this.random.between(54,88)+10;this.damageFromEvent(s,amount,e.team,at)}
  for(const v of this.vehicles){if(!v.alive||v.team===e.team)continue;const d=dist(v.pos,at);if(d>radius+5)continue;this.damageVehicle(v,(1-d/(radius+5))*this.random.between(34,62)+8,e.team)}
 }
 private updateMajorEvent(){
  if(!this.activeMajorEvent){if(this.elapsed>=this.nextMajorEventAt&&this.elapsed>45)this.startMajorEvent();return}const e=this.activeMajorEvent;
  if(e.kind==='ARTILLERY'&&e.pulsesRemaining>0&&this.elapsed>=e.nextPulse){if(e.pulsesRemaining===4)this.emit({type:'majorEvent',action:'start',kind:e.kind,team:e.team,objective:e.objective});this.artilleryImpact(e);e.pulsesRemaining--;e.nextPulse=this.elapsed+3.15}
  if(this.elapsed>=e.endsAt){this.emit({type:'majorEvent',action:'end',kind:e.kind,team:e.team,objective:e.objective});this.activeMajorEvent=null;this.assignSquads()}
 }

 private damageVehicle(v:ArmoredVehicle,amount:number,attacker:Team,playerCredit=false){
  if(!v.alive)return;v.hp=Math.max(0,v.hp-amount);if(v.hp>0)return;
  v.alive=false;v.disabledAt=this.elapsed;v.respawnAt=v.requisitioned?Infinity:this.elapsed+VEHICLE_TYPES[v.kind].respawnDelay;v.speed=0;v.route=[];
  if(v.driver==='player'){const p=this.player;this.playerVehicleId=null;v.driver='ai';const side={x:v.pos.x+4.8,z:v.pos.z};p.pos=collides(side.x,side.z,1.05)?{x:v.pos.x,z:v.pos.z+4.8}:side;p.hp=Math.max(15,p.hp-55)}
  if(playerCredit&&v.team!==this.player.team)this.awardRP(RP_REWARDS.vehicle,'vehicle destroyed');
  this.emit({type:'vehicleDisabled',team:v.team,id:v.id,at:{...v.pos}});
  // Vehicle losses matter strategically but do not duplicate a full soldier ticket penalty.
  if(attacker!==v.team&&this.settings.killTicketPenalty){this.tickets[v.team]=Math.max(0,this.tickets[v.team]-.5*(this.settings.killTicketPenalty??1));this.checkFinish()}
 }

 private resetVehicle(v:ArmoredVehicle){const p=v.spawn;v.pos={...p};v.lastProgress={...p};v.yaw=v.team==='blue'?0:Math.PI;v.turretYaw=v.yaw;v.hp=v.maxHp;v.alive=true;v.speed=0;v.driver=v.team==='blue'?null:'ai';v.route=[];v.nextPath=0;v.nextDecision=this.elapsed+(v.team==='blue'?12:.5);v.nextShot=this.elapsed+1;v.stuckFor=0;this.emit({type:'vehicleRespawn',team:v.team,id:v.id})}

 private vehicleMove(v:ArmoredVehicle,destination:Point,dt:number){
  if(!v.route.length||v.nextPath<=this.elapsed){v.route=this.nav.find(v.pos,destination);v.nextPath=this.elapsed+2.2}
  while(v.route.length&&dist(v.pos,v.route[0]!)<7)v.route.shift();const next=v.route[0]??destination;
  const desired=Math.atan2(next.x-v.pos.x,next.z-v.pos.z),delta=Math.atan2(Math.sin(desired-v.yaw),Math.cos(desired-v.yaw));v.yaw+=clamp(delta,-VEHICLE_TYPES[v.kind].turnRate*dt,VEHICLE_TYPES[v.kind].turnRate*dt);
  const alignment=Math.max(.15,Math.cos(delta));const target=VEHICLE_TYPES[v.kind].maxForward*(.48+.52*alignment);v.speed+=clamp(target-v.speed,-VEHICLE_TYPES[v.kind].acceleration*dt,VEHICLE_TYPES[v.kind].acceleration*dt);
  const dx=Math.sin(v.yaw)*v.speed*dt,dz=Math.cos(v.yaw)*v.speed*dt;let moved=false;
  if(!collides(v.pos.x+dx,v.pos.z,VEHICLE_TYPES[v.kind].radius)){v.pos.x+=dx;moved=true}if(!collides(v.pos.x,v.pos.z+dz,VEHICLE_TYPES[v.kind].radius)){v.pos.z+=dz;moved=true}
  if(dist(v.pos,v.lastProgress)>2.2){v.lastProgress={...v.pos};v.stuckFor=0}else v.stuckFor+=dt;
  if(!moved||v.stuckFor>2.2){
   let escape:Point|null=null,best=Infinity;
   for(let i=0;i<16;i++){const a=v.yaw+i*Math.PI/8,r=VEHICLE_TYPES[v.kind].radius+5,q={x:v.pos.x+Math.sin(a)*r,z:v.pos.z+Math.cos(a)*r};if(collides(q.x,q.z,VEHICLE_TYPES[v.kind].radius))continue;let clear=true;for(let t=.2;t<=1;t+=.2)if(collides(v.pos.x+(q.x-v.pos.x)*t,v.pos.z+(q.z-v.pos.z)*t,VEHICLE_TYPES[v.kind].radius)){clear=false;break}if(!clear)continue;const score=dist(q,destination);if(score<best){best=score;escape=q}}
   if(escape){v.route=[escape];v.yaw=Math.atan2(escape.x-v.pos.x,escape.z-v.pos.z);v.nextPath=this.elapsed+1.8;v.speed=2.5;v.stuckFor=0}
   else{v.speed=0;v.nextPath=this.elapsed+1;v.yaw+=.5;if(v.stuckFor>6){this.navigationFailures++;v.stuckFor=0}}
  }
 }

 private vehicleAction(v:ArmoredVehicle,dt:number){
  const push=this.eventForTeam(v.team,'ARMORED_PUSH'),pushTarget=push?this.points.find(p=>p.id===push.objective)??null:null;const candidates=this.points.filter(p=>p.owner!==v.team);const destination=pushTarget??(candidates.sort((a,b)=>dist(v.pos,a)-dist(v.pos,b))[0]??this.points[2]!);v.goal=destination.id;
  let enemyVehicle:ArmoredVehicle|null=null,vd:number=VEHICLE_TYPES[v.kind].weaponRange;
  for(const e of this.vehicles){if(!e.alive||e.team===v.team)continue;const d=dist(v.pos,e.pos);if(d<vd&&!this.sightBlocked(v.pos,e.pos,undefined,1.2)){vd=d;enemyVehicle=e}}
  let enemy:Soldier|null=null,sd=Math.min(145,vd);
  for(const s of this.soldiers){if(!s.alive||s.team===v.team)continue;const d=dist(v.pos,s.pos);if(d<sd&&!this.sightBlocked(v.pos,s.pos)){sd=d;enemy=s}}
  const target=enemyVehicle?.pos??enemy?.pos; if(target){v.turretYaw=Math.atan2(target.x-v.pos.x,target.z-v.pos.z);if(this.elapsed>=v.nextShot){v.nextShot=this.elapsed+VEHICLE_TYPES[v.kind].weaponInterval+this.random.between(.1,.45);const hit=this.random.next()<(enemyVehicle ? .6 : .48);const impact=hit?target:{x:target.x+this.random.between(-7,7),z:target.z+this.random.between(-7,7)};this.emit({type:'vehicleShot',team:v.team,from:{...v.pos},to:{...impact},hit,player:false});if(hit){if(enemyVehicle)this.damageVehicle(enemyVehicle,VEHICLE_TYPES[v.kind].weaponDamage,v.team);else if(enemy)this.damage(enemy,this.random.between(42,70),this.soldiers.find(s=>s.team===v.team&&!s.player)??this.player)}}}
  if(!target||dist(v.pos,target)>34)this.vehicleMove(v,destination,dt*(push?1.12:1));else v.speed*=Math.max(0,1-dt*2.4);
 }

 private checkFinish(){
  if(this.finished)return;if(this.tickets.blue>0&&this.tickets.red>0)return;
  this.finished=true;
  if(this.tickets.blue!==this.tickets.red)this.winner=this.tickets.blue>this.tickets.red?'blue':'red';
  else {const held=(team:Team)=>this.points.filter(p=>p.owner===team).length,blueHeld=held('blue'),redHeld=held('red');
   if(blueHeld!==redHeld)this.winner=blueHeld>redHeld?'blue':'red';else {const blueKills=this.soldiers.filter(s=>s.team==='blue').reduce((n,s)=>n+s.kills,0),redKills=this.soldiers.filter(s=>s.team==='red').reduce((n,s)=>n+s.kills,0);this.winner=blueKills>=redKills?'blue':'red'}}
  this.emit({type:'end',winner:this.winner});
 }

 tick(dt:number){
  if(this.finished)return;dt=clamp(dt,0,.15);this.elapsed+=dt;
  if(this.playerReloadUntil>0&&this.elapsed>=this.playerReloadUntil&&this.reloadingWeapon){const weapon=this.reloadingWeapon,def=WEAPONS[weapon],need=def.magazine-this.ammo[weapon],take=Math.min(need,this.reserve[weapon]);this.ammo[weapon]+=take;this.reserve[weapon]-=take;this.playerReloadUntil=0;this.reloadingWeapon=null}
  this.updateSupports();this.updateMajorEvent();this.commanderAcc+=dt;if(this.commanderAcc>=2.2){this.commanderAcc=0;this.assignSquads()}
  for(const s of this.soldiers){
   if(!s.alive){if(!s.player&&this.elapsed>=s.respawnAt){this.placeAtSpawn(s);this.emit({type:'respawn',id:s.id})}continue}
   if(s.player)continue;if(this.elapsed<s.nextAiAt)continue;
   const fromPlayer=dist(s.pos,this.player.pos),difficultyRate={easy:1.28,normal:1,hard:.82}[this.settings.difficulty];const interval=(fromPlayer<120 ? .05 : fromPlayer<300 ? .125 : .40)*difficultyRate;const aiDt=clamp(this.elapsed-s.lastAiAt,.03,.45);s.lastAiAt=this.elapsed;s.nextAiAt=this.elapsed+interval+this.random.between(0,interval*.12);const old={...s.pos};this.botAction(s,aiDt);s.velocity={x:(s.pos.x-old.x)/aiDt,z:(s.pos.z-old.z)/aiDt};
  }
  for(const v of this.vehicles){if(!v.alive){if(this.elapsed>=v.respawnAt)this.resetVehicle(v);continue}if(v.driver==='player'){this.player.pos={...v.pos};continue}if(this.elapsed<v.nextDecision)continue;if(v.driver===null)v.driver='ai';const vehicleDt=Math.max(.05,Math.min(.35,this.elapsed-v.nextDecision+.22));v.nextDecision=this.elapsed+.22+this.random.between(0,.04);this.vehicleAction(v,vehicleDt)}
  for(const p of this.points){const change=updateCapture(p,this.soldiers,dt);if(change){this.emit({type:'capture',id:p.id,owner:change.after});if(change.after==='blue'&&this.player.alive&&dist(this.player.pos,p)<25)this.awardRP(RP_REWARDS.capture,'capture')}}
  if(this.player.alive&&this.elapsed>=this.nextObjectiveReward&&this.points.some(p=>dist(p,this.player.pos)<25&&(p.owner!=='blue'||p.contested))){this.awardRP(RP_REWARDS.objectiveTick,'objective participation');this.nextObjectiveReward=this.elapsed+5}
  this.tickets.blue=Math.max(0,this.tickets.blue-bleedRate(this.points,'blue')*dt);this.tickets.red=Math.max(0,this.tickets.red-bleedRate(this.points,'red')*dt);
  if(this.elapsed>900){this.tickets.blue=Math.max(0,this.tickets.blue-.28*dt);this.tickets.red=Math.max(0,this.tickets.red-.28*dt)}
  this.checkFinish();
 }

 getSquadTargets():{team:Team;squad:number;objective:string;leaderId:number;intent:SquadIntent;approach:Point}[]{return [...this.squadOrders.values()].map(o=>({team:o.team,squad:o.squad,objective:o.objective,leaderId:o.leaderId,intent:o.intent,approach:{...o.approach}}))}
 getMajorEvent():ActiveMajorEvent|null{return this.activeMajorEvent?{...this.activeMajorEvent}:null}
 getNavigationFailures(){return this.navigationFailures}
}
