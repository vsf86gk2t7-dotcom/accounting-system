namespace AccountingSystem.Models.ViewModels.Admin
{
	public class RegistrationRequestItem
	{
		public int Id { get; set; }

		public string Type { get; set; } = string.Empty;

		public string TypeKey { get; set; } = string.Empty;

		public string Name { get; set; } = string.Empty;

		public string Phone { get; set; } = string.Empty;

		public string? Email { get; set; }

		public DateTime RequestedAt { get; set; }

		public bool IsApproved { get; set; }

		public bool HasAccount { get; set; }
	}
}
