// The glyphs CellTextMetrics names in a class (ADR-0016; ticket 83), for measure.mjs to measure and
// tables.mjs to leave out of a table: a glyph of a class is charged its class, never a table entry.
// Kept beside the class rules in dotnet/src/ExGrid/Columns/CellTextMetrics.cs, which they must match.
export const wide = [...'%€−+#'];
export const narrow = [...'.,()/: '];
export const digit = [...'0123456789$£¥₹₺₫E-\'’  ‎‏؜'];
export const classed = [...wide, ...narrow, ...digit];

// East Asian Width Wide or Fullwidth, charged the em rather than a class: CellTextMetrics' IsFullWidth, block for
// block, which packages/core/test/default-glyph-widths.test.ts holds this to.
export const isFullWidth = (c) => (c >= 0x1100 && c <= 0x115F) || (c >= 0x2E80 && c <= 0x303E)
  || (c >= 0x3041 && c <= 0x33FF) || (c >= 0x3400 && c <= 0x4DBF) || (c >= 0x4E00 && c <= 0x9FFF)
  || (c >= 0xA000 && c <= 0xA4CF) || (c >= 0xA960 && c <= 0xA97F) || (c >= 0xAC00 && c <= 0xD7A3)
  || (c >= 0xF900 && c <= 0xFAFF) || (c >= 0xFE10 && c <= 0xFE19) || (c >= 0xFE30 && c <= 0xFE6F)
  || (c >= 0xFF01 && c <= 0xFF60) || (c >= 0xFFE0 && c <= 0xFFE6) || (c >= 0x1F300 && c <= 0x1F64F)
  || (c >= 0x1F900 && c <= 0x1F9FF) || (c >= 0x20000 && c <= 0x3FFFD);
