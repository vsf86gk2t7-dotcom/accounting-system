using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Services.Pdf;
using AccountingSystem.Services.WhatsApp;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Services;

public interface IInvoiceReminderService
{
	Task SendOverdueAndUpcomingRemindersAsync(CancellationToken ct);
	Task SendManualReminderAsync(int invoiceId, int invoiceType, int? userId);
}

public class InvoiceReminderService : IInvoiceReminderService
{
	private readonly IServiceProvider _services;
	private readonly ILogger<InvoiceReminderService> _logger;
	private const int CheckWindowDays = 3;
	private const int MinDaysBetweenReminders = 3;

	public InvoiceReminderService(
		IServiceProvider services,
		ILogger<InvoiceReminderService> logger)
	{
		_services = services;
		_logger = logger;
	}

	public async Task SendOverdueAndUpcomingRemindersAsync(
		CancellationToken ct)
	{
		await using var scope = _services.CreateAsyncScope();
		var db = scope.ServiceProvider
			.GetRequiredService<ApplicationDbContext>();
		var whatsApp = scope.ServiceProvider
			.GetRequiredService<IWhatsAppService>();
		var pdfService = scope.ServiceProvider
			.GetRequiredService<IPdfInvoiceService>();

		var today = DateTime.Today;

		var overdueInvoices = await db.SalesInvoices
			.Where(x => x.Status == SalesInvoiceStatus.Confirmed &&
						x.DueDate != null &&
						x.DueDate < today &&
						x.DueDate >= today.AddDays(-30))
			.Include(x => x.Customer)
			.ToListAsync(ct);

		var upcomingInvoices = await db.SalesInvoices
			.Where(x => x.Status == SalesInvoiceStatus.Confirmed &&
						x.DueDate != null &&
						x.DueDate >= today &&
						x.DueDate <= today.AddDays(CheckWindowDays))
			.Include(x => x.Customer)
			.ToListAsync(ct);

		var allSales = overdueInvoices.Concat(upcomingInvoices).ToList();

		foreach (var invoice in allSales)
		{
			if (ct.IsCancellationRequested) break;
			if (invoice.Customer == null ||
				string.IsNullOrWhiteSpace(invoice.Customer.Phone))
				continue;

			var canSend = await CanSendReminderAsync(
				db, invoice.Id, 1, ct);
			if (!canSend) continue;

			var daysDiff =
				(invoice.DueDate!.Value.Date - today).Days;
			var type = daysDiff < 0
				? ReminderType.Overdue
				: ReminderType.Upcoming;

			var message = BuildReminderMessage(
				invoice, daysDiff, type);

			// اسم العميل لتمريره كـ recipientName للواجهة
			var recipientName = invoice.Customer.Name ?? "عميل";

			try
			{
				var pdfPath = await pdfService
					.GenerateSalesInvoicePdfAsync(invoice.Id);

				if (!string.IsNullOrEmpty(pdfPath))
				{
					await whatsApp.SendDocumentAsync(
						invoice.Customer.Phone,
						recipientName,
						pdfPath,
						Path.GetFileName(pdfPath),
						message);
				}
				else
				{
					await whatsApp.SendMessageAsync(
						invoice.Customer.Phone,
						recipientName,
						message);
				}

				await RecordReminderAsync(
					db, invoice.Id, 1, daysDiff, type, ct);

				_logger.LogInformation(
					"Reminder sent: Invoice {InvoiceNumber}, type {Type}",
					invoice.InvoiceNumber, type);
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex,
					"Failed to send reminder for invoice {InvoiceNumber}",
					invoice.InvoiceNumber);
			}
		}

		var overduePurchases = await db.PurchaseInvoices
			.Where(x => x.Status == PurchaseInvoiceStatus.Posted &&
						x.DueDate != null &&
						x.DueDate < today &&
						x.DueDate >= today.AddDays(-30))
			.Include(x => x.Supplier)
			.ToListAsync(ct);

		var upcomingPurchases = await db.PurchaseInvoices
			.Where(x => x.Status == PurchaseInvoiceStatus.Posted &&
						x.DueDate != null &&
						x.DueDate >= today &&
						x.DueDate <= today.AddDays(CheckWindowDays))
			.Include(x => x.Supplier)
			.ToListAsync(ct);

		var allPurchases = overduePurchases
			.Concat(upcomingPurchases).ToList();

		foreach (var invoice in allPurchases)
		{
			if (ct.IsCancellationRequested) break;
			if (invoice.Supplier == null ||
				string.IsNullOrWhiteSpace(invoice.Supplier.Phone))
				continue;

			var canSend = await CanSendReminderAsync(
				db, invoice.Id, 2, ct);
			if (!canSend) continue;

			var daysDiff =
				(invoice.DueDate!.Value.Date - today).Days;
			var type = daysDiff < 0
				? ReminderType.Overdue
				: ReminderType.Upcoming;

			var message = BuildPurchaseReminderMessage(
				invoice, daysDiff, type);

			// اسم المورد لتمريره كـ recipientName للواجهة
			var recipientName = invoice.Supplier.Name ?? "مورد";

			try
			{
				var pdfPath = await pdfService
					.GeneratePurchaseInvoicePdfAsync(invoice.Id);

				if (!string.IsNullOrEmpty(pdfPath))
				{
					await whatsApp.SendDocumentAsync(
						invoice.Supplier.Phone,
						recipientName,
						pdfPath,
						Path.GetFileName(pdfPath),
						message);
				}
				else
				{
					await whatsApp.SendMessageAsync(
						invoice.Supplier.Phone,
						recipientName,
						message);
				}

				await RecordReminderAsync(
					db, invoice.Id, 2, daysDiff, type, ct);

				_logger.LogInformation(
					"Reminder sent: Purchase {InvoiceNumber}, type {Type}",
					invoice.InvoiceNumber, type);
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex,
					"Failed to send purchase reminder for {InvoiceNumber}",
					invoice.InvoiceNumber);
			}
		}
	}

	public async Task SendManualReminderAsync(
		int invoiceId, int invoiceType, int? userId)
	{
		await using var scope = _services.CreateAsyncScope();
		var db = scope.ServiceProvider
			.GetRequiredService<ApplicationDbContext>();
		var whatsApp = scope.ServiceProvider
			.GetRequiredService<IWhatsAppService>();
		var pdfService = scope.ServiceProvider
			.GetRequiredService<IPdfInvoiceService>();

		var salesInvoice = await db.SalesInvoices
			.Include(x => x.Customer)
			.FirstOrDefaultAsync(x => x.Id == invoiceId);

		if (salesInvoice != null)
		{
			var daysDiff = salesInvoice.DueDate != null
				? (salesInvoice.DueDate.Value.Date -
					DateTime.Today).Days
				: 0;
			var type = daysDiff < 0
				? ReminderType.Overdue
				: ReminderType.Upcoming;
			var message = BuildReminderMessage(
				salesInvoice, daysDiff, type);

			var recipientName = salesInvoice.Customer?.Name ?? "عميل";

			var pdfPath = await pdfService
				.GenerateSalesInvoicePdfAsync(invoiceId);

			if (!string.IsNullOrEmpty(pdfPath) &&
				salesInvoice.Customer != null &&
				!string.IsNullOrWhiteSpace(salesInvoice.Customer.Phone))
			{
				await whatsApp.SendDocumentAsync(
					salesInvoice.Customer.Phone,
					recipientName,
					pdfPath,
					Path.GetFileName(pdfPath),
					message);
			}
			else if (salesInvoice.Customer != null &&
					 !string.IsNullOrWhiteSpace(salesInvoice.Customer.Phone))
			{
				await whatsApp.SendMessageAsync(
					salesInvoice.Customer.Phone,
					recipientName,
					message);
			}

			await RecordReminderAsync(
				db, invoiceId, 1, daysDiff, type,
				default);
			return;
		}

		var purchaseInvoice = await db.PurchaseInvoices
			.Include(x => x.Supplier)
			.FirstOrDefaultAsync(x => x.Id == invoiceId);

		if (purchaseInvoice != null)
		{
			var daysDiff = purchaseInvoice.DueDate != null
				? (purchaseInvoice.DueDate.Value.Date -
					DateTime.Today).Days
				: 0;
			var type = daysDiff < 0
				? ReminderType.Overdue
				: ReminderType.Upcoming;
			var message = BuildPurchaseReminderMessage(
				purchaseInvoice, daysDiff, type);

			var recipientName = purchaseInvoice.Supplier?.Name ?? "مورد";

			var pdfPath = await pdfService
				.GeneratePurchaseInvoicePdfAsync(invoiceId);

			if (!string.IsNullOrEmpty(pdfPath) &&
				purchaseInvoice.Supplier != null &&
				!string.IsNullOrWhiteSpace(purchaseInvoice.Supplier.Phone))
			{
				await whatsApp.SendDocumentAsync(
					purchaseInvoice.Supplier.Phone,
					recipientName,
					pdfPath,
					Path.GetFileName(pdfPath),
					message);
			}
			else if (purchaseInvoice.Supplier != null &&
					 !string.IsNullOrWhiteSpace(purchaseInvoice.Supplier.Phone))
			{
				await whatsApp.SendMessageAsync(
					purchaseInvoice.Supplier.Phone,
					recipientName,
					message);
			}

			await RecordReminderAsync(
				db, invoiceId, 2, daysDiff, type,
				default);
		}
	}

	private async Task<bool> CanSendReminderAsync(
		ApplicationDbContext db,
		int invoiceId,
		int invoiceType,
		CancellationToken ct)
	{
		var recentCount = await db.PaymentReminders
			.CountAsync(x =>
				x.InvoiceId == invoiceId &&
				x.InvoiceType == invoiceType &&
				x.IsSent &&
				x.SentAt >= DateTime.UtcNow.AddDays(
					-MinDaysBetweenReminders),
				ct);

		return recentCount == 0;
	}

	private async Task RecordReminderAsync(
		ApplicationDbContext db,
		int invoiceId,
		int invoiceType,
		int daysDiff,
		ReminderType type,
		CancellationToken ct)
	{
		var reminder = new PaymentReminder
		{
			InvoiceId = invoiceId,
			InvoiceType = invoiceType,
			DueDate = DateTime.Today.AddDays(daysDiff),
			DaysDifference = daysDiff,
			Type = type,
			IsSent = true,
			SentAt = DateTime.UtcNow,
			CreatedAt = DateTime.UtcNow
		};

		db.PaymentReminders.Add(reminder);
		await db.SaveChangesAsync(ct);
	}

	private static string BuildReminderMessage(
		SalesInvoice invoice,
		int daysDiff,
		ReminderType type)
	{
		var statusText = type == ReminderType.Overdue
			? $"متأخرة بـ {Math.Abs(daysDiff)} يوم"
			: $"تستحق بعد {daysDiff} يوم";

		return $"تذكير بدفع فاتورة رقم {invoice.InvoiceNumber}\n" +
			   $"التاريخ: {invoice.InvoiceDate:dd/MM/yyyy}\n" +
			   $"المستحق: {invoice.DueDate?.ToString("dd/MM/yyyy") ?? "-"} \n" +
			   $"الحالة: {statusText}\n" +
			   $"المبلغ الإجمالي: {invoice.TotalAmount:N2} جنيه\n" +
			   $"— من شركة بسملة —";
	}

	private static string BuildPurchaseReminderMessage(
		PurchaseInvoice invoice,
		int daysDiff,
		ReminderType type)
	{
		var statusText = type == ReminderType.Overdue
			? $"متأخرة بـ {Math.Abs(daysDiff)} يوم"
			: $"تستحق بعد {daysDiff} يوم";

		return $"تذكير بدفع فاتورة شراء رقم {invoice.InvoiceNumber}\n" +
			   $"التاريخ: {invoice.InvoiceDate:dd/MM/yyyy}\n" +
			   $"المستحق: {invoice.DueDate?.ToString("dd/MM/yyyy") ?? "-"} \n" +
			   $"الحالة: {statusText}\n" +
			   $"المبلغ الإجمالي: {invoice.TotalAmount:N2} جنيه\n" +
			   $"— من شركة بسملة —";
	}
}
