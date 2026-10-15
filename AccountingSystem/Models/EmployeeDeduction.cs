using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
	public class EmployeeDeduction
	{
		public int Id { get; set; }

		[Required]
		public int EmployeeId { get; set; }
		public Employee? Employee { get; set; }

		[Required]
		[Range(0.01, double.MaxValue, ErrorMessage = "المبلغ يجب أن يكون أكبر من صفر.")]
		public decimal Amount { get; set; }

		[Required]
		[MaxLength(500)]
		public string Reason { get; set; } = string.Empty;

		[Required]
		[DataType(DataType.Date)]
		public DateTime Date { get; set; }

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	}
}
