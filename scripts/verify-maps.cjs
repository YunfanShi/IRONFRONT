// Fixed-seed autonomous acceptance for both maps and modes; no player input.
const fs=require('node:fs'),assert=require('node:assert/strict'),{performance}=require('node:perf_hooks');
const {Battle}=require('../.logic-build/core/Battle.js');
const results=[],supportChecks=[];
for(const mapId of ['industrial-frontier','dust-horizon']){
 const b=new Battle({mapId,size:8,difficulty:'easy',tickets:500,aiEnabled:false,seed:911});b.awardRP(9999,'map QA');const c=b.points[2];
 for(const id of ['recon','smoke','artillery','reinforce','scout','ifv','tank'])assert(b.requestSupport(id,'C').ok,mapId+' support '+id);
 assert(b.hasReconContact(c));assert(b.sightBlocked({x:c.x-10,z:c.z},{x:c.x+10,z:c.z}));const requested=b.vehicles.filter(v=>v.requisitioned);assert.equal(requested.length,3);assert(requested.every(v=>v.airborne&&v.reservedFor===b.player.id));
 const enemy=b.soldiers.find(s=>s.team==='red');enemy.alive=true;enemy.hp=100;enemy.pos={...c};enemy.spawnGraceUntil=0;enemy.nextAiAt=Infinity;const ally=b.soldiers[1];ally.alive=false;ally.respawnAt=100;
 let impacts=0;for(let n=0;n<200;n++){b.tick(.1);for(const e of b.events())if(e.type==='artilleryImpact')impacts++;}
 assert(impacts===4&&enemy.hp<100,mapId+' artillery state');assert(ally.respawnAt<100,mapId+' reinforcement state');assert(requested.every(v=>!v.airborne&&v.reservedFor===b.player.id),mapId+' landed reservations');b.player.pos={...requested[0].pos};assert(b.togglePlayerVehicle());assert.equal(b.playerVehicle.id,requested[0].id);
 supportChecks.push({mapId,recon:true,smoke:true,artilleryImpacts:impacts,reinforcement:true,landedVehicles:3,playerDriver:true});
}

for(const mapId of ['industrial-frontier','dust-horizon'])for(const mode of ['conquest','breakthrough'])for(const seed of [505,12731,1905]){
 const b=new Battle({mapId,mode,size:32,difficulty:'easy',tickets:500,seed}),counts={capture:0,death:0,respawn:0,shot:0,vehicleShot:0,vehicleDisabled:0,vehicleRespawn:0,majorEvent:0,revive:0},events=new Set(),start=performance.now();
 for(let n=0;n<32000&&!b.finished;n++){b.tick(.125);for(const e of b.events()){if(e.type in counts)counts[e.type]++;if(e.type==='majorEvent'&&e.action==='start')events.add(e.kind);}if(n%80===0){for(const s of b.soldiers)assert(Number.isFinite(s.pos.x)&&Number.isFinite(s.pos.z));for(const v of b.vehicles){assert(new Set(v.occupants.filter(x=>x!==null)).size===v.occupants.filter(x=>x!==null).length);assert(Number.isFinite(v.pos.x)&&Number.isFinite(v.altitude));}}}
 assert(b.finished,`${mapId} ${mode} ${seed} failed to finish`);assert(counts.capture>=(mode==='conquest'?5:1)&&counts.shot>20&&counts.respawn>0&&counts.vehicleShot>0,JSON.stringify(counts));
 results.push({mapId,mode,seed,winner:b.winner,seconds:b.elapsed,tickets:b.tickets,counts,eventKinds:[...events],diagnostics:b.getAIDiagnostics(),runtimeMs:Math.round(performance.now()-start)});console.log(mapId,mode,seed,b.winner,b.elapsed);
}
fs.mkdirSync('docs/qa',{recursive:true});fs.writeFileSync('docs/qa/maps-019.json',JSON.stringify({version:'0.19.0',size:32,startingTickets:500,supportChecks,results},null,2)+'\n');
