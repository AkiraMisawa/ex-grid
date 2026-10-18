using Xunit;

namespace ExGrid.Tests;

// ADR-0240: input to one product instance is taken in one order, by number, one input at a time.
public class InputOrderTests
{
    private readonly List<int> _answered = [];
    private readonly List<string> _log = [];

    private InputOrder NewOrder() => new(_answered.Add);

    [Fact] // ADR-0240 §2: an input that arrives before the one numbered ahead of it waits for it
    public async Task ADR0240_an_input_that_arrives_before_its_predecessor_waits_for_its_number()
    {
        var order = NewOrder();
        var second = order.RunAsync(2, () => { _log.Add("2"); return Task.CompletedTask; });
        Assert.Empty(_log);
        Assert.False(second.IsCompleted);

        await order.RunAsync(1, () => { _log.Add("1"); return Task.CompletedTask; });
        await second;

        Assert.Equal(["1", "2"], _log);
        Assert.Equal([1, 2], _answered);
    }

    [Fact] // ADR-0240 §3: a handler that awaits holds the next input until it has finished
    public async Task ADR0240_an_input_sent_while_the_one_before_awaits_starts_after_it()
    {
        var order = NewOrder();
        var consumer = new TaskCompletionSource();
        var first = order.RunAsync(1, async () =>
        {
            _log.Add("1 starts");
            await consumer.Task;
            _log.Add("1 ends");
        });
        var second = order.RunAsync(2, () => { _log.Add("2"); return Task.CompletedTask; });
        Assert.Equal(["1 starts"], _log);

        consumer.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(["1 starts", "1 ends", "2"], _log);
        Assert.Equal([1, 2], _answered);
    }

    [Fact] // ADR-0240 §2: a number told ahead of an event that never came is voided, and the order moves on
    public async Task ADR0240_a_voided_number_lets_the_inputs_behind_it_go()
    {
        var order = NewOrder();
        var third = order.RunAsync(3, () => { _log.Add("3"); return Task.CompletedTask; });
        await order.RunAsync(1, () => { _log.Add("1"); return Task.CompletedTask; });
        Assert.False(third.IsCompleted);

        order.Void(2);
        await third;

        Assert.Equal(["1", "3"], _log);
        Assert.Equal([1, 2, 3], _answered);
    }

    [Fact] // ADR-0240 §3: a handling asked for inside the flow of the one under way does not wait for itself
    public async Task ADR0240_work_asked_for_inside_the_flow_runs_at_once()
    {
        var order = NewOrder();
        await order.RunAsync(1, async () =>
        {
            await Task.Yield();
            Assert.True(order.InFlow);
            await order.AfterReceivedAsync(() => { _log.Add("inside"); return Task.CompletedTask; });
            _log.Add("1 ends");
        });

        Assert.Equal(["inside", "1 ends"], _log);
        Assert.False(order.InFlow);
    }

    [Fact] // ADR-0240 §3: work the instance starts itself goes after every input received so far
    public async Task ADR0240_work_started_by_the_instance_goes_after_the_inputs_received()
    {
        var order = NewOrder();
        var consumer = new TaskCompletionSource();
        var first = order.RunAsync(1, async () => { await consumer.Task; _log.Add("1"); });
        var own = order.AfterReceivedAsync(() => { _log.Add("own"); return Task.CompletedTask; });
        var second = order.RunAsync(2, () => { _log.Add("2"); return Task.CompletedTask; });

        consumer.SetResult();
        await Task.WhenAll(first, own, second);

        Assert.Equal(["1", "own", "2"], _log);
    }

    [Fact] // ADR-0240 §3: a handler that throws reports it on its own task and lets the inputs behind it go
    public async Task ADR0240_a_handler_that_throws_releases_the_inputs_behind_it()
    {
        var order = NewOrder();
        var first = order.RunAsync(1, async () => { await Task.Yield(); throw new InvalidOperationException("one"); });
        var second = order.RunAsync(2, () => { _log.Add("2"); return Task.CompletedTask; });

        await Assert.ThrowsAsync<InvalidOperationException>(() => first);
        await second;

        Assert.Equal(["2"], _log);
        Assert.Equal([1, 2], _answered);
    }

    [Fact] // ADR-0240 §4: nothing waits on a clock; disposal lets every waiting input go
    public async Task ADR0240_disposal_lets_every_waiting_input_go()
    {
        var order = NewOrder();
        var waiting = order.RunAsync(2, () => { _log.Add("2"); return Task.CompletedTask; });

        order.Dispose();

        await waiting;
        Assert.Empty(_log);
    }

    [Fact] // ADR-0240 §2: an input with no number keeps today's path and runs at once
    public void ADR0240_an_unnumbered_input_runs_at_once()
    {
        var order = NewOrder();
        _ = order.RunAsync(2, () => { _log.Add("2"); return Task.CompletedTask; });

        _ = order.RunAsync(0, () => { _log.Add("unnumbered"); return Task.CompletedTask; });

        Assert.Equal(["unnumbered"], _log);
    }

    [Fact] // ADR-0240 §3: a burst handled synchronously is taken in one loop, in order
    public async Task ADR0240_a_long_synchronous_burst_is_taken_in_order()
    {
        var order = NewOrder();
        var tasks = new List<Task>();
        for (var n = 10_000; n >= 2; n--)
        {
            var input = n;
            tasks.Add(order.RunAsync(input, () => { _log.Add(input.ToString()); return Task.CompletedTask; }));
        }
        tasks.Add(order.RunAsync(1, () => { _log.Add("1"); return Task.CompletedTask; }));
        await Task.WhenAll(tasks);

        Assert.Equal(Enumerable.Range(1, 10_000).Select(n => n.ToString()), _log);
        Assert.Equal(10_000, order.AnsweredUpTo);
    }

    [Fact] // ADR-0240 §2: a handling that answers a value answers it in its place in the order
    public async Task ADR0240_a_handling_that_answers_a_value_answers_it_in_order()
    {
        var order = NewOrder();
        var second = order.RunAsync(2, () => Task.FromResult("two"));
        await order.RunAsync(1, () => Task.FromResult("one"));

        Assert.Equal("two", await second);
        Assert.Equal([1, 2], _answered);
    }
    [Fact] // ADR-0240 §3: the flow of the input under way is named inside it, and to no later input and no work that outlives it
    public async Task ADR0240_the_flow_names_the_input_under_way_and_no_other()
    {
        var order = NewOrder();
        Assert.Null(order.Flow);
        object? first = null;
        object? second = null;
        var outliving = new TaskCompletionSource();
        Task? leftBehind = null;
        object? seenAfterward = new();
        await order.RunAsync(1, () =>
        {
            first = order.Flow;
            leftBehind = LaterAsync();
            return Task.CompletedTask;

            async Task LaterAsync()
            {
                await outliving.Task;
                seenAfterward = order.Flow;
            }
        });
        await order.RunAsync(2, async () =>
        {
            await Task.Yield();
            second = order.Flow;
            outliving.SetResult();
            await leftBehind!;
        });

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotSame(first, second);
        // Work input 1 started and left behind runs inside input 2's handling, but is not its flow.
        Assert.Null(seenAfterward);
        Assert.Null(order.Flow);
    }
}
