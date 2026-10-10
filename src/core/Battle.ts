import {MapContext,type MapId} from '../world/Maps';
import {clamp,dist,norm,Random,type Point} from './math';
import {type Team} from '../world/Layout';
import {controlFlight,guideMissile} from '../vehicles/Flight';
import {SquadLeader} from '../ai/SquadLeader';
import {Commander} from '../ai/Commander';
import {Intelligence} from '../ai/Intelligence';
import {Navigation} from '../ai/Navigation';
import {resolveIntent,type AIIntent,type AIIntentKind,type AIProfile} from '../ai/Intent';
import {bleedRate,updateCapture,type CapturePoint} from '../gameplay/Conquest';
import {WEAPONS,WEAPON_ORDER,CLASSES,DEFAULT_LOADOUT,validateLoadout,type Loadout,type ClassId,type WeaponId} from '../combat/Weapons';
import {createArmoredVehicles,AMMUNITION,VEHICLE,VEHICLE_TYPES,createVehicle,vehicleMuzzle,type ArmoredVehicle} from '../vehicles/Vehicle';

import {SUPPORTS,RP_REWARDS,type SupportId,type SupportEffect} from '../gameplay/Requisition';

export type BotState='SPAWN'|'IDLE'|'FOLLOW_SQUAD'|'MOVE_TO_OBJECTIVE'|'SEARCH'|'ENGAGE'|'SEEK_COVER'|'DEFEND'|'RETREAT'|'RESPAWN'|'DRIVING'|'BOARD_VEHICLE'|'REVIVE'|'DOWNED';
export type SquadIntent='ATTACK'|'DEFEND'|'FLANK'|'REINFORCE';
export type MajorEventKind='ARTILLERY'|'COUNTER_OFFENSIVE'|'ARMORED_PUSH';
export interface ActiveMajorEvent {id:number;kind:MajorEventKind;team:Team;objective:string;startedAt:number;endsAt:number;warningUntil:number;nextPulse:number;pulsesRemaining:number}

export interface Soldier {
 id:number;team:Team;classId:ClassId;equipmentUntil?:number;equipmentKind?:'medical'|'repair'|'ammo'|'rocket'|'smoke';weaponId:WeaponId;nextEquipment:number;aiAmmo:number;player:boolean;squad:number;squadLeader:boolean;pos:Point;yaw:number;
 vehicleId:number|null;boardingUntil:number;velocity:Point;visualTarget:number|null;reactionUntil:number;burstLeft:number;
 downedUntil:number;downedBy:number|null;rescueCalled:boolean;altitude:number;parachuting:boolean;combatUntil:number;hp:number;alive:boolean;respawnAt:number;deathAt:number;spawnGraceUntil:number;kills:number;deaths:number;
 state:BotState;goal:string;target:number|null;nextShot:number;route:Point[];routeFailed:boolean;nextPath:number;
 alertUntil:number;lastSeen:Point|null;lastSeenAt:number;suppression:number;morale:number;
 cover:Point|null;nextAiAt:number;lastAiAt:number;stuckFor:number;lastProgress:Point;
 aiIntent:AIIntent|null;aiStats:{destinationChanges:number;stateChanges:number;navigationFailures:number;movingWithoutProgress:number;idleWithoutReason:number};
}

export interface Projectile {id:number;kind:'shell'|'rocket';team:Team;player:boolean;pos:{x:number;y:number;z:number};velocity:{x:number;y:number;z:number};damage:number;radius:number;gravity:number;armor:number;remaining:number;source:number|null;attackerId?:number;targetId?:number;guidedSpeed?:number}

interface SquadOrder {team:Team;squad:number;objective:string;leaderId:number;sharedEnemyId:number|null;sharedEnemyAt:Point|null;sharedEnemyUntil:number;intent:SquadIntent;approach:Point}

export type BattleEvent =
 |{type:'shot';team:Team;from:Point;to:Point;hit:boolean;player:boolean;owner?:number;weapon?:WeaponId;muzzle?:{x:number;y:number;z:number};end?:{x:number;y:number;z:number}}
 |{type:'hitConfirmed';owner:number;target:number;vehicle:boolean;amount:number;killed:boolean;at:Point}
 |{type:'playerHit';amount:number;from:Point;victim?:number}
 |{type:'death';victim:number;killer:number|null;team:Team}
 |{type:'capture';id:string;owner:Team|null}
 |{type:'respawn';id:number}
 |{type:'end';winner:Team}
 |{type:'vehicleShot';team:Team;from:Point;to:Point;hit:boolean;player:boolean;owner?:number;weapon:'shell'|'mg'|'rocket';muzzle?:{x:number;y:number;z:number}}
 |{type:'projectileImpact';team:Team;at:Point;radius:number;kind:'shell'|'rocket'}
 |{type:'revive';id:number}
 |{type:'thrown';owner:number;kind:'frag'|'smoke'}
 |{type:'vehicleHit';id:number;amount:number;from:Point}
 |{type:'vehicleDisabled';team:Team;id:number;at:Point}
 |{type:'vehicleRespawn';team:Team;id:number}
 |{type:'vehicleEnter'|'vehicleExit';id:number}
 |{type:'majorEvent';action:'warning'|'start'|'end';kind:MajorEventKind;team:Team;objective:string}
 |{type:'rp';amount:number;reason:string;owner?:number}
 |{type:'support';kind:SupportId;at:Point}
 |{type:'grenade';at:Point;radius:number}
 |{type:'artilleryImpact';team:Team;at:Point;radius:number};

export interface BattleSettings {
 mapId?:MapId;
 mode?:'conquest'|'breakthrough';
 size:8|16|32|64;aiEnabled?:boolean;vehicleLimit?:number;
 aiProfile?:AIProfile;
 difficulty:'easy'|'normal'|'hard';
 tickets:number;
 killTicketPenalty?:number;
 seed?:number;
}

const copyState=<T>(value:T):T=>value&&typeof value==='object'?(Array.isArray(value)?value.map(copyState):Object.fromEntries(Object.entries(value).map(([k,v])=>[k,copyState(v)]))) as T:value;
const OPP=(t:Team):Team=>t==='blue'?'red':'blue';
const SQUAD_KEY=(team:Team,squad:number)=>`${team}-${squad}`;

export class Battle {
 readonly soldiers:Soldier[]=[];
 readonly map:MapContext;
 readonly points:CapturePoint[];
 readonly tickets:{blue:number;red:number};
 readonly settings:BattleSettings;
 readonly vehicles:ArmoredVehicle[];
 readonly nav:Navigation;
 private vehicleNav=new Map<string,Navigation>();
 readonly commanders={blue:new Commander('blue'),red:new Commander('red')};
 readonly squadLeaderAI=new SquadLeader();
 readonly tacticalLog:{at:number;soldier:number;from:string;to:string;reason:string}[]=[];
 private rescuePaths=new Map<string,{checked:number;reachable:boolean}>();
 private intentSwitches=0;private formationUpdates=0;private rescueCount=0;private repairCount=0;
 private squadPlanAt=0;private coverReservations=new Map<string,{id:number;until:number}>();
 remoteRecommendation:ReturnType<Battle["getRecommendation"]>|undefined;
 readonly intelligence={blue:new Intelligence(),red:new Intelligence()};
 readonly random:Random;
 sectorIndex=0;
 elapsed=0;winner:Team|null=null;finished=false;
 playerWeapon:WeaponId='carbine';playerReloadUntil=0;playerNextShot=0;playerAiming=false;
 loadout:Loadout={...DEFAULT_LOADOUT};
 requisitionPoints=0;readonly supports:SupportEffect[]=[];
 readonly supportCooldowns:Partial<Record<SupportId,number>>={};
 readonly projectiles:Projectile[]=[];private projectileSequence=1;
 playerSeat=0;aaCount=2;aaUntil=0;rocketCount=2;rocketUntil=0;beacon:{at:Point;expires:number}|null=null;
 slideUntil=0;slideCooldown=0;slideDirection:Point={x:0,z:0};
 grenadeCount=2;gadgetCharges=2;playerThrowUntil=0;playerGadgetUntil=0;
 private supportSequence=1;private nextObjectiveReward=0;
 private contributions=new Map<number,{damage:number;at:number;owner:number}>();
 readonly grenades:{id:number;kind:'frag'|'smoke';pos:{x:number;y:number;z:number};velocity:{x:number;y:number;z:number};detonate:number;team:Team;owner:number}[]=[];
 readonly ammoBoxes:{id:number;at:Point;owner:number;team:Team;expires:number;nextPulse:number}[]=[];
 private ammo=Object.fromEntries(WEAPON_ORDER.map(id=>[id,WEAPONS[id].magazine])) as Record<WeaponId,number>;
 private reserve=Object.fromEntries(WEAPON_ORDER.map(id=>[id,WEAPONS[id].reserve])) as Record<WeaponId,number>;
 private reloadingWeapon:WeaponId|null=null;
 private commanderAcc=100;private squadOrders=new Map<string,SquadOrder>();private eventQueue:BattleEvent[]=[];
 private navigationFailures=0;private pointThreatUntil=new Map<string,number>();private playerAttackers=new Map<number,number>();
 private assignedMissions=0;private completedMissions=0;private completedOrders=new Set<string>();
 private playerVehicleId:number|null=null;
 private nextMajorEventAt=64;private majorEventSequence=0;private majorEventOffset=0;private majorEventId=1;private activeMajorEvent:ActiveMajorEvent|null=null;

 constructor(settings:BattleSettings){
  this.map=new MapContext(settings.mapId);this.nav=new Navigation(1.12,false,this.map);this.points=this.map.objectives.map(o=>({...o,control:0,owner:null,contested:false,blueCount:0,redCount:0}));
  this.settings={...settings,mapId:this.map.definition.id,mode:settings.mode==='breakthrough'?'breakthrough':'conquest',aiProfile:settings.aiProfile==='elite'?'elite':'regular',killTicketPenalty:settings.killTicketPenalty??1};
  delete this.settings.vehicleLimit;this.vehicles=createArmoredVehicles(16,this.map.definition.parking).filter(v=>this.map.definition.allowedVehicles.includes(v.kind));
  this.random=new Random(settings.seed??12731);this.tickets={blue:settings.tickets,red:settings.tickets};
  let id=0;
  for(const team of ['blue','red'] as const)for(let i=0;i<settings.size;i++){
   const player=team==='blue'&&i===0;
   const soldier:Soldier={id:id++,team,weaponId:(['carbine','smg','sniper','lmg'] as WeaponId[])[i%4]!,nextEquipment:10+i%7,aiAmmo:30,classId:(['assault','medic','recon','engineer'] as const)[i%4]!,player,squad:Math.floor(i/4),squadLeader:false,pos:{...this.map.bases[team]},yaw:team==='blue'?0:Math.PI,
    vehicleId:null,boardingUntil:0,velocity:{x:0,z:0},visualTarget:null,reactionUntil:0,burstLeft:0,downedUntil:0,downedBy:null,rescueCalled:false,altitude:0,parachuting:false,combatUntil:0,hp:100,alive:true,respawnAt:0,deathAt:-999,spawnGraceUntil:1.2,kills:0,deaths:0,state:'SPAWN',goal:'C',target:null,nextShot:0,
    route:[],routeFailed:false,nextPath:0,alertUntil:0,lastSeen:null,lastSeenAt:-999,suppression:0,morale:1,cover:null,nextAiAt:0,lastAiAt:0,
    stuckFor:0,lastProgress:{...this.map.bases[team]},aiIntent:null,aiStats:{destinationChanges:0,stateChanges:0,navigationFailures:0,movingWithoutProgress:0,idleWithoutReason:0}};
   this.soldiers.push(soldier);this.placeAtSpawn(soldier,true);
  }
  if(this.settings.mode==='breakthrough')for(const p of this.points){p.owner='red';p.control=-100;}
  this.majorEventOffset=this.random.int(3);this.nextMajorEventAt=this.random.between(46,55);
  this.assignSquads();
 }

 get combatObjectives():CapturePoint[]{return this.settings.mode==='breakthrough'?[this.points[Math.min(this.sectorIndex,4)]!]:this.points}
 controlledPlayerId=0;
 get player():Soldier{return this.soldiers[this.controlledPlayerId]!}
 exportPlayerState(){return {loadout:copyState(this.loadout),playerWeapon:copyState(this.playerWeapon),playerReloadUntil:copyState(this.playerReloadUntil),playerNextShot:copyState(this.playerNextShot),playerAiming:copyState(this.playerAiming),requisitionPoints:copyState(this.requisitionPoints),playerSeat:copyState(this.playerSeat),rocketCount:copyState(this.rocketCount),rocketUntil:copyState(this.rocketUntil),aaCount:copyState(this.aaCount),aaUntil:copyState(this.aaUntil),beacon:copyState(this.beacon),slideUntil:copyState(this.slideUntil),slideCooldown:copyState(this.slideCooldown),slideDirection:copyState(this.slideDirection),grenadeCount:copyState(this.grenadeCount),gadgetCharges:copyState(this.gadgetCharges),playerThrowUntil:copyState(this.playerThrowUntil),playerGadgetUntil:copyState(this.playerGadgetUntil),ammo:copyState(this.ammo),reserve:copyState(this.reserve),reloadingWeapon:copyState(this.reloadingWeapon),playerVehicleId:copyState(this.playerVehicleId),nextObjectiveReward:copyState(this.nextObjectiveReward),unstuckUntil:copyState(this.unstuckUntil),supportCooldowns:{...this.supportCooldowns}};}
 importPlayerState(state:ReturnType<Battle["exportPlayerState"]>){this.loadout=copyState(state.loadout);this.playerWeapon=copyState(state.playerWeapon);this.playerReloadUntil=copyState(state.playerReloadUntil);this.playerNextShot=copyState(state.playerNextShot);this.playerAiming=copyState(state.playerAiming);this.requisitionPoints=copyState(state.requisitionPoints);this.playerSeat=copyState(state.playerSeat);this.rocketCount=copyState(state.rocketCount);this.rocketUntil=copyState(state.rocketUntil);this.aaCount=copyState(state.aaCount);this.aaUntil=copyState(state.aaUntil);this.beacon=copyState(state.beacon);this.slideUntil=copyState(state.slideUntil);this.slideCooldown=copyState(state.slideCooldown);this.slideDirection=copyState(state.slideDirection);this.grenadeCount=copyState(state.grenadeCount);this.gadgetCharges=copyState(state.gadgetCharges);this.playerThrowUntil=copyState(state.playerThrowUntil);this.playerGadgetUntil=copyState(state.playerGadgetUntil);this.ammo=copyState(state.ammo);this.reserve=copyState(state.reserve);this.reloadingWeapon=copyState(state.reloadingWeapon);this.playerVehicleId=copyState(state.playerVehicleId);this.nextObjectiveReward=copyState(state.nextObjectiveReward);this.unstuckUntil=copyState(state.unstuckUntil);Object.keys(this.supportCooldowns).forEach(k=>delete this.supportCooldowns[k as SupportId]);Object.assign(this.supportCooldowns,state.supportCooldowns);}
 get ongoing():boolean{return !this.finished}
 get playerAmmo():number{return this.ammo[this.playerWeapon]}
 get playerReserveAmmo():number{return this.reserve[this.playerWeapon]}
 get activeWeapon(){return WEAPONS[this.playerWeapon]}
 get playerVehicle():ArmoredVehicle|null{return this.playerVehicleId===null?null:(this.vehicles[this.playerVehicleId]??null)}
 get inVehicle():boolean{return !!this.playerVehicle?.alive}
 events():BattleEvent[]{const out=this.eventQueue;this.eventQueue=[];return out}
 private emit(e:BattleEvent){if(e.type==='shot'&&!e.muzzle){const d=norm({x:e.to.x-e.from.x,z:e.to.z-e.from.z});e.muzzle={x:e.from.x+d.x*.95+d.z*.1,y:this.map.heightAt(e.from.x,e.from.z)+1.09,z:e.from.z+d.z*.95-d.x*.1};e.end={x:e.to.x,y:this.map.heightAt(e.to.x,e.to.z)+1.35,z:e.to.z};}if(this.eventQueue.length>=320)this.eventQueue.shift();this.eventQueue.push(e);if(e.type==='shot'||e.type==='vehicleShot'){this.reactToGunfire(e.team,e.from,e.to,e.type==='vehicleShot'?105:72);for(const point of this.points)if(point.owner!==e.team&&dist(point,e.to)<35)this.pointThreatUntil.set(point.id,this.elapsed+8);for(const bot of this.soldiers)if(bot.team===e.team&&dist(bot.pos,e.from)<2)bot.combatUntil=this.elapsed+8;}}
 private reactToGunfire(team:Team,from:Point,to:Point,radius:number){for(const s of this.soldiers){if(!s.alive||s.team===team)continue;const near=Math.min(dist(s.pos,from),dist(s.pos,to));if(near>radius)continue;if(this.random.next()>.58)continue;s.alertUntil=Math.max(s.alertUntil,this.elapsed+2.2);s.lastSeen={x:from.x+this.random.between(-8,8),z:from.z+this.random.between(-8,8)};s.lastSeenAt=this.elapsed-.25;s.suppression=clamp(s.suppression+(near<30?.11:.045),0,1)}}

 private placeAtSpawn(s:Soldier,home=false,pointId?:string){
  let center:Point=this.map.bases[s.team];
  const point=this.points.find(x=>x.id===pointId&&x.owner===s.team);
  if(point)center=point;
  else if(!home){const own=this.points.filter(x=>x.owner===s.team);if(own.length&&this.random.next()<.68)center=own[this.random.int(own.length)]!}
  let final={...center};
  if(!(s.player&&home))for(let n=0;n<50;n++){
   const theta=this.random.between(0,Math.PI*2),radius=this.random.between(8,28);
   const candidate={x:center.x+Math.cos(theta)*radius,z:center.z+Math.sin(theta)*radius};
   if(!this.map.collides(candidate.x,candidate.z,1.3)){final=candidate;break}
  }
  s.downedUntil=0;s.downedBy=null;s.rescueCalled=false;s.altitude=0;s.parachuting=false;s.aiAmmo=WEAPONS[s.weaponId].magazine;s.nextEquipment=this.elapsed+8;s.pos=final;s.lastProgress={...final};s.yaw=s.team==='blue'?0:Math.PI;s.hp=100;s.alive=true;s.target=null;s.route=[];s.routeFailed=false;s.nextPath=0;
  s.state='SPAWN';s.spawnGraceUntil=this.elapsed+1.15;s.lastSeen=null;s.lastSeenAt=-999;s.suppression=0;s.morale=1;s.cover=null;
  s.combatUntil=0;s.velocity={x:0,z:0};s.visualTarget=null;s.burstLeft=0;s.reactionUntil=this.elapsed+.6;
  s.lastAiAt=this.elapsed;s.nextAiAt=this.elapsed+this.random.between(.02,.16);s.stuckFor=0;s.aiIntent=null;
 }

 getSquadMembers(){return this.soldiers.filter(s=>s.team===this.player.team&&s.squad===this.player.squad&&(this.settings.aiEnabled!==false||s.player)).slice(0,4);}
 private spawnThreat(at:Point):boolean{
  return this.soldiers.some(e=>e.alive&&e.team!==this.player.team&&dist(e.pos,at)<65&&(dist(e.pos,at)<30||!this.sightBlocked(e.pos,at)))||this.vehicles.some(e=>e.alive&&e.team!==this.player.team&&dist(e.pos,at)<100&&!this.sightBlocked(e.pos,at));
 }
 getDeploymentLocations():{id:string;name:string;at:Point;available:boolean;reason:string;kind:'base'|'point'|'squad'|'beacon'|'vehicle'}[]{
  const result:{id:string;name:string;at:Point;available:boolean;reason:string;kind:'base'|'point'|'squad'|'beacon'|'vehicle'}[]=[{id:'BASE',name:'主基地',at:{...this.map.bases[this.player.team]},available:true,reason:'安全部署',kind:'base'}];
  for(const p of this.points){const threat=p.contested||(this.pointThreatUntil.get(p.id)??0)>this.elapsed||this.spawnThreat(p)||(this.activeMajorEvent?.kind==='ARTILLERY'&&this.activeMajorEvent.team!==this.player.team&&this.activeMajorEvent.objective===p.id)||this.supports.some(e=>e.kind==='artillery'&&dist(e.at,p)<40);const own=p.owner===this.player.team;result.push({id:p.id,name:p.id+' · '+p.zh,at:{x:p.x,z:p.z},available:own,reason:!own?'未被己方控制':threat?'交战据点 · 远距离部署':'安全部署',kind:'point'});}
  for(const member of this.soldiers.filter(m=>m.id!==this.player.id&&m.team===this.player.team&&m.squad===this.player.squad&&(this.settings.aiEnabled!==false||m.player))){const combat=member.combatUntil>this.elapsed||member.vehicleId!==null;result.push({id:'SQUAD-'+member.id,name:'队员 '+member.id+' · '+CLASSES[member.classId].zh,at:{...member.pos},available:member.alive&&!combat,reason:!member.alive?'队员阵亡':combat?'队员正在交战':'安全部署',kind:'squad'});}
  for(const v of this.vehicles.filter(v=>v.team===this.player.team)){const reason=!v.alive?'载具已被击毁':v.airborne?'空投未落地':v.combatUntil>this.elapsed?'载具正在交战':v.occupants.every(id=>id!==null)?'座位已满':'安全座位';const available=v.alive&&!v.airborne&&v.combatUntil<=this.elapsed;const seats=v.occupants.map((id,seat)=>({id,seat})).filter(x=>x.id===null&&(x.seat!==0||v.reservedFor===null||v.reservedFor===this.player.id));if(!seats.length)result.push({id:'VEHICLE-'+v.id,name:VEHICLE_TYPES[v.kind].name,at:{...v.pos},available:false,reason:reason==='安全座位'?'驾驶位保留给召唤者':reason,kind:'vehicle'});for(const {seat} of seats)result.push({id:`VEHICLE-${v.id}-${seat}`,name:VEHICLE_TYPES[v.kind].name+' · '+['驾驶位','机枪位','成员位 1','成员位 2'][seat],at:{...v.pos},available,reason,kind:'vehicle'});}
  if(this.beacon){const safe=this.beacon.expires>this.elapsed&&!this.spawnThreat(this.beacon.at);result.push({id:'BEACON',name:'侦察信标',at:{...this.beacon.at},available:safe,reason:safe?'一次性部署':'信标附近正在交战',kind:'beacon'});}
  return result;
 }
 respawnPlayer(pointId='BASE'):boolean{
  const s=this.player;if(s.alive||this.finished||this.elapsed<s.respawnAt)return false;
  // Re-check at click time; UI colour alone is never authority to spawn.
  const location=this.getDeploymentLocations().find(l=>l.id===pointId&&l.available);if(!location)return false;
  if(location.kind==='vehicle'){const [,id,seat]=location.id.split('-').map(Number),v=this.vehicles.find(v=>v.id===id);if(!v||seat===undefined||v.occupants[seat]!==null||!v.alive||v.airborne||v.combatUntil>this.elapsed)return false;this.placeAtSpawn(s,true);this.resetPlayerLoadout();v.occupants[seat]=s.id;s.vehicleId=v.id;s.pos={...v.pos};this.playerVehicleId=v.id;this.playerSeat=seat;if(seat===0){v.driver='player';v.speed=0;}this.emit({type:'respawn',id:s.id});this.emit({type:'vehicleEnter',id:v.id});return true;}
  const remote=location.kind==='point'&&location.reason.includes('远距离');const candidates=location.kind==='base'?[location.at]:Array.from({length:72},(_,i)=>{const a=i*Math.PI/12,r=location.kind==='squad'?3+Math.floor(i/24)*2:remote?42+Math.floor(i/24)*20:6+Math.floor(i/24)*12;return {x:location.at.x+Math.sin(a)*r,z:location.at.z+Math.cos(a)*r}});
  const at=candidates.find(q=>!this.map.collides(q.x,q.z,1.3)&&!this.vehicles.some(v=>v.alive&&dist(v.pos,q)<VEHICLE_TYPES[v.kind].radius+1)&&!this.soldiers.some(other=>other.alive&&!other.player&&dist(other.pos,q)<1.5)&&(location.kind==='base'||location.kind==='squad'||!this.spawnThreat(q)));
  if(!at)return false;this.placeAtSpawn(s,true);s.pos={...at};s.lastProgress={...at};if(location.kind==='beacon')this.beacon=null;if(location.kind==='squad')this.awardRP(10,'squad deployment');this.resetPlayerLoadout();this.emit({type:'respawn',id:s.id});return true;
 }
 setLoadout(value:unknown):boolean {
  if(this.elapsed>0&&this.player.alive)return false;
  this.loadout=validateLoadout(value);this.player.classId=this.loadout.classId;this.resetPlayerLoadout();return true;
 }
 private resetPlayerLoadout(){
  this.playerWeapon=this.loadout.primary;
  for(const id of WEAPON_ORDER){this.ammo[id]=WEAPONS[id].magazine;this.reserve[id]=WEAPONS[id].reserve}
  this.playerReloadUntil=0;this.reloadingWeapon=null;this.playerNextShot=0;this.grenadeCount=2;this.gadgetCharges=2;this.rocketCount=2;this.aaCount=2;this.aaUntil=0;this.rocketUntil=0;this.slideUntil=0;this.slideCooldown=0;this.playerThrowUntil=0;this.playerGadgetUntil=0;
 }
 switchPlayerWeapon(id:WeaponId):boolean{
  if(this.finished||!this.player.alive||!([this.loadout.primary,this.loadout.secondary] as WeaponId[]).includes(id)||id===this.playerWeapon)return false;
  this.playerWeapon=id;this.playerReloadUntil=0;this.reloadingWeapon=null;this.playerNextShot=Math.max(this.playerNextShot,this.elapsed+.22);return true;
 }
 movePlayer(x:number,z:number){
  const s=this.player;if(!s.alive||this.finished)return;
  if(!this.map.collides(s.pos.x+x,s.pos.z,1.05))s.pos.x+=x;
  if(!this.map.collides(s.pos.x,s.pos.z+z,1.05))s.pos.z+=z;
 }

 get nearbyVehicle():ArmoredVehicle|null {
  return this.vehicles.filter(v=>!v.airborne&&v.altitude<3&&v.alive&&v.team===this.player.team&&(v.driver!=='player'||v.occupants.some(id=>id===null))&&dist(this.player.pos,v.pos)<VEHICLE_TYPES[v.kind].radius+8&&!this.map.lineBlocked(this.player.pos,v.pos)).sort((a,b)=>dist(this.player.pos,a.pos)-dist(this.player.pos,b.pos))[0]??null;
 }
 togglePlayerVehicle():boolean{
  const p=this.player;if(!p.alive||this.finished)return false;
  const current=this.playerVehicle;
  if(current){
   const r=VEHICLE_TYPES[current.kind].radius+2.2;
   const out=Array.from({length:16},(_,i)=>{const a=current.yaw+i*Math.PI/8;return {x:current.pos.x+Math.sin(a)*r,z:current.pos.z+Math.cos(a)*r}}).find(q=>!this.map.collides(q.x,q.z,1.05)&&!this.vehicles.some(v=>v!==current&&v.alive&&dist(q,v.pos)<VEHICLE_TYPES[v.kind].radius+1));
   if(!out)return false;
   p.pos={...out};p.altitude=current.altitude;p.parachuting=current.altitude>2;p.vehicleId=null;current.occupants[this.playerSeat]=null;current.driver=current.occupants[0]===null?null:'ai';current.nextDecision=this.elapsed+10;if(current.kind!=='helicopter'&&current.kind!=='jet')current.speed=0;this.playerVehicleId=null;this.playerSeat=0;this.emit({type:'vehicleExit',id:current.id});return true;
  }
  const best=this.nearbyVehicle;if(!best)return false;
  const driver=this.soldiers.find(s=>s.id===best.occupants[0]);const seat=driver?.player?best.occupants.findIndex((id,i)=>i>0&&id===null):0;if(seat<0||!this.displaceSeat(best,seat))return false;best.occupants[seat]=p.id;p.vehicleId=best.id;if(seat===0){best.driver='player';best.speed=0;best.steering=0;}this.playerVehicleId=best.id;this.playerSeat=seat;p.pos={...best.pos};p.altitude=0;p.parachuting=false;p.route=[];this.slideUntil=0;this.emit({type:'vehicleEnter',id:best.id});return true;
 }
 private displaceSeat(v:ArmoredVehicle,seat:number):boolean{
  const id=v.occupants[seat];if(id===null||id===undefined)return true;const s=this.soldiers.find(s=>s.id===id);if(s?.player&&s.id!==this.player.id)return false;if(!s){v.occupants[seat]=null;return true;}
  const radius=VEHICLE_TYPES[v.kind].radius+2.2;
  const out=Array.from({length:32},(_,i)=>{const a=i*Math.PI/8,r=radius+Math.floor(i/16)*3;return {x:v.pos.x+Math.sin(a)*r,z:v.pos.z+Math.cos(a)*r}}).find(p=>!this.map.collides(p.x,p.z,1.05)&&!this.vehicles.some(o=>o!==v&&o.alive&&!o.airborne&&dist(p,o.pos)<VEHICLE_TYPES[o.kind].radius+1));
  if(!out&&s.alive)return false;s.pos=out??{...v.pos};s.vehicleId=null;s.boardingUntil=this.elapsed+15;s.state='IDLE';s.route=[];s.nextAiAt=this.elapsed+.2;v.occupants[seat]=null;return true;
 }
 switchVehicleSeat(seat:number):boolean{
  const v=this.playerVehicle;if(!this.player.alive||!v?.alive||this.finished||!Number.isInteger(seat)||seat<0||seat>=v.seatCount)return false;
  if(seat===this.playerSeat)return true;if(!this.displaceSeat(v,seat))return false;v.occupants[this.playerSeat]=null;v.occupants[seat]=this.player.id;this.playerSeat=seat;v.driver=seat===0?'player':v.occupants[0]===null?null:'ai';v.speed=0;v.steering=0;return true;
 }
 drivePlayerVehicle(throttle:number,steer:number,dt:number,viewYaw?:number,brake=false,viewPitch=0,boost=false){
  const v=this.playerVehicle;if(!v||!v.alive||v.airborne||v.occupants[0]!==this.player.id||this.finished||this.playerSeat!==0)return;
  const cfg=VEHICLE_TYPES[v.kind];if(v.kind==='helicopter'||v.kind==='jet'){this.flyVehicle(v,throttle,steer,dt,viewYaw,viewPitch,boost,brake);this.player.pos={...v.pos};return;}dt=clamp(dt,0,.5);throttle=clamp(throttle,-1,1);steer=clamp(steer,-1,1);
  // Assisted WASD: steer toward the requested direction relative to the view.
  // Arrow keys use the traditional throttle / steering path (no viewYaw).
  if(viewYaw!==undefined&&(throttle||steer)){
   const x=Math.cos(viewYaw)*steer-Math.sin(viewYaw)*throttle,z=-Math.sin(viewYaw)*steer-Math.cos(viewYaw)*throttle;
   const reverse=throttle<0&&steer===0;
   const desired=Math.atan2(x,z)+(reverse?Math.PI:0),delta=Math.atan2(Math.sin(desired-v.yaw),Math.cos(desired-v.yaw));
   steer=clamp(delta*2.5,-1,1);throttle=(reverse?-1:1)*Math.max(.25,Math.cos(delta));
  }
  const target=brake?0:throttle*(throttle>=0?cfg.maxForward:cfg.maxReverse);
  const rate=brake?cfg.braking*3:Math.abs(target)<Math.abs(v.speed)?cfg.braking:cfg.acceleration;
  v.speed+=clamp(target-v.speed,-rate*dt,rate*dt);
  v.steering+=(steer-v.steering)*(1-Math.exp(-dt*8));
  const steerScale=viewYaw!==undefined?.8:(v.kind==='tank'||v.kind==='ifv'?.65:Math.min(1,Math.abs(v.speed)/4));
  v.yaw+=v.steering*cfg.turnRate*steerScale*dt*(v.speed<-.1&&viewYaw===undefined?-1:1);
  let moved=0;const steps=Math.max(1,Math.ceil(Math.abs(v.speed)*dt/.6));
  for(let i=0;i<steps;i++){const dx=Math.sin(v.yaw)*v.speed*dt/steps,dz=Math.cos(v.yaw)*v.speed*dt/steps;
   const clear=(x:number,z:number)=>!this.map.collides(x,z,cfg.radius)&&!this.vehicles.some(other=>other!==v&&other.alive&&dist({x,z},other.pos)<cfg.radius+VEHICLE_TYPES[other.kind].radius);
   if(clear(v.pos.x+dx,v.pos.z+dz)){v.pos.x+=dx;v.pos.z+=dz;moved++;}else{v.speed*=.25;break;}
  }
  if(!moved&&Math.abs(v.speed)<.05)v.speed=0;this.player.pos={...v.pos};
 }
 changeAltitude(direction:number,dt:number){const v=this.playerVehicle;if(v&&this.playerSeat===0&&(v.kind==='helicopter'||v.kind==='jet'))v.climbInput=clamp(direction,-1,1);}
 private flyVehicle(v:ArmoredVehicle,throttle:number,steer:number,dt:number,viewYaw?:number,pitch=0,boost=false,brake=false){
  if(v.driver==='ai'){v.climbInput=clamp((55-v.altitude)/12,-1,1);pitch=v.kind==='jet'?clamp((55-v.altitude)/70,-.3,.5):0;}
  const move=controlFlight(v,throttle,steer,dt,viewYaw,pitch,boost,brake),oldGround=this.map.heightAt(v.pos.x,v.pos.z),q={x:clamp(v.pos.x+move.x,-this.map.limit,this.map.limit),z:clamp(v.pos.z+move.z,-this.map.limit,this.map.limit)};
  if(v.altitude>28||!this.map.collides(q.x,q.z,VEHICLE_TYPES[v.kind].radius)){v.pos=q;v.altitude+=oldGround-this.map.heightAt(q.x,q.z);}else v.speed=0;
 }
 private updateFlightPhysics(v:ArmoredVehicle,dt:number,powered:boolean){
  if(v.kind!=='helicopter'&&v.kind!=='jet')return;
  const weight=1-Math.exp(-dt*(powered?3:1.6));v.enginePower+=((powered?1:0)-v.enginePower)*weight;
  const oldAltitude=v.altitude;
  if(!powered){const oldGround=this.map.heightAt(v.pos.x,v.pos.z);const q={x:clamp(v.pos.x+Math.sin(v.yaw)*v.speed*dt,-this.map.limit,this.map.limit),z:clamp(v.pos.z+Math.cos(v.yaw)*v.speed*dt,-this.map.limit,this.map.limit)};if(v.altitude>28||!this.map.collides(q.x,q.z,VEHICLE_TYPES[v.kind].radius)){v.pos=q;v.altitude+=oldGround-this.map.heightAt(q.x,q.z);}else v.speed*=.2;v.speed*=Math.exp(-dt*(v.kind==='jet'?.14:.5));}
  const lift=v.kind==='helicopter'?v.enginePower:Math.min(1,Math.abs(v.speed)/32)*v.enginePower;
  if(powered&&lift>.95){const target=v.kind==='jet'?Math.sin(v.flightPitch)*v.speed:v.climbInput*16;v.verticalVelocity+=(target-v.verticalVelocity)*(1-Math.exp(-dt*4));}
  else v.verticalVelocity-=9.81*(powered?1-lift:1)*dt;
  v.verticalVelocity*=Math.exp(-dt*.06);v.altitude=Math.min(130,v.altitude+v.verticalVelocity*dt);
  const obstacle=this.map.blocks.some(b=>Math.abs(v.pos.x-b.x)<b.w/2+1&&Math.abs(v.pos.z-b.z)<b.d/2+1&&v.altitude<b.h&&oldAltitude>0);
  if(obstacle){v.speed=0;v.verticalVelocity=0;v.altitude=oldAltitude;return;}
  if(v.altitude<=0){const impact=Math.abs(v.verticalVelocity);v.altitude=0;v.verticalVelocity=0;void impact;if(!powered)v.speed=0;}
 }
 aimPlayerVehicle(yaw:number,pitch=0,dt=1/60){const v=this.playerVehicle;if(!v||this.playerSeat>1)return;this.aimTurret(v,yaw+Math.PI,pitch,dt,this.playerSeat===1||v.kind==='scout');}
 private aimTurret(v:ArmoredVehicle,yaw:number,pitch:number,dt:number,mg=false){
  const limit=v.kind==='jet'?.18:v.kind==='helicopter'?.65:v.kind==='scout'?1.9:v.kind==='transport'?2.5:Math.PI;
  const relative=Math.atan2(Math.sin(yaw-v.yaw),Math.cos(yaw-v.yaw)),wanted=v.yaw+clamp(relative,-limit,limit),current=mg?v.mgYaw:v.turretYaw;
  const delta=Math.atan2(Math.sin(wanted-current),Math.cos(wanted-current)),rate=mg?2.8:v.kind==='tank'?1.2:1.9;
  const result=current+clamp(delta,-rate*dt,rate*dt);if(mg)v.mgYaw=result;else v.turretYaw=result;
  v.turretPitch+=clamp(clamp(pitch,v.kind==='aa'?-.12:v.kind==='helicopter'||v.kind==='jet'?-.8:-.18,v.kind==='aa'?1.35:.55)-v.turretPitch,-dt,dt);
 }
 shootPlayerVehicle(_direction:{x:number;y:number;z:number}):boolean{
  const v=this.playerVehicle;if(!v||v.kind==='motorcycle'||!v.alive||v.airborne||!this.player.alive||this.finished||this.playerSeat>1)return false;
  const mg=this.playerSeat===1||v.kind==='scout';if(this.playerSeat===0&&v.kind==='transport')return false;
  if(!mg&&v.weaponSlot===1){if(['aa','helicopter','jet'].includes(v.kind))return this.fireVehicleMissile(v,true);if(v.kind==='ifv'||v.kind==='tank'){if(this.elapsed<v.nextSecondary)return false;v.nextSecondary=this.elapsed+.16;this.fireVehicleWeapon(v,_direction,true,true);return true;}}
  if(this.elapsed<(mg?v.nextMGShot:v.nextShot))return false;
  if(mg)v.nextMGShot=this.elapsed+.12;else v.nextShot=this.elapsed+VEHICLE_TYPES[v.kind].weaponInterval;
  const yaw=mg?v.mgYaw:v.turretYaw,p=v.turretPitch;
  this.fireVehicleWeapon(v,{x:Math.sin(yaw)*Math.cos(p),y:Math.sin(p),z:Math.cos(yaw)*Math.cos(p)},mg,true);return true;
 }
 private fireVehicleWeapon(v:ArmoredVehicle,direction:{x:number;y:number;z:number},mg:boolean,player:boolean){
  v.combatUntil=this.elapsed+8;const coax=mg&&player&&this.playerSeat===0&&v.weaponSlot===1&&['tank','ifv'].includes(v.kind),yaw=mg&&!coax?v.mgYaw:v.turretYaw,pitch=v.turretPitch;direction={x:Math.sin(yaw)*Math.cos(pitch),y:Math.sin(pitch),z:Math.cos(yaw)*Math.cos(pitch)};const muzzle=vehicleMuzzle(v,mg,coax);muzzle.y+=this.map.heightAt(v.pos.x,v.pos.z);const cfg=VEHICLE_TYPES[v.kind],origin={x:muzzle.x,z:muzzle.z},length=Math.hypot(direction.x,direction.y,direction.z)||1,dir={x:direction.x/length,y:direction.y/length,z:direction.z/length};
  if(!mg){this.launchProjectile(origin,dir,'shell',v.team,player,cfg.weaponDamage*(player?1:({easy:.45,normal:.65,hard:.85}[this.settings.difficulty])),v.kind==='tank'?8:5,cfg.weaponRange,v.id,muzzle.y);return;}
  const spread=player?.008:.022,a=(this.random.next()-.5)*spread,d=norm({x:dir.x+Math.cos(v.turretYaw)*a,z:dir.z-Math.sin(v.turretYaw)*a});
  let best=110,target:Soldier|ArmoredVehicle|null=null;
  for(const t of [...this.soldiers,...this.vehicles]){if(!t.alive||('kind' in t?t.airborne:t.vehicleId!==null)||t.team===v.team)continue;const along=(t.pos.x-origin.x)*d.x+(t.pos.z-origin.z)*d.z,lateral=Math.abs((t.pos.x-origin.x)*d.z-(t.pos.z-origin.z)*d.x);if(along>0&&along<best&&lateral<('kind' in t?VEHICLE_TYPES[t.kind].radius:1)&&!this.sightBlocked(origin,t.pos)){best=along;target=t}}
  // Elevation matters: MG cannot damage ground targets when aimed into the sky.
  if(player&&Math.abs(dir.y)>.25)target=null;
  const to=target?.pos??{x:origin.x+d.x*best,z:origin.z+d.z*best};this.emit({type:'vehicleShot',team:v.team,from:origin,to:{...to},hit:!!target,player,owner:player?this.player.id:undefined,weapon:'mg',muzzle});
  if(target){if('kind' in target)this.damageVehicle(target,target.kind==='tank'?2:7,v.team,player,v.pos);else this.damage(target,player?24:18,player?this.player:this.soldiers.find(s=>s.id===v.occupants[1]||s.id===v.occupants[0])??this.player)}
 }
 private launchProjectile(origin:Point,direction:{x:number;y:number;z:number},kind:'shell'|'rocket',team:Team,player:boolean,damage:number,radius:number,range:number,source:number|null,muzzleHeight?:number,attackerId?:number){
  if(this.projectiles.length>=96)return;
  const ammo=source===null?null:AMMUNITION[this.vehicles[source]!.kind],speed=ammo?.speed??75;
  this.projectiles.push({id:this.projectileSequence++,kind,team,player,pos:{x:origin.x,y:muzzleHeight??this.map.heightAt(origin.x,origin.z)+(kind==='rocket'?1.65:3.1),z:origin.z},velocity:{x:direction.x*speed,y:direction.y*speed,z:direction.z*speed},damage,radius:ammo?.radius??radius,gravity:ammo?.gravity??2,armor:ammo?.armor??1,remaining:range,source,attackerId:attackerId??(player?this.player.id:source!==null?this.vehicles[source]?.occupants[0]??undefined:undefined)});
  const shot=this.projectiles[this.projectiles.length-1]!;if(source!==null&&this.vehicles[source]!.ammoMode==='he'&&kind==='shell'){shot.damage*=.85;shot.radius=Math.max(shot.radius,8);shot.armor*=.38;}
  this.emit({type:'vehicleShot',team,from:{...origin},to:{x:origin.x+direction.x*range,z:origin.z+direction.z*range},hit:false,player,owner:player?this.player.id:undefined,weapon:kind,muzzle:{x:origin.x,y:muzzleHeight??this.map.heightAt(origin.x,origin.z)+(kind==='rocket'?1.65:3.1),z:origin.z}});
 }
 private updateProjectiles(dt:number){
  for(let i=this.projectiles.length-1;i>=0;i--){const p=this.projectiles[i]!,steps=Math.max(1,Math.ceil(Math.hypot(p.velocity.x,p.velocity.y,p.velocity.z)*dt/.8));let impact=false;let direct:Soldier|ArmoredVehicle|null=null;
   for(let n=0;n<steps&&!impact;n++){
    const step=dt/steps;if(p.targetId!==undefined){const target=this.vehicles.find(v=>v.id===p.targetId&&v.alive);if(target&&target.flareUntil>this.elapsed){delete p.targetId;}else if(target){const delta={x:target.pos.x-p.pos.x,y:this.map.heightAt(target.pos.x,target.pos.z)+target.altitude+1.7-p.pos.y,z:target.pos.z-p.pos.z};if(!guideMissile(p.velocity,delta,p.guidedSpeed??90,step))delete p.targetId;else target.incomingUntil=this.elapsed+.25;}}p.velocity.y-=p.gravity*step;p.pos.x+=p.velocity.x*step;p.pos.y+=p.velocity.y*step;p.pos.z+=p.velocity.z*step;p.remaining-=Math.hypot(p.velocity.x,p.velocity.y,p.velocity.z)*step;
    const ground=this.map.heightAt(p.pos.x,p.pos.z);
    impact=p.pos.y<=ground+.1||this.map.blocks.some(b=>Math.abs(p.pos.x-b.x)<b.w/2+.12&&Math.abs(p.pos.z-b.z)<b.d/2+.12&&p.pos.y<ground+b.h);
    if(!impact)for(const t of [...this.vehicles,...this.soldiers]){if(!t.alive||('kind' in t?t.airborne:t.vehicleId!==null)||t.team===p.team||('kind' in t&&t.id===p.source))continue;const r='kind' in t?VEHICLE_TYPES[t.kind].radius:.7,h='kind' in t?3.6:1.9,base=this.map.heightAt(t.pos.x,t.pos.z)+('kind' in t?t.altitude:0);if(dist(p.pos,t.pos)<r&&p.pos.y<base+h&&p.pos.y>base){impact=true;direct=t;break;}}
   }
   if(impact){const at={x:p.pos.x,z:p.pos.z};for(const point of this.points)if(point.owner!==p.team&&dist(point,at)<35)this.pointThreatUntil.set(point.id,this.elapsed+8);this.emit({type:'projectileImpact',team:p.team,at,radius:p.radius,kind:p.kind});const attacker=this.soldiers.find(s=>p.attackerId!==undefined?s.id===p.attackerId:s.team===p.team&&!s.player)??this.player;
    for(const t of [...this.soldiers,...this.vehicles]){if(!t.alive||('kind' in t?t.airborne:t.vehicleId!==null)||t.team===p.team)continue;const d=Math.hypot(dist(t.pos,at),('kind' in t?t.altitude+this.map.heightAt(t.pos.x,t.pos.z):this.map.heightAt(t.pos.x,t.pos.z))-p.pos.y);if(t!==direct&&(d>=p.radius||this.map.lineBlocked(at,t.pos)))continue;const amount=t===direct?p.damage:p.damage*.65*(1-d/p.radius);if('kind' in t)this.damageVehicle(t,amount*p.armor,p.team,p.player,p.pos,p.attackerId);else this.damage(t,amount,attacker);}
   }
   if(impact||p.remaining<=0)this.projectiles.splice(i,1);
  }
 }
 getAATarget(direction:{x:number;y:number;z:number}):ArmoredVehicle|null {
  if(this.inVehicle||this.loadout.classId!=='engineer')return null;const len=Math.hypot(direction.x,direction.y,direction.z)||1;
  const eye={x:this.player.pos.x,y:this.map.heightAt(this.player.pos.x,this.player.pos.z)+1.7,z:this.player.pos.z};
  return this.vehicles.filter(v=>{if(!v.alive||v.airborne||v.flareUntil>this.elapsed||v.team===this.player.team||v.altitude<5)return false;const dx=v.pos.x-eye.x,dz=v.pos.z-eye.z,dy=v.altitude+this.map.heightAt(v.pos.x,v.pos.z)+1.7-eye.y,d=Math.hypot(dx,dy,dz);if(d>320||(dx*direction.x+dy*direction.y+dz*direction.z)/d/len<.9)return false;for(let t=.05;t<1;t+=.05){const x=eye.x+dx*t,z=eye.z+dz*t,y=eye.y+dy*t;if(this.map.blocks.some(b=>Math.abs(x-b.x)<b.w/2&&Math.abs(z-b.z)<b.d/2&&y<this.map.heightAt(x,z)+b.h))return false;}return true;}).sort((a,b)=>dist(a.pos,eye)-dist(b.pos,eye))[0]??null;
 }
 private equipmentOrigin(muzzle?:{x:number;y:number;z:number}){const p=this.player.pos,eye={x:p.x,y:this.map.heightAt(p.x,p.z)+1.65,z:p.z};return muzzle&&Object.values(muzzle).every(Number.isFinite)&&Math.hypot(muzzle.x-eye.x,muzzle.y-eye.y,muzzle.z-eye.z)<3?muzzle:eye;}
 warnAATarget(direction:{x:number;y:number;z:number}){if(!this.player.alive||!this.aaCount||this.aaUntil>this.elapsed)return;const target=this.getAATarget(direction);if(target)target.warningUntil=this.elapsed+.25;}
 fireAA(direction:{x:number;y:number;z:number},muzzle?:{x:number;y:number;z:number}):boolean {
  if(!this.player.alive||this.finished||this.inVehicle||this.projectiles.length>=96||!this.aaCount||this.elapsed<this.aaUntil)return false;const target=this.getAATarget(direction);if(!target)return false;
  this.aaCount--;this.aaUntil=this.elapsed+2;const dx=target.pos.x-this.player.pos.x,dz=target.pos.z-this.player.pos.z,dy=target.altitude+this.map.heightAt(target.pos.x,target.pos.z)-this.map.heightAt(this.player.pos.x,this.player.pos.z),len=Math.hypot(dx,dy,dz);
  const origin=this.equipmentOrigin(muzzle);this.launchProjectile(origin,{x:dx/len,y:dy/len,z:dz/len},'rocket',this.player.team,true,135,5,360,null,origin.y);this.projectiles[this.projectiles.length-1]!.targetId=target.id;target.incomingUntil=this.elapsed+.25;return true;
 }
 fireRocket(direction:{x:number;y:number;z:number},muzzle?:{x:number;y:number;z:number}):boolean{
  if(!this.player.alive||this.finished||this.inVehicle||this.loadout.classId!=='engineer'||this.projectiles.length>=96||!this.rocketCount||this.elapsed<this.rocketUntil||this.isReloading()||this.elapsed<this.playerThrowUntil)return false;
  const length=Math.hypot(direction.x,direction.y,direction.z);if(!Number.isFinite(length)||length<.01)return false;
  this.rocketCount--;this.rocketUntil=this.elapsed+2.5;this.playerNextShot=Math.max(this.playerNextShot,this.rocketUntil);
  const origin=this.equipmentOrigin(muzzle);this.launchProjectile(origin,{x:direction.x/length,y:direction.y/length,z:direction.z/length},'rocket',this.player.team,true,230,5,180,null,origin.y);return true;
 }
 private unstuckUntil=0;
 unstuckPlayer():boolean {
  if((!this.player.alive&&!this.player.downedUntil)||this.finished||this.elapsed<this.unstuckUntil)return false;this.unstuckUntil=this.elapsed+20;this.eliminate(this.player,null);return true;
 }
 get sliding(){return this.elapsed<this.slideUntil}
 startSlide(direction:Point,speed:number):boolean{
  if(!this.player.alive||this.finished||this.inVehicle||this.sliding||this.elapsed<this.slideCooldown||speed<6||!Number.isFinite(speed)||!Number.isFinite(direction.x)||!Number.isFinite(direction.z)||Math.hypot(direction.x,direction.z)<.1)return false;
  this.slideDirection=norm(direction);this.slideUntil=this.elapsed+.7;this.slideCooldown=this.elapsed+2.2;return true;
 }
 startReload(){
  const def=this.activeWeapon,weapon=this.playerWeapon;
  if(!this.player.alive||this.ammo[weapon]>=def.magazine||this.reserve[weapon]<=0||this.playerReloadUntil>this.elapsed)return;
  this.reloadingWeapon=weapon;this.playerReloadUntil=this.elapsed+def.reload;
 }
 isReloading(){return this.playerReloadUntil>this.elapsed&&this.reloadingWeapon===this.playerWeapon}

 shootPlayer(direction:{x:number;y:number;z:number},eyeHeight=1.78,muzzle?:{x:number;y:number;z:number}):boolean{
  const p=this.player,def=this.activeWeapon,weapon=this.playerWeapon;
  if(!p.alive||this.inVehicle||this.finished||this.elapsed<this.playerThrowUntil||this.isReloading()||this.elapsed<this.playerNextShot)return false;
  if(this.ammo[weapon]<=0){this.startReload();return false}
  this.playerNextShot=this.elapsed+def.fireInterval;this.ammo[weapon]--;
  const eye={x:p.pos.x,y:this.map.heightAt(p.pos.x,p.pos.z)+eyeHeight,z:p.pos.z};
  const origin=muzzle&&Object.values(muzzle).every(Number.isFinite)&&Math.hypot(muzzle.x-eye.x,muzzle.y-eye.y,muzzle.z-eye.z)<3?{...muzzle}:eye;
  // Align a muzzle ray with the camera's aim point, preserving close-range parallax.
  let aimDistance=def.maxRange;for(const t of this.soldiers){if(!t.alive||t.vehicleId!==null||t.team===p.team)continue;const along=(t.pos.x-eye.x)*direction.x+(this.map.heightAt(t.pos.x,t.pos.z)+1.15-eye.y)*direction.y+(t.pos.z-eye.z)*direction.z;if(along>0&&along<aimDistance&&Math.hypot(eye.x+direction.x*along-t.pos.x,eye.z+direction.z*along-t.pos.z)<.8)aimDistance=along;}
  const target={x:eye.x+direction.x*aimDistance,y:eye.y+direction.y*aimDistance,z:eye.z+direction.z*aimDistance},aimLength=Math.hypot(target.x-origin.x,target.y-origin.y,target.z-origin.z)||1;direction={x:(target.x-origin.x)/aimLength,y:(target.y-origin.y)/aimLength,z:(target.z-origin.z)/aimLength};
  const spread=this.playerAiming?def.adsSpread:def.hipSpread;
  const jitter={x:this.random.between(-spread,spread),y:this.random.between(-spread,spread),z:this.random.between(-spread,spread)};
  const length=Math.hypot(direction.x+jitter.x,direction.y+jitter.y,direction.z+jitter.z)||1;
  const d={x:(direction.x+jitter.x)/length,y:(direction.y+jitter.y)/length,z:(direction.z+jitter.z)/length};
  let found:Soldier|ArmoredVehicle|null=null,best=def.maxRange;
  // A wall between eye and muzzle also stops the shot; no firing through close cover.
  for(let t=0;t<=1;t+=.1){const x=eye.x+(origin.x-eye.x)*t,z=eye.z+(origin.z-eye.z)*t,y=eye.y+(origin.y-eye.y)*t;if(this.map.blocks.some(b=>Math.abs(x-b.x)<b.w/2&&Math.abs(z-b.z)<b.d/2&&y<this.map.heightAt(x,z)+b.h))best=0;}
  for(let distance=.25;distance<best;distance+=.5){const x=origin.x+d.x*distance,z=origin.z+d.z*distance,y=origin.y+d.y*distance;if(y<this.map.heightAt(x,z)||this.map.blocks.some(b=>Math.abs(x-b.x)<b.w/2&&Math.abs(z-b.z)<b.d/2&&y<this.map.heightAt(x,z)+b.h)){best=distance;break;}}
  for(const s of this.soldiers){if(!s.alive||s.vehicleId!==null||s.team===p.team)continue;
   const center={x:s.pos.x,y:this.map.heightAt(s.pos.x,s.pos.z)+1.15,z:s.pos.z};
   const proj=(center.x-origin.x)*d.x+(center.y-origin.y)*d.y+(center.z-origin.z)*d.z;
   if(proj<0||proj>best)continue;
   const distanceSq=(origin.x+d.x*proj-center.x)**2+(origin.y+d.y*proj-center.y)**2+(origin.z+d.z*proj-center.z)**2;
   if(distanceSq<.82*.82&&!this.sightBlocked(p.pos,s.pos)){best=proj;found=s}
  }
  for(const v of this.vehicles){if(!v.alive||v.airborne||v.team===p.team)continue;const center={x:v.pos.x,y:this.map.heightAt(v.pos.x,v.pos.z)+v.altitude+1.7,z:v.pos.z},along=(center.x-origin.x)*d.x+(center.y-origin.y)*d.y+(center.z-origin.z)*d.z;if(along<0||along>best)continue;const miss=Math.hypot(origin.x+d.x*along-center.x,origin.y+d.y*along-center.y,origin.z+d.z*along-center.z);if(miss<VEHICLE_TYPES[v.kind].radius){found=v;best=along;}}
  const endpoint=found?{...found.pos}:{x:origin.x+d.x*def.maxRange,z:origin.z+d.z*def.maxRange};
  this.emit({type:'shot',team:p.team,from:{...p.pos},to:endpoint,muzzle:origin,end:{x:origin.x+d.x*best,y:origin.y+d.y*best,z:origin.z+d.z*best},hit:!!found,player:true,owner:p.id,weapon});
  if(found){const falloff=clamp(1-(best-def.effectiveRange)/(def.maxRange-def.effectiveRange+1),.64,1);if('kind' in found)this.damageVehicle(found,def.damage*(found.kind==='tank'?.04:.15),p.team,true,p.pos,p.id);else this.damage(found,def.damage*falloff,p)}
  if(this.ammo[weapon]===0)this.startReload();return true;
 }

 vehicleImpact(){const v=this.playerVehicle;if(!v||v.kind==='motorcycle')return null;const mg=this.playerSeat===1||v.kind==='scout'||v.weaponSlot===1&&!['aa','jet','helicopter'].includes(v.kind);const coax=mg&&this.playerSeat===0&&['tank','ifv'].includes(v.kind),m=vehicleMuzzle(v,mg,coax);m.y+=this.map.heightAt(v.pos.x,v.pos.z);const ammo=mg?AMMUNITION.scout:AMMUNITION[v.kind],yaw=mg&&!coax?v.mgYaw:v.turretYaw,p={...m},vel={x:Math.sin(yaw)*Math.cos(v.turretPitch)*ammo.speed,y:Math.sin(v.turretPitch)*ammo.speed,z:Math.cos(yaw)*Math.cos(v.turretPitch)*ammo.speed};for(let i=0;i<600;i++){const step=.008;vel.y-=ammo.gravity*step;p.x+=vel.x*step;p.y+=vel.y*step;p.z+=vel.z*step;if(p.y<=this.map.heightAt(p.x,p.z)||this.map.blocks.some(b=>Math.abs(p.x-b.x)<b.w/2&&Math.abs(p.z-b.z)<b.d/2&&p.y<this.map.heightAt(p.x,p.z)+b.h))return p;if(Math.hypot(p.x-m.x,p.z-m.z)>VEHICLE_TYPES[v.kind].weaponRange)return p;}return p;}
 private knockDown(s:Soldier,attacker:Soldier|null){if(s.downedUntil>0)return;s.alive=false;s.hp=0;s.state='DOWNED';s.downedUntil=this.elapsed+30;s.downedBy=attacker?.id??null;s.rescueCalled=false;s.deathAt=this.elapsed;s.respawnAt=Infinity;s.velocity={x:0,z:0};}
 callRescue(){if(this.player.downedUntil>this.elapsed)this.player.rescueCalled=true;}
 giveUp(dt:number){if(this.player.downedUntil>this.elapsed)this.player.downedUntil=Math.max(this.elapsed,this.player.downedUntil-clamp(dt,0,.15)*12);}
 selectVehicleWeapon(slot:0|1){const v=this.playerVehicle;if(v&&this.playerSeat===0&&!['transport','scout'].includes(v.kind))v.weaponSlot=slot;}
 setVehicleAmmo(mode:'armor'|'he'){const v=this.playerVehicle;if(v&&this.playerSeat===0&&['tank','ifv','aa','helicopter','jet'].includes(v.kind))v.ammoMode=mode;}
 deployFlares(v=this.playerVehicle):boolean {if(!v||!v.alive||this.elapsed<v.nextFlare)return false;if(v.kind==='motorcycle')return false;if(v.kind==='scout'){this.supports.push({id:this.supportSequence++,kind:'recon',team:v.team,owner:v.occupants[0]??this.player.id,at:{...v.pos},starts:this.elapsed,ends:this.elapsed+12,nextPulse:0,pulses:0});v.nextFlare=this.elapsed+24;return true;}if(v.kind==='transport'){const owner=v.occupants[0]??this.player.id,own=this.ammoBoxes.filter(b=>b.owner===owner);if(own.length>=2)this.ammoBoxes.splice(this.ammoBoxes.indexOf(own[0]!),1);this.ammoBoxes.push({id:this.supportSequence++,at:{...v.pos},owner,team:v.team,expires:this.elapsed+60,nextPulse:0});v.nextFlare=this.elapsed+24;return true;}v.flareUntil=this.elapsed+5;v.nextFlare=this.elapsed+24;v.lockTarget=null;v.lockProgress=0;v.warningUntil=0;v.incomingUntil=0;for(const launcher of this.vehicles)if(launcher.lockTarget===v.id){launcher.lockTarget=null;launcher.lockProgress=0;}for(const p of this.projectiles)if(p.targetId===v.id)delete p.targetId;if(['tank','ifv','aa'].includes(v.kind))this.supports.push({id:this.supportSequence++,kind:'smoke',team:v.team,owner:v.occupants[0]??0,at:{...v.pos},starts:this.elapsed,ends:this.elapsed+5,nextPulse:0,pulses:0});return true;}
 private updateVehicleLock(v:ArmoredVehicle,dt:number){if(!['aa','helicopter','jet'].includes(v.kind)||v.occupants[0]===null||(this.soldiers.find(s=>s.id===v.occupants[0])?.player&&v.weaponSlot!==1)){v.lockTarget=null;v.lockProgress=0;return;}const m=vehicleMuzzle(v),yaw=v.turretYaw,pitch=v.turretPitch,dir={x:Math.sin(yaw)*Math.cos(pitch),y:Math.sin(pitch),z:Math.cos(yaw)*Math.cos(pitch)};const target=this.vehicles.filter(t=>{if(!t.alive||t.airborne||t.team===v.team||t.flareUntil>this.elapsed||(v.kind==='aa'&&t.altitude<5))return false;const dx=t.pos.x-m.x,dz=t.pos.z-m.z,dy=this.map.heightAt(t.pos.x,t.pos.z)+t.altitude+1.7-this.map.heightAt(v.pos.x,v.pos.z)-m.y,d=Math.hypot(dx,dy,dz);if(d>(v.kind==='aa'?320:650)||d<5||(dx*dir.x+dy*dir.y+dz*dir.z)/d<.965)return false;for(let a=.05;a<1;a+=.05){const x=m.x+dx*a,z=m.z+dz*a,y=this.map.heightAt(v.pos.x,v.pos.z)+m.y+dy*a;if(this.map.blocks.some(b=>Math.abs(x-b.x)<b.w/2&&Math.abs(z-b.z)<b.d/2&&y<this.map.heightAt(x,z)+b.h))return false;}return !this.supports.some(e=>e.kind==='smoke'&&e.ends>this.elapsed&&dist(e.at,t.pos)<25&&t.altitude<15);}).sort((a,b)=>dist(a.pos,v.pos)-dist(b.pos,v.pos))[0];if(!target){v.lockTarget=null;v.lockProgress=0;return;}if(v.lockTarget!==target.id){v.lockTarget=target.id;v.lockProgress=0;}v.lockProgress=Math.min(1,v.lockProgress+dt/1.5);target.warningUntil=this.elapsed+.25;}
 private fireVehicleMissile(v:ArmoredVehicle,player:boolean):boolean {if(!v.alive||v.lockProgress<1||v.lockTarget===null||this.elapsed<v.nextSecondary||this.projectiles.length>=96)return false;const t=this.vehicles.find(t=>t.id===v.lockTarget&&t.alive&&t.flareUntil<=this.elapsed);if(!t)return false;const m=vehicleMuzzle(v);m.y+=this.map.heightAt(v.pos.x,v.pos.z);const dx=t.pos.x-m.x,dz=t.pos.z-m.z,dy=this.map.heightAt(t.pos.x,t.pos.z)+t.altitude+1.7-m.y,d=Math.hypot(dx,dy,dz);this.launchProjectile(m,{x:dx/d,y:dy/d,z:dz/d},'rocket',v.team,player,v.kind==='aa'?135:260,4,v.kind==='aa'?360:850,null,m.y,v.occupants[0]??undefined);const p=this.projectiles[this.projectiles.length-1]!;p.targetId=t.id;p.guidedSpeed=145;t.incomingUntil=this.elapsed+.25;p.gravity=0;v.nextSecondary=this.elapsed+7;v.combatUntil=this.elapsed+8;return true;}
 private updateFieldEquipment(dt:number){
  for(let i=this.grenades.length-1;i>=0;i--){const g=this.grenades[i]!,steps=Math.max(1,Math.ceil(dt*30));for(let k=0;k<steps;k++){const step=dt/steps;g.velocity.y-=9.81*step;const x=g.pos.x+g.velocity.x*step,z=g.pos.z+g.velocity.z*step,y=g.pos.y+g.velocity.y*step;if(this.map.blocks.some(b=>Math.abs(x-b.x)<b.w/2+.1&&Math.abs(z-b.z)<b.d/2+.1&&y<this.map.heightAt(x,z)+b.h)){g.velocity.x*=-.3;g.velocity.z*=-.3;}else{g.pos.x=x;g.pos.z=z;}g.pos.y=y;const floor=this.map.heightAt(g.pos.x,g.pos.z)+.12;if(g.pos.y<floor){g.pos.y=floor;g.velocity.y=Math.abs(g.velocity.y)*.25;g.velocity.x*=.65;g.velocity.z*=.65;}}
   if(this.elapsed<g.detonate)continue;const at={x:g.pos.x,z:g.pos.z};if(g.kind==='smoke')this.supports.push({id:this.supportSequence++,kind:'smoke',team:g.team,owner:g.owner,at,starts:this.elapsed,ends:this.elapsed+16,nextPulse:0,pulses:0});else{this.emit({type:'grenade',at,radius:12});for(const s of this.soldiers)if(s.alive&&s.team!==g.team&&Math.hypot(dist(s.pos,at),g.pos.y-this.map.heightAt(s.pos.x,s.pos.z)-1)<12&&!this.map.lineBlocked(at,s.pos))this.damage(s,130*(1-Math.hypot(dist(s.pos,at),g.pos.y-this.map.heightAt(s.pos.x,s.pos.z)-1)/12),this.soldiers[g.owner]!);for(const v of this.vehicles)if(v.alive&&v.team!==g.team&&dist(v.pos,at)<15)this.damageVehicle(v,75*(1-dist(v.pos,at)/15),g.team,true,at,g.owner);}this.grenades.splice(i,1);
  }
  for(let i=this.ammoBoxes.length-1;i>=0;i--){const b=this.ammoBoxes[i]!;if(b.expires<=this.elapsed){this.ammoBoxes.splice(i,1);continue;}if(this.elapsed<b.nextPulse)continue;b.nextPulse=this.elapsed+1;for(const s of this.soldiers)if(s.alive&&!s.player&&s.team===b.team&&dist(s.pos,b.at)<7&&!this.map.lineBlocked(s.pos,b.at))s.aiAmmo=WEAPONS[s.weaponId].magazine;}
 }

 private respawnDelay(team:Team){return this.eventForTeam(team,'COUNTER_OFFENSIVE')?4.4:7}
 private eliminate(victim:Soldier,killer:Soldier|null){
  if(!victim.alive&&!victim.downedUntil&&victim.state==='RESPAWN')return;
  const contribution=this.contributions.get(victim.id);this.contributions.delete(victim.id);
  if(killer?.player){this.awardRP(RP_REWARDS.kill,'kill',killer.id);if(this.points.some(p=>p.owner===killer.team&&dist(p,victim.pos)<25))this.awardRP(RP_REWARDS.defense,'defense',killer.id)}
  else if(contribution&&contribution.damage>=20&&this.elapsed-contribution.at<10)this.awardRP(RP_REWARDS.assist,'assist',contribution.owner);
  victim.downedUntil=0;victim.rescueCalled=false;victim.hp=0;if(victim.vehicleId!==null){const v=this.vehicles.find(v=>v.id===victim.vehicleId);if(v){v.occupants=v.occupants.map(id=>id===victim.id?null:id);if(v.occupants[0]===null){v.driver=null;v.speed=0;}}victim.vehicleId=null;if(victim.id===this.player.id)this.playerVehicleId=null;}victim.alive=false;victim.state='RESPAWN';victim.deaths++;victim.deathAt=this.elapsed;victim.respawnAt=this.elapsed+(victim.player?4.5:this.respawnDelay(victim.team));if(killer)killer.kills++;
  if(this.settings.mode!=='breakthrough'||victim.team==='blue')this.tickets[victim.team]=Math.max(0,this.tickets[victim.team]-(this.settings.killTicketPenalty??1));this.emit({type:'death',victim:victim.id,killer:killer?.id??null,team:victim.team});this.checkFinish();
 }
 private damage(victim:Soldier,amount:number,attacker:Soldier){
  if(!victim.alive||victim.vehicleId!==null)return;amount=Math.min(victim.hp,Math.max(0,amount));if(attacker.player&&victim.team!==attacker.team)this.emit({type:'hitConfirmed',owner:attacker.id,target:victim.id,vehicle:false,amount,killed:amount>=victim.hp,at:{...victim.pos}});victim.combatUntil=this.elapsed+8;if(victim.player)this.emit({type:'playerHit',amount,from:{...attacker.pos},victim:victim.id});if(attacker.player&&victim.team!==attacker.team){const old=this.contributions.get(victim.id);this.contributions.set(victim.id,{damage:(old?.owner===attacker.id?old.damage:0)+amount,at:this.elapsed,owner:attacker.id})}victim.hp=Math.max(0,victim.hp-amount);victim.alertUntil=this.elapsed+4;victim.target=attacker.id;victim.lastSeen={...attacker.pos};victim.lastSeenAt=this.elapsed;
  victim.suppression=clamp(victim.suppression+.28,0,1);victim.morale=clamp(victim.morale-.12,0,1);if(victim.hp<=0)this.knockDown(victim,attacker)
 }
 private damageFromEvent(victim:Soldier,amount:number,attackerTeam:Team,origin:Point){
  if(!victim.alive||victim.team===attackerTeam)return;victim.combatUntil=this.elapsed+8;if(victim.player)this.emit({type:'playerHit',amount,from:{...origin},victim:victim.id});victim.hp=Math.max(0,victim.hp-amount);victim.alertUntil=this.elapsed+5;victim.target=null;victim.lastSeen={...origin};victim.lastSeenAt=this.elapsed;victim.suppression=clamp(victim.suppression+.55,0,1);victim.morale=clamp(victim.morale-.22,0,1);if(victim.hp<=0)this.knockDown(victim,null)
 }

 private localStrength(team:Team,at:Point,radius=34):number{
  let value=0;for(const s of this.soldiers)if(s.alive&&s.team===team&&dist(s.pos,at)<radius)value+=1;
  for(const v of this.vehicles)if(v.alive&&v.team===team&&dist(v.pos,at)<radius*1.3)value+=3;return value;
 }
 private objectiveApproach(team:Team,squad:number,p:CapturePoint,intent:SquadIntent):Point{
  if(intent==='DEFEND'||intent==='REINFORCE'){
   let best:Point|null=null,bestD=Infinity;for(const c of this.map.cover){const d=dist(c,p);if(d<8||d>29||this.map.collides(c.x,c.z,1.2))continue;if(d<bestD){bestD=d;best=c}}
   if(best)return {...best};
  }
  const base=this.map.bases[team],toward=norm({x:p.x-base.x,z:p.z-base.z}),side={x:toward.z,z:-toward.x};
  const sign=squad%2===0?1:-1;const lateral=intent==='FLANK'?(32+(squad%3)*8)*sign:(squad%3-1)*8;
  const back=intent==='FLANK'?14:6;
  const q={x:p.x-side.x*lateral-toward.x*back,z:p.z-side.z*lateral-toward.z*back};
  return this.map.collides(q.x,q.z,2)?{x:p.x-side.x*lateral*.55,z:p.z-side.z*lateral*.55}:q;
 }
 private eventForTeam(team:Team,kind?:MajorEventKind){const e=this.activeMajorEvent;return e&&e.team===team&&(!kind||e.kind===kind)?e:null}
 private assignSquads(){
  for(const s of this.soldiers)s.squadLeader=false;
  for(const team of ['blue','red'] as const){
   const groups=Array.from(new Set(this.soldiers.filter(s=>s.team===team).map(s=>s.squad))).map(squad=>{const all=this.soldiers.filter(s=>s.team===team&&s.squad===squad),alive=all.filter(s=>s.alive),previousLeader=this.squadOrders.get(SQUAD_KEY(team,squad))?.leaderId,leader=alive.find(s=>s.id===previousLeader)??alive.find(s=>!s.player)??alive[0]??all[0]!;return {squad,leader:leader.id,at:{...leader.pos},members:alive.length,health:alive.reduce((n,s)=>n+s.hp/100,0)/Math.max(1,alive.length),blocked:alive.some(s=>s.stuckFor>3)||this.intelligence[team].snapshot(this.elapsed).some(r=>r.kind==='NAVIGATION_BLOCKED'&&r.squad===squad&&r.confidenceNow>.5),human:all.some(s=>s.player)};});
   const reports=this.intelligence[team].snapshot(this.elapsed),commander=this.commanders[team];
   commander.evaluate(this.elapsed,this.combatObjectives.map(p=>({id:p.id,x:p.x,z:p.z})),reports,groups,this.tickets[team],this.tickets[OPP(team)]);
   const major=this.activeMajorEvent?.team===team?this.activeMajorEvent:null;
   for(const group of groups){const {squad}=group,assignment=commander.assignments.get(squad);if(!assignment)continue;const previous=this.squadOrders.get(SQUAD_KEY(team,squad));const force=!!major&&(major.kind==='COUNTER_OFFENSIVE'?squad<Math.ceil(groups.length*.7):major.kind==='ARMORED_PUSH'?squad<Math.min(3,groups.length):false);const best=this.points.find(p=>p.id===(force?major!.objective:assignment.objective))??this.combatObjectives[0]!;
    const intent:SquadIntent=force&&major?.kind==='ARMORED_PUSH'?(squad===1?'FLANK':'REINFORCE'):force&&major?.kind==='COUNTER_OFFENSIVE'?(squad%3===1?'FLANK':'ATTACK'):assignment.mission==='DEFEND_OBJECTIVE'?'DEFEND':assignment.mission==='REINFORCE_OBJECTIVE'?'REINFORCE':squad%4===1?'FLANK':'ATTACK';
    this.soldiers[group.leader]!.squadLeader=true;
    if(previous?.objective!==best.id||previous.intent!==intent){if(intent!=='DEFEND')this.assignedMissions++;this.completedOrders.delete(SQUAD_KEY(team,squad));}
    this.squadOrders.set(SQUAD_KEY(team,squad),{team,squad,objective:best.id,leaderId:group.leader,sharedEnemyId:previous?.sharedEnemyId??null,sharedEnemyAt:previous?.sharedEnemyAt??null,sharedEnemyUntil:previous?.sharedEnemyUntil??0,intent,approach:previous?.objective===best.id&&previous.intent===intent?previous.approach:this.objectiveApproach(team,squad,best,intent)});
   }
  }
  for(const s of this.soldiers){if(s.player)continue;const goal=this.squadOrders.get(SQUAD_KEY(s.team,s.squad))?.objective??'C';if(s.goal!==goal){s.goal=goal;s.route=[];s.nextPath=0}}
 }
 getRecommendation(){const r=this.commanders[this.player.team].recommendations.get(this.player.squad);return r?{...r}:null;}
 respondRecommendation(accept:boolean){const ok=this.commanders[this.player.team].respond(this.player.squad,accept);if(ok&&accept){this.assignSquads();}return ok;}
 private updateSquadReports(){
  for(const team of ['blue','red'] as const){const intel=this.intelligence[team];intel.snapshot(this.elapsed);
   for(const order of this.squadOrders.values()){if(order.team!==team)continue;const members=this.soldiers.filter(s=>s.team===team&&s.squad===order.squad&&s.alive),leader=members.find(s=>s.id===order.leaderId)??members[0];if(!leader)continue;if(order.leaderId!==leader.id){order.leaderId=leader.id;leader.squadLeader=true;}const objective=this.points.find(p=>p.id===order.objective)!;this.squadLeaderAI.plan(SQUAD_KEY(team,order.squad),this.elapsed,members,leader.id,objective,order.intent==='DEFEND');
    for(const p of this.combatObjectives)if(members.some(s=>dist(s.pos,p)<70&&!this.sightBlocked(s.pos,p)))intel.receive({source:leader.id,squad:order.squad,kind:'OBJECTIVE_STATUS',subject:p.id,at:p,time:this.elapsed,confidence:1,owner:p.owner,contested:p.contested});
    if(members.length<2)intel.receive({source:leader.id,squad:order.squad,kind:'SQUAD_NEEDS_ASSISTANCE',subject:`squad:${order.squad}`,at:leader.pos,time:this.elapsed,confidence:1});
   }
  }
  for(const [key,r] of this.coverReservations)if(r.until<=this.elapsed||!this.soldiers[r.id]?.alive)this.coverReservations.delete(key);
 }
 private recordCaptureMissions(point:CapturePoint,team:Team){
  for(const order of this.squadOrders.values())if(order.team===team&&order.objective===point.id&&order.intent!=='DEFEND'){
   if(!this.soldiers.some(s=>s.alive&&s.team===team&&s.squad===order.squad&&dist(s.pos,point)<25&&(s.vehicleId===null||(this.vehicles.find(v=>v.id===s.vehicleId)?.altitude??0)<5)))continue;
   const key=SQUAD_KEY(team,order.squad);if(this.completedOrders.has(key))continue;this.completedOrders.add(key);this.completedMissions++;
   this.intelligence[team].receive({source:order.leaderId,squad:order.squad,kind:'MISSION_COMPLETED',subject:point.id,at:point,time:this.elapsed,confidence:1});
  }
 }

 /** Smoke is a gameplay volume, independent of graphics quality. */
 sightBlocked(from:Point,to:Point,_blocks?:undefined,eye=1.5):boolean {
  if(this.map.lineBlocked(from,to,undefined,eye))return true;
  const dx=to.x-from.x,dz=to.z-from.z,len=dx*dx+dz*dz;
  return this.supports.some(e=>e.kind==='smoke'&&e.starts<=this.elapsed&&e.ends>this.elapsed&&(()=>{const t=clamp(((e.at.x-from.x)*dx+(e.at.z-from.z)*dz)/(len||1),0,1);return Math.hypot(from.x+dx*t-e.at.x,from.z+dz*t-e.at.z)<23})());
 }
 hasReconContact(at:Point):boolean{return this.supports.some(e=>(e.team??'blue')===this.player.team&&e.kind==='recon'&&e.ends>this.elapsed&&dist(e.at,at)<105)}
 canIdentify(at:Point,range=55):boolean{return this.player.alive&&dist(this.player.pos,at)<range&&!this.sightBlocked(this.player.pos,at)}
 onAward:((id:number,amount:number,reason:string)=>void)|null=null;
 awardRP(amount:number,reason:string,owner=this.player.id){if(!Number.isFinite(amount)||amount<=0||this.finished)return;if(this.onAward)this.onAward(owner,Math.floor(amount),reason);else this.requisitionPoints=Math.min(9999,this.requisitionPoints+Math.floor(amount));this.emit({type:'rp',amount:Math.floor(amount),reason,owner})}
 requestSupport(id:SupportId,objective:string):{ok:boolean;reason:string} {
  const def=SUPPORTS[id],point=this.points.find(p=>p.id===objective);
  if(!Object.hasOwn(SUPPORTS,id)||!def||!point)return {ok:false,reason:'Invalid support or target / 支援或目标无效'};
  if(this.finished||!this.player.alive)return {ok:false,reason:'Deploy first / 请先部署'};
  if(this.elapsed<(this.supportCooldowns[id]??0))return {ok:false,reason:'Cooling down / 冷却中'};
  if(this.requisitionPoints<def.cost)return {ok:false,reason:'Insufficient RP / 积分不足'};
  const isVehicle=id==='scout'||id==='ifv'||id==='tank'||id==='transport'||id==='aa'||id==='helicopter'||id==='jet'||id==='motorcycle';
  let vehicle:ArmoredVehicle|null=null;
  if(isVehicle){


   const cfg=VEHICLE_TYPES[id],base=this.player.pos;
   let spawn:Point|null=null;
   for(let i=0;i<20;i++){const q={x:base.x+12+(i%5)*10,z:base.z+12+Math.floor(i/5)*11};if(!this.map.collides(q.x,q.z,cfg.radius)&&!this.vehicles.some(v=>v.alive&&dist(v.pos,q)<cfg.radius+VEHICLE_TYPES[v.kind].radius+1)){spawn=q;break}}
   if(!spawn)return {ok:false,reason:'Parking obstructed / 停车区受阻'};
   const reusable=this.vehicles.find(v=>v.requisitioned&&!v.alive),index=reusable?.id??this.vehicles.length;
   vehicle=createVehicle(index,this.player.team,id,spawn,true);vehicle.reservedFor=this.player.id;vehicle.nextDecision=Infinity;vehicle.airborne=true;vehicle.dropStarted=this.elapsed;vehicle.landAt=this.elapsed+8;
   if(reusable)this.vehicles[index]=vehicle;else this.vehicles.push(vehicle);
  }
  this.requisitionPoints-=def.cost;this.supportCooldowns[id]=this.elapsed+def.cooldown;
  if(!isVehicle){const kind=id as SupportEffect['kind'];this.supports.push({id:this.supportSequence++,kind,team:this.player.team,owner:this.player.id,at:{x:point.x,z:point.z},starts:this.elapsed,ends:this.elapsed+(kind==='recon'?12:kind==='smoke'?22:kind==='artillery'?19:18),nextPulse:this.elapsed+5,pulses:kind==='artillery'?4:0});}
  this.emit({type:'support',kind:id,at:vehicle?{...vehicle.pos}:{x:point.x,z:point.z}});
  return {ok:true,reason:isVehicle?'Airdrop inbound · reserved for you / 8 秒空投至附近，专属载具':'Support active / 支援已呼叫'};
 }
 throwGrenade(direction:Point & {y?:number}):boolean {
  if(!this.player.alive||this.inVehicle||this.finished||!this.grenadeCount||this.elapsed<this.playerThrowUntil)return false;
  const len=Math.hypot(direction.x,direction.y??.25,direction.z);if(!Number.isFinite(len)||len<.01)return false;
  this.playerReloadUntil=0;this.reloadingWeapon=null;this.player.combatUntil=this.elapsed+8;this.grenadeCount--;this.playerThrowUntil=this.elapsed+.8;const p=this.player.pos;
  this.grenades.push({id:this.supportSequence++,kind:this.loadout.throwable,pos:{x:p.x,y:this.map.heightAt(p.x,p.z)+1.55+this.player.altitude,z:p.z},velocity:{x:direction.x/len*22,y:(direction.y??.25)/len*22+5,z:direction.z/len*22},detonate:this.elapsed+2.4,team:this.player.team,owner:this.player.id});this.emit({type:'thrown',owner:this.player.id,kind:this.loadout.throwable});return true;
 }
 useGadget():boolean {
  if(!this.player.alive||this.finished||this.inVehicle||(!['repair','ammo'].includes(this.loadout.gadget)&&!this.gadgetCharges)||this.elapsed<this.playerGadgetUntil)return false;
  const near=(s:Soldier)=>s.team===this.player.team&&s.id!==this.player.id&&dist(s.pos,this.player.pos)<6&&!this.map.lineBlocked(s.pos,this.player.pos);
  if(this.loadout.gadget==='medkit'){
   const down=this.soldiers.find(s=>!s.alive&&near(s)&&s.downedUntil>this.elapsed);
   if(down){down.downedUntil=0;down.rescueCalled=false;down.alive=true;down.hp=50;down.state='SPAWN';down.spawnGraceUntil=this.elapsed+.7;down.target=null;down.route=[];down.nextAiAt=this.elapsed+.5;down.lastAiAt=this.elapsed;down.lastProgress={...down.pos};down.suppression=0;this.emit({type:'revive',id:down.id});this.awardRP(35,'revive');}
   else {const target=this.soldiers.find(s=>s.alive&&near(s)&&s.hp<100)??this.player;if(target.hp>=100)return false;const healed=Math.min(100-target.hp,45);target.hp+=healed;if(target.id!==this.player.id)this.awardRP(Math.ceil(healed*.3),'team healing');target.suppression=Math.max(0,target.suppression-.4);}
  }else if(this.loadout.gadget==='repair'){
   const v=this.vehicles.find(v=>v.team===this.player.team&&v.alive&&v.hp<v.maxHp&&dist(v.pos,this.player.pos)<VEHICLE_TYPES[v.kind].radius+6&&!this.map.lineBlocked(v.pos,this.player.pos));if(!v)return false;const repaired=Math.min(v.maxHp-v.hp,130);v.hp+=repaired;this.awardRP(Math.ceil(repaired*.15),'vehicle repair');
  }else if(this.loadout.gadget==='ammo'){
   const own=this.ammoBoxes.filter(b=>b.owner===this.player.id);if(own.length>=2)this.ammoBoxes.splice(this.ammoBoxes.indexOf(own[0]!),1);this.ammoBoxes.push({id:this.supportSequence++,owner:this.player.id,team:this.player.team,at:{...this.player.pos},expires:this.elapsed+180,nextPulse:0});
  }else {
   if(this.map.collides(this.player.pos.x,this.player.pos.z,1.3))return false;
   this.beacon={at:{...this.player.pos},expires:this.elapsed+180};
  }
  if(!['repair','ammo'].includes(this.loadout.gadget))this.gadgetCharges--;this.playerGadgetUntil=this.elapsed+(this.loadout.gadget==='repair'?.75:10);return true;
 }
 private updateSupports(){
  for(const e of this.supports){
   if(e.ends<=this.elapsed)continue;
   if(e.kind==='reinforce')for(const s of this.soldiers)if(s.team===(e.team??'blue')&&!s.player){s.morale=Math.max(s.morale,.8);if(!s.alive)s.respawnAt=Math.min(s.respawnAt,this.elapsed+1)}
   if(e.kind==='artillery'&&e.pulses>0&&this.elapsed>=e.nextPulse){const a=this.random.between(0,6.28),r=this.random.between(0,17),at={x:e.at.x+Math.cos(a)*r,z:e.at.z+Math.sin(a)*r};this.emit({type:'artilleryImpact',team:e.team??'blue',at,radius:22});for(const s of this.soldiers)if(s.alive&&s.team!==(e.team??'blue')&&dist(s.pos,at)<22)this.damage(s,100*(1-dist(s.pos,at)/22)+12,this.soldiers[e.owner??this.player.id]!);for(const v of this.vehicles)if(v.alive&&v.team!==(e.team??'blue')&&dist(v.pos,at)<27)this.damageVehicle(v,120*(1-dist(v.pos,at)/27)+15,e.team??'blue',true,e.at,e.owner);e.pulses--;e.nextPulse=this.elapsed+3;}
  }
  for(let i=this.supports.length-1;i>=0;i--)if(this.supports[i]!.ends<=this.elapsed)this.supports.splice(i,1);

 }
 private findVisibleEnemy(s:Soldier):Soldier|null{
  let candidate:Soldier|null=null,best={easy:110,normal:125,hard:150}[this.settings.difficulty];const prior=s.visualTarget===null?null:this.soldiers[s.visualTarget];if(prior?.alive&&prior.vehicleId===null&&prior.team!==s.team&&dist(prior.pos,s.pos)<best&&!this.sightBlocked(s.pos,prior.pos))return prior;
  for(const e of this.soldiers){if(!e.alive||e.vehicleId!==null||e.team===s.team||(e.player&&this.inVehicle))continue;const d=dist(s.pos,e.pos);if(d>best)continue;
   const angle=Math.atan2(e.pos.x-s.pos.x,e.pos.z-s.pos.z),facing=Math.cos(angle-s.yaw),remembered=s.target===e.id||this.elapsed<s.alertUntil;
   if(!remembered&&(d>38&&facing<.12))continue;if(this.sightBlocked(s.pos,e.pos))continue;const score=d*(e.player?1.25:1);if(score<best){best=score;candidate=e;}
  }
  return candidate;
 }
 private findAudibleEnemy(s:Soldier):Soldier|null{const range={easy:18,normal:23,hard:28}[this.settings.difficulty];let heard:Soldier|null=null,best=range;for(const e of this.soldiers){if(!e.alive||e.vehicleId!==null||e.team===s.team||(e.player&&this.inVehicle))continue;const d=dist(s.pos,e.pos);if(d<best){best=d;heard=e}}return heard}
 private shareEnemy(s:Soldier,e:Soldier){
  this.intelligence[s.team].receive({source:s.id,squad:s.squad,kind:'ENEMY_OBSERVED',subject:`soldier:${e.id}`,at:e.pos,time:this.elapsed,confidence:.85});
  const order=this.squadOrders.get(SQUAD_KEY(s.team,s.squad));if(!order)return;
  order.sharedEnemyId=e.id;order.sharedEnemyAt={...e.pos};order.sharedEnemyUntil=this.elapsed+({easy:1.9,normal:2.7,hard:3.6}[this.settings.difficulty]);
 }
 private squadLeader(s:Soldier):Soldier|null{
  const id=this.squadOrders.get(SQUAD_KEY(s.team,s.squad))?.leaderId;return id===undefined?null:(this.soldiers[id]??null);
 }
 private formationDestination(s:Soldier,leader:Soldier):Point{
  const planned=this.squadLeaderAI.get(SQUAD_KEY(s.team,s.squad))?.memberDestinations[s.id];if(planned)return this.map.collides(planned.x,planned.z,1.12)?{...leader.pos}:{...planned};
  const members=this.soldiers.filter(m=>m.team===s.team&&m.squad===s.squad&&!m.player).sort((a,b)=>a.id-b.id);
  const idx=Math.max(0,members.findIndex(m=>m.id===s.id));const row=Math.floor(idx/2)+1,side=idx%2===0?-1:1;
  const forward=-4.7*row,lateral=3.2*side,sy=Math.sin(leader.yaw),cy=Math.cos(leader.yaw);
  return {x:leader.pos.x+cy*lateral+sy*forward,z:leader.pos.z-sy*lateral+cy*forward};
 }
 private findCover(s:Soldier,enemy:Point):Point|null{
  let best:Point|null=null,bestScore=Infinity;
  for(const c of this.map.cover){const reservation=this.coverReservations.get(`${c.x},${c.z}`);if(reservation&&reservation.id!==s.id&&reservation.until>this.elapsed)continue;const d=dist(s.pos,c);if(this.map.collides(c.x,c.z,1.12)||d>44||d<3.5)continue;if(!this.sightBlocked(c,enemy,undefined,.25))continue;
   const enemyDistance=dist(c,enemy);if(enemyDistance<12)continue;const score=d-enemyDistance*.035+this.random.between(0,1.2);if(score<bestScore){bestScore=score;best=c}
  }
  if(best)this.coverReservations.set(`${best.x},${best.z}`,{id:s.id,until:this.elapsed+5});return best?{...best}:null;
 }
 private safeDestination(s:Soldier,enemy:Point):Point{
  const options:Point[]=[this.map.bases[s.team],...this.points.filter(p=>p.owner===s.team)];let best=options[0]!,score=-Infinity;
  for(const p of options){const value=dist(p,enemy)-dist(s.pos,p)*.28;if(value>score&&!this.sightBlocked(s.pos,p,undefined,1.5)){score=value;best=p}}
  return {...best};
 }
 private safeObservation(at:Point,origin:Point):Point{
  const center={x:clamp(at.x,-this.map.limit+2,this.map.limit-2),z:clamp(at.z,-this.map.limit+2,this.map.limit-2)};
  if(!this.map.collides(center.x,center.z,1.12))return center;
  for(const radius of [4,8,12,18,28])for(let i=0;i<16;i++){
   const angle=i*Math.PI/8,q={x:clamp(center.x+Math.cos(angle)*radius,-this.map.limit+2,this.map.limit-2),z:clamp(center.z+Math.sin(angle)*radius,-this.map.limit+2,this.map.limit-2)};
   if(!this.map.collides(q.x,q.z,1.12))return q;
  }
  return {...origin};
 }
 private setBotIntent(s:Soldier,kind:AIIntentKind,state:BotState,destination:Point,speed:number,reason:string,targetId:number|null=null,urgent=false):AIIntent{
  const previous=s.aiIntent,{intent,changed}=resolveIntent(this.elapsed,s.pos,previous,{kind,state,destination,goal:s.goal,targetId,speed,reason},this.settings.aiProfile??'regular',urgent);
  if(previous&&dist(previous.destination,intent.destination)>3)s.aiStats.destinationChanges++;
  if(changed){
   this.intentSwitches++;this.tacticalLog.push({at:this.elapsed,soldier:s.id,from:previous?.kind??'SPAWN',to:intent.kind,reason:intent.reason});if(this.tacticalLog.length>256)this.tacticalLog.shift();
   if(previous?.state!==intent.state)s.aiStats.stateChanges++;
   s.aiIntent=intent;s.route=[];s.routeFailed=false;s.nextPath=0;
  }else if(intent!==previous){
   this.formationUpdates++;
   if(s.route.length){
    const anchor=s.route.length>1?s.route[s.route.length-2]!:s.pos;
    if(!this.map.collides(intent.destination.x,intent.destination.z,1.12)&&!this.map.lineBlocked(anchor,intent.destination,undefined,1.12))s.route[s.route.length-1]={...intent.destination};
    else{s.route=[];s.nextPath=0;}
   }
   s.aiIntent=intent;
  }
  s.state=intent.state;return intent;
 }
 private reportBotBlocked(s:Soldier){
  this.navigationFailures++;s.aiStats.navigationFailures++;
  this.intelligence[s.team].receive({source:s.id,squad:s.squad,kind:'NAVIGATION_BLOCKED',subject:`squad:${s.squad}`,at:s.pos,time:this.elapsed,confidence:1});
  this.setBotIntent(s,'RECOVER_FROM_NAVIGATION_FAILURE','SEARCH',{...s.pos},0,'路线不可达，重新规划',null,true);
  s.route=[];s.routeFailed=true;s.nextPath=this.elapsed+2.5;s.stuckFor=0;
 }
 private moveBot(s:Soldier,destination:Point,dt:number,speed:number){
  if(!s.route.length&&s.nextPath>this.elapsed){if(s.routeFailed){s.stuckFor+=dt;s.aiStats.movingWithoutProgress+=dt;if(s.stuckFor>2)this.reportBotBlocked(s);}return;}
  if(!s.route.length||s.nextPath<=this.elapsed){s.route=this.nav.find(s.pos,destination);s.routeFailed=!s.route.length;s.nextPath=this.elapsed+(this.settings.aiProfile==='elite'?3.5:2.8);if(!s.route.length){s.stuckFor+=dt;s.aiStats.movingWithoutProgress+=dt;if(s.stuckFor>2)this.reportBotBlocked(s);return;}}
  while(s.route.length&&dist(s.pos,s.route[0]!)<2.4)s.route.shift();
  if(!s.route.length)return;
  const next=s.route[0]!;let velocity=norm({x:next.x-s.pos.x,z:next.z-s.pos.z});
  let avoidX=0,avoidZ=0;
  for(const mate of this.soldiers){if(mate.id===s.id||!mate.alive||mate.team!==s.team)continue;const d=dist(s.pos,mate.pos);if(d>0&&d<2.7){avoidX+=(s.pos.x-mate.pos.x)/d*(2.7-d);avoidZ+=(s.pos.z-mate.pos.z)/d*(2.7-d)}}
  velocity=norm({x:velocity.x+avoidX*.46,z:velocity.z+avoidZ*.46});const move=Math.min(4.2,dt*speed*Math.min(1,Math.max(.3,dist(s.pos,destination)/7)));s.yaw=Math.atan2(velocity.x,velocity.z);
  let moved=false;if(!this.map.collides(s.pos.x+velocity.x*move,s.pos.z,1.12)){s.pos.x+=velocity.x*move;moved=true}
  if(!this.map.collides(s.pos.x,s.pos.z+velocity.z*move,1.12)){s.pos.z+=velocity.z*move;moved=true}
  const progress=dist(s.pos,s.lastProgress);
  if(moved&&progress>.8){s.lastProgress={...s.pos};s.stuckFor=0}else {s.stuckFor+=dt;s.aiStats.movingWithoutProgress+=dt;}
  if(!moved){s.route=[];s.nextPath=0}
  if(s.stuckFor>(this.settings.aiProfile==='elite'?1.2:1.65)){let escape:Point|null=null,best=Infinity;const seed=(s.id%8)*Math.PI/4;for(let i=0;i<8;i++){const a=seed+i*Math.PI/4,r=4.8+(i%2)*1.8,c={x:s.pos.x+Math.cos(a)*r,z:s.pos.z+Math.sin(a)*r};if(this.map.collides(c.x,c.z,1.12)||this.map.lineBlocked(s.pos,c,undefined,1.12))continue;let crowd=0;for(const m of this.soldiers)if(m.id!==s.id&&m.alive&&m.team===s.team&&dist(c,m.pos)<3.2)crowd++;const score=dist(c,destination)+crowd*8;if(score<best){best=score;escape=c}}if(escape){s.route=[escape];s.nextPath=this.elapsed+.8;s.lastProgress={...s.pos}}else this.reportBotBlocked(s);s.stuckFor=0}
 }

 private combatDestination(s:Soldier,enemy:Soldier):Point {
  const d=dist(s.pos,enemy.pos),dx=enemy.pos.x-s.pos.x,dz=enemy.pos.z-s.pos.z,len=Math.hypot(dx,dz)||1,fx=dx/len,fz=dz/len;
  const preferred=s.classId==='recon'?85:s.weaponId==='lmg'?45:s.weaponId==='smg'?22:35;
  if(Math.abs(d-preferred)<10)return {...s.pos};
  const order=this.squadOrders.get(SQUAD_KEY(s.team,s.squad)),side=s.id%2===0?1:-1,lateral=order?.intent==='FLANK'?18*side:6*side;
  const q={x:enemy.pos.x-fx*preferred+fz*lateral,z:enemy.pos.z-fz*preferred-fx*lateral};return this.map.collides(q.x,q.z,1.3)?{...s.pos}:q;
 }
 private botEquipment(s:Soldier){
  if(this.elapsed<s.nextEquipment||s.vehicleId!==null)return;s.nextEquipment=this.elapsed+this.random.between(12,20);
  if(s.classId==='medic'){const casualty=this.soldiers.find(o=>o.team===s.team&&!o.alive&&o.downedUntil>this.elapsed&&dist(o.pos,s.pos)<6&&!this.map.collides(o.pos.x,o.pos.z,1.12)&&!this.sightBlocked(s.pos,o.pos));if(casualty&&!this.spawnThreatForTeam(s.team,casualty.pos)){s.equipmentUntil=this.elapsed+1.2;s.equipmentKind='medical';casualty.downedUntil=0;casualty.rescueCalled=false;casualty.alive=true;casualty.hp=55;casualty.spawnGraceUntil=this.elapsed+1;casualty.target=null;this.rescueCount++;this.emit({type:'revive',id:casualty.id});}else for(const o of this.soldiers)if(o.alive&&o.team===s.team&&dist(o.pos,s.pos)<8){s.equipmentUntil=this.elapsed+1.2;s.equipmentKind='medical';o.hp=Math.min(100,o.hp+30);}}
  if(s.classId==='engineer'){const friend=this.vehicles.find(v=>v.alive&&v.team===s.team&&v.hp<v.maxHp&&dist(v.pos,s.pos)<9);if(friend){s.equipmentUntil=this.elapsed+1.2;s.equipmentKind='repair';friend.hp=Math.min(friend.maxHp,friend.hp+85);this.repairCount++;}else {const enemy=this.vehicles.find(v=>v.alive&&!v.airborne&&v.team!==s.team&&dist(v.pos,s.pos)<150&&!this.sightBlocked(s.pos,v.pos));if(enemy&&this.projectiles.length<96){s.equipmentUntil=this.elapsed+1.2;s.equipmentKind='rocket';const dx=enemy.pos.x-s.pos.x,dz=enemy.pos.z-s.pos.z,dy=enemy.altitude+1.7,d=Math.hypot(dx,dy,dz);this.launchProjectile(s.pos,{x:dx/d,y:dy/d,z:dz/d},'rocket',s.team,false,120,4,250,null,undefined,s.id);if(enemy.altitude>5)this.projectiles[this.projectiles.length-1]!.targetId=enemy.id;}}}
  if(s.classId==='assault'&&this.soldiers.some(o=>o.team===s.team&&o.aiAmmo<WEAPONS[o.weaponId].magazine/2&&dist(o.pos,s.pos)<12)){s.equipmentUntil=this.elapsed+1.2;s.equipmentKind='ammo';const own=this.ammoBoxes.filter(b=>b.owner===s.id);if(own.length>=2)this.ammoBoxes.splice(this.ammoBoxes.indexOf(own[0]!),1);this.ammoBoxes.push({id:this.supportSequence++,owner:s.id,team:s.team,at:{...s.pos},expires:this.elapsed+180,nextPulse:0});}
  if(s.classId==='recon'&&s.suppression>.35){s.equipmentUntil=this.elapsed+1.2;s.equipmentKind='smoke';this.supports.push({id:this.supportSequence++,kind:'smoke',team:s.team,owner:s.id,at:{...s.pos},starts:this.elapsed,ends:this.elapsed+10,nextPulse:0,pulses:0});}
  if((s.equipmentUntil??0)<=this.elapsed)s.nextEquipment=this.elapsed+1;
 }
 private rescueTarget(s:Soldier):Soldier|null{
  if(s.classId!=='medic'||s.vehicleId!==null)return null;
  const candidates=this.soldiers.filter(o=>!o.alive&&o.downedUntil>this.elapsed&&o.team===s.team&&this.map.collides(o.pos.x,o.pos.z,1.12)===false&&dist(s.pos,o.pos)<(o.rescueCalled?100:60)).sort((a,b)=>Number(b.player)-Number(a.player)||dist(s.pos,a.pos)-dist(s.pos,b.pos));
  for(const target of candidates){const key=s.id+':'+target.id,cached=this.rescuePaths.get(key);if(cached&&this.elapsed-cached.checked<2.5){if(cached.reachable)return target;continue;}const reachable=this.nav.find(s.pos,target.pos).length>0;this.rescuePaths.set(key,{checked:this.elapsed,reachable});if(reachable)return target;}
  return null;
 }
 private spawnThreatForTeam(team:Team,at:Point){return this.soldiers.some(o=>o.alive&&o.team!==team&&dist(o.pos,at)<25&&!this.sightBlocked(o.pos,at));}
 private botAction(s:Soldier,dt:number){
  if(!s.alive||s.player)return;s.suppression=Math.max(0,s.suppression-dt*.17);s.morale=clamp(s.morale+dt*.025,0,1);
  const surge=!!this.eventForTeam(s.team,'COUNTER_OFFENSIVE');if(surge)s.morale=Math.max(s.morale,.56);
  const visible=this.findVisibleEnemy(s);if(visible?.id!==s.visualTarget){s.visualTarget=visible?.id??null;s.reactionUntil=this.elapsed+({easy:1.15,normal:.8,hard:.45}[this.settings.difficulty])+this.random.between(.05,.22);s.burstLeft=0}if(visible){s.target=visible.id;s.alertUntil=this.elapsed+2.8;s.lastSeen={...visible.pos};s.lastSeenAt=this.elapsed;this.shareEnemy(s,visible)}else{const audible=this.findAudibleEnemy(s);if(audible&&this.random.next()<.58){const error={easy:7,normal:4.5,hard:2.8}[this.settings.difficulty];s.target=audible.id;s.alertUntil=this.elapsed+1.6;s.lastSeen={x:audible.pos.x+this.random.between(-error,error),z:audible.pos.z+this.random.between(-error,error)};s.lastSeenAt=this.elapsed-.15;this.intelligence[s.team].receive({source:s.id,squad:s.squad,kind:'SOUND_HEARD',subject:`sector:${Math.round(s.lastSeen.x/20)},${Math.round(s.lastSeen.z/20)}`,at:s.lastSeen,time:this.elapsed,confidence:.35});}}
  const order=this.squadOrders.get(SQUAD_KEY(s.team,s.squad));
  if(!visible&&order?.sharedEnemyId!==null&&order&&this.elapsed<order.sharedEnemyUntil){const shared=this.soldiers[order.sharedEnemyId];if(shared?.alive&&order.sharedEnemyAt&&dist(s.pos,order.sharedEnemyAt)<125){s.target=shared.id;s.lastSeen={...order.sharedEnemyAt};s.lastSeenAt=this.elapsed-.4}}
  const enemy=visible;
  const objective=this.points.find(p=>p.id===s.goal)??this.points[2]!,approach=order?.approach??objective,leader=this.squadLeader(s);
  const friendlyForce=this.localStrength(s.team,s.pos,30),enemyForce=this.soldiers.filter(e=>e.alive&&e.team!==s.team&&dist(e.pos,s.pos)<30&&!this.sightBlocked(s.pos,e.pos)).length;let destination:Point=approach,speed=9.3*(surge?1.12:1);
  const barrage=this.activeMajorEvent?.kind==='ARTILLERY'&&this.activeMajorEvent.team!==s.team&&this.elapsed<this.activeMajorEvent.warningUntil?this.activeMajorEvent:null;
  const barragePoint=barrage?this.points.find(p=>p.id===barrage.objective)??null:null;

  if(this.elapsed<s.spawnGraceUntil){s.state='SPAWN';destination=leader&&leader.id!==s.id?this.formationDestination(s,leader):approach;speed=8.2}
  else if(this.supports.some(e=>(e.team??'blue')!==s.team&&e.kind==='artillery'&&this.elapsed<e.starts+5&&dist(s.pos,e.at)<48)){s.state='SEEK_COVER';destination=this.safeDestination(s,this.supports.find(e=>e.kind==='artillery')!.at);speed=11.6}
  else if(barragePoint&&dist(s.pos,barragePoint)<48){s.state='SEEK_COVER';destination=this.safeDestination(s,barragePoint);speed=11.6;s.cover=null}
  else if(enemy&&(this.squadLeaderAI.get(SQUAD_KEY(s.team,s.squad))?.state==='WITHDRAWING'||s.hp<24||(enemyForce>friendlyForce*1.55&&(s.hp<68||s.morale<.55)))){s.state='RETREAT';destination=this.safeDestination(s,enemy.pos);speed=11.3;s.cover=null}
  else if(enemy&&(s.hp<55||s.suppression>.64)){
   if(!s.cover||!this.sightBlocked(s.cover,enemy.pos,undefined,.2))s.cover=this.findCover(s,enemy.pos);
   if(s.cover){s.state='SEEK_COVER';destination=s.cover;speed=10.4}else{s.state='ENGAGE';destination=this.combatDestination(s,enemy);speed=7.2}
  }
  else if(!enemy&&s.classId==='engineer'&&this.vehicles.some(v=>v.alive&&v.team===s.team&&v.hp<v.maxHp&&Math.abs(v.speed)<2&&dist(v.pos,s.pos)<25&&!this.sightBlocked(s.pos,v.pos))){const v=this.vehicles.find(v=>v.alive&&v.team===s.team&&v.hp<v.maxHp&&Math.abs(v.speed)<2&&dist(v.pos,s.pos)<25&&!this.sightBlocked(s.pos,v.pos))!;const dir=norm({x:s.pos.x-v.pos.x,z:s.pos.z-v.pos.z}),offset=VEHICLE_TYPES[v.kind].radius+2;destination={x:v.pos.x+dir.x*offset,z:v.pos.z+dir.z*offset};s.state='FOLLOW_SQUAD';speed=dist(s.pos,v.pos)>8?8.6:0;}
  else if(enemy){s.state='ENGAGE';destination=this.combatDestination(s,enemy);speed=dist(s.pos,enemy.pos)>62?7.8:dist(destination,s.pos)>2?5.4:0}
  else if(s.lastSeen&&this.elapsed-s.lastSeenAt<4.8){s.state='SEARCH';destination=this.safeObservation(s.lastSeen,s.pos);speed=8.7}
  else if(leader&&leader.id!==s.id&&leader.alive&&(dist(s.pos,objective)>25||this.settings.aiProfile==='elite'&&dist(s.pos,leader.pos)>25&&dist(s.pos,objective)>18)){s.state='FOLLOW_SQUAD';destination=this.formationDestination(s,leader);speed=dist(s.pos,destination)>13?10:8.2}
  else if(dist(s.pos,approach)>8&&dist(s.pos,objective)>17){s.state='MOVE_TO_OBJECTIVE';destination=approach;speed=10.2*(surge?1.08:1)}
  else if(dist(s.pos,objective)<27){s.state=objective.owner===s.team&&!objective.contested&&dist(s.pos,approach)<5?'IDLE':'DEFEND';destination=order?.intent==='DEFEND'?approach:objective;speed=s.state==='IDLE'?0:5.8}
  else {s.state='MOVE_TO_OBJECTIVE';destination=objective;speed=10.1}

  const localPlan=this.squadLeaderAI.get(SQUAD_KEY(s.team,s.squad));if(!enemy&&(!s.lastSeen||this.elapsed-s.lastSeenAt>=4.8)&&localPlan?.holdLeader&&s.id===order?.leaderId){speed=0;s.state='IDLE';destination={...s.pos};}
  if(enemy&&s.state==='ENGAGE'&&s.classId==='recon'&&dist(s.pos,enemy.pos)>40){speed=0;s.state='DEFEND';destination={...s.pos};}else if(enemy&&s.state==='ENGAGE'&&localPlan?.state==='ENGAGING'&&localPlan.roles[s.id]==='cover'&&dist(s.pos,enemy.pos)<WEAPONS[s.weaponId].effectiveRange&&!this.sightBlocked(s.pos,enemy.pos)){speed=0;s.state='ENGAGE';destination={...s.pos};}
  speed=Math.min(speed,(s.state==='RETREAT'||s.state==='SEEK_COVER')?13.7:this.settings.aiProfile==='elite'&&s.state==='FOLLOW_SQUAD'?10:8.6);
  const kind:AIIntentKind=s.state==='RETREAT'?'WITHDRAW':s.state==='SEEK_COVER'?'TAKE_COVER':s.state==='ENGAGE'?'ENGAGE_IN_GAME_COMBAT':s.state==='SEARCH'?'OBSERVE':s.state==='FOLLOW_SQUAD'?'REGROUP':s.state==='DEFEND'||s.state==='IDLE'?'HOLD_POSITION':s.state==='MOVE_TO_OBJECTIVE'&&dist(s.pos,objective)<27?'INTERACT_WITH_OBJECTIVE':'MOVE_WITH_SQUAD';
  const prior=s.aiIntent,roleChanged=enemy!==null&&localPlan?.state==='ENGAGING'&&((speed===0)!==(prior?.speed===0)),urgent=roleChanged||(kind==='WITHDRAW'&&prior?.kind!=='WITHDRAW')||(enemy!==null&&prior?.targetId!==enemy.id)||(kind==='OBSERVE'&&prior?.kind!=='OBSERVE'&&this.elapsed-s.lastSeenAt<.4);
  const intent=this.setBotIntent(s,kind,s.state,destination,speed,localPlan?.reason??'执行小队任务',enemy?.id??null,urgent);
  destination=intent.destination;speed=intent.speed;
  if(s.state==='IDLE'&&dist(s.pos,objective)>30&&!localPlan?.holdLeader)s.aiStats.idleWithoutReason+=dt;
  if(speed>0&&dist(s.pos,destination)>2.5)this.moveBot(s,destination,dt,speed);else if(enemy)s.yaw=Math.atan2(enemy.pos.x-s.pos.x,enemy.pos.z-s.pos.z);

  if(enemy&&visible?.id===enemy.id&&this.elapsed>=s.reactionUntil){const d=dist(s.pos,enemy.pos);if(d<Math.min(WEAPONS[s.weaponId].effectiveRange+25,s.classId==='recon'?150:120)&&this.elapsed>=s.nextShot){
   const cfg=WEAPONS[s.weaponId];
   if(enemy.player){for(const [id,until] of this.playerAttackers)if(until<=this.elapsed)this.playerAttackers.delete(id);if(!this.playerAttackers.has(s.id)&&this.playerAttackers.size>=({easy:1,normal:2,hard:3}[this.settings.difficulty])){s.nextShot=this.elapsed+this.random.between(.3,.7);return;}this.playerAttackers.set(s.id,this.elapsed+1.2);}
   if(s.burstLeft<=0)s.burstLeft=cfg.automatic?(d>65?2:3):1;
   if(s.aiAmmo<=0){s.aiAmmo=cfg.magazine;s.nextShot=this.elapsed+cfg.reload;return;}s.aiAmmo--;s.burstLeft--;s.nextShot=this.elapsed+(s.burstLeft>0?Math.max(.22,cfg.fireInterval):Math.max(cfg.fireInterval,this.random.between(1.2,2.2)));
   const lead={easy:.25,normal:.65,hard:.85}[this.settings.difficulty],flight=d/230;
   const error=({easy:.075,normal:.045,hard:.024}[this.settings.difficulty]*d+.5)*(1+s.suppression*2.8)*(speed>0?1.3:1)*(s.morale<.5?1.3:1);
   const impact={x:enemy.pos.x+enemy.velocity.x*flight*lead+this.random.between(-error,error),z:enemy.pos.z+enemy.velocity.z*flight*lead+this.random.between(-error,error)};
   // A geometric ray test replaces the old chance-to-damage roll.
   const aim=norm({x:impact.x-s.pos.x,z:impact.z-s.pos.z}),dx=enemy.pos.x+enemy.velocity.x*flight-s.pos.x,dz=enemy.pos.z+enemy.velocity.z*flight-s.pos.z;
   const hit=Math.abs(dx*aim.z-dz*aim.x)<.82&&dx*aim.x+dz*aim.z>0&&!this.sightBlocked(s.pos,enemy.pos);
   this.emit({type:'shot',team:s.team,from:{...s.pos},to:impact,hit,player:false,weapon:s.weaponId});
   enemy.suppression=clamp(enemy.suppression+(hit?.18:.04),0,1);
   if(hit)this.damage(enemy,cfg.damage*({easy:.4,normal:.55,hard:.7}[this.settings.difficulty])*clamp(1-d/180,.45,1),s);
  }}else if(this.elapsed-s.lastSeenAt>5){s.target=null;s.lastSeen=null;s.cover=null}
 }

 private eventTarget(team:Team):CapturePoint{
  const reports=this.intelligence[team].snapshot(this.elapsed);let best=this.combatObjectives[0]!,bestScore=-Infinity;for(const p of this.combatObjectives){const known=reports.find(r=>r.kind==='OBJECTIVE_STATUS'&&r.subject===p.id&&r.confidenceNow>.2),hostile=reports.filter(r=>r.kind==='ENEMY_OBSERVED'&&dist(r.at,p)<62).reduce((n,r)=>n+r.confidenceNow,0),friendly=this.localStrength(team,p,62);let score=(known?.owner===team?-2:known?.owner?4.2:2.5)+(known?.contested?3.5:0)+(hostile-friendly)*.16-dist(this.map.bases[team],p)*.002;score+=this.random.between(0,.35);if(score>bestScore){bestScore=score;best=p}}return best;
 }
 private chooseEventTeam():Team{
  const diff=this.tickets.blue-this.tickets.red;if(Math.abs(diff)>45)return diff<0?'blue':'red';const blueHeld=this.points.filter(p=>p.owner==='blue').length,redHeld=this.points.filter(p=>p.owner==='red').length;if(blueHeld!==redHeld)return blueHeld<redHeld?'blue':'red';return this.majorEventSequence%2===0?'red':'blue';
 }
 private startMajorEvent(){
  const kinds:MajorEventKind[]=['ARTILLERY','COUNTER_OFFENSIVE','ARMORED_PUSH'],kind=kinds[(this.majorEventSequence+this.majorEventOffset)%kinds.length]!,team=this.chooseEventTeam(),target=this.eventTarget(team);this.majorEventSequence++;
  const warning=kind==='ARTILLERY'?5:0,duration=kind==='ARTILLERY'?18:kind==='COUNTER_OFFENSIVE'?44:52;
  this.activeMajorEvent={id:this.majorEventId++,kind,team,objective:target.id,startedAt:this.elapsed,endsAt:this.elapsed+duration,warningUntil:this.elapsed+warning,nextPulse:this.elapsed+warning,pulsesRemaining:kind==='ARTILLERY'?4:0};
  if(kind==='COUNTER_OFFENSIVE'){for(const s of this.soldiers)if(s.team===team&&!s.player){s.morale=Math.max(s.morale,.72);if(!s.alive)s.respawnAt=Math.min(s.respawnAt,this.elapsed+2.8)}}
  if(kind==='ARMORED_PUSH')for(const armor of this.vehicles)if(armor.team===team&&armor.alive&&['tank','ifv'].includes(armor.kind)&&armor.driver==='ai'&&armor.occupants[0]!==null){armor.goal=target.id;armor.nextDecision=this.elapsed;armor.route=[];}
  this.nextMajorEventAt=this.elapsed+duration+this.random.between(15,22);this.emit({type:'majorEvent',action:warning?'warning':'start',kind,team,objective:target.id});this.assignSquads();
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

 private damageVehicle(v:ArmoredVehicle,amount:number,attacker:Team,playerCredit=false,origin:Point=this.map.bases[attacker],creditOwner=this.player.id){
  if(!v.alive)return;amount=Math.min(v.hp,Math.max(0,amount));if(playerCredit&&v.team!==attacker)this.emit({type:'hitConfirmed',owner:creditOwner,target:v.id,vehicle:true,amount,killed:amount>=v.hp,at:{...v.pos}});v.combatUntil=this.elapsed+8;this.emit({type:'vehicleHit',id:v.id,amount,from:{...origin}});v.hp=Math.max(0,v.hp-amount);if(v.hp>0)return;
  const crew=[...v.occupants];for(const id of crew){const occupant=this.soldiers.find(s=>s.id===id);if(occupant&&(occupant.alive||occupant.downedUntil>0))this.eliminate(occupant,this.soldiers.find(s=>s.id===creditOwner&&s.team===attacker)??null);}v.occupants.fill(null);v.driver=null;v.alive=false;v.disabledAt=this.elapsed;v.respawnAt=v.requisitioned?Infinity:this.elapsed+VEHICLE_TYPES[v.kind].respawnDelay;v.speed=0;v.route=[];
  if(this.playerVehicleId===v.id){const p=this.player;this.playerVehicleId=null;p.vehicleId=null;const side={x:v.pos.x+4.8,z:v.pos.z};p.pos=this.map.collides(side.x,side.z,1.05)?{x:v.pos.x,z:v.pos.z+4.8}:side;p.hp=Math.max(15,p.hp-55)}
  if(playerCredit&&v.team!==attacker)this.awardRP(RP_REWARDS.vehicle,'vehicle destroyed',creditOwner);
  this.emit({type:'vehicleDisabled',team:v.team,id:v.id,at:{...v.pos}});
  // Vehicle losses matter strategically but do not duplicate a full soldier ticket penalty.
  if(attacker!==v.team&&this.settings.killTicketPenalty&&(this.settings.mode!=='breakthrough'||v.team==='blue')){this.tickets[v.team]=Math.max(0,this.tickets[v.team]-.5*(this.settings.killTicketPenalty??1));this.checkFinish()}
 }

 private resetVehicle(v:ArmoredVehicle){const p=v.spawn;v.pos={...p};v.lastProgress={...p};v.yaw=v.team==='blue'?0:Math.PI;v.turretYaw=v.yaw;v.turretPitch=0;v.mgYaw=v.yaw;v.hp=v.maxHp;v.combatUntil=0;v.altitude=0;v.verticalVelocity=0;v.enginePower=0;v.climbInput=0;v.roll=0;v.flightPitch=0;v.incomingUntil=0;v.warningUntil=0;v.flareUntil=0;v.lockProgress=0;v.lockTarget=null;v.alive=true;v.speed=0;v.driver=null;v.occupants.fill(null);v.airborne=false;v.route=[];v.coverAt=null;v.nextCoverPlan=0;v.nextPath=0;v.nextDecision=this.elapsed+(v.team==='blue'?12:.5);v.nextShot=this.elapsed+1;v.nextMGShot=this.elapsed+1;v.steering=0;v.stuckFor=0;this.emit({type:'vehicleRespawn',team:v.team,id:v.id})}

 private reportVehicleBlocked(v:ArmoredVehicle){
  this.navigationFailures++;this.intelligence[v.team].receive({source:v.occupants[0]??-1,squad:this.soldiers[v.occupants[0]??-1]?.squad??0,kind:'NAVIGATION_BLOCKED',subject:`vehicle:${v.id}`,at:v.pos,time:this.elapsed,confidence:1});
  // After recovery cannot find a usable route, AI continues its mission on foot.
  for(let seat=0;seat<v.seatCount;seat++){const s=this.soldiers[v.occupants[seat]??-1];if(s&&!s.player&&this.displaceSeat(v,seat))s.boardingUntil=this.elapsed+40;}
  v.driver=v.occupants[0]===null?null:this.soldiers[v.occupants[0]!]?.player?'player':'ai';v.nextDecision=this.elapsed+40;v.stuckFor=0;v.speed=0;
 }
 private vehicleMove(v:ArmoredVehicle,destination:Point,dt:number){
  if(v.kind==='helicopter'||v.kind==='jet'){const yaw=Math.atan2(destination.x-v.pos.x,destination.z-v.pos.z),evading=v.warningUntil>this.elapsed||v.incomingUntil>this.elapsed||v.flareUntil>this.elapsed;this.flyVehicle(v,dist(v.pos,destination)>30?1:.2,evading?(v.id%2?1:-1):0,dt,yaw-Math.PI,0,evading); return;}
  if(!v.route.length&&v.nextPath>this.elapsed){v.speed=0;v.stuckFor+=dt;return;}
  if(!v.route.length||v.nextPath<=this.elapsed){let nav=this.vehicleNav.get(v.kind);if(!nav){nav=new Navigation(VEHICLE_TYPES[v.kind].radius,true,this.map);this.vehicleNav.set(v.kind,nav);}v.route=nav.find(v.pos,destination);v.nextPath=this.elapsed+3;if(!v.route.length){v.speed=0;v.stuckFor+=dt;if(v.stuckFor>6)this.reportVehicleBlocked(v);return;}}
  while(v.route.length&&dist(v.pos,v.route[0]!)<7)v.route.shift();const next=v.route[0]??destination;
  const desired=Math.atan2(next.x-v.pos.x,next.z-v.pos.z),delta=Math.atan2(Math.sin(desired-v.yaw),Math.cos(desired-v.yaw));v.yaw+=clamp(delta,-VEHICLE_TYPES[v.kind].turnRate*dt,VEHICLE_TYPES[v.kind].turnRate*dt);
  const alignment=Math.max(.15,Math.cos(delta));const target=VEHICLE_TYPES[v.kind].maxForward*alignment*Math.min(1,dist(v.pos,destination)/35);v.speed+=clamp(target-v.speed,-VEHICLE_TYPES[v.kind].acceleration*dt,VEHICLE_TYPES[v.kind].acceleration*dt);
  const dx=Math.sin(v.yaw)*v.speed*dt,dz=Math.cos(v.yaw)*v.speed*dt;let moved=false;
  if(!this.map.collides(v.pos.x+dx,v.pos.z,VEHICLE_TYPES[v.kind].radius)&&!this.vehicles.some(o=>o!==v&&o.alive&&!o.airborne&&o.altitude<5&&dist({x:v.pos.x+dx,z:v.pos.z},o.pos)<VEHICLE_TYPES[v.kind].radius+VEHICLE_TYPES[o.kind].radius)){v.pos.x+=dx;moved=true}if(!this.map.collides(v.pos.x,v.pos.z+dz,VEHICLE_TYPES[v.kind].radius)&&!this.vehicles.some(o=>o!==v&&o.alive&&!o.airborne&&o.altitude<5&&dist({x:v.pos.x,z:v.pos.z+dz},o.pos)<VEHICLE_TYPES[v.kind].radius+VEHICLE_TYPES[o.kind].radius)){v.pos.z+=dz;moved=true}
  if(dist(v.pos,v.lastProgress)>2.2){v.lastProgress={...v.pos};v.stuckFor=0}else v.stuckFor+=dt;
  if(!moved||v.stuckFor>2.2){
   let escape:Point|null=null,best=Infinity;
   for(let i=0;i<16;i++){const a=v.yaw+i*Math.PI/8,r=VEHICLE_TYPES[v.kind].radius+5,q={x:v.pos.x+Math.sin(a)*r,z:v.pos.z+Math.cos(a)*r};if(this.map.collides(q.x,q.z,VEHICLE_TYPES[v.kind].radius))continue;let clear=true;for(let t=.2;t<=1;t+=.2)if(this.map.collides(v.pos.x+(q.x-v.pos.x)*t,v.pos.z+(q.z-v.pos.z)*t,VEHICLE_TYPES[v.kind].radius)){clear=false;break}if(!clear)continue;const score=dist(q,destination);if(score<best){best=score;escape=q}}
   if(escape){v.route=[escape];v.nextPath=this.elapsed+1.8;v.speed=2.5;v.stuckFor=0}
   else{v.speed=0;v.nextPath=this.elapsed+1;if(v.stuckFor>6)this.reportVehicleBlocked(v)}
  }
 }

 private planVehicleCover(v:ArmoredVehicle,driver:Soldier|undefined,objective:CapturePoint){
  if(!driver||!['tank','ifv','scout','aa'].includes(v.kind)){v.coverAt=null;return;}
  if(this.elapsed<v.nextCoverPlan)return;v.nextCoverPlan=this.elapsed+5;
  const infantry=this.soldiers.filter(s=>s.alive&&s.team===v.team&&s.squad===driver.squad&&s.vehicleId===null&&dist(s.pos,v.pos)<130&&dist(s.pos,this.map.bases[v.team])>55&&dist(s.pos,objective)+12<dist(v.pos,objective)).sort((a,b)=>dist(a.pos,objective)-dist(b.pos,objective))[0];
  if(!infantry){v.coverAt=null;return;}
  const base=this.map.bases[v.team],forward=norm({x:objective.x-base.x,z:objective.z-base.z}),side={x:forward.z,z:-forward.x};
  let nav=this.vehicleNav.get(v.kind);if(!nav){nav=new Navigation(VEHICLE_TYPES[v.kind].radius,true,this.map);this.vehicleNav.set(v.kind,nav);}
  const radius=VEHICLE_TYPES[v.kind].radius;
  const candidates=[0,-12,12,-24,24].map(offset=>({x:infantry.pos.x-forward.x*19+side.x*offset,z:infantry.pos.z-forward.z*19+side.z*offset}));
  const next=candidates.find(p=>!this.map.collides(p.x,p.z,radius)&&!this.vehicles.some(o=>o!==v&&o.alive&&o.altitude<5&&dist(o.pos,p)<radius+VEHICLE_TYPES[o.kind].radius+2)&&nav!.find(v.pos,p).length>0)??null;
  if(next&&(!v.coverAt||dist(v.coverAt,next)>12)){v.route=[];v.nextPath=0;}
  v.coverAt=next;
 }

 private vehicleAction(v:ArmoredVehicle,dt:number){
  const push=this.eventForTeam(v.team,'ARMORED_PUSH'),pushTarget=push?this.points.find(p=>p.id===push.objective)??null:null;const driver=this.soldiers[v.occupants[0]??-1],assigned=driver?this.squadOrders.get(SQUAD_KEY(v.team,driver.squad)):undefined;let destination=pushTarget??this.points.find(p=>p.id===assigned?.objective)??this.combatObjectives[0]!;v.goal=destination.id;this.planVehicleCover(v,driver,destination);
  let enemyVehicle:ArmoredVehicle|null=null,vd:number=VEHICLE_TYPES[v.kind].weaponRange;
  for(const e of this.vehicles){if(!e.alive||e.airborne||e.team===v.team)continue;const d=dist(v.pos,e.pos);if(d<vd&&!this.sightBlocked(v.pos,e.pos,undefined,1.2)){vd=d;enemyVehicle=e}}
  let enemy:Soldier|null=null,sd=Math.min(145,vd);
  for(const s of this.soldiers){if(!s.alive||s.vehicleId!==null||s.team===v.team)continue;const d=dist(v.pos,s.pos);if(d<sd&&!this.sightBlocked(v.pos,s.pos)){sd=d;enemy=s}}
  const air=this.vehicles.filter(e=>e.alive&&e.altitude>10&&e.team!==v.team&&dist(e.pos,v.pos)<VEHICLE_TYPES[v.kind].weaponRange&&!this.sightBlocked(v.pos,e.pos)).sort((a,b)=>dist(a.pos,v.pos)-dist(b.pos,v.pos))[0];if(v.kind==='aa'&&air)enemyVehicle=air;const target=enemyVehicle?.pos??enemy?.pos; if(target&&v.kind!=='motorcycle'){this.intelligence[v.team].receive({source:v.occupants[0]??-1,squad:0,kind:'ENEMY_OBSERVED',subject:enemyVehicle?`vehicle:${enemyVehicle.id}`:`soldier:${enemy!.id}`,at:target,time:this.elapsed,confidence:.8});this.aimTurret(v,Math.atan2(target.x-v.pos.x,target.z-v.pos.z),Math.atan2((enemyVehicle?.altitude??0)+this.map.heightAt(target.x,target.z)+1.7-this.map.heightAt(v.pos.x,v.pos.z)-v.altitude-3.1,dist(v.pos,target)),dt);if(this.elapsed>=v.nextShot){v.nextShot=this.elapsed+VEHICLE_TYPES[v.kind].weaponInterval+this.random.between(.45,.9);const hit=this.random.next()<(enemyVehicle ? .6 : .48);const impact=hit?target:{x:target.x+this.random.between(-7,7),z:target.z+this.random.between(-7,7)};const d=dist(v.pos,impact),mg=v.kind==='scout'||v.kind==='transport',speed=AMMUNITION[v.kind].speed,flight=d/speed,dy=this.map.heightAt(impact.x,impact.z)+(enemyVehicle?.altitude??0)+1.4-this.map.heightAt(v.pos.x,v.pos.z)-v.altitude-3.1+(mg?0:AMMUNITION[v.kind].gravity*.5*flight*flight);const length=Math.hypot(d,dy)||1;this.fireVehicleWeapon(v,{x:(impact.x-v.pos.x)/length,y:dy/length,z:(impact.z-v.pos.z)/length},mg,false);}}
  const destinationAt=v.coverAt??destination;v.movingToGoal=['transport','motorcycle'].includes(v.kind)?dist(v.pos,destination)>25:target?dist(v.pos,target)>(v.kind==='tank'?85:v.kind==='aa'?180:40):dist(v.pos,destinationAt)>18;
 }

 private checkFinish(){
  if(this.finished)return;
  if(this.settings.mode==='breakthrough'){if(this.sectorIndex<5&&this.tickets.blue>0)return;this.finished=true;this.winner=this.sectorIndex>=5?'blue':'red';this.emit({type:'end',winner:this.winner});return;}if(this.tickets.blue>0&&this.tickets.red>0)return;
  this.finished=true;
  if(this.tickets.blue!==this.tickets.red)this.winner=this.tickets.blue>this.tickets.red?'blue':'red';
  else {const held=(team:Team)=>this.points.filter(p=>p.owner===team).length,blueHeld=held('blue'),redHeld=held('red');
   if(blueHeld!==redHeld)this.winner=blueHeld>redHeld?'blue':'red';else {const blueKills=this.soldiers.filter(s=>s.team==='blue').reduce((n,s)=>n+s.kills,0),redKills=this.soldiers.filter(s=>s.team==='red').reduce((n,s)=>n+s.kills,0);this.winner=blueKills>=redKills?'blue':'red'}}
  this.emit({type:'end',winner:this.winner});
 }

 advancePlayerTimers(){
  if(this.player.alive&&this.ammoBoxes.some(b=>b.team===this.player.team&&b.expires>this.elapsed&&dist(this.player.pos,b.at)<7&&!this.map.lineBlocked(this.player.pos,b.at)))for(const id of [this.loadout.primary,this.loadout.secondary])this.reserve[id]=WEAPONS[id].reserve;
  if(this.player.alive&&this.elapsed>=this.nextObjectiveReward&&this.points.some(p=>dist(p,this.player.pos)<25&&(p.owner!==this.player.team||p.contested))){this.awardRP(RP_REWARDS.objectiveTick,'objective participation');this.nextObjectiveReward=this.elapsed+5}
  if(this.playerVehicleId!==null&&(this.player.vehicleId!==this.playerVehicleId||!this.vehicles.find(v=>v.id===this.playerVehicleId)?.alive)){this.playerVehicleId=null;this.playerSeat=0;}
  if(this.playerReloadUntil>0&&this.elapsed>=this.playerReloadUntil&&this.reloadingWeapon){const weapon=this.reloadingWeapon,def=WEAPONS[weapon],need=def.magazine-this.ammo[weapon],take=Math.min(need,this.reserve[weapon]);this.ammo[weapon]+=take;this.reserve[weapon]-=take;this.playerReloadUntil=0;this.reloadingWeapon=null}
 }
 tick(dt:number){
  if(this.finished)return;dt=clamp(dt,0,.15);this.elapsed+=dt;
  this.advancePlayerTimers();
  if(this.beacon&&this.elapsed>=this.beacon.expires)this.beacon=null;this.updateProjectiles(dt);this.updateSupports();if(this.settings.aiEnabled!==false)this.updateMajorEvent();this.commanderAcc+=dt;if(this.commanderAcc>=({easy:6,normal:4,hard:3}[this.settings.difficulty])){this.commanderAcc=0;this.assignSquads()}
  if(this.elapsed>=this.squadPlanAt){this.squadPlanAt=this.elapsed+1;this.updateSquadReports();}
  this.updateFieldEquipment(dt);
  for(const s of this.soldiers){
   if(s.downedUntil>0){if(this.elapsed>=s.downedUntil)this.eliminate(s,this.soldiers.find(o=>o.id===s.downedBy)??null);else continue;}
   if(s.alive&&s.hp<100&&s.vehicleId===null&&s.combatUntil<=this.elapsed)s.hp=Math.min(100,s.hp+dt*25);
   if(s.parachuting){s.altitude=Math.max(0,s.altitude-dt*5);if(s.altitude===0)s.parachuting=false;}
   if(!s.alive){if(this.settings.aiEnabled!==false&&!s.player&&this.elapsed>=s.respawnAt){this.placeAtSpawn(s);this.emit({type:'respawn',id:s.id})}continue}
   if(s.vehicleId!==null){const v=this.vehicles.find(v=>v.id===s.vehicleId);if(v?.alive){s.pos={...v.pos};s.yaw=v.yaw;s.velocity={x:Math.sin(v.yaw)*v.speed,z:Math.cos(v.yaw)*v.speed};s.state='DRIVING';continue;}s.vehicleId=null;}if(s.player)continue;
   const crewLimit=Math.max(2,Math.min(10,Math.floor(this.settings.size*.35))),crewCount=this.soldiers.filter(o=>o.alive&&!o.player&&o.team===s.team&&o.vehicleId!==null).length;const push=this.eventForTeam(s.team,'ARMORED_PUSH');const rallyArmor=this.vehicles.find(v=>v.alive&&!v.airborne&&v.reservedFor===null&&v.team===s.team&&['tank','ifv'].includes(v.kind)&&v.occupants[0]===null&&this.elapsed>=v.nextDecision&&s.boardingUntil<=this.elapsed&&this.soldiers.filter(o=>o.alive&&!o.player&&o.team===s.team&&o.vehicleId===null&&o.boardingUntil<=this.elapsed).sort((a,b)=>dist(a.pos,v.pos)-dist(b.pos,v.pos)||a.id-b.id)[0]?.id===s.id&&this.nav.find(s.pos,v.pos).length>0);const boarding=crewCount<crewLimit?(rallyArmor??this.vehicles.find(v=>v.alive&&!v.airborne&&v.reservedFor===null&&v.team===s.team&&v.occupants[0]===null&&this.elapsed>=v.nextDecision&&s.boardingUntil<=this.elapsed&&dist(s.pos,v.pos)<55&&!this.soldiers.some(o=>o.id<s.id&&o.alive&&!o.player&&o.vehicleId===null&&o.team===s.team&&dist(o.pos,v.pos)<55))):undefined;
   if(boarding){const intent=this.setBotIntent(s,'MOVE_WITH_SQUAD','BOARD_VEHICLE',boarding.pos,push?8.6:6,'前往小队载具',boarding.id);this.moveBot(s,intent.destination,dt,intent.speed);continue;}
   if(this.elapsed<s.nextAiAt)continue;
   const fromPlayer=dist(s.pos,this.player.pos),difficultyRate={easy:1.28,normal:1,hard:.82}[this.settings.difficulty];const interval=(fromPlayer<120 ? .05 : fromPlayer<300 ? .125 : .40)*difficultyRate*(this.settings.aiProfile==='elite'?.8:1);const aiDt=clamp(this.elapsed-s.lastAiAt,.03,.45);s.lastAiAt=this.elapsed;s.nextAiAt=this.elapsed+interval+this.random.between(0,interval*.12);const old={...s.pos};const casualty=this.rescueTarget(s);if(casualty&&s.classId==='medic'){const intent=this.setBotIntent(s,'ASSIST_TEAMMATE','REVIVE',casualty.pos,8.6,'救援倒地队友',casualty.id);if(dist(s.pos,casualty.pos)>3)this.moveBot(s,intent.destination,aiDt,intent.speed);else{casualty.downedUntil=0;casualty.rescueCalled=false;casualty.alive=true;casualty.hp=55;casualty.spawnGraceUntil=this.elapsed+2;this.rescueCount++;this.emit({type:'revive',id:casualty.id});}s.velocity={x:(s.pos.x-old.x)/aiDt,z:(s.pos.z-old.z)/aiDt};continue;}this.botEquipment(s);this.botAction(s,aiDt);s.velocity={x:(s.pos.x-old.x)/aiDt,z:(s.pos.z-old.z)/aiDt};
  }
  for(const v of this.vehicles){
   if(!v.alive){v.enginePower*=Math.exp(-dt*2);v.altitude=Math.max(0,v.altitude-dt*10);if(this.elapsed>=v.respawnAt)this.resetVehicle(v);continue}
   if(v.airborne){v.speed=0;if(this.elapsed>=v.landAt){if(!this.map.collides(v.pos.x,v.pos.z,VEHICLE_TYPES[v.kind].radius)&&!this.vehicles.some(o=>o!==v&&o.alive&&!o.airborne&&dist(o.pos,v.pos)<VEHICLE_TYPES[o.kind].radius+VEHICLE_TYPES[v.kind].radius)){v.airborne=false;this.emit({type:'vehicleRespawn',team:v.team,id:v.id});}else v.landAt=this.elapsed+1;}continue;}
   this.updateVehicleLock(v,dt);const driver=this.soldiers.find(s=>s.id===v.occupants[0]&&s.alive&&s.vehicleId===v.id);this.updateFlightPhysics(v,dt,!!driver);if(!v.alive)continue;
   if(!driver){v.driver=null;v.occupants[0]=null;if(v.kind!=='helicopter'&&v.kind!=='jet')v.speed=0;if(v.altitude<2&&v.reservedFor===null&&this.elapsed>=v.nextDecision){const crewCount=this.soldiers.filter(s=>s.alive&&!s.player&&s.team===v.team&&s.vehicleId!==null).length;const candidate=crewCount<Math.max(2,Math.min(10,Math.floor(this.settings.size*.35)))?this.soldiers.filter(s=>s.alive&&!s.player&&s.team===v.team&&s.vehicleId===null&&s.boardingUntil<=this.elapsed&&dist(s.pos,v.pos)<VEHICLE_TYPES[v.kind].radius+5).sort((a,b)=>dist(a.pos,v.pos)-dist(b.pos,v.pos))[0]:undefined;if(candidate){candidate.vehicleId=v.id;candidate.pos={...v.pos};candidate.state='DRIVING';v.occupants[0]=candidate.id;v.driver='ai';} }continue;}
   if(v.occupants[1]===null&&v.kind!=='jet'&&v.reservedFor===null){const count=this.soldiers.filter(s=>s.team===v.team&&s.vehicleId!==null).length;const gunner=count<Math.floor(this.settings.size*.45)?this.soldiers.find(s=>s.alive&&!s.player&&s.team===v.team&&s.squad===driver.squad&&s.vehicleId===null&&s.boardingUntil<=this.elapsed&&dist(s.pos,v.pos)<12):undefined;if(gunner){v.occupants[1]=gunner.id;gunner.vehicleId=v.id;}}
   if(v.kind==='transport'&&v.reservedFor===null)for(let seat=2;seat<v.seatCount;seat++)if(v.occupants[seat]===null){const mate=this.soldiers.find(s=>s.alive&&!s.player&&s.team===v.team&&s.squad===driver.squad&&s.vehicleId===null&&s.boardingUntil<=this.elapsed&&dist(s.pos,v.pos)<12);if(mate){v.occupants[seat]=mate.id;mate.vehicleId=v.id;}}
   const gunner=this.soldiers.find(s=>s.id===v.occupants[1]&&s.alive);if(gunner&&!gunner.player&&v.kind!=='motorcycle'&&this.elapsed>=v.nextMGShot){const enemy=this.soldiers.find(s=>s.alive&&s.team!==v.team&&s.vehicleId===null&&dist(s.pos,v.pos)<100&&!this.sightBlocked(v.pos,s.pos));if(enemy){v.mgYaw=Math.atan2(enemy.pos.x-v.pos.x,enemy.pos.z-v.pos.z);const dir=norm({x:enemy.pos.x-v.pos.x,z:enemy.pos.z-v.pos.z});this.fireVehicleWeapon(v,{...dir,y:0},true,false);v.nextMGShot=this.elapsed+.32;}}
   if(!driver.player&&(v.warningUntil>this.elapsed||v.incomingUntil>this.elapsed))this.deployFlares(v);if(!driver.player&&v.lockProgress>=1&&this.elapsed>=v.nextSecondary)this.fireVehicleMissile(v,false);v.driver=driver.player?'player':'ai';if(driver.player){driver.pos={...v.pos};continue;}if(this.elapsed>=v.nextDecision){v.nextDecision=this.elapsed+.22;this.vehicleAction(v,.22);}const destination=this.points.find(p=>p.id===v.goal)??this.points[2]!;if(v.movingToGoal||v.kind==='jet')this.vehicleMove(v,v.coverAt??destination,dt);else v.speed*=Math.exp(-dt*2.4);driver.pos={...v.pos};if(['transport','motorcycle'].includes(v.kind)&&dist(v.pos,destination)<30){v.speed=0;for(let seat=0;seat<v.seatCount;seat++){const s=this.soldiers[v.occupants[seat]??-1];if(s&&!s.player&&this.displaceSeat(v,seat)){s.boardingUntil=this.elapsed+30;s.goal=destination.id;}}v.driver=v.occupants[0]===null?null:'player';v.nextDecision=this.elapsed+25;}
  }
  for(const v of this.vehicles)for(const id of v.occupants){const s=this.soldiers.find(s=>s.id===id);if(s?.alive)s.pos={...v.pos};}
  for(const p of this.combatObjectives){const change=updateCapture(p,this.soldiers.filter(s=>s.vehicleId===null||(this.vehicles.find(v=>v.id===s.vehicleId)?.altitude??0)<5),dt);if(change){this.emit({type:'capture',id:p.id,owner:change.after});if(change.after)this.recordCaptureMissions(p,change.after);if(change.after===this.player.team&&this.player.alive&&dist(this.player.pos,p)<25)this.awardRP(RP_REWARDS.capture,'capture')}}
  if(this.settings.mode==='breakthrough'){const active=this.points[this.sectorIndex];if(active?.owner==='blue'){this.sectorIndex++;this.tickets.blue=Math.min(this.settings.tickets,this.tickets.blue+40);if(this.activeMajorEvent)this.emit({type:'majorEvent',action:'end',kind:this.activeMajorEvent.kind,team:this.activeMajorEvent.team,objective:this.activeMajorEvent.objective});this.activeMajorEvent=null;this.nextMajorEventAt=Math.max(this.nextMajorEventAt,this.elapsed+25);this.assignSquads();}}
  else {this.tickets.blue=Math.max(0,this.tickets.blue-bleedRate(this.points,'blue')*dt);this.tickets.red=Math.max(0,this.tickets.red-bleedRate(this.points,'red')*dt);}
  if(this.elapsed>900){this.tickets.blue=Math.max(0,this.tickets.blue-.28*dt);if(this.settings.mode!=='breakthrough')this.tickets.red=Math.max(0,this.tickets.red-.28*dt)}
  this.checkFinish();
 }

 getSquadTargets():{team:Team;squad:number;objective:string;leaderId:number;intent:SquadIntent;approach:Point}[]{return [...this.squadOrders.values()].map(o=>({team:o.team,squad:o.squad,objective:o.objective,leaderId:o.leaderId,intent:o.intent,approach:{...o.approach}}))}
 getMajorEvent():ActiveMajorEvent|null{return this.activeMajorEvent?{...this.activeMajorEvent}:null}
 getNavigationFailures(){return this.navigationFailures}
 getAIDiagnostics(){const bots=this.soldiers.filter(s=>!s.player),botMinutes=Math.max(this.elapsed/60,1/60)*Math.max(1,bots.length),plans=this.getSquadTargets().map(o=>this.squadLeaderAI.get(SQUAD_KEY(o.team,o.squad))).filter(p=>p!==undefined);return {intentSwitches:this.intentSwitches,formationUpdates:this.formationUpdates,rescues:this.rescueCount,repairs:this.repairCount,coverRoleSwitches:plans.reduce((n,p)=>n+p.roleSwitches,0),profile:this.settings.aiProfile??'regular',destinationChangesPerMinute:bots.reduce((n,s)=>n+s.aiStats.destinationChanges,0)/botMinutes,stateChangesPerMinute:bots.reduce((n,s)=>n+s.aiStats.stateChanges,0)/botMinutes,movingWithoutProgress:bots.reduce((n,s)=>n+s.aiStats.movingWithoutProgress,0),idleWithoutReason:bots.reduce((n,s)=>n+s.aiStats.idleWithoutReason,0),squadCohesion:plans.reduce((n,p)=>n+p.cohesion,0)/Math.max(1,plans.length),navigationFailures:this.navigationFailures,missionCompletionRate:this.completedMissions/Math.max(1,this.assignedMissions)};}
}
