import type {Point} from '../core/math';
import type {Team} from '../world/Layout';

export interface ArmoredVehicle {
 id:number;
 kind:VehicleKind;spawn:Point;requisitioned:boolean;
 team:Team;
 pos:Point;
 yaw:number;
 turretYaw:number;
 hp:number;
 maxHp:number;
 alive:boolean;
 disabledAt:number;
 respawnAt:number;
 speed:number;
 driver:'ai'|'player'|null;
 goal:string;
 route:Point[];
 nextPath:number;
 nextDecision:number;
 nextShot:number;
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
 respawnDelay:24,
} as const;

export const VEHICLE_SPAWNS:{blue:Point;red:Point}={
 blue:{x:-304,z:-302},
 red:{x:304,z:302},
};

export type VehicleKind='scout'|'ifv'|'tank';
export const VEHICLE_TYPES={
 scout:{...VEHICLE,name:'R4 SCOUT',maxHp:180,radius:2.1,maxForward:22,maxReverse:9,acceleration:13,turnRate:1.6,weaponDamage:22,weaponInterval:.22,weaponRange:110,respawnDelay:18},
 ifv:{...VEHICLE,name:'V12 LANCER',maxHp:420},
 tank:{...VEHICLE,name:'T90 BASTION',maxHp:820,radius:3.5,maxForward:10,maxReverse:4,acceleration:5,turnRate:.7,weaponDamage:145,weaponInterval:2.4,weaponRange:230,respawnDelay:40}
} as const;
export function createVehicle(id:number,team:Team,kind:VehicleKind,spawn:Point,requisitioned=false):ArmoredVehicle {
 const cfg=VEHICLE_TYPES[kind];
 return {id,team,kind,spawn:{...spawn},requisitioned,pos:{...spawn},yaw:team==='blue'?0:Math.PI,turretYaw:team==='blue'?0:Math.PI,hp:cfg.maxHp,maxHp:cfg.maxHp,alive:true,disabledAt:-999,respawnAt:0,speed:0,driver:team==='blue'?null:'ai',goal:'C',route:[],nextPath:0,nextDecision:team==='blue'?45:.5,nextShot:0,stuckFor:0,lastProgress:{...spawn}};
}
export function createArmoredVehicles():ArmoredVehicle[]{
 // Preserve the original IFV IDs. Extra vehicles use separate parking bays.
 const vehicles=(['blue','red'] as Team[]).map((team,id)=>createVehicle(id,team,'ifv',VEHICLE_SPAWNS[team]));
 for(const team of ['blue','red'] as Team[])for(const [kind,offset] of [['scout',12],['tank',24]] as const){const p=VEHICLE_SPAWNS[team];vehicles.push(createVehicle(vehicles.length,team,kind,{x:p.x+(team==='blue'?offset:-offset),z:p.z}));}
 return vehicles;
}
