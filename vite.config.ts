import {defineConfig} from 'vitest/config';
export default defineConfig({
 test:{testTimeout:20_000,include:['src/tests/**/*.test.ts']},
 build:{rollupOptions:{output:{manualChunks:{three:['three']}}}}
});
