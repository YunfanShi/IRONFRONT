import {clamp} from '../core/math';
import type {ArmoredVehicle} from './Vehicle';
/** Jets bank through turns and need forward speed; helicopters can hover, reverse and translate. */
export function controlFlight(v:ArmoredVehicle,throttle:number,side:number,dt:number,viewYaw?:number,pitch=0,boost=false,brake=false){
 const jet=v.kind==='jet';const delta=viewYaw===undefined?0:Math.atan2(Math.sin(viewYaw+Math.PI-v.yaw),Math.cos(viewYaw+Math.PI-v.yaw));
 if(jet){const bank=clamp(side*.95+delta*.45,-1.1,1.1);v.roll+=(bank-v.roll)*(1-Math.exp(-dt*5));v.flightPitch+=(clamp(pitch,-.85,.85)-v.flightPitch)*(1-Math.exp(-dt*3));v.yaw+=(v.roll*1.8+clamp(delta,-.5,.5)*.3)*dt;const target=brake?32:boost?92:clamp(52+throttle*23,29,75);v.speed+=clamp(target-v.speed,-22*dt,18*dt);}
 else {v.yaw+=clamp(viewYaw===undefined?side*2.2:delta,-2.2*dt,2.2*dt);const target=clamp(throttle,-1,1)*(boost?48:38);v.speed+=clamp(target-v.speed,-22*dt,22*dt);v.roll+=(clamp(-side*.4,-.4,.4)-v.roll)*(1-Math.exp(-dt*5));v.flightPitch+=(-throttle*.12-v.flightPitch)*(1-Math.exp(-dt*4));}
 return {x:Math.sin(v.yaw)*v.speed*dt*Math.cos(jet?v.flightPitch:0)+(jet?0:Math.cos(v.yaw)*side*19*dt),z:Math.cos(v.yaw)*v.speed*dt*Math.cos(jet?v.flightPitch:0)-(jet?0:Math.sin(v.yaw)*side*19*dt)};
}
/** Finite seeker cone and turn rate: a high angular-speed crossing can lose guidance. */
export function guideMissile(velocity:{x:number;y:number;z:number},delta:{x:number;y:number;z:number},speed:number,dt:number):boolean{
 const vl=Math.hypot(velocity.x,velocity.y,velocity.z)||1,dl=Math.hypot(delta.x,delta.y,delta.z)||1,a={x:velocity.x/vl,y:velocity.y/vl,z:velocity.z/vl},b={x:delta.x/dl,y:delta.y/dl,z:delta.z/dl};const angle=Math.acos(clamp(a.x*b.x+a.y*b.y+a.z*b.z,-1,1));if(angle>1.35)return false;
 const t=angle<.00001?1:Math.min(1,dt*1.25/angle),sin=Math.sin(angle),wa=t===1?0:Math.sin((1-t)*angle)/sin,wb=t===1?1:Math.sin(t*angle)/sin;
 velocity.x=(a.x*wa+b.x*wb)*speed;velocity.y=(a.y*wa+b.y*wb)*speed;velocity.z=(a.z*wa+b.z*wb)*speed;return true;
}
