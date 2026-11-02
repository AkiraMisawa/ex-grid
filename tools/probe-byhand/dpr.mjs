import { chromium } from 'playwright-core';
for (const channel of ['chrome', 'msedge']) {
    const b = await chromium.launch({ channel, headless: false });
    const p = await (await b.newContext({ viewport: null })).newPage();
    await p.setContent('<div style="height:3000px;width:3000px"></div>');
    console.log(channel, JSON.stringify(await p.evaluate(() => ({ dpr: devicePixelRatio, inner: [innerWidth, innerHeight], screen: [screen.width, screen.height], scrollbar: innerWidth - document.documentElement.clientWidth }))));
    await b.close();
}
