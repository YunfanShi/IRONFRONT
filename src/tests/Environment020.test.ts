import {describe,it,expect} from 'vitest';
import {soldierRayHit} from '../combat/Hitbox';
import {buildingPlan} from '../world/BuildingPlan';
import {MapContext} from '../world/Maps';
import {Navigation} from '../ai/Navigation';
import {Battle} from '../core/Battle';
import {createVehicle} from '../vehicles/Vehicle';
import {controlFlight} from '../vehicles/Flight';
describe('architectural collision and navigation contract',()=>{
 it('produces distinct structural styles with sensible floors without changing closed footprints',()=>{
  const map=new MapContext(),plans=map.definition.blocks.filter(b=>['house','factory','barracks'].includes(b.kind)).map(buildingPlan);
  expect(new Set(plans.map(p=>p.style)).size).toBeGreaterThanOrEqual(4);for(const p of plans){expect(p.floorHeight).toBeGreaterThan(2.5);expect(p.floorHeight).toBeLessThan(6.5);expect(p.floors).toBeGreaterThan(0);}
 });
 it('setback collision envelopes are generated from the same architectural volumes',()=>{
  const map=new MapContext();const b=map.definition.blocks.find(b=>buildingPlan(b).setback&&b.kind==='house')!;expect(b).toBeDefined();const p=buildingPlan(b);expect(p.upperWidth).toBeLessThan(b.w);expect(map.blocks.filter(v=>v.x===b.x&&v.z===b.z).map(v=>({w:v.w,d:v.d,h:v.h}))).toEqual(p.volumes.map(v=>({w:v.w,d:v.d,h:v.h})));expect(map.collides(b.x+b.w/2-.1,b.z,.1)).toBe(true);
 });
 it('dust rooms are accessible to both players and AI through a real entrance',()=>{
  const map=new MapContext('dust-horizon'),nav=new Navigation(1.12,false,map),outside={x:58,z:20},inside={x:63,z:-10};
  expect(map.collides(outside.x,outside.z,1.12)).toBe(false);expect(map.collides(inside.x,inside.z,1.12)).toBe(false);
  const route=nav.find(outside,inside);expect(route.length).toBeGreaterThan(0);let previous=outside;for(const p of route){expect(map.lineBlocked(previous,p,undefined,1.12)).toBe(false);previous=p;}
  expect(map.collides(58,-15,1.12)).toBe(true);expect(map.collides(58,0,1.12)).toBe(false);
 });
});
describe('head and airborne hit registration',()=>{
 it('recognizes the visible head and respects airborne altitude',()=>{
  expect(soldierRayHit({x:0,y:2.2,z:0},{x:0,y:0,z:1},{x:0,y:0,z:15})).not.toBeNull();
  expect(soldierRayHit({x:0,y:21.7,z:0},{x:0,y:0,z:1},{x:0,y:20,z:15})).not.toBeNull();
  expect(soldierRayHit({x:0,y:1.7,z:0},{x:0,y:0,z:1},{x:0,y:20,z:15})).toBeNull();
 });
 it('a visible head shot changes actual HP and a miss does not',()=>{
  const b=new Battle({size:8,difficulty:'easy',tickets:500,aiEnabled:false});for(const s of b.soldiers)s.alive=false;for(const v of b.vehicles)v.alive=false;
  b.player.alive=true;b.player.pos={x:0,z:0};b.playerAiming=true;const enemy=b.soldiers[8]!;enemy.alive=true;enemy.pos={x:0,z:15};enemy.altitude=0;
  const targetY=b.map.heightAt(0,15)+2.15,eyeY=b.map.heightAt(0,0)+2.15,dy=targetY-eyeY,len=Math.hypot(15,dy);b.shootPlayer({x:0,y:dy/len,z:15/len},2.15);expect(enemy.hp).toBeLessThan(100);expect(b.events().some(e=>e.type==='hitConfirmed')).toBe(true);
 });
});
describe('authoritative map selected support',()=>{
 it('rejects nonfinite and out of bounds targets without charging',()=>{const b=new Battle({size:8,difficulty:'easy',tickets:500});b.awardRP(1000,'test');for(const target of [{x:NaN,z:0},{x:1000,z:0},{x:0,z:Infinity}])expect(b.requestSupport('artillery',target).ok).toBe(false);expect(b.requisitionPoints).toBe(1000);});
 it('delivers a vehicle near the selected location rather than the caller',()=>{const b=new Battle({size:8,difficulty:'easy',tickets:500,aiEnabled:false});b.awardRP(1000,'test');expect(b.requestSupport('tank',{x:0,z:0}).ok).toBe(true);const v=b.vehicles.at(-1)!;expect(v.pos).toEqual({x:0,z:0});expect(v.airborne).toBe(true);expect(v.reservedFor).toBe(b.player.id);expect(b.supportFlights.at(-1)!.at).toEqual(v.pos);});
 it('four physical impact locations match the bomber mission targets',()=>{const b=new Battle({size:8,difficulty:'easy',tickets:500,aiEnabled:false});b.awardRP(1000,'test');expect(b.requestSupport('artillery',{x:0,z:0}).ok).toBe(true);const f=b.supportFlights[0]!,impacts=[];for(let i=0;i<120;i++){b.tick(.125);impacts.push(...b.events().filter(e=>e.type==='artilleryImpact'));}expect(impacts).toHaveLength(4);expect(impacts.map(e=>(e as any).at)).toEqual(f.targets);});
});
it('boosted bank input completes a barrel roll; ordinary input remains assisted',()=>{const v=createVehicle(999,'blue','jet',{x:0,z:0});for(let i=0;i<180;i++)controlFlight(v,1,1,1/60,undefined,0,true);expect(v.roll).toBeGreaterThan(Math.PI*2);expect(Number.isFinite(v.yaw)).toBe(true);expect(v.speed).toBeGreaterThan(30);});
