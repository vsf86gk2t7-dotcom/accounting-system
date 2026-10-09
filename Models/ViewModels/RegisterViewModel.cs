using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models.ViewModels
{
	public enum RegisterType
	{
		Customer = 1,
		Supplier = 2,
		Employee = 3
	}

	public class RegisterViewModel
	{
		[Required(ErrorMessage = "نوع الحساب مطلوب.")]
		[Display(Name = "نوع الحساب")]
		public RegisterType AccountType { get; set; }
			= RegisterType.Customer;

		[Required(ErrorMessage = "الاسم مطلوب.")]
		[MaxLength(200)]
		[Display(Name = "الاسم")]
		public string Name { get; set; } = string.Empty;

		[Required(ErrorMessage = "رقم الهاتف مطلوب.")]
		[MaxLength(50)]
		[Display(Name = "رقم الهاتف")]
		public string Phone { get; set; } = string.Empty;

		[EmailAddress(ErrorMessage = "البريد الإلكتروني غير صحيح.")]
		[MaxLength(200)]
		[Display(Name = "البريد الإلكتروني (اختياري)")]
		public string? Email { get; set; }

		[MaxLength(500)]
		[Display(Name = "العنوان (اختياري)")]
		public string? Address { get; set; }
	}
}
