import {dist,type Point} from '../core/math';
import {MapContext} from '../world/Maps';
const DIRECTIONS:[[number,number],...[number,number][]]=[[1,0],[-1,0],[0,1],[0,-1],[1,1],[-1,1],[1,-1],[-1,-1]];

/** Stable A* queue: equal priorities retain the original insertion order. */
class OpenQueue {
 private nodes:number[]=[];private scores:number[]=[];private order:number[]=[];private sequence=0;
 get length(){return this.nodes.length;}
 reset(){this.nodes.length=0;this.scores.length=0;this.order.length=0;this.sequence=0;}
 private before(score:number,order:number,index:number){return score<this.scores[index]!||score===this.scores[index]&&order<this.order[index]!;}
 push(node:number,score:number){let i=this.nodes.length;const order=this.sequence++;
  while(i>0){const parent=(i-1)>>1;if(!this.before(score,order,parent))break;this.nodes[i]=this.nodes[parent]!;this.scores[i]=this.scores[parent]!;this.order[i]=this.order[parent]!;i=parent;}
  this.nodes[i]=node;this.scores[i]=score;this.order[i]=order;
 }
 pop(){const first=this.nodes[0]!,node=this.nodes.pop()!,score=this.scores.pop()!,order=this.order.pop()!;if(!this.nodes.length)return first;
  let i=0;while(i*2+1<this.nodes.length){let child=i*2+1;const right=child+1;if(right<this.nodes.length&&this.before(this.scores[right]!,this.order[right]!,child))child=right;if(this.before(score,order,child))break;this.nodes[i]=this.nodes[child]!;this.scores[i]=this.scores[child]!;this.order[i]=this.order[child]!;i=child;}
  this.nodes[i]=node;this.scores[i]=score;this.order[i]=order;return first;
 }
}

/** 16 m walkability grid + A*. Scratch buffers belong to this synchronous instance. */
export class Navigation {
 readonly step:number;readonly count:number;private walkable:Uint8Array;private edges=new Map<number,boolean>();
 private points:Point[]=[];private cost=new Float32Array(0);private prior=new Int32Array(0);private closed=new Uint8Array(0);
 private candidates:number[]=[];private distances=new Float64Array(0);private open=new OpenQueue();
 constructor(private clearance=1.12,private vehicle=false,private map=new MapContext()){this.step=map.definition.navigationStep;this.count=Math.ceil(map.size/this.step);const size=this.count*this.count;this.cost=new Float32Array(size);this.prior=new Int32Array(size);this.closed=new Uint8Array(size);this.distances=new Float64Array(size);this.walkable=new Uint8Array(this.count*this.count);for(let z=0;z<this.count;z++)for(let x=0;x<this.count;x++){const p={x:(x+.5)*this.step-this.map.size/2,z:(z+.5)*this.step-this.map.size/2};this.points.push(p);this.walkable[z*this.count+x]=this.map.collides(p.x,p.z,this.clearance)?0:1;}}
 private visibleNode(p:Point){const candidates=this.candidates;candidates.length=0;
  // Only cells in the same 80 m search radius can qualify. Preserve row order for ties.
  const minX=Math.max(0,Math.ceil((p.x-80+this.map.size/2)/this.step-.5)),maxX=Math.min(this.count-1,Math.floor((p.x+80+this.map.size/2)/this.step-.5));
  const minZ=Math.max(0,Math.ceil((p.z-80+this.map.size/2)/this.step-.5)),maxZ=Math.min(this.count-1,Math.floor((p.z+80+this.map.size/2)/this.step-.5));
  for(let z=minZ;z<=maxZ;z++)for(let x=minX;x<=maxX;x++){const k=z*this.count+x;if(this.walkable[k]){const distance=dist(p,this.points[k]!);if(distance<80){this.distances[k]=distance;candidates.push(k);}}}
  candidates.sort((a,b)=>this.distances[a]!-this.distances[b]!);
  return candidates.find(k=>this.clear(p,this.points[k]!))??-1;
 }
 private clear(a:Point,b:Point){if(this.map.collides(a.x,a.z,this.clearance)||this.map.collides(b.x,b.z,this.clearance))return false;if(!this.vehicle)return !this.map.lineBlocked(a,b,undefined,this.clearance);const steps=Math.ceil(dist(a,b)/2);for(let i=0;i<=steps;i++){const t=steps?i/steps:0;if(this.map.collides(a.x+(b.x-a.x)*t,a.z+(b.z-a.z)*t,this.clearance))return false;}return true;}
 private edgeClear(a:number,b:number){const key=Math.min(a,b)*this.walkable.length+Math.max(a,b),cached=this.edges.get(key);if(cached!==undefined)return cached;const result=this.clear(this.points[a]!,this.points[b]!);this.edges.set(key,result);return result;}
 find(from:Point,to:Point):Point[]{if(this.clear(from,to))return [{...to}];
  const start=this.visibleNode(from),goal=this.visibleNode(to);if(start<0||goal<0)return [];
  const {cost,prior,closed,open}=this;cost.fill(Infinity);prior.fill(-1);closed.fill(0);open.reset();
  const heur=(a:number,b:number)=>Math.hypot(a%this.count-b%this.count,Math.floor(a/this.count)-Math.floor(b/this.count));
  open.push(start,heur(start,goal));cost[start]=0;let scanned=0;
  while(open.length&&scanned++<this.count*this.count+175){const cur=open.pop();if(closed[cur])continue;closed[cur]=1;if(cur===goal)break;
   const cx=cur%this.count,cz=Math.floor(cur/this.count);
   for(const [dx,dz] of DIRECTIONS){
    const nx=cx+dx,nz=cz+dz;if(nx<0||nz<0||nx>=this.count||nz>=this.count)continue;const nk=nz*this.count+nx;if(!this.walkable[nk]||closed[nk])continue;
    if(!this.edgeClear(cur,nk))continue;
    if(dx&&dz&&(!this.walkable[cz*this.count+nx]||!this.walkable[nz*this.count+cx]))continue;
    const alt=cost[cur]!+Math.hypot(dx,dz);if(alt<cost[nk]!){cost[nk]=alt;prior[nk]=cur;open.push(nk,alt+heur(nk,goal));}
   }
  }
  if(start!==goal&&prior[goal]===-1)return [];
  const reverse:Point[]=[];for(let at=goal;at!==start&&at!==-1;at=prior[at]!)reverse.push(this.points[at]!);
  reverse.reverse();const raw=[this.points[start]!,...reverse,{...to}],simplified:Point[]=[];let current={...from};
  for(let i=0;i<raw.length;){let far=-1;for(let j=raw.length-1;j>=i;j--)if(this.clear(current,raw[j]!)){far=j;break}if(far<0)return [];current=raw[far]!;simplified.push({...current});i=far+1;}
  return simplified;
 }
}
