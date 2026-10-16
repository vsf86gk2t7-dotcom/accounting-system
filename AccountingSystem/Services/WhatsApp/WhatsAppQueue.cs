using System.Threading.Channels;

namespace AccountingSystem.Services.WhatsApp
{
public enum WhatsAppJobKind
{
Document = 1,
Message = 2
}

public sealed class WhatsAppJob
{
public Guid Id { get; } = Guid.NewGuid();

public required WhatsAppJobKind Kind { get; init; }

public required string Phone { get; init; }

public required string RecipientName { get; init; }

public string? DocumentPath { get; init; }
public string? FileName { get; init; }
public string? Caption { get; init; }

public string? MessageText { get; init; }

public bool DeleteFileAfterSend { get; init; } = true;

public string? Context { get; init; }
}

public interface IWhatsAppQueue
{
ValueTask EnqueueAsync(
WhatsAppJob job,
CancellationToken ct = default);

IAsyncEnumerable<WhatsAppJob> ReadAllAsync(
CancellationToken ct = default);
}

public sealed class WhatsAppQueue : IWhatsAppQueue
{
private readonly Channel<WhatsAppJob> _channel;

public WhatsAppQueue()
{
_channel = Channel.CreateUnbounded<WhatsAppJob>(
new UnboundedChannelOptions
{
SingleReader = true,
SingleWriter = false
});
}

public ValueTask EnqueueAsync(
WhatsAppJob job,
CancellationToken ct = default)
=> _channel.Writer.WriteAsync(job, ct);

public IAsyncEnumerable<WhatsAppJob> ReadAllAsync(
CancellationToken ct = default)
=> _channel.Reader.ReadAllAsync(ct);
}
}
