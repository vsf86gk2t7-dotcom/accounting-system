namespace AccountingSystem.Services
{
// =========================================
// خدمة تنظيف النسخ الاحتياطية القديمة تلقائيًا
// - تعمل كل 24 ساعة
// - تحذف النسخ الأقدم من RetentionDays
// - تحتفظ دائمًا بـ MinBackups نسخ على الأقل
// =========================================
public sealed class BackupRetentionService : BackgroundService
{
private readonly IConfiguration _configuration;
private readonly ILogger<BackupRetentionService> _logger;

private readonly TimeSpan _interval =
TimeSpan.FromHours(24);

public BackupRetentionService(
IConfiguration configuration,
ILogger<BackupRetentionService> logger)
{
_configuration = configuration;
_logger = logger;
}

protected override async Task ExecuteAsync(
CancellationToken stoppingToken)
{
// تأخير أول تشغيل دقيقة (لا نزاحم بدء التطبيق)
try
{
await Task.Delay(
TimeSpan.FromMinutes(1),
stoppingToken);
}
catch (OperationCanceledException) { return; }

// تشغيل فوري مرة
try
{
RunCleanup();
}
catch (Exception ex)
{
_logger.LogWarning(ex,
"Backup retention: initial run failed.");
}

using var timer = new PeriodicTimer(_interval);

try
{
while (await timer.WaitForNextTickAsync(stoppingToken))
{
try
{
RunCleanup();
}
catch (Exception ex)
{
_logger.LogWarning(ex,
"Backup retention: tick failed.");
}
}
}
catch (OperationCanceledException)
{
// إيقاف طبيعي
}
}

private void RunCleanup()
{
var folder =
_configuration["BackupSettings:Folder"];

if (string.IsNullOrWhiteSpace(folder) ||
!Directory.Exists(folder))
{
return;
}

var retentionDays =
int.TryParse(
_configuration["BackupSettings:RetentionDays"],
out var rd)
? rd
: 30;

var minBackups =
int.TryParse(
_configuration["BackupSettings:MinBackups"],
out var mb)
? mb
: 5;

var allFiles = new DirectoryInfo(folder)
.GetFiles("AccountingSystemDb_*.bak")
.OrderByDescending(x => x.LastWriteTime)
.ToList();

if (allFiles.Count <= minBackups)
{
return;
}

var cutoff =
DateTime.UtcNow.AddDays(-retentionDays);

// احتفظ بأحدث MinBackups دائمًا + احذف الأقدم من cutoff
var toDelete = allFiles
.Skip(minBackups)
.Where(x => x.LastWriteTime.ToUniversalTime() < cutoff)
.ToList();

foreach (var file in toDelete)
{
try
{
file.Delete();

_logger.LogInformation(
"Backup retention: deleted {File}",
file.Name);
}
catch (Exception ex)
{
_logger.LogWarning(ex,
"Backup retention: failed to delete {File}",
file.Name);
}
}
}
}
}
