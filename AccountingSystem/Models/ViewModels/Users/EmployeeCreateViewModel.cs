using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models.ViewModels.Users
{
	public class EmployeeCreateViewModel
	{
		[Required(ErrorMessage = "اسم الموظف مطلوب.")]
		[MaxLength(200)]
		public string Name { get; set; } = string.Empty;

		[MaxLength(100)]
		public string? JobTitle { get; set; }

		[Required(ErrorMessage = "رقم الهاتف مطلوب.")]
		[MaxLength(50)]
		public string Phone { get; set; } = string.Empty;

		[EmailAddress(ErrorMessage = "البريد الإلكتروني غير صحيح.")]
		[MaxLength(200)]
		public string? Email { get; set; }

		[Range(
			0,
			1000000000,
			ErrorMessage = "الراتب يجب أن يكون أكبر من أو يساوي صفر.")]
		public decimal Salary { get; set; }

		// =========================================
		// الحساب
		// =========================================

		[Required(ErrorMessage = "يجب اختيار الدور.")]
		public int RoleId { get; set; }

		[Required(ErrorMessage = "كلمة المرور مطلوبة.")]
		[MinLength(
			8,
			ErrorMessage = "كلمة المرور يجب ألا تقل عن 8 أحرف.")]
		public string Password { get; set; } = string.Empty;

		[Compare(
			"Password",
			ErrorMessage = "تأكيد كلمة المرور غير مطابق.")]
		public string ConfirmPassword { get; set; } = string.Empty;

		// =========================================
		// نطاق العمل
		// =========================================

		public List<int> BranchIds { get; set; }
			= new List<int>();

		public List<int> StoreIds { get; set; }
			= new List<int>();

		// =========================================
		// البيانات التي ستظهر في الشاشة
		// =========================================

		public List<RoleOptionViewModel> Roles { get; set; }
			= new List<RoleOptionViewModel>();

		public List<BranchOptionViewModel> Branches { get; set; }
			= new List<BranchOptionViewModel>();

		public List<StoreOptionViewModel> Stores { get; set; }
			= new List<StoreOptionViewModel>();
	}

	public class RoleOptionViewModel
	{
		public int Id { get; set; }

		public string Name { get; set; } = string.Empty;

		public string Code { get; set; } = string.Empty;
	}

	public class BranchOptionViewModel
	{
		public int Id { get; set; }

		public string Name { get; set; } = string.Empty;

		public bool IsSelected { get; set; }
	}

	public class StoreOptionViewModel
	{
		public int Id { get; set; }

		public int BranchId { get; set; }

		public string Name { get; set; } = string.Empty;

		public string? Code { get; set; }

		public bool IsSelected { get; set; }
	}
}
