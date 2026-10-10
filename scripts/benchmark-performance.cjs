// Same seed, paths and fixed simulated time for reproducible CPU comparisons.
// Run after npm run compile:logic. An optional directory supports a frozen baseline.
const path=require('node:path'),crypto=require('node:crypto'),{performance}=require('node:perf_hooks');
const root=path.resolve(process.argv[2]||path.join(__dirname,'../.logic-build'));
const {Navigation}=require(path.join(root,'ai/Navigation.js')),{Battle}=require(path.join(root,'core/Battle.js'));
const {collides,lineBlocked}=require(path.join(root,'world/Layout.js'));
const digest=value=>crypto.createHash('sha256').update(JSON.stringify(value)).digest('hex');
const median=a=>[...a].sort((x,y)=>x-y)[Math.floor(a.length/2)];
let seed=505;const random=()=>{seed=(Math.imul(seed,1664525)+1013904223)>>>0;return seed/4294967296;};
const points=[];while(points.length<100){const p={x:random()*650-325,z:random()*650-325};if(!collides(p.x,p.z,3.5))points.push(p);}
const routes=[];for(let i=0;i<points.length;i++)if(lineBlocked(points[i],points[(i+37)%points.length],undefined,1.12))routes.push([points[i],points[(i+37)%points.length]]);
function navigation(){const nav=new Navigation();for(const [a,b] of routes)nav.find(a,b);const start=performance.now();let paths=[];for(let loop=0;loop<5;loop++)paths=routes.map(([a,b])=>nav.find(a,b));return {ms:performance.now()-start,hash:digest(paths),routes:routes.length*5};}
function geometry(){const start=performance.now();let hits=0;for(let loop=0;loop<100;loop++)for(let i=0;i<points.length;i++)hits+=Number(lineBlocked(points[i],points[(i+37)%points.length],undefined,1.12))+Number(collides(points[i].x,points[i].z,1.12));return {ms:performance.now()-start,hits,queries:points.length*200};}
function battle(size){const b=new Battle({size,difficulty:'normal',tickets:500,seed:505});const start=performance.now();for(let tick=0;tick<960&&!b.finished;tick++){b.tick(.125);b.events();}return {ms:performance.now()-start,hash:digest({points:b.points,soldiers:b.soldiers,vehicles:b.vehicles,stats:b.stats,elapsed:b.elapsed,tickets:b.tickets,projectiles:b.projectiles}),elapsed:b.elapsed};}
// First pass warms runtime code; five independent measured passes retain all samples.
const results={node:process.version,seed:505,passes:5,simulatedSeconds:120};
for(const [key,run] of [['geometry',geometry],['navigation',navigation],['battle32',()=>battle(32)],['battle64',()=>battle(64)]]){run();const samples=Array.from({length:5},run);if(new Set(samples.map(s=>s.hash??s.hits)).size!==1)throw Error('Nondeterministic '+key);results[key]={...samples[0],ms:undefined,medianMs:median(samples.map(s=>s.ms)),samplesMs:samples.map(s=>s.ms)};}
console.log(JSON.stringify(results,null,2));
