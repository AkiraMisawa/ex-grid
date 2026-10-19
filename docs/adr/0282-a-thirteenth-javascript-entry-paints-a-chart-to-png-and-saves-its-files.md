# A thirteenth JavaScript entry paints a chart to PNG and saves its files

*Decided with the user, 2026-10-19 (M4 grilling Q9 and Q10). This rewrites ADR-0021's list from twelve
entries to thirteen. ADR-0252 settled that files are written on the click, not on every render; how
the PNG is made and how any file reaches the disk were open.* *(2026-10-19: ADR-0240's two entries,
decided on 2026-10-18, joined the list on the same branch as this one, so this entry is the fifteenth
and the list has fifteen.)*

## Why Blazor cannot

- **A PNG is pixels, text included.** C# can write the SVG (ADR-0252), but painting it means laying
  out its text in fonts. Off the browser that is a raster library (SkiaSharp or similar, a dependency
  ADR-0250 refuses) or a rasteriser and font shaping of our own. The browser already has both, and
  paints in the very fonts the screen shows.
- **Blazor cannot start a download.** It can render an `<a download>`, but only the user's click on
  it saves; a `data:` link made on the first click and pressed on a second is two clicks, and a long
  URL for a large CSV.

## The entry: files, with exactly three jobs

1. **Paint an SVG to PNG**: it takes the SVG text C# wrote on the click and a scale, draws it onto a
   canvas off the page, and returns the PNG's bytes. The scale is the Device Pixel's (ADR-0090), so a
   PNG at 150% is not a blurred 100% one; the entry reads it as `devicePixelRatio` on the click. The 3D surface's PNG is the SVG its own module draws
   (ADR-0255's twelfth entry hands over its current drawing), painted the same way.
2. **Save bytes under a file name**: CSV, SVG or PNG, through a `Blob` and an object URL revoked once
   the save has started. One click saves.
3. **Read the chart's Visual Tokens as the screen has them**, from its root's computed style, on the
   click, so a saved SVG or PNG has the theme's colours and font. A file has no page to inherit them
   from; without this it would be drawn in ExChart's default palette while the screen and its legend
   show the theme's, a picture that disagrees with what the reader saw. C# writes the values on the
   file's root. *(Added 2026-10-19, decided with the user in the shipping thread: the files follow the
   theme. It reads style values only, never layout.)*

It runs once per click, never per render; it adds no listener and measures nothing. It reads no
layout: the SVG carries its own size.

**Another product that later saves a file** (ExPivot's or ExSheet's CSV) reuses this entry, and widens
it by rewriting this ADR.

## Considered

- **No PNG in the first version**: SVG and CSV only, no JavaScript added. A PNG is what goes into a
  mail or a slide, and converting an SVG is a step every user would repeat.
- **Painting in C#**: a dependency, or a font stack of our own.
- **Two clicks through a `data:` link**: rejected above.
- **Files in ExChart's default palette**, with no token read: the entry stays at two jobs, and a file
  saved under a theme disagrees with the screen it was saved from. The user chose the theme's colours.
- **Leaving the save to the Consumer** through an event carrying the bytes: a Consumer who does
  nothing gets a menu that does nothing.
