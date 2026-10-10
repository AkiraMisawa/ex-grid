import { defineConfig } from '@playwright/test';
import path from 'node:path';
export default defineConfig({
  testDir: '.', testMatch: 'perf-memory.spec.mjs', workers: 1, fullyParallel: false, retries: 0, reporter: [['list']],
  projects: [{ name: 'chrome', use: { launchOptions: { executablePath: '/opt/pw-browsers/chromium' } } }],
  use: { headless: true, viewport: { width: 1400, height: 1100 }, baseURL: process.env.EXGRID_BASE_URL },
  webServer: { command: `node static-host.mjs ${JSON.stringify(process.env.PERF_WWWROOT)} ${new URL(process.env.EXGRID_BASE_URL).port}`, url: process.env.EXGRID_BASE_URL, reuseExistingServer: false, timeout: 60_000 },
});
