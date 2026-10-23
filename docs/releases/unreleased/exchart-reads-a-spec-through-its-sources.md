Product: ExChart
Kind: Added
ADR: 0322, 0344

`ExChart` reads a Chart Spec through Chart Sources itself: given `Spec` and `Sources` in place of a `Definition`, it asks for the device pixels of its width, and again whenever the range changes, keeping what it shows, Busy, until the newest ask is answered. An overtaken answer is dropped, and one from older data is said in the chart's place. `ExChart.Definition` is now nullable, so code that reads it back sees a nullable reference.
