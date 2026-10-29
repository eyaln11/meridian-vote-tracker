using System.Threading.Channels;

namespace MeridianVoteTracker.Infrastructure.Imports;

/// <summary>
/// The hand-off between the upload request and the background worker (README ING-01).
/// The request puts an import id here and returns at once; one worker takes ids one at a time, so two big imports never run together.
/// </summary>
public sealed class ImportQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(Guid importId) => _channel.Writer.TryWrite(importId);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}
