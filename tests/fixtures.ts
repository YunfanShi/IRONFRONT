import {test as base} from '@playwright/test';
export const test=base.extend<{softwareGraphics:void}>({softwareGraphics:[async({page},use)=>{
 if(process.env.IRONFRONT_SOFTWARE_WEBGL)await page.addInitScript(()=>{try{const v=JSON.parse(localStorage.getItem('ironfront-options')||'{}');if(!v.quality)v.quality='low';if(!v.renderScale)v.renderScale=.5;localStorage.setItem('ironfront-options',JSON.stringify(v));}catch{}});
 await use();
},{auto:true}]});
// Manual multi-client contexts use the same software-rendering budget as page fixtures.
export async function qaContext(browser:import('@playwright/test').Browser,options?:Parameters<import('@playwright/test').Browser['newContext']>[0]){
 const context=await browser.newContext(options);if(process.env.IRONFRONT_SOFTWARE_WEBGL)await context.addInitScript(()=>{try{const v=JSON.parse(localStorage.getItem('ironfront-options')||'{}');if(!v.quality)v.quality='low';if(!v.renderScale)v.renderScale=.5;localStorage.setItem('ironfront-options',JSON.stringify(v));}catch{}});return context;
}
