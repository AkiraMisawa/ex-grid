using ExGrid.Data;
using ExGrid.Data.Storage;
using Xunit;
using static ExGrid.Data.Tests.Fixtures;

namespace ExGrid.Data.Tests;

/// <summary>How each kind holds its values, and a Blank in every kind (ADR-0063, DA-3).</summary>
public class KindTests
{
    [Fact] // ADR-0063: text is held exactly and told apart ordinally — two spellings are two entries
    public void Text_is_exact_and_two_spellings_are_two_entries()
    {
        var snapshot = Column<string?>(b => b.Text("V", c => c.Value), "amer", "AMER", "amer", "Amer ", "ｱ", "ア");

        var column = (TextColumn)snapshot["V"];
        Assert.Equal(["amer", "AMER", "Amer ", "ｱ", "ア"], column.Dictionary);
        Assert.Equal([0, 1, 0, 2, 3, 4], Codes(snapshot, "V"));
        Assert.Equal(["amer", "AMER", "amer", "Amer ", "ｱ", "ア"], Values(snapshot, "V"));
    }

    [Fact] // ADR-0063: the dictionary holds every distinct value once, in the order it first appears
    public void The_dictionary_is_in_order_of_first_appearance()
    {
        var snapshot = Column<string?>(b => b.Text("V", c => c.Value), "b", "a", "c", "a", "b", "d");

        var dictionary = ((TextColumn)snapshot["V"]).Dictionary;
        Assert.Equal(["b", "a", "c", "d"], dictionary);
        Assert.True(dictionary.TryGetCode("c", out var code));
        Assert.Equal(2, code);
        Assert.True(dictionary.TryGetCode("d".AsSpan(), out code));
        Assert.Equal(3, code);
        Assert.False(dictionary.TryGetCode("B", out _));
    }

    [Fact] // ADR-0063: a Blank text is kept apart from the empty string, which is a value
    public void A_blank_text_is_not_the_empty_string()
    {
        var snapshot = Column<string?>(b => b.Text("V", c => c.Value), null, "", null);

        var column = (TextColumn)snapshot["V"];
        Assert.Equal([""], column.Dictionary);
        Assert.Equal([-1, 0, -1], Codes(snapshot, "V"));
        Assert.Equal([null, "", null], Values(snapshot, "V"));
        Assert.True(snapshot.IsBlank(snapshot.Rows[0], column));
        Assert.False(snapshot.IsBlank(snapshot.Rows[1], column));
        Assert.Equal([0b101UL], snapshot.Slice(0).Blanks(column).ToArray());
    }

    [Fact] // ADR-0063: a Blank is possible in every kind, and differs from zero and from false
    public void A_blank_is_possible_in_every_kind_and_differs_from_zero()
    {
        AssertBlankThenZero(Column<decimal?>(b => b.Decimal("V", c => c.Value), null, 0m), 0m);
        AssertBlankThenZero(Column<double?>(b => b.Double("V", c => c.Value), null, 0d), 0d);
        AssertBlankThenZero(Column<long?>(b => b.Integer("V", c => c.Value), null, 0L), 0L);
        AssertBlankThenZero(Column<DateTime?>(b => b.Date("V", c => c.Value), null, DateTime.MinValue), DateTime.MinValue);
        AssertBlankThenZero(Column<bool?>(b => b.Boolean("V", c => c.Value), null, false), false);
        AssertBlankThenZero(Column<string?>(b => b.Text("V", c => c.Value), null, ""), "");

        static void AssertBlankThenZero(Snapshot snapshot, object zero)
        {
            var column = snapshot["V"];
            Assert.True(snapshot.IsBlank(snapshot.Rows[0], column));
            Assert.Null(snapshot.ValueAt(snapshot.Rows[0], column));
            Assert.False(snapshot.IsBlank(snapshot.Rows[1], column));
            Assert.Equal(zero, snapshot.ValueAt(snapshot.Rows[1], column));
            Assert.Equal([1UL], snapshot.Slice(0).Blanks(column).ToArray());
        }
    }

    [Fact] // ADR-0063: a column with no Blank hands out no Blanks
    public void A_column_without_blanks_hands_out_an_empty_bit_set()
    {
        var snapshot = Column<long?>(b => b.Integer("V", c => c.Value), 1, 2, 3);

        Assert.True(snapshot.Slice(0).Blanks(snapshot["V"]).IsEmpty);
    }

    [Fact] // ADR-0063: Decimal is held exactly, whatever its size or its places
    public void Decimal_is_exact()
    {
        decimal[] values = [0.1m, 0.2m, 79228162514264337593543950335m, -0.0000000000000000000000000001m, 123456789.123456789m];
        var snapshot = Column<decimal?>(b => b.Decimal("V", c => c.Value), [.. values.Select(v => (decimal?)v)]);

        Assert.Equal(values.Cast<object>(), Values(snapshot, "V"));
        var numbers = snapshot.Slice(0).Decimals((DecimalColumn)snapshot["V"]);
        Assert.Equal(-1, numbers.Scale);
        Assert.Equal(values, numbers.Exact.ToArray());
    }

    [Fact] // ADR-0063: a Decimal holds values, not the scale each was written with — 1.5 and 1.50 read back alike
    public void Decimal_one_point_five_and_one_point_fifty_read_back_alike()
    {
        var snapshot = Column<decimal?>(b => b.Decimal("V", c => c.Value), 1.5m, 1.50m, 1.500m, 2.00m, 0.00m);

        var values = Values(snapshot, "V");
        Assert.Equal(["1.5", "1.5", "1.5", "2", "0"], values.Select(v => ((decimal)v!).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        var numbers = snapshot.Slice(0).Decimals((DecimalColumn)snapshot["V"]);
        Assert.Equal(1, numbers.Scale);
        Assert.Equal([15L, 15L, 15L, 20L, 0L], numbers.Scaled.ToArray());
    }

    [Fact] // ADR-0063: when every value fits at one power of ten, Decimal is held as scaled 64-bit integers
    public void Decimal_that_fits_is_held_as_scaled_integers()
    {
        var snapshot = Column<decimal?>(b => b.Decimal("V", c => c.Value), 12.34m, 5m, -0.5m, null, 92233720368547758.07m);

        var numbers = snapshot.Slice(0).Decimals((DecimalColumn)snapshot["V"]);
        Assert.Equal(2, numbers.Scale);
        Assert.Equal([1234L, 500L, -50L, 0L, long.MaxValue], numbers.Scaled.ToArray());
        Assert.True(numbers.Exact.IsEmpty);
        Assert.Equal(12.34m, numbers[0]);
        Assert.Equal(92233720368547758.07m, numbers[4]);
    }

    [Fact] // ADR-0063: a value that does not fit a long at the scale turns the slice to decimal, still exact
    public void Decimal_that_does_not_fit_is_held_as_decimal()
    {
        var snapshot = Column<decimal?>(b => b.Decimal("V", c => c.Value), 0.5m, 92233720368547758.08m, null);

        var numbers = snapshot.Slice(0).Decimals((DecimalColumn)snapshot["V"]);
        Assert.Equal(-1, numbers.Scale);
        Assert.Equal([0.5m, 92233720368547758.08m, 0m], numbers.Exact.ToArray());
        Assert.Equal([0.5m, 92233720368547758.08m, null], Values(snapshot, "V"));
    }

    [Fact] // ADR-0063: each slice holds its Decimals at its own scale
    public void Each_slice_has_its_own_decimal_scale()
    {
        var snapshot = new SnapshotBuilder<Cell<decimal?>> { Tuning = new SnapshotTuning(SegmentShift: 1) }
            .Decimal("V", c => c.Value)
            .Build([new(1.5m), new(2m), new(3.125m), new(4m), new(1e20m), new(1m)]);

        var column = (DecimalColumn)snapshot["V"];
        Assert.Equal(3, snapshot.SliceCount);
        Assert.Equal(1, snapshot.Slice(0).Decimals(column).Scale);
        Assert.Equal(3, snapshot.Slice(1).Decimals(column).Scale);
        Assert.Equal(-1, snapshot.Slice(2).Decimals(column).Scale);
        Assert.Equal([1.5m, 2m, 3.125m, 4m, 1e20m, 1m], Values(snapshot, "V"));
    }

    [Fact] // ADR-0063: Double keeps NaN, the infinities and negative zero exactly as they came
    public void Double_keeps_non_finite_values()
    {
        double[] values = [double.NaN, double.PositiveInfinity, double.NegativeInfinity, -0.0, double.Epsilon, BitConverter.Int64BitsToDouble(0x7FF8_0000_0000_0001)];
        var snapshot = Column<double?>(b => b.Double("V", c => c.Value), [.. values.Select(v => (double?)v)]);

        var held = snapshot.Slice(0).Doubles((DoubleColumn)snapshot["V"]).ToArray();
        Assert.Equal(values.Select(BitConverter.DoubleToInt64Bits), held.Select(BitConverter.DoubleToInt64Bits));
        Assert.All(Values(snapshot, "V"), v => Assert.IsType<double>(v));
    }

    [Fact] // ADR-0063: Integer is held as a 64-bit integer
    public void Integer_is_64_bit()
    {
        var snapshot = Column<long?>(b => b.Integer("V", c => c.Value), long.MinValue, long.MaxValue, 0, -1);

        Assert.Equal([long.MinValue, long.MaxValue, 0L, -1L], snapshot.Slice(0).Integers((IntegerColumn)snapshot["V"]).ToArray());
        Assert.Equal([long.MinValue, long.MaxValue, 0L, -1L], Values(snapshot, "V"));
    }

    [Fact] // ADR-0063: an int accessor is an Integer column too, widened without a box
    public void An_int_accessor_makes_an_integer_column()
    {
        var snapshot = Column<int?>(b => b.Integer("V", c => c.Value), int.MinValue, null, int.MaxValue);

        Assert.Equal(SnapshotKind.Integer, snapshot["V"].Kind);
        Assert.Equal([(long)int.MinValue, null, (long)int.MaxValue], Values(snapshot, "V"));
    }

    [Fact] // ADR-0063: a DateTime is held as its ticks, with its Kind ignored
    public void Date_from_a_DateTime_is_its_ticks_with_its_kind_ignored()
    {
        var clock = new DateTime(2026, 9, 30, 10, 15, 0);
        var snapshot = Column<DateTime?>(b => b.Date("V", c => c.Value),
            DateTime.SpecifyKind(clock, DateTimeKind.Utc),
            DateTime.SpecifyKind(clock, DateTimeKind.Local),
            DateTime.SpecifyKind(clock, DateTimeKind.Unspecified));

        Assert.Equal([clock.Ticks, clock.Ticks, clock.Ticks], snapshot.Slice(0).Ticks((DateColumn)snapshot["V"]).ToArray());
        Assert.All(Values(snapshot, "V"), v =>
        {
            Assert.Equal(clock, (DateTime)v!);
            Assert.Equal(DateTimeKind.Unspecified, ((DateTime)v!).Kind);
        });
    }

    [Fact] // ADR-0063: a DateOnly is held as its midnight
    public void Date_from_a_DateOnly_is_its_midnight()
    {
        var snapshot = Column<DateOnly?>(b => b.Date("V", c => c.Value), new DateOnly(2026, 9, 30), DateOnly.MinValue, DateOnly.MaxValue);

        Assert.Equal(
            [new DateTime(2026, 9, 30).Ticks, 0L, new DateTime(9999, 12, 31).Ticks],
            snapshot.Slice(0).Ticks((DateColumn)snapshot["V"]).ToArray());
        Assert.False(((DateColumn)snapshot["V"]).HasTime);
    }

    [Fact] // ADR-0063: a DateTimeOffset is held as the clock it shows, with its offset dropped
    public void Date_from_a_DateTimeOffset_is_its_clock_with_the_offset_dropped()
    {
        var tokyo = new DateTimeOffset(2026, 9, 30, 9, 0, 0, TimeSpan.FromHours(9));
        var london = new DateTimeOffset(2026, 9, 30, 9, 0, 0, TimeSpan.FromHours(1));
        var snapshot = Column<DateTimeOffset?>(b => b.Date("V", c => c.Value), tokyo, london);

        var nine = new DateTime(2026, 9, 30, 9, 0, 0).Ticks;
        Assert.Equal([nine, nine], snapshot.Slice(0).Ticks((DateColumn)snapshot["V"]).ToArray());
    }

    [Fact] // ADR-0063: a Date column says whether it holds any time that is not a midnight
    public void A_date_column_says_whether_it_holds_times()
    {
        var dates = Column<DateTime?>(b => b.Date("V", c => c.Value), new DateTime(2026, 1, 1), null);
        var times = Column<DateTime?>(b => b.Date("V", c => c.Value), new DateTime(2026, 1, 1), new DateTime(2026, 1, 1, 0, 0, 1));

        Assert.False(((DateColumn)dates["V"]).HasTime);
        Assert.True(((DateColumn)times["V"]).HasTime);
    }

    [Fact] // ADR-0063: Boolean is held as true or false
    public void Boolean_is_true_or_false()
    {
        var snapshot = Column<bool?>(b => b.Boolean("V", c => c.Value), true, false, null);

        Assert.Equal([true, false, false], snapshot.Slice(0).Booleans((BooleanColumn)snapshot["V"]).ToArray());
        Assert.Equal([true, false, null], Values(snapshot, "V"));
    }

    [Fact] // ADR-0063: an untyped accessor under a declared kind takes values of that kind, and exact conversions
    public void An_untyped_accessor_takes_its_kind_and_exact_conversions()
    {
        var snapshot = new SnapshotBuilder<Cell<object?>>()
            .Column("Text", SnapshotKind.Text, c => c.Value is int ? (object)"x" : c.Value is char ? c.Value : null)
            .Column("Decimal", SnapshotKind.Decimal, c => c.Value is int i ? (object)i : c.Value is char ? 2.5m : DBNull.Value)
            .Column("Double", SnapshotKind.Double, c => c.Value is int ? (object)1.5f : c.Value is char ? double.NaN : null)
            .Column("Integer", SnapshotKind.Integer, c => c.Value is int i ? (object)(byte)i : c.Value is char ? ulong.MaxValue / 2 : null)
            .Column("Date", SnapshotKind.Date, c => c.Value is int ? (object)new DateOnly(2026, 1, 2) : c.Value is char ? new DateTimeOffset(2026, 1, 2, 3, 0, 0, TimeSpan.FromHours(5)) : null)
            .Column("Boolean", SnapshotKind.Boolean, c => c.Value is int ? (object)true : null)
            .Build([new(7), new('c'), new(null)]);

        Assert.Equal(["x", "c", null], Values(snapshot, "Text"));
        Assert.Equal([7m, 2.5m, null], Values(snapshot, "Decimal"));
        Assert.Equal([1.5, double.NaN, null], Values(snapshot, "Double"));
        Assert.Equal([7L, (long)(ulong.MaxValue / 2), null], Values(snapshot, "Integer"));
        Assert.Equal([new DateTime(2026, 1, 2), new DateTime(2026, 1, 2, 3, 0, 0), null], Values(snapshot, "Date"));
        Assert.Equal([true, null, null], Values(snapshot, "Boolean"));
    }

    [Fact] // ADR-0063: a column's caption is the data's own, and the name unless one is declared
    public void Captions_default_to_names()
    {
        var snapshot = new SnapshotBuilder<Cell<long?>>()
            .Integer("Qty", c => c.Value, caption: "Quantity")
            .Integer("Raw", c => c.Value)
            .Build([new(1)]);

        Assert.Equal("Quantity", snapshot["Qty"].Caption);
        Assert.Equal("Raw", snapshot["Raw"].Caption);
        Assert.Equal([0, 1], snapshot.Columns.Select(c => c.Ordinal));
        Assert.IsType<IntegerColumn>(snapshot["Qty"]);
        Assert.True(snapshot.TryGetColumn("Raw", out var raw));
        Assert.Same(snapshot["Raw"], raw);
        Assert.False(snapshot.TryGetColumn("raw", out _));
        var missing = Assert.Throws<KeyNotFoundException>(() => snapshot["Missing"]);
        Assert.Contains("'Missing'", missing.Message);
    }
}
