import {describe,it,expect} from 'vitest';
import {Battle} from '../core/Battle';
import {WEAPONS,WEAPON_ORDER,validateLoadout} from '../combat/Weapons';
import {SUPPORTS,type SupportId} from '../gameplay/Requisition';
import {VEHICLE_TYPES} from '../vehicles/Vehicle';
const make=()=>new Battle({size:8,vehicleLimit:20,difficulty:'normal',tickets:500,seed:911});
const run=(b:Battle,seconds:number)=>{for(let i=0;i<seconds/.05;i++)b.tick(.05)};
function isolate(b:Battle){for(const s of b.soldiers)if(!s.player){s.pos={x:300,z:-300};s.nextAiAt=1e8;s.spawnGraceUntil=0}for(const v of b.vehicles)v.nextDecision=1e8;}
describe('Requisition transactions and state effects',()=>{
 it('rejects invalid, unaffordable, cooling down and dead-player requests without charging',()=>{
  const b=make();expect(b.requestSupport('recon','C').ok).toBe(false);b.awardRP(1000,'test');expect(b.requestSupport('fake' as SupportId,'C').ok).toBe(false);expect(b.requestSupport('recon','Z').ok).toBe(false);expect(b.requisitionPoints).toBe(1000);
  expect(b.requestSupport('recon','C').ok).toBe(true);expect(b.requisitionPoints).toBe(920);expect(b.requestSupport('recon','C').ok).toBe(false);b.player.alive=false;expect(b.requestSupport('smoke','C').ok).toBe(false);expect(b.requisitionPoints).toBe(920);
 });
 it('recon reveals regional contacts then expires and smoke blocks actual sight on all presets',()=>{
  const b=make();isolate(b);b.awardRP(1000,'test');const c=b.points[2]!;expect(b.hasReconContact(c)).toBe(false);b.requestSupport('recon','C');expect(b.hasReconContact(c)).toBe(true);expect(b.sightBlocked({x:-10,z:0},{x:10,z:0})).toBe(false);b.requestSupport('smoke','C');expect(b.sightBlocked({x:c.x-10,z:c.z},{x:c.x+10,z:c.z})).toBe(true);run(b,23);expect(b.hasReconContact(c)).toBe(false);expect(b.supports).toHaveLength(0);
 });
 it('artillery warns, damages infantry and armor and emits four real impacts',()=>{
  const b=make();isolate(b);b.awardRP(500,'test');b.requestSupport('artillery','C');const c=b.points[2]!,red=b.soldiers.find(s=>s.team==='red')!,armor=b.vehicles.find(v=>v.team==='red')!;red.pos={x:c.x,z:c.z};red.hp=1000;armor.pos={...red.pos};const hp=armor.hp;run(b,4.5);expect(red.hp).toBe(1000);expect(armor.hp).toBe(hp);run(b,10);expect(red.hp).toBeLessThan(1000);expect(armor.hp).toBeLessThan(hp);expect(b.events().filter(e=>e.type==='artilleryImpact')).toHaveLength(4);
 });
 it('rapid reinforcement accelerates allied AI respawns without inflating the army',()=>{
  const b=make();isolate(b);b.awardRP(500,'test');const blue=b.soldiers[1]!;blue.alive=false;blue.respawnAt=100;expect(b.requestSupport('reinforce','C').ok).toBe(true);run(b,1.2);expect(blue.alive).toBe(true);expect(b.soldiers).toHaveLength(16);expect(blue.morale).toBeGreaterThanOrEqual(.8);
 });
 it('summons three distinct vehicles, caps capacity and allows driving',()=>{
  const b=make();b.awardRP(2000,'test');for(const kind of ['scout','ifv','tank'] as const){expect(b.requestSupport(kind,'C').ok).toBe(true);const v=b.vehicles.at(-1)!;expect(v.kind).toBe(kind);expect(v.hp).toBe(VEHICLE_TYPES[kind].maxHp);expect(v.driver).toBe(null);b.player.pos={...v.pos};expect(b.togglePlayerVehicle()).toBe(false);expect(v.airborne).toBe(true);run(b,8.1);expect(v.airborne).toBe(false);expect(b.togglePlayerVehicle()).toBe(true);const old={...v.pos};b.drivePlayerVehicle(1,0,.5);expect(Math.hypot(v.pos.x-old.x,v.pos.z-old.z)).toBeGreaterThan(0);expect(b.togglePlayerVehicle()).toBe(true);}
  run(b,41);const before=b.requisitionPoints;expect(b.requestSupport('scout','C').ok).toBe(false);expect(b.requisitionPoints).toBe(before);
 });
});
describe('Loadout, equipment and scoring',()=>{
 it('validates persisted configuration and provides distinct weapon statistics',()=>{
  expect(validateLoadout({primary:'broken',secondary:'sniper',gadget:'broken',throwable:'bad'})).toEqual({classId:'assault',primary:'carbine',secondary:'marksman',gadget:'ammo',throwable:'frag'});expect(WEAPON_ORDER).toHaveLength(7);expect(new Set(WEAPON_ORDER.map(id=>WEAPONS[id].fireInterval)).size).toBe(7);
  const b=make();expect(b.setLoadout({primary:'smg',secondary:'pistol',gadget:'repair',throwable:'smoke'})).toBe(true);expect(b.playerAmmo).toBe(36);expect(b.switchPlayerWeapon('sniper')).toBe(false);expect(b.switchPlayerWeapon('pistol')).toBe(true);expect(b.playerAmmo).toBe(15);run(b,.1);expect(b.setLoadout({primary:'sniper'})).toBe(false);
 });
 it('awards kills and defense, prevents duplicate kills, and preserves independent ammo',()=>{
  const b=make();isolate(b);b.player.pos={x:0,z:0};b.playerAiming=true;const red=b.soldiers.find(s=>s.team==='red')!;red.pos={x:0,z:10};red.hp=20;const point=b.points[2]!;point.x=0;point.z=0;point.owner='blue';point.control=100;
  expect(b.shootPlayer({x:0,y:-.063,z:.998})).toBe(true);expect(red.alive).toBe(false);expect(b.requisitionPoints).toBe(90);run(b,.2);b.shootPlayer({x:0,y:-.063,z:.998});expect(b.requisitionPoints).toBe(90);expect(b.playerAmmo).toBe(28);b.switchPlayerWeapon('marksman');expect(b.playerAmmo).toBe(12);
 });
 it('awards real capture participation and player assists',()=>{
  const b=make();isolate(b);b.player.pos={x:0,z:0};b.playerAiming=true;const red=b.soldiers.find(s=>s.team==='red')!;red.pos={x:0,z:10};b.shootPlayer({x:0,y:-.063,z:.998});expect(red.hp).toBeLessThan(100);(b as unknown as {eliminate:(s:typeof red,k:typeof red)=>void}).eliminate(red,b.soldiers[1]!);expect(b.requisitionPoints).toBe(25);red.respawnAt=100;b.player.pos={x:b.points[2]!.x,z:b.points[2]!.z};run(b,8);expect(b.points[2]!.owner).toBe('blue');expect(b.requisitionPoints).toBeGreaterThanOrEqual(145);
 });
 it('medical and repair tools consume charges only when effective; grenades have a fuse',()=>{
  const b=make();b.setLoadout({classId:'medic'});isolate(b);expect(b.useGadget()).toBe(false);b.player.hp=40;expect(b.useGadget()).toBe(true);expect(b.player.hp).toBe(85);expect(b.gadgetCharges).toBe(1);expect(b.useGadget()).toBe(false);
  b.player.pos={x:0,z:0};const red=b.soldiers.find(s=>s.team==='red')!;red.pos={x:0,z:28};red.hp=100;expect(b.throwGrenade({x:0,z:1})).toBe(true);expect(b.grenadeCount).toBe(1);run(b,1);expect(red.alive).toBe(true);run(b,.6);expect(red.alive).toBe(false);
  const r=make();r.setLoadout({gadget:'repair'});const v=r.vehicles[0]!;r.player.pos={...v.pos};v.hp=100;expect(r.useGadget()).toBe(true);expect(v.hp).toBe(230);
 });
});
describe('Fair AI perception and shooting',()=>{
 it('waits for visual reaction and cannot shoot through smoke',()=>{
  const b=make();isolate(b);const blue=b.soldiers[1]!,red=b.soldiers.find(s=>s.team==='red')!;blue.pos={x:0,z:-10};blue.yaw=0;blue.nextAiAt=0;red.pos={x:0,z:10};run(b,.3);expect(b.events().filter(e=>e.type==='shot'&&!e.player)).toHaveLength(0);run(b,1.8);expect(b.events().some(e=>e.type==='shot'&&!e.player)).toBe(true);
  b.awardRP(500,'test');b.requestSupport('smoke','C');const c=b.points[2]!;blue.pos={x:c.x,z:c.z-10};red.pos={x:c.x,z:c.z+10};blue.route=[];blue.lastProgress={...blue.pos};b.events();run(b,1);expect(b.events().filter(e=>e.type==='shot'&&!e.player)).toHaveLength(0);
 });
 it('nearby wall-occluded contacts never get IFF labels',()=>{
  const b=make();b.player.pos={x:-315,z:-170};expect(b.canIdentify({x:-220,z:-170},150)).toBe(false);expect(b.canIdentify({x:-310,z:-170},35)).toBe(true);
 });
});

describe('Deployment and vehicle credit regressions',()=>{
 it('prevents duplicate weapon slots and early redeployment',()=>{const b=make();expect(b.setLoadout({primary:'marksman',secondary:'marksman'})).toBe(true);expect(b.loadout.secondary).toBe('pistol');b.player.alive=false;b.player.respawnAt=4.5;expect(b.respawnPlayer('BASE')).toBe(false);run(b,4.6);expect(b.respawnPlayer('BASE')).toBe(true);expect(b.playerWeapon).toBe('marksman');expect(b.grenadeCount).toBe(2);});
 it('credits a player armor kill once and summoned armor is not freely respawned',()=>{const b=make();isolate(b);const blue=b.vehicles[0]!,red=b.vehicles[1]!;blue.pos={x:0,z:0};red.pos={x:0,z:24};red.hp=10;b.player.pos={...blue.pos};expect(b.togglePlayerVehicle()).toBe(true);expect(b.shootPlayerVehicle({x:0,y:0,z:1})).toBe(true);expect(b.requisitionPoints).toBe(0);run(b,.4);expect(b.requisitionPoints).toBe(150);b.togglePlayerVehicle();b.awardRP(300,'test');expect(b.requestSupport('scout','C').ok).toBe(true);const summoned=b.vehicles.at(-1)!;(b as unknown as {damageVehicle:(v:typeof summoned,n:number,t:'red')=>void}).damageVehicle(summoned,1000,'red');expect(summoned.respawnAt).toBe(Infinity);run(b,50);expect(summoned.alive).toBe(false);});
});
