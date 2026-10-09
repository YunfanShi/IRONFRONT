const fs=require('node:fs');const path=require('node:path');const out=path.join(__dirname,'../.logic-build');fs.mkdirSync(out,{recursive:true});fs.writeFileSync(path.join(out,'package.json'),JSON.stringify({type:'commonjs'}));
const assert=require('node:assert/strict');
const {performance}=require('node:perf_hooks');
const {Battle}=require('../.logic-build/core/Battle.js');
const {createCapturePoints,updateCapture,bleedRate}=require('../.logic-build/gameplay/Conquest.js');
const {collides,lineBlocked,COVER_POINTS}=require('../.logic-build/world/Layout.js');
const {WEAPONS}=require('../.logic-build/combat/Weapons.js');

function logicChecks(){
 const p=createCapturePoints()[0];const blue={alive:true,team:'blue',pos:{x:p.x,z:p.z}},red={alive:true,team:'red',pos:{x:p.x,z:p.z}};
 updateCapture(p,[blue,red],10);assert.equal(p.contested,true);assert.equal(p.control,0);
 for(let i=0;i<10;i++)updateCapture(p,[blue],1);assert.equal(p.owner,'blue');
 const points=createCapturePoints();points[0].owner='red';points[1].owner='red';points[2].owner='blue';assert(bleedRate(points,'blue')>0);assert.equal(bleedRate(points,'red'),0);
 assert.equal(collides(-269,-170,1),true);assert.equal(lineBlocked({x:-315,z:-170},{x:-220,z:-170}),true);assert(COVER_POINTS.length>25);
 const battle=new Battle({size:8,difficulty:'normal',tickets:100,seed:7});assert.equal(battle.playerAmmo,WEAPONS.carbine.magazine);assert.equal(battle.switchPlayerWeapon('marksman'),true);assert.equal(battle.playerAmmo,WEAPONS.marksman.magazine);assert.equal(battle.activeWeapon.id,'marksman');
}

function stateChecks(){
 const b=new Battle({size:8,difficulty:'normal',tickets:100,seed:19});const blue=b.soldiers.find(s=>s.team==='blue'&&!s.player);const red=b.soldiers.find(s=>s.team==='red');
 blue.pos={x:5,z:0};blue.lastProgress={...blue.pos};blue.hp=52;blue.suppression=.92;blue.spawnGraceUntil=0;blue.nextAiAt=0;red.pos={x:5,z:10};red.lastProgress={...red.pos};red.spawnGraceUntil=0;red.nextAiAt=0;
 const seen=new Set();for(let i=0;i<30;i++){b.tick(.05);seen.add(blue.state)}assert([...seen].some(x=>['RETREAT','SEEK_COVER','ENGAGE'].includes(x)),`expected combat survival state, saw ${[...seen].join(',')}`);
}

function auditoryCheck(){
 const b=new Battle({size:8,difficulty:'normal',tickets:100,seed:91}),blue=b.soldiers.find(s=>s.team==='blue'&&!s.player),red=b.soldiers.find(s=>s.team==='red');
 blue.pos={x:-193,z:-216};blue.lastProgress={...blue.pos};blue.spawnGraceUntil=0;blue.nextAiAt=0;red.pos={x:-193,z:-208};red.lastProgress={...red.pos};red.spawnGraceUntil=0;red.nextAiAt=999;
 assert.equal(lineBlocked(blue.pos,red.pos),true,'auditory test positions must be visually blocked');const seen=new Set();for(let i=0;i<24;i++){b.tick(.05);seen.add(blue.state)}assert(seen.has('SEARCH'),'blocked nearby enemy did not create an auditory search memory');return {blocked:true,searchObserved:true};
}

function vehicleChecks(){
 const b=new Battle({size:8,difficulty:'normal',tickets:100,seed:44});const blue=b.vehicles.find(v=>v.team==='blue'),red=b.vehicles.find(v=>v.team==='red');
 blue.pos={x:0,z:0};blue.lastProgress={...blue.pos};blue.nextDecision=9999;red.pos={x:0,z:24};red.lastProgress={...red.pos};red.nextDecision=9999;b.player.pos={...blue.pos};
 assert.equal(b.togglePlayerVehicle(),true);assert.equal(b.inVehicle,true);const before={...blue.pos};b.drivePlayerVehicle(1,0,.5);const movedDistance=Math.hypot(blue.pos.x-before.x,blue.pos.z-before.z);assert(movedDistance>.1,'player vehicle did not move');
 blue.pos={x:0,z:0};b.player.pos={...blue.pos};for(let i=0;i<9;i++){assert.equal(b.shootPlayerVehicle({x:0,y:0,z:1}),true);for(let k=0;k<10;k++)b.tick(.125);red.pos={x:0,z:24};red.nextDecision=9999}
 assert.equal(red.alive,false,'vehicle weapon did not disable enemy armor');const events=b.events();assert(events.some(e=>e.type==='vehicleDisabled'),'vehicle disable event missing');
 assert.equal(b.togglePlayerVehicle(),true);assert.equal(b.inVehicle,false,'player could not exit vehicle');
 for(let i=0;i<760;i++)b.tick(.125);assert.equal(red.alive,true,'disabled vehicle did not respawn');return {entered:true,movedDistance:Number(movedDistance.toFixed(2)),enemyDisabled:true,exited:true,enemyRespawned:true};
}

function simulate(size,seed){
 const battle=new Battle({size,difficulty:'normal',tickets:36,killTicketPenalty:1,seed});const initial=battle.soldiers.map(s=>({...s.pos}));let captures=0,deaths=0,respawns=0,shots=0,vehicleShots=0,vehicleDisabled=0,vehicleRespawns=0,majorStarts=0,artilleryImpacts=0;const states=new Set(),eventKinds=new Set();const started=performance.now();
 for(let i=0;i<40000&&!battle.finished;i++){
  battle.tick(.125);for(const s of battle.soldiers)if(!s.player)states.add(s.state);
  for(const e of battle.events()){if(e.type==='capture')captures++;else if(e.type==='death')deaths++;else if(e.type==='respawn')respawns++;else if(e.type==='shot')shots++;else if(e.type==='vehicleShot')vehicleShots++;else if(e.type==='vehicleDisabled')vehicleDisabled++;else if(e.type==='vehicleRespawn')vehicleRespawns++;else if(e.type==='majorEvent'&&e.action==='start'){majorStarts++;eventKinds.add(e.kind)}else if(e.type==='artilleryImpact')artilleryImpacts++}
 }
 const ms=performance.now()-started;assert(battle.finished,`${size}v${size} did not finish`);assert(battle.winner);assert(captures>0,`${size}v${size} had no captures`);assert(deaths>0,`${size}v${size} had no deaths`);assert(shots>0,`${size}v${size} had no shots`);assert(vehicleShots>0,`${size}v${size} vehicles did not participate`);assert(battle.soldiers.some((s,i)=>!s.player&&Math.hypot(s.pos.x-initial[i].x,s.pos.z-initial[i].z)>35),`${size}v${size} bots did not traverse map`);assert(states.has('ENGAGE'));assert(states.has('MOVE_TO_OBJECTIVE')||states.has('FOLLOW_SQUAD'));
 return {size,winner:battle.winner,seconds:battle.elapsed,captures,deaths,respawns,shots,vehicleShots,vehicleDisabled,vehicleRespawns,majorStarts,artilleryImpacts,eventKinds:[...eventKinds].sort(),states:[...states].sort(),navFailures:battle.getNavigationFailures(),runtimeMs:Math.round(ms)};
}

function defaultMatchCheck(){
 const battle=new Battle({size:32,difficulty:'normal',tickets:500,killTicketPenalty:1,seed:505});
 let captures=0,deaths=0,respawns=0,shots=0,vehicleShots=0,vehicleDisabled=0,vehicleRespawns=0,majorStarts=0,artilleryImpacts=0,counterEffect=false,armoredEffect=false;const eventKinds=new Set();const started=performance.now();
 for(let i=0;i<24000&&!battle.finished;i++){
  battle.tick(.125);const active=battle.getMajorEvent();if(active?.kind==='COUNTER_OFFENSIVE'){const teamOrders=battle.getSquadTargets().filter(o=>o.team===active.team);const committedOrders=teamOrders.filter(o=>o.objective===active.objective),committed=committedOrders.length;let spread=0;for(const a of committedOrders)for(const b of committedOrders)spread=Math.max(spread,Math.hypot(a.approach.x-b.approach.x,a.approach.z-b.approach.z));if(committed>=Math.ceil(teamOrders.length*.6)&&committedOrders.some(o=>o.intent==='FLANK')&&spread>18)counterEffect=true}if(active?.kind==='ARMORED_PUSH'){const teamOrders=battle.getSquadTargets().filter(o=>o.team===active.team),committed=teamOrders.filter(o=>o.objective===active.objective).length,vehicle=battle.vehicles.find(v=>v.team===active.team);if(committed>=Math.min(2,teamOrders.length)&&vehicle?.goal===active.objective)armoredEffect=true}
  for(const e of battle.events()){if(e.type==='capture')captures++;else if(e.type==='death')deaths++;else if(e.type==='respawn')respawns++;else if(e.type==='shot')shots++;else if(e.type==='vehicleShot')vehicleShots++;else if(e.type==='vehicleDisabled')vehicleDisabled++;else if(e.type==='vehicleRespawn')vehicleRespawns++;else if(e.type==='majorEvent'&&e.action==='start'){majorStarts++;eventKinds.add(e.kind)}else if(e.type==='artilleryImpact')artilleryImpacts++}
 }
 assert(battle.finished,'default 32v32 / 500 ticket battle did not finish');
 assert(battle.winner,'default battle has no winner');assert(captures>0);assert(deaths>0);assert(respawns>0);assert(shots>0);assert(vehicleShots>0);assert(majorStarts>=3,'major events did not recur');assert(artilleryImpacts>=4,'artillery barrage did not produce impacts');assert(counterEffect,'counter-offensive did not concentrate squads');assert(armoredEffect,'armored push did not coordinate armor and infantry');for(const kind of ['ARTILLERY','COUNTER_OFFENSIVE','ARMORED_PUSH'])assert(eventKinds.has(kind),`missing major event ${kind}`);
 return {size:32,tickets:500,winner:battle.winner,seconds:battle.elapsed,finalTickets:{...battle.tickets},captures,deaths,respawns,shots,vehicleShots,vehicleDisabled,vehicleRespawns,majorStarts,artilleryImpacts,counterEffect,armoredEffect,eventKinds:[...eventKinds].sort(),navFailures:battle.getNavigationFailures(),runtimeMs:Math.round(performance.now()-started)};
}

logicChecks();stateChecks();const auditory=auditoryCheck();const vehicleControl=vehicleChecks();
const reports=[simulate(8,101),simulate(16,102),simulate(32,103),simulate(64,104)];
const defaultMatch=defaultMatchCheck();
console.log(JSON.stringify({ok:true,auditory,vehicleControl,reports,defaultMatch},null,2));
