// Where things are in an ExGrid, for the scenes: a cell by its column's header and its row on
// screen, read from the painted header and rows rather than from any internal state.

/** The centre of the cell under the header `header`, in the `row`-th painted row (0-based). */
export async function cellAt(page, header, row, scope = page) {
  const head = scope.locator('.ex-header [role="columnheader"]').filter({ hasText: new RegExp(`^${escape(header)}`) }).first();
  const h = await head.boundingBox();
  const r = await scope.locator('.ex-row:not(.ex-placeholder)').nth(row).boundingBox();
  if (!h || !r) throw new Error(`No cell under "${header}" in row ${row}`);
  return { x: h.x + h.width / 2, y: r.y + r.height / 2 };
}

/** Waits until a grid has painted rows. */
export async function rowsPainted(page) {
  await page.locator('.ex-row:not(.ex-placeholder)').first().waitFor();
}

function escape(text) {
  return text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}
