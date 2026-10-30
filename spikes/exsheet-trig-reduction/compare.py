#!/usr/bin/env python3
"""Acceptance experiment: which computation reproduces every trigonometric double Excel returned?

Not shipped. Reads every oracle results file under dotnet/tests/ExSheet.Engine.Tests/ExcelOracle/, takes
each number Excel answered for SIN, COS, TAN, COT, CSC and SEC (the agreeing cases too, so a fix
breaks none), and counts, for each candidate computation, how many of them it reproduces bit for
bit. Exact arithmetic is Python's Fraction; sine and cosine of a reduced argument are summed to
120 decimal digits and rounded once to a double, so no candidate here depends on a platform's libm
except the ones that say "libm".

Run with Python 3, standard library only:

    python3 spikes/exsheet-trig-reduction/compare.py
"""
from __future__ import annotations

import decimal
import json
import math
import platform
import re
import sys
from fractions import Fraction
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
RESULTS = ROOT / "dotnet/tests/ExSheet.Engine.Tests/ExcelOracle"

decimal.getcontext().prec = 120
decimal.getcontext().traps[decimal.DivisionByZero] = False  # 1/0 is an infinity, as a double's
D = decimal.Decimal

# pi to 120 digits, for the "true pi" candidates.
PI_TRUE = D("3.14159265358979323846264338327950288419716939937510582097494459230781640628620899"
            "8628034825342117067982148086513282306647")

# The double PI() gives (Math.PI), and pi rounded to the x87's 64-bit significand (FLDPI).
PI_DOUBLE = Fraction(math.pi)
PI_X87 = Fraction(0xC90FDAA22168C235, 2**62)
# The 66-bit pi FSIN's own reduction is documented to use (Intel; Dawson 2014): 0xC90FDAA22168C234C / 2^66.
PI_FSIN = Fraction(0xC90FDAA22168C234C, 2**66)


def to_decimal(q: Fraction) -> D:
    return D(q.numerator) / D(q.denominator)


def dsin(r: D) -> D:
    """sin(r) by its series, |r| small (the reduced argument)."""
    term, total, n = r, r, 1
    r2 = r * r
    while True:
        term = -term * r2 / ((2 * n) * (2 * n + 1))
        if abs(term) < D(10) ** -118:
            return total
        total += term
        n += 1


def dcos(r: D) -> D:
    term, total, n = D(1), D(1), 1
    r2 = r * r
    while True:
        term = -term * r2 / ((2 * n - 1) * (2 * n))
        if abs(term) < D(10) ** -118:
            return total
        total += term
        n += 1


def dbl(v) -> float:
    """Round once to the nearest double (Python's float() of a decimal string is correctly rounded)."""
    if isinstance(v, Fraction):
        return v.numerator / v.denominator  # correctly rounded true division of ints
    return float(v)


def round_bits(q: Fraction, bits: int) -> Fraction:
    """q rounded to a significand of `bits` bits, ties to even (the x87's extended format at 64)."""
    if q == 0:
        return q
    sign = -1 if q < 0 else 1
    a = abs(q)
    e = a.numerator.bit_length() - a.denominator.bit_length()
    if Fraction(2) ** e > a:
        e -= 1
    scale = Fraction(2) ** (bits - 1 - e)
    m = a * scale
    f = m.numerator // m.denominator
    rem = m - f
    if rem > Fraction(1, 2) or (rem == Fraction(1, 2) and f % 2 == 1):
        f += 1
    return sign * f / scale


# ---- reduction -------------------------------------------------------------------------------------

def reduce_exact(x: float, pi: Fraction):
    """k = nearest whole number of x / (pi/2); r = x - k*(pi/2), exactly."""
    q = Fraction(x)
    half = pi / 2
    k = round(q / half)
    return k, q - k * half


def reduce_x87(x: float, pi: Fraction):
    """The same, with each step rounded to the x87's 64-bit significand: k*(pi/2), then x minus it."""
    q = Fraction(x)
    half = pi / 2
    k = round(q / half)
    return k, round_bits(q - round_bits(k * half, 64), 64)


def reduce_double(x: float, pi_hi: float):
    """Reduction with a double constant and double arithmetic (FMA-free), as a naive port would."""
    half = pi_hi / 2
    k = round(x / half)
    return k, Fraction(x - k * half)


def quadrant(k: int, s: D, c: D):
    """sin and cos of x from those of the reduced r, x = r + k*(pi/2)."""
    return [(s, c), (c, -s), (-s, -c), (-c, s)][k % 4]


# ---- candidates --------------------------------------------------------------------------------------
# Each candidate maps (function name, x) to a double, or None where it gives no number.

def exact_true(func, x):
    """True mathematics: the function of the double x, correctly rounded."""
    q = to_decimal(Fraction(x))
    k = int((q / (PI_TRUE / 2)).to_integral_value(rounding=decimal.ROUND_HALF_EVEN))
    r = q - k * PI_TRUE / 2
    return finish(func, k, r, "correct")


def make_reduced(pi: Fraction, reducer, finishing: str):
    def candidate(func, x):
        k, r = reducer(x, pi)
        return finish(func, k, to_decimal(r), finishing, r)
    return candidate


def finish(func, k, r: D, finishing: str, r_exact: Fraction | None = None):
    """
    finishing:
      "correct"  sin/cos of the reduced r to 120 digits; each function rounded once from them
                 (TAN = sin/cos, COT = cos/sin, CSC = 1/sin, SEC = 1/cos, all in decimal);
      "dsincos"  SIN and COS rounded to doubles first, then TAN/COT/CSC/SEC from those doubles
                 in double arithmetic (sin/cos, cos/sin, 1/sin, 1/cos);
      "tanr"     as "correct" for SIN and COS; TAN from tan(r) rounded to a double (and -1/that in
                 the odd quadrants, in double), COT = 1/TAN in double, CSC = 1/SIN, SEC = 1/COS in
                 double;
      "rdouble"  r rounded to a double, then libm's sin and cos of it (Python's math, glibc here),
                 the rest as "dsincos";
      "rdd"      r as a double-double (hi + lo): sin = sin(hi) + cos(hi)*lo, cos = cos(hi) - sin(hi)*lo
                 with libm, in double arithmetic, the rest as "dsincos".
    """
    if finishing in ("rdouble", "rdd"):
        hi = float(r_exact if r_exact is not None else r)
        lo = float((r_exact if r_exact is not None else Fraction(str(r))) - Fraction(hi)) if finishing == "rdd" else 0.0
        sh, ch = math.sin(hi), math.cos(hi)
        s_r, c_r = (sh + ch * lo, ch - sh * lo) if finishing == "rdd" else (sh, ch)
        s, c = [(s_r, c_r), (c_r, -s_r), (-s_r, -c_r), (-c_r, s_r)][k % 4]
        return from_doubles(func, s, c)
    s, c = quadrant(k, dsin(r), dcos(r))
    if finishing == "correct":
        return {"SIN": lambda: dbl(s), "COS": lambda: dbl(c), "TAN": lambda: dbl(s / c),
                "COT": lambda: dbl(c / s), "CSC": lambda: dbl(1 / s), "SEC": lambda: dbl(1 / c)}[func]()
    if finishing == "dsincos":
        return from_doubles(func, dbl(s), dbl(c))
    if finishing == "tanr":
        sr, cr = dsin(r), dcos(r)
        t = dbl(sr / cr)
        tan = t if k % 2 == 0 else -div(1, t)
        sd, cd = dbl(s), dbl(c)
        return {"SIN": sd, "COS": cd, "TAN": tan, "COT": div(1, tan), "CSC": div(1, sd), "SEC": div(1, cd)}[func]
    raise ValueError(finishing)


def div(a: float, b: float) -> float:
    """IEEE division: a zero divisor gives an infinity (the functions refuse it before it matters)."""
    return a / b if b != 0 else math.copysign(math.inf, a) * math.copysign(1.0, b)


def from_doubles(func, s: float, c: float):
    return {"SIN": lambda: s, "COS": lambda: c, "TAN": lambda: div(s, c), "COT": lambda: div(c, s),
            "CSC": lambda: div(1, s), "SEC": lambda: div(1, c)}[func]()


def libm_direct(func, x):
    """What the engine did before: libm of the double x (Python's math is glibc here, as .NET on Linux)."""
    return {"SIN": lambda: math.sin(x), "COS": lambda: math.cos(x), "TAN": lambda: math.tan(x),
            "COT": lambda: div(1, math.tan(x)), "CSC": lambda: div(1, math.sin(x)),
            "SEC": lambda: div(1, math.cos(x))}[func]()


CANDIDATES = {
    "libm of x (the engine before)": libm_direct,
    "true pi, correctly rounded": exact_true,
    "x87 pi, exact reduction, correctly rounded": make_reduced(PI_X87, reduce_exact, "correct"),
    "x87 pi, exact reduction, SIN/COS doubles then double arithmetic": make_reduced(PI_X87, reduce_exact, "dsincos"),
    "x87 pi, exact reduction, TAN from tan(r)": make_reduced(PI_X87, reduce_exact, "tanr"),
    "x87 pi, exact reduction, r a double, libm": make_reduced(PI_X87, reduce_exact, "rdouble"),
    "x87 pi, exact reduction, r a double-double, libm": make_reduced(PI_X87, reduce_exact, "rdd"),
    "x87 pi, extended-precision reduction, correctly rounded": make_reduced(PI_X87, reduce_x87, "correct"),
    "x87 pi, extended-precision reduction, SIN/COS doubles": make_reduced(PI_X87, reduce_x87, "dsincos"),
    "FSIN's 66-bit pi, exact reduction, correctly rounded": make_reduced(PI_FSIN, reduce_exact, "correct"),
    "x87 pi, exact reduction, SIN/COS/TAN correctly rounded, COT/CSC/SEC 1 over them (the engine now)":
        lambda f, x: chosen(f, x),
    "the engine's double-double computation, ported": lambda f, x: engine(f, x),
    "double PI(), exact reduction, correctly rounded": make_reduced(PI_DOUBLE, reduce_exact, "correct"),
    "double PI(), double arithmetic, r a double, libm": lambda f, x: (lambda k, r: from_doubles(
        f, *[(math.sin(float(r)), math.cos(float(r))), (math.cos(float(r)), -math.sin(float(r))),
             (-math.sin(float(r)), -math.cos(float(r))), (-math.cos(float(r)), math.sin(float(r)))][k % 4]))(
        *reduce_double(x, math.pi)),
}


# ---- the observations ----------------------------------------------------------------------------------

FUNCS = ("SIN", "COS", "TAN", "COT", "CSC", "SEC")


def argument(text: str) -> float:
    """The few argument spellings the corpus uses, in Excel's double arithmetic left to right."""
    t = text.replace("PI()", repr(math.pi))
    t = re.sub(r"RADIANS\(([^()]*)\)", r"(\1)*" + repr(math.pi) + "/180", t)
    t = t.replace("^", "**")
    if not re.fullmatch(r"[0-9.eE+\-*/() ]+", t):
        raise ValueError(f"not an argument this experiment reads: {text}")
    return float(eval(t))  # numbers and operators only, checked above


def observations():
    seen = {}
    for path in sorted(RESULTS.glob("results-*.json")):
        data = json.loads(path.read_text(encoding="utf-8-sig"))
        for case in data.get("cases", []):
            excel = case.get("excel") or {}
            formula = excel.get("formula") or ""
            m = re.fullmatch(r"=(SIN|COS|TAN|COT|CSC|SEC)\((.*)\)", formula)
            if not m or excel.get("kind") != "number":
                continue
            func, arg = m.groups()
            expected = float(excel["number"])
            if expected != excel["value2"]:
                raise ValueError(f"{case['id']}: the round-trip spelling and value2 differ")
            key = (case["id"], formula)
            if key in seen and seen[key][3] != expected:
                raise ValueError(f"{case['id']}: two different Excel answers")
            seen[key] = (func, argument(arg), path.name, expected)
    return [(cid, formula, *rest) for (cid, formula), rest in sorted(seen.items())]


# ---- how TAN, COT, CSC and SEC are made from the reduction ---------------------------------------------
# With the reduction fixed (x87 pi, exact), the ways the four can be built from sine and cosine.

def x87_exact(x):
    k, r = reduce_exact(x, PI_X87)
    rd = to_decimal(r)
    sr, cr = dsin(rd), dcos(rd)
    s, c = quadrant(k, sr, cr)
    return k, sr, cr, s, c


def tan_variants(x):
    k, sr, cr, s, c = x87_exact(x)
    t = dbl(sr / cr)
    return {
        "tan(x) correctly rounded": dbl(s / c),
        "SIN/COS, each a double, divided": div(dbl(s), dbl(c)),
        "tan(r) a double, -1/that in odd quadrants": t if k % 2 == 0 else -div(1, t),
    }


def cot_variants(x):
    k, sr, cr, s, c = x87_exact(x)
    out = {f"1/TAN, TAN as {name}": div(1, value) for name, value in tan_variants(x).items()}
    out["cot(x) correctly rounded"] = dbl(c / s)
    out["COS/SIN, each a double, divided"] = div(dbl(c), dbl(s))
    return out


def csc_variants(x):
    k, sr, cr, s, c = x87_exact(x)
    return {"1/SIN in double": div(1, dbl(s)), "csc(x) correctly rounded": dbl(1 / s)}


def sec_variants(x):
    k, sr, cr, s, c = x87_exact(x)
    return {"1/COS in double": div(1, dbl(c)), "sec(x) correctly rounded": dbl(1 / c)}


VARIANTS = {"TAN": tan_variants, "COT": cot_variants, "CSC": csc_variants, "SEC": sec_variants}


# ---- the engine's computation, ported line for line from Functions.Trigonometry.cs ----------------------
# Double arithmetic and math.fma are IEEE binary64, as C#'s are, so the port gives the C# doubles.

HALF_PI_HIGH = math.pi / 2
HALF_PI_LOW = math.ldexp(565.0, -63)
assert Fraction(HALF_PI_HIGH) + Fraction(HALF_PI_LOW) == PI_X87 / 2


def two_sum(a, b):
    s = a + b
    bb = s - a
    return s, (a - (s - bb)) + (b - bb)


def quick_two_sum(a, b):
    s = a + b
    return s, b - (s - a)


def dd_add(a, b):
    s1, s2 = two_sum(a[0], b[0])
    t1, t2 = two_sum(a[1], b[1])
    s2 += t1
    s1, s2 = quick_two_sum(s1, s2)
    s2 += t2
    return quick_two_sum(s1, s2)


def dd_mul(a, b):
    p1 = a[0] * b[0]
    p2 = math.fma(a[0], b[0], -p1)
    p2 += a[0] * b[1] + a[1] * b[0]
    return quick_two_sum(p1, p2)


def dd_div_double(a, b):
    q1 = a[0] / b
    p1 = q1 * b
    p2 = math.fma(q1, b, -p1)
    s, e = two_sum(a[0], -p1)
    e -= p2
    e += a[1]
    q2 = (s + e) / b
    return quick_two_sum(q1, q2)


def dd_mul_double(a, b):
    p1 = a[0] * b
    p2 = math.fma(a[0], b, -p1)
    p2 += a[1] * b
    return quick_two_sum(p1, p2)


def dd_div(a, b):
    q1 = a[0] / b[0]
    r = dd_add(a, dd_neg(dd_mul_double(b, q1)))
    q2 = r[0] / b[0]
    r = dd_add(r, dd_neg(dd_mul_double(b, q2)))
    q3 = r[0] / b[0]
    q = quick_two_sum(q1, q2)
    return dd_add(q, (q3, 0.0))


def dd_neg(a):
    return (-a[0], -a[1])


SERIES_TERMS = 14


def series(r2, odd):
    inner = (1.0, 0.0)
    for n in range(SERIES_TERMS, 0, -1):
        d = float((2 * n) * (2 * n + 1)) if odd else float((2 * n - 1) * (2 * n))
        inner = dd_add((1.0, 0.0), dd_neg(dd_div_double(dd_mul(inner, r2), d)))
    return inner


def engine_sine_cosine(x):
    k = float(round(x / HALF_PI_HIGH))  # Math.Round: to even at a tie, as Python's round
    p = k * HALF_PI_HIGH
    p_rest = math.fma(k, HALF_PI_HIGH, -p)
    t = x - p - p_rest
    r = two_sum(t, -(k * HALF_PI_LOW))
    r2 = dd_mul(r, r)
    sin = dd_mul(r, series(r2, True))
    cos = series(r2, False)
    q = int(k) & 3
    return [(sin, cos), (cos, dd_neg(sin)), (dd_neg(sin), dd_neg(cos)), (dd_neg(cos), sin)][q]


def engine(func, x):
    s, c = engine_sine_cosine(x)
    sin, cos = s[0] + s[1], c[0] + c[1]
    tan = (lambda q: q[0] + q[1])(dd_div(s, c)) if c[0] != 0 else math.inf
    return {"SIN": sin, "COS": cos, "TAN": tan, "COT": div(1, tan), "CSC": div(1, sin), "SEC": div(1, cos)}[func]


def chosen(func, x):
    """What the engine is meant to compute: SIN, COS, TAN correctly rounded from the x87-pi reduction; 1/those in double."""
    k, sr, cr, s, c = x87_exact(x)
    sd, cd, td = dbl(s), dbl(c), dbl(s / c)
    return {"SIN": sd, "COS": cd, "TAN": td, "COT": div(1, td), "CSC": div(1, sd), "SEC": div(1, cd)}[func]


def random_arguments(count, seed=20261011):
    """Arguments over every scale the functions answer, seeded so a run is reproducible."""
    import random
    rng = random.Random(seed)
    out = []
    for i in range(count):
        kind = i % 4
        if kind == 0:
            x = rng.uniform(-4, 4)
        elif kind == 1:
            x = math.copysign(math.exp(rng.uniform(math.log(1e-12), math.log(2**27 - 1))), rng.uniform(-1, 1))
        elif kind == 2:  # near a multiple of pi/2, where the reduction matters most
            m = rng.randint(-85_000_000, 85_000_000)
            x = float(m) * (math.pi / 2) + rng.uniform(-1e-6, 1e-6)
        else:  # whole numbers and short decimals, as a sheet holds
            x = round(rng.uniform(-1e6, 1e6), rng.randint(0, 3))
        if abs(x) < 2**27:
            out.append(x)
    return out


def discriminating(variants, limit=8):
    """Short arguments a Windows run can type, where the variants that reproduce every observation part."""
    found = []
    for x in [float(n) for n in range(1, 400)] + [n / 10 for n in range(1, 100)]:
        values = variants(x)
        if len(set(values.values())) > 1:
            found.append((x, values))
        if len(found) >= limit:
            break
    return found


# Arguments as a Formula types them (15 significant digits at most, as Excel keeps), for the layer-1
# test's known answers: each function's value from the exact computation, never from the port.
KNOWN_ARGUMENTS = [
    "PI()", "PI()/2", "PI()/4", "2*PI()", "3*PI()/2", "-PI()", "-PI()/2",
    "0.785", "1.047", "1", "-1", "0.5", "-2.5", "9", "30", "45", "1E-8", "1E-300",
    "3.14159265358979", "1.5707963267949", "7.85398163397448", "355", "-710", "103993",
    "12345678.9", "100000000", "-100000000", "134217727", "-134217727", "134217727.5",
]


def known_answers():
    rows = []
    for text in KNOWN_ARGUMENTS:
        x = argument(text)
        for func in FUNCS:
            rows.append((f"={func}({text})", chosen(func, x), engine(func, x)))
    return rows


def main():
    if len(sys.argv) > 1 and sys.argv[1] == "--known":
        for formula, value, ported in known_answers():
            assert value == ported, formula
            print(f'        ["{formula}", {value!r}],')
        return
    random_count = int(sys.argv[1]) if len(sys.argv) > 1 else 20000
    cases = observations()
    print(f"Python {platform.python_version()} on {platform.platform()}")
    print(f"{len(cases)} numbers Excel returned for SIN, COS, TAN, COT, CSC and SEC\n")
    for cid, formula, func, x, source, expected in cases:
        print(f"  {cid:8} {formula:24} x = {x!r:24} Excel {expected!r}")

    print("\n## Whole computations: how many of Excel's numbers each reproduces bit for bit\n")
    for name, candidate in CANDIDATES.items():
        misses = [(cid, formula, expected, candidate(func, x))
                  for cid, formula, func, x, source, expected in cases if candidate(func, x) != expected]
        print(f"{len(cases) - len(misses):2}/{len(cases)}  {name}")
        for cid, formula, expected, got in misses:
            print(f"        {cid:8} {formula:24} Excel {expected!r:24} candidate {got!r}")

    print("\n## TAN, COT, CSC and SEC from the x87-pi reduction: each way of building them\n")
    for func, variants in VARIANTS.items():
        mine = [(cid, x, expected) for cid, formula, f, x, source, expected in cases if f == func]
        tallies = {}
        for cid, x, expected in mine:
            for name, value in variants(x).items():
                tallies.setdefault(name, []).append(cid if value != expected else None)
        for name, misses in tallies.items():
            missed = [m for m in misses if m]
            print(f"  {func} {len(mine) - len(missed)}/{len(mine)}  {name}" + (f"   misses {', '.join(missed)}" if missed else ""))

    print("\n## The engine's double-double computation (ported) against the exact one it is meant to round\n")
    engine_misses = [(cid, expected, engine(func, x)) for cid, formula, func, x, source, expected in cases
                     if engine(func, x) != expected]
    print(f"  the observations: {len(cases) - len(engine_misses)}/{len(cases)}" +
          "".join(f"\n      {cid} Excel {e!r} engine {g!r}" for cid, e, g in engine_misses))
    arguments = random_arguments(random_count)
    differ = {}
    for x in arguments:
        for func in FUNCS:
            if func in ("COT", "CSC") and x == 0:
                continue
            a, b = engine(func, x), chosen(func, x)
            if a != b:
                differ.setdefault(func, []).append((x, a, b))
    print(f"  {len(arguments)} seeded arguments, every function: "
          + ", ".join(f"{func} {len(arguments) - len(differ.get(func, []))}/{len(arguments)}" for func in FUNCS))
    for func, rows in differ.items():
        for x, a, b in rows[:5]:
            print(f"      {func}({x!r}) engine {a!r} exact {b!r}")

    print("\n## Arguments that would tell apart the ways that reproduce every observation\n")
    for func, variants in VARIANTS.items():
        def agreeing(x, variants=variants, func=func):
            observed = [(cid, xx, e) for cid, formula, f, xx, source, e in cases if f == func]
            keep = [name for name in variants(observed[0][1])
                    if all(variants(xx)[name] == e for cid, xx, e in observed)]
            return {name: value for name, value in variants(x).items() if name in keep}
        for x, values in discriminating(agreeing, limit=4):
            print(f"  ={func}({x!r})")
            for name, value in values.items():
                print(f"      {value!r:24} {name}")


if __name__ == "__main__":
    main()
