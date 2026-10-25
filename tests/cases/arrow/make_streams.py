"""Writes the Arrow streams the shared Arrow cases read (ADR-0065, ADR-0370 section 3).

Each stream is written by pyarrow, a producer neither the C# reader nor the Script Core's shares code
with, and holds what its case asserts; the cases name what each one is for. A stream a producer would
never write on purpose (a dictionary index past its dictionary, text that is not UTF-8, a view that
points outside its data) is made from raw buffers, which pyarrow writes without checking. The streams
are kept in the repository, so the tests need no Python. This script is only for making them again:

    python -m venv .venv && .venv/bin/pip install pyarrow==25.0.1
    .venv/bin/python tests/cases/arrow/make_streams.py

The streams other tools wrote (Polars, DuckDB, pyarrow's delta dictionaries) are under producers/,
with the script that made them.
"""

import datetime
import gzip
import io
import pathlib
import struct

import pyarrow as pa
import pyarrow.ipc as ipc

HERE = pathlib.Path(__file__).parent / "streams"
HERE.mkdir(exist_ok=True)
written = set()


def save(name, data):
    assert name not in written, name
    written.add(name)
    (HERE / name).write_bytes(data)


def stream(*batches, schema=None, **options):
    sink = io.BytesIO()
    with ipc.new_stream(sink, schema or batches[0].schema, options=ipc.IpcWriteOptions(**options)) as writer:
        for batch in batches:
            writer.write_batch(batch)
    return sink.getvalue()


def file(*batches, **options):
    sink = io.BytesIO()
    with ipc.new_file(sink, batches[0].schema, options=ipc.IpcWriteOptions(**options)) as writer:
        for batch in batches:
            writer.write_batch(batch)
    return sink.getvalue()


def batch(*columns, metadata=None):
    """A record batch of (name, array) pairs, every field nullable."""
    schema = pa.schema([pa.field(name, array.type) for name, array in columns], metadata=metadata)
    return pa.record_batch([array for _, array in columns], schema=schema)


def validity(values):
    bits = bytearray(max(8, (len(values) + 7) // 8))
    for i, value in enumerate(values):
        if value is not None:
            bits[i >> 3] |= 1 << (i & 7)
    nulls = sum(value is None for value in values)
    return (pa.py_buffer(bytes(bits)) if nulls else None), nulls


def raw(type, form, values):
    """An array of `type` from raw values packed with struct's `form`, a null for each None, unchecked."""
    data = b"".join(struct.pack("<" + form, 0 if value is None else value) for value in values)
    bits, nulls = validity(values)
    return pa.Array.from_buffers(type, len(values), [bits, pa.py_buffer(data or bytes(8))], null_count=nulls)


def words(value, lanes):
    """An integer's two's complement words, low word first, as a decimal's value is laid out."""
    return value.to_bytes(8 * lanes, "little", signed=True)


def decimals(type, lanes, values):
    data = b"".join(words(0 if value is None else value, lanes) for value in values)
    bits, nulls = validity(values)
    return pa.Array.from_buffers(type, len(values), [bits, pa.py_buffer(data)], null_count=nulls)


def utf8_bytes(values, type=pa.string()):
    """A utf8 array of raw bytes, valid UTF-8 or not."""
    offsets = [0]
    for value in values:
        offsets.append(offsets[-1] + len(value))
    return pa.Array.from_buffers(type, len(values), [None, pa.py_buffer(struct.pack(f"<{len(offsets)}i", *offsets)), pa.py_buffer(b"".join(values))])


def inline_view(text):
    return struct.pack("<i", len(text)) + text + bytes(12 - len(text))


def buffer_view(buffers, buffer, offset, length):
    prefix = b""
    if 0 <= buffer < len(buffers) and 0 <= offset < len(buffers[buffer]):
        prefix = buffers[buffer][offset:offset + 4]
    return struct.pack("<i", length) + prefix + bytes(4 - len(prefix)) + struct.pack("<ii", buffer, offset)


def views(buffers, *items):
    """A utf8_view array made from its views, a None for each null, over `buffers`, its data buffers."""
    data = b"".join(bytes(16) if item is None else item for item in items)
    bits, nulls = validity(items)
    return pa.Array.from_buffers(pa.string_view(), len(items), [bits, pa.py_buffer(data), *map(pa.py_buffer, buffers)], null_count=nulls)


def dictionary(indices, entries):
    return pa.DictionaryArray.from_arrays(indices, entries, safe=False)


def messages(data):
    """Where each message of a stream starts and ends, up to its end-of-stream marker."""
    source = pa.BufferReader(data)
    found = []
    while True:
        start = source.tell()
        message = ipc.read_message(source) if start < len(data) - 8 else None
        if message is None:
            return found
        found.append((start, source.tell()))


# ---- text ------------------------------------------------------------------------------------

save("utf8.arrows", stream(batch(("Region", pa.array(["amer", "AMER", None, "", "amer", "Zürich"], pa.string())))))
save("large-utf8.arrows", stream(batch(("Region", pa.array(["b", None, "a", "b"], pa.large_string())))))
save("utf8-view.arrows", stream(batch(("Region", pa.array(
    ["amer", "AMER", None, "", "twelve bytes", "thirteen byte", "Zürich 東京 🚀, at length", "amer", "thirteen byte"], pa.string_view())))))

spread = [b"Emerging Markets Rates", b"--Global Credit Trading--"]
save("utf8-view-buffers.arrows", stream(batch(("Desk", views(spread,
    buffer_view(spread, 1, 2, 21), inline_view(b"FX"), None, buffer_view(spread, 0, 0, 22), buffer_view(spread, 0, 9, 13), buffer_view(spread, 1, 2, 21))))))

save("dictionary-utf8-view.arrows", stream(batch(("Region", dictionary(
    pa.array([3, 1, None, 0, 2, 4, 1, 4], pa.uint32()),
    pa.array(["emea", "AMER", None, "amer", "Asia Pacific, excluding Japan", "unused"], pa.string_view()))))))
save("dictionary-out-of-order.arrows", stream(batch(("Region", dictionary(
    pa.array([3, 1, None, 0, 2, 4, 1, 3], pa.int32()),
    pa.array(["emea", "AMER", None, "amer", "APAC", "unused"], pa.string()))))))
save("dictionary-large-utf8.arrows", stream(batch(("Region", dictionary(pa.array([1, None, 0], pa.int32()), pa.array(["x", "y"], pa.large_string()))))))

indices = [("int8", pa.int8()), ("int16", pa.int16()), ("int32", pa.int32()), ("int64", pa.int64()),
           ("uint8", pa.uint8()), ("uint16", pa.uint16()), ("uint32", pa.uint32()), ("uint64", pa.uint64())]
save("dictionary-indices.arrows", stream(batch(*[(name, dictionary(pa.array([2, 0, None, 1, 2], type), pa.array(["z", "y", "x"])))
                                                 for name, type in indices])))

shared = pa.array(["c", "b", "a"])
parts = [([0, 1], [1, 2]), ([], []), ([2, None, 0], [3, None, 5]), ([1], [6])]
save("several-batches.arrows", stream(*[batch(("Region", dictionary(pa.array(codes, pa.int32()), shared)), ("N", pa.array(numbers, pa.int64())))
                                        for codes, numbers in parts]))

# ---- numbers ---------------------------------------------------------------------------------

save("decimal128.arrows", stream(batch(
    ("Small", decimals(pa.decimal128(38, 4), 2, [15000, -1, None, 0])),
    ("Large", decimals(pa.decimal128(38, 4), 2, [792281625142643375935439503350000, -1234567890123456789012345678, None, 1])))))
save("decimal256.arrows", stream(
    batch(("Wide", decimals(pa.decimal256(76, 2), 4, [-250, None, 7922816251426433759354395033500]))),
    batch(("Wide", decimals(pa.decimal256(76, 2), 4, [1, 2, 3])))))
save("decimal256-tens.arrows", stream(batch(("Tens", decimals(pa.decimal256(76, 40), 4, [10**40, 0])))))
save("decimal32-decimal64.arrows", stream(batch(
    ("D32", raw(pa.decimal32(9, 2), "i", [150, None, -225, 999999999, 0])),
    ("D32Whole", raw(pa.decimal32(9, 9), "i", [999999999, -999999999, None, 1, 0])),
    ("D64", raw(pa.decimal64(18, 4), "q", [15000, -1, None, 999999999999999999, 0])),
    ("D64Whole", raw(pa.decimal64(18, 18), "q", [999999999999999999, None, -999999999999999999, 1, 0])))))
save("decimal-odd-scales.arrows", stream(batch(
    ("Thousands", raw(pa.decimal32(9, -3), "i", [7, -12, None, 2147483647])),
    ("Tiny", raw(pa.decimal64(18, 30), "q", [1500000000000, None, -250000000000000000, 0])))))
save("decimal-negative-scale.arrows", stream(batch(
    ("Hundreds", decimals(pa.decimal128(10, -2), 2, [5, -7])),
    ("Fine", decimals(pa.decimal128(38, 30), 2, [1500000000000000000000000000000, -25000000000000000000000000000])))))

save("floats.arrows", stream(batch(
    ("F64", raw(pa.float64(), "d", [float("nan"), float("-inf"), -0.0, None, 0.1])),
    ("F32", raw(pa.float32(), "f", [float("nan"), float("inf"), 0.1, None, 1.401298464324817e-45])))))
save("integers.arrows", stream(batch(
    ("I8", raw(pa.int8(), "b", [-128, 127, None])),
    ("I16", raw(pa.int16(), "h", [-32768, 32767, None])),
    ("I32", raw(pa.int32(), "i", [-2**31, 2**31 - 1, None])),
    ("I64", raw(pa.int64(), "q", [-2**63, 2**63 - 1, None])),
    ("U8", raw(pa.uint8(), "B", [0, 255, None])),
    ("U16", raw(pa.uint16(), "H", [0, 65535, None])),
    ("U32", raw(pa.uint32(), "I", [0, 2**32 - 1, None])),
    ("U64", raw(pa.uint64(), "Q", [0, 2**63 - 1, None])))))

# ---- dates, times and flags --------------------------------------------------------------------

noon = datetime.datetime(2026, 9, 30, 12, 0, 0) - datetime.datetime(1970, 1, 1)
noon_us = noon // datetime.timedelta(microseconds=1)
save("dates.arrows", stream(batch(
    ("D32", raw(pa.date32(), "i", [noon.days, -719162, 2932896, None])),
    ("D64", raw(pa.date64(), "q", [noon_us // 1000, -1, 0, None])),
    ("S", raw(pa.timestamp("s"), "q", [noon_us // 1_000_000, -62135596800, 253402300799, None])),
    ("Ms", raw(pa.timestamp("ms"), "q", [noon_us // 1000 + 1, -1, 0, None])),
    ("Us", raw(pa.timestamp("us"), "q", [noon_us + 1, -1, 0, None])),
    ("Ns", raw(pa.timestamp("ns"), "q", [noon_us * 1000 + 100, -2**63 + 8, 2**63 - 8, None])))))

time = (12 * 3600 + 30 * 60 + 15)
save("times.arrows", stream(batch(
    ("S", raw(pa.time32("s"), "i", [0, time, None, 86399])),
    ("Ms", raw(pa.time32("ms"), "i", [0, time * 1000 + 1, None, 86399999])),
    ("Us", raw(pa.time64("us"), "q", [0, time * 1_000_000 + 1, None, 86399999999])),
    ("Ns", raw(pa.time64("ns"), "q", [0, time * 1_000_000_000 + 100, None, 86399999999900])))))

zones = ["UTC", "Etc/UTC", "GMT", "Etc/GMT", "UCT", "Etc/UCT", "Universal", "Etc/Universal", "Zulu", "Etc/Zulu",
         "Greenwich", "Etc/Greenwich", "GMT0", "Etc/GMT0", "GMT+0", "Etc/GMT+0", "GMT-0", "Etc/GMT-0",
         "+00:00", "-00:00", "Z", "utc", "gmt", "etc/zulu", "UNIVERSAL"]
evening = (datetime.datetime(2026, 9, 30, 21, 15, 0) - datetime.datetime(1970, 1, 1)) // datetime.timedelta(microseconds=1)
save("utc-zones.arrows", stream(batch(*[(zone, raw(pa.timestamp("us", tz=zone), "q", [evening, None])) for zone in zones])))

save("bool.arrows", stream(batch(("Live", pa.array([None if i == 9 else i % 3 == 0 for i in range(20)], pa.bool_())))))

# ---- the stream itself -------------------------------------------------------------------------

desk = pa.array(["Rates", "FX"])
save("file.arrow", file(
    batch(("Desk", dictionary(pa.array([1, 0, None], pa.int32()), desk)), ("Pnl", pa.array([1.5, None, -2.0]))),
    batch(("Desk", dictionary(pa.array([0], pa.int32()), desk)), ("Pnl", pa.array([3.0])))))

schema = pa.schema([pa.field("Id", pa.int64(), nullable=False), pa.field("Desk", pa.string(), metadata={"exgrid.caption": "Trading desk"})],
                   metadata={"exgrid.recordKey": "Id", "exgrid.version": "41"})
save("metadata.arrows", stream(pa.record_batch([pa.array([7, 8], pa.int64()), pa.array(["FX", None])], schema=schema)))
save("no-metadata.arrows", stream(batch(("Desk", pa.array(["FX"])))))
save("no-batches.arrows", stream(schema=pa.schema([pa.field("Desk", pa.string())])))
save("legacy-framing.arrows", stream(batch(("N", pa.array([1, None, 3], pa.int64()))), use_legacy_format=True))

# ---- refusals: types outside the table, and zones other than UTC --------------------------------

outside = {
    "list": pa.array([[1], None], pa.list_(pa.int64())),
    "large-list": pa.array([[1], None], pa.large_list(pa.int64())),
    "struct": pa.array([{"a": 1}, {"a": 2}], pa.struct([("a", pa.int64())])),
    "map": pa.array([[("k", 1)], None], pa.map_(pa.string(), pa.int64())),
    "binary": pa.array([b"\x01\x02", None], pa.binary()),
    "large-binary": pa.array([b"\x01\x02", None], pa.large_binary()),
    "binary-view": pa.array([b"\x01\x02", None], pa.binary_view()),
    "fixed-size-binary": pa.array([bytes(16), bytes(16)], pa.binary(16)),
    "duration": pa.array([1, None], pa.duration("ms")),
    "interval": pa.array([pa.MonthDayNano([1, 2, 3]), None], pa.month_day_nano_interval()),
    "float16": raw(pa.float16(), "e", [1.5, None]),
    "null": pa.nulls(2),
    "dictionary-int64": dictionary(pa.array([0, None], pa.int32()), pa.array([5], pa.int64())),
}
for name, array in outside.items():
    save(f"outside-{name}.arrows", stream(batch(("Fine", pa.array(["a", "b"])), ("Value", array))))

for i, zone in enumerate(["Asia/Tokyo", "+09:00", "Europe/London", "America/New_York", "-05:00", "Etc/GMT+5"]):
    save(f"zone-{i + 1}.arrows", stream(batch(("When", raw(pa.timestamp("us", tz=zone), "q", [0, None])))))

# ---- refusals: values a kind cannot hold -------------------------------------------------------

save("decimal128-beyond.arrows", stream(
    batch(("Notional", decimals(pa.decimal128(38, 0), 2, [1, 2, None]))),
    batch(("Notional", decimals(pa.decimal128(38, 0), 2, [3, -(2**96)])))))
save("decimal128-places.arrows", stream(batch(("Rate", decimals(pa.decimal128(38, 30), 2, [1500000000000000000000000000000, 1])))))
save("decimal256-beyond.arrows", stream(batch(("Wide", decimals(pa.decimal256(76, 2), 4, [5, 10**40])))))
save("decimal32-beyond.arrows", stream(batch(("Notional", raw(pa.decimal32(9, -25), "i", [1, None, 999999999])))))
save("decimal64-beyond.arrows", stream(batch(("Notional", raw(pa.decimal64(18, -15), "q", [1, None, -100000000000000000])))))
save("decimal32-places.arrows", stream(batch(("Notional", raw(pa.decimal32(9, 30), "i", [1000, None, 15])))))
save("decimal64-places.arrows", stream(batch(("Notional", raw(pa.decimal64(18, 29), "q", [10, None, -123])))))
save("uint64-beyond.arrows", stream(batch(("Count", raw(pa.uint64(), "Q", [1, None, 2**63])))))
save("date32-outside.arrows", stream(batch(("When", raw(pa.date32(), "i", [0, -719163])))))
save("date64-outside.arrows", stream(batch(("When", raw(pa.date64(), "q", [0, 253402300800000])))))
save("timestamp-s-outside.arrows", stream(batch(("When", raw(pa.timestamp("s"), "q", [0, -62135596801])))))
save("timestamp-us-utc-outside.arrows", stream(batch(("When", raw(pa.timestamp("us", tz="UTC"), "q", [0, 253402300800000000])))))
save("timestamp-ns-finer.arrows", stream(batch(("When", raw(pa.timestamp("ns"), "q", [100, 150])))))
save("time32-s-outside.arrows", stream(batch(("At", raw(pa.time32("s"), "i", [86399, None, 86400])))))
save("time32-ms-outside.arrows", stream(batch(("At", raw(pa.time32("ms"), "i", [0, None, -1])))))
save("time64-us-outside.arrows", stream(batch(("At", raw(pa.time64("us"), "q", [86399999999, None, 86400000000])))))
save("time64-ns-outside.arrows", stream(batch(("At", raw(pa.time64("ns"), "q", [0, None, -100])))))
save("time64-ns-finer.arrows", stream(batch(("At", raw(pa.time64("ns"), "q", [100, 45015000000150])))))
for name, index in [("past", 3), ("negative", -1)]:
    save(f"dictionary-index-{name}.arrows", stream(batch(("Region", dictionary(raw(pa.int32(), "i", [0, None, index]), pa.array(["a", "b", "c"]))))))
save("utf8-invalid.arrows", stream(batch(("Name", utf8_bytes([b"fine", b"\xc3\x28"])))))
save("dictionary-entry-invalid.arrows", stream(batch(("Region", dictionary(pa.array([0, 0, 1], pa.int32()), utf8_bytes([b"fine", b"\xff"]))))))

text = [b"a text longer than a view" + b"\xc3\x28" + b" and more after it"]
bad_views = {
    "inline-invalid": inline_view(b"\xc3\x28"),
    "buffer-invalid": buffer_view(text, 0, 2, 30),
    "past-end": buffer_view(text, 0, 2, len(text[0])),
    "no-buffer": buffer_view(text, 1, 0, 13),
    "negative-length": buffer_view(text, 0, 0, -1),
}
for name, bad in bad_views.items():
    save(f"utf8-view-{name}.arrows", stream(batch(("Name", views(text, inline_view(b"fine"), buffer_view(text, 0, 0, 13), bad)))))
short = [b"a text longer than a view"]
save("dictionary-view-outside.arrows", stream(batch(("Region", dictionary(
    pa.array([0, 0, 1], pa.int32()), views(short, inline_view(b"fine"), buffer_view(short, 0, 5, 40)))))))

# ---- refusals: the schema and its metadata ------------------------------------------------------

save("duplicate-name.arrows", stream(pa.record_batch([pa.array([1], pa.int64()), pa.array(["x"])],
                                                     schema=pa.schema([("A", pa.int64()), ("A", pa.string())]))))
save("no-name.arrows", stream(pa.record_batch([pa.array([1], pa.int64()), pa.array([2], pa.int64())],
                                              schema=pa.schema([("A", pa.int64()), ("", pa.int64())]))))
for name, version in [("text", "abc"), ("negative", "-1"), ("fraction", "1.5")]:
    save(f"version-{name}.arrows", stream(batch(("N", pa.array([1], pa.int64())), metadata={"exgrid.version": version})))
save("key-missing.arrows", stream(batch(("N", pa.array([1], pa.int64())), metadata={"exgrid.recordKey": "Id"})))
save("key-double.arrows", stream(batch(("Price", pa.array([1.5])), metadata={"exgrid.recordKey": "Price"})))
save("key-blank.arrows", stream(batch(("Id", pa.array([7, None], pa.int64())), metadata={"exgrid.recordKey": "Id"})))
save("key-twice.arrows", stream(
    batch(("Id", pa.array(["T1", "T2"])), metadata={"exgrid.recordKey": "Id"}),
    batch(("Id", pa.array(["T3", "T1"])), metadata={"exgrid.recordKey": "Id"})))
save("key-twice-integer.arrows", stream(batch(("Id", pa.array([5, 6, 5], pa.int64())), metadata={"exgrid.recordKey": "Id"})))
save("rows-without-columns.arrows", stream(*pa.table({"a": [1, 2, 3]}).drop_columns(["a"]).to_batches()))

# ---- refusals: bytes that are not a whole stream or file ---------------------------------------

for i, text in enumerate(["Region,Pnl\nEMEA,1.5\n", '[{"Region":"EMEA"}]', "<!DOCTYPE html><html></html>", "PAR1\0\0\0\0PAR1", "\x10" + "\0" * 19]):
    save(f"not-arrow-{i + 1}.bin", text.encode())

whole = stream(batch(("N", pa.array([1], pa.int64()))))
save("no-schema.arrows", whole[messages(whole)[0][1]:])
save("gzipped.bin", gzip.compress(whole, mtime=0))
save("empty.bin", b"")
save("marker-alone.bin", b"\xff\xff\xff\xff\0\0\0\0")
save("inside-first-message.bin", whole[:20])

two = stream(batch(("N", pa.array([1, 2], pa.int64()))), batch(("N", pa.array([3], pa.int64()))))
second = messages(two)[-1]
for name, cut in [("before-marker", len(two) - 8), ("inside-marker", len(two) - 4), ("inside-body", second[1] - 4),
                  ("inside-metadata", second[0] + 12), ("between-batches", second[0])]:
    save(f"cut-{name}.arrows", two[:cut])
whole_file = file(batch(("N", pa.array([1, 2], pa.int64()))))
save("file-cut-footer.arrow", whole_file[:-10])
save("file-cut-head.arrow", whole_file[:8])

print(f"{len(written)} streams written to {HERE}")
