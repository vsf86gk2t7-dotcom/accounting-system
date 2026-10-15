using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models.ViewModels
{
	public class ActivateAccountViewModel
	{
		[Required(ErrorMessage = "رقم الهاتف مطلوب.")]
		[MaxLength(50)]
		[Display(Name = "رقم الهاتف")]
		public string Phone { get; set; } = string.Empty;

		[Required(ErrorMessage = "كود التفعيل مطلوب.")]
		[StringLength(
			6,
			MinimumLength = 6,
			ErrorMessage = "كود التفعيل يجب أن يكون 6 أرقام.")]
		[Display(Name = "كود التفعيل")]
		public string ActivationCode { get; set; } = string.Empty;

		[Required(ErrorMessage = "كلمة المرور مطلوبة.")]
		[MinLength(
			8,
			ErrorMessage = "كلمة المرور يجب أن تكون 8 أحرف على الأقل.")]
		[DataType(DataType.Password)]
		[Display(Name = "كلمة المرور")]
		public string Password { get; set; } = string.Empty;

		[Required(ErrorMessage = "تأكيد كلمة المرور مطلوب.")]
		[Compare(
			"Password",
			ErrorMessage = "كلمة المرور وتأكيدها غير متطابقين.")]
		[DataType(DataType.Password)]
		[Display(Name = "تأكيد كلمة المرور")]
		public string ConfirmPassword { get; set; } = string.Empty;
	}
}
