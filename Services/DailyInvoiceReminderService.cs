using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AccountingSystem.Services;

public class DailyInvoiceReminderService : BackgroundService
{
	private readonly IServiceProvider _services;
	private readonly ILogger<DailyInvoiceReminderService> _logger;

	public DailyInvoiceReminderService(
		IServiceProvider services,
		ILogger<DailyInvoiceReminderService> logger)
	{
		_services = services;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(
		CancellationToken stoppingToken)
	{
		_logger.LogInformation(
			"Daily invoice reminder service started.");

		try
		{
			await SendRemindersAsync(stoppingToken);
		}
		catch (OperationCanceledException)
		{
			return;
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex,
				"Daily invoice reminder: initial run failed.");
		}

		using var timer = new PeriodicTimer(
			TimeSpan.FromHours(24));

		try
		{
			while (await timer.WaitForNextTickAsync(stoppingToken))
			{
				try
				{
					await SendRemindersAsync(stoppingToken);
				}
				catch (OperationCanceledException)
				{
					return;
				}
				catch (Exception ex)
				{
					_logger.LogWarning(ex,
						"Daily invoice reminder tick failed.");
				}
			}
		}
		catch (OperationCanceledException)
		{
		}
	}

	private async Task SendRemindersAsync(
		CancellationToken ct)
	{
		await using var scope = _services.CreateAsyncScope();
		var reminderService = scope.ServiceProvider
			.GetRequiredService<IInvoiceReminderService>();

		await reminderService
			.SendOverdueAndUpcomingRemindersAsync(ct);

		_logger.LogInformation(
			"Daily invoice reminder cycle completed.");
	}
}
