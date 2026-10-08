import {describe,it,expect} from 'vitest';
import * as THREE from 'three';
import {selectMusic,MUSIC_TRACKS,type MusicSituation} from '../audio/MusicDirector';
import {buildWeaponModel,MODEL_PROFILES} from '../rendering/WeaponModels';
import {WEAPON_ORDER} from '../combat/Weapons';
import {weaponSilhouette} from '../ui/WeaponSilhouette';
const situation:MusicSituation={screen:'none',elapsed:90,finished:false,winner:null,mode:'conquest',blue:450,red:400,initial:500,sector:0,event:null};
describe('Music follows game state',()=>{
 it('resolves menu, deployment, combat and all major events',()=>{expect(selectMusic(null)).toBe('menu');expect(selectMusic({...situation,elapsed:3})).toBe('deployment');expect(selectMusic({...situation,screen:'respawn'})).toBe('deployment');expect(selectMusic(situation)).toBe('combat');for(const [event,cue] of [['ARTILLERY','barrage'],['COUNTER_OFFENSIVE','counteroffensive'],['ARMORED_PUSH','armored-push']] as const)expect(selectMusic({...situation,event})).toBe(cue);});
 it('outcome outranks danger and losing/winning pressures reflect tickets or sectors',()=>{expect(selectMusic({...situation,blue:70})).toBe('critical');expect(selectMusic({...situation,red:60})).toBe('winning');expect(selectMusic({...situation,mode:'breakthrough',sector:4})).toBe('winning');expect(selectMusic({...situation,mode:'breakthrough',blue:50})).toBe('critical');expect(selectMusic({...situation,finished:true,winner:'blue',blue:0,event:'ARTILLERY'})).toBe('victory');expect(selectMusic({...situation,finished:true,winner:'red'})).toBe('defeat');});
 it('maps all eleven provided tracks and keeps streamed playlists nonempty',()=>{expect(new Set(Object.values(MUSIC_TRACKS).flat()).size).toBe(11);});
});
describe('Separate weapon structures',()=>{
 it('gives all seven weapons unique geometry profiles and menu silhouettes',()=>{const material=new THREE.MeshStandardMaterial(),m=Object.fromEntries(['steel','matte','grip','tan','glove','gold','glass'].map(k=>[k,material]));const profiles=new Set<string>(),icons=new Set<string>();for(const id of WEAPON_ORDER){const model=buildWeaponModel(id,m);expect(model.name).toBe(`weapon-${id}`);profiles.add(model.userData.profile);icons.add(weaponSilhouette(id));const names=model.children.map(c=>c.name);if(id==='pistol'){expect(names.some(n=>n.includes('stock'))).toBe(false);expect(names).toContain('slide');expect(names.filter(n=>n==='gloved-hand')).toHaveLength(1);}if(id==='lmg')expect(names).toContain('ammo-box');if(id==='sniper')expect(names).toContain('bolt-handle');expect(MODEL_PROFILES[id].muzzle).toBeLessThan(-.7);model.traverse(o=>{if(o instanceof THREE.Mesh)o.geometry.dispose()});}expect(profiles.size).toBe(7);expect(icons.size).toBe(7);material.dispose();});
});
