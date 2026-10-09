using AccountingSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Services;

/// <summary>
/// يحافظ على اتصال قاعدة البيانات نشطًا.
/// مهم جدًا مع LocalDB الذي يتوقف تلقائيًا بعد 5 دقائق خمول.
/// يقوم بإرسال استعلام خفيف كل دقيقتين لمنع الإيقاف.
/// </summary>
public class DatabaseKeepAliveService : BackgroundService
{
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly ILogger<DatabaseKeepAliveService> _logger;
	private readonly IConfiguration _configuration;

	// 2 دقائق — أقل من 5 دقائق (LocalDB idle timeout الافتراضي)
	private static readonly TimeSpan PingInterval = TimeSpan.FromMinutes(2);

	// انتظار قبل أول ping بعد بدء التطبيق
	private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(15);

	// مهلة الاستعلام
	private static readonly TimeSpan PingTimeout = TimeSpan.FromSeconds(15);

	public DatabaseKeepAliveService(
		IServiceScopeFactory scopeFactory,
		ILogger<DatabaseKeepAliveService> logger,
		IConfiguration configuration)
	{
		_scopeFactory = scopeFactory;
		_logger = logger;
		_configuration = configuration;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		// تجاهل الخدمة في بيئة الاختبارات (InMemory)
		var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
		if (string.Equals(env, "Testing", StringComparison.OrdinalIgnoreCase))
		{
			_logger.LogInformation("DatabaseKeepAliveService معطّل في بيئة Testing.");
			return;
		}

		// إمكانية التعطيل من الإعدادات
		if (_configuration.GetValue<bool>("DatabaseKeepAlive:Enabled") == false &&
			_configuration["DatabaseKeepAlive:Enabled"] is not null)
		{
			_logger.LogInformation("DatabaseKeepAliveService معطّل من الإعدادات.");
			return;
		}

		_logger.LogInformation(
			"DatabaseKeepAliveService بدأ — الفاصل بين كل ping {Interval} دقائق.",
			PingInterval.TotalMinutes);

		// انتظار أولي بسيط قبل البدء
		try
		{
			await Task.Delay(InitialDelay, stoppingToken);
		}
		catch (TaskCanceledException)
		{
			return;
		}

		var consecutiveFailures = 0;

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
				timeoutCts.CancelAfter(PingTimeout);

				await using var scope = _scopeFactory.CreateAsyncScope();
				var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

				// استعلام خفيف جدًا
				await db.Database.ExecuteSqlRawAsync(
					"SELECT 1",
					timeoutCts.Token);

				if (consecutiveFailures > 0)
				{
					_logger.LogInformation(
						"DatabaseKeepAliveService استعاد الاتصال بنجاح بعد {Count} فشل متتالي.",
						consecutiveFailures);
				}

				consecutiveFailures = 0;
				_logger.LogDebug("DatabaseKeepAlive ping نجح في {Time:HH:mm:ss}.", DateTime.Now);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				consecutiveFailures++;

				// سجّل التحذير فقط بعد فشلين متتاليين لتقليل الضوضاء
				if (consecutiveFailures >= 2)
				{
					_logger.LogWarning(
						ex,
						"DatabaseKeepAlive ping فشل ({Count} مرات متتالية). " +
						"قد يكون LocalDB قد توقف. سيتم إعادة المحاولة.",
						consecutiveFailures);
				}
				else
				{
					_logger.LogDebug(ex, "DatabaseKeepAlive ping فشل مرة واحدة (طبيعي).");
				}

				// عند الفشل المتكرر، انتظر قليلًا قبل إعادة المحاولة
				if (consecutiveFailures >= 3)
				{
					try
					{
						await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
					}
					catch (TaskCanceledException)
					{
						break;
					}
				}
			}

			try
			{
				await Task.Delay(PingInterval, stoppingToken);
			}
			catch (TaskCanceledException)
			{
				break;
			}
		}

		_logger.LogInformation("DatabaseKeepAliveService توقف.");
	}
}