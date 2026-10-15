using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
	public class FiscalPeriod
	{
		public int Id { get; set; }

		[Required]
		[MaxLength(20)]
		public string PeriodName { get; set; } = string.Empty;

		[Required]
		public DateTime StartDate { get; set; }

		[Required]
		public DateTime EndDate { get; set; }

		public bool IsClosed { get; set; } = false;

		public DateTime? ClosedAt { get; set; }

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	}
}
