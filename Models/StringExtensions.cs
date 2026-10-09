namespace AccountingSystem.Models
{
	public static class StringExtensions
	{
		public static string? TrimOrNull(this string? value) =>
			string.IsNullOrWhiteSpace(value) ? null : value.Trim();

		public static string TrimOrEmpty(this string? value) =>
			value?.Trim() ?? string.Empty;
	}
}
