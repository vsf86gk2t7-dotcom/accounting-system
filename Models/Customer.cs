using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
	public class Customer
	{
		public int Id { get; set; }

		[Required]
		[MaxLength(200)]
		public string Name { get; set; } = string.Empty;

		[Required]
		[MaxLength(50)]
		public string Phone { get; set; } = string.Empty;

		[MaxLength(200)]
		public string? Email { get; set; }

		[MaxLength(500)]
		public string? Address { get; set; }

		public decimal OpeningBalance { get; set; }

		[Range(0, 365,
			ErrorMessage = "أيام الائتمان يجب أن تكون بين 0 و 365.")]
		public int CreditDays { get; set; }
			= 0;

		public bool IsActive { get; set; } = true;

		public bool PortalRequested { get; set; } = false;

		public bool PortalApproved { get; set; } = false;

		public bool HasPortalAccount { get; set; } = false;

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		// =========================================
		// انتماء العميل لشركة (لاحتياجات تحويل المحافظ
		// بين شركات متعددة)
		// =========================================

		public int? CompanyId { get; set; }

		public Company? Company { get; set; }

		// =========================================
		// المندوب المسؤول عن العميل
		// =========================================

		public int? SalesRepId { get; set; }

		public SalesRepProfile? SalesRep { get; set; }

		public ICollection<User> Users { get; set; } = new List<User>();
	}
}