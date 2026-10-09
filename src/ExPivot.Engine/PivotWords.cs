using System.Globalization;

namespace ExPivot.Engine;

/// <summary>
/// The words ExPivot paints, by id, with their English (ADR-0060). A Consumer replaces any of
/// them through a function from id to text, returning null to keep the English. A word with
/// <c>{0}</c> (and <c>{1}</c>) is a template: the id says what goes in it.
///
/// <para>The words of Excel's Japanese edition are bundled: <see cref="Japanese"/> is such a
/// function, chosen in one line (<c>Label="PivotWords.Japanese"</c>). They never follow the
/// culture on their own, because a screen whose language changed unasked is the surprise the
/// family avoids (ADR-0060).</para>
/// </summary>
public static class PivotWords
{
    /// <summary>The Compact form's label column header: <c>Row Labels</c>.</summary>
    public const string RowLabels = "row-labels";

    /// <summary>The Σ Values pseudo-field's header, and a report's lone value column when the
    /// values stand in rows: <c>Values</c>.</summary>
    public const string Values = "values";

    /// <summary>The grand total row and column: <c>Grand Total</c>.</summary>
    public const string GrandTotal = "grand-total";

    /// <summary>An outer Item's subtotal: <c>{0} Total</c>, {0} the Item's label.</summary>
    public const string ItemTotal = "item-total";

    /// <summary>A Value Field's grand total row, with the values in rows: <c>Total {0}</c>, {0} its caption.</summary>
    public const string TotalOf = "total-of";

    /// <summary>An outer Item's subtotal row for one Value Field: <c>{0} {1}</c>, {0} the Item's
    /// label and {1} the caption.</summary>
    public const string ItemValue = "item-value";

    /// <summary>The Blank Item: <c>(blank)</c>.</summary>
    public const string Blank = "blank";

    /// <summary>A report filter with nothing hidden: <c>(All)</c>.</summary>
    public const string All = "all";

    /// <summary>A report filter showing several Items: <c>(Multiple Items)</c>.</summary>
    public const string MultipleItems = "multiple-items";

    /// <summary>The Σ Values entry in an Area: <c>Σ Values</c>.</summary>
    public const string ValuesPseudoField = "values-pseudo-field";

    /// <summary>The id of an Aggregation's name — <c>aggregation-sum</c> is <c>Sum</c>.</summary>
    public static string AggregationName(PivotAggregation aggregation) => "aggregation-" + Slug(aggregation);

    /// <summary>The id of a Value Field's default caption — <c>caption-sum</c> is
    /// <c>Sum of {0}</c>, {0} the field's caption.</summary>
    public static string CaptionOf(PivotAggregation aggregation) => "caption-" + Slug(aggregation);

    /// <summary>The id of a Show Values As choice's name — <c>show-percent-of-grand-total</c> is
    /// <c>% of Grand Total</c>.</summary>
    public static string ShowValuesAsName(PivotShowValuesAs showAs) => showAs switch
    {
        PivotShowValuesAs.NoCalculation => "show-no-calculation",
        PivotShowValuesAs.PercentOfGrandTotal => "show-percent-of-grand-total",
        PivotShowValuesAs.PercentOfColumnTotal => "show-percent-of-column-total",
        PivotShowValuesAs.PercentOfRowTotal => "show-percent-of-row-total",
        _ => throw new ArgumentOutOfRangeException(nameof(showAs), showAs, "Unknown PivotShowValuesAs."),
    };

    /// <summary>The English for an id, or null for an id this class does not know.</summary>
    public static string? EnglishFor(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return English.TryGetValue(id, out var word) ? word : null;
    }

    /// <summary>Every id this class has an English word for — what a Consumer replaces to give
    /// ExPivot another language.</summary>
    public static IReadOnlyCollection<string> Ids => English.Keys;

    /// <summary>
    /// The words of Excel's Japanese edition (ADR-0060): <c>行ラベル</c>, <c>総計</c>,
    /// <c>合計 / 金額</c>, <c>(空白)</c>, <c>ピボットテーブルのフィールド</c> and every other word
    /// ExPivot paints, with the ExGrid commands of the report's Context Menu, which ExGrid words
    /// only in English, and a date part's Items (<see cref="PivotDateWords"/>): <c>2026年</c>,
    /// <c>第3四半期</c>, <c>9月</c>. A function from id to word, to hand to ExPivot's <c>Label</c> — or to
    /// <see cref="PivotOptions.Label"/> on a server — in one line. It answers null for an id it
    /// does not know, which keeps the English.
    /// </summary>
    public static Func<string, string?> Japanese { get; } = JapaneseFor;

    /// <summary>The Japanese edition's word for an id, or null for an id it has none for.</summary>
    public static string? JapaneseFor(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return JapaneseWords.TryGetValue(id, out var word) ? word : null;
    }

    /// <summary>The word for <paramref name="id"/>: the Consumer's through
    /// <paramref name="label"/>, the English where it says nothing, and the id itself where
    /// neither knows it — an id painted is a missing word seen, never a blank.</summary>
    public static string Resolve(string id, Func<string, string?>? label)
        => label?.Invoke(id) ?? EnglishFor(id) ?? id;

    /// <summary>A template word with its arguments put in, under the invariant culture — the
    /// arguments are already text.</summary>
    public static string Fill(string template, params string[] arguments)
        => string.Format(CultureInfo.InvariantCulture, template, arguments);

    private static string Slug(PivotAggregation aggregation) => aggregation switch
    {
        PivotAggregation.Sum => "sum",
        PivotAggregation.Count => "count",
        PivotAggregation.Average => "average",
        PivotAggregation.Max => "max",
        PivotAggregation.Min => "min",
        PivotAggregation.Product => "product",
        PivotAggregation.CountNumbers => "count-numbers",
        PivotAggregation.StdDev => "stddev",
        PivotAggregation.StdDevp => "stddevp",
        PivotAggregation.Var => "var",
        PivotAggregation.Varp => "varp",
        _ => throw new ArgumentOutOfRangeException(nameof(aggregation), aggregation, "Unknown PivotAggregation."),
    };

    private static readonly Dictionary<string, string> English = new(StringComparer.Ordinal)
    {
        [RowLabels] = "Row Labels",
        [Values] = "Values",
        [GrandTotal] = "Grand Total",
        [ItemTotal] = "{0} Total",
        [TotalOf] = "Total {0}",
        [ItemValue] = "{0} {1}",
        [Blank] = "(blank)",
        [All] = "(All)",
        [MultipleItems] = "(Multiple Items)",
        [ValuesPseudoField] = "Σ Values",

        ["aggregation-sum"] = "Sum",
        ["aggregation-count"] = "Count",
        ["aggregation-average"] = "Average",
        ["aggregation-max"] = "Max",
        ["aggregation-min"] = "Min",
        ["aggregation-product"] = "Product",
        ["aggregation-count-numbers"] = "Count Numbers",
        ["aggregation-stddev"] = "StdDev",
        ["aggregation-stddevp"] = "StdDevp",
        ["aggregation-var"] = "Var",
        ["aggregation-varp"] = "Varp",

        ["caption-sum"] = "Sum of {0}",
        ["caption-count"] = "Count of {0}",
        ["caption-average"] = "Average of {0}",
        ["caption-max"] = "Max of {0}",
        ["caption-min"] = "Min of {0}",
        ["caption-product"] = "Product of {0}",
        ["caption-count-numbers"] = "Count Numbers of {0}",
        ["caption-stddev"] = "StdDev of {0}",
        ["caption-stddevp"] = "StdDevp of {0}",
        ["caption-var"] = "Var of {0}",
        ["caption-varp"] = "Varp of {0}",

        ["show-no-calculation"] = "No Calculation",
        ["show-percent-of-grand-total"] = "% of Grand Total",
        ["show-percent-of-column-total"] = "% of Column Total",
        ["show-percent-of-row-total"] = "% of Row Total",

        // The Field List and its panels (ADR-0061).
        ["field-list"] = "PivotTable Fields",
        ["choose-fields"] = "Choose fields to add to report:",
        ["drag-fields"] = "Drag fields between areas below:",
        ["search-fields"] = "Search",
        ["area-filters"] = "Filters",
        ["area-columns"] = "Columns",
        ["area-rows"] = "Rows",
        ["area-values"] = "Values",
        ["field-menu"] = "Options for {0}",
        ["filtered"] = "Filtered",
        ["drop-here"] = "Drop here",
        ["move-up"] = "Move Up",
        ["move-down"] = "Move Down",
        ["move-to-beginning"] = "Move to Beginning",
        ["move-to-end"] = "Move to End",
        ["move-to-filters"] = "Move to Report Filter",
        ["move-to-rows"] = "Move to Row Labels",
        ["move-to-columns"] = "Move to Column Labels",
        ["move-to-values"] = "Move to Values",
        ["remove-field"] = "Remove Field",
        ["field-settings"] = "Field Settings…",
        ["value-field-settings"] = "Value Field Settings…",
        ["sort-ascending"] = "Sort A to Z",
        ["sort-descending"] = "Sort Z to A",
        ["filter-items"] = "Filter…",
        ["expand-field"] = "Expand Entire Field",
        ["collapse-field"] = "Collapse Entire Field",
        ["ok"] = "OK",
        ["cancel"] = "Cancel",
        ["select-all"] = "(Select All)",
        ["search-items"] = "Search",
        ["too-many-items"] = "More than {0} items. Search to narrow the list.",
        ["no-items-match"] = "No items match.",
        ["custom-name"] = "Custom Name",
        ["summarize-by"] = "Summarize value field by",
        ["show-values-as"] = "Show values as",
        ["number-format"] = "Number format",
        ["number-format-general"] = "General",
        ["sample"] = "Sample: {0}",
        ["subtotals"] = "Subtotals",
        ["subtotals-automatic"] = "Automatic",
        ["subtotals-none"] = "None",
        ["sort-order"] = "Sort",
        ["sort-label-ascending"] = "Ascending (A to Z) by label",
        ["sort-label-descending"] = "Descending (Z to A) by label",
        ["sort-value-ascending"] = "Ascending by {0}",
        ["sort-value-descending"] = "Descending by {0}",
        ["refused-hides-every-item"] = "Select at least one item.",
        ["refused-caption-taken"] = "PivotTable field name already exists.",
        ["refused-caption-empty"] = "Enter a name.",
        ["refused-number-format"] = "This number format cannot be used.",
        ["empty-report"] = "To build a report, choose fields from the PivotTable Fields list.",
        ["report-filters"] = "Report filters",
        ["filter-of"] = "Filter {0}",

        // The report's Context Menu and its label cells (ADR-0059/0063).
        ["expand"] = "Expand",
        ["collapse"] = "Collapse",
        ["expand-item"] = "Expand {0}",
        ["collapse-item"] = "Collapse {0}",
        ["show-details"] = "Show Details",
        ["sort-smallest-to-largest"] = "Sort Smallest to Largest",
        ["sort-largest-to-smallest"] = "Sort Largest to Smallest",
        ["remove-named-field"] = "Remove \"{0}\"",
        ["show-field-list"] = "Show Field List",
        ["hide-field-list"] = "Hide Field List",

        // The Pivot Toolbar above the report and its Layout menu, Excel's Design tab (ADR-0061).
        ["layout-menu"] = "Layout",
        ["grand-totals"] = "Grand Totals",
        ["report-layout"] = "Report Layout",
        ["subtotals-do-not-show"] = "Do Not Show Subtotals",
        ["subtotals-at-bottom"] = "Show all Subtotals at Bottom of Group",
        ["subtotals-at-top"] = "Show all Subtotals at Top of Group",
        ["grand-totals-off"] = "Off for Rows and Columns",
        ["grand-totals-on"] = "On for Rows and Columns",
        ["grand-totals-rows-only"] = "On for Rows Only",
        ["grand-totals-columns-only"] = "On for Columns Only",
        ["form-compact"] = "Show in Compact Form",
        ["form-outline"] = "Show in Outline Form",
        ["form-tabular"] = "Show in Tabular Form",
        ["repeat-item-labels"] = "Repeat All Item Labels",
        ["do-not-repeat-item-labels"] = "Do Not Repeat Item Labels",
        ["refresh"] = "Refresh",
        ["field-list-toggle"] = "Field List",

        // Asking the Pivot Source, Defer Layout Update and the caps (ADR-0061/0066).
        ["loading"] = "Loading…",
        ["defer-layout-update"] = "Defer Layout Update",
        ["update"] = "Update",
        ["refused-too-many-cells"] = "This layout needs more than {0} cells.",
        ["refused-too-many-rows"] = "This layout needs more than {0} rows.",
        ["refused-too-many-columns"] = "This layout needs more than {0} columns.",
        ["refused-out-of-memory"] = "Memory ran out while this layout was laid out.",
        ["aggregation-not-offered"] = "The source does not answer {0}.",
        ["source-failed"] = "The source could not answer: {0}",
        ["source-refused"] = "The source refused to answer: {0}",
        ["data-changed"] = "The data has changed — refresh.",

        // A Copy or a Selection Summary the source answered for another Report Version, or in part:
        // refused, and said where the grid says a refusal (ADR-0152).
        ["copy-another-version"] = "The copy answered another Report Version.",
        ["copy-missing-range"] = "The copy did not answer every selected range.",
        ["copy-incomplete-range"] = "The copy returned an incomplete selected range.",
        ["summary-another-version"] = "The summary answered another Report Version.",
        ["summary-inconsistent-errors"] = "The summary returned inconsistent error counts.",

        // Show Details: the tabs at the report's foot and the dialog (ADR-0059).
        ["details-title"] = "Details: {0}",
        ["report-tab"] = "PivotTable",
        ["sheets"] = "Sheets",
        ["close-tab"] = "Close {0}",
        ["close"] = "Close",

        // The Stale Report's notice (ADR-0067): {0} the time of the version shown, {1} what
        // happened; each reason's {0} its cap or the source's own sentence.
        ["stale-report"] = "Showing the data as of {0}: {1}",
        ["stale-too-many-cells"] = "the newest data needs more than {0} cells.",
        ["stale-too-many-rows"] = "the newest data needs more than {0} rows.",
        ["stale-too-many-columns"] = "the newest data needs more than {0} columns.",
        ["stale-source-failed"] = "the source could not answer: {0}",
        ["stale-source-refused"] = "the source refused to answer: {0}",
        ["stale-out-of-memory"] = "memory ran out while the newest data was laid out.",
        ["retry"] = "Retry",
    };

    // The words of Excel's Japanese edition (ADR-0060), for every id above, and for the ExGrid
    // commands in the report's Context Menu (copy, copy-with-headers), which ExGrid words only
    // in English. Where Excel shows the words, they are Excel's; where ExPivot paints something
    // Excel has no counterpart for (a refusal, a tab's close button), they are written in the
    // same register.
    private static readonly Dictionary<string, string> JapaneseWords = new(StringComparer.Ordinal)
    {
        [RowLabels] = "行ラベル",
        [Values] = "値",
        [GrandTotal] = "総計",
        [ItemTotal] = "{0} 集計",
        [TotalOf] = "全体の {0}",
        [ItemValue] = "{0} {1}",
        [Blank] = "(空白)",
        [All] = "(すべて)",
        [MultipleItems] = "(複数のアイテム)",
        [ValuesPseudoField] = "Σ 値",

        ["aggregation-sum"] = "合計",
        ["aggregation-count"] = "データの個数",
        ["aggregation-average"] = "平均",
        ["aggregation-max"] = "最大",
        ["aggregation-min"] = "最小",
        ["aggregation-product"] = "積",
        ["aggregation-count-numbers"] = "数値の個数",
        ["aggregation-stddev"] = "標本標準偏差",
        ["aggregation-stddevp"] = "標準偏差",
        ["aggregation-var"] = "標本分散",
        ["aggregation-varp"] = "分散",

        ["caption-sum"] = "合計 / {0}",
        ["caption-count"] = "データの個数 / {0}",
        ["caption-average"] = "平均 / {0}",
        ["caption-max"] = "最大 / {0}",
        ["caption-min"] = "最小 / {0}",
        ["caption-product"] = "積 / {0}",
        ["caption-count-numbers"] = "数値の個数 / {0}",
        ["caption-stddev"] = "標本標準偏差 / {0}",
        ["caption-stddevp"] = "標準偏差 / {0}",
        ["caption-var"] = "標本分散 / {0}",
        ["caption-varp"] = "分散 / {0}",

        ["show-no-calculation"] = "計算なし",
        ["show-percent-of-grand-total"] = "総計に対する比率",
        ["show-percent-of-column-total"] = "列集計に対する比率",
        ["show-percent-of-row-total"] = "行集計に対する比率",

        ["field-list"] = "ピボットテーブルのフィールド",
        ["choose-fields"] = "レポートに追加するフィールドを選択してください:",
        ["drag-fields"] = "次のボックス間でフィールドをドラッグしてください:",
        ["search-fields"] = "検索",
        ["area-filters"] = "フィルター",
        ["area-columns"] = "列",
        ["area-rows"] = "行",
        ["area-values"] = "値",
        ["field-menu"] = "{0} のオプション",
        ["filtered"] = "フィルター適用",
        ["drop-here"] = "ここにドロップ",
        ["move-up"] = "上へ移動",
        ["move-down"] = "下へ移動",
        ["move-to-beginning"] = "先頭へ移動",
        ["move-to-end"] = "末尾へ移動",
        ["move-to-filters"] = "レポート フィルターに移動",
        ["move-to-rows"] = "行ラベルに移動",
        ["move-to-columns"] = "列ラベルに移動",
        ["move-to-values"] = "値に移動",
        ["remove-field"] = "フィールドの削除",
        ["field-settings"] = "フィールドの設定...",
        ["value-field-settings"] = "値フィールドの設定...",
        ["sort-ascending"] = "昇順",
        ["sort-descending"] = "降順",
        ["filter-items"] = "フィルター...",
        ["expand-field"] = "フィールド全体の展開",
        ["collapse-field"] = "フィールド全体の折りたたみ",
        ["ok"] = "OK",
        ["cancel"] = "キャンセル",
        ["select-all"] = "(すべて選択)",
        ["search-items"] = "検索",
        ["too-many-items"] = "{0} 個を超えるアイテムがあります。検索して一覧を絞り込んでください。",
        ["no-items-match"] = "一致するアイテムはありません。",
        ["custom-name"] = "名前の指定",
        ["summarize-by"] = "値フィールドの集計",
        ["show-values-as"] = "計算の種類",
        ["number-format"] = "表示形式",
        ["number-format-general"] = "標準",
        ["sample"] = "サンプル: {0}",
        ["subtotals"] = "小計",
        ["subtotals-automatic"] = "自動",
        ["subtotals-none"] = "なし",
        ["sort-order"] = "並べ替え",
        ["sort-label-ascending"] = "ラベルの昇順",
        ["sort-label-descending"] = "ラベルの降順",
        ["sort-value-ascending"] = "{0} の昇順",
        ["sort-value-descending"] = "{0} の降順",
        ["refused-hides-every-item"] = "少なくとも 1 つのアイテムを選択してください。",
        ["refused-caption-taken"] = "そのピボットテーブルのフィールド名は既に存在します。",
        ["refused-caption-empty"] = "名前を入力してください。",
        ["refused-number-format"] = "この表示形式は使用できません。",
        ["empty-report"] = "レポートを作成するには、[ピボットテーブルのフィールド] リストからフィールドを選択してください。",
        ["report-filters"] = "レポート フィルター",
        ["filter-of"] = "{0} のフィルター",

        ["expand"] = "展開",
        ["collapse"] = "折りたたみ",
        ["expand-item"] = "{0} を展開",
        ["collapse-item"] = "{0} を折りたたむ",
        ["show-details"] = "詳細の表示",
        ["sort-smallest-to-largest"] = "昇順",
        ["sort-largest-to-smallest"] = "降順",
        ["remove-named-field"] = "\"{0}\" の削除",
        ["show-field-list"] = "フィールド リストを表示する",
        ["hide-field-list"] = "フィールド リストを表示しない",

        ["layout-menu"] = "レイアウト",
        ["grand-totals"] = "総計",
        ["report-layout"] = "レポートのレイアウト",
        ["subtotals-do-not-show"] = "小計を表示しない",
        ["subtotals-at-bottom"] = "すべての小計をグループの末尾に表示する",
        ["subtotals-at-top"] = "すべての小計をグループの先頭に表示する",
        ["grand-totals-off"] = "行と列の集計を行わない",
        ["grand-totals-on"] = "行と列の集計を行う",
        ["grand-totals-rows-only"] = "行のみ集計を行う",
        ["grand-totals-columns-only"] = "列のみ集計を行う",
        ["form-compact"] = "コンパクト形式で表示",
        ["form-outline"] = "アウトライン形式で表示",
        ["form-tabular"] = "表形式で表示",
        ["repeat-item-labels"] = "アイテムのラベルをすべて繰り返す",
        ["do-not-repeat-item-labels"] = "アイテムのラベルを繰り返さない",
        ["refresh"] = "更新",
        ["field-list-toggle"] = "フィールド リスト",

        ["loading"] = "読み込み中...",
        ["defer-layout-update"] = "レイアウトの更新を保留する",
        ["update"] = "更新",
        ["refused-too-many-cells"] = "このレイアウトには {0} 個を超えるセルが必要です。",
        ["refused-too-many-rows"] = "このレイアウトには {0} を超える行が必要です。",
        ["refused-too-many-columns"] = "このレイアウトには {0} を超える列が必要です。",
        ["refused-out-of-memory"] = "このレイアウトを配置する途中でメモリが不足しました。",
        ["aggregation-not-offered"] = "ソースは {0} に対応していません。",
        ["source-failed"] = "ソースから応答を得られませんでした: {0}",
        ["source-refused"] = "ソースが応答を拒否しました: {0}",
        ["data-changed"] = "データが変更されました。更新してください。",

        ["copy-another-version"] = "コピーへの応答が、別のバージョンのレポートのものでした。",
        ["copy-missing-range"] = "コピーへの応答に、選択したすべての範囲が含まれていませんでした。",
        ["copy-incomplete-range"] = "コピーへの応答に、欠けのある選択範囲が含まれていました。",
        ["summary-another-version"] = "集計への応答が、別のバージョンのレポートのものでした。",
        ["summary-inconsistent-errors"] = "集計への応答で、エラーの数が食い違っていました。",

        ["details-title"] = "詳細: {0}",
        ["report-tab"] = "ピボットテーブル",
        ["sheets"] = "シート",
        ["close-tab"] = "{0} を閉じる",
        ["close"] = "閉じる",

        ["stale-report"] = "{0} 時点のデータを表示しています: {1}",
        ["stale-too-many-cells"] = "最新のデータには {0} 個を超えるセルが必要です。",
        ["stale-too-many-rows"] = "最新のデータには {0} を超える行が必要です。",
        ["stale-too-many-columns"] = "最新のデータには {0} を超える列が必要です。",
        ["stale-source-failed"] = "ソースから応答を得られませんでした: {0}",
        ["stale-source-refused"] = "ソースが応答を拒否しました: {0}",
        ["stale-out-of-memory"] = "最新のデータを配置する途中でメモリが不足しました。",
        ["retry"] = "再試行",

        // ExGrid's commands in the report's Context Menu (ExGrid.Chrome.GridCommandIds).
        ["copy"] = "コピー",
        ["copy-with-headers"] = "見出し付きでコピー",

        // A date part's Items (PivotDateWords), as the Japanese edition's date grouping labels them.
        [PivotDateWords.Year] = "{0}年",
        [PivotDateWords.Quarter] = "第{0}四半期",
        ["date-month-1"] = "1月",
        ["date-month-2"] = "2月",
        ["date-month-3"] = "3月",
        ["date-month-4"] = "4月",
        ["date-month-5"] = "5月",
        ["date-month-6"] = "6月",
        ["date-month-7"] = "7月",
        ["date-month-8"] = "8月",
        ["date-month-9"] = "9月",
        ["date-month-10"] = "10月",
        ["date-month-11"] = "11月",
        ["date-month-12"] = "12月",
    };
}
