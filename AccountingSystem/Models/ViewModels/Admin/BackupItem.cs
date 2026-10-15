namespace AccountingSystem.Models.ViewModels.Admin
{
	public class BackupItem
	{
		public string FileName { get; set; } = string.Empty;

		public decimal SizeMb { get; set; }

		public DateTime CreatedAt { get; set; }
	}
}
