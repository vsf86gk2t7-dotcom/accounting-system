using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models.ViewModels
{
	public class ResetPasswordViewModel
	{
		[Required]
		[MaxLength(50)]
		public string Phone { get; set; } = string.Empty;

		[Required(ErrorMessage = "كود التحقق مطلوب.")]
		[StringLength(
			6,
			MinimumLength = 6,
			ErrorMessage = "كود التحقق يجب أن يكون 6 أرقام.")]
		[Display(Name = "كود التحقق")]
		public string VerificationCode { get; set; } = string.Empty;

		[Required(ErrorMessage = "كلمة المرور مطلوبة.")]
		[MinLength(
			8,
			ErrorMessage = "كلمة المرور يجب أن تكون 8 أحرف على الأقل.")]
		[RegularExpression(
			"^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d).{8,}$",
			ErrorMessage = "كلمة المرور يجب أن تحتوي على حرف كبير وحرف صغير ورقم على الأقل.")]
		[DataType(DataType.Password)]
		[Display(Name = "كلمة المرور الجديدة")]
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
