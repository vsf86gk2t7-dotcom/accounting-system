using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models.ViewModels.Returns
{
	// =============================================
	// ط¨ظ†ظˆط¯ ط§ظ„ظ…ط±طھط¬ط¹ ط§ظ„ظ…ط´طھط±ظƒط©
	// =============================================

	public abstract class ReturnItemViewModel
	{
		[Range(1, int.MaxValue,
			ErrorMessage = "ظٹط¬ط¨ ط§ط®طھظٹط§ط± ط§ظ„ظ…ظ†طھط¬.")]
		public int ProductId { get; set; }

		[Range(0.000001, double.MaxValue,
			ErrorMessage = "ط§ظ„ظƒظ…ظٹط© ظٹط¬ط¨ ط£ظ† طھظƒظˆظ† ط£ظƒط¨ط± ظ…ظ† طµظپط±.")]
		public decimal Quantity { get; set; }
	}

	public class PurchaseReturnItemViewModel : ReturnItemViewModel
		{
			public int? PurchaseInvoiceItemId { get; set; }
		}

	// =============================================
	// ظ…ط±طھط¬ط¹ ط´ط±ط§ط،
	// =============================================

	public class PurchaseReturnViewModel
	{
		[Range(1, int.MaxValue,
			ErrorMessage = "ظٹط¬ط¨ ط§ط®طھظٹط§ط± ط§ظ„ظ…ظˆط±ط¯.")]
		public int SupplierId { get; set; }

		[Range(1, int.MaxValue,
			ErrorMessage = "ظٹط¬ط¨ ط§ط®طھظٹط§ط± ط§ظ„ظ…ط®ط²ظ†.")]
		public int StoreId { get; set; }

		[DataType(DataType.Date)]
		[Display(Name = "طھط§ط±ظٹط® ط§ظ„ظ…ط±طھط¬ط¹")]
		public DateTime ReturnDate { get; set; } = DateTime.Today;

		[MaxLength(
			1000,
			ErrorMessage = "ط§ظ„ط³ط¨ط¨ ظ„ط§ ظٹظ…ظƒظ† ط£ظ† ظٹطھط¬ط§ظˆط² 1000 ط­ط±ظپ.")]
		[Display(Name = "ط³ط¨ط¨ ط§ظ„ظ…ط±طھط¬ط¹")]
		public string? Reason { get; set; }

		public List<PurchaseReturnItemViewModel> Items { get; set; }
			= new List<PurchaseReturnItemViewModel>();

		[Display(Name = "فاتورة الشراء الأصلية")]
		public int? OriginalPurchaseInvoiceId { get; set; }

		public List<KeyValuePair<int, string>> OriginalInvoices { get; set; }
			= new List<KeyValuePair<int, string>>();

		public List<KeyValuePair<int, string>> Suppliers { get; set; }
			= new List<KeyValuePair<int, string>>();

		public List<KeyValuePair<int, string>> Stores { get; set; }
			= new List<KeyValuePair<int, string>>();

		public List<ReturnProductOptionViewModel> Products { get; set; }
			= new List<ReturnProductOptionViewModel>();
	}

	// =============================================
	// ط®ظٹط§ط± ظ…ظ†طھط¬ ظ„ظ„ط´ط§ط´ط§طھ
	// =============================================

	public class ReturnProductOptionViewModel
	{
		public int Id { get; set; }

		public string Name { get; set; } = string.Empty;

		public string? Code { get; set; }
	}
}
