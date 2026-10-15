namespace AccountingSystem.Models
{
	/// <summary>
	/// سجل تدقيق تلقائي لأي تغيير يتم على جدول بيانات الأعمال
	/// (إضافة / تعديل / حذف).
	/// </summary>
	public class AuditLog
	{
		public int Id { get; set; }

		/// <summary>معرّف المستخدم الذي نفذ العملية.</summary>
		public int? UserId { get; set; }

		/// <summary>اسم المستخدم / رقم هاتفه.</summary>
		public string? UserName { get; set; }

		/// <summary>عنوان المستقبل أثناء العملية.</summary>
		public string? IpAddress { get; set; }

		/// <summary>
		/// نوع العملية: إنشاء / تعديل / حذف.
		/// </summary>
		public string Action { get; set; } = string.Empty;

		/// <summary>اسم الجدول/الكيان المتأثر.</summary>
		public string EntityName { get; set; } = string.Empty;

		/// <summary>المعرّف الأساسي للسجل المتأثر إن وُجد.</summary>
		public string EntityId { get; set; } = string.Empty;

		/// <summary>
		/// المفتاح / وصف مختصر يساعد في التعرف على السجل.
		/// </summary>
		public string? Summary { get; set; }

		/// <summary>
		/// JSON يصف الحقول المتغيرة قبل وبعد التعديل.
		/// </summary>
		public string? Details { get; set; }

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	}
}
