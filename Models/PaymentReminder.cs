namespace AccountingSystem.Models
{
	/// <summary>
	/// سجل تذكير الدفع — يمنع إرسال نفس التذكير أكثر من مرة لكل فاتورة.
	/// </summary>
	public class PaymentReminder
	{
		public int Id { get; set; }

		public int InvoiceId { get; set; }

		/// <summary>
		/// 1 = فاتورة بيع، 2 = فاتورة شراء
		/// </summary>
		public int InvoiceType { get; set; }

		public DateTime DueDate { get; set; }

		public DateTime RemindedAt { get; set; } = DateTime.UtcNow;

		/// <summary>
		/// عدد الأيام بين الاستحقاق والتذكير.
		/// (-) = قبل الاستحقاق، (+) = متأخر.
		/// </summary>
		public int DaysDifference { get; set; }

		/// <summary>
		/// نوع التذكير: 0 = قبل الاستحقاق، 1 = متأخر
		/// </summary>
		public ReminderType Type { get; set; }

		public bool IsSent { get; set; } = false;

		public DateTime? SentAt { get; set; }

		public int CreatedByUserId { get; set; }

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	}

	public enum ReminderType
	{
		Upcoming = 0,
		Overdue = 1
	}
}
