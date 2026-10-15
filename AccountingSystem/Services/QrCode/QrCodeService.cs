using QRCoder;

namespace AccountingSystem.Services.QrCode
{
	public class QrCodeService : IQrCodeService
	{
		public string GenerateQrCode(string content)
		{
			if (string.IsNullOrWhiteSpace(content))
			{
				return string.Empty;
			}

			using var qrGenerator = new QRCodeGenerator();

			using var qrCodeData =
				qrGenerator.CreateQrCode(
					content,
					QRCodeGenerator.ECCLevel.Q);

			using var qrCode =
				new PngByteQRCode(qrCodeData);

			var qrCodeBytes =
				qrCode.GetGraphic(20);

			return Convert.ToBase64String(qrCodeBytes);
		}
	}
}
