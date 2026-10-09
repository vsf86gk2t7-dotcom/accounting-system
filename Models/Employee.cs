using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
	public class Employee
	{
		public int Id { get; set; }

		[Required]
		[MaxLength(200)]
		public string Name { get; set; } = string.Empty;

		[MaxLength(100)]
		public string? JobTitle { get; set; }

		[MaxLength(50)]
		public string? Phone { get; set; }

		[MaxLength(200)]
		public string? Email { get; set; }

		public decimal Salary { get; set; }

		public bool IsActive { get; set; } = true;

		// =========================================
		// بيانات وظيفية وشخصية
		// =========================================

		[MaxLength(14)]
		public string? NationalId { get; set; }

		public DateTime? HireDate { get; set; }

		public DateTime? BirthDate { get; set; }

		[MaxLength(500)]
		public string? Address { get; set; }

		[MaxLength(500)]
		public string? ImagePath { get; set; }

		// ✅ نوع الموظف (اللي كان ناقص)
		public EmployeeType EmployeeType { get; set; } = EmployeeType.Office;

		public int? DepartmentId { get; set; }
		public Department? Department { get; set; }         // ✅ كان ناقص

		public int? PositionId { get; set; }
		public Position? Position { get; set; }             // ✅ كان ناقص

		// =========================================
		// بوابة الموظف
		// =========================================

		public bool PortalRequested { get; set; } = false;

		public bool PortalApproved { get; set; } = false;

		public bool HasPortalAccount { get; set; } = false;

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		// =========================================
		// حسابات المستخدم المرتبطة بالموظف
		// =========================================

		public ICollection<User> Users { get; set; }
			= new List<User>();

		// =========================================
		// الفروع التي يعمل بها الموظف
		// =========================================

		public ICollection<EmployeeBranch> EmployeeBranches { get; set; }
			= new List<EmployeeBranch>();

		// =========================================
		// المخازن التي يعمل بها الموظف
		// =========================================

		public ICollection<EmployeeStore> EmployeeStores { get; set; }
			= new List<EmployeeStore>();
	}
}