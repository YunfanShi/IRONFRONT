export interface Point {x:number;z:number}
export const clamp=(n:number,a:number,b:number)=>Math.max(a,Math.min(b,n));
export const dist=(a:Point,b:Point)=>Math.hypot(a.x-b.x,a.z-b.z);
export const lerp=(a:number,b:number,t:number)=>a+(b-a)*t;
export const norm=(a:Point):Point=>{const m=Math.hypot(a.x,a.z)||1;return{x:a.x/m,z:a.z/m}};
export function heightAt(x:number,z:number):number {
  // Hills are positioned away from the principal capture routes.
  const hill=(hx:number,hz:number,r:number,h:number)=>h*Math.exp(-((x-hx)**2+(z-hz)**2)/(r*r));
  return hill(-270,175,110,17)+hill(245,-210,120,16)+hill(285,265,105,8);
}
export class Random {
  constructor(private seed=783154){}
  next(){let t=this.seed+=0x6D2B79F5;t=Math.imul(t^t>>>15,t|1);t^=t+Math.imul(t^t>>>7,t|61);return ((t^t>>>14)>>>0)/4294967296}
  between(a:number,b:number){return a+(b-a)*this.next()}
  int(n:number){return Math.floor(this.next()*n)}
}
