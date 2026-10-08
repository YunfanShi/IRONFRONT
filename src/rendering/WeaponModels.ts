import * as THREE from 'three';
import type {WeaponId} from '../combat/Weapons';
export const MODEL_PROFILES:Record<WeaponId,{muzzle:number;sight:number;profile:string}>={
 carbine:{muzzle:-1.5,sight:.245,profile:'rail-carbine'},marksman:{muzzle:-1.97,sight:.28,profile:'semi-auto-dmr'},smg:{muzzle:-.98,sight:.24,profile:'folding-stock-pdw'},lmg:{muzzle:-1.85,sight:.25,profile:'belt-fed-support'},battleRifle:{muzzle:-1.68,sight:.25,profile:'heavy-receiver-rifle'},sniper:{muzzle:-2.18,sight:.3,profile:'bolt-action-precision'},pistol:{muzzle:-.73,sight:.15,profile:'handgun-slide'}
};
/** Each weapon has its own receiver, magazine, stock, barrel and sight construction. */
export function buildWeaponModel(id:WeaponId,m:Record<string,THREE.Material>){
 const g=new THREE.Group();g.name=`weapon-${id}`;g.userData.profile=MODEL_PROFILES[id].profile;
 const box=(name:string,w:number,h:number,d:number,x:number,y:number,z:number,mat='steel')=>{const mesh=new THREE.Mesh(new THREE.BoxGeometry(w,h,d),m[mat]);mesh.name=name;mesh.position.set(x,y,z);g.add(mesh);return mesh};
 const tube=(name:string,r:number,d:number,x:number,y:number,z:number,mat='steel')=>{const mesh=new THREE.Mesh(new THREE.CylinderGeometry(r,r,d,12),m[mat]);mesh.name=name;mesh.rotation.x=Math.PI/2;mesh.position.set(x,y,z);g.add(mesh);return mesh};
 const grip=(z:number)=>{box('pistol-grip',.13,.29,.15,0,-.24,z,'grip').rotation.x=.24;box('trigger-guard',.13,.025,.17,0,-.21,z-.13,'matte');};
 const rail=(length:number,z:number)=>{box('top-rail',.12,.04,length,0,.13,z,'matte');for(let i=0;i<Math.ceil(length/.09);i++)box('rail-tooth',.17,.018,.024,0,.158,z-length/2+i*.09,'matte');};
 const optic=(scope:boolean,z:number)=>{if(scope){tube('scope-body',.068,.48,0,.28,z,'matte');tube('objective-lens',.057,.012,0,.28,z-.25,'glass');tube('ocular-lens',.055,.012,0,.28,z+.25,'glass');box('scope-mount',.15,.10,.14,0,.18,z,'steel');box('elevation-dial',.075,.055,.08,0,.355,z,'matte');}else{box('sight-left',.025,.17,.055,-.09,.23,z,'matte');box('sight-right',.025,.17,.055,.09,.23,z,'matte');box('sight-top',.2,.025,.055,0,.31,z,'matte');box('reticle-dot',.012,.012,.012,0,.245,z-.015,'gold');}};
 const bipod=(z:number)=>{for(const side of [-1,1]){const leg=box('bipod-leg',.025,.34,.025,side*.16,-.23,z,'steel');leg.rotation.z=side*.35;}};
 if(id==='pistol'){
  box('slide',.13,.13,.46,0,.08,-.48,'steel');box('frame',.14,.09,.35,0,-.02,-.40,'matte');box('handgun-grip',.135,.28,.14,0,-.18,-.25,'grip').rotation.x=.2;box('trigger-guard',.13,.03,.16,0,-.12,-.43,'matte');tube('barrel',.035,.32,0,.075,-.57);box('front-sight',.025,.025,.03,0,.16,-.69,'matte');box('rear-sight',.1,.025,.035,0,.16,-.3,'matte');for(let i=0;i<6;i++)box('slide-serration',.138,.10,.007,0,.07,-.29-i*.017,'grip');
 }else if(id==='smg'){
  box('compact-receiver',.19,.18,.47,0,.01,-.32,'matte');box('short-handguard',.20,.15,.23,0,0,-.66,'grip');tube('short-barrel',.036,.33,0,.025,-.8);tube('muzzle-brake',.055,.09,0,.025,-.93,'matte');grip(-.14);box('straight-magazine',.105,.4,.13,0,-.26,-.43,'steel');for(const side of [-1,1])box('folding-stock-rail',.025,.05,.36,side*.08,.035,.13,'steel');box('stock-pad',.15,.19,.035,0,-.03,.32,'grip');rail(.4,-.33);optic(false,-.35);
 }else if(id==='lmg'){
  box('belt-fed-receiver',.26,.23,.65,0,0,-.35,'steel');box('feed-cover',.29,.06,.48,0,.155,-.32,'matte');box('ammo-box',.34,.32,.35,.16,-.29,-.37,'tan');for(let i=0;i<6;i++)tube('exposed-belt-round',.018,.095,.16+i*.025,-.06,-.28,'gold');box('heat-shield',.24,.16,.56,0,0,-.95,'grip');tube('heavy-barrel',.05,.95,0,.035,-1.35);tube('flash-suppressor',.065,.12,0,.035,-1.8,'matte');box('support-stock',.18,.20,.47,0,-.015,.2,'tan');grip(-.04);rail(.55,-.37);optic(false,-.3);bipod(-1.25);box('carry-handle',.025,.10,.23,.1,.25,-.75,'steel');
 }else if(id==='sniper'){
  box('precision-chassis',.17,.16,.92,0,-.02,-.52,'tan');tube('bolt-receiver',.07,.43,0,.045,-.3);box('bolt-handle',.17,.025,.035,.09,.035,-.15,'steel');box('bolt-knob',.05,.055,.055,.18,.015,-.15,'grip');tube('long-free-barrel',.038,1.25,0,.025,-1.43);tube('precision-brake',.06,.14,0,.025,-2.12,'steel');box('small-box-magazine',.14,.19,.15,0,-.19,-.35,'matte');box('adjustable-stock',.17,.16,.46,0,-.045,.27,'tan');box('cheek-rest',.15,.055,.23,0,.065,.23,'grip');grip(-.05);rail(.65,-.40);optic(true,-.43);bipod(-1.10);
 }else if(id==='marksman'){
  box('dmr-receiver',.17,.18,.77,0,0,-.33);box('dmr-handguard',.19,.17,.55,0,-.015,-.9,'grip');tube('match-barrel',.038,.90,0,.02,-1.46);tube('dmr-brake',.055,.13,0,.02,-1.9,'matte');box('dmr-fixed-stock',.16,.18,.43,0,-.02,.25,'matte');box('straight-20-magazine',.16,.26,.16,0,-.25,-.32);grip(-.08);rail(.68,-.42);optic(true,-.4);
 }else if(id==='battleRifle'){
  box('heavy-receiver',.22,.21,.73,0,0,-.33);box('ribbed-handguard',.22,.16,.43,0,-.025,-.87,'tan');tube('battle-barrel',.045,.78,0,.03,-1.24);tube('battle-brake',.06,.13,0,.03,-1.62,'matte');box('solid-stock',.20,.20,.47,0,-.025,.24,'grip');box('wide-20-magazine',.20,.28,.21,0,-.25,-.37);grip(-.1);rail(.50,-.32);optic(false,-.32);for(let i=0;i<5;i++)box('handguard-rib',.235,.17,.024,0,-.025,-.69-i*.075,'grip');
 }else{
  box('carbine-receiver',.16,.18,.64,0,0,-.3);box('carbine-handguard',.20,.17,.41,0,0,-.75,'grip');tube('carbine-barrel',.039,.66,0,.025,-1.10);tube('carbine-brake',.06,.12,0,.025,-1.44,'matte');box('telescopic-buffer',.065,.065,.30,0,.03,.14,'steel');box('telescopic-stock',.15,.18,.24,0,-.02,.29,'matte');grip(-.10);for(let i=0;i<3;i++)box('curved-magazine',.16,.12,.17,0,-.16-i*.10,-.33-i*.022,'steel').rotation.x=-.12-i*.07;rail(.64,-.38);optic(false,-.4);
 }
 // Rounded sleeves and gloves instead of rectangular arm blocks.
 for(const side of id==='pistol'?[1]:[-1,1]){const hand=new THREE.Mesh(new THREE.CapsuleGeometry(.105,.13,4,8),m.glove);hand.name='gloved-hand';hand.rotation.x=Math.PI/2;hand.position.set(side*.12,-.19,side<0?-.72:id==='pistol'?-.23:-.08);g.add(hand);const arm=new THREE.Mesh(new THREE.CylinderGeometry(.105,.13,.50,10),m.tan);arm.name='sleeve';arm.rotation.x=Math.PI/2;arm.rotation.z=side*.2;arm.position.set(side*.21,-.28,side<0?-.40:.19);g.add(arm);}
 return g;
}
