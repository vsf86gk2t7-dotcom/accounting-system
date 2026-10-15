using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
	/// <summary>
	/// إشعار داخل التطبيق يظهر في الجرس أعلى الشاشة.
	/// </summary>
	public class AppNotification
	{
		public int Id { get; set; }

		/// <summary>المستخدم المستهدف (وليس مدير النظام).</summary>
		public int UserId { get; set; }

		[MaxLength(200)]
		public string Title { get; set; } = string.Empty;

		[MaxLength(2000)]
		public string? Message { get; set; }

		/// <summary>أيقونة Bootstrap لإظهارها بجانب العنوان.</summary>
		[MaxLength(50)]
		public string Icon { get; set; } = "bi-bell";

		/// <summary>رابط تفصيلي عند النقر (اختياري).</summary>
		[MaxLength(500)]
		public string? Url { get; set; }

		public bool IsRead { get; set; }

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		// =========================================
		// ملاحة
		// =========================================

		public User? User { get; set; }
	}
}
