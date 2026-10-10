import {describe,it,expect} from 'vitest';
import {Battle} from '../core/Battle';
import {MapContext,MAPS,type MapId} from '../world/Maps';
import {Navigation} from '../ai/Navigation';
import {BASES,OBJECTIVES} from '../world/Layout';
import {VEHICLE_TYPES,VEHICLE_SPAWNS} from '../vehicles/Vehicle';
import {heightAt,dist} from '../core/math';

describe('per-match maps',()=>{
 it('defaults old settings to the unchanged industrial geography',()=>{
  const b=new Battle({size:8,difficulty:'easy',tickets:500});expect(b.map.size).toBe(720);expect(b.map.bases).toEqual(BASES);expect(b.map.objectives).toEqual(OBJECTIVES);expect(b.map.definition.parking).toEqual(VEHICLE_SPAWNS);
  for(const [x,z] of [[-300,200],[20,80],[300,-220]])expect(b.map.heightAt(x!,z!)).toBe(heightAt(x!,z!));
 });
 for(const id of Object.keys(MAPS) as MapId[])it(id+' has safe bases, parking, and connected infantry and tank routes',()=>{
  const map=new MapContext(id),foot=new Navigation(1.12,false,map),tank=new Navigation(3.5,true,map),b=new Battle({mapId:id,size:32,difficulty:'easy',tickets:500});
  for(const p of [...Object.values(map.bases),...map.objectives])expect(map.collides(p.x,p.z,3.5)).toBe(false);
  for(const v of b.vehicles){expect(map.collides(v.pos.x,v.pos.z,VEHICLE_TYPES[v.kind].radius)).toBe(false);expect(dist(v.pos,map.bases[v.team])).toBeLessThan(125);}
  for(const from of Object.values(map.bases))for(const to of map.objectives)for(const [nav,radius] of [[foot,1.12],[tank,3.5]] as const){const route=nav.find(from,to);expect(route.length).toBeGreaterThan(0);let a=from;for(const p of route){expect(map.collides(p.x,p.z,radius)).toBe(false);if(radius===1.12)expect(map.lineBlocked(a,p,undefined,radius)).toBe(false);else for(let t=0;t<=1;t+=.02)expect(map.collides(a.x+(p.x-a.x)*t,a.z+(p.z-a.z)*t,radius)).toBe(false);a=p;}}
 });
 it('runs two different maps simultaneously without cross-contamination',()=>{
  const a=new Battle({size:8,difficulty:'easy',tickets:500}),b=new Battle({mapId:'dust-horizon',size:8,difficulty:'easy',tickets:500});
  for(let i=0;i<80;i++){a.tick(.125);b.tick(.125);}expect(a.points[0]!.x).toBe(-201);expect(b.points[0]!.x).toBe(-270);expect(a.map.size).toBe(720);expect(b.map.size).toBe(950);
  a.player.pos={x:380,z:380};b.player.pos={x:380,z:380};expect(a.map.collides(380,380,1)).toBe(true);expect(b.map.collides(380,380,1)).toBe(false);expect(b.map.project({x:475,z:-475},100)).toEqual({x:100,y:0});
 });
 it('rejects unsupported maps instead of creating a divergent client battle',()=>{expect(()=>new MapContext('unknown' as MapId)).toThrow('Unsupported map');});
 it('uses dust context for spawning, projectile height and air boundaries',()=>{
  const b=new Battle({mapId:'dust-horizon',size:8,difficulty:'easy',tickets:500,aiEnabled:false});expect(b.getDeploymentLocations()[0]!.at).toEqual(b.map.bases.blue);expect(b.player.pos.x).toBeLessThan(-370);
  const jet=b.vehicles.find(v=>v.kind==='jet'&&v.team==='blue')!;jet.pos={x:430,z:0};jet.altitude=70;jet.occupants[0]=b.player.id;jet.driver='player';b.player.vehicleId=jet.id;(b as any).flyVehicle(jet,1,0,.1,0,0,false,false);
  expect(jet.pos.x).toBeGreaterThan(350);expect(jet.pos.x).toBeLessThanOrEqual(b.map.limit);
 });
});
