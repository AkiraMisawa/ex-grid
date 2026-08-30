namespace ExGrid;

/// <summary>The contiguous range a Range Request names (ADR-0001).</summary>
public readonly record struct RowRange(int Start, int Count);
