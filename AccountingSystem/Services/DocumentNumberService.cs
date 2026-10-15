using AccountingSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Services
{
	public interface IDocumentNumberService
	{
		Task<string> GenerateNumberAsync(string prefix, string tableName, string datePart);
	}

	public class DocumentNumberService : IDocumentNumberService
	{
		private readonly ApplicationDbContext _context;

		public DocumentNumberService(ApplicationDbContext context)
		{
			_context = context;
		}

		public async Task<string> GenerateNumberAsync(string prefix, string tableName, string datePart)
		{
			var searchPrefix = $"{prefix}-{datePart}-";

			var lastNumber = tableName switch
			{
				"SalesInvoices" => await _context.SalesInvoices
					.AsNoTracking()
					.Where(x => x.InvoiceNumber.StartsWith(searchPrefix))
					.OrderByDescending(x => x.InvoiceNumber)
					.Select(x => x.InvoiceNumber)
					.FirstOrDefaultAsync(),
				"PurchaseInvoices" => await _context.PurchaseInvoices
					.AsNoTracking()
					.Where(x => x.InvoiceNumber.StartsWith(searchPrefix))
					.OrderByDescending(x => x.InvoiceNumber)
					.Select(x => x.InvoiceNumber)
					.FirstOrDefaultAsync(),
				"SalesReturnInvoices" => await _context.SalesReturnInvoices
					.AsNoTracking()
					.Where(x => x.InvoiceNumber.StartsWith(searchPrefix))
					.OrderByDescending(x => x.InvoiceNumber)
					.Select(x => x.InvoiceNumber)
					.FirstOrDefaultAsync(),
				"PurchaseReturnInvoices" => await _context.PurchaseReturnInvoices
					.AsNoTracking()
					.Where(x => x.InvoiceNumber.StartsWith(searchPrefix))
					.OrderByDescending(x => x.InvoiceNumber)
					.Select(x => x.InvoiceNumber)
					.FirstOrDefaultAsync(),
				_ => null
			};

			var nextSeq = 1;

			if (!string.IsNullOrEmpty(lastNumber))
			{
				var parts = lastNumber.Split('-');

				if (parts.Length == 3 &&
					int.TryParse(parts[2], out var last))
				{
					nextSeq = last + 1;
				}
			}

			return $"{searchPrefix}{nextSeq:0000}";
		}
	}
}
