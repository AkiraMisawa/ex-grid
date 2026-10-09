using ExPivot.Engine;

namespace ExPivot.Components.Tests.Support;

/// <summary>
/// A Pivot Source whose answers the test gives, one question at a time (PV-25/26): each
/// <see cref="AggregateAsync"/> is held until <see cref="Question.AnswerAsync"/>,
/// <see cref="Question.Fail"/> or <see cref="Question.Refuse"/>, and the answer is the reference's —
/// the bundled source over the same records — so a test reads the report the screen would show.
/// It counts every question, and the Items and records asked for, and can refuse a Source Version
/// as a server whose data moved on does (ADR-0066).
/// </summary>
internal sealed class OnDemandSource(PivotSource reference, PivotSourceFeatures? features = null) : PivotSource
{
    /// <summary>The questions asked, in order.</summary>
    public List<Question> Questions { get; } = [];
    private readonly Dictionary<int, TaskCompletionSource<Question>> _asked = [];

    /// <summary>Waits for the source's own call; starting a question need not render the component.</summary>
    public Task<Question> QuestionAsync(int index, CancellationToken cancellationToken)
    {
        lock (Questions)
        {
            if (Questions.Count > index) return Task.FromResult(Questions[index]);
            if (!_asked.TryGetValue(index, out var completion))
                _asked[index] = completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            return completion.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        }
    }

    /// <summary>The Items asked for, in order.</summary>
    public List<PivotItemsQuery> ItemQueries { get; } = [];

    /// <summary>The records asked for, in order.</summary>
    public List<PivotDetailsQuery> DetailQueries { get; } = [];

    /// <summary>How many times Refresh was asked for.</summary>
    public int Refreshes { get; private set; }

    /// <summary>Whether a question is answered at once, as the reference answers it, rather than
    /// held for the test.</summary>
    public bool AnswersAtOnce { get; set; }

    /// <summary>Whether the Items and the records are refused for their Source Version — the
    /// server's data moved on.</summary>
    public bool RefusesVersions { get; set; }

    /// <summary>Whether a question ignores its cancellation, as a server that answers anyway does:
    /// its answer then arrives after it was superseded, and only the generation discards it.</summary>
    public bool IgnoresCancellation { get; set; }

    /// <summary>The Items' answers held for the test while set: each completes when the test says.</summary>
    public bool HoldsItems { get; set; }

    /// <summary>The Items questions held, with their completions.</summary>
    public List<(PivotItemsQuery Query, TaskCompletionSource<PivotItemPage> Completion)> HeldItems { get; } = [];

    public override IReadOnlyList<PivotField> Fields => reference.Fields;

    public override PivotSourceFeatures Features { get; } = features ?? reference.Features;

    /// <summary>The questions not yet answered, failed or cancelled.</summary>
    public IEnumerable<Question> Open => Questions.Where(q => !q.Completion.Task.IsCompleted);

    public override ValueTask<PivotAnswer> AggregateAsync(PivotQuery query, CancellationToken cancellationToken = default)
    {
        var question = new Question(reference, query, cancellationToken);
        lock (Questions)
        {
            Questions.Add(question);
            if (_asked.Remove(Questions.Count - 1, out var completion)) completion.TrySetResult(question);
        }
        if (AnswersAtOnce)
        {
            question.Completion.TrySetResult(reference.AggregateAsync(query, CancellationToken.None).AsTask().GetAwaiter().GetResult());
            return new ValueTask<PivotAnswer>(question.Completion.Task);
        }
        if (!IgnoresCancellation)
            cancellationToken.Register(() => question.Completion.TrySetCanceled(cancellationToken));
        return new ValueTask<PivotAnswer>(question.Completion.Task);
    }

    public override ValueTask<PivotItemPage> ItemsAsync(PivotItemsQuery query, CancellationToken cancellationToken = default)
    {
        ItemQueries.Add(query);
        if (RefusesVersions)
            return ValueTask.FromResult(PivotItemPage.Refused(PivotSourceRefusal.SourceVersionNotHeld(query.SourceVersion)));
        if (HoldsItems)
        {
            var completion = new TaskCompletionSource<PivotItemPage>();
            HeldItems.Add((query, completion));
            return new ValueTask<PivotItemPage>(completion.Task);
        }
        return reference.ItemsAsync(query, cancellationToken);
    }

    public override ValueTask<PivotDetailPage> DetailsAsync(PivotDetailsQuery query, CancellationToken cancellationToken = default)
    {
        DetailQueries.Add(query);
        if (RefusesVersions)
            return ValueTask.FromResult(PivotDetailPage.Refused(PivotSourceRefusal.SourceVersionNotHeld(query.SourceVersion)));
        return reference.DetailsAsync(query, cancellationToken);
    }

    /// <summary>What Refresh fails with, as a server that cannot be reached does; null while it
    /// succeeds.</summary>
    public Exception? RefreshFails { get; set; }

    public override ValueTask RefreshAsync(CancellationToken cancellationToken = default)
    {
        Refreshes++;
        return RefreshFails is { } error ? ValueTask.FromException(error) : ValueTask.CompletedTask;
    }

    /// <summary>One question, held until the test answers it.</summary>
    public sealed class Question(PivotSource reference, PivotQuery query, CancellationToken token)
    {
        public PivotQuery Query { get; } = query;

        public CancellationToken Token { get; } = token;

        public TaskCompletionSource<PivotAnswer> Completion { get; } = new();

        /// <summary>Whether ExPivot cancelled it: a further change superseded it.</summary>
        public bool IsCancelled => Token.IsCancellationRequested;

        /// <summary>Answers it as the reference does — even after it was cancelled, as a late
        /// server would.</summary>
        public async Task AnswerAsync()
        {
            var answer = await reference.AggregateAsync(Query, CancellationToken.None);
            if (!Completion.TrySetResult(answer))
            {
                // A cancelled question's completion is spent; a late answer reaches nobody.
            }
        }

        /// <summary>Fails it, as a network or a server does.</summary>
        public void Fail(Exception error) => Completion.TrySetException(error);

        /// <summary>Refuses it, as a source over its cap does.</summary>
        public void Refuse(PivotSourceRefusal refusal) => Completion.TrySetResult(PivotAnswer.Refused(refusal));
    }
}
