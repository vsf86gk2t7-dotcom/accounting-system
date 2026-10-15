namespace AccountingSystem.Models
{
	public class UserPermission
	{
		public int Id { get; set; }

		public int UserId { get; set; }

		public User? User { get; set; }

		public int PermissionId { get; set; }

		public Permission? Permission { get; set; }

		public bool IsGranted { get; set; } = true;

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	}
}
