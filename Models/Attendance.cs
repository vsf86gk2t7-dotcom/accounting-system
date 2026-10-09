using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
	public class Attendance
	{
		public int Id { get; set; }

		[Required]
		public int EmployeeId { get; set; }
		public Employee? Employee { get; set; }

		[Required]
		[DataType(DataType.Date)]
		public DateTime Date { get; set; }

		public DateTime? CheckIn { get; set; }

		public DateTime? CheckOut { get; set; }

		public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;

		[MaxLength(500)]
		public string? Notes { get; set; }

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	}

	public enum AttendanceStatus
	{
		Present = 1,
		Absent = 2,
		Late = 3,
		OnLeave = 4
	}
}
