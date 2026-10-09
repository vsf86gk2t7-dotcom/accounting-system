using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
	public class LeaveRequest
	{
		public int Id { get; set; }

		[Required]
		public int EmployeeId { get; set; }
		public Employee? Employee { get; set; }

		[Required]
		[DataType(DataType.Date)]
		public DateTime StartDate { get; set; }

		[Required]
		[DataType(DataType.Date)]
		public DateTime EndDate { get; set; }

		[Required]
		[MaxLength(500)]
		public string Reason { get; set; } = string.Empty;

		public LeaveStatus Status { get; set; } = LeaveStatus.Pending;

		public int? ApprovedBy { get; set; }
		public User? ApprovedByUser { get; set; }

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	}

	public enum LeaveStatus
	{
		Pending = 1,
		Approved = 2,
		Rejected = 3
	}
}
