import {describe,it,expect} from 'vitest';
import {Battle} from '../core/Battle';import {LanClient} from '../network/LanClient';import {dist} from '../core/math';
describe('LAN authority correction',()=>{
 it('spreads a small correction across frames with a two metre per second bound',()=>{const client=new LanClient(),b=new Battle({size:8,aiEnabled:false,difficulty:'easy',tickets:500});b.player.pos={x:0,z:0};(client as any).correction={x:2,z:1};const original={...b.player.pos};for(let i=0;i<20;i++){const old={...b.player.pos};client.reconcile(b,.065);expect(dist(old,b.player.pos)).toBeLessThanOrEqual(.1300001);}expect(dist(original,b.player.pos)).toBeGreaterThan(1);expect(dist(b.player.pos,{x:2,z:1})).toBeLessThan(.1);});
 it('does not correct an incapacitated player or a vehicle occupant',()=>{const client=new LanClient(),b=new Battle({size:8,aiEnabled:false,difficulty:'easy',tickets:500});(client as any).correction={x:2,z:1};const old={...b.player.pos};b.player.alive=false;client.reconcile(b,.065);expect(b.player.pos).toEqual(old);b.player.alive=true;(b as any).playerVehicleId=0;b.player.vehicleId=0;client.reconcile(b,.065);expect(b.player.pos).toEqual(old);});
});
