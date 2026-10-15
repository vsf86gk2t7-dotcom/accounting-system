namespace AccountingSystem.Models
{
	public class EmployeeBranch
	{
		public int EmployeeId { get; set; }

		public Employee Employee { get; set; } = null!;

		public int BranchId { get; set; }

		public Branch Branch { get; set; } = null!;

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	}
}
