using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
	/// <summary>
	/// مستوى أهمية الإشعار — يحدد اللون والأيقونة في الواجهة.
	/// </summary>
	public enum NotificationLevel
	{
		Normal = 0,   // عادية — أزرق
		Medium = 1,   // متوسطة — برتقالي
		Critical = 2  // حرجة — أحمر
	}

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

		/// <summary>مستوى الأهمية — يحدد لون الإشعار.</summary>
		public NotificationLevel Level { get; set; }
			= NotificationLevel.Normal;

		public bool IsRead { get; set; }

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		// =========================================
		// ملاحة
		// =========================================

		public User? User { get; set; }
	}
}