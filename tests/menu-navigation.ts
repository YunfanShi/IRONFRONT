import type {Page} from '@playwright/test';
/** Existing regression cases now reach forms through the visible lobby navigation. */
export async function showMenuControl(page:Page,selector:string){
 if(await page.locator('#modal.menu').count()&&!await page.locator(selector).first().isVisible()){
  if(selector.startsWith('#lan-'))await page.locator('#lan-open').click();
  else await page.locator('.lobby-header [data-view="settings"]').click();
 }
}
