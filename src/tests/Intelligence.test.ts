import {describe,it,expect} from 'vitest';
import {Intelligence} from '../ai/Intelligence';
describe('observation boundary',()=>{
 it('stores copies, freezes lost contact, decays and expires',()=>{const i=new Intelligence(),p={x:2,z:3};i.receive({source:1,squad:0,kind:'ENEMY_OBSERVED',subject:'enemy:2',at:p,time:0,confidence:.8});p.x=99;expect(i.snapshot(0)[0]!.at.x).toBe(2);expect(i.snapshot(12)[0]!.confidenceNow).toBeCloseTo(.4);expect(i.snapshot(12)[0]!.status).toBe('last-known');expect(i.snapshot(25)).toHaveLength(0);});
 it('has no reports without an observation and throttles repeated source traffic',()=>{const i=new Intelligence();expect(i.snapshot(1)).toEqual([]);const r={source:1,squad:0,kind:'SOUND_HEARD' as const,subject:'sector',at:{x:0,z:0},time:1,confidence:.3};expect(i.receive(r)).toBe(true);expect(i.receive({...r,time:1.1})).toBe(false);expect(i.snapshot(10)).toEqual([]);});
});
