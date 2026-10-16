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
		// توحيد رقم الهاتف: 01xxxxxxxxx
		// إلى الصيغة الدولية 2010xxxxxxxxx
		// =====================================

		private static string NormalizePhone(string phone)
		{
			var digits =
				new string(
					(phone ?? "")
						.Trim()
						.Where(char.IsDigit)
						.ToArray());

			// الصيغة المحلية المصرية: 01XXXXXXXXX (11 رقم)
			if (digits.Length == 11 &&
				digits.StartsWith("01"))
			{
				return "20" + digits.Substring(1);
			}

			// الصيغة الدولية بدون + (12 رقم)
			if (digits.Length == 12 &&
				digits.StartsWith("201"))
			{
				return digits;
			}

			// رقم بدون 0 في البداية: 1XXXXXXXXX
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

		public async Task<WhatsAppResult> SendDocumentAsync(
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

			var gatewayKey =
				_configuration["WhatsAppLocal:GatewayKey"];

			// إن كانت هناك بوابة محلية → نستخدمها
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

			// إن لم تكن إعدادات Meta مضبوطة → نحاكي الإرسال
			if (string.IsNullOrWhiteSpace(accessToken) ||
				string.IsNullOrWhiteSpace(phoneNumberId))
			{
				LogAndPrint(
					$"\n" +
					$"[MOCK] WhatsApp Document (Meta غير معد)\n" +
					$"----------------------------------------\n" +
					$"Recipient : {recipientName}\n" +
					$"Phone     : {phone}\n" +
					$"File      : {fileName}\n" +
					$"Caption   : {caption}\n" +
					$"Path      : {documentPath}\n" +
					$"========================================\n");

				return WhatsAppResult.Simulated(
					"إعدادات Meta WhatsApp غير مضبوطة");
			}

			if (!File.Exists(documentPath))
			{
				_logger.LogWarning(
					"ملف PDF غير موجود: {Path}",
					documentPath);

				return WhatsAppResult.Failed(
					$"ملف PDF غير موجود: {documentPath}");
			}

			var recipient =
				NormalizePhone(phone);

			if (string.IsNullOrEmpty(recipient))
			{
				_logger.LogWarning(
					"رقم هاتف غير صالح: {Phone}",
					phone);

				return WhatsAppResult.Failed(
					$"رقم هاتف غير صالح: {phone}");
			}

			try
			{
				// 1) رفع الوسائط
				var mediaId =
					await UploadMediaAsync(
						accessToken,
						phoneNumberId,
						documentPath);

				if (string.IsNullOrWhiteSpace(mediaId))
				{
					return WhatsAppResult.Failed(
						"فشل رفع المستند إلى WhatsApp");
				}

				// 2) إرسال رسالة document
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
						"فشل إرسال المستند: {Status} {Body}",
						(int)response.StatusCode,
						body);

					return WhatsAppResult.Failed(
						$"HTTP {(int)response.StatusCode}: {body}");
				}

				_logger.LogInformation(
					"تم إرسال المستند إلى {Phone}: {File}",
					recipient,
					fileName);

				return WhatsAppResult.Sent();
			}
			catch (Exception ex)
			{
				_logger.LogError(
					ex,
					"خطأ أثناء إرسال المستند إلى {Phone}",
					phone);

				return WhatsAppResult.Failed(
					$"خطأ أثناء الإرسال: {ex.Message}");
			}
		}

		// =====================================
		// اختبار الاتصال بواتساب
		// =====================================

		public async Task<WhatsAppResult> SendTestAsync(
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
						return WhatsAppResult.Failed(
							"بوابة الواتساب غير جاهزة (لم يتم مسح QR بعد أو تم فقد الاتصال).");
					}

					return await SendMessageViaGatewayAsync(
						gatewayUrl,
						_configuration["WhatsAppLocal:GatewayKey"],
						phone,
						"اختبار",
						"اختبار الواتساب تم بنجاح");
				}
				catch (Exception ex)
				{
					return WhatsAppResult.Failed(
						$"تعذر الاتصال ببوابة الواتساب: {ex.Message}");
				}
			}

			var accessToken =
				_configuration["WhatsApp:AccessToken"];

			var phoneNumberId =
				_configuration["WhatsApp:PhoneNumberId"];

			if (string.IsNullOrWhiteSpace(accessToken) ||
				string.IsNullOrWhiteSpace(phoneNumberId))
			{
				return WhatsAppResult.Failed(
					"لم يتم إعداد WhatsApp:AccessToken / WhatsApp:PhoneNumberId في appsettings.json.");
			}

			var recipient =
				NormalizePhone(phone);

			if (string.IsNullOrEmpty(recipient))
			{
				return WhatsAppResult.Failed(
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
					body = "اختبار الواتساب تم بنجاح"
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
					return WhatsAppResult.Failed(
						$"HTTP {(int)response.StatusCode}: {body}");
				}

				return WhatsAppResult.Sent();
			}
			catch (Exception ex)
			{
				return WhatsAppResult.Failed(
					$"السوكيت: {ex.Message}");
			}
		}

		// =====================================
		// رفع الوسائط وإرجاع media id
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

		public async Task<WhatsAppResult> SendActivationCodeAsync(
			string phone,
			string recipientName,
			string activationCode)
		{
			var message =
				$"مرحبًا {recipientName}،\n\n" +
				$"كود التفعيل الخاص بك هو: {activationCode}\n\n" +
				$"شكرًا لك.";

			return await SendMessageAsync(
				phone,
				recipientName,
				message);
		}

		// =====================================
		// رسالة عامة
		// =====================================

		public async Task<WhatsAppResult> SendMessageAsync(
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
				return await SendMessageViaGatewayAsync(
					gatewayUrl,
					_configuration["WhatsAppLocal:GatewayKey"],
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
					$"[MOCK] WhatsApp Message (Meta غير معد)\n" +
					$"----------------------------------------\n" +
					$"Recipient : {recipientName}\n" +
					$"Phone     : {phone}\n" +
					$"----------------------------------------\n" +
					$"{message}\n" +
					$"========================================\n";

				LogAndPrint(fullMessage);

				return WhatsAppResult.Simulated(
					"إعدادات Meta WhatsApp غير مضبوطة");
			}

			var recipient =
				NormalizePhone(phone);

			if (string.IsNullOrEmpty(recipient))
			{
				_logger.LogWarning(
					"رقم هاتف غير صالح: {Phone}",
					phone);

				return WhatsAppResult.Failed(
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
						"فشل إرسال الرسالة: {Status} {Body}",
						(int)response.StatusCode,
						body);

					return WhatsAppResult.Failed(
						$"HTTP {(int)response.StatusCode}: {body}");
				}

				return WhatsAppResult.Sent();
			}
			catch (Exception ex)
			{
				_logger.LogError(
					ex,
					"خطأ أثناء إرسال الرسالة إلى {Phone}",
					phone);

				return WhatsAppResult.Failed(
					$"خطأ أثناء الإرسال: {ex.Message}");
			}
		}

		// =====================================
		// إعادة تعيين كلمة المرور
		// =====================================

		public Task<WhatsAppResult> SendPasswordResetAsync(
			string phone,
			string recipientName,
			string code,
			string resetUrl,
			DateTime expiresAt)
		{
			var message =
				$"مرحبًا {recipientName}،\n\n" +
				$"كود إعادة تعيين كلمة المرور: {code}\n" +
				$"صالح حتى: {expiresAt:yyyy-MM-dd HH:mm}\n\n" +
				$"لإعادة تعيين كلمة المرور اضغط:\n" +
				$"{resetUrl}\n\n" +
				$"لو مش انت من طلب ده، تجاهل الرسالة.";

			return SendMessageAsync(
				phone,
				recipientName,
				message);
		}

		// =====================================
		// ملخص فاتورة شراء (نص، مش ملف)
		// =====================================

		public Task<WhatsAppResult> SendPurchaseInvoiceAsync(
			string phone,
			string supplierName,
			string invoiceNumber,
			decimal totalAmount,
			int itemCount,
			DateTime invoiceDate,
			string portalUrl)
		{
			var message =
				$"ملخص فاتورة شراء\n" +
				$"-------------------------\n" +
				$"المورد: {supplierName}\n" +
				$"رقم الفاتورة: {invoiceNumber}\n" +
				$"التاريخ: {invoiceDate:yyyy-MM-dd}\n" +
				$"عدد البنود: {itemCount}\n" +
				$"الإجمالي: {totalAmount:N2} ج.م\n" +
				$"-------------------------\n" +
				$"يمكنك مراجعتها عبر: {portalUrl}";

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
		// إرسال عبر البوابة المحلية
		// =====================================

		private async Task<WhatsAppResult> SendMessageViaGatewayAsync(
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

					return WhatsAppResult.Failed(
						$"رقم هاتف غير صالح: {phone}");
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
						$"[ERROR] WhatsApp Gateway فشل إرسال: {recipient}\n" +
						$"{(int)response.StatusCode} {body}\n");

					return WhatsAppResult.Failed(
						$"HTTP {(int)response.StatusCode}: {body}");
				}

				_logger.LogInformation(
					"تم إرسال رسالة عبر البوابة المحلية إلى {Phone}",
					recipient);

				return WhatsAppResult.Sent();
			}
			catch (Exception ex)
			{
				_logger.LogError(
					ex,
					"خطأ أثناء إرسال الرسالة عبر البوابة المحلية إلى {Phone}",
					phone);

				return WhatsAppResult.Failed(
					$"خطأ أثناء الإرسال: {ex.Message}");
			}
		}

		private async Task<WhatsAppResult> SendDocumentViaGatewayAsync(
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

					return WhatsAppResult.Failed(
						$"ملف PDF غير موجود: {documentPath}");
				}

				var recipient =
					NormalizePhone(phone);

				if (string.IsNullOrEmpty(recipient))
				{
					_logger.LogWarning(
						"رقم هاتف غير صالح (بوابة محلية): {Phone}",
						phone);

					return WhatsAppResult.Failed(
						$"رقم هاتف غير صالح: {phone}");
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
						$"[ERROR] WhatsApp Gateway فشل إرسال المستند: {recipient}\n" +
						$"{fileName}\n" +
						$"{(int)response.StatusCode} {body}\n");

					return WhatsAppResult.Failed(
						$"HTTP {(int)response.StatusCode}: {body}");
				}

				_logger.LogInformation(
					"تم إرسال المستند عبر البوابة المحلية إلى {Phone}: {File}",
					recipient,
					fileName);

				return WhatsAppResult.Sent();
			}
			catch (Exception ex)
			{
				_logger.LogError(
					ex,
					"خطأ أثناء إرسال المستند عبر البوابة المحلية إلى {Phone}",
					phone);

				return WhatsAppResult.Failed(
					$"خطأ أثناء الإرسال: {ex.Message}");
			}
		}
	}
}