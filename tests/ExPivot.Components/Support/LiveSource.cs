using ExPivot.Engine;

namespace ExPivot.Components.Tests.Support;

/// <summary>
/// A Pivot Source whose data moves on when the test says (ADR-0067): <see cref="Publish"/> makes
/// the given sales its data — a new Source Version — and raises <c>Changed</c>, as the bundled
/// source does when it folds a Change Batch in, or a server's source when the Consumer says its
/// data moved on. Its answers are the reference's — the bundled source over the data current when
/// the question was asked — given at once, or held for the test like <see cref="OnDemandSource"/>'s.
/// It can refuse or fail every question, as a server over its cap or out of reach does. A field's
/// Items and a cell's records are answered under the version they name, while it is held.
/// </summary>
internal sealed class LiveSource : PivotSource
{
    private readonly Dictionary<string, PivotSource> _versions = new(StringComparer.Ordinal);
    private PivotSourceFeatures _features;
    private PivotSource _current;

    public LiveSource(IReadOnlyList<Sale>? records = null, bool canRefresh = false)
    {
        _current = PivotTestContext.Bundled(records);
        _features = new PivotSourceFeatures(Enum.GetValues<PivotAggregation>(), canRefresh);
    }

    /// <summary>The questions asked, in order.</summary>
    public List<Question> Questions { get; } = [];
    private readonly Dictionary<int, TaskCompletionSource<Question>> _asked = [];

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

    /// <summary>Whether a question is answered at once, rather than held for the test.</summary>
    public bool AnswersAtOnce { get; set; } = true;

    /// <summary>What every question is refused with while set, as a source over its cap.</summary>
    public PivotSourceRefusal? Refuses { get; set; }

    /// <summary>What every question fails with while set, as a server out of reach.</summary>
    public Exception? Fails { get; set; }

    /// <summary>How many times Refresh was asked for.</summary>
    public int Refreshes { get; private set; }

    /// <summary>The Source Versions of the answers given, in order.</summary>
    public List<string> AnsweredVersions { get; } = [];

    public override IReadOnlyList<PivotField> Fields => _current.Fields;

    public override PivotSourceFeatures Features => _features;

    /// <summary>The questions not yet answered, failed or cancelled.</summary>
    public IEnumerable<Question> Open => Questions.Where(q => !q.Completion.Task.IsCompleted);

    /// <summary>The data moves on to <paramref name="records"/>, and the source says so — naming
    /// the new version when <paramref name="sayVersion"/>, as a server's notice may.</summary>
    public void Publish(IReadOnlyList<Sale> records, bool sayVersion = false)
    {
        _current = PivotTestContext.Bundled(records);
        OnChanged(new PivotSourceChanged(sayVersion ? VersionOf(_current) : null));
    }

    /// <summary>The source says its data moved on, naming <paramref name="version"/> or none,
    /// whether or not it did.</summary>
    public void Notify(string? version = null) => OnChanged(new PivotSourceChanged(version));

    /// <summary>The data moves on to <paramref name="records"/>, and the source says nothing: the
    /// next question asked for any other reason brings it.</summary>
    public void Replace(IReadOnlyList<Sale> records) => _current = PivotTestContext.Bundled(records);

    /// <summary>The source answers only <paramref name="aggregations"/> from now on, as a server
    /// reconfigured under the report might.</summary>
    public void Offer(params PivotAggregation[] aggregations) => _features = new PivotSourceFeatures(aggregations, _features.CanRefresh);

    /// <summary>The Source Version a reference answers under.</summary>
    public string VersionOf(PivotSource reference)
    {
        var version = reference.AggregateAsync(PivotQuery.For(PivotLayout.Empty), CancellationToken.None).AsTask().GetAwaiter().GetResult().SourceVersion;
        _versions.TryAdd(version, reference);
        return version;
    }

    public override ValueTask<PivotAnswer> AggregateAsync(PivotQuery query, CancellationToken cancellationToken = default)
    {
        var question = new Question(this, _current, query, cancellationToken);
        lock (Questions)
        {
            Questions.Add(question);
            if (_asked.Remove(Questions.Count - 1, out var completion)) completion.TrySetResult(question);
        }
        if (AnswersAtOnce)
        {
            question.Settle();
            return new ValueTask<PivotAnswer>(question.Completion.Task);
        }
        cancellationToken.Register(() => question.Completion.TrySetCanceled(cancellationToken));
        return new ValueTask<PivotAnswer>(question.Completion.Task);
    }

    /// <summary>Whether a field's Items are held for the test, each answered under the version it
    /// names when the test says (<see cref="ItemsQuestion.AnswerAsync"/>), rather than at once.</summary>
    public bool HoldsItems { get; set; }

    /// <summary>The Items asked for while <see cref="HoldsItems"/> is set, in order.</summary>
    public List<ItemsQuestion> ItemQuestions { get; } = [];

    public override ValueTask<PivotItemPage> ItemsAsync(PivotItemsQuery query, CancellationToken cancellationToken = default)
    {
        if (!HoldsItems)
            return ListItems(query);
        var question = new ItemsQuestion(this, query);
        ItemQuestions.Add(question);
        return new ValueTask<PivotItemPage>(question.Completion.Task);
    }

    private ValueTask<PivotItemPage> ListItems(PivotItemsQuery query)
        => _versions.TryGetValue(query.SourceVersion, out var reference)
            ? reference.ItemsAsync(query, CancellationToken.None)
            : ValueTask.FromResult(PivotItemPage.Refused(PivotSourceRefusal.SourceVersionNotHeld(query.SourceVersion)));

    /// <summary>A field's Items, asked for under a Source Version and held until the test answers.</summary>
    public sealed class ItemsQuestion(LiveSource source, PivotItemsQuery query)
    {
        public PivotItemsQuery Query { get; } = query;

        public TaskCompletionSource<PivotItemPage> Completion { get; } = new();

        /// <summary>Answers it as the source answers under the version it names.</summary>
        public async Task AnswerAsync() => Completion.TrySetResult(await source.ListItems(Query));
    }

    public override ValueTask<PivotDetailPage> DetailsAsync(PivotDetailsQuery query, CancellationToken cancellationToken = default)
        => _versions.TryGetValue(query.SourceVersion, out var reference)
            ? reference.DetailsAsync(query, cancellationToken)
            : ValueTask.FromResult(PivotDetailPage.Refused(PivotSourceRefusal.SourceVersionNotHeld(query.SourceVersion)));

    /// <summary>Excel's Refresh: says the data moved on, as a server's source does.</summary>
    public override ValueTask RefreshAsync(CancellationToken cancellationToken = default)
    {
        Refreshes++;
        OnChanged(new PivotSourceChanged());
        return ValueTask.CompletedTask;
    }

    /// <summary>One question, answered from the data current when it was asked.</summary>
    public sealed class Question(LiveSource source, PivotSource reference, PivotQuery query, CancellationToken token)
    {
        public PivotQuery Query { get; } = query;

        public CancellationToken Token { get; } = token;

        public TaskCompletionSource<PivotAnswer> Completion { get; } = new();

        /// <summary>Whether ExPivot cancelled it: a further change superseded it.</summary>
        public bool IsCancelled => Token.IsCancellationRequested;

        /// <summary>Answers it as the source stands: refused or failed while it is set to, else as
        /// the reference answers.</summary>
        public void Settle()
        {
            if (source.Fails is { } error)
            {
                Completion.TrySetException(error);
                return;
            }
            if (source.Refuses is { } refusal)
            {
                Completion.TrySetResult(PivotAnswer.Refused(refusal));
                return;
            }
            var answer = reference.AggregateAsync(Query, CancellationToken.None).AsTask().GetAwaiter().GetResult();
            if (!answer.IsRefused)
            {
                source._versions.TryAdd(answer.SourceVersion, reference);
                source.AnsweredVersions.Add(answer.SourceVersion);
            }
            Completion.TrySetResult(answer);
        }

        /// <summary>Answers it — even after it was cancelled, as a late server would; a cancelled
        /// question's completion is spent, and the answer reaches nobody.</summary>
        public Task AnswerAsync()
        {
            Settle();
            return Task.CompletedTask;
        }
    }
}
