using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
	public class PasswordResetRequest
	{
		public int Id { get; set; }

		[Required]
		[MaxLength(50)]
		public string Phone { get; set; } = string.Empty;

		[Required]
		[MaxLength(200)]
		public string UserName { get; set; } = string.Empty;

		public UserType UserType { get; set; }

		[Required]
		[MaxLength(200)]
		public string CodeHash { get; set; } = string.Empty;

		public int Attempts { get; set; } = 0;

		public DateTime? LockedUntil { get; set; }

		public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

		public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(15);

		public bool IsSent { get; set; } = false;

		public DateTime? SentAt { get; set; }

		public string? SentBy { get; set; }

		public bool IsCompleted { get; set; } = false;

		public DateTime? CompletedAt { get; set; }
	}
}
