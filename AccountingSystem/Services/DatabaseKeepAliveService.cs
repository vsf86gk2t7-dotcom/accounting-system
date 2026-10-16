using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Services;

/// <summary>
/// يحافظ على صلاحية الاتصال بالقاعدة ودفء كاشها
/// لتفادي مهلات القراءة الباردة على الأقراص البطيئة.
/// </summary>
public sealed class DatabaseKeepAliveService : BackgroundService
{
	private readonly IServiceProvider _services;
	private readonly ILogger<DatabaseKeepAliveService> _logger;
	private long _lastSuccessTicks;

	public DatabaseKeepAliveService(
		IServiceProvider services,
		ILogger<DatabaseKeepAliveService> logger)
	{
		_services = services;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(
		CancellationToken stoppingToken)
	{
		// إحماء أولي فورًا عند تشغيل التطبيق
		try
		{
			await WarmUpAsync(stoppingToken);
			_logger.LogInformation(
				"Database keep-alive: warm-up done.");
		}
		catch (OperationCanceledException)
		{
			return;
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex,
				"Database keep-alive: initial warm-up failed.");
		}

		using var timer = new PeriodicTimer(
			TimeSpan.FromMinutes(2));

		try
		{
			// عند الإيقاف تُلغي الـ Host الـ token
			// فيرمي WaitForNextTickAsync استثناء إلغاء —
			// وهنا نعتبره إيقافًا طبيعيًا لا خطأً.
			while (await timer.WaitForNextTickAsync(stoppingToken))
			{
				try
				{
					await WarmUpAsync(stoppingToken);
				}
				catch (OperationCanceledException)
				{
					return;
				}
				catch (Exception ex)
				{
					_logger.LogWarning(ex,
						"Database keep-alive tick failed.");
				}
			}
		}
		catch (OperationCanceledException)
		{
			// إيقاف طبيعي للخدمة أثناء الإقلاع أو الإغلاق
		}
	}

	private async Task WarmUpAsync(
		CancellationToken ct)
	{
		// نفتح نطاق جديدًا في كل مرة لضمان تمرير
		// AuditLogInterceptor Scoped بصورة صحيحة
		await using var scope =
			_services.CreateAsyncScope();

		var db = scope.ServiceProvider
			.GetRequiredService<Data.ApplicationDbContext>();

		// ✅ استعلام خفيف جداً: يبقي الاتصال دافئاً بدون مسح جداول كبيرة
		await db.Database
			.ExecuteSqlRawAsync("SELECT 1", ct);

		_lastSuccessTicks = Environment.TickCount64;
	}

	public TimeSpan? TimeSinceLastSuccess
	{
		get
		{
			if (_lastSuccessTicks == 0)
			{
				return null;
			}

			return TimeSpan.FromMilliseconds(
				Environment.TickCount64 -
				_lastSuccessTicks);
		}
	}
}