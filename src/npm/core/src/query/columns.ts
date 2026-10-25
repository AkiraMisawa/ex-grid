// What the query semantics read of a column (ADR-0001, ADR-0023): its name, its declared type, and how a
// row's value is read. The declared type is the law: every cell and every operand is held to it.

/** A column's declared type (ADR-0002/0023). It decides the filter's operators, the comparison a sort
 * and a filter use, and the numeric presentation. */
export type ColumnType = 'text' | 'number' | 'date' | 'boolean';

/** Which kind of date a Date column holds (ADR-0023, section of 2026-10-02): a wall-clock date and time,
 * a day, or an instant. Every cell and operand of the column is of that one kind. */
export type DateType = 'dateTime' | 'dateOnly' | 'dateTimeOffset';

/** A column as the query semantics see it: the counterpart of C#'s `ColumnInfo<TRow>`. */
export interface QueryColumn<Row> {
    /** The column's name, unique among the columns: what a sort and a filter name it by. */
    readonly name: string;
    /** The declared type. */
    readonly type: ColumnType;
    /** Reads the row's value in this column. `undefined` and `null` are a Blank. */
    readonly value: (row: Row) => unknown;
    /** False for a column that carries no value (an Action Column, a Mark Column), which cannot be sorted
     * or filtered (ADR-0020). True unless given. */
    readonly queryable?: boolean;
    /** A Date column's kind of date; `dateTime` unless given, and given only on a Date column. */
    readonly dateType?: DateType;
}
