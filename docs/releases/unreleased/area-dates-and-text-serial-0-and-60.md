Product: ExSheet
Kind: Behaviour
Docs: exsheet/functions
ADR: 0047

`EOMONTH` and `EDATE` from serial 0 or 60 answer as Excel does, where they gave `#VALUE!`: the start is read as `MONTH` and `DAY` read it, so `EOMONTH(0,1)` is 59 (28 February 1900), `EDATE(60,1)` is 89 (29 March 1900), and `EDATE(0,1)` is 31, serial 0's day 0 being the day before the 1st of the month reached. `EDATE` from serial 0 to January 1900, March 1900 or January 10000 stays `#VALUE!` until Excel is asked, because the readings that give Excel's other answers part there. A start between -1 and 0 is cut down to -1 and is `#NUM!`, as in Excel, where it gave `#VALUE!`.
