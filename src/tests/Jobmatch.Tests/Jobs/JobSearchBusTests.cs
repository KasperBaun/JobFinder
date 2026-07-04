using Jobmatch.Api.Infrastructure;
using Jobmatch.Jobs;
using Jobmatch.Search;

namespace Jobmatch.Tests.Jobs;

public sealed class JobSearchBusTests
{
    private static readonly DateTimeOffset T0 = new(2026, 6, 5, 12, 0, 0, TimeSpan.Zero);
    private static JobSearch Running(string id) => JobSearch.Create(id, new SearchRequest(), T0).MarkRunning(T0);

    [Fact]
    public async Task Subscriber_Receives_Snapshots_And_Stream_Completes_On_Terminal()
    {
        var bus = new JobSearchBus();
        using var sub = bus.Subscribe("id1");

        var running = Running("id1");
        bus.Publish(running);
        bus.Publish(running.MarkSucceeded(3, 0.8, T0.AddMinutes(1)));

        var received = new List<JobSearch>();
        await foreach (var s in sub.Reader.ReadAllAsync())
            received.Add(s);

        Assert.Equal(2, received.Count);
        Assert.Equal(JobSearchState.Succeeded, received[^1].State);
    }

    [Fact]
    public async Task Multiple_Subscribers_Each_Receive_The_Snapshot()
    {
        var bus = new JobSearchBus();
        using var a = bus.Subscribe("id1");
        using var b = bus.Subscribe("id1");

        bus.Publish(Running("id1").MarkSucceeded(1, 0.5, T0));

        var ra = new List<JobSearch>();
        await foreach (var s in a.Reader.ReadAllAsync()) ra.Add(s);
        var rb = new List<JobSearch>();
        await foreach (var s in b.Reader.ReadAllAsync()) rb.Add(s);

        Assert.Single(ra);
        Assert.Single(rb);
    }

    [Fact]
    public void Publish_With_No_Subscribers_Is_A_Noop()
    {
        var bus = new JobSearchBus();
        bus.Publish(Running("nobody")); // must not throw
    }

    [Fact]
    public void Disposed_Subscriber_Stops_Receiving()
    {
        var bus = new JobSearchBus();
        var sub = bus.Subscribe("id1");
        sub.Dispose();
        bus.Publish(Running("id1")); // no subscribers left → noop, no throw
        Assert.False(sub.Reader.TryRead(out _));
    }
}
