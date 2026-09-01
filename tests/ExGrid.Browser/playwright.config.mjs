import { defineConfig } from '@playwright/test';

const BASE_URL = process.env.EXGRID_BASE_URL ?? 'http://localhost:5299';

export default defineConfig({
    testDir: '.',
    // These tests read layout and set the device scale factor on the whole page, so
    // they are about the browser's own state rather than about the server's. Running
    // them side by side would have them changing each other's zoom.
    workers: 1,
    fullyParallel: false,
    reporter: [['list']],
    // A failing layout assertion is a real answer about this platform, not a flake.
    // Retrying would turn "the scrollbar covers the Focus here" into an intermittent
    // report, which is the opposite of what this suite is for.
    retries: 0,
    // Both target browsers, because ADR-0017 says both are verified: "Passing on one
    // does not satisfy the requirement." Each channel resolves a browser that is
    // already on the machine rather than one downloaded into the repository — what
    // this suite asks about is what the real browser on the real OS does, so a
    // bundled build would be answering for the wrong one. A machine without Edge
    // fails that project by name, which is the honest outcome (ADR-0026).
    projects: [
        { name: 'chrome', use: { channel: 'chrome' } },
        { name: 'msedge', use: { channel: 'msedge' } },
    ],
    use: {
        // Headed by default, which is not a preference. Measured on macOS 15 / Chrome:
        // headless keeps overlay scrollbars on the horizontal axis whatever CSS asks
        // for, so the row band's 15px could not be reproduced there at all and the
        // scrollbar tests would pass without testing anything. Headed reports 15/15.
        //
        // On Windows and Linux the native scrollbars already occupy layout and nothing
        // has to be forced, so EXGRID_HEADLESS=1 is available for a machine without a
        // display. Do not make it the default: it would quietly disarm the suite on the
        // one platform where the bug is invisible to begin with.
        headless: process.env.EXGRID_HEADLESS === '1',
        baseURL: BASE_URL,
        trace: 'retain-on-failure',
    },
    // Started here so `npx playwright test` is the whole command on any machine. An
    // already-running host is reused, which is what makes an edit-and-rerun loop quick.
    webServer: {
        command: 'dotnet run --project ../../samples/ExGrid.DemoHost --urls http://localhost:5299',
        url: `${BASE_URL}/wide`,
        reuseExistingServer: true,
        timeout: 180_000,
        stdout: 'ignore',
        stderr: 'pipe',
    },
});
