// The words ExChart's own markup shows, by id, with their English (ADR-0370 section 10). A Consumer
// replaces any of them through a function from id to text, answering undefined to keep the English;
// ExChart's Japanese is bundled and chosen in one line, `label: japaneseChartWords`, never by the locale.
// A word with {0} (and {1}) is a template, filled as .NET's string.Format fills one. The sentences the
// layout writes (Notes, the tooltip's notes, the summary) are the engine's, in English, as C#'s are.

/** The Consumer's word for an id, or `undefined` to keep the English. */
export type ChartLabel = (id: string) => string | undefined;

/** The ids of the words ExChart's built-in markup shows. A command's id is its command id (`chartCommandIds`). */
export const chartWordIds = Object.freeze({
    /** Opens or closes Chart Filters (ADR-0273): `Chart Filters`. */
    filters: 'exchart.filters',
    /** Returns the range to the whole axis: `Reset range`. */
    resetRange: 'exchart.reset-range',
    /** Shows the chart's data as a table (ADR-0275): `View as table`. */
    table: 'exchart.table',
    /** The menu's word for the table while it is shown: `Hide table`. */
    hideTable: 'exchart.table.hide',
    /** The toolbar's CSV button (ADR-0282): `CSV`. */
    downloadCsv: 'exchart.download.csv',
    /** The toolbar's SVG button: `SVG`. */
    downloadSvg: 'exchart.download.svg',
    /** The toolbar's PNG button: `PNG`. */
    downloadPng: 'exchart.download.png',
    /** The menu's CSV command: `Download CSV`. */
    menuDownloadCsv: 'exchart.menu.download.csv',
    /** The menu's SVG command: `Download SVG`. */
    menuDownloadSvg: 'exchart.menu.download.svg',
    /** The menu's PNG command: `Download PNG`. */
    menuDownloadPng: 'exchart.menu.download.png',
    /** The downloads' group name: `Download`. */
    downloads: 'exchart.downloads',
    /** The chart's name where it has no title: `Chart`. */
    chart: 'exchart.chart',
    /** The table's caption where the chart has no title: `Chart data`. */
    chartData: 'exchart.chart-data',
    /** Chart Filters' start: `From`. */
    from: 'exchart.filters.from',
    /** Chart Filters' end: `To`. */
    to: 'exchart.filters.to',
    /** Applies the typed range: `Apply`. */
    apply: 'exchart.filters.apply',
    /** The axis switch: `Axis`. */
    axis: 'exchart.filters.axis',
    /** A proportional axis: `Proportional`. */
    proportional: 'exchart.filters.proportional',
    /** An axis of categories: `Categories`. */
    categories: 'exchart.filters.categories',
    /** What a value on a date axis looks like: `yyyy-MM-dd`. */
    dateHint: 'exchart.filters.hint.date',
    /** On a category axis: `category`. */
    categoryHint: 'exchart.filters.hint.category',
    /** On a number axis: `number`. */
    numberHint: 'exchart.filters.hint.number',
    /** A typed value the axis does not hold, `{0}` the text and `{1}` what the axis holds: `“{0}” is not {1} on this axis.` */
    notOnAxis: 'exchart.filters.not-on-axis',
    /** What a date axis holds: `a date (yyyy-MM-dd)`. */
    aDate: 'exchart.filters.a-date',
    /** What a category axis holds: `a category`. */
    aCategory: 'exchart.filters.a-category',
    /** What a number axis holds: `a number`. */
    aNumber: 'exchart.filters.a-number',
    /** A typed range backwards: `From is after To.` */
    fromAfterTo: 'exchart.filters.from-after-to',
    /** Why a chart its Chart Source thinned saves no CSV and shows no table (ADR-0344). */
    thinnedAtSource: 'exchart.thinned-at-source',
    /** The tooltip's count of the series it does not list, one: `and 1 more series`. */
    oneMoreSeries: 'exchart.tooltip.one-more',
    /** The same for more than one, `{0}` the count: `and {0} more series`. */
    moreSeries: 'exchart.tooltip.more',
    /** The notice of a stylesheet of another version (ADR-0123). */
    stylesheetNotice: 'exchart.notice.stylesheet',
});

const english: Readonly<Record<string, string>> = {
    'exchart.filters': 'Chart Filters',
    'exchart.reset-range': 'Reset range',
    'exchart.table': 'View as table',
    'exchart.table.hide': 'Hide table',
    'exchart.download.csv': 'CSV',
    'exchart.download.svg': 'SVG',
    'exchart.download.png': 'PNG',
    'exchart.menu.download.csv': 'Download CSV',
    'exchart.menu.download.svg': 'Download SVG',
    'exchart.menu.download.png': 'Download PNG',
    'exchart.downloads': 'Download',
    'exchart.chart': 'Chart',
    'exchart.chart-data': 'Chart data',
    'exchart.filters.from': 'From',
    'exchart.filters.to': 'To',
    'exchart.filters.apply': 'Apply',
    'exchart.filters.axis': 'Axis',
    'exchart.filters.proportional': 'Proportional',
    'exchart.filters.categories': 'Categories',
    'exchart.filters.hint.date': 'yyyy-MM-dd',
    'exchart.filters.hint.category': 'category',
    'exchart.filters.hint.number': 'number',
    'exchart.filters.not-on-axis': '“{0}” is not {1} on this axis.',
    'exchart.filters.a-date': 'a date (yyyy-MM-dd)',
    'exchart.filters.a-category': 'a category',
    'exchart.filters.a-number': 'a number',
    'exchart.filters.from-after-to': 'From is after To.',
    'exchart.thinned-at-source': 'The rows are where the data lives: this chart holds only the points drawn, each pixel\'s first, last, lowest and highest, so it saves no CSV and shows no table.',
    'exchart.tooltip.one-more': 'and 1 more series',
    'exchart.tooltip.more': 'and {0} more series',
    'exchart.notice.stylesheet': 'This page holds another version\'s stylesheet. Reload the page.',
};

// Excel's Japanese edition's words where Excel shows the same thing (グラフ フィルター, 表), the family's
// own elsewhere.
const japanese: Readonly<Record<string, string>> = {
    'exchart.filters': 'グラフ フィルター',
    'exchart.reset-range': '範囲をリセット',
    'exchart.table': '表として表示',
    'exchart.table.hide': '表を隠す',
    'exchart.download.csv': 'CSV',
    'exchart.download.svg': 'SVG',
    'exchart.download.png': 'PNG',
    'exchart.menu.download.csv': 'CSV をダウンロード',
    'exchart.menu.download.svg': 'SVG をダウンロード',
    'exchart.menu.download.png': 'PNG をダウンロード',
    'exchart.downloads': 'ダウンロード',
    'exchart.chart': 'グラフ',
    'exchart.chart-data': 'グラフのデータ',
    'exchart.filters.from': '開始',
    'exchart.filters.to': '終了',
    'exchart.filters.apply': '適用',
    'exchart.filters.axis': '軸',
    'exchart.filters.proportional': '比例',
    'exchart.filters.categories': '項目',
    'exchart.filters.hint.date': 'yyyy-MM-dd',
    'exchart.filters.hint.category': '項目',
    'exchart.filters.hint.number': '数値',
    'exchart.filters.not-on-axis': '「{0}」はこの軸の{1}ではありません。',
    'exchart.filters.a-date': '日付 (yyyy-MM-dd)',
    'exchart.filters.a-category': '項目',
    'exchart.filters.a-number': '数値',
    'exchart.filters.from-after-to': '開始が終了より後です。',
    'exchart.thinned-at-source': 'データの行はデータ元にあります。このグラフは描いた点 (各ピクセルの最初、最後、最小、最大) だけを持つため、CSV の保存も表の表示もしません。',
    'exchart.tooltip.one-more': 'ほか 1 系列',
    'exchart.tooltip.more': 'ほか {0} 系列',
    'exchart.notice.stylesheet': 'このページには別のバージョンのスタイルシートがあります。ページを再読み込みしてください。',
};

/** ExChart's Japanese: `label: japaneseChartWords`. A Consumer's own function may fall back to it. */
export const japaneseChartWords: ChartLabel = id => japanese[id];

/** Every id of ExChart's words, in no particular order. */
export const chartWordIdList: readonly string[] = Object.freeze(Object.keys(english));

/** The word for an id: the Consumer's, the English where it says nothing, and the id itself where neither
 * knows it — an id painted is a missing word seen, never a blank. Each `{i}` is filled with `args[i]`. */
export function chartWord(id: string, label: ChartLabel | undefined, ...args: readonly string[]): string {
    const template = label?.(id) ?? english[id] ?? id;
    return template.replace(/\{(\d+)\}/g, (_whole, index: string) => {
        const arg = args[Number(index)];
        if (arg === undefined) {
            throw new Error(`Internal: the word ${id} names {${index}}, which it was not given.`);
        }
        return arg;
    });
}
