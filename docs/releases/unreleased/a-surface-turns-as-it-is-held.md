Product: ExChart
Kind: Behaviour
ADR: 0274

A 3D surface is seen from above and turns the way it is dragged. It was projected as seen from below its floor, so the picture read as a mirror image and every drag looked reversed. A drag now holds the surface where it was pressed, as Plotly's turntable does: a drag the box's height turns it a whole turn, the pitch runs from -90 to 90 so it may be seen from below, the arrow keys turn it as a drag the same way would (ArrowDown now tips its top toward the viewer, where it did the opposite), the right button pans, and the wheel zooms by how far it moved. A `SurfaceView` kept from before this change is now drawn from above at the same angles, so it no longer gives the same picture.
