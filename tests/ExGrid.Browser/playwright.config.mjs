import { defineConfig } from '@playwright/test';
import fs from 'node:fs';
import { BASE_URL, HOST_LOG, HOST_PORT, LATENCY_CONTROL_URL, SERVER } from './hosting.mjs';

// The host is started on whatever port BASE_URL names, so two checkouts — one per
// parallel agent — do not share a port: with reuseExistingServer a second runner on
// the same port would be driving the OTHER checkout's DemoHost and passing (AGENTS.md,
// "Working in parallel"). Which host, and every port derived from it, is hosting.mjs's.
const HOST_URL = `http://localhost:${HOST_PORT}`;

// A fresh log per run: CON-6 reads what each test appended to it. The config is loaded
// again in every worker, and a worker is replaced after each failing test, so only the
// runner itself clears it — a worker doing so would erase the lines it is meant to read.
if (SERVER && process.env.TEST_WORKER_INDEX === undefined) {
    fs.rmSync(HOST_LOG, { force: true });
}

// Started here so `npx playwright test` is the whole command on any machine. An
// already-running host is reused, which is what makes an edit-and-rerun loop quick.
//
// Into the run's own output, which §22 Step 4 tees into layer3.log. On WebAssembly the
// log the renderer writes is the browser console, which fixtures.mjs reads, and this is
// the dev server's side. On Server the circuit's log is this process's, and the host
// also appends it to HOST_LOG so the fixture can read it per test (CON-6). A reused
// host was started elsewhere and its output is wherever that was.
const webServer = SERVER
    ? [
        {
            command: `dotnet run --project ../../samples/ExGrid.DemoHost.Server --urls ${HOST_URL}`,
            url: `${HOST_URL}/wide`,
            env: { EXGRID_HOST_LOG: HOST_LOG },
            reuseExistingServer: true,
            timeout: 180_000,
            stdout: 'pipe',
            stderr: 'pipe',
        },
        {
            // The browser reaches the Server host through this, at 0 ms until a test
            // sets a round trip (ED-22, SRV-5, SRV-6).
            command: `node latency-proxy.mjs ${new URL(BASE_URL).port} ${HOST_PORT} ${new URL(LATENCY_CONTROL_URL).port}`,
            url: `${BASE_URL}/wide`,
            reuseExistingServer: true,
            timeout: 180_000,
        },
    ]
    : {
        command: `dotnet run --project ../../samples/ExGrid.DemoHost --urls ${HOST_URL}`,
        url: `${BASE_URL}/wide`,
        reuseExistingServer: true,
        timeout: 180_000,
        stdout: 'pipe',
        stderr: 'pipe',
    };

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
    webServer,
});
