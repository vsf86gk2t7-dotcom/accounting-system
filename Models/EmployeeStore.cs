namespace AccountingSystem.Models
{
	public class EmployeeStore
	{
		public int EmployeeId { get; set; }

		public Employee Employee { get; set; } = null!;

		public int StoreId { get; set; }

		public Store Store { get; set; } = null!;

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	}
}
