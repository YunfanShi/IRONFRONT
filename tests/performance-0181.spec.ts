import {test,expect} from '@playwright/test';
import {writeFileSync} from 'node:fs';

test('soldier instances omit inactive equipment and recover after seat, team and equipment changes',async({page})=>{
 await page.goto('/');await page.locator('#setup-size').selectOption('32');await page.locator('#play').click();await page.locator('#ready').click();await expect(page.locator('#modal')).toHaveClass('hidden');
 const sample=await page.evaluate(()=>{const q=(window as any).__IRONFRONT_QA,b=q.battle,r=q.world.soldierRender;for(const s of b.soldiers){s.alive=true;s.vehicleId=null;s.equipmentUntil=0;s.nextAiAt=Infinity;s.nextEquipment=Infinity;s.boardingUntil=Infinity;s.downedUntil=0;}
  for(let i=0;i<30;i++)r.update(b,1/60,false);const times=[];for(let pass=0;pass<5;pass++){const start=performance.now();for(let i=0;i<120;i++)r.update(b,1/60,false);times.push(performance.now()-start);}
  const armies=r.armies.map((a:any)=>({team:a.team,meshes:a.meshes.length,activeMeshes:a.meshes.filter((m:any)=>m.visible&&m.count>0).length,instances:a.meshes.reduce((n:number,m:any)=>n+(m.visible?m.count:0),0),gadgetInstances:a.meshes.reduce((n:number,m:any,i:number)=>n+(a.parts[i].kind==='gadget'&&m.visible?m.count:0),0)}));return {times,armies};});
 writeFileSync('test-results/soldier-performance-'+(process.env.IRONFRONT_PERF_BASELINE?'before':'after')+'.json',JSON.stringify(sample,null,2));console.log('SOLDIER_PERFORMANCE',JSON.stringify(sample));
 if(process.env.IRONFRONT_PERF_BASELINE)return;
 expect(sample.armies.every((a:any)=>a.gadgetInstances===0)).toBe(true);expect(sample.armies.every((a:any)=>a.activeMeshes<a.meshes)).toBe(true);
 const changed=await page.evaluate(()=>{const q=(window as any).__IRONFRONT_QA,b=q.battle,r=q.world.soldierRender,m=b.soldiers.find((s:any)=>!s.player&&s.team==='blue');m.equipmentKind='repair';m.equipmentUntil=b.elapsed+10;r.update(b,1/60,false);const active=r.armies.flatMap((a:any)=>a.meshes.map((mesh:any,i:number)=>({kind:a.parts[i].kind,type:a.parts[i].gadgetType,count:mesh.count,visible:mesh.visible}))).filter((p:any)=>p.kind==='gadget'&&p.type==='repair'&&p.visible);m.equipmentUntil=0;m.team='red';r.update(b,1/60,true);return {active:active.reduce((n:number,p:any)=>n+p.count,0),redMembers:r.armies.find((a:any)=>a.team==='red').soldiers.length,redExpected:b.soldiers.filter((s:any)=>s.team==='red').length,gadgets:r.armies.flatMap((a:any)=>a.meshes.filter((mesh:any,i:number)=>a.parts[i].kind==='gadget'&&mesh.visible)).length};});
 expect(changed.active).toBeGreaterThan(0);expect(changed.redMembers).toBe(changed.redExpected);expect(changed.gadgets).toBe(0);
 await page.screenshot({path:'test-results/performance-0181.png'});
});
