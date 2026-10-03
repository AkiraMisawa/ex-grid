#!/usr/bin/env python3
"""Acceptance experiment only: compare generic root solvers with recorded Excel results.

This is not an Excel solver and none of its functions are shipped. There are no
case-specific answers or iteration adjustments. Python's binary64 math supplies a
candidate recurrence; success here would still require validation of its C# port.
"""
from __future__ import annotations

import argparse
import datetime as dt
import decimal
import json
import math
import platform
from pathlib import Path
import re
import struct

ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = ROOT / "verification/2026-10-03-windows-functions"


class Refusal(Exception):
    pass


def scalar(value):
    if value is None:
        return 0.0
    if isinstance(value, str):
        if value == "=NA()" or value == "NA()":
            raise Refusal("#N/A")
        match = re.fullmatch(r"=DATE\((\d+),(\d+),(\d+)\)", value)
        if match:
            date = dt.date(*map(int, match.groups()))
            return float((date - dt.date(1899, 12, 30)).days)
        if value in ("TRUE", "FALSE"):
            return float(value == "TRUE")
        value = value.strip('"')
    try:
        return float(value)
    except (ValueError, TypeError):
        raise Refusal("#VALUE!")


def read_range(reference, cells):
    match = re.fullmatch(r"([A-Z]+)(\d+):\1(\d+)", reference)
    if not match:
        raise ValueError(f"Experiment does not parse this range: {reference}")
    column, first, last = match.groups()
    return [cells.get(f"{column}{row}") for row in range(int(first), int(last) + 1)]


def cash_equation(values, times):
    def equation(rate):
        if rate <= -1:
            raise Refusal("#NUM!")
        base = 1 + rate
        value = sum(flow / math.pow(base, time) for flow, time in zip(values, times))
        derivative = sum(-time * flow / math.pow(base, time + 1) for flow, time in zip(values, times))
        return value, derivative
    return equation


def rate_equation(nper, payment, present, future, kind):
    def equation(rate):
        if rate <= -1:
            raise Refusal("#NUM!")
        if rate == 0:
            return (present + payment * nper + future,
                    present * nper + payment * (kind * nper + nper * (nper - 1) / 2))
        power = math.pow(1 + rate, nper)
        derivative = nper * math.pow(1 + rate, nper - 1)
        annuity = (power - 1) / rate
        annuity_derivative = (derivative * rate - power + 1) / (rate * rate)
        return (present * power + payment * (1 + kind * rate) * annuity + future,
                present * derivative + payment * (kind * annuity + (1 + kind * rate) * annuity_derivative))
    return equation


def prepare(case):
    name = case["function"]
    body = case["formula"][len(name) + 2:-1]
    arguments = body.split(",")  # The observed financial calls have no nested comma-bearing expressions.
    cells = case["cells"]
    if name == "RATE":
        args = [scalar(v) for v in arguments]
        args += [0.0, 0.0, 0.1][len(args) - 3:]
        nper, payment, present, future, kind, guess = args
        if guess <= -1:
            raise Refusal("#VALUE!")
        if nper <= 0:
            raise Refusal("#NUM!")
        return rate_equation(nper, payment, present, future, int(kind != 0)), guess
    guess_index = 1 if name == "IRR" else 2
    guess = scalar(arguments[guess_index]) if len(arguments) > guess_index else 0.1
    if guess <= -1:
        raise Refusal("#VALUE!" if name == "IRR" else "#NUM!")
    raw = read_range(arguments[0], cells)
    if name == "IRR":
        if any(isinstance(v, str) and v.startswith("=") for v in raw):
            raise Refusal("#VALUE!")
        values = [float(v) for v in raw if type(v) in (float, int)]
        times = list(range(len(values)))
    else:
        raw_dates = read_range(arguments[1], cells)
        if len(raw_dates) != len(raw):
            raise Refusal("#NUM!")
        if any(type(v) is bool for v in raw + raw_dates):
            raise Refusal("#VALUE!")
        values = [scalar(v) for v in raw]
        dates = [math.trunc(scalar(v)) for v in raw_dates]
        if any(date < 0 or date > 2_958_465 for date in dates) or any(date < dates[0] for date in dates):
            raise Refusal("#NUM!")
        times = [(date - dates[0]) / 365 for date in dates]
    if not any(v > 0 for v in values) or not any(v < 0 for v in values):
        raise Refusal("#NUM!")
    return cash_equation(values, times), guess


def newton(equation, guess, tolerance, attempts):
    rate = guess
    for _ in range(attempts):
        value, derivative = equation(rate)
        if derivative == 0:
            raise Refusal("#NUM!")
        next_rate = rate - value / derivative
        if not math.isfinite(next_rate) or next_rate <= -1:
            raise Refusal("#NUM!")
        if abs(next_rate - rate) <= tolerance:
            return next_rate
        rate = next_rate
    raise Refusal("#NUM!")


def secant(equation, guess, tolerance, attempts):
    previous, rate = guess, guess + 0.0001
    previous_value = equation(previous)[0]
    for _ in range(attempts):
        value = equation(rate)[0]
        if value == previous_value:
            raise Refusal("#NUM!")
        next_rate = rate - value * (rate - previous) / (value - previous_value)
        if not math.isfinite(next_rate) or next_rate <= -1:
            raise Refusal("#NUM!")
        if abs(next_rate - rate) <= tolerance:
            return next_rate
        previous, previous_value, rate = rate, value, next_rate
    raise Refusal("#NUM!")


def candidate(case, method):
    try:
        equation, guess = prepare(case)
    except Refusal as error:
        return str(error)
    try:
        xirr = case["function"] == "XIRR"
        if method == "newton-tight":
            return newton(equation, guess, 1e-14, 100)
        tolerance, attempts = (1e-8, 100) if xirr else (1e-7, 20)
        return (newton if method == "newton" else secant)(equation, guess, tolerance, attempts)
    except (Refusal, OverflowError, ZeroDivisionError, ValueError) as error:
        return str(error) if isinstance(error, Refusal) else "#NUM!"


def significant(number):
    # Same 15-significant-digit admission criterion, on the exact binary64 value,
    # half away from zero. Neither decimal-place rounding nor a relative tolerance.
    with decimal.localcontext() as context:
        context.prec = 400
        number = decimal.Decimal.from_float(number)
        return number.quantize(decimal.Decimal(1).scaleb(number.adjusted() - 14),
                               rounding=decimal.ROUND_HALF_UP)


def observed(cell):
    if cell["kind"] == "error":
        return cell["text"]
    number = float(cell["roundTrip"])
    assert struct.pack(">d", number).hex().upper() == cell["bits"], "Observation lost binary64 precision"
    return number


def same(expected, actual):
    if isinstance(expected, str) or isinstance(actual, str):
        return expected == actual
    return significant(expected) == significant(actual)


def compare():
    cases = json.loads((EVIDENCE / "cases.json").read_text(encoding="utf-8-sig"))["cases"]
    records = json.loads((EVIDENCE / "results.json").read_text(encoding="utf-8-sig"))["cases"]
    results = {c["id"]: c["states"][0]["cells"][0] for c in records if c["function"] in ("IRR", "XIRR", "RATE")}
    report = {"runtime": {"python": platform.python_version(), "system": platform.system(), "machine": platform.machine()},
              "criterion": "15 significant digits of exact binary64, half away from zero; exact Error Value",
              "evidence": "verification/2026-10-03-windows-functions/results.json", "candidates": {}}
    for method in ("newton", "secant", "newton-tight"):
        groups = {}
        mismatches = []
        for case in cases:
            name = case["function"]
            if name not in ("IRR", "XIRR", "RATE"):
                continue
            expected, actual = observed(results[case["id"]]), candidate(case, method)
            group = groups.setdefault(name, {"cases": 0, "matched": 0, "mismatched": 0})
            group["cases"] += 1
            if same(expected, actual):
                group["matched"] += 1
            else:
                group["mismatched"] += 1
                mismatches.append({"id": case["id"], "formula": case["formula"],
                                   "expected": expected if isinstance(expected, str) else results[case["id"]]["roundTrip"],
                                   "actual": actual if isinstance(actual, str) else format(actual, ".17g")})
        report["candidates"][method] = {"summary": groups, "mismatches": mismatches}
    return report


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--all", action="store_true", help="Include every mismatch, rather than only the summary")
    args = parser.parse_args()
    report = compare()
    if not args.all:
        for value in report["candidates"].values():
            value.pop("mismatches")
    print(json.dumps(report, indent=2))
