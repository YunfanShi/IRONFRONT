import {describe,it,expect} from 'vitest';
import {Battle} from '../core/Battle';import {SquadLeader,type MemberStatus} from '../ai/SquadLeader';import {dist} from '../core/math';
const members=():MemberStatus[]=>[0,1,2,3].map(id=>({id,pos:{x:id*2,z:0},hp:100,alive:true,player:false,vehicleId:null,visualTarget:8,stuckFor:0}));
describe('squad tactics',()=>{
 it('assigns opposing cover and advance roles and swaps them after a bound',()=>{const ai=new SquadLeader(),m=members(),a=ai.plan('b',1,m,0,{x:0,z:100},false),b=ai.plan('b',8,m,0,{x:0,z:100},false);expect(new Set(Object.values(a.roles)).size).toBe(2);for(const s of m)expect(a.roles[s.id]).not.toBe(b.roles[s.id]);expect(b.roleSwitches).toBe(1);});
 it('keeps formation slots after a casualty and hands command to a surviving member',()=>{const ai=new SquadLeader(),m=members(),a=ai.plan('b',1,m,0,{x:0,z:100},false);m[1]!.alive=false;const b=ai.plan('b',2,m,0,{x:0,z:100},false);expect(b.memberDestinations[2]).toEqual(a.memberDestinations[2]);m[0]!.alive=false;expect(ai.plan('b',3,m,0,{x:0,z:100},false).leaderId).toBe(2);});
 it('cover members really hold while advance members move and both can fire',()=>{
  const b=new Battle({size:8,difficulty:'normal',tickets:500,aiEnabled:false,seed:17});for(const s of b.soldiers)s.alive=false;for(const v of b.vehicles)v.alive=false;
  const m=b.soldiers.slice(4,8),enemy=b.soldiers[8]!;enemy.alive=true;enemy.pos={x:0,z:60};for(const s of m){s.alive=true;s.weaponId='carbine';s.pos={x:(s.id-5.5)*3,z:0};s.spawnGraceUntil=0;s.visualTarget=enemy.id;s.reactionUntil=0;s.nextShot=0;}
  b.elapsed=2;(b as any).squadLeaderAI.plan('blue-1',2,m,m[0]!.id,{x:0,z:100},false);const old=m.map(s=>({...s.pos}));for(const s of m)(b as any).botAction(s,.2);
  const plan=b.squadLeaderAI.get('blue-1')!;for(let i=0;i<m.length;i++)if(plan.roles[m[i]!.id]==='cover')expect(dist(m[i]!.pos,old[i]!)).toBe(0);expect(m.some((s,i)=>dist(s.pos,old[i]!)>.1)).toBe(true);expect(b.events().filter(e=>e.type==='shot').length).toBeGreaterThan(0);
  b.elapsed=9;const next=b.squadLeaderAI.plan('blue-1',9,m,m[0]!.id,{x:0,z:100},false);for(const s of m){s.nextShot=0;(b as any).botAction(s,.2);}expect(next.roleSwitches).toBe(1);
 });
});

describe('support duties return real outcomes',()=>{
 it('a medic approaches and revives a downed player using a reachable route',()=>{
  const b=new Battle({size:8,difficulty:'easy',tickets:500,seed:21});for(const s of b.soldiers){s.alive=false;s.respawnAt=Infinity;}for(const v of b.vehicles){v.alive=false;v.respawnAt=Infinity;}
  b.player.pos={x:-300,z:-302};b.player.hp=0;b.player.downedUntil=30;b.player.rescueCalled=true;
  const medic=b.soldiers[1]!;medic.alive=true;medic.pos={x:-290,z:-302};medic.lastProgress={...medic.pos};medic.spawnGraceUntil=0;
  for(let n=0;n<80&&!b.player.alive;n++)b.tick(.125);
  expect(b.player.alive).toBe(true);expect(b.player.hp).toBeGreaterThan(0);expect(dist(medic.pos,b.player.pos)).toBeLessThan(3.2);expect(b.events().some(e=>e.type==='revive')).toBe(true);
 });
 it('a medic skips an unreachable casualty instead of endlessly walking into a building',()=>{
  const b=new Battle({size:8,difficulty:'easy',tickets:500}),medic=b.soldiers[1]!,casualty=b.soldiers[2]!;medic.pos={x:-315,z:-170};casualty.alive=false;casualty.downedUntil=30;casualty.pos={x:-269,z:-170};
  expect((b as any).rescueTarget(medic)).toBeNull();
 });
 it('an engineer approaches nearby damaged armor and actually repairs it',()=>{
  const b=new Battle({size:8,difficulty:'easy',tickets:500,aiEnabled:false}),engineer=b.soldiers[3]!,v=b.vehicles[0]!;for(const other of b.vehicles)if(other!==v)other.alive=false;
  engineer.pos={x:-280,z:-302};v.pos={x:-300,z:-302};v.hp=150;engineer.spawnGraceUntil=0;engineer.nextEquipment=0;for(const enemy of b.soldiers)if(enemy.team==='red')enemy.alive=false;
  for(let n=0;n<40;n++){b.elapsed+=.125;(b as any).botAction(engineer,.125);(b as any).botEquipment(engineer);}
  expect(dist(engineer.pos,v.pos)).toBeLessThan(9);expect(v.hp).toBeGreaterThan(150);expect(b.getAIDiagnostics().repairs).toBeGreaterThan(0);
 });
});

describe('contact maneuvers have priority and visible outcomes',()=>{
 const scene=()=>{const b=new Battle({size:8,difficulty:'normal',tickets:500,aiEnabled:false,seed:73});for(const s of b.soldiers)s.alive=false;for(const v of b.vehicles)v.alive=false;const s=b.soldiers[4]!,enemy=b.soldiers[8]!;s.alive=enemy.alive=true;s.pos={x:0,z:0};s.lastProgress={...s.pos};s.spawnGraceUntil=0;s.visualTarget=enemy.id;s.reactionUntil=0;s.nextShot=0;enemy.pos={x:0,z:75};b.elapsed=2;return {b,s,enemy};};
 it('a flanking member moves laterally and fires after acquiring a real line of sight',()=>{const {b,s,enemy}=scene();s.weaponId='carbine';(b as any).squadOrders.set('blue-1',{team:'blue',squad:1,objective:'C',leaderId:s.id,intent:'FLANK',approach:{x:0,z:100}} as any);const at={...s.pos};(b as any).botAction(s,.2);expect(dist(s.pos,at)).toBeGreaterThan(.1);expect(Math.abs(s.aiIntent!.destination.x)).toBeGreaterThan(10);expect(b.events().some(e=>e.type==='shot'&&e.team===s.team)).toBe(true);expect(s.target).toBe(enemy.id);});
 it('low-health recon withdraws instead of being overridden by the sniper hold rule',()=>{const {b,s,enemy}=scene();s.classId='recon';s.weaponId='sniper';s.hp=18;const distance=dist(s.pos,enemy.pos);(b as any).botAction(s,.2);expect(s.state).toBe('RETREAT');expect(s.aiIntent?.kind).toBe('WITHDRAW');expect(dist(s.pos,enemy.pos)).toBeGreaterThan(distance);});
});

it('short-range medic equipment cannot revive a casualty through a solid wall',()=>{
 const b=new Battle({size:8,difficulty:'easy',tickets:500,aiEnabled:false}),medic=b.soldiers[1]!,victim=b.soldiers[2]!,wall=b.map.blocks.find(p=>p.kind==='factory')!;for(const s of b.soldiers)s.alive=false;medic.alive=true;medic.pos={x:wall.x-wall.w/2-1.5,z:wall.z};victim.pos={x:wall.x-wall.w/2+1,z:wall.z};victim.downedUntil=30;medic.nextEquipment=0;b.elapsed=1;
 (b as any).botEquipment(medic);expect(victim.alive).toBe(false);expect(victim.downedUntil).toBe(30);expect(b.events().some(e=>e.type==='revive')).toBe(false);
});
