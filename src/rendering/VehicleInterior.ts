import * as THREE from 'three';
import type {ArmoredVehicle,VehicleKind} from '../vehicles/Vehicle';

/** Camera-space, original low-poly interiors. Seats and controls follow the actual occupied vehicle. */
export class VehicleInterior {
 readonly root=new THREE.Group();
 private models=new Map<string,THREE.Group>();
 private materials:THREE.Material[]=[];
 private screens:{canvas:HTMLCanvasElement;texture:THREE.CanvasTexture;kind:VehicleKind;seat:number}[]=[];
 private controls=new Map<string,THREE.Group>();
 private clock=0;private nextDisplay=0;private lastId=-1;private lastYaw=0;private steering=0;
 constructor(camera:THREE.Camera){camera.add(this.root);this.root.name='vehicle-interior';this.root.renderOrder=9;this.root.visible=false;
  for(const kind of ['tank','ifv','aa','transport','scout','helicopter','jet','motorcycle'] as VehicleKind[]){this.build(kind,0);if(kind!=='jet')this.build(kind,2);}
 }
 private build(kind:VehicleKind,seat:number){
  const key=kind+'-'+seat,g=new THREE.Group();g.name='interior-'+key;g.visible=false;this.models.set(key,g);this.root.add(g);
  const mat=(color:number,metalness=.2)=>{const m=new THREE.MeshStandardMaterial({color,roughness:.75,metalness,depthTest:false,depthWrite:false});this.materials.push(m);return m;};
  const dark=mat(0x182125),metal=mat(0x465052,.6),cloth=mat(0x555e45,0),rubber=mat(0x101518,0),trim=mat(0x8b9687),red=mat(0xb24b35);
  const box=(w:number,h:number,d:number,m:THREE.Material,x:number,y:number,z:number,parent:THREE.Object3D=g)=>{const mesh=new THREE.Mesh(new THREE.BoxGeometry(w,h,d),m);mesh.position.set(x,y,z);parent.add(mesh);return mesh;};
  const rod=(a:THREE.Vector3,b:THREE.Vector3,r:number,m:THREE.Material,parent:THREE.Object3D=g)=>{const delta=b.clone().sub(a),mesh=new THREE.Mesh(new THREE.CylinderGeometry(r,r,delta.length(),10),m);mesh.position.copy(a).add(b).multiplyScalar(.5);mesh.quaternion.setFromUnitVectors(new THREE.Vector3(0,1,0),delta.normalize());parent.add(mesh);return mesh;};
  const screen=(x:number,y:number,z:number,w:number,h:number)=>{if(seat===0&&kind!=='motorcycle')y+=.18;box(w+.06,h+.06,.06,rubber,x,y,z+.015);const canvas=document.createElement('canvas');canvas.width=256;canvas.height=128;const texture=new THREE.CanvasTexture(canvas);texture.colorSpace=THREE.SRGBColorSpace;const m=new THREE.MeshBasicMaterial({map:texture,depthTest:false,depthWrite:false});this.materials.push(m);const mesh=new THREE.Mesh(new THREE.PlaneGeometry(w,h),m);mesh.position.set(x,y,z+.052);g.add(mesh);this.screens.push({canvas,texture,kind,seat});};
  const grip=(x:number,y:number,z:number,parent:THREE.Object3D)=>{box(.13,.10,.18,rubber,x,y,z,parent);box(.13,.13,.28,cloth,x,y-.08,z+.19,parent);};
  const steeringWheel=(x:number,y:number,z:number)=>{const group=new THREE.Group();group.position.set(x,y+.24,z);g.add(group);const wheel=new THREE.Mesh(new THREE.TorusGeometry(.23,.025,8,24),rubber);group.add(wheel);box(.12,.085,.045,metal,0,0,0,group);for(const a of [0,2.1,4.2])rod(new THREE.Vector3(0,0,0),new THREE.Vector3(Math.sin(a)*.21,Math.cos(a)*.21,0),.014,metal,group);grip(-.22,0,.01,group);grip(.22,0,.01,group);this.controls.set(key,group);return group;};
  if(seat>0){
   if(kind==='motorcycle'){box(.43,.48,.26,cloth,0,-.58,-.75);box(.23,.2,.22,rubber,0,-.32,-.8);for(const x of [-.3,.3]){grip(x,-.48,-.48,g);rod(new THREE.Vector3(x,-.52,-.48),new THREE.Vector3(x,-.85,-.1),.05,cloth);}return;}
   // Rear bench has its own seatback, grab handles and sidewalls; no fake steering controls.
   box(.7,.8,.15,cloth,-.5,-.45,-1.0);box(.7,.8,.15,cloth,.5,-.45,-1.0);box(2,.1,.65,dark,0,-.9,-.85);
   for(const x of [-1.02,1.02]){box(.12,1.1,1.1,metal,x,-.4,-.75);rod(new THREE.Vector3(x*.85,-.3,-.65),new THREE.Vector3(x*.85,.1,-.65),.025,trim);}
   screen(0,-.58,-.96,.3,.15);return;
  }
  if(kind==='motorcycle'){
   box(.42,.24,.5,cloth,0,-.7,-.5);box(.12,.025,.12,metal,0,-.565,-.5); // Fuel tank and cap.
   const bar=new THREE.Group();bar.position.set(0,-.43,-.85);g.add(bar);this.controls.set(key,bar);
   rod(new THREE.Vector3(-.38,.04,.08),new THREE.Vector3(.38,.04,.08),.026,metal,bar);
   for(const x of [-.4,.4]){rod(new THREE.Vector3(x,.04,.08),new THREE.Vector3(x,.10,.20),.042,rubber,bar);grip(x,.08,.20,bar);rod(new THREE.Vector3(x*.7,0,0),new THREE.Vector3(x*.7,-.35,-.15),.025,metal,bar);rod(new THREE.Vector3(x,.07,.09),new THREE.Vector3(x*.83,.17,-.17),.012,metal,bar);box(.14,.075,.035,metal,x,.20,-.18,bar);}
   screen(0,-.31,-.89,.24,.14);box(.08,.1,.1,red,.32,-.4,-.77);return;
  }
  if(kind==='scout'||kind==='transport'){
   box(2.45,.22,.48,dark,0,-.70,-1.13);box(2.1,.07,.55,metal,0,-.55,-1.25);
   for(const x of [-1.06,1.06])rod(new THREE.Vector3(x,-.55,-1.3),new THREE.Vector3(x,.73,-1.43),kind==='scout'?.038:.065,metal);
   if(kind==='transport'){box(2.18,.13,.12,metal,0,.73,-1.43);box(2.35,.20,.45,dark,0,.90,-1.33);}else {for(const x of [-1.1,1.1])rod(new THREE.Vector3(x,-.45,-1.5),new THREE.Vector3(x,.8,-.5),.025,trim);} // Open roll cage on scout, windshield frame on transport.
   screen(-.18,-.57,-.9,.42,.18);if(kind==='transport')screen(.6,-.62,-.92,.27,.15);steeringWheel(-.18,-.66,-.57);if(kind==='transport'){box(.25,.18,.04,dark,.52,.49,-1.28);box(.12,.025,.08,trim,.52,.48,-1.24);}else box(.24,.045,.24,cloth,.50,-.48,-1.10);
   for(const x of [.38,.55,.72])box(.1,.035,.025,trim,x,-.66,-.86);box(.05,.2,.05,metal,.38,-.8,-.55);return;
  }
  if(kind==='tank'||kind==='ifv'||kind==='aa'){
   // Armored viewing aperture, roof armor and side racks. The center sightline stays unobstructed.
   const aperture=kind==='tank'?.51:kind==='ifv'?.66:.79;box(3,.27,1,dark,0,aperture+.19,-1.1);box(3,.30,.65,metal,0,-.79,-1.1);
   for(const x of [-1.18,1.18])box(.26,1.5,.6,metal,x,0,-1.15);
   box(2.6,.055,.12,trim,0,aperture,-1.16);box(2.6,.055,.12,trim,0,-.58,-1.16);
   if(kind==='tank'){
    for(const y of [-.42,-.63,-.84]){box(.22,.15,.28,cloth,-.96,y,-.67);box(.08,.12,.29,trim,-.84,y,-.67);}screen(.42,-.60,-.84,.48,.22);
    const yoke=steeringWheel(-.25,-.72,-.48);yoke.scale.set(.85,.65,1);box(.1,.32,.09,metal,.76,-.72,-.55);grip(.76,-.55,-.53,g);
   }else if(kind==='ifv'){
    screen(-.5,-.65,-.83,.44,.22);screen(.18,-.65,-.83,.36,.22);box(.08,.22,.08,rubber,.64,-.68,-.54);grip(.64,-.57,-.51,g);for(let i=0;i<5;i++)box(.05,.035,.025,i%2?red:trim,-.3+i*.1,-.79,-.75);
   }else{
    screen(-.5,-.63,-.85,.42,.25);screen(.20,-.63,-.85,.42,.25);for(const x of [-.48,.48]){box(.06,.18,.08,rubber,x,-.79,-.5);grip(x,-.71,-.49,g);}box(.10,.05,.04,red,.7,-.75,-.65);
   }return;
  }
  // Different cockpit silhouettes: single-seat fighter canopy vs broad helicopter windscreen.
  if(kind==='jet'){
   for(let i=0;i<9;i++){const a=i*Math.PI/9,b=(i+1)*Math.PI/9;rod(new THREE.Vector3(Math.cos(a)*.98,Math.sin(a)*.93-.22,-1.3),new THREE.Vector3(Math.cos(b)*.98,Math.sin(b)*.93-.22,-1.3),.035,metal);}
   box(1.2,.38,.4,dark,0,-.79,-1.0);screen(-.32,-.67,-.85,.28,.2);screen(.32,-.67,-.85,.28,.2);screen(0,-.38,-1.18,.27,.12);
   box(.08,.25,.08,rubber,0,-.70,-.52);grip(0,-.61,-.48,g);box(.1,.08,.2,metal,-.6,-.74,-.6);
  }else{
   box(2.25,.32,.5,dark,0,-.78,-1.15);for(const x of [-1.05,1.05])rod(new THREE.Vector3(x,-.6,-1.3),new THREE.Vector3(x*.85,.74,-1.4),.055,metal);rod(new THREE.Vector3(.12,-.6,-1.35),new THREE.Vector3(.12,.75,-1.4),.035,metal);box(2,.08,.08,metal,0,.75,-1.4);
   screen(-.52,-.61,-.94,.38,.25);screen(.46,-.61,-.94,.38,.25);box(.06,.3,.07,rubber,-.20,-.69,-.5);grip(-.2,-.56,-.47,g);rod(new THREE.Vector3(-.62,-.86,-.4),new THREE.Vector3(-.52,-.66,-.5),.025,metal);grip(-.53,-.66,-.47,g);
  }
 }
 render(v:ArmoredVehicle|null,seat:number,firstPerson:boolean,ads:boolean,dt:number){
  this.clock+=dt;this.root.visible=!!v&&firstPerson&&!ads&&seat!==1; // Gunner uses the separate mounted weapon.
  if(v?.kind==='motorcycle')this.root.visible=firstPerson&&!ads;
  if(!v||!this.root.visible){for(const g of this.models.values())g.visible=false;return;}
  const key=v.kind+'-'+(seat===0?0:2);for(const [id,g] of this.models)g.visible=id===key;
  if(this.lastId!==v.id){this.lastId=v.id;this.lastYaw=v.yaw;this.steering=0;}
  const turn=Math.atan2(Math.sin(v.yaw-this.lastYaw),Math.cos(v.yaw-this.lastYaw));this.lastYaw=v.yaw;this.steering=THREE.MathUtils.damp(this.steering,THREE.MathUtils.clamp(turn/Math.max(.001,dt),-1,1),8,dt);
  const control=this.controls.get(key);if(control){control.rotation.z=v.kind==='motorcycle'?0:-this.steering*.55;control.rotation.y=v.kind==='motorcycle'?-this.steering*.24:0;}
  const vibration=Math.min(.009,Math.abs(v.speed)*.0003);this.root.position.y=Math.sin(this.clock*37)*vibration;this.root.rotation.z=v.kind==='motorcycle'?this.steering*.035:['jet','helicopter'].includes(v.kind)?v.roll*.035:0;
  if(this.clock>=this.nextDisplay){this.nextDisplay=this.clock+.15;for(const display of this.screens)if(display.kind===v.kind&&display.seat===(seat===0?0:2)){
   const c=display.canvas.getContext('2d')!;c.fillStyle='#0b191b';c.fillRect(0,0,256,128);c.strokeStyle='#618781';c.strokeRect(3,3,250,122);c.fillStyle='#b1e5c8';c.font='bold 20px monospace';c.fillText(v.kind.toUpperCase()+(seat===0?'':' CREW'),12,27);c.font='26px monospace';c.fillText(Math.round(Math.abs(v.speed)*3.6)+' km/h',12,66);c.font='16px monospace';c.fillStyle=v.hp/v.maxHp<.35?'#ff8c6b':'#97caba';c.fillText('ARMOR '+Math.ceil(v.hp),12,96);if(['jet','helicopter'].includes(v.kind))c.fillText('ALT '+Math.round(v.altitude)+'m',135,96);
   if(v.kind==='aa'){c.strokeStyle='#83ab79';c.beginPath();c.arc(207,46,24,0,Math.PI*2);c.moveTo(207,46);c.lineTo(207+Math.sin(this.clock)*23,46+Math.cos(this.clock)*23);c.stroke();}display.texture.needsUpdate=true;
  }}
 }
 dispose(){this.root.removeFromParent();this.root.traverse(o=>{if(o instanceof THREE.Mesh)o.geometry.dispose();});for(const d of this.screens)d.texture.dispose();for(const m of this.materials)m.dispose();}
}
