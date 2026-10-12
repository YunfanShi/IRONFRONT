import {defineConfig} from '@playwright/test';
export default defineConfig({
 testDir:'./tests',workers:1,timeout:120_000,expect:{timeout:20_000},use:{baseURL:'http://127.0.0.1:4176',headless:true,launchOptions:{executablePath:process.env.IRONFRONT_CHROME_PATH,args:process.env.IRONFRONT_SOFTWARE_WEBGL?['--use-angle=swiftshader','--enable-unsafe-swiftshader','--enable-webgl']:['--enable-webgl']}},
 webServer:[{command:'npx vite build --mode qa --outDir .qa-dist && npx vite preview --outDir .qa-dist --host 127.0.0.1 --port 4176',url:'http://127.0.0.1:4176',reuseExistingServer:false,timeout:120_000},{command:'npm run compile:logic && node server/lan.cjs',env:{PORT:'18788'},url:'http://127.0.0.1:18788/api/status',reuseExistingServer:false,timeout:60_000}]
});
