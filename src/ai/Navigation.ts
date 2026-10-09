import {dist,type Point} from '../core/math';
import {collides,lineBlocked} from '../world/Layout';
/** 16 m walkability grid + A*: used by AI and does not access rendering objects. */
export class Navigation {
 readonly step=16;readonly count=45;private walkable:Uint8Array;private edges=new Map<number,boolean>();
 constructor(private clearance=1.12,private vehicle=false){this.walkable=new Uint8Array(this.count*this.count);for(let z=0;z<this.count;z++)for(let x=0;x<this.count;x++){const p=this.point(x,z);this.walkable[z*this.count+x]=collides(p.x,p.z,this.clearance)?0:1}}
 private point(x:number,z:number):Point{return {x:(x+.5)*this.step-360,z:(z+.5)*this.step-360}}
 private visibleNode(p:Point){const candidates:number[]=[];for(let k=0;k<this.walkable.length;k++)if(this.walkable[k]&&dist(p,this.point(k%45,Math.floor(k/45)))<80)candidates.push(k);candidates.sort((a,b)=>dist(p,this.point(a%45,Math.floor(a/45)))-dist(p,this.point(b%45,Math.floor(b/45))));return candidates.find(k=>this.clear(p,this.point(k%45,Math.floor(k/45))))??-1;}
 private clear(a:Point,b:Point){if(collides(a.x,a.z,this.clearance)||collides(b.x,b.z,this.clearance))return false;if(!this.vehicle)return !lineBlocked(a,b,undefined,this.clearance);const steps=Math.ceil(dist(a,b)/2);for(let i=0;i<=steps;i++){const t=steps?i/steps:0;if(collides(a.x+(b.x-a.x)*t,a.z+(b.z-a.z)*t,this.clearance))return false;}return true;}
 private edgeClear(a:number,b:number){const key=Math.min(a,b)*this.walkable.length+Math.max(a,b),cached=this.edges.get(key);if(cached!==undefined)return cached;const result=this.clear(this.point(a%45,Math.floor(a/45)),this.point(b%45,Math.floor(b/45)));this.edges.set(key,result);return result;}
 find(from:Point,to:Point):Point[]{if(this.clear(from,to))return [{...to}];
  const start=this.visibleNode(from),goal=this.visibleNode(to);if(start<0||goal<0)return [];
  const cost=new Float32Array(2025).fill(Infinity),prior=new Int32Array(2025).fill(-1),closed=new Uint8Array(2025);
  const heur=(a:number,b:number)=>Math.hypot(a%45-b%45,Math.floor(a/45)-Math.floor(b/45));
  const open:[number,number][]=[[start,heur(start,goal)]];cost[start]=0;let scanned=0;
  while(open.length&&scanned++<2200){let best=0;for(let i=1;i<open.length;i++)if(open[i]![1]<open[best]![1])best=i;
   const cur=open.splice(best,1)[0]![0];if(closed[cur])continue;closed[cur]=1;if(cur===goal)break;
   const cx=cur%45,cz=Math.floor(cur/45);
   for(const [dx,dz] of [[1,0],[-1,0],[0,1],[0,-1],[1,1],[-1,1],[1,-1],[-1,-1]] as [number,number][]){
    const nx=cx+dx,nz=cz+dz;if(nx<0||nz<0||nx>=45||nz>=45)continue;const nk=nz*45+nx;if(!this.walkable[nk]||closed[nk])continue;
    if(!this.edgeClear(cur,nk))continue;
    if(dx&&dz&&(!this.walkable[cz*45+nx]||!this.walkable[nz*45+cx]))continue;
    const alt=cost[cur]!+Math.hypot(dx,dz);if(alt<cost[nk]!){cost[nk]=alt;prior[nk]=cur;open.push([nk,alt+heur(nk,goal)])}
   }
  }
  if(start!==goal&&prior[goal]===-1)return [];
  const reverse:Point[]=[];for(let at=goal;at!==start&&at!==-1;at=prior[at]!)reverse.push(this.point(at%45,Math.floor(at/45)));
  reverse.reverse();const raw=[this.point(start%45,Math.floor(start/45)),...reverse,{...to}],simplified:Point[]=[];let current={...from};
  for(let i=0;i<raw.length;){let far=-1;for(let j=raw.length-1;j>=i;j--)if(this.clear(current,raw[j]!)){far=j;break}if(far<0)return [];current=raw[far]!;simplified.push(current);i=far+1;}
  return simplified;
 }
}
