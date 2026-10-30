// What a chart is of, in the data's own terms (ADR-0251): the vocabulary the layout reads. The C# engine's
// records are plain data here; the definition that checks them is `ChartDefinition`.

/**
 * A series' value: a number, or a whole number held exactly as a bigint, so the tooltip and the files
 * write the digits the cell holds (ADR-0251). It becomes a number only where a position is computed.
 */
export type ChartValue = number | bigint;

/** What a series' numbers are, which decides how it is drawn (ADR-0253). Declared, never defaulted. */
export type SeriesKind =
    /** A quantity at an instant: points, joined only to their neighbours in the data. */
    | 'level'
    /** A rate constant over an interval: a step with a filled end where it takes the value and an open end where it does not. */
    | 'rateOverInterval'
    /** A total over its interval: a step whose height is the amount per year, from zero. */
    | 'amountPerPeriod'
    /** One thing measured on two quantities: a marker, never joined, its points neither ordered nor unique in x (ADR-0290). */
    | 'observation'
    /** One period's open, high, low and close, and optionally its volume: a candle (ADR-0300). */
    | 'priceRange';

/** Which interval a step's point stands for: `preceding` is (tᵢ₋₁, tᵢ], `following` is [tᵢ, tᵢ₊₁). */
export type StepSide = 'preceding' | 'following';

/** The vertical axis a series reads on. */
export type AxisSide = 'left' | 'right';

/**
 * How an axis places its positions (ADR-0251): in proportion, evenly as categories, or by their logarithm,
 * all above zero (ADR-0290).
 */
export type AxisScale = 'proportional' | 'categories' | 'logarithmic';

/** What the horizontal positions are: dates as day numbers, numbers, or labels by their index. */
export type AxisValues = 'dates' | 'numbers' | 'labels';

/** The calendar period candles merge into past the plot's pixels (ADR-0302): each the market's own. */
export type CandlePeriod = 'row' | 'week' | 'month' | 'quarter' | 'year';

/** How a series' Interval is drawn (ADR-0303): whiskers at each point, or a band joining neighbours. */
export type IntervalStyle = 'whiskers' | 'band';

/** A labelled position of a number axis, such as "6M" at 0.5. */
export type NamedPosition = readonly [position: number, label: string];

/** A point of a series: its position across and its value, or `null` where it is missing — a gap, never a zero. */
export interface ChartPoint {
    /** The position: a day number, a number, or a category's index. */
    readonly x: number;
    /** The value, or `null` where it is missing. */
    readonly y: ChartValue | null;
    /** What the point is, such as a name, which an Observation's tooltip says (ADR-0290). */
    readonly name?: string;
    /** The Row Key of the row the point was read from, raised when it is clicked (ADR-0290). */
    readonly row?: unknown;
}

/** A column an Observation's tooltip says beside its values, one value a point (ADR-0321). */
export interface PointDetail {
    /** What the values are. */
    readonly title: string;
    /** One value a point, written; `null` where the point has none. */
    readonly values: readonly (string | null)[];
}

/** A range of an Interpolated series and the method the Consumer used over it (ADR-0257). */
export interface Segment {
    /** Where the range starts. */
    readonly from: number;
    /** Where it ends. */
    readonly to: number;
    /** The method's name, as the tooltip and the Notes say it. */
    readonly method: string;
    /** Whether the range lies beyond the quotes. */
    readonly extrapolated?: boolean;
}

/** The keys a series' look is chosen by; the same key always takes the same look (ADR-0256). */
export interface AppearanceKeys {
    /** The colour key. */
    readonly colour: string;
    /** The line-style key; absent for a solid line. */
    readonly lineStyle?: string;
    /** The marker key; absent for the first marker. */
    readonly marker?: string;
}

/** A Consumer's curve as a function, asked at about one position per device pixel as it is drawn (ADR-0257). */
export interface SeriesSampler {
    /** The first position the function is asked at. */
    readonly from: number;
    /** The last. */
    readonly to: number;
    /** The Consumer's curve: the value at a position, or `null` where it has none. */
    readonly valueAt: (position: number) => ChartValue | null;
}

/** An edge between two nodes of an Observation, each named by its point's `row` (ADR-0321). */
export interface ChartEdge {
    /** The Row Key of the node it starts at. */
    readonly from: unknown;
    /** The Row Key of the node it ends at. */
    readonly to: unknown;
}

/** One row of a Price Range (ADR-0300): a period's open, high, low and close, and optionally its volume. */
export interface PriceBar {
    /** The period's position: on a Dates axis, its day number. */
    readonly x: number;
    /** The first price of the period, or `null`. */
    readonly open: ChartValue | null;
    /** The highest, or `null`. */
    readonly high: ChartValue | null;
    /** The lowest, or `null`. */
    readonly low: ChartValue | null;
    /** The last, or `null`. */
    readonly close: ChartValue | null;
    /** The quantity traded, or `null` (absent is the same). */
    readonly volume?: ChartValue | null;
}

/** An Interval's bounds at one position of its series (ADR-0303). */
export interface IntervalBound {
    /** The position: one of the series' points. */
    readonly x: number;
    /** The lower bound, or `null`. */
    readonly lower: ChartValue | null;
    /** The upper bound, or `null`. */
    readonly upper: ChartValue | null;
}

/** A series' Interval (ADR-0303): bounds handed in by the Consumer, with what they mean. */
export interface SeriesInterval {
    /** What the interval is — "95% CI", "±1σ", "Bid–Ask"; required. */
    readonly meaning: string;
    /** The bounds at the series' positions. */
    readonly bounds: readonly IntervalBound[];
    /** Whiskers (the default) or a band. */
    readonly style?: IntervalStyle;
}

/** A point's colour read from a column (ADR-0320): one value a point, coloured in nine bands. */
export interface PointColour {
    /** What the values are. */
    readonly title: string;
    /** One value a point; `null` where it has none, drawn as an outline. */
    readonly values: readonly (ChartValue | null)[];
    /** The value the colours diverge from (ADR-0310); absent colours the values low to high. */
    readonly centre?: number;
}

/** A point's size read from a column (ADR-0320): one value a point, never negative. */
export interface PointSize {
    /** What the values are. */
    readonly title: string;
    /** One value a point; `null` where it has none, drawn as an outline. */
    readonly values: readonly (ChartValue | null)[];
}

/** What one thinned device pixel holds, for the tooltip to say (ADR-0254). */
export interface PixelSummary {
    /** The first position in the pixel. */
    readonly from: number;
    /** The last. */
    readonly to: number;
    /** The lowest height. */
    readonly min: number;
    /** The highest. */
    readonly max: number;
    /** How many points the pixel holds. */
    readonly count: number;
}

/** The horizontal axis as a Consumer declares it (ADR-0251). */
export interface ChartAxisOptions {
    /** What the positions are. */
    readonly values: AxisValues;
    /** The axis' title; empty by default. */
    readonly title?: string;
    /** How positions are placed; absent takes the default for `values`. */
    readonly scale?: AxisScale;
    /** The categories' labels in their order, for `labels`; position i is label i. */
    readonly labels?: readonly string[];
    /** Labels for declared positions, shown in place of the number. */
    readonly named?: readonly NamedPosition[];
    /** How many days a year has when an Amount per period on a Dates axis is drawn per year; 365.25 by default. */
    readonly daysPerYear?: number;
    /** The sessions packed (ADR-0301): each position a Price Range has one evenly spaced slot. */
    readonly packed?: boolean;
    /** Writes a position, as the column the positions came from writes it (ADR-0250). */
    readonly format?: (position: number) => string;
    /**
     * An Excel number format code in the invariant spelling that writes every position (ADR-0420): a date code
     * on a Dates axis, a number code on a Numbers axis, written invariantly. A code the reader does not read,
     * one beside a `format`, and one on a Labels axis are refused.
     */
    readonly formatCode?: string;
}

/** The horizontal axis, its defaults applied. */
export interface ChartAxis {
    /** What the positions are. */
    readonly values: AxisValues;
    /** The axis' title. */
    readonly title: string;
    /** The scale declared, or absent for the default. */
    readonly scale?: AxisScale;
    /** The categories' labels. */
    readonly labels: readonly string[];
    /** The declared positions' labels. */
    readonly named: readonly NamedPosition[];
    /** Days in a year, for an Amount per period on dates. */
    readonly daysPerYear: number;
    /** Whether the sessions are packed. */
    readonly packed: boolean;
    /** The Consumer's writer of a position, if any. */
    readonly format?: (position: number) => string;
    /** The format code that writes its positions, if any (ADR-0420). */
    readonly formatCode?: string;
    /** The scale, defaults applied. */
    readonly effectiveScale: AxisScale;
}

/** One series as a Consumer declares it, in the data's own terms (ADR-0251). */
export interface ChartSeriesOptions {
    /** The series' identity in the chart: unique, and stable through a sort or a recalculation. */
    readonly id: string;
    /** Its name in the legend and the tooltip. */
    readonly name: string;
    /** What its numbers are; a series that declares none is refused (ADR-0253). */
    readonly kind: SeriesKind;
    /** Which interval a step's point stands for; `preceding` by default. */
    readonly step?: StepSide;
    /** The vertical axis it reads on; `left` by default. */
    readonly axis?: AxisSide;
    /** The panel it is drawn in; series naming the same panel share it. Empty by default. */
    readonly panel?: string;
    /** The keys its look is chosen by (ADR-0256); by default its colour key is its id. */
    readonly keys?: AppearanceKeys;
    /** Whether its axis reaches zero; absent takes the default for its kind (ADR-0253, ADR-0290). */
    readonly fromZero?: boolean;
    /** Whether it is values the Consumer computed between quotes (ADR-0257). */
    readonly interpolated?: boolean;
    /** The ranges of an Interpolated series and each one's method (ADR-0257). */
    readonly segments?: readonly Segment[];
    /** Its points, in increasing position (an Observation's in any order). */
    readonly points?: readonly ChartPoint[];
    /** An Observation's colour read from a column (ADR-0320). */
    readonly colourBy?: PointColour;
    /** An Observation's size read from a column (ADR-0320). */
    readonly sizeBy?: PointSize;
    /** An Observation's columns its tooltip says (ADR-0321). */
    readonly details?: readonly PointDetail[];
    /** An Observation's edges between its nodes (ADR-0321). */
    readonly edges?: readonly ChartEdge[];
    /** A Price Range's rows (ADR-0300); its points are then each row's close, or a gap. */
    readonly bars?: readonly PriceBar[];
    /** A Price Range's period fixed by the Consumer (ADR-0302). */
    readonly mergeTo?: CandlePeriod;
    /** The vertical Interval (ADR-0303, ADR-0304). */
    readonly interval?: SeriesInterval;
    /** An Observation's horizontal Interval (ADR-0304). */
    readonly xInterval?: SeriesInterval;
    /** What each pixel its Chart Source thinned held (ADR-0344). */
    readonly thinned?: readonly PixelSummary[];
    /** The function it is sampled from, in place of points. */
    readonly sampler?: SeriesSampler;
    /** Writes one of its values, as its column does (ADR-0250, ADR-0251). */
    readonly format?: (value: ChartValue) => string;
    /**
     * An Excel number format code in the invariant spelling that writes every value, held or computed (ADR-0420),
     * written invariantly. A code the reader does not read, a date code, and one beside a `format` are refused.
     */
    readonly formatCode?: string;
}

/** One series, its defaults applied. */
export interface ChartSeries {
    /** Its identity. */
    readonly id: string;
    /** Its name. */
    readonly name: string;
    /** Its Series Kind. */
    readonly kind: SeriesKind;
    /** Which interval a step's point stands for. */
    readonly step: StepSide;
    /** The vertical axis it reads on. */
    readonly axis: AxisSide;
    /** Its panel. */
    readonly panel: string;
    /** The keys declared, if any. */
    readonly keys?: AppearanceKeys;
    /** Whether it said it reaches zero, if it said. */
    readonly fromZero?: boolean;
    /** Whether it is the Consumer's interpolation. */
    readonly interpolated: boolean;
    /** Its Segments. */
    readonly segments: readonly Segment[];
    /** Its points; a Price Range's are its closes. */
    readonly points: readonly ChartPoint[];
    /** Its colour scale, if any. */
    readonly colourBy?: PointColour;
    /** Its size scale, if any. */
    readonly sizeBy?: PointSize;
    /** Its tooltip's columns. */
    readonly details: readonly PointDetail[];
    /** Its edges. */
    readonly edges: readonly ChartEdge[];
    /** A Price Range's rows. */
    readonly bars: readonly PriceBar[];
    /** A Price Range's fixed period, if any. */
    readonly mergeTo?: CandlePeriod;
    /** Its vertical Interval, if any. */
    readonly interval?: SeriesInterval;
    /** Its horizontal Interval, if any. */
    readonly xInterval?: SeriesInterval;
    /** What its source's thinned pixels held. */
    readonly thinned: readonly PixelSummary[];
    /** Its function, if it is sampled. */
    readonly sampler?: SeriesSampler;
    /** Its writer of values, if any. */
    readonly format?: (value: ChartValue) => string;
    /** The format code that writes its values, if any (ADR-0420). */
    readonly formatCode?: string;
    /** The keys its look is chosen by, its id standing for a colour key it does not set. */
    readonly effectiveKeys: AppearanceKeys;
    /** Whether its axis reaches zero, defaults applied. */
    readonly reachesZero: boolean;
}

/** The look a series' Appearance Keys chose (ADR-0256): slot numbers the stylesheet's Visual Tokens say the look of. */
export interface SeriesLook {
    /** The colour slot, from 0. */
    readonly colour: number;
    /** The line-style slot, from 0 (solid). */
    readonly lineStyle: number;
    /** The marker slot, from 0 (a circle). */
    readonly marker: number;
    /** The marker's shape. */
    readonly shape: MarkerShape;
}

/** The shapes a marker key chooses from, in order. */
export type MarkerShape = 'circle' | 'square' | 'diamond' | 'triangle';
