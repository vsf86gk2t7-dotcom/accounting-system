namespace AccountingSystem.Services.Pdf
{
	public interface IPdfInvoiceService
	{
		// =====================================
		// إنشاء ملف PDF لفاتورة بيع
		// يعيد مسار الملف أو null
		// =====================================

		Task<string?> GenerateSalesInvoicePdfAsync(
			int invoiceId,
			InvoicePageSize pageSize = InvoicePageSize.A4);

		// =====================================
		// إنشاء ملف PDF لفاتورة شراء
		// =====================================

		Task<string?> GeneratePurchaseInvoicePdfAsync(
			int invoiceId,
			InvoicePageSize pageSize = InvoicePageSize.A4);
	}
}