"""PMT against Python's decimal at 1200 digits (2026-10-12, the third and fourth rounds of the financial area).

ADR-0047's exception for PMT (decided with the user, 2026-10-12) lets PMT give the exact payment over its
arguments' doubles, rounded once, where Excel's double differs from it in its last digits. This checks the
engine's payment, and that it refuses where the engine's rules say it refuses, over a dense random set of
inputs: tiny, subnormal and negative rates, rates near and below -1, whole, fractional, negative, tiny and vast
numbers of periods, and present and future values from 0 to the edges of a double's range.

    python3 -B spikes/exsheet-financial-evaluation/pmt_exact.py --generate 20000 > pmt.txt
    nix develop -c dotnet run -c Release spikes/exsheet-financial-evaluation/Search.cs -- --engine pmt.txt engine.txt
    python3 -B spikes/exsheet-financial-evaluation/pmt_exact.py --check pmt.txt engine.txt

Everything here is computed independently of the engine's code: the payment in decimal at 1200 digits (1 + rate
is exact at that precision for every rate the engine does not refuse first, and (1+rate)^n - 1 keeps some 900
digits of its own), and the refusals from the rules in Functions.Financial.cs and ExactPayment.cs as their
comments state them, re-implemented here over exact values rather than the engine's enclosures:

- a rate other than 0 and an nper past 2^53: #VALUE!; nper of 0: #NUM!, but #VALUE! at the start at a rate above
  -1 other than 0; a rate of 0: -(pv+fv)/nper in doubles;
- a rate of -1 or below: #NUM! below -1 where nper is a fraction whose reciprocal is not an odd whole number,
  else #VALUE!;
- otherwise #VALUE! where a listed quantity (QUANTITIES, the engine's list) is not 0 and lies outside
  [2^-1021, 2^1022], where the growth lies below 2^-52, and where a reading magnifies its roundings more than
  2^6 times (magnified(), ExactPayment.Conditioned, the fourth round);
- elsewhere the exact payment rounded once, at the end of each period and at the start (2026-10-13, decided
  with the user: at the start too). --check also counts how many payments at the start the earlier rule, the
  payment at the end's double divided by 1+rate exactly and rounded again, put a unit in the last place from
  the exact payment at the start rounded once.

Nothing here ships.
"""

import decimal
import math
import multiprocessing
import random
import struct
import sys
from decimal import Decimal as D

PRECISION = 1200
LEAST = None
MOST = None

QUANTITIES = [
    "r", "pv", "fv", "s", "g", "/ g", "e", "b", "/ b", "c", "/ c",
    "e / r", "r / e", "e / g", "e / g r", "c e", "c e / r", "c e / g", "c e / g r", "c / r", "r / c",
    "b g", "b e", "r g",
    "pv g", "fv / g", "pv r", "fv r", "pv r g", "fv r / g",
    "pv g / e", "fv / e", "r g / e", "pv r g / e", "fv r / e",
    "s r", "s / e", "s r / e",
    "pv r g / c e", "fv r / c e",
]
AT_START = ["pv r r g / e", "fv r r / e", "pv r r g / b e", "fv r r / b e"]


def context():
    global LEAST, MOST
    decimal.setcontext(decimal.Context(prec=PRECISION, Emax=10**7, Emin=-10**7))
    LEAST = D(2) ** -1021
    MOST = D(2) ** 1022


def powers(quantity):
    sign, result = 1, {}
    for name in quantity.split():
        if name == "/":
            sign = -1
        else:
            result[name] = result.get(name, 0) + sign
    return result


def show(x):
    return repr(float(x)).replace("e", "E")


def typed(x):
    """The double a Formula holds for x: the engine, as Excel, keeps 15 significant digits of a typed number."""
    return float("%.15g" % x)


def formula(rate, periods, present, future, start):
    return f"=PMT({show(typed(rate))},{show(typed(periods))},{show(typed(present))},{show(typed(future))},{1 if start else 0})"


def generate(count, seed):
    rnd = random.Random(seed)

    def log_uniform(low, high):
        return 10 ** rnd.uniform(low, high)

    def rate():
        k = rnd.random()
        if k < 0.30:
            return log_uniform(-6, 1) if rnd.random() < 0.8 else -log_uniform(-6, -0.01)  # ordinary rates
        if k < 0.45:
            return log_uniform(-30, -6) * rnd.choice([1, -1])  # tiny, where 1+rate in a double loses the rate
        if k < 0.55:
            return log_uniform(-307.5, -30) * rnd.choice([1, -1])  # tinier, to the normal numbers' edge
        if k < 0.60:
            return rnd.uniform(4.9e-324, 2.3e-308) * rnd.choice([1, -1])  # subnormal
        if k < 0.68:
            return -(1 - log_uniform(-16, -0.5))  # just above -1
        if k < 0.73:
            return rnd.choice([-1.0, -2.0, -1.5, -9.0, -1.000001, -3.7])
        if k < 0.80:
            return log_uniform(0, 12)  # large
        return rnd.choice([0.05 / 12, 0.08 / 12, 0.1, 0.0365 / 365, 0.42, 1e-7, 0.07 / 12])

    def periods():
        k = rnd.random()
        if k < 0.30:
            n = float(rnd.randint(1, 480))
        elif k < 0.40:
            n = float(int(log_uniform(0, 15.9)))  # whole, up to about 2^53
        elif k < 0.70:
            n = rnd.uniform(0, 500)  # fractional
        elif k < 0.80:
            n = rnd.choice([0.5, 1 / 3, 0.25, 10.5, 2.5, 36.25, 1 / 5, 283.5]) + rnd.choice([0, 0, 1, 12])
        elif k < 0.88:
            n = log_uniform(-18, 0)  # a small fraction, to where nper+1 rounds to 1
        else:
            n = rnd.choice([0.0, 2.0**53 + 2, 1e17, 1020.0, 1100.0, 1740.0, 160.0])
        return -n if rnd.random() < 0.2 else n

    def amount():
        k = rnd.random()
        if k < 0.2:
            return 0.0
        if k < 0.85:
            return log_uniform(-5, 9) * rnd.choice([1, -1])
        return log_uniform(-320, 307.99) * rnd.choice([1, -1])  # the engine, as Excel, takes no typed number past 9.99999999999999E+307

    named = [
        (1e-320, 0.5, 1000.0, 0.0, False), (1e-315, 10.5, 1000.0, 0.0, False),
        (5.1699029180828e-320, 0.0907921350606893, -1000.0, 20000.0, False),
        (-1.64435447903749e-307, 283.5, 1e-05, 0.0, False),
        (1.0, 1020.0, 1000.0, 0.0, False), (0.5, 1740.0, 1000.0, 0.0, False), (1e10, 30.0, 1.0, 0.0, False),
        (1.0, 1100.0, 1000.0, 0.0, False), (-0.99, 160.0, 1000.0, 500.0, False),
        (1e-20, 12.0, 1000.0, 0.0, False), (1e-200, 10.5, 1000.0, -500.0, True), (-3e-17, 360.0, 250000.0, 0.0, True),
        (1.0, 1.0, 1000.0, -2000.0, False),
        # The fourth round: a tiny nper at the start, and pv g nearly cancelling fv.
        (0.1, 1e-17, 1000.0, 0.0, True), (0.1, -5e-17, 1000.0, 0.0, True), (-0.999999, 1e-12, 1000.0, 0.0, True),
        (0.1, 2.0, 1000.0, -1210.0, False), (0.1, 1e-17, 1000.0, -1000.0, False), (-0.5, 36.25, -150.0, 0.0, False),
        (0.1, 10.0, 1000.0, 0.0, True), (0.0725 / 12, 12.0, 250000.0, 0.0, True),
    ]
    for case in named:
        print(formula(*case))
    for _ in range(count - len(named)):
        print(formula(rate(), periods(), amount(), amount(), rnd.random() < 0.4))


def parse(line):
    args = line[len("=PMT("):-1].split(",")
    return float(args[0]), float(args[1]), float(args[2]), float(args[3]), args[4] == "1"


def expected(line):
    context()
    rate, periods, present, future, start = parse(line)
    if rate != 0 and abs(periods) > 2.0**53:
        return "#VALUE!"
    if periods == 0:
        return "#VALUE!" if start and rate > -1 and rate != 0 else "#NUM!"
    if rate == 0:
        return show(-(present + future) / periods)
    if rate <= -1:
        if rate < -1 and periods != math.floor(periods):
            root = 1 / abs(periods)
            if not (math.isfinite(root) and root == math.floor(root) and abs(math.fmod(root, 2)) == 1):
                return "#NUM!"
        return "#VALUE!"
    r, pv, fv = D(rate), D(present), D(future)
    b = 1 + r
    s = pv + fv
    for value in (r, pv, fv, s):
        if value != 0 and not (LEAST <= abs(value) <= MOST):
            return "#VALUE!"
    if not abs(periods * math.log2(1 + rate)) <= 1100:
        return "#VALUE!"
    if periods == math.floor(periods):
        g = b ** int(periods) if periods > 0 else 1 / (b ** int(-periods))
    else:
        g = (b.ln() * D(periods)).exp()
    e = g - 1
    if g < D(2) ** -52:
        return "#VALUE!"
    values = {"r": r, "pv": pv, "fv": fv, "s": s, "g": g, "e": e, "b": b, "c": b if start else D(1)}
    for quantity in QUANTITIES + (AT_START if start else []):
        p = powers(quantity)
        if any(values[name] == 0 for name in p):
            continue
        magnitude = D(1)
        for name, power in p.items():
            magnitude *= abs(values[name]) ** power
        if not (LEAST <= magnitude <= MOST):
            return "#VALUE!"
    if magnified(r, D(periods), pv, fv, b, g, e, start):
        return "#VALUE!"
    payment = float(-r * (pv * g + fv) / e / b if start else -r * (pv * g + fv) / e)
    return "0" if payment == 0 else show(payment)


def magnified(r, n, pv, fv, b, g, e, start):
    """ExactPayment.Conditioned's rule: whether some factor by which a reading magnifies its roundings exceeds 2^6."""
    limit = D(64)
    owes_nothing = pv == 0 and fv == 0  # every reading adds only zeros, but LibreOffice's divisor at the start
    owed = pv * g + fv
    if not owes_nothing and owed == 0:
        return True
    log = b.ln()
    x = abs(n * log)
    s = pv + fv
    if not owes_nothing:
        if abs(pv) * (1 + g) + abs(fv) > limit * abs(owed):
            return True
        if x * abs(s) * g > limit * abs(e) * abs(owed):
            return True
        if start and abs(b) + abs(r) > limit:
            return True
        if start and x * abs(pv) * g > limit * abs(owed):
            return True
    return start and abs((n + 1) * log) * b * g + abs(r) > limit * b * abs(e)


def exact_at_start(line):
    """At the start, the exact payment at the start rounded once, beside the earlier rule; None elsewhere."""
    rate, periods, present, future, start = parse(line)
    if not start:
        return None
    want = expected(line)
    if want.startswith("#") or want == "0":
        return None
    context()
    r, pv, fv = D(rate), D(present), D(future)
    b = 1 + r
    if periods == math.floor(periods):
        g = b ** int(periods) if periods > 0 else 1 / (b ** int(-periods))
    else:
        g = (b.ln() * D(periods)).exp()
    return want, show(float(D(float(-r * (pv * g + fv) / (g - 1))) / b))


def units(a, b):
    """How many doubles lie from a to b."""
    def ordered(x):
        bits = struct.unpack("<q", struct.pack("<d", x))[0]
        return bits if bits >= 0 else -(bits & 0x7FFFFFFFFFFFFFFF)
    return ordered(b) - ordered(a)


def check(formulas_path, engine_path):
    formulas = [l.strip() for l in open(formulas_path) if l.strip()]
    answers = [l.strip() for l in open(engine_path) if l.strip()]
    assert len(formulas) == len(answers), (len(formulas), len(answers))
    with multiprocessing.Pool() as pool:
        wanted = pool.map(expected, formulas, chunksize=20)
    counts = {}
    failures = []
    for line, want, got in zip(formulas, wanted, answers):
        same = want == got if want.startswith("#") or got.startswith("#") else float(want) == float(got)
        kind = "refused (#VALUE!)" if want == "#VALUE!" else "#NUM!" if want == "#NUM!" else "a number"
        counts[kind] = counts.get(kind, 0) + 1
        if not same:
            failures.append(f"{line}: the engine {got}, decimal {want}")
    print(f"{len(formulas)} inputs: " + ", ".join(f"{v} {k}" for k, v in sorted(counts.items())))
    with multiprocessing.Pool() as pool:
        starts = [x for x in pool.map(exact_at_start, formulas, chunksize=20) if x is not None]
    apart = sum(1 for once, rule in starts if float(once) != float(rule))
    most = max((abs(units(float(once), float(rule))) for once, rule in starts), default=0)
    print(f"  at the start, {len(starts)} payments other than 0: the earlier rule (the double at the end over 1+rate, rounded again) was"
          f" not the exact payment at the start rounded once at {apart} ({100 * apart / max(1, len(starts)):.0f}%), {most} unit(s) in the last place from it at most")
    rates = [parse(l)[0] for l, w in zip(formulas, wanted) if not w.startswith("#")]
    tiny = sum(1 for x in rates if 0 < abs(x) < 1e-15)
    print(f"  of the numbers, {tiny} at a rate below 1e-15 in magnitude, the smallest {min((abs(x) for x in rates if x), default=0):.3g}")
    print("  the engine gives decimal's answer at every input" if not failures else f"  {len(failures)} inputs where it does not:")
    for f in failures[:40]:
        print("    " + f)
    return not failures


if __name__ == "__main__":
    if sys.argv[1] == "--generate":
        generate(int(sys.argv[2]), int(sys.argv[3]) if len(sys.argv) > 3 else 20261012)
    elif sys.argv[1] == "--check":
        sys.exit(0 if check(sys.argv[2], sys.argv[3]) else 1)
    elif sys.argv[1] == "--one":
        print(expected(sys.argv[2]))
