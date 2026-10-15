namespace AccountingSystem.Services.WhatsApp
{
	public interface IWhatsAppService
	{
		// =====================================
		// كود التفعيل
		// =====================================

		Task<bool> SendActivationCodeAsync(
			string phone,
			string recipientName,
			string activationCode);

		// =====================================
		// رسالة عامة
		// =====================================

		Task<bool> SendMessageAsync(
			string phone,
			string recipientName,
			string message);

		// =====================================
		// فاتورة شراء
		// =====================================

		Task<bool> SendPurchaseInvoiceAsync(
			string phone,
			string supplierName,
			string invoiceNumber,
			decimal totalAmount,
			int itemCount,
			DateTime invoiceDate,
			string portalUrl);

		// =====================================
		// إرسال مستند (PDF) عبر الواتساب
		// =====================================

		Task<bool> SendDocumentAsync(
			string phone,
			string recipientName,
			string documentPath,
			string fileName,
			string caption);

		// =====================================
		// اختبار الاتصال ويرجع تفاصيل الاستجابة
		// =====================================

		Task<(bool Ok, string Details)> SendTestAsync(
			string phone);

		// =====================================
		// استعادة كلمة المرور
		// =====================================

		Task<bool> SendPasswordResetAsync(
			string phone,
			string recipientName,
			string code,
			string resetUrl,
			DateTime expiresAt);
	}
}
