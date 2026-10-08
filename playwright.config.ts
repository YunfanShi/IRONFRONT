import {defineConfig} from '@playwright/test';
export default defineConfig({
 testDir:'./tests',timeout:120_000,expect:{timeout:20_000},use:{baseURL:'http://127.0.0.1:4173',headless:true,launchOptions:{executablePath:process.env.IRONFRONT_CHROME_PATH,args:process.env.IRONFRONT_SOFTWARE_WEBGL?['--use-angle=swiftshader','--enable-unsafe-swiftshader','--enable-webgl']:['--enable-webgl']}},
 webServer:{command:'npm run dev -- --host 127.0.0.1 --port 4173',url:'http://127.0.0.1:4173',reuseExistingServer:true,timeout:60_000}
});
