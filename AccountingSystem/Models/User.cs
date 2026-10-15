using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
	public class User
	{
		public int Id { get; set; }

		[Required]
		[MaxLength(50)]
		public string Phone { get; set; } = string.Empty;

		public string? PasswordHash { get; set; }

		public UserType UserType { get; set; }

		public int? RoleId { get; set; }
		public Role? Role { get; set; }

		public UserStatus Status { get; set; } = UserStatus.Pending;
		public bool IsActive { get; set; } = true;
		public bool IsPasswordSet { get; set; } = false;

		public string? ActivationCodeHash { get; set; }
		public DateTime? ActivationCodeExpiresAt { get; set; }

		public int FailedLoginAttempts { get; set; } = 0;
		public DateTime? LockoutUntil { get; set; }
		public string? SecurityStamp { get; set; }

		public int? CustomerId { get; set; }
		public Customer? Customer { get; set; }

		public int? SupplierId { get; set; }
		public Supplier? Supplier { get; set; }

		public int? EmployeeId { get; set; }
		public Employee? Employee { get; set; }

		// ✅ CreatedAt — مرة واحدة بس
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		// بيانات البروفايل
		[MaxLength(200)]
		public string? FullName { get; set; }

		[MaxLength(500)]
		public string? Address { get; set; }

		[DataType(DataType.Date)]
		public DateTime? BirthDate { get; set; }

		[MaxLength(500)]
		public string? ProfileImagePath { get; set; }
	}

	public enum UserType
	{
		Admin = 1,
		Employee = 2,
		Customer = 3,
		Supplier = 4
	}

	public enum UserStatus
	{
		Pending = 1,
		Approved = 2,
		Rejected = 3,
		Suspended = 4
	}
}