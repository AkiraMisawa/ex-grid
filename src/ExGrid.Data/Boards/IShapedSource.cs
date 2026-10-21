namespace ExGrid.Data;

/// <summary>
/// A source that answers its shape before any row (ADR-0341): its columns' names and kinds, so a
/// Board Input bound to it is checked before anything is fetched or drawn
/// (<see cref="BoardInput.CheckAsync"/>). A source held where the data lives answers from its schema;
/// one over a Snapshot answers the Snapshot's own.
/// </summary>
public interface IShapedSource
{
    /// <summary>The source's shape, or null when it was given none to say, which a Board refuses to bind
    /// rather than guess from rows.</summary>
    /// <param name="cancellationToken">Cancels the question.</param>
    ValueTask<DataShape?> ShapeAsync(CancellationToken cancellationToken = default);
}
