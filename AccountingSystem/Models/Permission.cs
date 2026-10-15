using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
	public class Permission
	{
		public int Id { get; set; }

		// الكود الداخلي للصلاحية
		// مثال: company.view
		[Required]
		[MaxLength(100)]
		public string Code { get; set; } = string.Empty;

		// الاسم الذي سيظهر للمستخدم
		// مثال: عرض الشركات
		[Required]
		[MaxLength(200)]
		public string Name { get; set; } = string.Empty;

		// المجموعة الرئيسية
		// مثال: Company / Branch / Store
		[Required]
		[MaxLength(100)]
		public string Group { get; set; } = string.Empty;

		// وصف اختياري للصلاحية
		[MaxLength(500)]
		public string? Description { get; set; }

		// هل الصلاحية فعالة؟
		public bool IsActive { get; set; } = true;

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		// الصلاحيات المرتبطة بالأدوار
		public ICollection<RolePermission> RolePermissions { get; set; }
			= new List<RolePermission>();

		// الصلاحيات الممنوحة مباشرة للمستخدم
		public ICollection<UserPermission> UserPermissions { get; set; }
			= new List<UserPermission>();
	}
}
