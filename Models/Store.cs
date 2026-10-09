using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
	public class Store
	{
		public int Id { get; set; }

		public int BranchId { get; set; }

		public Branch? Branch { get; set; }

		[Required]
		[MaxLength(200)]
		public string Name { get; set; } = string.Empty;

		[MaxLength(50)]
		public string? Code { get; set; }

		public bool IsActive { get; set; } = true;

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		// =========================================
		// الموظفون
		// =========================================

		public ICollection<EmployeeStore> EmployeeStores { get; set; }
			= new List<EmployeeStore>();
	}
}
