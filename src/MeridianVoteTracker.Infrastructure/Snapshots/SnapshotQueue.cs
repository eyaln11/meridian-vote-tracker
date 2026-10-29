using System.Threading.Channels;

namespace MeridianVoteTracker.Infrastructure.Snapshots;

/// <summary>The hand-off between "create snapshot" and the background worker that calculates it (README SNP-02).</summary>
public sealed class SnapshotQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(Guid snapshotId) => _channel.Writer.TryWrite(snapshotId);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}
