// Untracked: the bundled Chromium standing in for Chrome in a container without it.
import base from './playwright.config.mjs';
export default {
    ...base,
    projects: [{ name: 'chromium-local', use: { launchOptions: { executablePath: '/opt/pw-browsers/chromium-1194/chrome-linux/chrome' } } }],
};
