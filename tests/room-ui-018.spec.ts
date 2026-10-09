import {test,expect} from '@playwright/test';

test('LAN entry keeps room fields while navigating settings and shows a clear connection state',async({page})=>{
 await page.setViewportSize({width:1440,height:1000});await page.goto('/');
 await page.locator('#lan-open').click();await expect(page.locator('#lan-panel')).toBeVisible();
 await page.locator('#lan-address').fill('127.0.0.1:18788');await page.locator('#lan-code').fill('ab12cd');
 await expect(page.locator('#lan-code')).toHaveValue('AB12CD');await page.locator('#lan-ai').uncheck();await page.locator('#lan-team').selectOption('red');
 await page.locator('[data-tab="graphics"]').click();await expect(page.locator('#lan-address')).toHaveValue('127.0.0.1:18788');await expect(page.locator('#lan-code')).toHaveValue('AB12CD');await expect(page.locator('#lan-ai')).not.toBeChecked();await expect(page.locator('#lan-team')).toHaveValue('red');
 await page.locator('#lan-probe').click();await expect(page.locator('#lan-status')).toContainText('服务可达');
 await page.locator('#lan-code').evaluate(input=>{const data=new DataTransfer();data.setData('text','http://127.0.0.1:18788/?room=FED123');input.dispatchEvent(new ClipboardEvent('paste',{clipboardData:data,bubbles:true,cancelable:true}));});await expect(page.locator('#lan-code')).toHaveValue('FED123');await expect(page.locator('#lan-address')).toHaveValue('127.0.0.1:18788');
 await page.locator('#lan-open').click();await expect(page.locator('#lan-panel')).toBeInViewport();
 await page.locator('#lan-panel').screenshot({path:'test-results/room-menu-018.png'});
 await page.setViewportSize({width:390,height:844});await page.locator('#lan-open').click();
 expect(await page.evaluate(()=>{const panel=document.querySelector('.menu-content')!;return panel.scrollWidth<=panel.clientWidth+1})).toBe(true);
 expect(await page.locator('.lan-mode-card').first().evaluate(el=>getComputedStyle(el.parentElement!).gridTemplateColumns.split(' ').length)).toBe(1);
 await page.screenshot({path:'test-results/room-mobile-018.png'});
});

test('host lobby shows readiness and automatically restores the same player after a dropped socket',async({page})=>{
 await page.setViewportSize({width:1440,height:1000});await page.goto('/');await page.locator('#setup-size').selectOption('8');await page.locator('#lan-address').fill('127.0.0.1:18788');await page.locator('#lan-ai').uncheck();await page.locator('#lan-host').click();
 await expect(page.locator('.room-lobby')).toBeVisible();await expect(page.locator('#room-occupancy')).toHaveText('1 / 8');await expect(page.locator('.roster-card')).toHaveCount(1);await expect(page.locator('#begin-match')).toBeDisabled();
 const code=await page.locator('#connection-code').innerText();await expect(page.locator('.share-address').first()).toContainText(`room=${code}`);
 await page.locator('.copy-button[data-copy]').first().click();await expect(page.locator('.copy-feedback')).toContainText('已复制');
 await page.locator('#ready').click();await expect(page.locator('.roster-card.is-ready')).toHaveCount(1);await expect(page.locator('#begin-match')).toBeEnabled();await page.locator('#ready').click();await expect(page.locator('#begin-match')).toBeDisabled();await page.locator('#ready').click();await expect(page.locator('#begin-match')).toBeEnabled();await page.locator('.room-lobby').screenshot({path:'test-results/room-lobby-018.png'});
 await page.locator('#begin-match').click();await expect(page.locator('#modal')).toHaveClass('hidden');
 const playerId=await page.evaluate(()=>(window as any).__IRONFRONT_QA.lan.id);
 await page.evaluate(()=>{const lan=(window as any).__IRONFRONT_QA.lan;lan.__connectionStates=[];const original=lan.onConnectionState;lan.onConnectionState=(state:string)=>{lan.__connectionStates.push(state);original(state)};lan.socket.close(3001,'test drop');});
 await expect.poll(()=>page.evaluate(()=>(window as any).__IRONFRONT_QA.lan.__connectionStates.join(','))).toContain('reconnecting,connected');
 expect(await page.evaluate(()=>(window as any).__IRONFRONT_QA.lan.id)).toBe(playerId);
 await expect(page.locator('#lan-connection')).toContainText('已连接');
});

test('a guest becomes room host when the original host leaves the lobby',async({browser})=>{
 const context=await browser.newContext(),host=await context.newPage(),guest=await context.newPage();try{
  await host.goto('/');await host.locator('#setup-size').selectOption('8');await host.locator('#lan-address').fill('127.0.0.1:18788');await host.locator('#lan-host').click();await expect(host.locator('.room-lobby')).toBeVisible();const code=await host.locator('#connection-code').innerText();
  await guest.goto('/');await guest.locator('#lan-address').fill('127.0.0.1:18788');await guest.locator('#lan-code').fill(code);await guest.locator('#lan-join').click();await expect(guest.locator('.roster-card')).toHaveCount(2);await expect(guest.locator('#begin-match')).toBeHidden();
  await host.locator('#prepare-back').click();await expect(guest.locator('#begin-match')).toBeVisible();await expect(guest.locator('.roster-card')).toHaveCount(1);await guest.locator('#ready').click();await expect(guest.locator('#begin-match')).toBeEnabled();
 }finally{await context.close();}
});
