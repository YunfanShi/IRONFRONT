import {test,expect} from '@playwright/test';

test('menu exposes the required setup surfaces',async({page})=>{
 const errors:string[]=[];page.on('pageerror',e=>{errors.push(e.message);console.log('PAGE_ERROR',e.message)});await page.goto('/');
 await expect(page.locator('#play')).toBeVisible();await expect(page.getByRole('button',{name:/战局|BATTLE/})).toBeVisible();
 await page.getByRole('button',{name:/画面|GRAPHICS/}).click();await expect(page.locator('#setup-quality')).toBeVisible();await expect(page.locator('#setup-fov')).toBeVisible();
 await page.getByRole('button',{name:/音频|AUDIO/}).click();await expect(page.locator('#setup-volume')).toBeVisible();
 await page.getByRole('button',{name:/操作|CONTROLS/}).click();await expect(page.locator('kbd').filter({hasText:'1 / 2'})).toBeVisible();expect(errors).toEqual([]);
});

test('starts a battle, shows HUD, pause and diagnostics',async({page})=>{
 const errors:string[]=[];page.on('pageerror',e=>{errors.push(e.message);console.log('PAGE_ERROR',e.message)});await page.goto('/');
 await page.getByRole('button',{name:/战局|BATTLE/}).click();await page.locator('#setup-size').selectOption('8');await page.locator('#play').click();
 await expect(page.locator('#battle-canvas')).toBeVisible();await expect(page.locator('#hud')).not.toHaveClass(/hidden/);await expect(page.locator('#player-stats')).toContainText(/HP/);
 await page.keyboard.press('F3');await expect(page.locator('#debug')).not.toHaveClass(/hidden/);await page.keyboard.press('Escape');await expect(page.locator('#resume')).toBeVisible();expect(errors).toEqual([]);
});

test('loadout persists, real RP support works, ADS fires and menu cleanup survives restart',async({page})=>{
 test.setTimeout(120_000);const errors:string[]=[];page.on('pageerror',e=>{errors.push(e.message);console.log('PAGE_ERROR',e.message)});await page.setViewportSize({width:1440,height:1000});await page.goto('/');
 await page.locator('#loadout-primary').selectOption('sniper');await page.locator('#loadout-secondary').selectOption('pistol');await page.reload();await expect(page.locator('#loadout-primary')).toHaveValue('sniper');
 await page.locator('#setup-size').selectOption('32');await page.locator('#play').click();await expect(page.locator('#hud')).not.toHaveClass(/hidden/);await expect(page.locator('#player-stats')).toContainText('S12');
 await page.keyboard.press('Q');await expect(page.locator('[data-support]')).toHaveCount(7);await expect(page.locator('[data-support="tank"]')).toBeDisabled();await page.keyboard.press('Q');
 await page.evaluate(()=>{const q=(window as any).__IRONFRONT_QA; q.battle.awardRP(1500,'qa');});await page.keyboard.press('Q');await page.locator('#support-target').selectOption('C');await page.locator('[data-support="smoke"]').click();await page.locator('[data-support="tank"]').click();
 expect(await page.evaluate(()=>{const b=(window as any).__IRONFRONT_QA.battle;return {rp:b.requisitionPoints,smoke:b.supports.some((s:any)=>s.kind==='smoke'),vehicles:b.vehicles.length}})).toEqual({rp:950,smoke:true,vehicles:7});
 await page.screenshot({path:'test-results/support-menu.png'});await page.locator('#support-close').click();await page.waitForTimeout(1500);
 await page.mouse.down({button:'right'});await expect.poll(()=>page.evaluate(()=>(window as any).__IRONFRONT_QA.world.weapon.aimBlend)).toBeGreaterThan(.9);await expect(page.locator('#hud')).toHaveClass(/scoped/);await page.screenshot({path:'test-results/sniper-ads.png'});
 const before=await page.evaluate(()=>(window as any).__IRONFRONT_QA.battle.playerAmmo);await page.mouse.down({button:'left'});await page.mouse.up({button:'left'});await expect.poll(()=>page.evaluate(()=>(window as any).__IRONFRONT_QA.battle.playerAmmo)).toBeLessThan(before);await page.mouse.up({button:'right'});
 await page.keyboard.press('M');await expect(page.locator('#tactical')).not.toHaveClass(/hidden/);await page.keyboard.press('Escape');await page.keyboard.press('Escape');await page.locator('#quit').click();await expect(page.locator('#tactical')).toHaveClass(/hidden/);await expect(page.locator('#scoreboard')).toHaveClass(/hidden/);
 await page.locator('#play').click();await expect(page.locator('#hud')).not.toHaveClass(/hidden/);await page.waitForTimeout(1200);await expect(page.locator('#killfeed')).toBeEmpty();await page.screenshot({path:'test-results/battle-32v32.png'});
 // Headless frame pacing is a short QA sample, not a sustained interactive benchmark.
 const samples:number[]=[];for(let i=0;i<8;i++){await page.waitForTimeout(250);samples.push(await page.evaluate(()=>(window as any).__IRONFRONT_QA.fps));}
 const info=await page.evaluate(()=>{const w=(window as any).__IRONFRONT_QA.world;return {renderer:w.renderer.getContext().getParameter(w.renderer.getContext().RENDERER),calls:w.renderer.info.render.calls,ratio:w.renderer.getPixelRatio()}});console.log('HEADLESS_32V32_FPS',JSON.stringify({samples,info}));expect(errors).toEqual([]);
});

test('pointer-lock denial leaves drag look and weapon controls playable',async({page})=>{
 test.setTimeout(90_000);await page.addInitScript(()=>{HTMLCanvasElement.prototype.requestPointerLock=function(){return Promise.reject(new Error('QA denied'))}});await page.goto('/');await page.locator('#setup-size').selectOption('8');await page.locator('#play').click();await page.waitForTimeout(500);
 const before=await page.evaluate(()=>(window as any).__IRONFRONT_QA.world.camera.rotation.y);await page.mouse.move(700,450);await page.mouse.down({button:'right'});await page.mouse.move(750,450,{steps:5});await page.mouse.up({button:'right'});await page.waitForTimeout(200);expect(await page.evaluate(()=>(window as any).__IRONFRONT_QA.world.camera.rotation.y)).not.toBe(before);await page.keyboard.press('Digit2');await expect(page.locator('#player-stats')).toContainText('M89');
});
