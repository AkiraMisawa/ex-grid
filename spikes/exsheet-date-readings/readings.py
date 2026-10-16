"""The readings of EDATE and EOMONTH, and of a serial between -1 and 0, against every observed case.

Each reading is a whole evaluation of EDATE and EOMONTH. The script counts the observed cases each
reproduces, and for the readings that reproduce all of them, prints the formulas where they part.
Standard library only: python3 spikes/exsheet-date-readings/readings.py
"""
import calendar
import datetime
import itertools
import math

NUM, VALUE = "#NUM!", "#VALUE!"
MAXIMUM = 2958465  # 31 December 9999
BASE = datetime.date(1899, 12, 31)


def serial_of(year, month, day):
    """Excel's serial of a real calendar day from 1 January 1900 (29 February 1900 is 60), or None."""
    if (year, month, day) == (1900, 2, 29):
        return 60
    if not 1900 <= year <= 9999:
        return None
    try:
        days = (datetime.date(year, month, day) - BASE).days
    except ValueError:
        return None
    return days + 1 if days >= 60 else days


def first_by_date_arithmetic(year, month):
    """DATE(year,month,1) on Excel's calendar, which counts 29 February 1900."""
    days = (datetime.date(year, month, 1) - BASE).days
    return days + 1 if days >= 60 else days


def calendar_day(serial):
    """YEAR, MONTH and DAY of a whole serial as observed: serial 0 is 0 January 1900, 60 is 29 February."""
    if serial == 0:
        return (1900, 1, 0)
    if serial == 60:
        return (1900, 2, 29)
    date = BASE + datetime.timedelta(days=serial - 1 if serial > 60 else serial)
    return (date.year, date.month, date.day)


def month_length(year, month, lengths):
    if lengths == "Excel's calendar" and (year, month) == (1900, 2):
        return 29
    return calendar.monthrange(year, month)[1]


# ---- the observed cases --------------------------------------------------------------------------

D = serial_of
MONTHS_AWAY = [  # (case, function, start, months, Excel's answer)
    ("EDATE-001", "EDATE", D(2011, 1, 15), 1, 40589), ("EDATE-002", "EDATE", D(2011, 1, 15), -1, 40527),
    ("EDATE-003", "EDATE", D(2011, 1, 15), 2, 40617), ("EDATE-004", "EDATE", D(2011, 1, 31), 1, 40602),
    ("EDATE-005", "EDATE", D(2024, 1, 31), 1, 45351), ("EDATE-006", "EDATE", D(2011, 1, 15), -1.9, 40527),
    ("EDATE-007", "EDATE", -1, 1, NUM), ("EDATE-008", "EDATE", D(1900, 3, 15), -1, 46),
    ("EDATE-010", "EDATE", 60, 1, 89), ("EDATE-011", "EDATE", 31, 1, 59), ("EDATE-012", "EDATE", 15, -1, NUM),
    ("EDATE-013", "EDATE", 60, 0, 59), ("EDATE-014", "EDATE", 60, 12, 425), ("EDATE-015", "EDATE", 60, -1, 29),
    ("EDATE-016", "EDATE", 0, 1, 31), ("EDATE-017", "EDATE", -0.5, 1, NUM),
    # Windows run 18a (2026-10-14) answered the three where the six readings that fit the cases above part,
    # and two on which they agree.
    ("EDATE-018", "EDATE", 0, 2, 60), ("EDATE-019", "EDATE", 0, 0, 0), ("EDATE-020", "EDATE", 0, 97200, NUM),
    ("EDATE-021", "EDATE", 0, 3, 91), ("EDATE-022", "EDATE", 0.5, 12, 366),
    ("EOMONTH-001", "EOMONTH", D(2011, 1, 1), 1, 40602), ("EOMONTH-002", "EOMONTH", D(2011, 1, 1), -3, 40482),
    ("EOMONTH-003", "EOMONTH", D(2011, 1, 1), 1.9, 40602), ("EOMONTH-004", "EOMONTH", -1, 0, NUM),
    ("EOMONTH-005", "EOMONTH", D(9999, 12, 1), 1, NUM), ("EOMONTH-006", "EOMONTH", D(2024, 1, 31), 1, 45351),
    ("EOMONTH-007", "EOMONTH", 45000.75, 0, 45016), ("EOMONTH-008", "EOMONTH", 31, 1, 59),
    ("EOMONTH-010", "EOMONTH", 0, 1, 59), ("EOMONTH-011", "EOMONTH", D(1900, 3, 1), -1, 59),
    ("EOMONTH-012", "EOMONTH", 60, 0, 59), ("EOMONTH-013", "EOMONTH", 60, 1, 91),
    ("EOMONTH-014", "EOMONTH", 0, 0, 31), ("EOMONTH-015", "EOMONTH", 0, -1, NUM),
    ("EOMONTH-016", "EOMONTH", -0.5, 0, NUM), ("EOMONTH-017", "EOMONTH", -0.5, 1, NUM),
]
# EDATE-009, EDATE-023, EOMONTH-009 and EOMONTH-018 (a boolean typed, #VALUE!) are no question of reading and
# are left out.

DATE_PARTS = [  # (case, the serial, the answer if the serial is whole and 0 or more, Excel's answer)
    ("YEAR-005", -1, None, NUM), ("YEAR-008", -0.5, 1900, NUM), ("YEAR-003", 0, 1900, 1900),
    ("MONTH-005", -0.5, 1, NUM), ("MONTH-004", 0, 1, 1), ("DAY-005", -0.5, 0, NUM), ("DAY-004", 0, 0, 0),
    ("WEEKDAY-008", -0.5, 7, NUM), ("WEEKDAY-006", 0, 7, 7), ("DAYS-005", -0.5, 1, NUM), ("DAYS-004", -1, None, NUM),
    ("NETWORKDAYS-009", -0.5, 5, NUM), ("WORKDAY-010", -0.5, 2, NUM), ("EOMONTH-004", -1, None, NUM),
    ("EDATE-007", -1, None, NUM), ("EOMONTH-007", 45000.75, 45016, 45016),
]

# ---- the readings --------------------------------------------------------------------------------

STARTS = {  # how a start or a serial is made whole
    "truncated": math.trunc,
    "cut down to its day (floor)": math.floor,
    "below 0 is #NUM!, then truncated": lambda s: -1 if s < 0 else math.trunc(s),
    "refused between -1 and 0": lambda s: VALUE if -1 < s < 0 else math.trunc(s),
}
SERIAL_0 = [  # what serial 0 is under EDATE and EOMONTH
    "0 January 1900 under both",
    "31 December 1899 under both",
    "31 December 1899 under EDATE, 0 January 1900 under EOMONTH",
]
DAY_0 = [  # EDATE from a start whose day is 0: what day 0 of the month it reaches becomes
    "refused",
    "the day before the 1st, by DATE's arithmetic (29 February 1900 counted)",
    "the month before's last day on the real calendar",
    "day 1",
    "no such day, #NUM!",
]
BEFORE_1900 = ["#NUM!", "31 December 1899 is serial 0"]
LENGTHS = ["the real calendar", "Excel's calendar"]  # February 1900 has 28 days, or 29


def months_away(function, start, months, reading):
    whole, serial_0, day_0, before_1900, lengths = reading
    s = STARTS[whole](start)
    if s == VALUE:
        return VALUE
    if s < 0 or s > MAXIMUM:
        return NUM
    months = math.trunc(months)
    if s != 0 or serial_0 == "0 January 1900 under both" or (function == "EOMONTH" and serial_0.endswith("under EOMONTH")):
        year, month, day = calendar_day(s)
    else:
        year, month, day = (1899, 12, 31)
    total = year * 12 + (month - 1) + months
    to_year, to_month = total // 12, total % 12 + 1

    def last_day(y, m):
        if (y, m) == (1899, 12):
            return 0 if before_1900 == "31 December 1899 is serial 0" else NUM
        if not 1900 <= y <= 9999:
            return NUM
        return serial_of(y, m, month_length(y, m, lengths))

    if function == "EOMONTH":
        return last_day(to_year, to_month)
    if day == 0:
        if day_0 == "refused":
            return VALUE
        if day_0 == "no such day, #NUM!":
            return NUM
        if day_0.startswith("the day before"):
            return first_by_date_arithmetic(to_year, to_month) - 1 if 1900 <= to_year <= 9999 else NUM
        if day_0.startswith("the month before"):
            return last_day(*((to_year, to_month - 1) if to_month > 1 else (to_year - 1, 12)))
        day = 1
    if (to_year, to_month) == (1899, 12) and day == 31:
        return last_day(1899, 12)
    if not 1900 <= to_year <= 9999:
        return NUM
    return serial_of(to_year, to_month, min(day, month_length(to_year, to_month, lengths)))


def readings():
    for reading in itertools.product(STARTS, SERIAL_0, DAY_0, BEFORE_1900, LENGTHS):
        # Day 0 arises only where EDATE reads serial 0 as 0 January 1900; elsewhere one row stands for all.
        if reading[1] != "0 January 1900 under both" and reading[2] != "refused":
            continue
        yield reading


def describe(reading):
    whole, serial_0, day_0, before_1900, lengths = reading
    day_0 = "day 0 does not arise" if serial_0 != "0 January 1900 under both" else f"EDATE's day 0: {day_0}"
    return f"start {whole}; serial 0 is {serial_0}; {day_0}; 31 December 1899: {before_1900}; months on {lengths}"


def main():
    print(f"EDATE and EOMONTH: {len(MONTHS_AWAY)} observed cases\n")
    fits = []
    for reading in readings():
        answers = [months_away(f, s, m, reading) for _, f, s, m, _ in MONTHS_AWAY]
        misses = [case for answer, (case, *_, excel) in zip(answers, MONTHS_AWAY) if answer != excel]
        print(f"{len(MONTHS_AWAY) - len(misses):2}/{len(MONTHS_AWAY)}  {describe(reading)}"
              + (f"\n        misses {', '.join(misses)}" if misses else ""))
        if not misses:
            fits.append(reading)

    print(f"\n{len(fits)} readings reproduce every observed case. Their answers:")
    probes = [("EDATE", 0, m) for m in (-1, 0, 1, 2, 3, 12, 97199, 97200, 97201)]
    probes += [("EDATE", 0.5, 2), ("EDATE", 0.9, 0), ("EDATE", 31, -1), ("EOMONTH", 0, 0), ("EOMONTH", -0.5, 0), ("EDATE", -0.0001, 0)]
    for function, start, months in probes:
        answers = sorted({str(months_away(function, start, months, r)) for r in fits})
        print(f"  ={function}({start},{months}): {' | '.join(answers)}" + ("   <- they part" if len(answers) > 1 else ""))

    print(f"\nA serial between -1 and 0 under the functions that read a date: {len(DATE_PARTS)} observed cases\n")
    for name, whole in STARTS.items():
        hits = 0
        for case, serial, answer, excel in DATE_PARTS:
            made = whole(serial)
            got = VALUE if made == VALUE else NUM if made < 0 else answer
            hits += got == excel
        print(f"{hits:2}/{len(DATE_PARTS)}  {name}")


if __name__ == "__main__":
    main()
