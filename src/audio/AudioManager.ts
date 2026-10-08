import type {BattleEvent} from '../core/Battle';
import {dist,clamp,type Point} from '../core/math';

/** Layered, locally generated audio with simple stereo positioning for nearby battlefield events. */
export class AudioManager {
 private context:AudioContext|null=null;private noise:AudioBuffer|null=null;private engineOsc:OscillatorNode|null=null;private engineGain:GainNode|null=null;volume=.42;
 unlock(){if(!this.context){try{this.context=new AudioContext();this.noise=this.makeNoise(this.context)}catch{return}}void this.context.resume()}
 private makeNoise(ctx:AudioContext){const buf=ctx.createBuffer(1,ctx.sampleRate*2,ctx.sampleRate),data=buf.getChannelData(0);for(let i=0;i<data.length;i++)data[i]=(Math.random()*2-1);return buf}
 private output(gain:GainNode,pan:number){const c=this.context;if(!c)return;try{const p=c.createStereoPanner();p.pan.value=clamp(pan,-1,1);gain.connect(p).connect(c.destination)}catch{gain.connect(c.destination)}}
 private beep(f:number,t:number,g:number,sweep:number,type:OscillatorType='sawtooth',pan=0){const c=this.context;if(!c||c.state!=='running'||this.volume<=0)return;const o=c.createOscillator(),gain=c.createGain(),start=c.currentTime;o.type=type;o.frequency.setValueAtTime(f,start);o.frequency.exponentialRampToValueAtTime(Math.max(22,sweep),start+t);gain.gain.setValueAtTime(Math.max(.0001,g*this.volume),start);gain.gain.exponentialRampToValueAtTime(.0001,start+t);o.connect(gain);this.output(gain,pan);o.start(start);o.stop(start+t+.005)}
 private noiseBurst(duration:number,volume:number,cutoff:number,pan=0){const c=this.context;if(!c||!this.noise||c.state!=='running'||this.volume<=0)return;const src=c.createBufferSource(),filter=c.createBiquadFilter(),gain=c.createGain(),now=c.currentTime;src.buffer=this.noise;src.loop=true;filter.type='lowpass';filter.frequency.setValueAtTime(cutoff,now);filter.frequency.exponentialRampToValueAtTime(Math.max(100,cutoff*.19),now+duration);gain.gain.setValueAtTime(volume*this.volume,now);gain.gain.exponentialRampToValueAtTime(.0001,now+duration);src.connect(filter).connect(gain);this.output(gain,pan);src.start(now);src.stop(now+duration+.008)}
 private spatial(from:Point,listener:Point,yaw:number){const dx=from.x-listener.x,dz=from.z-listener.z,d=Math.hypot(dx,dz)||1;return clamp((dx*Math.cos(yaw)-dz*Math.sin(yaw))/d,-.85,.85)}
 shoot(loudness=1,pan=0){this.beep(130,.13,.14*loudness,36,'sawtooth',pan);this.noiseBurst(.145,.29*loudness,3200,pan)}
 vehicleCannon(loudness=1,pan=0){this.beep(74,.26,.20*loudness,28,'sawtooth',pan);this.noiseBurst(.31,.43*loudness,1800,pan)}
 vehicleEngine(active:boolean,speed01:number){const c=this.context;if(!c||c.state!=='running')return;if(!this.engineOsc){this.engineOsc=c.createOscillator();this.engineGain=c.createGain();this.engineOsc.type='sawtooth';this.engineOsc.frequency.value=48;this.engineGain.gain.value=.0001;this.engineOsc.connect(this.engineGain).connect(c.destination);this.engineOsc.start()}const now=c.currentTime,target=active&&this.volume>0?(.012+.023*clamp(speed01,0,1))*this.volume:.0001;this.engineOsc.frequency.setTargetAtTime(48+54*clamp(speed01,0,1),now,.08);this.engineGain!.gain.setTargetAtTime(target,now,.10)}
 hit(){this.beep(820,.07,.07,370,'triangle')}
 click(){this.beep(550,.045,.05,800,'square')}
 reload(){this.beep(680,.06,.045,410,'triangle');setTimeout(()=>this.beep(450,.09,.06,580,'triangle'),450)}
 footstep(loudness=1){this.noiseBurst(.082,.12*loudness,620)}
 jump(){this.noiseBurst(.09,.06,950)}
 land(){this.noiseBurst(.11,.15,420)}
 hurt(){this.beep(95,.23,.11,40,'sine')}
 capture(){this.beep(330,.23,.05,680,'triangle')}
 gameEnd(){this.beep(380,.55,.08,190,'triangle')}
 artillery(loudness=1,pan=0){this.beep(58,.5,.19*loudness,24,'sawtooth',pan);this.noiseBurst(.62,.46*loudness,920,pan)}
 eventWarning(){this.beep(720,.16,.055,510,'square');setTimeout(()=>this.beep(720,.16,.05,510,'square'),210)}
 play(events:BattleEvent[],listener:Point,yaw=0){let distantSounds=0;for(const e of events){if(e.type==='shot'){const d=dist(e.from,listener);if(e.player){this.shoot(1);if(e.hit)this.hit()}else if(d<190&&distantSounds++<7){const loudness=Math.max(.045,1-d/195),pan=this.spatial(e.from,listener,yaw);this.shoot(loudness*.22,pan)}}else if(e.type==='vehicleShot'){const d=dist(e.from,listener),pan=this.spatial(e.from,listener,yaw);if(e.player)this.vehicleCannon(1,pan);else if(d<260&&distantSounds++<7)this.vehicleCannon(Math.max(.06,1-d/270)*.42,pan)}else if(e.type==='vehicleDisabled'){const d=dist(e.at,listener);if(d<260){const pan=this.spatial(e.at,listener,yaw),loudness=Math.max(.08,1-d/270);this.vehicleCannon(loudness*.72,pan);this.noiseBurst(.48,.34*loudness,760,pan)}}else if(e.type==='artilleryImpact'){const d=dist(e.at,listener),pan=this.spatial(e.at,listener,yaw);if(d<420)this.artillery(Math.max(.10,1-d/440),pan)}else if(e.type==='majorEvent'&&e.action==='warning')this.eventWarning();else if(e.type==='majorEvent'&&e.action==='start'){this.beep(260,.28,.055,420,'triangle')}else if(e.type==='capture')this.capture();else if(e.type==='end')this.gameEnd()}}
}
