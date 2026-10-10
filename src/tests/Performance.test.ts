import {describe,it,expect} from 'vitest';
import {Navigation} from '../ai/Navigation';
import {lineBlocked} from '../world/Layout';
import baseline from './fixtures/performance-018-baseline.json';

describe('performance changes preserve frozen 0.18 gameplay geometry',()=>{
 it('matches infantry and tank routes including A* tie-breaking',()=>{
  const infantry=new Navigation(),tank=new Navigation(3.5,true);
  for(const r of baseline.routes)expect((r.vehicle?tank:infantry).find(r.from,r.to)).toEqual(r.path);
 });
 it('keeps returned paths independent of reusable navigation buffers',()=>{
  const r=baseline.routes.find(r=>!r.vehicle&&r.path.length>1)!;const n=new Navigation();const first=n.find(r.from,r.to);first[0]!.x+=1000;
  n.find({x:0,z:0},{x:350,z:350});expect(n.find(r.from,r.to)).toEqual(r.path);
 });
 it('matches blocked, clear, parallel and degenerate segments with padding',()=>{
  for(const s of baseline.segments)expect(lineBlocked(s.a,s.b,undefined,s.pad)).toBe(s.blocked);
 });
});
