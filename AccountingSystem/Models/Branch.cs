using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
	public class Branch
	{
		public int Id { get; set; }

		public int CompanyId { get; set; }

		public Company? Company { get; set; }

		[Required]
		[MaxLength(200)]
		public string Name { get; set; } = string.Empty;

		[MaxLength(50)]
		public string? Phone { get; set; }

		[MaxLength(500)]
		public string? Address { get; set; }

		public bool IsActive { get; set; } = true;

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		public ICollection<Store> Stores { get; set; }
			= new List<Store>();

		public ICollection<EmployeeBranch> EmployeeBranches { get; set; }
			= new List<EmployeeBranch>();
	}
}
