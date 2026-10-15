using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
	public class Company
	{
		public int Id { get; set; }

		[Required]
		[MaxLength(200)]
		public string Name { get; set; } = string.Empty;

		[MaxLength(50)]
		public string? Phone { get; set; }

		[MaxLength(200)]
		public string? Email { get; set; }

		[MaxLength(500)]
		public string? Address { get; set; }

		[MaxLength(50)]
		public string? TaxNumber { get; set; }

		[MaxLength(50)]
		public string? CommercialRegister { get; set; }

		[MaxLength(255)]
		public string? LogoPath { get; set; }

		public bool IsActive { get; set; } = true;

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		public ICollection<Branch> Branches { get; set; }
			= new List<Branch>();
	}
}
