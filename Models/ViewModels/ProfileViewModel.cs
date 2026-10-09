using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models.ViewModels
{
	public class ProfileViewModel
	{
		[Required(ErrorMessage = "الاسم مطلوب")]
		[MaxLength(200, ErrorMessage = "الاسم طويل جداً")]
		[Display(Name = "الاسم الكامل")]
		public string FullName { get; set; } = string.Empty;

		// للعرض فقط — لا يُعدّل
		[Display(Name = "رقم الهاتف")]
		public string Phone { get; set; } = string.Empty;

		[MaxLength(500)]
		[Display(Name = "العنوان")]
		public string? Address { get; set; }

		[DataType(DataType.Date)]
		[Display(Name = "تاريخ الميلاد")]
		public DateTime? BirthDate { get; set; }

		public string? ExistingImagePath { get; set; }

		[Display(Name = "الصورة الشخصية")]
		public IFormFile? ImageFile { get; set; }
	}
}