import './style.css';
import {Battle,type BattleSettings} from './core/Battle';
import {clamp} from './core/math';
import {WorldView} from './rendering/WorldView';
import {UI,type GameOptions} from './ui/UI';
import {AudioManager} from './audio/AudioManager';

const app=document.querySelector<HTMLDivElement>('#app');
if(!app)throw new Error('Missing application mount');
const ui=new UI(app),audio=new AudioManager();
let lockFailed=false,recoilPitch=0,recoilYaw=0;
let battle:Battle|null=null,world:WorldView|null=null;
let paused=true,lookYaw=0,lookPitch=0,velocityY=0,jumpOffset=0;
let mapVisible=false,scoreVisible=false,debugVisible=false;
let shotPending=false;
let aiming=false,shooting=false,crouchToggle=false,semiLatch=false;
let fps=60,moving=false,sprinting=false,sideLean=0,velocityX=0,velocityZ=0,actualSpeed=0;
let stepTravel=0,eyeSmooth=1.78,lastPlayerHP=100;
let framePrevious=performance.now(),frameAccumulator=0;
const keys=new Set<string>();
function requestLock(){
 try{const maybe=ui.canvas.requestPointerLock?.();if(maybe&&typeof maybe.catch==='function')void maybe.catch(()=>{lockFailed=true});if(!ui.canvas.requestPointerLock)lockFailed=true}catch{lockFailed=true}
}
document.addEventListener('pointerlockerror',()=>{lockFailed=true;ui.controls.textContent='鼠标锁定不可用：按住鼠标拖动视角 / Hold a mouse button and drag to look';});
function faceObjective(){if(!battle)return;const goal=battle.points.filter(p=>p.owner!=='blue').sort((a,b)=>Math.hypot(a.x-battle!.player.pos.x,a.z-battle!.player.pos.z)-Math.hypot(b.x-battle!.player.pos.x,b.z-battle!.player.pos.z))[0]??battle.points[2]!;lookYaw=Math.atan2(-(goal.x-battle.player.pos.x),-(goal.z-battle.player.pos.z));lookPitch=0;recoilPitch=0;recoilYaw=0;}
function start(options:GameOptions){
 ui.resetBattleState();audio.unlock();audio.volume=options.volume;
 world?.dispose();
 if(document.pointerLockElement)document.exitPointerLock();
 battle=new Battle({size:options.size,difficulty:options.difficulty,tickets:options.tickets,killTicketPenalty:options.killTicketPenalty} satisfies BattleSettings);
 battle.setLoadout(ui.loadout);
 try {world=new WorldView(ui.canvas,battle,options.quality,options.fov,options.renderScale)}catch(err){
  battle=null;paused=true;ui.menu();
  const banner=document.createElement('p');banner.className='render-error';
  banner.textContent=`WebGL initialization failed / WebGL 初始化失败: ${err instanceof Error?err.message:String(err)}`;
  ui.modal.querySelector('.menu-content')?.prepend(banner);return;
 }
 const p=battle.player;
 faceObjective();
 velocityY=0;jumpOffset=0;velocityX=0;velocityZ=0;stepTravel=0;eyeSmooth=1.78;lastPlayerHP=100;shooting=false;shotPending=false;aiming=false;semiLatch=false;mapVisible=false;scoreVisible=false;
 keys.clear();frameAccumulator=0;paused=false;
 ui.hideOverlay();requestLock();
}
function returnMenu(){ui.resetBattleState();mapVisible=false;scoreVisible=false;debugVisible=false;ui.tactical.classList.add('hidden');ui.scoreboard.classList.add('hidden');ui.debug.classList.add('hidden');paused=true;shooting=false;shotPending=false;audio.vehicleEngine(false,0);keys.clear();if(document.pointerLockElement)document.exitPointerLock();battle=null;world?.dispose();world=null;ui.menu()}
function resume(){if(!battle||battle.finished)return;keys.clear();frameAccumulator=0;aiming=false;shooting=false;shotPending=false;paused=false;ui.hideOverlay();requestLock()}
function respawn(at:string){if(!battle||battle.elapsed<battle.player.respawnAt)return;battle.setLoadout(ui.loadout);if(battle.respawnPlayer(at)){
  keys.clear();aiming=false;shooting=false;shotPending=false;semiLatch=false;frameAccumulator=0;
  faceObjective();velocityY=0;jumpOffset=0;velocityX=0;velocityZ=0;stepTravel=0;lastPlayerHP=battle.player.hp;paused=false;ui.hideOverlay();audio.click();requestLock();
 }}
ui.onStart=start;ui.onMenu=returnMenu;ui.onResume=resume;ui.onRespawn=respawn;
ui.onSupport=(id,at)=>{if(!battle)return;const result=battle.requestSupport(id,at);ui.support(battle,result.reason)};
ui.onChange=(options)=>{audio.volume=options.volume};

document.addEventListener('pointerlockchange',()=>{
 if(!battle||battle.finished||!battle.player.alive)return;
 if(document.pointerLockElement===ui.canvas){lockFailed=false;if(ui.screen==='pause'){paused=false;ui.hideOverlay()}}
 else if(!paused&&ui.screen==='none'){paused=true;shooting=false;shotPending=false;ui.pause()}
});
document.addEventListener('mousemove',(e)=>{
 const dragLook=lockFailed&&!document.pointerLockElement&&e.buttons!==0&&ui.screen==='none'&&!mapVisible;
 if((document.pointerLockElement!==ui.canvas&&!dragLook)||paused||!battle?.player.alive)return;
 const sense=ui.settings.sensitivity*.01*(aiming?1/Math.sqrt(battle.activeWeapon.zoom):1);
 lookYaw-=e.movementX*sense;lookPitch=clamp(lookPitch-e.movementY*sense,-1.47,1.47);
 world?.onMouse(e.movementX,e.movementY);
});
ui.canvas.addEventListener('mousedown',(e)=>{
 if(!battle||paused)return;
 if(document.pointerLockElement!==ui.canvas){requestLock();if(!lockFailed)return}
 if(e.button===0){shooting=true;shotPending=true;}if(e.button===2)aiming=true;
 audio.unlock();
});
document.addEventListener('mouseup',e=>{if(e.button===0){shooting=false;semiLatch=false}if(e.button===2)aiming=false});
document.addEventListener('contextmenu',e=>e.preventDefault());
document.addEventListener('keydown',e=>{
 if(!battle||e.target instanceof HTMLInputElement||e.target instanceof HTMLSelectElement)return;
 if(['Space','Tab','ArrowUp','ArrowDown'].includes(e.code))e.preventDefault();
 keys.add(e.code);
 if(e.repeat)return;
 if(e.code==='KeyQ'&&battle.player.alive){if(ui.screen==='support'){resume();return}if(!paused){paused=true;aiming=false;shooting=false;shotPending=false;keys.clear();mapVisible=false;scoreVisible=false;ui.toggleMap(false,battle);ui.toggleScore(false,battle);ui.support(battle);if(document.pointerLockElement)document.exitPointerLock();}return}
 if(e.code==='Escape'&&ui.screen==='support'){resume();return}
 if(paused&&e.code!=='Escape'&&e.code!=='F3')return;
 if(e.code==='KeyG'&&battle.throwGrenade({x:-Math.sin(lookYaw),z:-Math.cos(lookYaw)})){world?.weapon.throw();audio.click()}
 if(e.code==='KeyX'&&battle.useGadget())audio.click();
 if(e.code==='KeyR'&&!battle.inVehicle&&!battle.isReloading()&&battle.playerAmmo<battle.activeWeapon.magazine&&battle.playerReserveAmmo>0){battle.startReload();audio.reload()}
 if(e.code==='KeyC')crouchToggle=!crouchToggle;
 if(e.code==='Digit1'&&!battle.inVehicle&&battle.switchPlayerWeapon(battle.loadout.primary)){semiLatch=false;audio.click()}
 if(e.code==='Digit2'&&!battle.inVehicle&&battle.switchPlayerWeapon(battle.loadout.secondary)){semiLatch=false;audio.click()}
 if(e.code==='KeyE'&&!paused&&battle.togglePlayerVehicle()){aiming=false;shooting=false;shotPending=false;crouchToggle=false;velocityX=0;velocityZ=0;audio.click()}
 if(e.code==='Escape'){
  if(mapVisible){mapVisible=false;ui.toggleMap(false,battle);return}
  if(!paused){paused=true;shooting=false;shotPending=false;aiming=false;ui.pause();if(document.pointerLockElement)document.exitPointerLock()}
  // Do not automatically resume: browsers can release Pointer Lock before keydown.
 }
 if(e.code==='KeyM'&&!paused){mapVisible=!mapVisible;ui.toggleMap(mapVisible,battle)}
 if(e.code==='Tab'&&!paused){scoreVisible=true;ui.toggleScore(true,battle)}
 if(e.code==='F3'){debugVisible=!debugVisible;ui.setDebug(debugVisible,battle,fps,world?.renderer.info.render.calls??0)}
});
document.addEventListener('keyup',e=>{
 keys.delete(e.code);
 if(e.code==='Tab'&&battle){scoreVisible=false;ui.toggleScore(false,battle)}
});
window.addEventListener('blur',()=>{keys.clear();shooting=false;shotPending=false;if(battle&&!paused&&battle.player.alive){paused=true;ui.pause()}});
function playerMovement(dt:number){
 if(!battle||!battle.player.alive)return;
 const player=battle.player;recoilPitch*=Math.exp(-dt*9);recoilYaw*=Math.exp(-dt*9);
 const horizontal=(keys.has('KeyD')?1:0)-(keys.has('KeyA')?1:0);
 const forward=(keys.has('KeyW')?1:0)-(keys.has('KeyS')?1:0);
 const magnitude=Math.hypot(horizontal,forward)||1;
 if(battle.inVehicle){
  aiming=false;sprinting=false;crouchToggle=false;moving=Math.abs(forward)>0||Math.abs(horizontal)>0;actualSpeed=Math.min(1,Math.abs(battle.playerVehicle?.speed??0)/13.2);sideLean=0;
  battle.drivePlayerVehicle(forward,horizontal,dt);battle.aimPlayerVehicle(lookYaw);
  const direction={x:-Math.sin(lookYaw+recoilYaw)*Math.cos(lookPitch+recoilPitch),y:Math.sin(lookPitch+recoilPitch),z:-Math.cos(lookYaw+recoilYaw)*Math.cos(lookPitch+recoilPitch)};
  if(shooting&&world&&battle.shootPlayerVehicle(direction)){ui.hud.classList.add('firing');setTimeout(()=>ui.hud.classList.remove('firing'),110)}
  return;
 }
 const crouching=keys.has('ControlLeft')||keys.has('ControlRight')||crouchToggle;
 sprinting=keys.has('ShiftLeft')&&!aiming&&!crouching&&forward>0;
 const maxSpeed=crouching?3.8:sprinting?13.7:aiming?5.8:8.6;
 const desiredX=(Math.cos(lookYaw)*horizontal-Math.sin(lookYaw)*forward)/magnitude*maxSpeed;
 const desiredZ=(-Math.sin(lookYaw)*horizontal-Math.cos(lookYaw)*forward)/magnitude*maxSpeed;
 const tau=1-Math.exp(-dt*(horizontal||forward?13:17));
 velocityX+=(desiredX-velocityX)*tau;velocityZ+=(desiredZ-velocityZ)*tau;
 const oldX=player.pos.x,oldZ=player.pos.z;
 battle.movePlayer(velocityX*dt,velocityZ*dt);player.yaw=lookYaw+Math.PI;player.velocity={x:velocityX,z:velocityZ};
 const traveled=Math.hypot(player.pos.x-oldX,player.pos.z-oldZ);
 const speed=Math.hypot(velocityX,velocityZ);
 moving=speed>1;actualSpeed=Math.min(1,speed/9);
 sideLean+=(horizontal*.75-sideLean)*(1-Math.exp(-dt*9));
 if(traveled<dt*speed*.22){velocityX*=.65;velocityZ*=.65}
 if(jumpOffset<=0&&keys.has('Space')&&!crouching){velocityY=8.4;jumpOffset=.001;audio.jump()}
 if(jumpOffset>0||velocityY>0){velocityY-=22*dt;jumpOffset=Math.max(0,jumpOffset+velocityY*dt);if(jumpOffset===0){velocityY=0;audio.land()}}
 if(moving&&jumpOffset===0){stepTravel+=traveled;if(stepTravel>(sprinting?4.1:crouching?3.5:3.2)){
  stepTravel=0;audio.footstep(sprinting ? 1.25 : crouching ? .52 : 1)
 }}else if(!moving)stepTravel=0;
 battle.playerAiming=aiming;
 const canFireHeld=battle.activeWeapon.automatic||!semiLatch;
 if((shooting||shotPending)&&canFireHeld&&world&&!sprinting){
  const direction={x:-Math.sin(lookYaw+recoilYaw)*Math.cos(lookPitch+recoilPitch),y:Math.sin(lookPitch+recoilPitch),z:-Math.cos(lookYaw+recoilYaw)*Math.cos(lookPitch+recoilPitch)};
  if(battle.shootPlayer(direction,(crouching?1.07:1.78)+jumpOffset)){
   shotPending=false;if(!battle.activeWeapon.automatic)semiLatch=true;
   world.playerShot(battle.playerWeapon);recoilPitch=Math.min(.12,recoilPitch+battle.activeWeapon.recoil*.07);recoilYaw+=(battle.random.next()-.5)*battle.activeWeapon.recoil*.02;ui.hud.classList.add('firing');setTimeout(()=>ui.hud.classList.remove('firing'),85)
  }
 }
}
function frame(time:number){
 requestAnimationFrame(frame);
 const rawDt=Math.max(.001,(time-framePrevious)/1000),dt=clamp(rawDt,0,.065);framePrevious=time;
 fps=fps*.92+(.08/rawDt);
 if(!battle||!world){audio.vehicleEngine(false,0);return}
 audio.vehicleEngine(battle.inVehicle&&!paused,Math.abs(battle.playerVehicle?.speed??0)/13.2);
 if(!paused&&!battle.finished){frameAccumulator=Math.min(.18,frameAccumulator+dt);
  let i=0;while(frameAccumulator>=1/60&&i++<8){playerMovement(1/60);battle.tick(1/60);frameAccumulator-=1/60}
 }
 const events=battle.events();
 if(events.length){ui.logEvents(events,battle);audio.play(events,battle.player.pos,lookYaw);world.showEvents(events,battle.player.pos);
  if(events.some(e=>e.type==='shot'&&e.player&&e.hit))ui.showHit();
 }
 if(!battle.player.alive&&!battle.finished&&ui.screen!=='respawn'){
  paused=false;shooting=false;shotPending=false;if(document.pointerLockElement)document.exitPointerLock();ui.dead(battle);
 }
 if(battle.finished&&ui.screen!=='end'){
  paused=true;shooting=false;shotPending=false;if(document.pointerLockElement)document.exitPointerLock();ui.end(battle);
 }
 const crouched=(keys.has('ControlLeft')||keys.has('ControlRight')||crouchToggle);
 const eyeTarget=crouched?1.07:1.78;eyeSmooth+=(eyeTarget-eyeSmooth)*(1-Math.exp(-dt*12));
 if(battle.player.hp<lastPlayerHP){audio.hurt();ui.hud.classList.remove('hurt');void ui.hud.offsetWidth;ui.hud.classList.add('hurt');setTimeout(()=>ui.hud.classList.remove('hurt'),430)}
 lastPlayerHP=battle.player.hp;
 world.render(battle,dt,{yaw:lookYaw+recoilYaw,pitch:lookPitch+recoilPitch,height:eyeSmooth+jumpOffset,ads:aiming,moving:moving&&!paused,sprint:sprinting,reload:battle.isReloading(),speed:actualSpeed,side:sideLean});
 ui.hud.classList.toggle('aiming',aiming&&!battle.isReloading()&&!battle.inVehicle);ui.hud.classList.toggle('scoped',aiming&&battle.activeWeapon.zoom>=3&&!battle.isReloading()&&!battle.inVehicle);ui.hud.style.setProperty('--ads-blend',String(world.weapon.aimBlend));ui.hud.classList.toggle('sprinting',sprinting);
 ui.drawHUD(battle,time/1000);
 if(mapVisible)ui.toggleMap(true,battle);
 if(debugVisible)ui.setDebug(true,battle,fps,world.renderer.info.render.calls);
}
requestAnimationFrame(frame);

// Development-only QA diagnostics. The production bundle removes this branch.
if(import.meta.env.DEV)Object.defineProperty(window,'__IRONFRONT_QA',{value:{get battle(){return battle},get world(){return world},get fps(){return fps},get paused(){return paused}},configurable:true});
