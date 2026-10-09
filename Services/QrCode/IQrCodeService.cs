namespace AccountingSystem.Services.QrCode
{
	public interface IQrCodeService
	{
		string GenerateQrCode(string content);
	}
}
