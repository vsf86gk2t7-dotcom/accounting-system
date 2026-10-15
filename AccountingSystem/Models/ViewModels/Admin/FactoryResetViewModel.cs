using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models.ViewModels.Admin
{
	public class FactoryResetViewModel
	{
		[Required(ErrorMessage = "كلمة المرور مطلوبة.")]
		[DataType(DataType.Password)]
		public string? Password { get; set; }

		[Required(ErrorMessage = "كلمة التأكيد مطلوبة.")]
		public string? ConfirmationWord { get; set; }
	}
}
