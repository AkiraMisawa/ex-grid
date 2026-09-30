namespace ExGrid.Data.Storage;

/// <summary>
/// The Consumer's records behind one segment's rows, kept by reference and in order (ADR-0063). The
/// records are held in an array of their own type, so keeping them boxes nothing.
/// </summary>
internal abstract class RecordStore
{
    public abstract Type RecordType { get; }

    public abstract object? Get(int offset);

    /// <summary>An empty store of the same type, filled row by row from other stores.</summary>
    public abstract RecordGather Gather(int length);
}

internal sealed class RecordStore<T>(T[] items) : RecordStore
{
    public T[] Items { get; } = items;

    public override Type RecordType => typeof(T);

    public override object? Get(int offset) => Items[offset];

    public override RecordGather Gather(int length) => new RecordGather<T>(length);
}

internal abstract class RecordGather
{
    public abstract void Add(RecordStore source, int offset);

    public abstract RecordStore Seal();
}

internal sealed class RecordGather<T>(int length) : RecordGather
{
    private readonly T[] items = new T[length];
    private int count;

    public override void Add(RecordStore source, int offset) => items[count++] = ((RecordStore<T>)source).Items[offset];

    public override RecordStore Seal() => new RecordStore<T>(count == items.Length ? items : items.AsSpan(0, count).ToArray());
}
