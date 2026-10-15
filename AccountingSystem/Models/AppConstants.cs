namespace AccountingSystem.Models
{
	public static class AppConstants
	{
		// إعدادات الأمان
		public const int MaxFailedLoginAttempts = 5;
		public const int LockoutDurationMinutes = 30;
		public const int ActivationCodeExpiryMinutes = 15;
		public const int MaxVerificationAttempts = 5;
		public const int PortalCodeExpiryHours = 24;

		// إعدادات التسلسل
		public const int DefaultSequenceDigits = 4;

		// إعدادات الصفحات
		public const int DefaultPageSize = 200;
		public const int SmallPageSize = 50;
		public const int RecentItemsCount = 5;

		// إعدادات الصور
		public const int MaxLogoSizeBytes = 2 * 1024 * 1024;

		// إعدادات الخزينة
		public const decimal WalletTransferSameNetworkFee = 1m;
		public const decimal WalletTransferOtherNetworkFee = 15m;
		public const decimal CommissionRate = 0.01m;

		// إعدادات المحاسبة
		public const decimal BalanceTolerance = 0.005m;
		public const decimal BudgetTolerance = 0.01m;
	}
}
