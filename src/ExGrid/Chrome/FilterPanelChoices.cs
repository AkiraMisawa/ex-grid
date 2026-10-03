using System.Globalization;

namespace ExGrid.Chrome;

/// <summary>
/// What a filter panel's choices mean (ADR-0009) — decided here, once, so that the
/// built-in panel and a substituted one turn the same choices into the same
/// <see cref="FilterSpec"/>, and swapping Chrome still changes nothing about behaviour
/// (ADR-0010, FN-17). A panel lays out a value list or a condition, keeps its working
/// state, and asks this class what that state means; it decides nothing itself. Pure, so
/// every rule is testable without a component.
/// </summary>
public static class FilterPanelChoices
{
    /// <summary>
    /// The values a value list starts with chosen: the applied <c>In</c> list where one is
    /// in force, and every value otherwise — no filter chooses everything. Intersected with
    /// the domain just fetched: another column's filter can have shifted it since the list
    /// was applied, and a value the panel cannot show must not ride along chosen unseen.
    /// </summary>
    public static HashSet<object?> InitiallyChosen(FilterSpec? current, DistinctValues domain)
    {
        ArgumentNullException.ThrowIfNull(domain);
        return current is { Clauses: [{ Operator: FilterOperator.In, Values: { } applied }] }
            ? new HashSet<object?>(applied.Intersect(domain.Values))
            : new HashSet<object?>(domain.Values);
    }

    /// <summary>
    /// A value list's filter: the chosen values as one <c>In</c> clause, in the domain's
    /// order — or null, no filter at all, when every value is chosen. Set membership, never
    /// a count: a count drifts when the domain shifts under another column's filter, and a
    /// count test once removed a filter the user was looking at. Nothing chosen is an
    /// <c>In</c> with no values, never no filter, which would show every row; it is no filter
    /// a panel may apply (<see cref="CanApply"/>).
    /// </summary>
    public static FilterSpec? FromValueList(DistinctValues domain, IReadOnlySet<object?> chosen)
    {
        ArgumentNullException.ThrowIfNull(domain);
        ArgumentNullException.ThrowIfNull(chosen);
        if (domain.IsTooMany)
            throw new ArgumentException("A TooMany answer has no values to choose from; the panel shows its condition form.", nameof(domain));
        if (domain.Values.All(chosen.Contains))
            return null;
        var ordered = domain.Values.Where(chosen.Contains).ToArray();
        return new FilterSpec([new FilterClause(FilterOperator.In, Values: ordered)]);
    }

    /// <summary>
    /// A value list's filter with a search in the box (ADR-0009, 2026-09-26): the checked
    /// values among those whose text matches, as Excel applies them — a checked value the
    /// search hides is not applied. With <paramref name="addToCurrent"/>, Excel's "Add
    /// current selection to filter", the values of the <c>In</c> list in force join them,
    /// including any the domain no longer shows. With no search it is
    /// <see cref="FromValueList(DistinctValues, IReadOnlySet{object?})"/>: what is checked.
    /// <paramref name="textOf"/> is the text the panel shows for a value, which is what the
    /// search is matched against.
    /// </summary>
    public static FilterSpec? FromValueList(
        DistinctValues domain, IReadOnlySet<object?> chosen, Func<object?, string> textOf,
        string search, bool addToCurrent, FilterSpec? current)
    {
        ArgumentNullException.ThrowIfNull(domain);
        ArgumentNullException.ThrowIfNull(chosen);
        ArgumentNullException.ThrowIfNull(textOf);
        ArgumentNullException.ThrowIfNull(search);
        if (search.Length == 0)
            return FromValueList(domain, chosen);
        if (domain.IsTooMany)
            throw new ArgumentException("A TooMany answer has no values to choose from; the panel shows its condition form.", nameof(domain));

        var applied = domain.Values.Where(v => chosen.Contains(v) && Matches(search, textOf(v))).ToList();
        if (addToCurrent && InForce(current) is { } inForce)
        {
            var extra = inForce.Where(v => !applied.Contains(v)).ToList();
            applied = [.. domain.Values.Where(v => applied.Contains(v) || extra.Contains(v)), .. extra.Where(v => !domain.Values.Contains(v))];
        }
        if (domain.Values.All(applied.Contains))
            return null;
        return new FilterSpec([new FilterClause(FilterOperator.In, Values: [.. applied])]);
    }

    /// <summary>
    /// Whether a panel's answer can be applied: every answer but one holding an <c>In</c>
    /// with no values — nothing chosen, or no chosen value among a search's matches. Such a
    /// clause keeps no row, and the engine refuses it (ADR-0023); no filter in its place
    /// would show every row, which nobody chose. So, as Excel's OK is with nothing ticked
    /// (ADR-0009), Apply shows it is unavailable while the answer is that one, and refuses it
    /// when it comes anyway — a press, or an Enter, that reached a circuit ahead of the render
    /// that disabled it (ADR-0039). The core's <see cref="FilterPanelContext.Apply"/> refuses
    /// it too, and the panel stands.
    /// </summary>
    public static bool CanApply(FilterSpec? spec)
        => spec is null || spec.Clauses.All(c => c.Operator != FilterOperator.In || c.Values is { Count: > 0 });

    /// <summary>Whether a value's text matches the search: contained, ignoring case, as
    /// the panels list it (ADR-0009). An empty search matches everything.</summary>
    public static bool Matches(string search, string text)
    {
        ArgumentNullException.ThrowIfNull(search);
        ArgumentNullException.ThrowIfNull(text);
        return search.Length == 0 || text.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Whether "Add current selection to filter" is offered: while a search is
    /// active on a column whose filter in force is a value list (ADR-0009, 2026-09-26).</summary>
    public static bool OffersAddToFilter(FilterSpec? current, string search)
    {
        ArgumentNullException.ThrowIfNull(search);
        return search.Length > 0 && InForce(current) is not null;
    }

    /// <summary>What "(Select All)" shows over the values the list shows now: checked when
    /// every one is chosen, clear when none is, mixed otherwise (ADR-0009, 2026-09-26).</summary>
    public static SelectAllState StateOfAll(IEnumerable<object?> shown, IReadOnlySet<object?> chosen)
    {
        ArgumentNullException.ThrowIfNull(shown);
        ArgumentNullException.ThrowIfNull(chosen);
        var any = false;
        var all = true;
        foreach (var value in shown)
        {
            if (chosen.Contains(value))
                any = true;
            else
                all = false;
        }
        return all ? SelectAllState.Checked : any ? SelectAllState.Mixed : SelectAllState.Unchecked;
    }

    /// <summary>"(Select All)" toggled: every value shown becomes chosen, or, when every
    /// one already was, none of them is. Values the search hides are left as they are.</summary>
    public static void ToggleAll(IEnumerable<object?> shown, ISet<object?> chosen)
    {
        ArgumentNullException.ThrowIfNull(shown);
        ArgumentNullException.ThrowIfNull(chosen);
        var values = shown.ToList();
        if (StateOfAll(values, (IReadOnlySet<object?>)chosen) == SelectAllState.Checked)
        {
            foreach (var value in values)
                chosen.Remove(value);
        }
        else
        {
            foreach (var value in values)
                chosen.Add(value);
        }
    }

    /// <summary>
    /// A condition form's filter with an optional second condition, as Excel's Custom
    /// AutoFilter (ADR-0009, 2026-09-26): each finished condition is a clause, joined by
    /// <paramref name="combinator"/>. A condition whose operator takes an operand and has
    /// none is unfinished and left out; one finished condition is
    /// <see cref="FromCondition"/>'s answer, and none is no filter.
    /// </summary>
    public static FilterSpec? FromConditions(
        FilterOperator first, object? firstOperand, FilterCombinator combinator,
        FilterOperator? second, object? secondOperand)
    {
        var clauses = new List<FilterClause>(2);
        foreach (var (op, operand) in new[] { ((FilterOperator?)first, firstOperand), (second, secondOperand) })
        {
            if (op is not { } oper)
                continue;
            if (FromCondition(oper, operand) is { Clauses: [var clause] })
                clauses.Add(clause);
        }
        return clauses.Count switch
        {
            0 => null,
            1 => new FilterSpec(clauses),
            _ => new FilterSpec(clauses, combinator),
        };
    }

    /// <summary>The second condition a condition form starts with, and the combinator
    /// between the two: those in force where the column's filter is two conditions; none,
    /// joined by AND, otherwise.</summary>
    public static (FilterOperator? Operator, object? Operand, FilterCombinator Combinator) InitialSecond(FilterSpec? current)
        => current is { Clauses: [{ Operator: not FilterOperator.In }, { Operator: not FilterOperator.In } second] }
            ? (second.Operator, second.Value, current.Combinator)
            : (null, null, FilterCombinator.And);

    private static IReadOnlyList<object?>? InForce(FilterSpec? current)
        => current is { Clauses: [{ Operator: FilterOperator.In, Values: { } applied }] } ? applied : null;

    /// <summary>
    /// The operator a condition form starts on: the one in force, where the column's filter
    /// is one or two conditions (the first of them); where a value list's answer was TooMany,
    /// <c>Contains</c> — the degraded form is Excel's own search box, where the type allows
    /// it; the first the column allows otherwise.
    /// </summary>
    public static FilterOperator InitialOperator(ColumnType type, FilterSpec? current, bool tooMany)
    {
        if (FirstCondition(current) is { } clause)
            return clause.Operator;
        var allowed = FilterOperators.AllowedFor(type);
        return tooMany && allowed.Contains(FilterOperator.Contains) ? FilterOperator.Contains : allowed[0];
    }

    /// <summary>The operand a condition form starts with: the one in force, where the
    /// column's filter is one or two conditions (the first's); null otherwise.</summary>
    public static object? InitialOperand(FilterSpec? current) => FirstCondition(current)?.Value;

    /// <summary>The first condition of a filter in force that is one or two conditions.</summary>
    private static FilterClause? FirstCondition(FilterSpec? current) => current switch
    {
        { Clauses: [{ Operator: not FilterOperator.In } only] } => only,
        { Clauses: [{ Operator: not FilterOperator.In } first, { Operator: not FilterOperator.In }] } => first,
        _ => null,
    };

    /// <summary>Whether the operator takes an operand — every one but <c>IsBlank</c> and
    /// <c>IsNotBlank</c>. A panel keeps Apply unavailable while one that does has none.</summary>
    public static bool TakesOperand(FilterOperator op) => op is not (FilterOperator.IsBlank or FilterOperator.IsNotBlank);

    /// <summary>
    /// A condition's filter, from its operator and its typed operand: one clause. An
    /// operator that takes an operand and has none is no filter at all (null) — the
    /// condition was cleared. <c>In</c> offered in a condition form is membership of the one
    /// value given, so its operand becomes a one-member list: a clause the engine reads from
    /// <c>Values</c> and would refuse with <c>Value</c> alone.
    /// </summary>
    public static FilterSpec? FromCondition(FilterOperator op, object? operand)
    {
        if (!TakesOperand(op))
            return new FilterSpec([new FilterClause(op)]);
        if (operand is null)
            return null;
        return op == FilterOperator.In
            ? new FilterSpec([new FilterClause(FilterOperator.In, Values: [operand])])
            : new FilterSpec([new FilterClause(op, operand)]);
    }

    /// <summary>
    /// A typed operand from what was typed, read in the current culture
    /// (<see cref="ReadOperand"/>): null where the text does not read, or reads two ways — an
    /// operand that cannot be read applies nothing, and the panel stands.
    /// </summary>
    public static object? ParseOperand(ColumnType type, string text, DateType dateType = DateType.DateTime)
        => ReadOperand(type, text, CultureInfo.CurrentCulture, dateType).Value;

    /// <summary>
    /// What a condition's typed operand reads as, for a panel whose value field is text: the
    /// engine compares typed values (ADR-0023). Empty text is no operand, not a refusal.
    /// <list type="bullet">
    /// <item><b>A number</b> is read in <paramref name="culture"/>, the culture the form shows
    /// numbers in (<see cref="OperandText"/>), and never in another culture's separators: a group
    /// separator stands only where the culture writes one, so <c>1234,5</c> under en-US is not
    /// 12345. A text that reads as two numbers is refused rather than guessed: under a culture
    /// that groups with a dot, <c>1.234</c> (ADR-0006, note of 2026-10-01; principle 1).</item>
    /// <item><b>A date</b> is read as a number is (ticket 97): ticket 94's ISO forms exactly, which
    /// a date operand reopens in, and anything else in <paramref name="culture"/> alone, never
    /// invariant first — under en-GB <c>05/01/2026</c> is 5 January. A numeric date whose year
    /// stands last under a culture that writes it first (ja-JP's <c>yyyy/MM/dd</c>) has no order
    /// of day and month from the culture, so where both orders are dates it reads two ways and is
    /// refused. The date read must then be of the column's declared <paramref name="dateType"/>
    /// (ADR-0023, section of 2026-10-02), or the text is refused as
    /// <see cref="OperandRefusal.NotTheColumnsDateForm"/>: a <see cref="DateType.DateOnly"/> takes
    /// a day with no time, a <see cref="DateType.DateTime"/> a date and time with no offset, and a
    /// <see cref="DateType.DateTimeOffset"/> a date and time with an explicit offset, written in
    /// ISO 8601's extended form (<c>2026-10-02T13:00:00+09:00</c>, <c>…Z</c>) or ticket 94's
    /// (<c>2026-10-02 13:00:00 +09:00</c>). An offset is read in those forms only.</item>
    /// <item><b>A boolean</b> is <c>true</c> or <c>false</c>; <b>text</b> is itself.</item>
    /// </list>
    /// </summary>
    public static OperandReading ReadOperand(ColumnType type, string text, CultureInfo culture, DateType dateType = DateType.DateTime)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(culture);
        if (text.Length == 0)
            return default;
        return type switch
        {
            ColumnType.Text => new OperandReading(text, OperandRefusal.None),
            ColumnType.Number => ReadNumber(text, culture.NumberFormat),
            ColumnType.Date => ReadDate(text, culture, dateType),
            ColumnType.Boolean => bool.TryParse(text, out var flag)
                ? new OperandReading(flag, OperandRefusal.None)
                : new OperandReading(null, OperandRefusal.NotReadable),
            _ => new OperandReading(null, OperandRefusal.NotReadable),
        };
    }

    /// <summary>
    /// The text a condition form shows an operand in, which <see cref="ReadOperand"/> reads back
    /// as the same value in <paramref name="culture"/> (ADR-0006, note of 2026-10-01; ticket 96):
    /// a number in the culture's own digits and decimal separator, ungrouped; a date or a time in
    /// its ISO form (ticket 94); anything else its own text. Empty for no operand.
    /// </summary>
    public static string OperandText(object? operand, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        return operand switch
        {
            null => "",
            decimal number => number.ToString(culture),
            double or float or int or long or short or byte or sbyte or uint or ulong or ushort =>
                Convert.ToDecimal(operand, CultureInfo.InvariantCulture).ToString(culture),
            _ => DisplayText.Of(operand),
        };
    }

    /// <summary>
    /// A value as a column with <paramref name="format"/> shows it in its cells (ADR-0006): the
    /// format where the column declares one, and without one a date's or a time's ISO form by
    /// type (note of 2026-10-01) or the value's own text — what a substituted panel's value list
    /// shows, so it lists each value as the cells and the built-in panel do.
    /// </summary>
    public static string ValueText(object value, Func<object, string>? format)
    {
        ArgumentNullException.ThrowIfNull(value);
        return format is { } declared ? declared(value) : DisplayText.Of(value);
    }

    /// <summary>
    /// Why a typed operand was refused, in the built-in Chrome's words: what the text reads as,
    /// and how to type it so it reads one way. Null where it was not refused. A Chrome with words
    /// of its own words <see cref="OperandRefusal"/> itself.
    /// </summary>
    public static string? RefusalText(OperandReading reading, string text, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(culture);
        return reading switch
        {
            { Refusal: OperandRefusal.ReadsTwoWays, OtherValue: (DateTime first, DateTime second) } => string.Format(culture,
                "\u201C{0}\u201D reads two ways: as {1}, and as {2}. Type the one you mean as {1} or {2}.",
                text, IsoDateText(first), IsoDateText(second)),
            { Refusal: OperandRefusal.ReadsTwoWays, OtherValue: var (asCulture, asPoint) } => string.Format(culture,
                "\u201C{0}\u201D reads two ways: as {1}, and as {2}. Type {1} without separators, or {3} for the other.",
                text, OperandText(asCulture, culture), OperandText(asPoint, CultureInfo.InvariantCulture), OperandText(asPoint, culture)),
            { Refusal: OperandRefusal.NotTheColumnsDateForm, OtherValue: (var read, DateType declared) } => DateFormRefusal(text, read, declared),
            { Refusal: OperandRefusal.NotReadable } => string.Format(culture,
                "\u201C{0}\u201D is not a value of this column as {1} writes it.", text, culture.DisplayName),
            _ => null,
        };
    }

    // Ticket 94's forms, which a date or a time operand reopens in: read exactly, in every culture.
    private static readonly string[] IsoDateForms = ["yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd"];

    // The forms an offset is read in (ADR-0023, section of 2026-10-02): ticket 94's, which a
    // DateTimeOffset reopens in, and ISO 8601's extended form, with an offset or Z.
    private static readonly string[] IsoOffsetForms =
        ["yyyy-MM-dd HH:mm:ss zzz", "yyyy-MM-dd'T'HH:mm:ssK", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK", "yyyy-MM-dd'T'HH:mmK"];

    // A date written as three runs of digits — day, month and year in some order — with a time or
    // nothing after it.
    private static readonly System.Text.RegularExpressions.Regex NumericDate =
        new(@"^(\d{1,4})[./\-](\d{1,2})[./\-](\d{1,4})(?=$|[\sT])", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>
    /// A date read as ticket 97 decides: ticket 94's ISO forms exactly, then the culture alone. A
    /// numeric date is read in the order its year stands in: first is year, month, day, as every
    /// culture that writes the year first writes it; last is the culture's own order of day and
    /// month where the culture writes the year last too. Where it writes the year first, a date
    /// with its year last has no order from the culture, so where day and month could be either it
    /// reads two ways, and is refused rather than guessed.
    /// </summary>
    private static OperandReading ReadDate(string text, CultureInfo culture, DateType dateType)
    {
        var trimmed = text.Trim();
        // An ISO 8601 offset needs a time with it, and Z is UTC; an offset-less text must not
        // match here, where K would read it as no offset at all.
        if (DateTimeOffset.TryParseExact(trimmed, IsoOffsetForms, CultureInfo.InvariantCulture, DateTimeStyles.None, out var offset)
            && (trimmed.EndsWith('Z') || trimmed.LastIndexOfAny(['+', '-']) > 10))
            return AsDeclared(offset, hasTime: true, dateType);
        if (DateTime.TryParseExact(trimmed, IsoDateForms, CultureInfo.InvariantCulture, DateTimeStyles.None, out var iso))
            return AsDeclared(iso, hasTime: trimmed.Length > "yyyy-MM-dd".Length, dateType);
        if (!DateTime.TryParse(trimmed, culture, DateTimeStyles.None, out var local))
            return new OperandReading(null, OperandRefusal.NotReadable);
        // The culture's reading carried an offset or a Z, and .NET converted it to this machine's
        // clock — on the Server host, the server's. An offset is read in the ISO forms only, so
        // this one is refused, naming what it read as (ADR-0023, section of 2026-10-02).
        if (local.Kind != DateTimeKind.Unspecified)
        {
            return DateTimeOffset.TryParse(trimmed, culture, DateTimeStyles.None, out var stated)
                ? new OperandReading(null, OperandRefusal.NotTheColumnsDateForm, (stated, dateType))
                : new OperandReading(null, OperandRefusal.NotReadable);
        }
        var reading = ReadInCulture(trimmed, culture, local);
        if (reading.Value is not DateTime date)
            return reading;
        // Whether a time was written, told from the text: once read, 00:00 typed and no time at
        // all are the same DateTime. A numeric date has a time where text follows it; any other
        // has one where it does not read as a day alone.
        var numeric = NumericDate.Match(trimmed);
        var hasTime = numeric.Success ? numeric.Length < trimmed.Length : !DateOnly.TryParse(trimmed, culture, DateTimeStyles.None, out _);
        return AsDeclared(date, hasTime, dateType);
    }

    /// <summary>A date typed without an offset, read in the culture alone (ticket 97).</summary>
    private static OperandReading ReadInCulture(string trimmed, CultureInfo culture, DateTime local)
    {
        var numeric = NumericDate.Match(trimmed);
        // Month names, or no year: the culture's own reading is the only one.
        if (!numeric.Success || numeric.Groups[1].Length != 4 && numeric.Groups[3].Length != 4)
            return new OperandReading(local, OperandRefusal.None);
        var (first, second, third) = (int.Parse(numeric.Groups[1].Value, CultureInfo.InvariantCulture),
            int.Parse(numeric.Groups[2].Value, CultureInfo.InvariantCulture), int.Parse(numeric.Groups[3].Value, CultureInfo.InvariantCulture));
        if (numeric.Groups[1].Length == 4)
            return DateOf(first, second, third, local) is { } yearFirst
                ? new OperandReading(yearFirst, OperandRefusal.None)
                : new OperandReading(null, OperandRefusal.NotReadable);
        var pattern = culture.DateTimeFormat.ShortDatePattern;
        var yearFirstHere = pattern.IndexOf('y', StringComparison.Ordinal) < pattern.IndexOf('M', StringComparison.Ordinal);
        var dayFirstHere = pattern.IndexOf('d', StringComparison.Ordinal) < pattern.IndexOf('M', StringComparison.Ordinal);
        if (!yearFirstHere)
        {
            return (dayFirstHere ? DateOf(third, second, first, local) : DateOf(third, first, second, local)) is { } inOrder
                ? new OperandReading(inOrder, OperandRefusal.None)
                : new OperandReading(null, OperandRefusal.NotReadable);
        }
        var monthFirst = DateOf(third, first, second, local);
        var dayFirst = DateOf(third, second, first, local);
        return (monthFirst, dayFirst) switch
        {
            ({ } m, { } d) when m != d => new OperandReading(null, OperandRefusal.ReadsTwoWays, (m, d)),
            ({ } m, _) => new OperandReading(m, OperandRefusal.None),
            (_, { } d) => new OperandReading(d, OperandRefusal.None),
            _ => new OperandReading(null, OperandRefusal.NotReadable),
        };
    }

    /// <summary>
    /// A date read, held to the column's declared type (ADR-0023, section of 2026-10-02): a
    /// <see cref="DateType.DateOnly"/> takes a day with no time written — not even midnight, which
    /// would be a time cut to its day — a <see cref="DateType.DateTime"/> takes no offset, and a
    /// <see cref="DateType.DateTimeOffset"/> needs one. A refusal carries what was read and the
    /// declared type, for its words.
    /// </summary>
    private static OperandReading AsDeclared(object read, bool hasTime, DateType dateType) => (dateType, read) switch
    {
        (DateType.DateTime, DateTime) => new OperandReading(read, OperandRefusal.None),
        (DateType.DateOnly, DateTime day) when !hasTime => new OperandReading(DateOnly.FromDateTime(day), OperandRefusal.None),
        (DateType.DateTimeOffset, DateTimeOffset) => new OperandReading(read, OperandRefusal.None),
        (DateType.DateTime or DateType.DateOnly or DateType.DateTimeOffset, _)
            => new OperandReading(null, OperandRefusal.NotTheColumnsDateForm, (read, dateType)),
        _ => throw new ArgumentOutOfRangeException(nameof(dateType), dateType, null),
    };

    /// <summary>Why a date is not the column's declared type, naming the form to type
    /// (ADR-0023, section of 2026-10-02).</summary>
    private static string DateFormRefusal(string text, object read, DateType declared) => (declared, read) switch
    {
        (DateType.DateOnly, DateTimeOffset stated) => $"\u201C{text}\u201D has an offset, and this column holds days. Type the day alone, as {Iso(stated.DateTime, "yyyy-MM-dd")}.",
        (DateType.DateOnly, DateTime date) => $"\u201C{text}\u201D has a time, and this column holds days. Type the day alone, as {Iso(date, "yyyy-MM-dd")}.",
        (DateType.DateTime, DateTimeOffset stated) => $"\u201C{text}\u201D has an offset, and this column's dates have none. Type it without one, as {IsoDateText(stated.DateTime)}.",
        (DateType.DateTimeOffset, DateTimeOffset stated) => $"\u201C{text}\u201D is not written in an ISO form. Type it as {stated.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture)}.",
        (DateType.DateTimeOffset, DateTime date) => $"\u201C{text}\u201D has no offset, and this column holds moments. Type it with one, as {Iso(date, "yyyy-MM-dd'T'HH:mm:ss")}+hh:mm, or with Z for UTC.",
        _ => $"\u201C{text}\u201D is not a date of this column.",
    };

    private static string Iso(DateTime date, string format) => date.ToString(format, CultureInfo.InvariantCulture);

    /// <summary>The date of <paramref name="year"/>, <paramref name="month"/> and
    /// <paramref name="day"/> at <paramref name="time"/>'s time of day, or null where there is
    /// no such date.</summary>
    private static DateTime? DateOf(int year, int month, int day, DateTime time)
        => year is >= 1 and <= 9999 && month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year, month)
            ? new DateTime(year, month, day).Add(time.TimeOfDay)
            : null;

    /// <summary>A date as a refusal names it, in a form that reads back exactly: the day alone at
    /// midnight, the day and time otherwise.</summary>
    private static string IsoDateText(DateTime date)
        => date.ToString(date.TimeOfDay == TimeSpan.Zero ? "yyyy-MM-dd" : "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    /// <summary>
    /// A number read in the culture, with its grouping checked (ADR-0006, note of 2026-10-01).
    /// .NET reads a group separator anywhere — <c>12,34</c> as 1234 under en-US — so a text that
    /// carries one must group as the culture writes it. A space-like group separator (fr-FR's
    /// narrow no-break space) is also typed as a space or a no-break space, so those stand for it.
    /// </summary>
    private static OperandReading ReadNumber(string text, NumberFormatInfo format)
    {
        var group = format.NumberGroupSeparator;
        var normalised = group is "\u0020" or "\u00A0" or "\u202F"
            ? text.Trim().Replace("\u0020", group).Replace("\u00A0", group).Replace("\u202F", group)
            : text.Trim();
        if (!decimal.TryParse(normalised, NumberStyles.Number, format, out var value) || !GroupedAsWritten(normalised, value, format))
            return new OperandReading(null, OperandRefusal.NotReadable);
        // A dot that groups here is the decimal point in the invariant culture, and in the grid's
        // panels before ticket 96: where the text has no decimal separator of the culture's own,
        // it may have meant either.
        if (group == "." && normalised.Contains('.') && !normalised.Contains(format.NumberDecimalSeparator, StringComparison.Ordinal)
            && decimal.TryParse(normalised, NumberStyles.Number, CultureInfo.InvariantCulture, out var asPoint) && asPoint != value)
        {
            return new OperandReading(null, OperandRefusal.ReadsTwoWays, (value, asPoint));
        }
        return new OperandReading(value, OperandRefusal.None);
    }

    /// <summary>Whether a number's text groups its integer digits as the culture writes them,
    /// where it groups them at all; a separator after the decimal point is never grouping.</summary>
    private static bool GroupedAsWritten(string text, decimal value, NumberFormatInfo format)
    {
        var group = format.NumberGroupSeparator;
        if (group.Length == 0 || !text.Contains(group, StringComparison.Ordinal))
            return true;
        var body = text;
        foreach (var sign in new[] { format.NegativeSign, format.PositiveSign })
        {
            if (sign.Length == 0)
                continue;
            if (body.StartsWith(sign, StringComparison.Ordinal))
                body = body[sign.Length..].TrimStart();
            if (body.EndsWith(sign, StringComparison.Ordinal))
                body = body[..^sign.Length].TrimEnd();
        }
        var point = body.IndexOf(format.NumberDecimalSeparator, StringComparison.Ordinal);
        if (point >= 0 && body[point..].Contains(group, StringComparison.Ordinal))
            return false;
        var integer = point < 0 ? body : body[..point];
        return integer == decimal.Truncate(Math.Abs(value)).ToString("N0", format);
    }
}
