# Analytics — stands for the risk system

The exposure profile, the CDS bootstrap and the curves' hazard rates are computed here, outside
the chart and outside the grid, as a risk or analytics system would compute them and a Consumer
would fetch them. The pages put what comes back in the grid's rows; the chart reads those rows
like any other column. Nothing under `Charting/` or `Components/ChartBox.razor` refers to this
folder, and neither may: the grid neither holds nor executes (the spine's third rule), and a
chart package that bootstrapped a curve would be a pricing library with a picture on it.

What the chart knows of a hazard rate is only what the page declares about its column: that it
reads on the right, in "% a year", drawn as a step over (tᵢ₋₁, tᵢ] — the convention the analytics
used, said by the Consumer, not assumed by the chart.
