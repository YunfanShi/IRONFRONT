import {test,qaContext} from './fixtures';
import {expect} from '@playwright/test';
test('lobby has navigable operations, persistent class equipment and stopped preview after deployment',async({page})=>{
 await page.setViewportSize({width:1440,height:900});await page.goto('/');await expect(page.locator('.lobby-hero')).toBeVisible();await expect(page.locator('.lobby-settings')).toBeHidden();await expect(page.locator('#lan-panel')).toBeHidden();await page.screenshot({path:'docs/screenshots/lobby-019.png'});
 await page.locator('.lobby-header [data-view="operations"]').click();await expect(page.locator('[data-map]')).toHaveCount(2);await page.locator('[data-map="dust-horizon"]').click();await expect(page.locator('.map-card.selected')).toContainText('沙尘地平线');await page.screenshot({path:'docs/screenshots/operations-019.png'});
 await page.locator('.lobby-header [data-view="loadout"]').click();await expect(page.locator('.class-card')).toHaveCount(4);await page.locator('[data-class="engineer"]').click();await page.screenshot({path:'docs/screenshots/loadout-019.png'});
 await page.evaluate(()=>(window as any).__lobbyBeforeEntry=(window as any).__IRONFRONT_QA.ui.preview);await page.locator('#play').click();expect(await page.evaluate(()=>(window as any).__lobbyBeforeEntry.stopped)).toBe(true);expect(await page.evaluate(()=>(window as any).__lobbyBeforeEntry.frame)).toBe(0);await expect(page.locator('.preparation-shell')).toContainText('沙尘地平线');expect(await page.evaluate(()=>(window as any).__IRONFRONT_QA.battle.settings.mapId)).toBe('dust-horizon');
 const preview=await page.evaluate(()=>{const p=(window as any).__IRONFRONT_QA.ui.preview;(window as any).__previewForTest=p;return p.frame;});expect(preview).toBeGreaterThan(0);await page.locator('#ready').click();await expect(page.locator('#modal')).toHaveClass('hidden');expect(await page.evaluate(()=>(window as any).__previewForTest.stopped)).toBe(true);expect(await page.evaluate(()=>(window as any).__previewForTest.frame)).toBe(0);
 await page.reload();await expect(page.locator('.lobby-operation')).toContainText('沙尘地平线');
});
test('host-selected dust map reaches guests and survives reconnect',async({browser})=>{
 const context=await qaContext(browser),host=await context.newPage(),guest=await context.newPage();try{
 await host.goto('/');await host.locator('.lobby-header [data-view="operations"]').click();await host.locator('[data-map="dust-horizon"]').click();await host.locator('#lan-open').click();await host.locator('#lan-address').fill('127.0.0.1:18788');await host.locator('#lan-host').click();await expect(host.locator('.room-lobby')).toBeVisible();const code=await host.locator('#connection-code').innerText();
 await guest.addInitScript(()=>{const v=JSON.parse(localStorage.getItem('ironfront-options')||'{}');v.mapId='industrial-frontier';localStorage.setItem('ironfront-options',JSON.stringify(v));});await guest.goto('/');await guest.locator('#lan-open').click();await guest.locator('#lan-address').fill('127.0.0.1:18788');await guest.locator('#lan-code').fill(code);await guest.locator('#lan-join').click();await expect(guest.locator('.roster-card')).toHaveCount(2);expect(await guest.evaluate(()=>(window as any).__IRONFRONT_QA.battle.map.size)).toBe(950);await expect(guest.locator('.preparation-shell')).toContainText('沙尘地平线');await expect(guest.locator('.room-rules')).toContainText('32v32 · 500 票');
 await guest.evaluate(()=>(window as any).__IRONFRONT_QA.lan.socket.close(3001,'QA'));await expect.poll(()=>guest.evaluate(()=>(window as any).__IRONFRONT_QA.lan.connected)).toBe(true);expect(await guest.evaluate(()=>(window as any).__IRONFRONT_QA.battle.settings.mapId)).toBe('dust-horizon');
 }catch(error){console.log('DUST_NETWORK_FAILURE',await guest.locator('#modal').innerText(),await host.locator('#modal').innerText());throw error;}finally{await context.close();}
});
test('small-screen lobby keeps forms in viewport and rejects unknown host maps',async({page,request})=>{
 await page.setViewportSize({width:390,height:844});await page.goto('/');await page.locator('#lan-open').click();expect(await page.locator('.menu-content').evaluate(e=>e.scrollWidth<=e.clientWidth+1)).toBe(true);await page.screenshot({path:'docs/screenshots/lobby-mobile-019.png'});
 const response=await request.post('http://127.0.0.1:18788/api/rooms',{data:{mapId:'unsupported'}});expect(response.status()).toBe(400);
});

test('lobby navigation remains usable when WebGL is unavailable',async({page})=>{
 await page.addInitScript(()=>{const original=HTMLCanvasElement.prototype.getContext;HTMLCanvasElement.prototype.getContext=function(kind:any,...args:any[]):any{if(String(kind).startsWith('webgl'))return null;return (original as any).call(this,kind,...args);};});
 await page.goto('/');await expect(page.locator('#play')).toBeEnabled();await page.locator('.lobby-header [data-view="operations"]').click();await expect(page.locator('[data-map]')).toHaveCount(2);await page.locator('.lobby-header [data-view="loadout"]').click();await expect(page.locator('.class-card')).toHaveCount(4);
});
