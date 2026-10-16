namespace AccountingSystem.Services.WhatsApp
{
// =========================================
// عامل في الخلفية يقرأ من الـ queue ويبعت
// =========================================
public sealed class WhatsAppBackgroundService
: BackgroundService
{
private readonly IWhatsAppQueue _queue;
private readonly IServiceProvider _services;
private readonly ILogger<WhatsAppBackgroundService> _logger;

public WhatsAppBackgroundService(
IWhatsAppQueue queue,
IServiceProvider services,
ILogger<WhatsAppBackgroundService> logger)
{
_queue = queue;
_services = services;
_logger = logger;
}

protected override async Task ExecuteAsync(
CancellationToken stoppingToken)
{
_logger.LogInformation(
"WhatsApp background worker started.");

try
{
await foreach (var job in _queue
.ReadAllAsync(stoppingToken))
{
try
{
await ProcessJobAsync(job, stoppingToken);
}
catch (OperationCanceledException)
{
throw;
}
catch (Exception ex)
{
_logger.LogError(
ex,
"WhatsApp job {JobId} failed (context: {Context})",
job.Id,
job.Context ?? "n/a");
}
finally
{
// حذف ملف الـ PDF لو مطلوب
if (job.Kind == WhatsAppJobKind.Document &&
job.DeleteFileAfterSend &&
!string.IsNullOrWhiteSpace(job.DocumentPath))
{
TryDeleteFile(job.DocumentPath);
}
}
}
}
catch (OperationCanceledException)
{
// إيقاف طبيعي
}

_logger.LogInformation(
"WhatsApp background worker stopped.");
}

private async Task ProcessJobAsync(
WhatsAppJob job,
CancellationToken ct)
{
// نفتح scope جديد لكل مهمة (IServiceScope مهم لأن
// WhatsAppService scoped)
await using var scope =
_services.CreateAsyncScope();

var whatsApp = scope.ServiceProvider
.GetRequiredService<IWhatsAppService>();

WhatsAppResult result;

if (job.Kind == WhatsAppJobKind.Document)
{
result = await whatsApp.SendDocumentAsync(
job.Phone,
job.RecipientName,
job.DocumentPath!,
job.FileName!,
job.Caption ?? string.Empty);
}
else
{
result = await whatsApp.SendMessageAsync(
job.Phone,
job.RecipientName,
job.MessageText ?? string.Empty);
}

if (result.Success)
{
_logger.LogInformation(
"WhatsApp job {JobId} succeeded ({Status}) — {Context}",
job.Id,
result.Status,
job.Context ?? "n/a");
}
else
{
_logger.LogWarning(
"WhatsApp job {JobId} failed: {Error} — {Context}",
job.Id,
result.Error ?? "unknown",
job.Context ?? "n/a");
}
}

private void TryDeleteFile(string path)
{
try
{
if (System.IO.File.Exists(path))
{
System.IO.File.Delete(path);
}
}
catch (Exception ex)
{
_logger.LogWarning(
ex,
"Failed to delete temp file: {Path}",
path);
}
}
}
}
