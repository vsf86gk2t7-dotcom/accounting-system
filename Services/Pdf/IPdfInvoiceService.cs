namespace AccountingSystem.Services.Pdf
{
	public interface IPdfInvoiceService
	{
		// =====================================
		// إنشاء ملف PDF لفاتورة بيع
		// يعيد مسار الملف أو null
		// =====================================

		Task<string?> GenerateSalesInvoicePdfAsync(
			int invoiceId);

		// =====================================
		// إنشاء ملف PDF لفاتورة شراء
		// =====================================

		Task<string?> GeneratePurchaseInvoicePdfAsync(
			int invoiceId);
	}
}
