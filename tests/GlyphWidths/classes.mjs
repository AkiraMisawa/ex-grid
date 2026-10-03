// The glyphs CellTextMetrics names in a class (ADR-0016; ticket 83), for measure.mjs to measure and
// tables.mjs to leave out of a table: a glyph of a class is charged its class, never a table entry.
// Kept beside the class rules in src/ExGrid/Columns/CellTextMetrics.cs, which they must match.
export const wide = [...'%€−+#'];
export const narrow = [...'.,()/: '];
export const digit = [...'0123456789$£¥₹₺₫E-\'’  ‎‏؜'];
export const classed = [...wide, ...narrow, ...digit];
