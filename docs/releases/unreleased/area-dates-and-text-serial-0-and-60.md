Product: ExSheet
Kind: Behaviour
Docs: exsheet/functions
ADR: 0047

`EOMONTH` from serial 0 or 60, and `EDATE` from serial 60, answer as Excel does, where they gave `#VALUE!`: the start is read as `MONTH` and `DAY` read it, so `EOMONTH(0,1)` is 59 (28 February 1900) and `EDATE(60,1)` is 89 (29 March 1900). `EDATE` from serial 0, day 0 of a month, stays `#VALUE!` until Excel is asked. A start between -1 and 0 stays `#VALUE!` until Excel is asked: truncated it would be serial 0, cut down to its day it would be -1 and `#NUM!`, and no run has observed which.
