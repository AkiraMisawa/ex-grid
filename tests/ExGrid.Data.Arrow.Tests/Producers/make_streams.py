"""Writes the Arrow streams other producers write, which ProducerTests reads (ADR-0064).

The streams are kept in the repository as they came, so the tests need no Python. This script is
only for making them again:

    python -m venv .venv && .venv/bin/pip install pyarrow==25.0.1 polars==1.44.2 duckdb==1.5.6
    .venv/bin/python tests/ExGrid.Data.Arrow.Tests/Producers/make_streams.py

Each stream is small and holds what its test asserts; the test names what each one is for.
"""

import datetime
import decimal
import io
import pathlib

import duckdb
import polars as pl
import pyarrow as pa
import pyarrow.ipc as ipc

HERE = pathlib.Path(__file__).parent


def save(name, data):
    (HERE / name).write_bytes(data)
    print(f"{name}: {len(data)} bytes")


def stream(schema, batches, **options):
    sink = io.BytesIO()
    with ipc.new_stream(sink, schema, options=ipc.IpcWriteOptions(**options)) as writer:
        for batch in batches:
            writer.write_batch(batch)
    return sink.getvalue()


def stats(data):
    reader = ipc.open_stream(pa.BufferReader(data))
    reader.read_all()
    return reader.stats


def dictionary_batch(schema, dictionary, indices, numbers):
    region = pa.DictionaryArray.from_arrays(pa.array(indices, pa.int32()), pa.array(dictionary, pa.string()))
    return pa.record_batch([region, pa.array(numbers, pa.int64())], schema=schema)


# pyarrow, with Arrow's delta dictionary batches: each record batch's dictionary extends the one
# before, and pyarrow sends only the new entries. The dictionary is out of the rows' order, holds
# "amer" beside "AMER" and a null entry, and the indices hold a null.
schema = pa.schema([("region", pa.dictionary(pa.int32(), pa.string())), ("n", pa.int64())])
first = ["emea", "AMER", None]
second = first + ["amer", "APAC"]
third = second + ["Amer"]
batches = [
    dictionary_batch(schema, first, [1, 0, None, 2, 1], [1, 2, 3, 4, 5]),
    dictionary_batch(schema, second, [3, 4, 1, 0], [6, 7, 8, 9]),
    dictionary_batch(schema, third, [5, 3], [10, 11]),
]
data = stream(schema, batches, emit_dictionary_deltas=True)
assert stats(data).num_dictionary_deltas == 2, stats(data)
save("pyarrow-deltas.arrows", data)

# pyarrow, with a dictionary replaced between record batches: the second batch's dictionary is not
# an extension of the first, so pyarrow sends it whole, and its codes mean other texts.
batches = [
    dictionary_batch(schema, ["b", "a"], [0, 1, 1], [1, 2, 3]),
    dictionary_batch(schema, ["a", "c", "b"], [1, 0, 2, None], [4, 5, 6, 7]),
]
data = stream(schema, batches)
assert stats(data).num_replaced_dictionaries == 1, stats(data)
save("pyarrow-replaced.arrows", data)

# pyarrow, every integer width as a dictionary's indices, over utf8 and large_utf8.
columns = {}
for name, index in [("i8", pa.int8()), ("i16", pa.int16()), ("u8", pa.uint8()), ("u16", pa.uint16()),
                    ("u32", pa.uint32()), ("i64", pa.int64()), ("u64", pa.uint64())]:
    values = pa.large_string() if name.startswith("u") else pa.string()
    columns[name] = pa.DictionaryArray.from_arrays(pa.array([2, 0, None, 1], index), pa.array(["z", "y", "x"], values))
table = pa.table(columns)
save("pyarrow-indices.arrows", stream(table.schema, table.to_batches()))

# pyarrow, an Arrow file (Feather 2) with ZSTD buffers: two record batches.
table = pa.table({
    "desk": pa.array(["Rates", None, "FX", "Rates"], pa.string()),
    "pnl": pa.array([decimal.Decimal("1.50"), decimal.Decimal("-2.25"), None, decimal.Decimal("1000000.01")], pa.decimal128(18, 2)),
})
sink = io.BytesIO()
with ipc.new_file(sink, table.schema, options=ipc.IpcWriteOptions(compression="zstd")) as writer:
    for batch in table.to_batches(max_chunksize=2):
        writer.write_batch(batch)
save("pyarrow-zstd.arrow", sink.getvalue())

# Polars, as it writes for readers of older Arrow (compat_level oldest): large_utf8, a Categorical as
# a dictionary of large_utf8 with uint32 indices, and its own column types.
frame = pl.DataFrame({
    "region": ["amer", "AMER", None, "emea"],
    "category": pl.Series(["b", "a", "b", None], dtype=pl.Categorical),
    "amount": [1.5, -2.0, None, float("inf")],
    "quantity": [1, None, 3, -4],
    "when": [datetime.datetime(2026, 1, 1, 12, 30), None, datetime.datetime(2026, 1, 2), datetime.datetime(2026, 1, 3, 0, 0, 0, 123456)],
    "day": [datetime.date(2026, 1, 1), None, datetime.date(2026, 1, 2), datetime.date(1969, 12, 31)],
    "pnl": pl.Series([decimal.Decimal("1.50"), None, decimal.Decimal("-2.25"), decimal.Decimal("0")], dtype=pl.Decimal(18, 2)),
    "live": [True, False, None, True],
    "utc": pl.Series([datetime.datetime(2026, 1, 1, 12, 0)] * 4).dt.replace_time_zone("UTC"),
})
sink = io.BytesIO()
frame.write_ipc_stream(sink, compat_level=pl.CompatLevel.oldest())
save("polars-oldest.arrows", sink.getvalue())

# Polars, as it writes by default: text as utf8_view, which ADR-0064's table does not read.
sink = io.BytesIO()
frame.select("region").write_ipc_stream(sink)
save("polars-default.arrows", sink.getvalue())

# DuckDB, through pyarrow as a Python service hands it on: an ENUM as a dictionary with uint8
# indices, decimals, a TIMESTAMPTZ in the session's zone (Etc/UTC here), every TIMESTAMP unit, a
# HUGEINT (a decimal128(38, 0)) and a UBIGINT, each within what a Snapshot holds.
connection = duckdb.connect()
connection.execute("SET TimeZone = 'Etc/UTC'")
connection.execute("CREATE TYPE desk AS ENUM ('Rates', 'FX', 'Credit')")
table = connection.sql("""
    SELECT * FROM (VALUES
        ('FX'::desk, 1.50::DECIMAL(18,2), 12345678901234567890.12::DECIMAL(38,2), TIMESTAMPTZ '2026-01-01 12:00:00+00',
         TIMESTAMP '2026-01-01 12:00:00.123456', TIMESTAMP_S '2026-01-01 12:00:01', TIMESTAMP_MS '2026-01-01 12:00:00.5',
         TIMESTAMP_NS '2026-01-01 12:00:00.0000001', DATE '2026-01-01', true, 42::BIGINT, 12345678901234567890123::HUGEINT,
         9223372036854775807::UBIGINT),
        (NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
        ('Rates'::desk, -0.01::DECIMAL(18,2), -1::DECIMAL(38,2), TIMESTAMPTZ '1999-12-31 23:59:59+00',
         TIMESTAMP '1970-01-01 00:00:00', TIMESTAMP_S '1970-01-01 00:00:00', TIMESTAMP_MS '1970-01-01 00:00:00',
         TIMESTAMP_NS '1970-01-01 00:00:00', DATE '1970-01-01', false, -42::BIGINT, -1::HUGEINT, 0::UBIGINT)
    ) AS t(desk, pnl, big, tstz, ts, ts_s, ts_ms, ts_ns, d, b, n, h, u)
""").arrow()
if isinstance(table, pa.RecordBatchReader):
    table = table.read_all()
save("duckdb.arrows", stream(table.schema, table.to_batches()))
