using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models.ViewModels
{
	public class ForgotPasswordViewModel
	{
		[Required(ErrorMessage = "رقم الهاتف مطلوب.")]
		[MaxLength(50)]
		[Display(Name = "رقم الهاتف")]
		public string Phone { get; set; } = string.Empty;
	}
}
