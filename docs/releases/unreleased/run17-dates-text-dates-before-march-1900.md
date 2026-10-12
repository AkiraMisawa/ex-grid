Product: ExSheet
Kind: Behaviour
Docs: exsheet/functions
ADR: 0047

`EDATE`, `EOMONTH`, `NETWORKDAYS` and `WORKDAY` answer for dates before 1 March 1900 as Excel does, where they gave `#VALUE!`: months are counted on the real calendar, so `EOMONTH(31,1)` is 59 (28 February 1900), and each day is the day of the week `WEEKDAY` gives it, so `NETWORKDAYS(30,100)` is 51. `EDATE` and `EOMONTH` from serial 0 or 60 stay `#VALUE!`, and a `WORKDAY` that falls before serial 0 is `#NUM!`.
