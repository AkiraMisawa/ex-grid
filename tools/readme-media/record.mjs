// Records the README's GIFs from the Docs Site's Showcases (ADR-0110).
//
//   node record.mjs --base http://localhost:5310/ [--only blotter,sales] [--chrome builtin,mud] [--write]
//
// The site must already be running (see README.md). Each scene in scenes/ opens its Showcase,
// waits for it to be ready, and plays its gestures; the recording is cut from the moment the
// scene starts and turned into a GIF with ffmpeg. --write puts the GIFs into docs/readme/,
// which the README reads; without it they stay in out/.

import { chromium } from 'playwright';
import { execFileSync } from 'node:child_process';
import { mkdirSync, readdirSync, rmSync, copyFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { dress } from './studio.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const args = Object.fromEntries(process.argv.slice(2).reduce((pairs, arg, i, all) => {
  if (arg.startsWith('--')) pairs.push([arg.slice(2), all[i + 1]?.startsWith('--') || all[i + 1] === undefined ? true : all[i + 1]]);
  return pairs;
}, []));

const base = String(args.base ?? 'http://localhost:5310/').replace(/\/?$/, '/');
const only = args.only ? String(args.only).split(',') : null;
const chromes = String(args.chrome ?? 'builtin,mud').split(',');
const width = 1280, height = 720;
const out = join(here, 'out');
mkdirSync(out, { recursive: true });

const scenes = [];
for (const file of readdirSync(join(here, 'scenes')).filter(f => f.endsWith('.mjs')).sort()) {
  const scene = (await import(join(here, 'scenes', file))).default;
  if (!only || only.includes(scene.name)) scenes.push(scene);
}
if (scenes.length === 0) throw new Error('No scene matches --only.');

const browser = await chromium.launch({
  // A container's bundled Chromium when there is one; otherwise Playwright's own.
  executablePath: process.env.CHROMIUM_PATH || undefined,
});

for (const scene of scenes) {
  for (const chrome of chromes) {
    const name = `${scene.name}-${chrome}`;
    const videoDir = join(out, `video-${name}`);
    rmSync(videoDir, { recursive: true, force: true });
    const context = await browser.newContext({
      viewport: { width, height },
      recordVideo: { dir: videoDir, size: { width, height } },
      colorScheme: 'light',
    });
    const page = await context.newPage();
    const opened = Date.now();
    const problems = [];
    page.on('console', m => { if (m.type() === 'error') problems.push(m.text()); });
    page.on('pageerror', e => problems.push(e.message));

    await page.goto(`${base}${scene.path}${chrome === 'mud' ? '?chrome=mud' : ''}`);
    await scene.ready(page);
    await dress(page);
    await page.mouse.move(width * 0.62, height * 0.55);
    await page.waitForTimeout(400);
    const start = (Date.now() - opened) / 1000;
    await scene.run(page);
    const end = (Date.now() - opened) / 1000;
    await context.close();

    if (problems.length > 0)
      throw new Error(`${name}: the page reported problems, so its recording is not used:\n${problems.join('\n')}`);

    const video = join(videoDir, readdirSync(videoDir)[0]);
    const gif = join(out, `${name}.gif`);
    execFileSync('ffmpeg', ['-v', 'error', '-y', '-ss', String(start), '-i', video, '-t', String(end - start),
      '-vf', 'fps=12,scale=960:-1:flags=lanczos,split[a][b];[a]palettegen=max_colors=128:stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=5:diff_mode=rectangle',
      gif]);
    rmSync(videoDir, { recursive: true, force: true });
    console.log(`${name}: ${(end - start).toFixed(1)} s -> ${gif}`);

    if (args.write) {
      const target = join(here, '..', '..', 'docs', 'readme', `${name}.gif`);
      mkdirSync(dirname(target), { recursive: true });
      copyFileSync(gif, target);
      console.log(`  written to ${target}`);
    }
  }
}

await browser.close();
