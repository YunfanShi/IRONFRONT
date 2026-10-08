export type WeaponId='carbine'|'marksman'|'smg'|'lmg'|'battleRifle'|'sniper'|'pistol';
export interface WeaponDefinition {id:WeaponId;name:string;zh:string;damage:number;fireInterval:number;magazine:number;reserve:number;reload:number;effectiveRange:number;maxRange:number;hipSpread:number;adsSpread:number;recoil:number;automatic:boolean;zoom:number}
export const WEAPONS:Record<WeaponId,WeaponDefinition>={
 carbine:{id:'carbine',name:'IF-27 Carbine',zh:'IF-27 卡宾枪',damage:31,fireInterval:.105,magazine:30,reserve:150,reload:1.9,effectiveRange:92,maxRange:165,hipSpread:.010,adsSpread:.0022,recoil:.14,automatic:true,zoom:1.5},
 marksman:{id:'marksman',name:'M89 Marksman',zh:'M89 精确射手步枪',damage:58,fireInterval:.32,magazine:12,reserve:60,reload:2.35,effectiveRange:145,maxRange:220,hipSpread:.016,adsSpread:.0011,recoil:.24,automatic:false,zoom:3},
 smg:{id:'smg',name:'VX-9 Wasp',zh:'VX-9 黄蜂冲锋枪',damage:23,fireInterval:.066,magazine:36,reserve:180,reload:1.65,effectiveRange:38,maxRange:105,hipSpread:.008,adsSpread:.003,recoil:.095,automatic:true,zoom:1.3},
 lmg:{id:'lmg',name:'H60 Sentinel',zh:'H60 哨兵轻机枪',damage:34,fireInterval:.12,magazine:80,reserve:240,reload:4.2,effectiveRange:110,maxRange:190,hipSpread:.022,adsSpread:.0035,recoil:.20,automatic:true,zoom:1.8},
 battleRifle:{id:'battleRifle',name:'BR-44 Atlas',zh:'BR-44 阿特拉斯战斗步枪',damage:43,fireInterval:.17,magazine:20,reserve:100,reload:2.5,effectiveRange:105,maxRange:185,hipSpread:.014,adsSpread:.002,recoil:.23,automatic:true,zoom:2},
 sniper:{id:'sniper',name:'S12 Meridian',zh:'S12 子午线狙击步枪',damage:92,fireInterval:1.1,magazine:5,reserve:30,reload:3.1,effectiveRange:210,maxRange:310,hipSpread:.035,adsSpread:.0006,recoil:.36,automatic:false,zoom:5},
 pistol:{id:'pistol',name:'P8 Relay',zh:'P8 接力手枪',damage:29,fireInterval:.22,magazine:15,reserve:60,reload:1.3,effectiveRange:28,maxRange:75,hipSpread:.009,adsSpread:.004,recoil:.12,automatic:false,zoom:1.2}
};
export const WEAPON_ORDER=Object.keys(WEAPONS) as WeaponId[];
export type ClassId='assault'|'medic'|'recon'|'engineer';
export type GadgetId='medkit'|'repair'|'ammo'|'beacon';
export const CLASSES={
 assault:{zh:'突击兵',name:'Assault',gadget:'ammo',description:'X 补充主副武器备弹，持续参与前线'},
 medic:{zh:'医疗兵',name:'Medic',gadget:'medkit',description:'X 治疗自己/近处队友，优先救起阵亡队友'},
 recon:{zh:'侦察兵',name:'Recon',gadget:'beacon',description:'X 部署一次性复活信标，阵亡后可选择信标'},
 engineer:{zh:'工程兵',name:'Engineer',gadget:'repair',description:'X 修理己方载具，Z 发射反装甲火箭（2 发）'}
} as const;
export type ThrowableId='frag'|'smoke';
export interface Loadout {classId:ClassId;primary:WeaponId;secondary:WeaponId;gadget:GadgetId;throwable:ThrowableId}
export const DEFAULT_LOADOUT:Loadout={classId:'assault',primary:'carbine',secondary:'marksman',gadget:'ammo',throwable:'frag'};
/** Migrate legacy equipment-only saves and constrain equipment to the selected class. */
export function validateLoadout(value:unknown):Loadout {
 const d=value&&typeof value==='object'?value as Partial<Loadout>:{};
 const classId:ClassId=d.classId&&Object.hasOwn(CLASSES,d.classId)?d.classId:d.gadget==='repair'?'engineer':d.gadget==='medkit'?'medic':'assault';
 return {classId,primary:WEAPON_ORDER.includes(d.primary as WeaponId)&&d.primary!=='pistol'?d.primary!:DEFAULT_LOADOUT.primary,
 secondary:d.primary==='marksman'?'pistol':d.secondary==='pistol'||d.secondary==='marksman'?d.secondary:DEFAULT_LOADOUT.secondary,
 gadget:CLASSES[classId].gadget,throwable:d.throwable==='smoke'?'smoke':'frag'};
}
