// A Cell Colour from its RGB value. @ex-grid/sheet-engine exports CellColour as a type only, so the class is
// reached through an instance the engine does hand out: Automatic, the default Font's colour. This module goes
// once the engine exports CellColour as a value.

import { CellFont, type CellColour } from '@ex-grid/sheet-engine';

/** The Cell Colour of an RGB value, `0xRRGGBB`. */
export function colourFromRgb(rgb: number): CellColour {
    // oxlint-disable-next-line typescript/no-unsafe-type-assertion -- the default Font's colour is a CellColour, so its constructor is CellColour's class
    const cellColour = CellFont.default.colour.constructor as typeof CellColour;
    return cellColour.fromRgb(rgb);
}

/** Automatic: the Ink, the colour a Font and a Border line take unless one is chosen. */
export function automaticColour(): CellColour {
    return CellFont.default.colour;
}

/** A colour as a swatch and a preview paint it: Automatic is the Ink of the text around it. */
export function paintOf(colour: CellColour): string {
    return colour.isAutomatic ? 'currentColor' : colour.toString();
}
