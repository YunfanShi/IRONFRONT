import {test,expect} from '@playwright/test';

test('elite profile reaches the live battle and diagnostics',async({page})=>{
 await page.goto('/');
 await page.locator('#setup-size').selectOption('8');
 await page.locator('#setup-ai-profile').selectOption('elite');
 await page.locator('#play').click();
 await page.locator('#ready').click();
 await expect(page.locator('#modal')).toHaveClass('hidden');
 expect(await page.evaluate(()=>(window as any).__IRONFRONT_QA.battle.settings.aiProfile)).toBe('elite');
 await page.keyboard.press('F3');
 await expect(page.locator('#debug')).toContainText('AI elite');
 await page.evaluate(()=>{
  const b=(window as any).__IRONFRONT_QA.battle;
  b.player.pos={x:0,z:0};
  const mate=b.soldiers.find((s:any)=>!s.player&&s.team===b.player.team);
  mate.pos={x:0,z:-7};mate.yaw=Math.PI;mate.nextAiAt=Infinity;mate.boardingUntil=Infinity;
 });
 await page.waitForTimeout(350);
 await page.screenshot({path:'test-results/ai-squad-017.png'});
 await page.keyboard.press('Escape');
 await page.reload();
 await expect(page.locator('#setup-ai-profile')).toHaveValue('elite');
});
