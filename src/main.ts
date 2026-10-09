import './style.css';
import {lanAddress,lanJSON} from './network/LanAddress';
import {LanClient} from './network/LanClient';
import type {EquipmentItem} from './rendering/EquipmentView';
import {Battle,type BattleSettings} from './core/Battle';
import {clamp,heightAt} from './core/math';
import {WorldView} from './rendering/WorldView';
import {UI,type GameOptions} from './ui/UI';
import {AudioManager} from './audio/AudioManager';

const app=document.querySelector<HTMLDivElement>('#app');
if(!app)throw new Error('Missing application mount');
const ui=new UI(app),audio=new AudioManager();audio.volume=ui.settings.volume;audio.musicVolume=ui.settings.musicVolume;audio.effectsVolume=ui.settings.effectsVolume;audio.ambientVolume=ui.settings.ambientVolume;
let nextLockAlarm=0;let giveUpHeld=false;let waitingLateReady=false;let nextEquipmentAction=0;let selectedEquipment:EquipmentItem|null=null,equipmentUntil=0,equipmentUsedAt=-99999;
let lockFailed=false,recoilPitch=0,recoilYaw=0;
let lan:LanClient|null=null;
let battle:Battle|null=null,world:WorldView|null=null;
let resultStarted=0;let introRemaining=0;let infantryFirstPerson=true;let deathAge:number|null=null;let vehicleFirstPerson=true,lastLockPause=0;
let paused=true,lookYaw=0,lookPitch=0,velocityY=0,jumpOffset=0;
let mapVisible=false,scoreVisible=false,debugVisible=false;
let shotPending=false,shiftPressedAt=0;
let aiming=false,shooting=false,crouchToggle=false,semiLatch=false;
let fps=60,moving=false,sprinting=false,sideLean=0,velocityX=0,velocityZ=0,actualSpeed=0;
let stepTravel=0,eyeSmooth=1.78,lastPlayerHP=100;
let framePrevious=performance.now(),frameAccumulator=0;
const keys=new Set<string>();
function requestLock(){
 try{const maybe=ui.canvas.requestPointerLock?.();if(maybe&&typeof maybe.catch==='function')void maybe.catch(()=>{lockFailed=true});if(!ui.canvas.requestPointerLock)lockFailed=true}catch{lockFailed=true}
}
document.addEventListener('pointerlockerror',()=>{lockFailed=true;ui.controls.textContent='鼠标锁定不可用：按住鼠标拖动视角 / Hold a mouse button and drag to look';});
function faceObjective(){if(!battle)return;const goal=battle.points.filter(p=>p.owner!==battle!.player.team).sort((a,b)=>Math.hypot(a.x-battle!.player.pos.x,a.z-battle!.player.pos.z)-Math.hypot(b.x-battle!.player.pos.x,b.z-battle!.player.pos.z))[0]??battle.points[2]!;lookYaw=Math.atan2(-(goal.x-battle.player.pos.x),-(goal.z-battle.player.pos.z));lookPitch=0;recoilPitch=0;recoilYaw=0;}
function start(options:GameOptions,shared?:Battle){
 ui.resetBattleState();audio.unlock();audio.volume=options.volume;audio.musicVolume=options.musicVolume;audio.effectsVolume=options.effectsVolume;audio.ambientVolume=options.ambientVolume;audio.music.begin('deployment');
 world?.dispose();
 if(document.pointerLockElement)document.exitPointerLock();
 battle=shared??new Battle({mode:options.mode,size:options.size,difficulty:options.difficulty,tickets:options.tickets,vehicleLimit:options.vehicleLimit,killTicketPenalty:options.killTicketPenalty} satisfies BattleSettings);
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
 selectedEquipment=null;equipmentUntil=0;keys.clear();frameAccumulator=0;paused=false;
 paused=true;introRemaining=0;ui.preparation();
}
function returnMenu(){ui.isLanHost=false;ui.isLanSession=false;ui.connection=null;lan?.close();lan=null;ui.resetBattleState();mapVisible=false;scoreVisible=false;debugVisible=false;ui.tactical.classList.add('hidden');ui.scoreboard.classList.add('hidden');ui.debug.classList.add('hidden');paused=true;shooting=false;shotPending=false;audio.vehicleEngine(false,0);keys.clear();if(document.pointerLockElement)document.exitPointerLock();battle=null;world?.dispose();world=null;ui.menu()}
function resume(){if(!battle||battle.finished)return;keys.clear();frameAccumulator=0;aiming=false;shooting=false;shotPending=false;paused=false;ui.hideOverlay();requestLock()}
function respawn(at:string){if(lan){lan.action('respawn',at,{loadout:ui.loadout});return;}if(!battle||battle.elapsed<battle.player.respawnAt)return;battle.setLoadout(ui.loadout);if(battle.respawnPlayer(at)){
  selectedEquipment=null;equipmentUntil=0;deathAge=null;keys.clear();aiming=false;shooting=false;shotPending=false;semiLatch=false;frameAccumulator=0;
  faceObjective();velocityY=0;jumpOffset=0;velocityX=0;velocityZ=0;stepTravel=0;lastPlayerHP=battle.player.hp;paused=false;ui.hideOverlay();audio.click();requestLock();
 }}
ui.onLoadoutChanged=()=>{lan?.action('unready');const btn=ui.modal.querySelector<HTMLButtonElement>('#ready');if(btn)btn.textContent='配装完成 / READY';};
ui.onReady=()=>{if(!battle)return;if(lan){lan.action('ready',undefined,{loadout:ui.loadout});const btn=ui.modal.querySelector<HTMLButtonElement>('#ready');if(btn)btn.textContent='已准备 / READY ✓';if(lan.phase==='battle')waitingLateReady=true;}else{battle.setLoadout(ui.loadout);introRemaining=3;ui.intro(3);}};
ui.onBegin=()=>lan?.action('begin');
ui.onAssignTeam=(id,team)=>lan?.action('team',id,{team});
ui.onProbe=async(address)=>{const el=ui.modal.querySelector('#lan-status');if(el)el.textContent='正在测试 HTTP 服务…';try{const url=lanAddress(address),info=await lanJSON(new URL('/api/status',url));if(el)el.textContent=`服务可达：${url.host} · 版本 ${info.version} · ${info.rooms.length} 个房间`;}catch(error){if(el)el.textContent=String(error);}};
let connecting=false;
ui.onNetwork=async(address,code,host)=>{if(connecting)return;connecting=true;const status=ui.modal.querySelector('#lan-status');if(status)status.textContent='连接房间服务…';const client=new LanClient();try{const shared=await client.connect(address,code,host,{...ui.settings,loadout:ui.loadout,aiEnabled:ui.modal.querySelector<HTMLInputElement>('#lan-ai')?.checked!==false,joinTeam:ui.modal.querySelector<HTMLSelectElement>('#lan-team')?.value??'blue'});lan?.close();lan=client;ui.isLanHost=client.isHost;ui.isLanSession=true;ui.connection={address:client.address,code:client.code,shareUrls:client.shareUrls};client.onRelocate=()=>{velocityX=0;velocityZ=0;velocityY=0;jumpOffset=0;};client.onDisconnect=()=>{returnMenu();const el=ui.modal.querySelector('#lan-status');if(el)el.textContent='连接已断开，请重新加入房间。';};start(ui.settings,shared);client.action('loadout',undefined,{loadout:ui.loadout});ui.hud.querySelector('#lan-hud')!.textContent=`LAN ${client.code} · YOU ${client.id+1} · IP ${address}`;}catch(error){client.close();if(status)status.textContent=String(error);}finally{connecting=false;}};
ui.onCallRescue=()=>{if(lan)lan.action('rescue');else battle?.callRescue();};ui.onGiveUp=held=>giveUpHeld=held;ui.onVehicleAmmo=mode=>{if(lan)lan.action('veh-ammo',mode);else battle?.setVehicleAmmo(mode);};
ui.onUnstuck=()=>{if(lan){lan.action('unstuck');resume();return;}if(battle?.unstuckPlayer()){velocityX=0;velocityZ=0;velocityY=0;jumpOffset=0;resume();}};
ui.onStart=options=>{lan?.close();lan=null;ui.isLanHost=false;ui.isLanSession=false;ui.connection=null;ui.hud.querySelector('#lan-hud')!.textContent='';start(options);};ui.onMenu=returnMenu;ui.onResume=resume;ui.onRespawn=respawn;
ui.onSupport=(id,at)=>{if(!battle)return;if(lan){lan.action('support',id,{at});ui.support(battle,'请求已发送到房主');return;}const result=battle.requestSupport(id,at);ui.support(battle,result.reason)};
document.addEventListener('click',e=>{if((e.target as HTMLElement).closest('button,input,select')){audio.unlock();if((e.target as HTMLElement).closest('button'))audio.click()}},{capture:true});
ui.onChange=(options)=>{audio.volume=options.volume;audio.musicVolume=options.musicVolume;audio.effectsVolume=options.effectsVolume;audio.ambientVolume=options.ambientVolume};

document.addEventListener('pointerlockchange',()=>{
 if(!battle||battle.finished||!battle.player.alive)return;
 if(document.pointerLockElement===ui.canvas){lockFailed=false;if(paused)document.exitPointerLock();}
 else if(!paused&&ui.screen==='none'){lastLockPause=performance.now();paused=true;shooting=false;shotPending=false;ui.pause()}
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
 if(document.pointerLockElement!==ui.canvas){requestLock();}
 if(e.button===0){shooting=true;shotPending=true;semiLatch=false;}if(e.button===2)aiming=true;
 audio.unlock();
});
document.addEventListener('mouseup',e=>{if(e.button===0){shooting=false;semiLatch=false}if(e.button===2)aiming=false});
document.addEventListener('contextmenu',e=>e.preventDefault());
document.addEventListener('keydown',e=>{
 if(!battle||e.target instanceof HTMLInputElement||e.target instanceof HTMLSelectElement)return;
 if(['Space','Tab','ArrowUp','ArrowDown','ArrowLeft','ArrowRight','F1','F2','F3','F4'].includes(e.code))e.preventDefault();
 keys.add(e.code);
 if(e.repeat)return;if(battle.player.downedUntil>battle.elapsed){if(e.code==='KeyH')ui.onCallRescue();return;}
 if(e.code==='KeyQ'&&battle.player.alive){if(ui.screen==='support'){resume();return}if(!paused){paused=true;aiming=false;shooting=false;shotPending=false;keys.clear();mapVisible=false;scoreVisible=false;ui.toggleMap(false,battle);ui.toggleScore(false,battle);ui.support(battle);if(document.pointerLockElement)document.exitPointerLock();}return}
 if(e.code==='Escape'&&ui.screen==='support'){resume();return}
 if(['preparation','intro'].includes(ui.screen))return;
 if(paused&&e.code!=='Escape'&&e.code!=='F3')return;
 const equipmentKey:Record<string,EquipmentItem>={KeyG:battle.loadout.throwable,KeyX:battle.loadout.gadget,KeyZ:'at',KeyB:'aa',Digit3:battle.loadout.gadget,Digit4:battle.loadout.throwable,Digit5:'at',Digit6:'aa'};
 if(equipmentKey[e.code]&&!battle.inVehicle){const item=equipmentKey[e.code]!;if((item==='at'||item==='aa')&&battle.loadout.classId!=='engineer')return;selectedEquipment=item;equipmentUntil=Infinity;equipmentUsedAt=-99999;shooting=false;shotPending=false;semiLatch=false;aiming=false;audio.click();return;}
 if(battle.inVehicle&&['Digit1','Digit2','KeyX'].includes(e.code)){if(lan)lan.action(e.code==='KeyX'?'flares':'veh-weapon',e.code==='Digit2'?1:0);else if(e.code==='KeyX')battle.deployFlares();else battle.selectVehicleWeapon(e.code==='Digit2'?1:0);return;}
 if(lan&&!paused){const actions:Record<string,string>={KeyR:'reload',KeyE:'enter',Digit1:'weapon',Digit2:'weapon',F1:'seat',F2:'seat',F3:'seat',F4:'seat'};const action=actions[e.code];if(action){lan.action(action,action==='weapon'?(e.code==='Digit1'?battle.loadout.primary:battle.loadout.secondary):action==='seat'?Number(e.code.slice(1))-1:undefined);if(action==='weapon'){selectedEquipment=null;equipmentUntil=0;shooting=false;semiLatch=false;}return;}}
 if((e.code==='ShiftLeft'||e.code==='ShiftRight')&&!battle.inVehicle){shiftPressedAt=performance.now();if(jumpOffset===0&&battle.startSlide({x:velocityX,z:velocityZ},Math.hypot(velocityX,velocityZ))){aiming=false;crouchToggle=false;audio.land()}}
 if(battle.inVehicle&&['F1','F2','F3','F4'].includes(e.code)){battle.switchVehicleSeat(Number(e.code.slice(1))-1);shooting=false;shotPending=false;audio.click();return}
 if(e.code==='KeyR'&&!battle.inVehicle&&!battle.isReloading()&&battle.playerAmmo<battle.activeWeapon.magazine&&battle.playerReserveAmmo>0){battle.startReload();audio.reload(battle.playerWeapon)}
 if(e.code==='KeyV'){if(battle.inVehicle)vehicleFirstPerson=!vehicleFirstPerson;else infantryFirstPerson=!infantryFirstPerson;}
 if(e.code==='KeyC')crouchToggle=!crouchToggle;
 if(e.code==='Digit1'&&!battle.inVehicle){battle.switchPlayerWeapon(battle.loadout.primary);selectedEquipment=null;semiLatch=false;audio.click()}
 if(e.code==='Digit2'&&!battle.inVehicle){battle.switchPlayerWeapon(battle.loadout.secondary);selectedEquipment=null;semiLatch=false;audio.click()}
 if(e.code==='KeyE'&&!paused&&battle.togglePlayerVehicle()){if(battle.inVehicle){lookYaw=battle.playerVehicle!.yaw-Math.PI;lookPitch=-.13;}aiming=false;shooting=false;shotPending=false;crouchToggle=false;velocityX=0;velocityZ=0;audio.click()}
 if(e.code==='Escape'){
  if(mapVisible){mapVisible=false;ui.toggleMap(false,battle);return}
  if(!paused){paused=true;shooting=false;shotPending=false;aiming=false;ui.pause();if(document.pointerLockElement)document.exitPointerLock()}
  else if(ui.screen==='pause'&&performance.now()-lastLockPause>150)resume();
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
 if(lan){lan.input({muzzleOffset:world?(()=>{const m=world.playerMuzzle(battle,infantryFirstPerson||aiming),p=battle.player.pos;return {x:m.x-p.x,y:m.y-heightAt(p.x,p.z),z:m.z-p.z};})():undefined,yaw:lookYaw,pitch:lookPitch,forward,side:horizontal,up:(keys.has('Space')?1:0)-(keys.has('ControlLeft')?1:0),fire:shooting&&!selectedEquipment,ads:aiming,sprint:(keys.has('ShiftLeft')||keys.has('ShiftRight'))&&performance.now()-shiftPressedAt>180&&forward>0,crouch:crouchToggle||keys.has('ControlLeft')});if(battle.inVehicle){moving=Math.abs(forward)+Math.abs(horizontal)>0;actualSpeed=Math.min(1,Math.abs(battle.playerVehicle?.speed??0)/13.2);return;}}
 if(battle.inVehicle){
  sprinting=false;crouchToggle=false;moving=Math.abs(forward)>0||Math.abs(horizontal)>0;actualSpeed=Math.min(1,Math.abs(battle.playerVehicle?.speed??0)/13.2);sideLean=0;
  const arrows=keys.has('ArrowUp')||keys.has('ArrowDown')||keys.has('ArrowLeft')||keys.has('ArrowRight');
  battle.drivePlayerVehicle(arrows?(keys.has('ArrowUp')?1:0)-(keys.has('ArrowDown')?1:0):forward,arrows?(keys.has('ArrowRight')?1:0)-(keys.has('ArrowLeft')?1:0):horizontal,dt,arrows?undefined:lookYaw,keys.has('Space'));battle.aimPlayerVehicle(lookYaw,lookPitch,dt);battle.changeAltitude((keys.has('Space')?1:0)-(keys.has('ControlLeft')?1:0),dt);
  const v=battle.playerVehicle!,mg=battle.playerSeat===1||v.kind==='scout'||v.weaponSlot===1;
  if((shooting||shotPending)&&world&&battle.elapsed>=(mg?v.nextMGShot:v.nextShot)&&battle.playerSeat<2){const direction=world.vehicleAim(battle);if(battle.shootPlayerVehicle(direction)){shotPending=false;ui.hud.classList.add('firing');setTimeout(()=>ui.hud.classList.remove('firing'),110)}}
  return;
 }
 const crouching=battle.sliding||keys.has('ControlLeft')||keys.has('ControlRight')||crouchToggle;
 sprinting=(keys.has('ShiftLeft')||keys.has('ShiftRight'))&&performance.now()-shiftPressedAt>180&&!aiming&&!crouching&&forward>0;
 const maxSpeed=crouching?3.8:sprinting?13.7:aiming?5.8:8.6;
 let desiredX=(Math.cos(lookYaw)*horizontal-Math.sin(lookYaw)*forward)/magnitude*maxSpeed;
 let desiredZ=(-Math.sin(lookYaw)*horizontal-Math.cos(lookYaw)*forward)/magnitude*maxSpeed;
 if(battle.sliding){const remaining=(battle.slideUntil-battle.elapsed)/.7,slideSpeed=7+8*remaining;desiredX=battle.slideDirection.x*slideSpeed;desiredZ=battle.slideDirection.z*slideSpeed;sprinting=false;aiming=false;}
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
 if(equipmentUntil<performance.now())selectedEquipment=null;
 if(lan){if(selectedEquipment&&(shooting||shotPending)&&(!semiLatch||selectedEquipment==='aa'||selectedEquipment==='repair')&&performance.now()>=nextEquipmentAction){nextEquipmentAction=performance.now()+300;const m=world?.equipment.muzzleWorldPosition(),p=battle.player.pos;lan.action(selectedEquipment==='aa'?'aa':selectedEquipment==='at'?'rocket':selectedEquipment==='frag'||selectedEquipment==='smoke'?'grenade':'gadget',undefined,{direction:{x:-Math.sin(lookYaw)*Math.cos(lookPitch),y:Math.sin(lookPitch),z:-Math.cos(lookYaw)*Math.cos(lookPitch)},muzzleOffset:m?{x:m.x-p.x,y:m.y-heightAt(p.x,p.z),z:m.z-p.z}:undefined});semiLatch=true;shotPending=false;equipmentUsedAt=performance.now();}return;}
 if(selectedEquipment&&(shooting||shotPending)&&(!semiLatch||selectedEquipment==='aa'||selectedEquipment==='repair')){const d={x:-Math.sin(lookYaw)*Math.cos(lookPitch),y:Math.sin(lookPitch),z:-Math.cos(lookYaw)*Math.cos(lookPitch)};const ok=selectedEquipment==='aa'?battle.fireAA(d,world?.equipment.muzzleWorldPosition()):selectedEquipment==='at'?battle.fireRocket(d,world?.equipment.muzzleWorldPosition()):selectedEquipment==='frag'||selectedEquipment==='smoke'?battle.throwGrenade(d):battle.useGadget();if(ok){equipmentUsedAt=performance.now();world?.weapon.throw();audio.click();}semiLatch=true;shotPending=false;}
 const canFireHeld=battle.activeWeapon.automatic||!semiLatch;
 if((shooting||shotPending)&&canFireHeld&&world&&!sprinting&&!selectedEquipment){
  const direction={x:-Math.sin(lookYaw+recoilYaw)*Math.cos(lookPitch+recoilPitch),y:Math.sin(lookPitch+recoilPitch),z:-Math.cos(lookYaw+recoilYaw)*Math.cos(lookPitch+recoilPitch)};
  if(battle.shootPlayer(direction,(crouching?1.07:1.78)+jumpOffset,world.playerMuzzle(battle,infantryFirstPerson||aiming))){
   shotPending=false;if(!battle.activeWeapon.automatic)semiLatch=true;
   world.playerShot(battle.playerWeapon);recoilPitch=Math.min(.12,recoilPitch+battle.activeWeapon.recoil*.07);recoilYaw+=(battle.random.next()-.5)*battle.activeWeapon.recoil*.02;ui.hud.classList.add('firing');setTimeout(()=>ui.hud.classList.remove('firing'),85)
  }
 }
}
function frame(time:number){
 requestAnimationFrame(frame);
 const rawDt=Math.max(.001,(time-framePrevious)/1000),dt=clamp(rawDt,0,.065);framePrevious=time;
 fps=fps*.92+(.08/rawDt);
 audio.update(battle?{screen:ui.screen,elapsed:battle.elapsed,finished:battle.finished,winner:battle.winner,mode:battle.settings.mode??'conquest',blue:battle.tickets.blue,red:battle.tickets.red,initial:battle.settings.tickets,sector:battle.sectorIndex,event:battle.getMajorEvent()?.kind??null}:null,dt,paused);
 if(!battle||!world){audio.vehicleEngine(false,0);return}
 audio.vehicleEngine(battle.inVehicle&&!paused,Math.abs(battle.playerVehicle?.speed??0)/13.2);
 if(ui.screen==='preparation'&&lan){ui.updatePreparation(lan.roster);if(lan.phase==='battle'&&waitingLateReady&&battle.player.alive){waitingLateReady=false;paused=false;ui.hideOverlay();requestLock();}if(lan.phase==='countdown')ui.intro(lan.countdown);}
 if(ui.screen==='intro'){const left=lan?lan.countdown:(introRemaining=Math.max(0,introRemaining-dt));ui.modal.querySelector('#entry-countdown')!.textContent=String(Math.ceil(left));if((lan?lan.phase==='battle':left===0)){paused=false;ui.hideOverlay();requestLock();battle.player.spawnGraceUntil=battle.elapsed+3;}}
 if(!paused&&!battle.finished){frameAccumulator=Math.min(.18,frameAccumulator+dt);
  let i=0;while(frameAccumulator>=1/60&&i++<8){playerMovement(1/60);if(!lan)battle.tick(1/60);frameAccumulator-=1/60}
 }
 if(lan){if(paused)lan.input({forward:0,side:0,fire:false,yaw:lookYaw,pitch:lookPitch});lan.reconcile(battle,dt);}
 const events=lan?lan.events():battle.events();
 if(events.length){if(lan)for(const event of events)if(event.type==='shot'&&event.player){world.playerShot(event.weapon??battle.playerWeapon);recoilPitch=Math.min(.12,recoilPitch+battle.activeWeapon.recoil*.07);recoilYaw+=(Math.random()-.5)*battle.activeWeapon.recoil*.02;}for(const event of events)if(event.type==='playerHit'||(event.type==='vehicleHit'&&event.id===battle.playerVehicle?.id))ui.showDamage(event.amount,event.from,battle,lookYaw);ui.logEvents(events,battle);audio.play(events,battle.player.pos,lookYaw);world.showEvents(events,battle.player.pos);
  for(const e of events)if(e.type==='thrown'&&e.owner===battle.player.id){equipmentUsedAt=performance.now();world.weapon.throw();}for(const e of events)if(e.type==='hitConfirmed'&&e.owner===battle.player.id)ui.showConfirmed(e);
  if(events.some(e=>e.type==='shot'&&e.player&&e.hit))ui.showHit();
 }
 if(!['preparation','intro'].includes(ui.screen)&&!battle.player.alive&&battle.player.downedUntil<=battle.elapsed&&!battle.finished&&deathAge===null){
  paused=false;shooting=false;shotPending=false;if(document.pointerLockElement)document.exitPointerLock();deathAge=0;ui.beginDeath();
 }
 if(battle.player.downedUntil>battle.elapsed){paused=false;shooting=false;shotPending=false;ui.downed(battle);if(document.pointerLockElement)document.exitPointerLock();if(lan)lan.input({yaw:lookYaw,pitch:lookPitch,forward:0,side:0,fire:false,giveUp:keys.has('Space')||giveUpHeld});else if(keys.has('Space')||giveUpHeld)battle.giveUp(dt);}else if(battle.player.alive&&ui.screen==='downed'){giveUpHeld=false;ui.hideOverlay();requestLock();}
 if(deathAge!==null&&!battle.player.alive&&!battle.finished){deathAge+=dt;if(deathAge>=4.8&&ui.screen!=='respawn')ui.dead(battle);}else if(battle.player.alive){if(deathAge!==null){selectedEquipment=null;equipmentUntil=0;paused=false;ui.hideOverlay();faceObjective();requestLock();}deathAge=null;}
 if(battle.finished&&ui.screen!=='end'){
  paused=true;shooting=false;shotPending=false;if(document.pointerLockElement)document.exitPointerLock();resultStarted=performance.now();ui.end(battle);
 }
 const crouched=battle.sliding||(keys.has('ControlLeft')||keys.has('ControlRight')||crouchToggle);
 const eyeTarget=battle.sliding?.72:crouched?1.07:1.78;eyeSmooth+=(eyeTarget-eyeSmooth)*(1-Math.exp(-dt*12));
 if(battle.player.hp<lastPlayerHP){audio.hurt();ui.hud.classList.remove('hurt');void ui.hud.offsetWidth;ui.hud.classList.add('hurt');setTimeout(()=>ui.hud.classList.remove('hurt'),430)}
 lastPlayerHP=battle.player.hp;
 world.render(battle,dt,{yaw:lookYaw+recoilYaw,pitch:lookPitch+recoilPitch,height:eyeSmooth+jumpOffset+battle.player.altitude,ads:aiming,moving:moving&&!paused,sprint:sprinting,reload:battle.isReloading(),speed:actualSpeed,side:sideLean,vehicleFirstPerson,infantryFirstPerson,introAge:ui.screen==='intro'?3-(lan?lan.countdown:introRemaining):undefined,resultAge:ui.screen==='end'?(performance.now()-resultStarted)/1000:undefined,network:!!lan,equipment:selectedEquipment,equipmentAge:Math.min(1,Math.max(0,(performance.now()-equipmentUsedAt)/700)),deathAge:deathAge??undefined});
 ui.hud.classList.toggle('vehicle-optics',aiming&&battle.inVehicle);ui.hud.classList.toggle('aiming',aiming&&!battle.isReloading()&&!battle.inVehicle);ui.hud.classList.toggle('scoped',aiming&&battle.activeWeapon.zoom>=3&&!battle.isReloading()&&!battle.inVehicle);ui.hud.style.setProperty('--ads-blend',String(world.weapon.aimBlend));ui.hud.classList.toggle('sprinting',sprinting);
 if(battle.playerVehicle&&battle.playerVehicle.warningUntil>battle.elapsed&&performance.now()>nextLockAlarm){audio.warning();nextLockAlarm=performance.now()+650;}
 const aa=ui.hud.querySelector<HTMLElement>('#aa-status')!;aa.classList.toggle('hidden',selectedEquipment!=='aa');if(selectedEquipment==='aa'){const target=battle.getAATarget({x:-Math.sin(lookYaw)*Math.cos(lookPitch),y:Math.sin(lookPitch),z:-Math.cos(lookYaw)*Math.cos(lookPitch)});aa.textContent=!battle.aaCount?'导弹耗尽':battle.aaUntil>battle.elapsed?'装填中 · '+(battle.aaUntil-battle.elapsed).toFixed(1)+'s':target?'◇ 目标捕获 · 左键发射 · '+Math.round(Math.hypot(target.pos.x-battle.player.pos.x,target.pos.z-battle.player.pos.z))+'m':'对准空中敌方载具 · 450m · 右键瞄准';aa.classList.toggle('locked',!!target);}
 const hit=ui.hud.querySelector('#hit-feedback')!;if(performance.now()-(ui as any).confirmedAt>1000)hit.classList.add('hidden');
 ui.heldEquipment=selectedEquipment;ui.drawHUD(battle,time/1000,lookYaw);
 if(mapVisible)ui.toggleMap(true,battle);
 if(debugVisible)ui.setDebug(true,battle,fps,world.renderer.info.render.calls);
}
requestAnimationFrame(frame);

// Development-only QA diagnostics. The production bundle removes this branch.
if(import.meta.env.DEV)Object.defineProperty(window,'__IRONFRONT_QA',{value:{get battle(){return battle},get world(){return world},get audio(){return audio.state},get fps(){return fps},get ui(){return ui},get paused(){return paused}},configurable:true});
