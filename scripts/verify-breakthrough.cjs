const {Battle}=require('../.logic-build/core/Battle.js');
const b=new Battle({size:32,difficulty:'normal',tickets:500,seed:505,mode:'breakthrough'});let captures=0,deaths=0,shots=0;
while(!b.finished&&b.elapsed<2400){b.tick(.125);for(const e of b.events()){if(e.type==='capture')captures++;if(e.type==='death')deaths++;if(e.type==='shot')shots++;}}
if(!b.finished||b.sectorIndex<1||b.getNavigationFailures()>150)throw Error('Breakthrough regression');
const result={ok:true,mode:'breakthrough',size:32,tickets:500,seed:505,seconds:b.elapsed,winner:b.winner,completedSectors:b.sectorIndex,captures,deaths,shots,blueTickets:b.tickets.blue,navFailures:b.getNavigationFailures()};console.log(JSON.stringify(result,null,2));
