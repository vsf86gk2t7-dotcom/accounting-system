namespace AccountingSystem.Services.WhatsApp
{
	public interface IWhatsAppService
	{
		// إرسال كود تفعيل
		Task<WhatsAppResult> SendActivationCodeAsync(
			string phone,
			string recipientName,
			string activationCode);

		// إرسال رسالة نصية
		Task<WhatsAppResult> SendMessageAsync(
			string phone,
			string recipientName,
			string message);

		// إرسال ملخص فاتورة شراء
		Task<WhatsAppResult> SendPurchaseInvoiceAsync(
			string phone,
			string supplierName,
			string invoiceNumber,
			decimal totalAmount,
			int itemCount,
			DateTime invoiceDate,
			string portalUrl);

		// إرسال مستند (PDF) عبر واتساب
		Task<WhatsAppResult> SendDocumentAsync(
			string phone,
			string recipientName,
			string documentPath,
			string fileName,
			string caption);

		// اختبار الاتصال بالواتساب
		Task<WhatsAppResult> SendTestAsync(
			string phone);

		// إرسال كود استعادة كلمة المرور
		Task<WhatsAppResult> SendPasswordResetAsync(
			string phone,
			string recipientName,
			string code,
			string resetUrl,
			DateTime expiresAt);
	}
}