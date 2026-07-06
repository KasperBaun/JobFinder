using System.Collections.Concurrent;
using System.Threading.Channels;
using Jobmatch.Jobs;

namespace Jobmatch.Api.Infrastructure;

/// <summary>
/// In-process pub/sub that fans <see cref="JobSearch"/> snapshots from the running Hangfire job out to
/// connected SSE viewers. Same process as the worker, so a per-viewer channel suffices. A viewer
/// disconnecting only drops its channel — it never affects the producer or other viewers.
/// </summary>
public sealed class JobSearchBus
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, Channel<JobSearch>>> _byId = new();

    public void Publish(JobSearch snapshot)
    {
        if (!_byId.TryGetValue(snapshot.Id, out var subscribers)) return;

        foreach (var ch in subscribers.Values)
            ch.Writer.TryWrite(snapshot);

        if (snapshot.IsTerminal)
        {
            foreach (var ch in subscribers.Values)
                ch.Writer.TryComplete();
            _byId.TryRemove(snapshot.Id, out _);
        }
    }

    /// <summary>
    /// Registers a viewer immediately (so no publish is missed between registration and the caller's
    /// first read). Dispose to unregister. Read live snapshots from <see cref="Subscription.Reader"/>.
    /// </summary>
    public Subscription Subscribe(string id)
    {
        // Bounded with DropOldest: viewers only care about the latest state, never a backlog.
        var channel = Channel.CreateBounded<JobSearch>(new BoundedChannelOptions(8)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });
        var key = Guid.NewGuid();
        _byId.GetOrAdd(id, _ => new ConcurrentDictionary<Guid, Channel<JobSearch>>())[key] = channel;
        return new Subscription(this, id, key, channel.Reader);
    }

    private void Unsubscribe(string id, Guid key)
    {
        if (_byId.TryGetValue(id, out var current))
        {
            current.TryRemove(key, out _);
            if (current.IsEmpty) _byId.TryRemove(id, out _);
        }
    }

    public sealed class Subscription(JobSearchBus bus, string id, Guid key, ChannelReader<JobSearch> reader) : IDisposable
    {
        public ChannelReader<JobSearch> Reader { get; } = reader;

        public void Dispose() => bus.Unsubscribe(id, key);
    }
}
