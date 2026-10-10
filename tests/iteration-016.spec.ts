import {showMenuControl} from './menu-navigation';
import {test} from './fixtures';
import {expect} from '@playwright/test';
test('all eight vehicle driver interiors are distinct; controls, crew seats and exterior views follow actual seats',async({page})=>{
 const errors:string[]=[];page.on('pageerror',e=>errors.push(e.message));await page.goto('/');await showMenuControl(page,'#setup-size');await page.locator('#setup-size').selectOption('8');await page.locator('#play').click();await page.locator('#ready').click();await expect(page.locator('#modal')).toHaveClass('hidden');
 await page.evaluate(()=>{const b=(window as any).__IRONFRONT_QA.battle;for(const s of b.soldiers)if(!s.player){s.nextAiAt=Infinity;s.nextEquipment=Infinity;s.boardingUntil=Infinity;s.pos={x:300,z:-300};}for(const v of b.vehicles){v.nextDecision=Infinity;v.pos={x:250,z:250};v.occupants.fill(null);}});
 const counts:number[]=[];
 for(const kind of ['motorcycle','tank','ifv','aa','transport','scout','helicopter','jet']){
  await page.evaluate(kind=>{const q=(window as any).__IRONFRONT_QA,b=q.battle;if(b.inVehicle)b.togglePlayerVehicle();for(const old of b.vehicles)old.pos={x:250,z:250};const v=b.vehicles.find((v:any)=>v.kind===kind&&v.team==='blue');v.pos={x:100,z:80};v.altitude=0;v.occupants.fill(null);b.player.pos={...v.pos};b.player.altitude=0;b.player.parachuting=false;if(!b.togglePlayerVehicle())throw new Error('Cannot board '+kind);},kind);
  await expect.poll(()=>page.evaluate(()=>{const q=(window as any).__IRONFRONT_QA;return q.world.interior.root.children.filter((g:any)=>g.visible).map((g:any)=>g.name).join(',');})).toBe('interior-'+kind+'-0');
  counts.push(await page.evaluate(()=>{const q=(window as any).__IRONFRONT_QA;return q.world.interior.root.children.find((g:any)=>g.visible).children.length;}));
  await page.waitForTimeout(200);await page.screenshot({path:'test-results/interior-'+kind+'-016.png'});
  if(kind==='motorcycle'){const before=await page.evaluate(()=>(window as any).__IRONFRONT_QA.world.interior.controls.get('motorcycle-0').rotation.y);await page.keyboard.down('W');await page.keyboard.down('D');await page.waitForTimeout(400);await page.keyboard.up('D');await page.keyboard.up('W');expect(await page.evaluate(()=>(window as any).__IRONFRONT_QA.battle.playerVehicle.speed)).toBeGreaterThan(0);expect(await page.evaluate(()=>(window as any).__IRONFRONT_QA.world.interior.controls.get('motorcycle-0').rotation.y)).not.toBe(before);}
  await page.keyboard.press('V');await expect.poll(()=>page.evaluate(()=>(window as any).__IRONFRONT_QA.world.interior.root.visible)).toBe(false);await page.screenshot({path:'test-results/exterior-'+kind+'-016.png'});await page.keyboard.press('V');
  if(['tank','transport'].includes(kind)){await page.keyboard.press('F3');await expect.poll(()=>page.evaluate(()=>{const q=(window as any).__IRONFRONT_QA;return q.world.interior.root.children.find((g:any)=>g.visible)?.name;})).toBe('interior-'+kind+'-2');await page.screenshot({path:'test-results/crew-'+kind+'-016.png'});await page.keyboard.press('F2');await expect.poll(()=>page.evaluate(()=>(window as any).__IRONFRONT_QA.world.interior.root.visible)).toBe(false);await expect.poll(()=>page.evaluate(()=>(window as any).__IRONFRONT_QA.world.equipment.root.visible)).toBe(true);await page.keyboard.press('F1');}
 }
 expect(new Set(counts).size).toBeGreaterThan(5);await page.keyboard.press('E');await expect.poll(()=>page.evaluate(()=>(window as any).__IRONFRONT_QA.world.interior.root.visible)).toBe(false);expect(errors).toEqual([]);
});
