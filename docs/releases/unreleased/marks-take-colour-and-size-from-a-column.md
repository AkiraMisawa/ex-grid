Product: ExChart
Kind: Added
Docs: exchart/spec#colour--size-and-edges-from-columns
ADR: 0320

An Observation's marks take their colour (`ColourBy`, in nine heat bands, or diverging from a centre) and their size (`SizeBy`, area in proportion) from a column, with a key beside the plot and in the saved files. A point with no value is drawn as an outline and counted in the Notes; the tooltip and the CSV carry the values.
