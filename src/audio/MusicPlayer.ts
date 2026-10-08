import {MUSIC_TRACKS,type MusicCue} from './MusicDirector';
const url=(id:string)=>`${import.meta.env.BASE_URL}audio/music/${id}.mp3`;
/** Two streamed media slots: full MP3s are not decoded into large AudioBuffers. */
export class MusicPlayer {
 private duckGain=1;private masterGain=0;
 private slots:{audio:HTMLAudioElement;level:number;target:number}[]=[];private active=-1;private enabled=false;private cue:MusicCue='menu';private desired:MusicCue='menu';private since=0;private serial=0;private playlistIndex=0;private failed=new Set<string>();private retryAt=0;private pending=false;private pendingIndex=-1;private lastError='';
 currentTrack='';status:'locked'|'playing'|'missing'|'blocked'='locked';
 unlock(){this.enabled=true;if(!this.slots.length)for(let i=0;i<2;i++){const audio=new Audio();audio.preload='none';audio.volume=0;audio.addEventListener('ended',()=>{if(this.slots[this.active]?.audio===audio){this.playlistIndex++;this.playNext()}});this.slots.push({audio,level:0,target:0});}if(this.active<0||this.status==='blocked'){this.failed.clear();this.playNext();}}
 begin(cue:MusicCue){this.cue=cue;this.desired=cue;this.since=performance.now()/1000;this.playlistIndex=0;this.playNext();}
 private playNext(){if(!this.enabled||this.pending)return;const playlist=MUSIC_TRACKS[this.cue],id=playlist[this.playlistIndex%playlist.length]!;if(this.failed.has(id)){this.status='missing';return;}
  this.pending=true;const loadingCue=this.cue;const index=this.active===0?1:0,slot=this.slots[index]!,serial=++this.serial;this.pendingIndex=index;slot.audio.pause();slot.audio.src=url(id);slot.audio.load();slot.level=0;slot.target=0;slot.audio.volume=0;
  void slot.audio.play().then(()=>{this.pending=false;this.pendingIndex=-1;if(serial!==this.serial||loadingCue!==this.cue){slot.audio.pause();this.playNext();return;}for(const s of this.slots)s.target=0;slot.target=1;this.active=index;this.currentTrack=id;this.status='playing';}).catch((error:unknown)=>{this.pending=false;this.pendingIndex=-1;if(serial!==this.serial||loadingCue!==this.cue){this.playNext();return;}this.lastError=String(error);this.status=error instanceof DOMException&&error.name==='NotAllowedError'?'blocked':'missing';if(this.status==='missing')this.failed.add(id);this.retryAt=performance.now()/1000+5;});
 }
 update(desired:MusicCue,dt:number,volume:number,duck:boolean,paused:boolean){this.desired=desired;const now=performance.now()/1000,terminal=desired==='victory'||desired==='defeat'||desired==='menu';
  if(desired!==this.cue&&(terminal||now-this.since>=6)){this.cue=desired;this.since=now;this.playlistIndex=0;this.playNext();}
  if(this.enabled&&this.active<0&&!this.pending&&this.status!=='missing'&&now>=this.retryAt)this.playNext();
  this.duckGain+=((duck?.84:1)*(paused?.8:1)-this.duckGain)*(1-Math.exp(-dt*(duck?7:1.5)));this.masterGain+=(Math.min(1,volume*1.65)-this.masterGain)*(1-Math.exp(-dt*4));
  for(const [index,slot] of this.slots.entries()){slot.level+=Math.sign(slot.target-slot.level)*Math.min(Math.abs(slot.target-slot.level),dt/4);slot.audio.volume=Math.min(1,Math.max(0,Math.sin(slot.level*Math.PI/2)*(volume===0?0:this.masterGain)*this.duckGain*(document.hidden?0:1)));if(index!==this.pendingIndex&&slot.target===0&&slot.level<.001&&!slot.audio.paused)slot.audio.pause();}
 }
 get state(){return {cue:this.cue,desired:this.desired,status:this.status,error:this.lastError,track:this.currentTrack,slots:this.slots.map(s=>({paused:s.audio.paused,volume:s.audio.volume,ready:s.audio.readyState,time:s.audio.currentTime}))};}
}
