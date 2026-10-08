import {dist,type Point} from '../core/math';
import {collides,lineBlocked} from '../world/Layout';
/** 20 m walkability grid + A*: used by AI and does not access rendering objects. */
export class Navigation {
 readonly step=16;readonly count=45;private walkable:Uint8Array;
 constructor(){this.walkable=new Uint8Array(this.count*this.count);for(let z=0;z<this.count;z++)for(let x=0;x<this.count;x++){const p=this.point(x,z);this.walkable[z*this.count+x]=collides(p.x,p.z,3.8)?0:1}}
 private point(x:number,z:number):Point{return {x:(x+.5)*this.step-360,z:(z+.5)*this.step-360}}
 private key(p:Point):number{const x=Math.max(0,Math.min(44,Math.floor((p.x+360)/this.step))),z=Math.max(0,Math.min(44,Math.floor((p.z+360)/this.step)));return z*45+x}
 private nearest(k:number):number{if(this.walkable[k])return k;const x=k%45,z=Math.floor(k/45);for(let r=1;r<12;r++)for(let j=-r;j<=r;j++)for(let i=-r;i<=r;i++){if(Math.abs(i)!==r&&Math.abs(j)!==r)continue;const xx=x+i,zz=z+j;if(xx>=0&&zz>=0&&xx<45&&zz<45&&this.walkable[zz*45+xx])return zz*45+xx;}return k}
 find(from:Point,to:Point):Point[]{if(!lineBlocked(from,to,undefined,2))return [{...to}];
  const start=this.nearest(this.key(from)),goal=this.nearest(this.key(to));if(start===goal)return[{...to}];
  const cost=new Float32Array(2025).fill(Infinity),prior=new Int32Array(2025).fill(-1),closed=new Uint8Array(2025);
  const heur=(a:number,b:number)=>Math.hypot(a%45-b%45,Math.floor(a/45)-Math.floor(b/45));
  const open:[number,number][]=[[start,heur(start,goal)]];cost[start]=0;let scanned=0;
  while(open.length&&scanned++<2200){let best=0;for(let i=1;i<open.length;i++)if(open[i]![1]<open[best]![1])best=i;
   const cur=open.splice(best,1)[0]![0];if(closed[cur])continue;closed[cur]=1;if(cur===goal)break;
   const cx=cur%45,cz=Math.floor(cur/45);
   for(const [dx,dz] of [[1,0],[-1,0],[0,1],[0,-1],[1,1],[-1,1],[1,-1],[-1,-1]] as [number,number][]){
    const nx=cx+dx,nz=cz+dz;if(nx<0||nz<0||nx>=45||nz>=45)continue;const nk=nz*45+nx;if(!this.walkable[nk]||closed[nk])continue;
    if(dx&&dz&&(!this.walkable[cz*45+nx]||!this.walkable[nz*45+cx]))continue;
    const alt=cost[cur]!+Math.hypot(dx,dz);if(alt<cost[nk]!){cost[nk]=alt;prior[nk]=cur;open.push([nk,alt+heur(nk,goal)])}
   }
  }
  if(prior[goal]===-1)return[{...to}];
  const reverse:Point[]=[];for(let at=goal;at!==start&&at!==-1;at=prior[at]!)reverse.push(this.point(at%45,Math.floor(at/45)));
  reverse.reverse();let prev={...from};const simplified:Point[]=[];
  for(let i=0;i<reverse.length;i++){const far=reverse[i+1]??to;if(lineBlocked(prev,far,undefined,2)){simplified.push(reverse[i]!);prev=reverse[i]!}}
  simplified.push({...to});return simplified;
 }
}
