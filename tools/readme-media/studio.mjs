// What every scene needs to look like a person using the page (ADR-0110): a drawn cursor, since
// a recording shows none; a badge naming each key pressed; and moves at a hand's pace. None of it
// changes what the page does: the cursor and the badge ignore the pointer and live outside the
// application's elements.

const CURSOR = `<svg width="22" height="26" viewBox="0 0 22 26" xmlns="http://www.w3.org/2000/svg">
<path d="M3 2 L3 20.5 L7.6 16.3 L10.8 23.6 L14.1 22.2 L11 15 L17.6 15 Z" fill="#fff" stroke="#111" stroke-width="1.5" stroke-linejoin="round"/></svg>`;

/** Puts the cursor and the key badge on the page. Call after every navigation. */
export async function dress(page) {
  await page.evaluate((cursor) => {
    if (document.getElementById('studio-cursor')) return;
    const c = document.createElement('div');
    c.id = 'studio-cursor';
    c.innerHTML = cursor;
    Object.assign(c.style, {
      position: 'fixed', left: '-40px', top: '-40px', zIndex: 2147483647, pointerEvents: 'none',
      transform: 'translate(-3px, -2px)', filter: 'drop-shadow(0 1px 1px rgba(0,0,0,.35))',
    });
    document.documentElement.appendChild(c);
    document.addEventListener('mousemove', e => { c.style.left = e.clientX + 'px'; c.style.top = e.clientY + 'px'; }, true);

    const k = document.createElement('div');
    k.id = 'studio-keys';
    Object.assign(k.style, {
      position: 'fixed', right: '24px', bottom: '24px', zIndex: 2147483647, pointerEvents: 'none',
      font: '600 15px/1 Roboto, Helvetica, Arial, sans-serif', color: '#fff',
      background: 'rgba(20, 24, 31, .86)', padding: '10px 14px', borderRadius: '8px',
      boxShadow: '0 4px 14px rgba(0,0,0,.25)', opacity: '0', transition: 'opacity .18s',
    });
    document.documentElement.appendChild(k);
  }, CURSOR);
}

const pause = (page, ms) => page.waitForTimeout(ms);

/** Moves the cursor to the centre of `target` (a locator, or {x, y}), at a hand's pace. */
export async function moveTo(page, target, { steps = 24, dx = 0, dy = 0 } = {}) {
  const point = typeof target.boundingBox === 'function' ? await centre(target) : target;
  await page.mouse.move(point.x + dx, point.y + dy, { steps });
  return { x: point.x + dx, y: point.y + dy };
}

/** Moves to `target` and clicks it. */
export async function click(page, target, options = {}) {
  const at = await moveTo(page, target, options);
  await pause(page, 120);
  await page.mouse.click(at.x, at.y);
  await pause(page, options.after ?? 450);
}

/** Drags from one target to another, as a selection is drawn. */
export async function drag(page, from, to, { steps = 30, after = 500 } = {}) {
  const a = await moveTo(page, from);
  await pause(page, 150);
  await page.mouse.down();
  const b = typeof to.boundingBox === 'function' ? await centre(to) : to;
  await page.mouse.move(b.x, b.y, { steps });
  await page.mouse.up();
  await pause(page, after);
  return { a, b };
}

/** Presses `keys` (Playwright's names, "Control+C"), showing each in the badge. */
export async function press(page, keys, { after = 380, label } = {}) {
  await badge(page, label ?? keys.replace('Control', 'Ctrl').replace('Arrow', '').replace(/\+/g, ' + '));
  await page.keyboard.press(keys);
  await pause(page, after);
}

/** Types `text` at a typist's pace. */
export async function type(page, text, { delay = 70, after = 300 } = {}) {
  await page.keyboard.type(text, { delay });
  await pause(page, after);
}

/** Shows `text` in the key badge, then fades it. */
export async function badge(page, text, ms = 900) {
  await page.evaluate(([t, d]) => {
    const k = document.getElementById('studio-keys');
    if (!k) return;
    k.textContent = t;
    k.style.opacity = '1';
    clearTimeout(window.__studioKeys);
    window.__studioKeys = setTimeout(() => { k.style.opacity = '0'; }, d);
  }, [text, ms]);
}

export { pause };

async function centre(locator) {
  await locator.scrollIntoViewIfNeeded();
  const box = await locator.boundingBox();
  if (!box) throw new Error(`Nothing to point at: ${locator}`);
  return { x: box.x + box.width / 2, y: box.y + box.height / 2 };
}
