import type {Point} from '../core/math';
import type {Team} from '../world/Layout';

export interface ArmoredVehicle {
 id:number;
 kind:VehicleKind;seatCount:number;spawn:Point;requisitioned:boolean;
 team:Team;
 pos:Point;
 yaw:number;
 turretYaw:number;turretPitch:number;mgYaw:number;
 hp:number;combatUntil:number;weaponSlot:0|1;ammoMode:"armor"|"he";nextSecondary:number;lockTarget:number|null;lockProgress:number;flareUntil:number;nextFlare:number;warningUntil:number;
 maxHp:number;
 alive:boolean;
 disabledAt:number;
 respawnAt:number;
 speed:number;
 driver:'ai'|'player'|null;
 occupants:(number|null)[]; reservedFor:number|null; dropStarted:number; landAt:number; airborne:boolean;
 goal:string;movingToGoal:boolean;altitude:number;verticalVelocity:number;enginePower:number;climbInput:number;
 route:Point[];
 nextPath:number;
 nextDecision:number;
 nextShot:number;nextMGShot:number;steering:number;
 stuckFor:number;
 lastProgress:Point;
}

export const VEHICLE={
 maxHp:420,
 radius:3.15,
 maxForward:13.2,
 maxReverse:5.8,
 acceleration:8.4,
 braking:10.5,
 turnRate:1.08,
 weaponDamage:74,
 weaponInterval:1.15,
 weaponRange:175,
 respawnDelay:90,
} as const;

export const VEHICLE_SPAWNS:{blue:Point;red:Point}={
 blue:{x:-304,z:-302},
 red:{x:304,z:302},
};

export type VehicleKind='scout'|'ifv'|'tank'|'transport'|'aa'|'helicopter'|'jet';
export const VEHICLE_TYPES={
 aa:{...VEHICLE,name:'A40 WATCHTOWER',maxHp:320,weaponDamage:30,weaponInterval:.24,weaponRange:360,maxForward:16},
 helicopter:{...VEHICLE,name:'H8 KESTREL',maxHp:260,radius:3,maxForward:38,weaponDamage:45,weaponInterval:.45,weaponRange:250,turnRate:1.4},
 jet:{...VEHICLE,name:'J20 STRATUS',maxHp:220,radius:4,maxForward:65,weaponDamage:32,weaponInterval:.25,weaponRange:400,turnRate:.8},
 transport:{...VEHICLE,name:'U8 ROVER',maxHp:140,radius:1.85,maxForward:25,maxReverse:10,acceleration:15,turnRate:1.9,weaponDamage:18,weaponInterval:.12,weaponRange:100,respawnDelay:60},
 scout:{...VEHICLE,name:'R4 SCOUT',maxHp:180,radius:2.1,maxForward:22,maxReverse:9,acceleration:13,turnRate:1.6,weaponDamage:22,weaponInterval:.22,weaponRange:110,respawnDelay:70},
 ifv:{...VEHICLE,name:'V12 LANCER',maxHp:420},
 tank:{...VEHICLE,name:'T90 BASTION',maxHp:820,radius:3.5,maxForward:10,maxReverse:4,acceleration:5,turnRate:.7,weaponDamage:145,weaponInterval:2.4,weaponRange:230,respawnDelay:120}
} as const;
export function createVehicle(id:number,team:Team,kind:VehicleKind,spawn:Point,requisitioned=false):ArmoredVehicle {
 const cfg=VEHICLE_TYPES[kind];
 return {id,team,kind,seatCount:kind==='jet'?1:kind==='scout'?2:4,spawn:{...spawn},requisitioned,pos:{...spawn},yaw:team==='blue'?0:Math.PI,turretYaw:team==='blue'?0:Math.PI,turretPitch:0,mgYaw:team==='blue'?0:Math.PI,hp:cfg.maxHp,combatUntil:0,weaponSlot:0,ammoMode:"armor",nextSecondary:0,lockTarget:null,lockProgress:0,flareUntil:0,nextFlare:0,warningUntil:0,maxHp:cfg.maxHp,alive:true,disabledAt:-999,respawnAt:0,speed:0,driver:null,occupants:Array(kind==='jet'?1:kind==='scout'?2:4).fill(null),reservedFor:requisitioned?0:null,dropStarted:0,landAt:0,airborne:false,goal:'C',movingToGoal:true,altitude:0,verticalVelocity:0,enginePower:0,climbInput:0,route:[],nextPath:0,nextDecision:.5,nextShot:0,nextMGShot:0,steering:0,stuckFor:0,lastProgress:{...spawn}};
}
export function createArmoredVehicles(_limit=14):ArmoredVehicle[]{
 // Preserve the original IFV IDs. Extra vehicles use separate parking bays.
 const vehicles=(['blue','red'] as Team[]).map((team,id)=>createVehicle(id,team,'ifv',VEHICLE_SPAWNS[team]));
 for(const team of ['blue','red'] as Team[])for(const [kind,offset] of [['scout',12],['tank',24],['transport',36],['aa',48],['helicopter',60],['jet',76]] as const){const p=VEHICLE_SPAWNS[team];vehicles.push(createVehicle(vehicles.length,team,kind,{x:p.x+(team==='blue'?offset:-offset),z:p.z}));}
 return vehicles;
}

/** Matches the rendered barrel endpoint, including hull scale and gun elevation. */
export function vehicleMuzzle(v:ArmoredVehicle,mg=false,coax=false):{x:number;y:number;z:number}{
 const scale=v.kind==='tank'?1.08:(v.kind==='ifv'||v.kind==='aa'||v.kind==='helicopter'||v.kind==='jet')?.96:v.kind==='scout'?.66:.7;
 if(coax){const yaw=v.turretYaw,pitch=v.turretPitch,forward=(3.35*Math.cos(pitch)-.1)*scale,side=.45*scale;return {x:v.pos.x+Math.sin(yaw)*forward+Math.cos(yaw)*side,y:(2.6+3.35*Math.sin(pitch))*scale+v.altitude,z:v.pos.z+Math.cos(yaw)*forward-Math.sin(yaw)*side};}
 const yaw=mg?v.mgYaw:v.turretYaw,pitch=v.turretPitch;
 const forward=mg?(1.775*Math.cos(pitch)-.25)*scale:(5.925*Math.cos(pitch)-.1)*scale;
 const side=mg?.8*(v.kind==='scout'?.65:v.kind==='transport'?.6:scale):v.kind==='aa'?-.55*scale:0;
 const y=mg?(3.4+.15*Math.cos(pitch)+1.775*Math.sin(pitch))*(v.kind==='scout'?.75:v.kind==='transport'?.6:scale):(2.6+5.925*Math.sin(pitch))*scale;
 return {x:v.pos.x+Math.sin(yaw)*forward+Math.cos(v.yaw)*side,y:y+v.altitude,z:v.pos.z+Math.cos(yaw)*forward-Math.sin(v.yaw)*side};
}

/** Distinct ammunition and traverse, shared by player and AI. */
export const AMMUNITION={
 tank:{name:'APFSDS',speed:420,gravity:.8,radius:2.4,armor:1.25},
 ifv:{name:'30mm HE',speed:180,gravity:4,radius:4,armor:.7},
 aa:{name:'35mm AIR',speed:280,gravity:1.5,radius:2.2,armor:.45},
 helicopter:{name:'70mm HE',speed:115,gravity:3,radius:6,armor:.85},
 jet:{name:'25mm AP',speed:330,gravity:1,radius:2,armor:.65},
 scout:{name:'7.62mm',speed:700,gravity:0,radius:0,armor:.1},
 transport:{name:'7.62mm',speed:700,gravity:0,radius:0,armor:.1}
} as const;
