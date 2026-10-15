using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models.ViewModels.Returns
{
	// =============================================
	// بنود المرتجع المشتركة
	// =============================================

	public abstract class ReturnItemViewModel
	{
		[Range(1, int.MaxValue,
			ErrorMessage = "يجب اختيار المنتج.")]
		public int ProductId { get; set; }

		[Range(0.000001, double.MaxValue,
			ErrorMessage = "الكمية يجب أن تكون أكبر من صفر.")]
		public decimal Quantity { get; set; }
	}

	public class PurchaseReturnItemViewModel : ReturnItemViewModel
	{
	}

	// =============================================
	// مرتجع شراء
	// =============================================

	public class PurchaseReturnViewModel
	{
		[Range(1, int.MaxValue,
			ErrorMessage = "يجب اختيار المورد.")]
		public int SupplierId { get; set; }

		[Range(1, int.MaxValue,
			ErrorMessage = "يجب اختيار المخزن.")]
		public int StoreId { get; set; }

		[DataType(DataType.Date)]
		[Display(Name = "تاريخ المرتجع")]
		public DateTime ReturnDate { get; set; } = DateTime.Today;

		[MaxLength(
			1000,
			ErrorMessage = "السبب لا يمكن أن يتجاوز 1000 حرف.")]
		[Display(Name = "سبب المرتجع")]
		public string? Reason { get; set; }

		public List<PurchaseReturnItemViewModel> Items { get; set; }
			= new List<PurchaseReturnItemViewModel>();

		public List<KeyValuePair<int, string>> Suppliers { get; set; }
			= new List<KeyValuePair<int, string>>();

		public List<KeyValuePair<int, string>> Stores { get; set; }
			= new List<KeyValuePair<int, string>>();

		public List<ReturnProductOptionViewModel> Products { get; set; }
			= new List<ReturnProductOptionViewModel>();
	}

	// =============================================
	// خيار منتج للشاشات
	// =============================================

	public class ReturnProductOptionViewModel
	{
		public int Id { get; set; }

		public string Name { get; set; } = string.Empty;

		public string? Code { get; set; }
	}
}
