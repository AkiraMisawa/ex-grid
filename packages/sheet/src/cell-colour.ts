// A Cell Colour from its RGB value, and the colours the Sheet's own lists paint.

import { CellColour } from '@ex-grid/sheet-engine';

/** The Cell Colour of an RGB value, `0xRRGGBB`. */
export function colourFromRgb(rgb: number): CellColour {
    return CellColour.fromRgb(rgb);
}

/** Automatic: the Ink, the colour a Font and a Border line take unless one is chosen. */
export function automaticColour(): CellColour {
    return CellColour.automatic;
}

/** A colour as a swatch and a preview paint it: Automatic is the Ink of the text around it. */
export function paintOf(colour: CellColour): string {
    return colour.isAutomatic ? 'currentColor' : colour.toString();
}
