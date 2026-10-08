export type MusicCue='menu'|'deployment'|'combat'|'critical'|'winning'|'barrage'|'counteroffensive'|'armored-push'|'victory'|'defeat';
export const MUSIC_TRACKS={
 menu:['main-theme','classic-theme'],deployment:['deployment'],combat:['combat-tropic','combat-resolve'],critical:['critical'],winning:['counteroffensive'],barrage:['barrage'],counteroffensive:['counteroffensive'],'armored-push':['armored-push'],victory:['victory'],defeat:['defeat']
} as const;
export interface MusicSituation {screen:string;elapsed:number;finished:boolean;winner:'blue'|'red'|null;mode:string;blue:number;red:number;initial:number;sector:number;event:string|null}
/** Music follows real battle state. Final results always outrank temporary threat cues. */
export function selectMusic(s:MusicSituation|null):MusicCue{
 if(!s)return 'menu';if(s.finished)return s.winner==='blue'?'victory':'defeat';
 if(s.screen==='respawn')return 'deployment';
 const low=s.blue<=Math.max(30,s.initial*.2),nearWin=s.mode==='breakthrough'?s.sector>=4:s.red<=Math.max(30,s.initial*.2)&&s.red<s.blue;
 if(low&&(s.mode==='breakthrough'||s.blue<=s.red))return 'critical';if(nearWin)return 'winning';
 if(s.elapsed<8)return 'deployment';if(s.event==='ARTILLERY')return 'barrage';if(s.event==='COUNTER_OFFENSIVE')return 'counteroffensive';if(s.event==='ARMORED_PUSH')return 'armored-push';return 'combat';
}
