import {buildingPlan} from '../world/BuildingPlan';
import * as THREE from 'three';
import {MapContext} from '../world/Maps';
import type {Block} from '../world/Layout';

/** Original procedural architecture. Closed envelopes retain the authoritative collision footprint. */
export class ModularBuildings {
 private box=new THREE.BoxGeometry(1,1,1);
 private cylinder=new THREE.CylinderGeometry(1,1,1,8);
 private groups:{group:THREE.Group;near:THREE.Group;at:THREE.Vector3}[]=[];
 private materials=new Map<string,THREE.MeshStandardMaterial>();
 private textures=new Map<string,THREE.Texture>();
 private scratch=new THREE.Object3D();
 private gable=(()=>{const shape=new THREE.Shape();shape.moveTo(-.5,0);shape.lineTo(.5,0);shape.lineTo(0,1);shape.closePath();const geo=new THREE.ExtrudeGeometry(shape,{depth:1,bevelEnabled:false});geo.translate(0,0,-.5);return geo;})();
 constructor(private scene:THREE.Scene,private map:MapContext,private shadows:boolean){
  for(const b of map.definition.blocks)if(['house','factory','barracks'].includes(b.kind))this.build(b);
  for(const b of map.definition.openBuildings)this.buildInterior(b);
  this.consolidate();
 }
 private material(name:string,color:number,metalness=0){
  const existing=this.materials.get(name);if(existing)return existing;
  const m=new THREE.MeshStandardMaterial({color,roughness:metalness?.65:.9,metalness});m.name=name;
  const asset=name.startsWith('brick')?'brick_wall_001':name.startsWith('plaster')?'plastered_wall':name.startsWith('concrete')?'concrete_wall_006':null;
  if(asset){
   const load=(channel:string)=>{const key=asset+channel;let texture=this.textures.get(key);if(!texture){texture=new THREE.TextureLoader().load(import.meta.env.BASE_URL+'textures/architecture/'+asset+'-'+channel+'.jpg');texture.wrapS=texture.wrapT=THREE.RepeatWrapping;texture.anisotropy=4;if(channel==='color')texture.colorSpace=THREE.SRGBColorSpace;this.textures.set(key,texture);}return texture;};
   m.map=load('color');m.normalMap=load('normal');m.normalScale.set(.32,.32);m.roughnessMap=m.aoMap=load('arm');m.aoMapIntensity=.65;m.roughness=1;
   m.onBeforeCompile=shader=>{
    shader.vertexShader=shader.vertexShader.replace('#include <common>','#include <common>\nvarying vec3 architecturalPosition; varying vec3 architecturalNormal;').replace('#include <begin_vertex>',`#include <begin_vertex>
vec4 aw=vec4(position,1.0);vec3 an=normal;
#ifdef USE_INSTANCING
aw=instanceMatrix*aw;an=mat3(instanceMatrix)*an;
#endif
architecturalPosition=(modelMatrix*aw).xyz;architecturalNormal=normalize(mat3(modelMatrix)*an);`);
    shader.fragmentShader=shader.fragmentShader.replace('#include <common>','#include <common>\nvarying vec3 architecturalPosition; varying vec3 architecturalNormal;');
    const uv=asset==='brick_wall_001'?1/3:.5;
    shader.fragmentShader=shader.fragmentShader.replace('#include <map_fragment>',`vec2 architecturalUV=(abs(architecturalNormal.y)>.5?architecturalPosition.xz:abs(architecturalNormal.x)>.5?architecturalPosition.zy:architecturalPosition.xy)*${uv};
`+THREE.ShaderChunk.map_fragment.replaceAll('vMapUv','architecturalUV'));
    for(const [chunk,varying] of [['roughnessmap_fragment','vRoughnessMapUv'],['normal_fragment_maps','vNormalMapUv'],['aomap_fragment','vAoMapUv']] as const)shader.fragmentShader=shader.fragmentShader.replace('#include <'+chunk+'>',THREE.ShaderChunk[chunk].replaceAll(varying,'architecturalUV'));
   };m.customProgramCacheKey=()=> 'architecture-pbr-world-v2-'+asset;
  }
  this.materials.set(name,m);return m;
 }
 private build(b:Block){
  const group=new THREE.Group(),near=new THREE.Group();group.add(near);group.position.set(b.x,this.map.heightAt(b.x,b.z),b.z);this.scene.add(group);this.groups.push({group,near,at:group.position});
  const p=buildingPlan(b),home=p.style==='residential',industrial=['factory','warehouse'].includes(p.style),office=p.style==='office';
  const plaster=this.material('plaster'+p.variant,[0xe1d8c7,0xcac7bd,0xd0c4ad,0xc9d0cf][p.variant]!);
  const brick=this.material('brick',0xe7d4c0),concrete=this.material('concrete',0xd9d8d0),steel=this.material('steel',0x53605f,.55),trim=this.material('trim',0x343d3d,.2),wood=this.material('wood',0x665a45),roof=this.material('roof',0x484d4b,.3);
  const wall=home?plaster:industrial?brick:concrete,glass=this.material('glass',0x40535c);glass.roughness=.21;
  const batches=new Map<string,{g:THREE.BufferGeometry;m:THREE.Material;items:THREE.Matrix4[];detail:boolean}>();
  const add=(key:string,m:THREE.Material,x:number,y:number,z:number,w:number,h:number,d:number,detail=false,rotation=0,geometry:THREE.BufferGeometry=this.box)=>{
   if(w<=0||h<=0||d<=0)return;let batch=batches.get(key);if(!batch){batch={g:geometry,m,items:[],detail};batches.set(key,batch);}this.scratch.position.set(x,y,z);this.scratch.scale.set(w,h,d);this.scratch.rotation.set(0,0,rotation);this.scratch.updateMatrix();batch.items.push(this.scratch.matrix.clone());
  };
  // A full ground footprint, with a setback upper storey or a raised industrial service block.
  for(const v of p.volumes){add('masonry',wall,0,v.h/2,0,v.w-1.6,v.h,v.d-1.6);add('roof-slab',roof,0,v.h+.12,0,v.w,.24,v.d);}
  add('plinth',concrete,0,.22,0,b.w,.44,b.d);
  const levels=industrial?1:p.floors;
  for(let floor=0;floor<levels;floor++){
   const upper=p.setback&&floor===p.floors-1,width=upper?p.upperWidth:b.w,depth=upper?p.upperDepth:b.d;
   const cy=industrial?p.shoulderHeight-2.1:floor*p.floorHeight+1.85,windowH=industrial?1.35:office?2.1:1.65;
   // Residential spacing and window proportions differ from curtain-wall offices and high-bay factories.
   for(const side of [-1,1])for(const face of ['front','side'] as const){
    const span=face==='front'?width:depth,bays=Math.max(2,Math.floor(span/(home?4.7:industrial?6:office?3.5:4.5))),windowW=home?1.5:industrial?2.9:office?2.5:1.8;
    for(let j=0;j<bays;j++){
     const position=(j+.5)*span/bays-span/2,pitch=span/bays,base=industrial?0:floor*p.floorHeight,levelHeight=industrial?p.shoulderHeight:p.floorHeight;
     const facade=(key:string,along:number,y:number,wide:number,tall:number)=>{
      const emit=(x:number,cy:number,w:number,h:number)=>add(key,wall,face==='front'?x:side*(width/2-.1),cy,face==='front'?side*(depth/2-.1):x,face==='front'?w:.2,h,face==='front'?.2:w);
      const doorHalf=industrial?2.2:.85,left=along-wide/2,right=along+wide/2,bottom=y-tall/2,top=y+tall/2;
      if(floor===0&&face==='front'&&side===1&&bottom<2.8&&left<doorHalf&&right>-doorHalf){
       if(left<-doorHalf)emit((left-doorHalf)/2,y,-doorHalf-left,tall);
       if(right>doorHalf)emit((right+doorHalf)/2,y,right-doorHalf,tall);
       if(top>2.8){const a=Math.max(left,-doorHalf),c=Math.min(right,doorHalf);emit((a+c)/2,(top+2.8)/2,c-a,top-2.8);}
      }else emit(along,y,wide,tall);
     };
     if(floor===0&&face==='front'&&side===1&&Math.abs(position)<(industrial?2.2:.85)+windowW/2){facade('entry-wall',position,base+levelHeight/2,pitch,levelHeight);continue;}
     facade('wall-piers',position-pitch/2+(pitch-windowW)/4,base+levelHeight/2,(pitch-windowW)/2,levelHeight);
     facade('wall-piers',position+pitch/2-(pitch-windowW)/4,base+levelHeight/2,(pitch-windowW)/2,levelHeight);
     const bottom=cy-windowH/2,top=cy+windowH/2;facade('wall-spandrel',position,(base+bottom)/2,windowW,bottom-base);facade('wall-spandrel',position,(top+base+levelHeight)/2,windowW,base+levelHeight-top);

     const z=face==='front'?side*(depth/2-.45):position,x=face==='front'?position:side*(width/2-.45),horizontal=face==='front';
     add('window-shadow',trim,x,cy,z,horizontal?windowW+.18:.16,windowH+.18,horizontal?.16:windowW+.18);
     add('glass',glass,horizontal?x:x+side*.05,cy,horizontal?z+side*.05:z,horizontal?windowW:.06,windowH,horizontal?.06:windowW);
     for(const offset of [-1,1])add('window-horizontal',steel,x,cy+offset*(windowH/2+.05),z,horizontal?windowW+.24:.2,.1,horizontal?.2:windowW+.24,true);
     add('sill',concrete,x,cy-windowH/2-.15,z,horizontal?windowW+.35:.24,.15,horizontal?.24:windowW+.35,true);
     if(office||industrial)add('mullion',steel,x,cy,z,horizontal?.08:.2,windowH,horizontal?.2:.08,true);
     if(home&&floor>0&&face==='front'&&j===p.variant%bays){
      add('balcony-floor',concrete,x,floor*p.floorHeight+.15,side*(depth/2-.85),2.8,.2,1.6,true);
      add('balcony-rail',steel,x,floor*p.floorHeight+1.22,side*(depth/2-.13),2.8,.07,.07,true);
      for(let k=0;k<5;k++)add('balcony-upright',steel,x-1.35+k*.675,floor*p.floorHeight+.74,side*(depth/2-.13),.045,.96,.045,true);
     }
    }
   }
   if(!industrial)for(const side of [-1,1])add('floor-edge',concrete,0,(floor+1)*p.floorHeight-.1,side*(depth/2-.14),width,.18,.26);
  }
  // Door surround and integrated entrance canopy, rather than a giant pasted dark rectangle.
  add('door',industrial?steel:wood,0,1.3,b.d/2-.1,industrial?4.4:1.7,2.6,.13);
  for(const side of [-1,1])add('door-jamb',concrete,side*(industrial?2.3:.98),1.4,b.d/2-.12,.18,2.8,.22,true);
  add('entry-lintel',concrete,0,2.85,b.d/2-.1,industrial?4.8:2.25,.25,.3);
  add('entry-canopy',roof,0,3.05,b.d/2+.45,industrial?5.2:3.6,.18,1.4,true);
  if(p.pitchedRoof){
   const angle=Math.atan2(2.1,b.w/2),length=Math.hypot(b.w/2,2.1);
   for(const side of [-1,1]){add('roof-slope',roof,side*b.w/4,b.h+1.15,0,length,.18,b.d,false,-side*angle);add('gable',wall,0,b.h,side*(b.d/2-.15),b.w,2.1,.2,false,0,this.gable);}
   add('roof-ridge',steel,0,b.h+2.15,0,.23,.18,b.d,true);
  }else for(const side of [-1,1])add('parapet',concrete,0,b.h+.48,side*(p.upperDepth/2-.15),p.upperWidth,.65,.3);
  if(p.shoulderHeight<b.h){for(const side of [-1,1]){add('terrace-rail',steel,side*(b.w/2-.3),p.shoulderHeight+.9,0,.06,.06,b.d,true);for(let j=0;j<6;j++)add('terrace-upright',steel,side*(b.w/2-.3),p.shoulderHeight+.5,(j/5-.5)*(b.d-.6),.05,.85,.05,true);}}
  // Roof services and rear utilities follow structural edges and stay on existing solid footprints.
  add('ventilation',steel,p.upperWidth*.2,b.h+.6,0,industrial?3.5:1.8,.9,1.6,true);
  if(industrial)add('exhaust',steel,-p.upperWidth*.24,b.h+1.4,-p.upperDepth*.23,.4,2.4,.4,true,0,this.cylinder);
  if(home)add('tank',steel,-p.upperWidth*.22,b.h+1.15,-p.upperDepth*.21,1.1,1.6,1.1,true,0,this.cylinder);
  add('antenna',steel,p.upperWidth*.3,b.h+1.8,-p.upperDepth*.3,.045,3.2,.045,true,0,this.cylinder);
  add('drainpipe',steel,b.w/2-.18,p.shoulderHeight/2,-b.d/2+.12,.075,p.shoulderHeight,.075,true,0,this.cylinder);
  add('air-conditioner',steel,-b.w*.3,2.7,-b.d/2+.1,1.1,.55,.25,true);
  for(let i=0;i<6;i++)add('ac-grille',trim,-b.w*.3-.46+i*.185,2.7,-b.d/2+.04,.035,.43,.05,true);
  for(const [key,batch] of batches){const mesh=new THREE.InstancedMesh(batch.g,batch.m,batch.items.length);batch.items.forEach((matrix,i)=>mesh.setMatrixAt(i,matrix));mesh.computeBoundingSphere();mesh.receiveShadow=true;mesh.castShadow=this.shadows&&['masonry','wall-piers','wall-spandrel','roof-slab','roof-slope','gable'].includes(key);(batch.detail?near:group).add(mesh);}
 }
 private buildInterior(b:{x:number;z:number;w:number;d:number;h:number}){
  const group=new THREE.Group(),near=new THREE.Group(),steel=this.material('steel',0x697576,.65),concrete=this.material('concrete0',0xaaa59a);group.position.set(b.x,this.map.heightAt(b.x,b.z),b.z);group.add(near);this.scene.add(group);this.groups.push({group,near,at:group.position});
  const components:{material:THREE.Material;matrix:THREE.Matrix4}[]=[];
  const part=(material:THREE.Material,x:number,y:number,z:number,w:number,h:number,d:number,angle=0)=>{this.scratch.position.set(x,y,z);this.scratch.rotation.set(0,0,angle);this.scratch.scale.set(w,h,d);this.scratch.updateMatrix();components.push({material,matrix:this.scratch.matrix.clone()});};
  // Trusses, girders and service lighting are overhead; the actual wall/room layout comes from MapContext.
  for(let z=-b.d*.35;z<=b.d*.35;z+=5){part(steel,0,b.h-.55,z,b.w,.2,.25);for(const side of [-1,1])part(steel,side*b.w*.24,b.h-1.5,z,b.w*.5,.12,.15,side*.13);}
  for(const side of [-1,1]){part(concrete,side*(b.w/2-.15),b.h/2,b.d*.45,.3,b.h,.3);part(steel,side*(b.w/2-.4),2,-b.d*.3,.15,3.8,.15);}
  part(steel,0,b.h-.3,b.d/2,b.w,.4,.35);
  const light=new THREE.MeshStandardMaterial({color:0xe3d9b4,emissive:0x9a926d,emissiveIntensity:.6,roughness:.8});this.materials.set('interior-light-'+b.x,light);part(light,0,b.h-.8,0,2.3,.1,.3);
  for(const material of new Set(components.map(c=>c.material))){const list=components.filter(c=>c.material===material),mesh=new THREE.InstancedMesh(this.box,material,list.length);list.forEach((c,i)=>mesh.setMatrixAt(i,c.matrix));mesh.computeBoundingSphere();group.add(mesh);}
 }
 private consolidate(){
  const clusters=new Map<string,{at:THREE.Vector3;group:THREE.Group;near:THREE.Group;batches:Map<string,{g:THREE.BufferGeometry;m:THREE.Material;detail:boolean;matrices:THREE.Matrix4[]}>}>();
  for(const building of this.groups){const sx=Math.floor(building.at.x/96),sz=Math.floor(building.at.z/96),key=sx+':'+sz;let c=clusters.get(key);if(!c){c={at:new THREE.Vector3(sx*96+48,0,sz*96+48),group:new THREE.Group(),near:new THREE.Group(),batches:new Map()};c.group.add(c.near);clusters.set(key,c);this.scene.add(c.group);}
   building.group.updateMatrixWorld(true);building.group.traverse(object=>{if(!(object instanceof THREE.InstancedMesh))return;const detail=object.parent===building.near,m=object.material as THREE.Material,k=m.uuid+object.geometry.uuid+detail;let batch=c!.batches.get(k);if(!batch){batch={g:object.geometry,m,detail,matrices:[]};c!.batches.set(k,batch);}const matrix=new THREE.Matrix4();for(let i=0;i<object.count;i++){object.getMatrixAt(i,matrix);batch.matrices.push(new THREE.Matrix4().multiplyMatrices(object.matrixWorld,matrix));}});this.scene.remove(building.group);
  }
  this.groups=[];for(const c of clusters.values()){for(const b of c.batches.values()){const mesh=new THREE.InstancedMesh(b.g,b.m,b.matrices.length);b.matrices.forEach((m,i)=>mesh.setMatrixAt(i,m));mesh.computeBoundingSphere();mesh.receiveShadow=true;mesh.castShadow=this.shadows&&!b.detail&&(/^(plaster|brick|concrete|roof)/.test(b.m.name));(b.detail?c.near:c.group).add(mesh);}this.groups.push({group:c.group,near:c.near,at:c.at});}
 }
 update(camera:THREE.Camera){for(const b of this.groups){const d=b.at.distanceTo(camera.position);b.near.visible=d<180;b.group.visible=d<700;}}
}
