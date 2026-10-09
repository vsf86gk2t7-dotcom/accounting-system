using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AccountingSystem.Services.WhatsApp
{
	public class WhatsAppService : IWhatsAppService
	{
		private readonly ILogger<WhatsAppService> _logger;
		private readonly IConfiguration _configuration;
		private readonly IHttpClientFactory _httpClientFactory;

		public WhatsAppService(
			ILogger<WhatsAppService> logger,
			IConfiguration configuration,
			IHttpClientFactory httpClientFactory)
		{
			_logger = logger;
			_configuration = configuration;
			_httpClientFactory = httpClientFactory;
		}

		// =====================================
		// رقم الهاتف: تحويل صيغة 01xxxxxxxxx
		// إلى صيغة دولية 2010xxxxxxxxx
		// =====================================

		private static string NormalizePhone(string phone)
		{
			var digits =
				new string(
					(phone ?? "")
						.Trim()
						.Where(char.IsDigit)
						.ToArray());

			if (digits.Length == 11 &&
				digits.StartsWith("01"))
			{
				return "20" + digits.Substring(1);
			}

			if (digits.Length == 12 &&
				digits.StartsWith("201"))
			{
				return digits;
			}

			if (digits.Length == 10 &&
				digits.StartsWith("1"))
			{
				return "20" + digits;
			}

			return digits;
		}

		// =====================================
		// إرسال مستند (PDF)
		// =====================================

		public async Task<bool> SendDocumentAsync(
			string phone,
			string recipientName,
			string documentPath,
			string fileName,
			string caption)
		{
			var accessToken =
				_configuration["WhatsApp:AccessToken"];

			var phoneNumberId =
				_configuration["WhatsApp:PhoneNumberId"];

			var gatewayUrl =
				_configuration["WhatsAppLocal:GatewayUrl"];

			// ✅ (CS8604 fix) GatewayKey قد يكون null
			var gatewayKey =
				_configuration["WhatsAppLocal:GatewayKey"] ?? string.Empty;

			// لو البوابة المحلية مفعّلة → استخدمها (الأولوية)
			if (!string.IsNullOrWhiteSpace(gatewayUrl))
			{
				return await SendDocumentViaGatewayAsync(
					gatewayUrl,
					gatewayKey,
					phone,
					recipientName,
					documentPath,
					fileName,
					caption);
			}

			// لو غير مُعد → نسخة تجريبية/تطبع فقط
			if (string.IsNullOrWhiteSpace(accessToken) ||
				string.IsNullOrWhiteSpace(phoneNumberId))
			{
				LogAndPrint(
					$"\n" +
					$"📄 WhatsApp Document (لا يوجد إعداد Meta — عرض فقط)\n" +
					$"----------------------------------------\n" +
					$"Recipient : {recipientName}\n" +
					$"Phone     : {phone}\n" +
					$"File      : {fileName}\n" +
					$"Caption   : {caption}\n" +
					$"Path      : {documentPath}\n" +
					$"========================================\n");

				return true;
			}

			if (!File.Exists(documentPath))
			{
				_logger.LogWarning(
					"ملف PDF غير موجود: {Path}",
					documentPath);

				return false;
			}

			var recipient =
				NormalizePhone(phone);

			if (string.IsNullOrEmpty(recipient))
			{
				_logger.LogWarning(
					"رقم هاتف غير صالح: {Phone}",
					phone);

				return false;
			}

			try
			{
				// 1) ارفع الملف
				var mediaId =
					await UploadMediaAsync(
						accessToken!,
						phoneNumberId!,
						documentPath);

				if (string.IsNullOrWhiteSpace(mediaId))
				{
					return false;
				}

				// 2) أرسل رسالة من نوع document
				var payload = new
				{
					messaging_product = "whatsapp",
					recipient_type = "individual",
					to = recipient,
					type = "document",
					document = new
					{
						id = mediaId,
						caption = caption,
						filename = fileName
					}
				};

				var json =
					JsonSerializer.Serialize(payload);

				using var request =
					new HttpRequestMessage(
						HttpMethod.Post,
						$"https://graph.facebook.com/v19.0/{phoneNumberId}/messages");

				request.Headers.Authorization =
					new AuthenticationHeaderValue(
						"Bearer",
						accessToken);

				request.Content =
					new StringContent(
						json,
						Encoding.UTF8,
						"application/json");

				using var response =
					await _httpClientFactory.CreateClient().SendAsync(request);

				var body =
					await response.Content.ReadAsStringAsync();

				if (!response.IsSuccessStatusCode)
				{
					_logger.LogError(
						"فشل إرسال رسالة الواتساب: {Status} {Body}",
						(int)response.StatusCode,
						body);

					return false;
				}

				_logger.LogInformation(
					"تم إرسال مستند واتساب إلى {Phone}: {File}",
					recipient,
					fileName);

				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError(
					ex,
					"خطأ أثناء إرسال واتساب إلى {Phone}",
					phone);

				return false;
			}
		}

		// =====================================
		// اختبار الاتصال ويرجع تفاصيل الاستجابة
		// =====================================

		public async Task<(bool Ok, string Details)> SendTestAsync(
			string phone)
		{
			var gatewayUrl =
				_configuration["WhatsAppLocal:GatewayUrl"];

			if (!string.IsNullOrWhiteSpace(gatewayUrl))
			{
				try
				{
					using var statusReq =
						new HttpRequestMessage(
							HttpMethod.Get,
							$"{gatewayUrl.TrimEnd('/')}/status");

					using var statusResp =
						await _httpClientFactory.CreateClient().SendAsync(statusReq);

					var statusBody =
						await statusResp.Content.ReadAsStringAsync();

					using var doc =
						JsonDocument.Parse(statusBody);

					var ready =
						doc.RootElement.TryGetProperty("ready", out var readyEl) &&
						readyEl.GetBoolean();

					if (!ready)
					{
						return (false,
							"البوابة المحلية غير جاهزة (لم تُمسح الجلسة بعد أو الخادم متوقف).");
					}

					// ✅ (CS8604 fix) GatewayKey قد يكون null
					var ok =
						await SendMessageViaGatewayAsync(
							gatewayUrl,
							_configuration["WhatsAppLocal:GatewayKey"] ?? string.Empty,
							phone,
							"اختبار",
							"اختبار اتصال من نظام الفواتير ✔");

					return (ok,
						ok
							? "تم الإرسال بنجاح عبر البوابة المحلية."
							: "فشل الإرسال عبر البوابة المحلية — راجع سجل الخادم.");
				}
				catch (Exception ex)
				{
					return (false,
						$"تعذر الاتصال بالبوابة المحلية: {ex.Message}");
				}
			}

			var accessToken =
				_configuration["WhatsApp:AccessToken"];

			var phoneNumberId =
				_configuration["WhatsApp:PhoneNumberId"];

			if (string.IsNullOrWhiteSpace(accessToken) ||
				string.IsNullOrWhiteSpace(phoneNumberId))
			{
				return (false,
					"لم يتم تعيين WhatsApp:AccessToken / WhatsApp:PhoneNumberId في appsettings.json.");
			}

			var recipient =
				NormalizePhone(phone);

			if (string.IsNullOrEmpty(recipient))
			{
				return (false,
					$"رقم هاتف غير صالح: {phone}");
			}

			var payload = new
			{
				messaging_product = "whatsapp",
				recipient_type = "individual",
				to = recipient,
				type = "text",
				text = new
				{
					body = "اختبار اتصال من نظام الفواتير ✔"
				}
			};

			var json =
				JsonSerializer.Serialize(payload);

			try
			{
				using var request =
					new HttpRequestMessage(
						HttpMethod.Post,
						$"https://graph.facebook.com/v19.0/{phoneNumberId}/messages");

				request.Headers.Authorization =
					new AuthenticationHeaderValue(
						"Bearer",
						accessToken);

				request.Content =
					new StringContent(
						json,
						Encoding.UTF8,
						"application/json");

				using var response =
					await _httpClientFactory.CreateClient().SendAsync(request);

				var body =
					await response.Content.ReadAsStringAsync();

				if (!response.IsSuccessStatusCode)
				{
					return (false,
						$"HTTP {(int)response.StatusCode}: {body}");
				}

				return (true,
					$"تم الإرسال إلى {recipient} بنجاح: {body}");
			}
			catch (Exception ex)
			{
				return (false,
					$"استثناء: {ex.Message}");
			}
		}

		// =====================================
		// رفع الملف للحصول على media id
		// =====================================

		private async Task<string?> UploadMediaAsync(
			string accessToken,
			string phoneNumberId,
			string filePath)
		{
			using var form =
				new MultipartFormDataContent();

			var fileStream =
				new StreamContent(File.OpenRead(filePath));

			fileStream.Headers.ContentType =
				new MediaTypeHeaderValue(
					"application/octet-stream");

			form.Add(fileStream, "file", Path.GetFileName(filePath));
			form.Add(new StringContent("application/pdf"), "type");
			form.Add(new StringContent("document"), "messaging_product");

			using var request =
				new HttpRequestMessage(
					HttpMethod.Post,
					$"https://graph.facebook.com/v19.0/{phoneNumberId}/media");

			request.Headers.Authorization =
				new AuthenticationHeaderValue(
					"Bearer",
					accessToken);

			request.Content = form;

			using var response =
				await _httpClientFactory.CreateClient().SendAsync(request);

			var body =
				await response.Content.ReadAsStringAsync();

			if (!response.IsSuccessStatusCode)
			{
				_logger.LogError(
					"فشل رفع الوسائط: {Status} {Body}",
					(int)response.StatusCode,
					body);

				return null;
			}

			using var doc =
				JsonDocument.Parse(body);

			if (doc.RootElement.TryGetProperty(
					"id",
					out var idElement))
			{
				return idElement.GetString();
			}

			return null;
		}

		// =====================================
		// كود التفعيل
		// =====================================

		public async Task<bool> SendActivationCodeAsync(
			string phone,
			string recipientName,
			string activationCode)
		{
			var message =
				$"مرحبًا {recipientName}،\n\n" +
				$"كود التفعيل الخاص بك: {activationCode}\n\n" +
				$"شكرًا لك.";

			return await SendMessageAsync(
				phone,
				recipientName,
				message);
		}

		// =====================================
		// رسالة عامة
		// =====================================

		public async Task<bool> SendMessageAsync(
			string phone,
			string recipientName,
			string message)
		{
			var accessToken =
				_configuration["WhatsApp:AccessToken"];

			var phoneNumberId =
				_configuration["WhatsApp:PhoneNumberId"];

			var gatewayUrl =
				_configuration["WhatsAppLocal:GatewayUrl"];

			if (!string.IsNullOrWhiteSpace(gatewayUrl))
			{
				// ✅ (CS8604 fix) GatewayKey قد يكون null
				return await SendMessageViaGatewayAsync(
					gatewayUrl,
					_configuration["WhatsAppLocal:GatewayKey"] ?? string.Empty,
					phone,
					recipientName,
					message);
			}

			if (string.IsNullOrWhiteSpace(accessToken) ||
				string.IsNullOrWhiteSpace(phoneNumberId))
			{
				var fullMessage =
					$"\n" +
					$"========================================\n" +
					$"📱 WhatsApp Message (لا يوجد إعداد Meta — عرض فقط)\n" +
					$"----------------------------------------\n" +
					$"Recipient : {recipientName}\n" +
					$"Phone     : {phone}\n" +
					$"----------------------------------------\n" +
					$"{message}\n" +
					$"========================================\n";

				LogAndPrint(fullMessage);

				return true;
			}

			var recipient =
				NormalizePhone(phone);

			if (string.IsNullOrEmpty(recipient))
			{
				_logger.LogWarning(
					"رقم هاتف غير صالح: {Phone}",
					phone);

				return false;
			}

			var payload = new
			{
				messaging_product = "whatsapp",
				recipient_type = "individual",
				to = recipient,
				type = "text",
				text = new
				{
					body = message
				}
			};

			var json =
				JsonSerializer.Serialize(payload);

			using var request =
				new HttpRequestMessage(
					HttpMethod.Post,
					$"https://graph.facebook.com/v19.0/{phoneNumberId}/messages");

			request.Headers.Authorization =
				new AuthenticationHeaderValue(
					"Bearer",
					accessToken);

			request.Content =
				new StringContent(
					json,
					Encoding.UTF8,
					"application/json");

			try
			{
				using var response =
					await _httpClientFactory.CreateClient().SendAsync(request);

				var body =
					await response.Content.ReadAsStringAsync();

				if (!response.IsSuccessStatusCode)
				{
					_logger.LogError(
						"فشل إرسال رسالة الواتساب: {Status} {Body}",
						(int)response.StatusCode,
						body);

					return false;
				}

				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError(
					ex,
					"خطأ أثناء إرسال واتساب إلى {Phone}",
					phone);

				return false;
			}
		}

		// =====================================
		// استعادة كلمة المرور
		// =====================================

		public Task<bool> SendPasswordResetAsync(
			string phone,
			string recipientName,
			string code,
			string resetUrl,
			DateTime expiresAt)
		{
			var message =
				$"مرحبًا {recipientName}،\n\n" +
				$"كود استعادة كلمة المرور: {code}\n" +
				$"صالح حتى: {expiresAt:yyyy-MM-dd HH:mm}\n\n" +
				$"لتعيين كلمة مرور جديدة:\n" +
				$"{resetUrl}\n\n" +
				$"لو مش إنت اللي طلبت، تجاهل الرسالة.";

			return SendMessageAsync(
				phone,
				recipientName,
				message);
		}

		// =====================================
		// فاتورة شراء (نص، محفوظ للتوافق)
		// =====================================

		public Task<bool> SendPurchaseInvoiceAsync(
			string phone,
			string supplierName,
			string invoiceNumber,
			decimal totalAmount,
			int itemCount,
			DateTime invoiceDate,
			string portalUrl)
		{
			var message =
				$"فاتورة شراء جديدة\n" +
				$"-------------------------\n" +
				$"المورد: {supplierName}\n" +
				$"رقم الفاتورة: {invoiceNumber}\n" +
				$"التاريخ: {invoiceDate:yyyy-MM-dd}\n" +
				$"عدد البنود: {itemCount}\n" +
				$"الإجمالي: {totalAmount:N2} ج.م\n" +
				$"-------------------------\n" +
				$"تفاصيل أكثر: {portalUrl}";

			return SendMessageAsync(
				phone,
				supplierName,
				message);
		}

		// =====================================
		// Helper
		// =====================================

		private void LogAndPrint(string message)
		{
			Console.WriteLine(message);

			_logger.LogWarning(message);
		}

		// =====================================
		// إرسال عبر البوابة المحلية (whatsapp-web.js)
		// =====================================

		private async Task<bool> SendMessageViaGatewayAsync(
			string gatewayUrl,
			string gatewayKey,
			string phone,
			string recipientName,
			string message)
		{
			try
			{
				var recipient =
					NormalizePhone(phone);

				if (string.IsNullOrEmpty(recipient))
				{
					_logger.LogWarning(
						"رقم هاتف غير صالح (بوابة محلية): {Phone}",
						phone);

					return false;
				}

				var payload = new
				{
					to = recipient,
					message = message
				};

				var json =
					JsonSerializer.Serialize(payload);

				using var request =
					new HttpRequestMessage(
						HttpMethod.Post,
						$"{gatewayUrl.TrimEnd('/')}/send-message");

				if (!string.IsNullOrWhiteSpace(gatewayKey))
				{
					request.Headers.TryAddWithoutValidation(
						"x-gateway-key",
						gatewayKey);
				}

				request.Content =
					new StringContent(
						json,
						Encoding.UTF8,
						"application/json");

				using var response =
					await _httpClientFactory.CreateClient().SendAsync(request);

				var body =
					await response.Content.ReadAsStringAsync();

				if (!response.IsSuccessStatusCode)
				{
					LogAndPrint(
						$"\n" +
						$"📱 WhatsApp Gateway فشل الإرسال: {recipient}\n" +
						$"{(int)response.StatusCode} {body}\n");

					return false;
				}

				_logger.LogInformation(
					"تم إرسال رسالة عبر البوابة المحلية إلى {Phone}",
					recipient);

				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError(
					ex,
					"خطأ أثناء إرسال رسالة عبر البوابة المحلية إلى {Phone}",
					phone);

				return false;
			}
		}

		private async Task<bool> SendDocumentViaGatewayAsync(
			string gatewayUrl,
			string gatewayKey,
			string phone,
			string recipientName,
			string documentPath,
			string fileName,
			string caption)
		{
			try
			{
				if (!File.Exists(documentPath))
				{
					_logger.LogWarning(
						"ملف PDF غير موجود (بوابة محلية): {Path}",
						documentPath);

					return false;
				}

				var recipient =
					NormalizePhone(phone);

				if (string.IsNullOrEmpty(recipient))
				{
					_logger.LogWarning(
						"رقم هاتف غير صالح (بوابة محلية): {Phone}",
						phone);

					return false;
				}

				var payload = new
				{
					to = recipient,
					filePath = Path.GetFullPath(documentPath),
					fileName = fileName,
					caption = caption
				};

				var json =
					JsonSerializer.Serialize(payload);

				using var request =
					new HttpRequestMessage(
						HttpMethod.Post,
						$"{gatewayUrl.TrimEnd('/')}/send-document");

				if (!string.IsNullOrWhiteSpace(gatewayKey))
				{
					request.Headers.TryAddWithoutValidation(
						"x-gateway-key",
						gatewayKey);
				}

				request.Content =
					new StringContent(
						json,
						Encoding.UTF8,
						"application/json");

				using var response =
					await _httpClientFactory.CreateClient().SendAsync(request);

				var body =
					await response.Content.ReadAsStringAsync();

				if (!response.IsSuccessStatusCode)
				{
					LogAndPrint(
						$"\n" +
						$"📄 WhatsApp Gateway فشل إرسال المستند: {recipient}\n" +
						$"{fileName}\n" +
						$"{(int)response.StatusCode} {body}\n");

					return false;
				}

				_logger.LogInformation(
					"تم إرسال مستند عبر البوابة المحلية إلى {Phone}: {File}",
					recipient,
					fileName);

				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError(
					ex,
					"خطأ أثناء إرسال مستند عبر البوابة المحلية إلى {Phone}",
					phone);

				return false;
			}
		}
	}
}