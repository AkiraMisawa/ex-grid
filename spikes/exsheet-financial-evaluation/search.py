#!/usr/bin/env python3
"""Search for the evaluation Excel uses for PMT, PV, FV, NPV and XNPV.

Acceptance experiment only (ADR-0047, ADR-0202): nothing here ships. Each candidate is an
evaluation order, a way of raising 1+rate to nper, and a precision for the intermediate steps.
It is run over EVERY Excel double the oracle's results files hold for these functions (the
agreeing cases as well as the disagreeing ones) and counted: a candidate is a match only where
its double is Excel's, bit for bit. There are no case-specific answers.

Arithmetic is exact (fractions.Fraction) and rounded to nearest-even at a chosen significand
width after every operation: 53 bits is IEEE double, 64 bits is the x87's extended format.
Exponent range is not modelled (no input here comes near it). Logarithms and exponentials are
taken from `decimal` at 80 digits and then rounded, which models a correctly rounded library
function at that width. Standard library only:

    python3 spikes/exsheet-financial-evaluation/search.py            # the table
    python3 spikes/exsheet-financial-evaluation/search.py --json     # also rewrite results.json
    python3 spikes/exsheet-financial-evaluation/search.py --detail   # every candidate's doubles
"""
from __future__ import annotations

import argparse
import decimal
import glob
import itertools
import json
import math
import platform
import re
import struct
import sys
from fractions import Fraction as Q
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
RESULTS = ROOT / "tests/ExSheet.Engine.Tests/ExcelOracle"
CASES = ROOT / "tests/ExSheet.Engine.Tests/ExcelCases"
AREAS = ("fv", "pv", "pmt", "npv", "xnpv")

decimal.getcontext().prec = 80


# --------------------------------------------------------------------------------------------
# Rounded arithmetic


def rnd(x: Q, p: int) -> Q:
    """x rounded to nearest, ties to even, at a p-bit significand."""
    if x == 0:
        return Q(0)
    sign = -1 if x < 0 else 1
    a = abs(x)
    e = a.numerator.bit_length() - a.denominator.bit_length()
    if Q(2) ** e > a:
        e -= 1
    scale = p - 1 - e
    m = a * Q(2) ** scale
    n = m.numerator // m.denominator
    rest = m - n
    if rest > Q(1, 2) or (rest == Q(1, 2) and n % 2 == 1):
        n += 1
    return sign * Q(n) / Q(2) ** scale


def to_decimal(x: Q) -> decimal.Decimal:
    return decimal.Decimal(x.numerator) / decimal.Decimal(x.denominator)


def ln(x: Q, p: int) -> Q:
    return rnd(Q(to_decimal(x).ln()), p)


def exp(x: Q, p: int) -> Q:
    return rnd(Q(to_decimal(x).exp()), p)


def log2(x: Q) -> Q:
    """log2 to 80 digits, unrounded (the caller rounds the step it models)."""
    return Q(to_decimal(x).ln() / decimal.Decimal(2).ln())


def exp2(x: Q) -> Q:
    return Q((to_decimal(x) * decimal.Decimal(2).ln()).exp())


class N:
    """A number whose every operation rounds to the precision `N.p`."""

    p = 53

    def __init__(self, value):
        self.v = value if isinstance(value, Q) else Q(value)

    @staticmethod
    def _v(x):
        return x.v if isinstance(x, N) else Q(x)

    def __add__(self, o):
        return N(rnd(self.v + N._v(o), N.p))

    __radd__ = __add__

    def __sub__(self, o):
        return N(rnd(self.v - N._v(o), N.p))

    def __rsub__(self, o):
        return N(rnd(N._v(o) - self.v, N.p))

    def __mul__(self, o):
        return N(rnd(self.v * N._v(o), N.p))

    __rmul__ = __mul__

    def __truediv__(self, o):
        return N(rnd(self.v / N._v(o), N.p))

    def __rtruediv__(self, o):
        return N(rnd(N._v(o) / self.v, N.p))

    def __neg__(self):
        return N(-self.v)


class precision:
    """`with precision(64): ...` evaluates the block's operations at that width."""

    def __init__(self, p):
        self.p = p

    def __enter__(self):
        self.saved = N.p
        N.p = self.p

    def __exit__(self, *_):
        N.p = self.saved


def double(x) -> float:
    v = x.v if isinstance(x, N) else x
    return float(rnd(v, 53))


# --------------------------------------------------------------------------------------------
# (1 + rate) ^ nper


def g_pow(rate, n, p):
    """Correctly rounded power of the base rounded at p (what a perfect pow gives)."""
    b = rnd(1 + rate.v, p)
    return N(rnd(b ** n, p)) if isinstance(n, int) else N(rnd(Q(to_decimal(b) ** to_decimal(Q(n))), p))


def g_square_rtl(rate, n, p):
    with precision(p):
        base = 1 + rate
        result, x, k, first = N(1), base, int(n), True
        while k:
            if k & 1:
                result = x if first else result * x
                first = False
            k >>= 1
            if k:
                x = x * x
        return result


def g_square_ltr(rate, n, p):
    with precision(p):
        base = 1 + rate
        result = N(1)
        for bit in bin(int(n))[2:]:
            result = result * result
            if bit == "1":
                result = result * base
        return result


def g_repeat(rate, n, p):
    with precision(p):
        base = 1 + rate
        result = base
        for _ in range(int(n) - 1):
            result = result * base
        return result if n >= 1 else N(1)


def g_explog(rate, n, p):
    with precision(p):
        base = 1 + rate
        return N(exp(rnd(Q(n) * ln(base.v, p), p), p))


def g_explog1p(rate, n, p):
    lg = rnd(Q(to_decimal(rate.v).ln1p()) if hasattr(decimal.Decimal, "ln1p") else Q((1 + to_decimal(rate.v)).ln()), p)
    return N(exp(rnd(Q(n) * lg, p), p))


def g_x87(rate, n, p):
    """FYL2X (n*log2(b), one rounding at 64 bits), FRNDINT, F2XM1 on the fraction, FSCALE."""
    b = rnd(1 + rate.v, p)
    t = rnd(Q(n) * log2(b), 64)
    i = math.floor(t + Q(1, 2))
    f = t - i
    m = rnd(exp2(f) - 1, 64)
    return N(rnd((m + 1) * Q(2) ** i, 64))


def g_crt(rate, n, p):
    """The C library's pow (CPython's math.pow, glibc on Linux), in double."""
    return N(Q(math.pow(1 + float(rate.v), float(n))))


# Excess forms: e = (1+rate)^n - 1 carried without ever forming 1+rate, so the low bits of the
# rate are not lost. (1+a)(1+b)-1 = a + b + ab; (1+a)^2-1 = a(2+a). Each returns (g, e) with
# g = 1 + e rounded.

COMBINE = {
    "(a+b)+ab": lambda a, b: (a + b) + a * b,
    "a+(b+ab)": lambda a, b: a + (b + a * b),
    "ab+(a+b)": lambda a, b: a * b + (a + b),
    "(ab+a)+b": lambda a, b: (a * b + a) + b,
}
SQUARE = {
    "a(2+a)": lambda a: a * (2 + a),
    "(a+a)+aa": lambda a: (a + a) + a * a,
    "aa+(a+a)": lambda a: a * a + (a + a),
}


def excess(order, combine, square):
    def growth(rate, n, p):
        with precision(p):
            k = int(n)
            if order == "rtl":
                result, x = None, rate
                while k:
                    if k & 1:
                        result = x if result is None else COMBINE[combine](result, x)
                    k >>= 1
                    if k:
                        x = SQUARE[square](x)
            elif order == "ltr":
                result = N(0)
                for bit in bin(k)[2:]:
                    result = SQUARE[square](result)
                    if bit == "1":
                        result = COMBINE[combine](result, rate)
            else:
                result = rate
                for _ in range(k - 1):
                    result = COMBINE[combine](result, rate)
            return (1 + result, result)

    return growth


def g_expm1_log1p(rate, n, p):
    """L = log1p(rate); g = exp(nL), e = expm1(nL), each correctly rounded at p."""
    lg = rnd(Q((1 + to_decimal(rate.v)).ln()), p)
    t = rnd(Q(n) * lg, p)
    g = rnd(Q(to_decimal(t).exp()), p)
    e = rnd(Q(to_decimal(t).exp() - 1), p)
    return (N(g), N(e))


def g_pv_fv(rate, n, p):
    """The PV/FV model (2026-10-12): squaring for a whole n, 1 over it for a negative one, the C library's
    exp(n log(1+rate)) otherwise, all in doubles. Python's math.exp and math.log are glibc's, as .NET's on Linux."""
    b = 1 + float(rate.v)
    if n == int(n):
        k = abs(int(n))
        g = float(g_square_rtl(N(rate.v), k, 53).v) if k else 1.0
        return N(Q(g if n >= 0 else 1 / g))
    return N(Q(math.exp(float(n) * math.log(b))))


GROWTH = {
    "pv-fv": g_pv_fv,
    "pow": g_pow,
    "square-rtl": g_square_rtl,
    "square-ltr": g_square_ltr,
    "repeat": g_repeat,
    "exp-log": g_explog,
    "exp-log1p": g_explog1p,
    "x87-f2xm1": g_x87,
    "crt-pow": g_crt,
    "expm1-log1p": g_expm1_log1p,
}
for _order in ("rtl", "ltr", "repeat"):
    for _combine in COMBINE:
        for _square in SQUARE if _order != "repeat" else ["a(2+a)"]:
            GROWTH[f"excess-{_order} {_combine} {_square}"] = excess(_order, _combine, _square)
INTEGER_ONLY = {"square-rtl", "square-ltr", "repeat"} | {k for k in GROWTH if k.startswith("excess")}
EXTENDED_ONLY = {"x87-f2xm1"}
DOUBLE_ONLY = {"crt-pow", "pv-fv"}


# --------------------------------------------------------------------------------------------
# Forms: each takes N arguments and g = (1+rate)^nper (already an N)


def fv_forms():
    return {
        "F1 -(pv*g + pmt*(1+r*t)*((g-1)/r))": lambda r, n, pmt, pv, t, g, e, grow: -(pv * g + pmt * (1 + r * t) * (e / r)),
        "F2 -(pv*g + pmt*(1+r*t)*(g-1)/r)": lambda r, n, pmt, pv, t, g, e, grow: -(pv * g + pmt * (1 + r * t) * e / r),
        "F3 -(pv*g + (pmt*(1+r*t)/r)*(g-1))": lambda r, n, pmt, pv, t, g, e, grow: -(pv * g + (pmt * (1 + r * t) / r) * e),
        "F4 -(pv*g + pmt*((1+r*t)*((g-1)/r)))": lambda r, n, pmt, pv, t, g, e, grow: -(pv * g + pmt * ((1 + r * t) * (e / r))),
        "F5 -((pv + pmt*(1+r*t)/r)*g - pmt*(1+r*t)/r)": lambda r, n, pmt, pv, t, g, e, grow: -((pv + pmt * (1 + r * t) / r) * g - pmt * (1 + r * t) / r),
        "F6 -(pmt*(1+r*t)*((g-1)/r) + pv*g)": lambda r, n, pmt, pv, t, g, e, grow: -(pmt * (1 + r * t) * (e / r) + pv * g),
        "F7 -(pv*g + pmt*(1+r*t)*(g-1)/r), (g-1)/r first then *(1+r*t)*pmt": lambda r, n, pmt, pv, t, g, e, grow: -(pv * g + (e / r) * (1 + r * t) * pmt),
    }


def pv_forms():
    return {
        "V1 -(fv + pmt*(1+r*t)*((g-1)/r))/g": lambda r, n, pmt, fv, t, g, e, grow: -(fv + pmt * (1 + r * t) * (e / r)) / g,
        "V2 -(fv + pmt*(1+r*t)*(g-1)/r)/g": lambda r, n, pmt, fv, t, g, e, grow: -(fv + pmt * (1 + r * t) * e / r) / g,
        "V3 -(fv/g + pmt*(1+r*t)*((1-1/g)/r))": lambda r, n, pmt, fv, t, g, e, grow: -(fv / g + pmt * (1 + r * t) * ((1 - 1 / g) / r)),
        "V4 -(fv + (pmt*(1+r*t)/r)*(g-1))/g": lambda r, n, pmt, fv, t, g, e, grow: -(fv + (pmt * (1 + r * t) / r) * e) / g,
        "V5 -(fv + pmt*((1+r*t)*((g-1)/r)))/g": lambda r, n, pmt, fv, t, g, e, grow: -(fv + pmt * ((1 + r * t) * (e / r))) / g,
        "V6 -(pmt*(1+r*t)*((g-1)/r) + fv)/g": lambda r, n, pmt, fv, t, g, e, grow: -(pmt * (1 + r * t) * (e / r) + fv) / g,
        "V7 -(fv + pmt*(1+r*t)*((g-1)/r))*(1/g)": lambda r, n, pmt, fv, t, g, e, grow: -(fv + pmt * (1 + r * t) * (e / r)) * (1 / g),
    }


def pmt_forms():
    return {
        "M1 -(r*(pv*g+fv))/((1+r*t)*(g-1))": lambda r, n, pv, fv, t, g, e, grow: -(r * (pv * g + fv)) / ((1 + r * t) * e),
        "M2 -(pv*g+fv)/((1+r*t)*((g-1)/r))": lambda r, n, pv, fv, t, g, e, grow: -(pv * g + fv) / ((1 + r * t) * (e / r)),
        "M3 -(pv*g+fv)*r/((1+r*t)*(g-1))": lambda r, n, pv, fv, t, g, e, grow: -(pv * g + fv) * r / ((1 + r * t) * e),
        "M4 -((pv*g+fv)/((g-1)/r))/(1+r*t)": lambda r, n, pv, fv, t, g, e, grow: -((pv * g + fv) / (e / r)) / (1 + r * t),
        "M5 -(pv*g+fv)/(g-1)*r/(1+r*t)": lambda r, n, pv, fv, t, g, e, grow: -(pv * g + fv) / e * r / (1 + r * t),
        "M6 -(pv+fv/g)*r/((1+r*t)*(1-1/g))": lambda r, n, pv, fv, t, g, e, grow: -(pv + fv / g) * r / ((1 + r * t) * (1 - 1 / g)),
        "M7 -(fv+pv*g)/((1+r*t)*((g-1)/r))": lambda r, n, pv, fv, t, g, e, grow: -(fv + pv * g) / ((1 + r * t) * (e / r)),
        "M8 -(pv*g+fv)/(pmt-factor (1+r*t)*(g-1)/r)": lambda r, n, pv, fv, t, g, e, grow: -(pv * g + fv) / ((1 + r * t) * e / r),
        "M9 -(pv*r*g + fv*r)/((1+r*t)*(g-1))": lambda r, n, pv, fv, t, g, e, grow: -(pv * r * g + fv * r) / ((1 + r * t) * e),
        "M10 -(pv*r/(1-1/g) + fv*r/(g-1))/(1+r*t)": lambda r, n, pv, fv, t, g, e, grow: -(pv * r / (1 - 1 / g) + fv * r / e) / (1 + r * t),
        "M11 -(pv*g+fv)/(((1+r*t)*(g-1))/r)": lambda r, n, pv, fv, t, g, e, grow: -(pv * g + fv) / (((1 + r * t) * e) / r),
        "M12 -(pv*g+fv)/((g-1)*(1+r*t)/r)": lambda r, n, pv, fv, t, g, e, grow: -(pv * g + fv) / (e * (1 + r * t) / r),
        "M13 -(pv*g+fv)/((g-1)/r*(1+r*t))": lambda r, n, pv, fv, t, g, e, grow: -(pv * g + fv) / (e / r * (1 + r * t)),
        "M14 -(pv*g+fv)*r/e, in advance /(e[n+1]-r)": lambda r, n, pv, fv, t, g, e, grow: -(pv * g + fv) * r / (e if t.v == 0 else grow(n + 1)[1] - r),
        "M15 -((pv*g+fv)*r/e)/(1+r*t)": lambda r, n, pv, fv, t, g, e, grow: -((pv * g + fv) * r / e) / (1 + r * t),
        "M16 -(pv*g/e + fv/e)*r/(1+r*t)": lambda r, n, pv, fv, t, g, e, grow: -(pv * g / e + fv / e) * r / (1 + r * t),
        "M17 -(pv*(r*g/e) + fv*(r/e))/(1+r*t)": lambda r, n, pv, fv, t, g, e, grow: -(pv * (r * g / e) + fv * (r / e)) / (1 + r * t),
        "M18 -(pv*(r+r/e) + fv*(r/e))/(1+r*t)": lambda r, n, pv, fv, t, g, e, grow: -(pv * (r + r / e) + fv * (r / e)) / (1 + r * t),
        "M19 -(pv*r + (pv+fv)*r/e)/(1+r*t)": lambda r, n, pv, fv, t, g, e, grow: -(pv * r + (pv + fv) * r / e) / (1 + r * t),
        "M20 -(pv*r + (pv+fv)/(e/r))/(1+r*t)": lambda r, n, pv, fv, t, g, e, grow: -(pv * r + (pv + fv) / (e / r)) / (1 + r * t),
    }


# NPV: values v[0..k-1], the first one period away


def gval(x):
    return x[0] if isinstance(x, tuple) else x


def npv_power_each(rate, values, growth, p):
    with precision(p):
        total = N(0)
        for i, v in enumerate(values):
            total = total + v / gval(growth(rate, i + 1, p))
        return total


def npv_running_divide(rate, values, growth, p):
    with precision(p):
        base, d, total = 1 + rate, N(1), N(0)
        for v in values:
            d = d * base
            total = total + v / d
        return total


def npv_running_factor(rate, values, growth, p):
    with precision(p):
        f = 1 / (1 + rate)
        d, total = N(1), N(0)
        for v in values:
            d = d * f
            total = total + v * d
        return total


def npv_horner_back(rate, values, growth, p):
    with precision(p):
        base, total = 1 + rate, N(0)
        for v in reversed(values):
            total = (total + v) / base
        return total


def npv_horner_back_factor(rate, values, growth, p):
    with precision(p):
        f, total = 1 / (1 + rate), N(0)
        for v in reversed(values):
            total = (total + v) * f
        return total


def npv_horner_forward(rate, values, growth, p):
    with precision(p):
        base, total = 1 + rate, N(0)
        for v in values:
            total = total * base + v
        return total / gval(growth(rate, len(values), p))


def npv_reverse_sum(rate, values, growth, p):
    with precision(p):
        terms = [v / gval(growth(rate, i + 1, p)) for i, v in enumerate(values)]
        total = N(0)
        for term in reversed(terms):
            total = total + term
        return total


def npv_kahan(rate, values, growth, p):
    with precision(p):
        total, c = N(0), N(0)
        for i, v in enumerate(values):
            y = v / gval(growth(rate, i + 1, p)) - c
            t = total + y
            c = (t - total) - y
            total = t
        return total


def npv_running_divide_ext_sum(rate, values, growth, p):
    """Terms in double, the sum in extended."""
    terms = []
    with precision(53):
        base, d = 1 + rate, N(1)
        for v in values:
            d = d * base
            terms.append(v / d)
    with precision(64):
        total = N(0)
        for term in terms:
            total = total + term
    return total


NPV = {
    "N1 total += v/(1+r)^i": (npv_power_each, True),
    "N2 d *= 1+r; total += v/d": (npv_running_divide, False),
    "N3 f = 1/(1+r); d *= f; total += v*d": (npv_running_factor, False),
    "N4 Horner from the last: t = (t+v)/(1+r)": (npv_horner_back, False),
    "N5 Horner from the last: t = (t+v)*(1/(1+r))": (npv_horner_back_factor, False),
    "N6 Horner from the first: t = t*(1+r)+v, then / (1+r)^n": (npv_horner_forward, True),
    "N7 terms v/(1+r)^i summed from the last": (npv_reverse_sum, True),
    "N8 terms v/(1+r)^i, Kahan sum": (npv_kahan, True),
    "N9 N2's terms in double, summed in extended": (npv_running_divide_ext_sum, False),
}


# --------------------------------------------------------------------------------------------
# The observations: every Excel double the results files hold for these areas


def excel_arithmetic(text: str) -> Q:
    """The few argument expressions the corpus writes (0.06/12, 8%/12, 12*20), in double."""
    text = text.strip()
    if text == "":
        return Q(0)
    m = re.fullmatch(r"(-?[\d.]+)(%?)(?:([*/])(-?[\d.]+))?", text)
    if not m:
        raise ValueError(text)
    a = Q(float(m.group(1)))
    if m.group(2):
        a = rnd(a / 100, 53)
    if m.group(3) == "/":
        a = rnd(a / Q(float(m.group(4))), 53)
    elif m.group(3) == "*":
        a = rnd(a * Q(float(m.group(4))), 53)
    return a


def fixture_cells(case):
    cells = {}
    if case.get("fixture"):
        fixtures = json.loads((CASES / "fixtures.json").read_text(encoding="utf-8"))["fixtures"]
        cells.update(fixtures[case["fixture"]]["cells"])
    cells.update(case.get("cells", {}))
    return cells


def numbers_in(reference, cells):
    """The numbers SUM takes from a range; text, Booleans and blanks passed over."""
    m = re.fullmatch(r"([A-Z]+)(\d+):\1(\d+)", reference)
    out = []
    for row in range(int(m.group(2)), int(m.group(3)) + 1):
        raw = cells.get(f"{m.group(1)}{row}")
        if raw is None or raw.startswith("'") or raw.startswith("="):
            continue
        try:
            out.append(Q(float(raw)))
        except ValueError:
            continue
    return out


def load_observations():
    corpus = {}
    for path in CASES.glob("*.json"):
        data = json.loads(path.read_text(encoding="utf-8"))
        if data.get("area") in AREAS:
            for case in data["cases"]:
                corpus[case["id"]] = case
    seen = {}
    for path in sorted(RESULTS.glob("results-*.json")):
        data = json.loads(path.read_text(encoding="utf-8-sig"))
        for case in data.get("cases", []):
            if case.get("area") not in AREAS:
                continue
            excel = case.get("excel") or {}
            if excel.get("kind") != "number":
                continue
            value = float(excel["number"])
            key = (case["id"], excel.get("formula"))
            if key in seen and seen[key]["excel"] != value:
                raise SystemExit(f"{case['id']}: two runs disagree ({seen[key]['excel']!r}, {value!r})")
            seen.setdefault(key, {"id": case["id"], "formula": excel["formula"], "excel": value, "files": []})
            seen[key]["files"].append(path.name)
    observations = []
    for (cid, formula), obs in sorted(seen.items()):
        obs["case"] = corpus.get(cid)
        observations.append(obs)
    return observations


def parse_call(formula):
    m = re.fullmatch(r"=([A-Z]+)\((.*)\)(\+([A-Z]+\d+))?", formula)
    return m.group(1), m.group(2).split(","), m.group(4)


def annuity_args(args):
    """rate, nper, third, fourth, type: Excel's arithmetic on the corpus's literals."""
    values = [excel_arithmetic(a) for a in args] + [Q(0)] * (5 - len(args))
    return values[:5]


# --------------------------------------------------------------------------------------------
# Candidates


def precisions(growth_name):
    gp = [64] if growth_name in EXTENDED_ONLY else [53] if growth_name in DOUBLE_ONLY else [53, 64]
    return [(a, b) for a in gp for b in (53, 64)]


def run_annuity(function, form, growth, gp, ep, args, due="inline", growth_name=""):
    rate, nper, third, fourth, kind = args
    if kind != 0:
        kind = Q(1)  # Excel reads type as a truth value: 2, 0.5 and -1 each answer as 1 (PMT-008 to PMT-010)
    if rate == 0:
        return None
    if growth_name in INTEGER_ONLY and (nper != int(nper) or nper < 0):
        return None  # squaring and the excess forms raise only to a whole number from 0 (PMT-011, PMT-012 ask others)
    k = int(nper) if nper == int(nper) else nper

    def grow(m):
        found = growth(N(rate), m, gp)
        g, e = found if isinstance(found, tuple) else (found, None)
        with precision(ep):
            g = N(rnd(g.v, ep))
            e = g - 1 if e is None else N(rnd(e.v, ep))
        return g, e

    g, e = grow(k)
    with precision(ep):
        r, a3, a4, t = N(rate), N(third), N(fourth), N(kind)
        if due == "inline":
            return double(form(r, k, a3, a4, t, g, e, grow))
        ordinary = double(form(r, k, a3, a4, N(0), g, e, grow))  # stored to a double
    if kind == 0:
        return ordinary
    if due == "exact":
        return float(rnd(Q(ordinary) / (1 + rate), 53))
    p = int(due)
    with precision(p):
        return double(N(Q(ordinary)) / (1 + N(rate)))


DUE = {
    "inline": "(1+r*t) inside the form",
    "53": "type 1: the ordinary payment, stored to a double, then / (1+r) in double",
    "64": "type 1: the ordinary payment, stored to a double, then / (1+r) in extended",
    "exact": "type 1: the ordinary payment, stored to a double, then / (1+r) correctly rounded",
}


def annuity_candidates(function, forms):
    dues = DUE if function == "PMT" else {"inline": DUE["inline"]}
    for gname, growth in GROWTH.items():
        for gp, ep in precisions(gname):
            for fname, form in forms.items():
                for due in dues:
                    if due != "inline" and fname.startswith("M14"):
                        continue
                    tag = "" if due == "inline" else f" | due={due}"
                    yield f"{fname} | g={gname}@{gp} | rest@{ep}{tag}", (lambda args, form=form, growth=growth, gp=gp, ep=ep, due=due, gname=gname: run_annuity(function, form, growth, gp, ep, args, due, gname))


def npv_candidates():
    for nname, (method, uses_growth) in NPV.items():
        growths = GROWTH.items() if uses_growth else [("-", None)]
        for gname, growth in growths:
            for p in ([64] if gname in EXTENDED_ONLY else [53] if gname in DOUBLE_ONLY else [53, 64]):
                if nname.startswith("N9") and p == 64:
                    continue
                yield f"{nname} | g={gname} | @{p}", (lambda rate, values, method=method, growth=growth, p=p: double(method(N(rate), [N(v) for v in values], growth, p)))


def npv_inputs(obs):
    name, args, plus = parse_call(obs["formula"])
    cells = fixture_cells(obs["case"]) if obs["case"] else {}
    rate_text = args[0]
    rate = Q(float(cells[rate_text])) if re.fullmatch(r"[A-Z]+\d+", rate_text) else excel_arithmetic(rate_text)
    values = []
    for a in args[1:]:
        if ":" in a:
            values += numbers_in(a, cells)
        elif re.fullmatch(r"[A-Z]+\d+", a):
            values += numbers_in(f"{a}:{a}", cells)
        else:
            values.append(excel_arithmetic(a))
    after = Q(float(cells[plus])) if plus else None
    return rate, values, after


def xnpv_inputs(obs):
    cells = fixture_cells(obs["case"])
    name, args, _ = parse_call(obs["formula"])
    rate = excel_arithmetic(args[0])
    values = numbers_in(args[1], cells)
    dates = numbers_in(args[2], cells)
    return rate, values, dates


def xnpv_candidates():
    def run(rate, values, dates, growth_name, p, divide):
        first = dates[0]
        with precision(p):
            total = N(0)
            for v, d in zip(values, dates):
                years = N(d - first) / 365 if divide == "/365" else N(d - first) * (1 / N(365))
                if growth_name == "pow":
                    g = N(rnd(Q(to_decimal(rnd(1 + rate, p)) ** to_decimal(years.v)), p))
                elif growth_name == "exp-log":
                    g = N(exp(rnd(years.v * ln(rnd(1 + rate, p), p), p), p))
                elif growth_name == "x87-f2xm1":
                    g = g_x87(N(rate), years.v, 64)
                else:
                    g = N(Q(math.pow(1 + float(rate), float(years.v))))
                total = total + v / g
            return double(total)

    for gname in ("pow", "exp-log", "x87-f2xm1", "crt-pow"):
        for p in ([64] if gname == "x87-f2xm1" else [53] if gname == "crt-pow" else [53, 64]):
            for divide in ("/365", "*(1/365)"):
                yield f"X total += v/(1+r)^((d-d0){divide}) | g={gname} | @{p}", (lambda rate, values, dates, gname=gname, p=p, divide=divide: run(rate, values, dates, gname, p, divide))


# --------------------------------------------------------------------------------------------


def search():
    observations = load_observations()
    by_function = {}
    for obs in observations:
        name = parse_call(obs["formula"])[0]
        by_function.setdefault(name, []).append(obs)

    table = {}
    for name, forms in (("FV", fv_forms()), ("PV", pv_forms()), ("PMT", pmt_forms())):
        cases = [o for o in by_function.get(name, []) if annuity_args(parse_call(o["formula"])[1])[0] != 0]
        rows = []
        for label, run in annuity_candidates(name, forms):
            got = {}
            for o in cases:
                try:
                    got[o["id"]] = run(annuity_args(parse_call(o["formula"])[1]))
                except (ZeroDivisionError, OverflowError, decimal.InvalidOperation):
                    got[o["id"]] = None
            rows.append((label, got))
        table[name] = (cases, rows)

    cases = by_function.get("NPV", [])
    rows = []
    for label, run in npv_candidates():
        got = {}
        for o in cases:
            rate, values, after = npv_inputs(o)
            value = run(rate, values)
            got[o["id"]] = value if after is None else float(Q(value) + after)  # Excel's + in double: exact here
        rows.append((label, got))
    table["NPV"] = (cases, rows)

    cases = by_function.get("XNPV", [])
    rows = []
    for label, run in xnpv_candidates():
        rows.append((label, {o["id"]: run(*xnpv_inputs(o)) for o in cases}))
    table["XNPV"] = (cases, rows)
    return observations, table


def signed(v):
    return v if isinstance(v, str) else f"{v:+d}"


def ulps(a, b):
    if b is None:
        return "none"
    ia = struct.unpack("<q", struct.pack("<d", a))[0]
    ib = struct.unpack("<q", struct.pack("<d", b))[0]
    return ib - ia


# --------------------------------------------------------------------------------------------
# NPV, wider: each term's discount by several methods, the terms summed in several orders, each
# step at 53 or 64 bits. NPV-001 is reproduced only by the running product in doubles summed
# forward; NPV-002 only by orders and precisions that NPV-001 rules out.


def npv_terms(method, rate, values, p):
    with precision(p):
        b = 1 + N(rate)
        out = []
        if method == "running product, v/d":
            d = N(1)
            for v in values:
                d = d * b
                out.append(N(v) / d)
        elif method == "running product, v*(1/d)":
            d = N(1)
            for v in values:
                d = d * b
                out.append(N(v) * (1 / d))
        elif method == "running quotient d/(1+r), v*d":
            d = N(1)
            for v in values:
                d = d / b
                out.append(N(v) * d)
        elif method == "running factor d*(1/(1+r)), v*d":
            f, d = 1 / b, N(1)
            for v in values:
                d = d * f
                out.append(N(v) * d)
        else:
            growth = GROWTH[method.split(" ", 1)[1]]
            for i, v in enumerate(values):
                g = gval(growth(N(rate), i + 1, p))
                out.append(N(v) / g if method.startswith("v/") else N(v) * (1 / g))
        return out


def npv_sum(order, terms, p):
    with precision(p):
        if order == "forward":
            total = N(0)
            for x in terms:
                total = total + x
            return total
        if order == "from the last":
            total = N(0)
            for x in reversed(terms):
                total = total + x
            return total
        if order == "pairwise":
            xs = list(terms)
            while len(xs) > 1:
                ys = [xs[i] + xs[i + 1] for i in range(0, len(xs) - 1, 2)]
                if len(xs) % 2:
                    ys.append(xs[-1])
                xs = ys
            return xs[0]
        if order == "Kahan":
            total, c = N(0), N(0)
            for x in terms:
                y = x - c
                t = total + y
                c = (t - total) - y
                total = t
            return total
        if order == "positive and negative apart":
            pos, neg = N(0), N(0)
            for x in terms:
                if x.v >= 0:
                    pos = pos + x
                else:
                    neg = neg + x
            return pos + neg
        if order == "by magnitude":
            total = N(0)
            for x in sorted(terms, key=lambda z: abs(z.v)):
                total = total + x
            return total


NPV_TERMS = ["running product, v/d", "running product, v*(1/d)", "running quotient d/(1+r), v*d", "running factor d*(1/(1+r)), v*d"] + [
    f"{kind} {g}" for kind in ("v/", "v*1/") for g in ("pow", "square-rtl", "square-ltr", "repeat", "exp-log", "exp-log1p", "expm1-log1p", "crt-pow")
]
NPV_ORDERS = ["forward", "from the last", "pairwise", "Kahan", "positive and negative apart", "by magnitude"]


def npv_wide(observations):
    cases = [o for o in observations if parse_call(o["formula"])[0] == "NPV"]
    rows = []
    for method in NPV_TERMS:
        for tp in (53, 64):
            if method.endswith("crt-pow") and tp == 64:
                continue
            for sp in (53, 64):
                for order in NPV_ORDERS:
                    got = {}
                    for o in cases:
                        rate, values, after = npv_inputs(o)
                        terms = [N(rnd(x.v, sp)) for x in npv_terms(method, rate, values, tp)]
                        value = double(npv_sum(order, terms, sp))
                        got[o["id"]] = value if after is None else float(Q(value) + after)
                    rows.append((f"{method} | terms@{tp} | {order}@{sp}", got))
    return cases, rows


# --------------------------------------------------------------------------------------------
# The evaluation the engine takes, for the C# port to be checked against


def model_fv(rate, nper, pmt, pv, kind):
    """FV as the engine evaluates it (2026-10-12): F4, the payment times (1+r*t)*((g-1)/r)."""
    if rate == 0:
        return float(-(pv + pmt * nper))
    with precision(53):
        g = g_pv_fv(N(rate), nper, 53)
        r, t = N(rate), N(1 if kind else 0)
        return double(-(N(pv) * g + N(pmt) * ((1 + r * t) * ((g - 1) / r))))


def model_pv(rate, nper, pmt, fv, kind):
    """PV as the engine evaluates it (2026-10-12): V5, the payment grouped as FV's."""
    if rate == 0:
        return float(-(fv + pmt * nper))
    with precision(53):
        g = g_pv_fv(N(rate), nper, 53)
        r, t = N(rate), N(1 if kind else 0)
        return double(-(N(fv) + N(pmt) * ((1 + r * t) * ((g - 1) / r))) / g)


def model_pmt(rate, nper, pv, fv, kind):
    """The payment at the end, exact and rounded once; at the start, that double over 1+rate, rounded once.

    Returns (double, margin): margin is how near, in units of the last place, the exact value lies
    to a rounding boundary; below about 2^-40 an x87 and a double-double could round differently.
    """
    if rate == 0:
        return float(rnd(-(pv + fv) / nper, 53)), 1
    g = (1 + rate) ** int(nper)
    exact = -(rate * (pv * g + fv)) / (g - 1)
    at_end = rnd(exact, 53)
    margin = boundary_margin(exact)
    if not kind:
        return float(at_end), margin
    quotient = at_end / (1 + rate)
    return float(rnd(quotient, 53)), min(margin, boundary_margin(quotient))


def boundary_margin(x):
    """Distance from x to the nearest midpoint between doubles, in units of x's last place."""
    lo = rnd(x, 53)
    a = abs(x)
    e = a.numerator.bit_length() - a.denominator.bit_length()
    if Q(2) ** e > a:
        e -= 1
    ulp = Q(2) ** (e - 52)
    return float(abs(abs(abs(x - lo) / ulp) - Q(1, 2)))


def vectors(count, seed):
    """Inputs of the kinds a Consumer types, and the model's doubles for them."""
    import random

    rng = random.Random(seed)
    out = []
    while len(out) < count:
        k, d, sign = rng.choice([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 15, 18]), rng.choice([1, 2, 4, 12, 52, 100, 1200]), rng.choice([1, 1, 1, -1])
        rate_text = f"{'-' if sign < 0 else ''}{k}/{d}/100"  # as a Formula writes it: two divisions in doubles
        rate = float(rnd(rnd(Q(k) / d, 53) / 100, 53)) * sign
        nper = rng.choice([1, 2, 3, 5, 7, 10, 12, 24, 30, 36, 48, 60, 120, 180, 240, 300, 360, 480, 1000, 0.5, 10.5, 36.25, -1, -12, -10.5])
        a = float(rng.choice([0, 100, 1000, 2500, 10000, 50000, 125000, 250000, 1e6]) * rng.choice([1, -1]))
        b = float(rng.choice([0, 0, 500, 1000, 20000]) * rng.choice([1, -1]))
        kind = rng.choice([0, 1, 2, 0.5, -1])
        q = [Q(rate), Q(nper), Q(a), Q(b)]
        try:
            # PMT refuses an nper that is not a whole number from 0 (ADR-0047, note of 2026-10-12)
            pmt, margin = model_pmt(q[0], nper, q[2], q[3], kind) if nper == int(nper) and nper > 0 else (None, 1)
            fv = model_fv(q[0], nper, q[2], q[3], kind)
            pv = model_pv(q[0], nper, q[2], q[3], kind)
        except (ZeroDivisionError, OverflowError):
            continue
        if not all(v is None or math.isfinite(v) for v in (pmt, fv, pv)):
            continue
        out.append({"rateText": rate_text, "rate": rate, "nper": nper, "a": a, "b": b, "type": kind, "pmt": pmt, "pmtMargin": margin, "fv": fv, "pv": pv})
    return out


# --------------------------------------------------------------------------------------------
# Inputs that would tell the surviving candidates apart, for the next Windows run


def discriminate(function, survivors, grid, limit):
    """Greedy: the inputs whose predictions split the survivors most, until each group is alone."""
    predictions = {}
    for args in grid:
        predictions[args] = {label: run(list(map(Q, args))) for label, run in survivors}
    groups = [set(label for label, _ in survivors)]
    chosen = []
    while len(chosen) < limit:
        best, best_groups = None, groups
        for args, by_label in predictions.items():
            split = []
            for group in groups:
                parts = {}
                for label in group:
                    parts.setdefault(by_label[label], set()).add(label)
                split.extend(parts.values())
            if len(split) > len(best_groups):
                best, best_groups = args, split
        if best is None:
            break
        chosen.append((best, {v: sorted(k for k in predictions[best] if predictions[best][k] == v) for v in set(predictions[best].values())}))
        groups = best_groups
    return chosen, groups


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--json", action="store_true", help="rewrite results.json")
    parser.add_argument("--detail", action="store_true", help="print every candidate's doubles")
    parser.add_argument("--vectors", metavar="FILE", help="write the engine model's doubles for generated inputs")
    parser.add_argument("--discriminate", action="store_true", help="list inputs that tell the survivors apart")
    parser.add_argument("--spread", metavar="FORMULA", action="append", default=[], help="the doubles PMT's survivors give for a formula such as =PMT(1/10000000,12,1000)")
    options = parser.parse_args()
    observations, table = search()
    table["NPV, wider"] = npv_wide(observations)

    print(f"{len(observations)} Excel doubles for {', '.join(sorted({parse_call(o['formula'])[0] for o in observations}))}")
    for o in observations:
        print(f"  {o['id']:10} {o['formula']:36} {o['excel']!r}  ({len(o['files'])} results files)")
    record = {"python": sys.version.split()[0], "platform": platform.platform(), "observations": [{k: o[k] for k in ("id", "formula", "excel")} for o in observations], "functions": {}}
    survivors_of = {}
    for name, (cases, rows) in table.items():
        scored = []
        for label, got in rows:
            matches = sum(1 for o in cases if got[o["id"]] == o["excel"])
            scored.append((matches, label, got))
        scored.sort(key=lambda x: -x[0])
        full = [s for s in scored if s[0] == len(cases)]
        survivors_of[name] = [label for _, label, _ in full]
        print(f"\n{name}: {len(cases)} observed doubles (a rate of 0 is not a search case), {len(scored)} candidates, {len(full)} reproduce all")
        shown = scored if options.detail else scored[:12]
        for matches, label, got in shown:
            off = ", ".join(f"{o['id']} {signed(ulps(o['excel'], got[o['id']]))}" for o in cases if got[o["id"]] != o["excel"])
            print(f"  {matches}/{len(cases)}  {label}" + (f"   [ulps off: {off}]" if off else ""))
        histogram = {}
        for matches, _, _ in scored:
            histogram[matches] = histogram.get(matches, 0) + 1
        record["functions"][name] = {
            "cases": [o["id"] for o in cases],
            "candidates": len(scored),
            "matchCounts": {f"{k}/{len(cases)}": v for k, v in sorted(histogram.items(), reverse=True)},
            "reproduceAll": [label for _, label, _ in full] if len(full) <= 40 else {
                "count": len(full),
                "byEvaluation": {family: sum(1 for _, label, _ in full if label.split(" | ", 1)[1] == family) for family in sorted({label.split(" | ", 1)[1] for _, label, _ in full})},
            },
            "bestOthers": [{"candidate": label, "matches": matches, "ulpsOff": {o["id"]: ulps(o["excel"], got[o["id"]]) for o in cases if got[o["id"]] != o["excel"]}} for matches, label, got in scored if matches < len(cases)][:40],
        }
    if options.json:
        (Path(__file__).parent / "results.json").write_text(json.dumps(record, indent=1) + "\n", encoding="utf-8")
    if options.vectors:
        Path(options.vectors).write_text(json.dumps(vectors(400, 17), indent=0) + "\n", encoding="utf-8")
    if options.discriminate:
        print_discriminators(survivors_of)
    for formula in options.spread:
        print_spread(formula, survivors_of["PMT"])


def print_spread(formula, labels):
    """The doubles PMT's survivors give for one formula: all of them, then the 64-bit kind, then the exact payment rounded once (the engine's model)."""
    name, args, _ = parse_call(formula)
    if name != "PMT":
        raise SystemExit("--spread takes a PMT formula")
    args = annuity_args(args)
    kept = set(labels)
    runs = [(label, run) for label, run in annuity_candidates("PMT", pmt_forms()) if label in kept]
    rate, nper, present, future, kind = args
    growth = (1 + rate) ** int(nper)
    exact = float(-(rate * (present * growth + future)) / (growth - 1))
    if kind != 0:
        exact = float(Q(exact) / (1 + rate))
    print(f"\n{formula}: the exact payment rounded once (the engine's model) {exact!r}")
    for title, chosen in (("every survivor", runs), ("at 64 bits, raised by no excess form", [(l, r) for l, r in runs if "@64 | rest@64" in l and "excess" not in l])):
        if not chosen:
            print(f"  {title}: none")
            continue
        counts = {}
        for _, run in chosen:
            value = run(args)
            counts[value] = counts.get(value, 0) + 1
        values = sorted(counts)
        print(f"  {title}: {len(chosen)} give {len(values)} doubles, {abs(ulps(values[0], values[-1]))} units in the last place apart")
        for value in values:
            print(f"      {value!r}: {counts[value]}" + ("  (the engine's)" if value == exact else ""))


def print_discriminators(survivors_of):
    grids = {
        "FV": [(r, n, a, b, t) for r in (0.05 / 12, 0.0725 / 12, 0.045, 0.1) for n in (12, 37, 60, 360) for a in (-150.0, -2500.0) for b in (0.0, -1000.0) for t in (1,)],
        "PV": [(r, n, a, b, t) for r in (0.05 / 12, 0.0725 / 12, 0.045, 0.1) for n in (12, 37, 60, 360) for a in (-150.0, 2500.0) for b in (0.0, 1000.0) for t in (0, 1)],
        "PMT": [(r, n, a, b, t) for r in (0.05 / 12, 0.0725 / 12, 0.045 / 12, 0.03, 0.1) for n in (12, 36, 60, 360) for a in (10000.0, 250000.0) for b in (0.0, -5000.0) for t in (0, 1)],
    }
    forms = {"FV": fv_forms(), "PV": pv_forms(), "PMT": pmt_forms()}
    for name in ("FV", "PV", "PMT"):
        labels = set(survivors_of[name])
        survivors = [(label, run) for label, run in annuity_candidates(name, forms[name]) if label in labels]
        if name == "PMT":  # one form per family: the families, not the forms, are in question
            seen, kept = set(), []
            for label, run in survivors:
                family = label.split(" | ", 1)[1]
                if family not in seen:
                    seen.add(family)
                    kept.append((label, run))
            survivors = kept
        chosen, groups = discriminate(name, survivors, grids[name], 6)
        print(f"\n{name}: {len(survivors)} surviving candidates; {len(chosen)} inputs leave {len(groups)} groups")
        for args, split in chosen:
            print(f"  ={name}({','.join(repr(a) for a in args)})")
            for value, members in sorted(split.items()):
                print(f"      {value!r}: {len(members)}  e.g. {members[0]}")


if __name__ == "__main__":
    main()
