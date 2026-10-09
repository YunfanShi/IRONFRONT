import {describe,it,expect} from 'vitest';
import {Navigation} from '../ai/Navigation';
import {resolveIntent,type AIIntentProposal} from '../ai/Intent';
import {SquadLeader,type MemberStatus} from '../ai/SquadLeader';
import {Battle} from '../core/Battle';
import {dist} from '../core/math';
import {BASES,collides,lineBlocked} from '../world/Layout';

describe('AI movement and squad decisions',()=>{
 it('routes around structures and rejects an unreachable destination',()=>{
  const nav=new Navigation(),from={x:-315,z:-170},to={x:-220,z:-170},route=nav.find(from,to);
  expect(lineBlocked(from,to,undefined,1.12)).toBe(true);
  expect(route.length).toBeGreaterThan(1);
  for(let i=0;i<route.length;i++){
   expect(collides(route[i]!.x,route[i]!.z,1.12)).toBe(false);
   expect(lineBlocked(i?route[i-1]!:from,route[i]!,undefined,1.12)).toBe(false);
  }
  expect(nav.find(from,{x:-269,z:-170})).toEqual([]);
 });

 it('reports a blocked infantry task and enters a short recovery intent without moving through a wall',()=>{
  const b=new Battle({size:8,difficulty:'normal',tickets:500,seed:2}),s=b.soldiers[1]!;
  s.pos={x:-315,z:-170};s.lastProgress={...s.pos};
  for(let i=0;i<7;i++){b.elapsed+=.4;(b as any).moveBot(s,{x:-269,z:-170},.4,8.6);}
  expect(s.pos).toEqual({x:-315,z:-170});
  expect(s.aiIntent?.kind).toBe('RECOVER_FROM_NAVIGATION_FAILURE');
  expect(s.aiStats.navigationFailures).toBe(1);
  expect(b.intelligence.blue.snapshot(b.elapsed).some(r=>r.kind==='NAVIGATION_BLOCKED')).toBe(true);
 });

 it('keeps noisy hearing estimates inside reachable map space',()=>{
  const b=new Battle({size:8,difficulty:'normal',tickets:500,seed:12}),s=b.soldiers[1]!;
  for(const e of b.soldiers)if(e.team==='red')e.alive=false;
  s.pos={x:-315,z:-170};s.lastProgress={...s.pos};s.lastSeen={x:-269,z:-170};s.lastSeenAt=.9;s.spawnGraceUntil=0;b.elapsed=1;
  (b as any).botAction(s,.1);
  expect(s.aiIntent?.kind).toBe('OBSERVE');
  expect(collides(s.aiIntent!.destination.x,s.aiIntent!.destination.z,1.12)).toBe(false);
  expect(new Navigation().find(s.pos,s.aiIntent!.destination).length).toBeGreaterThan(0);
 });

 it('commits to an intent, interrupts for urgency, and gives elite plans longer persistence',()=>{
  const at={x:0,z:0};
  const move:AIIntentProposal={kind:'MOVE_WITH_SQUAD',state:'MOVE_TO_OBJECTIVE',destination:{x:50,z:0},goal:'A',targetId:null,speed:8,reason:'advance'};
  const stop:AIIntentProposal={...move,kind:'HOLD_POSITION',state:'IDLE',destination:at,speed:0,reason:'wait'};
  const regular=resolveIntent(0,at,null,move,'regular').intent;
  expect(resolveIntent(1,at,regular,stop,'regular').intent).toBe(regular);
  expect(resolveIntent(1,at,regular,stop,'regular',true).intent.kind).toBe('HOLD_POSITION');
  const shift={...move,destination:{x:60,z:0}};
  expect(resolveIntent(3.2,at,regular,shift,'regular').changed).toBe(true);
  const elite=resolveIntent(0,at,null,move,'elite').intent;
  expect(resolveIntent(3.2,at,elite,shift,'elite').intent).toBe(elite);
  expect(resolveIntent(4.6,at,elite,shift,'elite').changed).toBe(true);
 });

 it('lets an elite follower track a moving formation slot without restarting its intent',()=>{
  const at={x:0,z:0},follow:AIIntentProposal={kind:'REGROUP',state:'FOLLOW_SQUAD',destination:{x:0,z:20},goal:'B',targetId:null,speed:8.2,reason:'formation'};
  const initial=resolveIntent(0,at,null,follow,'elite').intent;
  const update=resolveIntent(1,at,initial,{...follow,destination:{x:6,z:24}},'elite');
  expect(update.changed).toBe(false);
  expect(update.intent.since).toBe(0);
  expect(update.intent.destination).toEqual({x:6,z:24});
  expect(resolveIntent(1,at,resolveIntent(0,at,null,follow,'regular').intent,{...follow,destination:{x:6,z:24}},'regular').intent.destination).toEqual(follow.destination);
 });

 it('keeps formation facing the mission and resumes after regroup timeout',()=>{
  const ai=new SquadLeader(),objective={x:0,z:100};
  const members:MemberStatus[]=[0,1,2,3].map(id=>({id,pos:{x:id*2,z:0},hp:100,alive:true,player:false,vehicleId:null,visualTarget:null,stuckFor:0}));
  const first=ai.plan('blue:0',0,members,0,objective,false);
  const next=ai.plan('blue:0',1,members.map(m=>({...m,pos:{x:m.pos.x+1,z:m.pos.z+2}})),0,objective,false);
  expect(next.heading).toBeCloseTo(first.heading,1);
  expect(dist(first.memberDestinations[1]!,first.memberDestinations[2]!)).toBeGreaterThan(6);
  const split=members.map((m,i)=>({...m,pos:i?{x:120+i*5,z:0}:m.pos}));
  const wait=ai.plan('blue:0',2,split,0,objective,false);
  expect(wait.holdLeader).toBe(true);
  const resume=ai.plan('blue:0',11,split,0,objective,false);
  expect(resume.holdLeader).toBe(false);
  expect(resume.regroupTimedOut).toBe(true);
 });

 it('positions armored support behind infantry only after the infantry advances',()=>{
  const b=new Battle({size:8,difficulty:'normal',tickets:500,seed:9}),v=b.vehicles.find(v=>v.team==='blue'&&v.kind==='ifv')!;
  for(const other of b.vehicles)if(other!==v)other.alive=false;
  const driver=b.soldiers[1]!,infantry=b.soldiers[2]!,objective=b.points.find(p=>p.id==='B')!;
  v.occupants[0]=driver.id;driver.vehicleId=v.id;v.pos={x:-120,z:-240};
  infantry.pos={...BASES.blue};
  (b as any).planVehicleCover(v,driver,objective);
  expect(v.coverAt).toBeNull();
  infantry.pos={x:-95,z:-220};v.nextCoverPlan=0;
  (b as any).planVehicleCover(v,driver,objective);
  expect(v.coverAt).not.toBeNull();
  expect(dist(v.coverAt!,objective)).toBeGreaterThan(dist(infantry.pos,objective));
  expect(new Navigation(3.15,true).find(v.pos,v.coverAt!).length).toBeGreaterThan(0);
 });

 it('credits a capture mission only to an assigned squad actually at the flag',()=>{
  const b=new Battle({size:8,difficulty:'normal',tickets:500,seed:29});
  const order=b.getSquadTargets().find(o=>o.team==='blue'&&o.intent!=='DEFEND')!,point=b.points.find(p=>p.id===order.objective)!;
  for(const member of b.soldiers)if(member.team==='blue'&&member.squad===order.squad)member.pos={...BASES.blue};
  const before=(b as any).completedMissions;
  (b as any).recordCaptureMissions(point,'blue');
  expect((b as any).completedMissions).toBe(before);
  b.soldiers.find(s=>s.team==='blue'&&s.squad===order.squad&&!s.player)!.pos={x:point.x,z:point.z};
  (b as any).recordCaptureMissions(point,'blue');
  (b as any).recordCaptureMissions(point,'blue');
  expect((b as any).completedMissions).toBe(before+1);
 });

 it.each(['regular','elite'] as const)('%s squads complete a live autonomous battle',profile=>{
  const b=new Battle({size:16,difficulty:'normal',aiProfile:profile,tickets:36,seed:102});
  let engaged=false,captured=false;
  for(let i=0;i<40000&&!b.finished;i++){
   b.tick(.125);
   if(b.soldiers.some(s=>!s.player&&s.state==='ENGAGE'))engaged=true;
   if(b.events().some(e=>e.type==='capture'))captured=true;
  }
  const metrics=b.getAIDiagnostics();
  expect(b.finished).toBe(true);
  expect(engaged).toBe(true);
  expect(captured).toBe(true);
  expect(metrics.missionCompletionRate).toBeGreaterThan(0);
  expect(metrics.navigationFailures).toBeLessThan(10);
 });
});

describe('anti-air balance',()=>{
 it('requires two direct handheld AA hits to destroy a helicopter and enforces acquisition range',()=>{
  const b=new Battle({size:8,difficulty:'normal',tickets:500,seed:33});
  for(const s of b.soldiers)if(!s.player){s.alive=false;s.respawnAt=Infinity;}
  for(const v of b.vehicles){v.alive=false;v.respawnAt=Infinity;}
  b.setLoadout({classId:'engineer'});b.player.pos={x:0,z:0};
  const heli=b.vehicles.find(v=>v.team==='red'&&v.kind==='helicopter')!;heli.alive=true;heli.pos={x:0,z:330};heli.altitude=30;
  expect(b.getAATarget({x:0,y:.447,z:.894})).toBeNull();
  heli.pos={x:0,z:60};
  const aim={x:0,y:.447,z:.894};
  expect(b.fireAA(aim)).toBe(true);
  for(let i=0;i<130;i++)b.tick(.025);
  expect(heli.alive).toBe(true);
  expect(heli.hp).toBe(heli.maxHp-135);
  heli.altitude=30;
  expect(b.fireAA(aim)).toBe(true);
  for(let i=0;i<130&&!b.finished;i++)b.tick(.025);
  expect(heli.alive).toBe(false);
 });
});
