using AccountingSystem.Data;
using AccountingSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Services
{
	public interface ISalesInvoiceService
	{
		Task<(bool Success, string? Error, SalesInvoice? Invoice)> ConfirmInvoiceAsync(
			int invoiceId, int? userId);

		Task<(bool Success, string? Error)> CancelInvoiceAsync(
			int invoiceId, int? userId);
	}

	public class SalesInvoiceService : ISalesInvoiceService
	{
		private readonly ApplicationDbContext _context;
		private readonly IPostingService _postingService;

		public SalesInvoiceService(
			ApplicationDbContext context,
			IPostingService postingService)
		{
			_context = context;
			_postingService = postingService;
		}

	public async Task<(bool Success, string? Error, SalesInvoice? Invoice)> ConfirmInvoiceAsync(
		int invoiceId, int? userId)
	{
		await using var transaction = await _context.Database
			.BeginTransactionAsync();

		try
		{
			var invoice = await _context.SalesInvoices
				.Include(x => x.Items)
				.FirstOrDefaultAsync(x => x.Id == invoiceId);

			if (invoice == null)
				return (false, "الفاتورة غير موجودة.", null);

			if (invoice.Status != SalesInvoiceStatus.Draft)
				return (false, "لا يمكن تأكيد الفاتورة إلا وهي في حالة مسودة.", null);

			if (!invoice.Items.Any())
				return (false, "لا يمكن تأكيد فاتورة بدون بنود.", null);

			invoice.Status = SalesInvoiceStatus.Confirmed;
			invoice.ConfirmedAt = DateTime.UtcNow;

			await _context.SaveChangesAsync();
			await transaction.CommitAsync();

			return (true, null, invoice);
		}
		catch
		{
			await transaction.RollbackAsync();
			throw;
		}
	}

	public async Task<(bool Success, string? Error)> CancelInvoiceAsync(
		int invoiceId, int? userId)
	{
		await using var transaction = await _context.Database
			.BeginTransactionAsync();

		try
		{
			var invoice = await _context.SalesInvoices
				.Include(x => x.Items)
				.FirstOrDefaultAsync(x => x.Id == invoiceId);

			if (invoice == null)
				return (false, "الفاتورة غير موجودة.");

			if (invoice.Status != SalesInvoiceStatus.Confirmed)
				return (false, "لا يمكن إلغاء الفاتورة إلا وهي مؤكدة.");

			invoice.Status = SalesInvoiceStatus.Cancelled;

			await _context.SaveChangesAsync();
			await transaction.CommitAsync();

			return (true, null);
		}
		catch
		{
			await transaction.RollbackAsync();
			throw;
		}
	}
	}
}
