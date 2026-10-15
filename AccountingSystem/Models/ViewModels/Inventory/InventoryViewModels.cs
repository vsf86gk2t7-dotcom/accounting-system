using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models.ViewModels.Inventory
{
	// =============================================
	// رصيد منتج داخل مخزن
	// =============================================

	public class InventoryBalanceRowViewModel
	{
		public int ProductId { get; set; }

		public string ProductName { get; set; } = string.Empty;

		public string? ProductCode { get; set; }

		public string? Barcode { get; set; }

		public int StoreId { get; set; }

		public string StoreName { get; set; } = string.Empty;

		public decimal Quantity { get; set; }

		public decimal Value { get; set; }

		public decimal AverageCost { get; set; }

		public decimal MinQuantity { get; set; }

		public bool IsBelowMin =>
			MinQuantity > 0 && Quantity < MinQuantity;

		public bool IsOutOfStock => Quantity <= 0;
	}

	// =============================================
	// شاشة الأرصدة (فلترة + صفوف)
	// =============================================

	public class InventoryIndexViewModel
	{
		public List<InventoryBalanceRowViewModel> Rows { get; set; }
			= new List<InventoryBalanceRowViewModel>();

		public int? StoreId { get; set; }

		public string? Search { get; set; }

		public List<KeyValuePair<int, string>> Stores { get; set; }
			= new List<KeyValuePair<int, string>>();
	}

	// =============================================
	// بند استلام
	// =============================================

	public class InventoryItemLineViewModel
	{
		public int ProductId { get; set; }

		[Range(0.000001, double.MaxValue,
			ErrorMessage = "الكمية يجب أن تكون أكبر من صفر.")]
		public decimal Quantity { get; set; }
	}

	public class InventoryReceiveItemViewModel : InventoryItemLineViewModel
	{
		[Range(0, double.MaxValue,
			ErrorMessage = "التكلفة يجب ألا تكون سالبة.")]
		public decimal UnitCost { get; set; }
	}

	// =============================================
	// استلام مخزون
	// =============================================

	public class InventoryReceiveViewModel
	{
		[Range(1, int.MaxValue,
			ErrorMessage = "يجب اختيار المخزن.")]
		public int StoreId { get; set; }

		[Range(1, int.MaxValue,
			ErrorMessage = "يجب اختيار المورد.")]
		public int SupplierId { get; set; }

		[MaxLength(
			1000,
			ErrorMessage = "الملاحظات لا يمكن أن تتجاوز 1000 حرف.")]
		public string? Notes { get; set; }

		public List<InventoryReceiveItemViewModel> Items { get; set; }
			= new List<InventoryReceiveItemViewModel>();

		// بيانات الإعداد للعرض

		public List<KeyValuePair<int, string>> Stores { get; set; }
			= new List<KeyValuePair<int, string>>();

		public List<KeyValuePair<int, string>> Suppliers { get; set; }
			= new List<KeyValuePair<int, string>>();

		public List<InventoryProductOptionViewModel> Products { get; set; }
			= new List<InventoryProductOptionViewModel>();
	}

	// =============================================
	// صرف مخزون
	// =============================================

	public class InventoryIssueViewModel
	{
		[Range(1, int.MaxValue,
			ErrorMessage = "يجب اختيار المخزن.")]
		public int StoreId { get; set; }

		[MaxLength(
			1000,
			ErrorMessage = "الملاحظات لا يمكن أن تتجاوز 1000 حرف.")]
		public string? Notes { get; set; }

		public List<InventoryItemLineViewModel> Items { get; set; }
			= new List<InventoryItemLineViewModel>();

		[Display(Name = "السبب")]
		[MaxLength(
			500,
			ErrorMessage = "السبب لا يمكن أن يتجاوز 500 حرف.")]
		public string? Reason { get; set; }

		public List<KeyValuePair<int, string>> Stores { get; set; }
			= new List<KeyValuePair<int, string>>();

		public List<InventoryProductOptionViewModel> Products { get; set; }
			= new List<InventoryProductOptionViewModel>();
	}

	// =============================================
	// تحويل بين المخازن
	// =============================================

	public class InventoryTransferViewModel
	{
		[Range(1, int.MaxValue,
			ErrorMessage = "يجب اختيار المخزن المصدر.")]
		public int FromStoreId { get; set; }

		[Range(1, int.MaxValue,
			ErrorMessage = "يجب اختيار المخزن الوجهة.")]
		public int ToStoreId { get; set; }

		[MaxLength(
			1000,
			ErrorMessage = "الملاحظات لا يمكن أن تتجاوز 1000 حرف.")]
		public string? Notes { get; set; }

		public List<InventoryItemLineViewModel> Items { get; set; }
			= new List<InventoryItemLineViewModel>();

		public List<KeyValuePair<int, string>> Stores { get; set; }
			= new List<KeyValuePair<int, string>>();

		public List<InventoryProductOptionViewModel> Products { get; set; }
			= new List<InventoryProductOptionViewModel>();
	}

	// =============================================
	// تسوية مخزون (منتج واحد لكل تسوية)
	// =============================================

	public class InventoryAdjustViewModel
	{
		[Range(1, int.MaxValue,
			ErrorMessage = "يجب اختيار المخزن.")]
		public int StoreId { get; set; }

		[Range(1, int.MaxValue,
			ErrorMessage = "يجب اختيار المنتج.")]
		public int ProductId { get; set; }

		[Range(0, double.MaxValue,
			ErrorMessage = "الكمية الجديدة يجب ألا تكون سالبة.")]
		[Display(Name = "الكمية الجديدة")]
		public decimal NewQuantity { get; set; }

		[Required(ErrorMessage = "سبب التسوية مطلوب.")]
		[MaxLength(
			1000,
			ErrorMessage = "السبب لا يمكن أن يتجاوز 1000 حرف.")]
		[Display(Name = "سبب التسوية")]
		public string Reason { get; set; } = string.Empty;

		[Display(Name = "الكمية الحالية")]
		public decimal CurrentQuantity { get; set; }

		public List<KeyValuePair<int, string>> Stores { get; set; }
			= new List<KeyValuePair<int, string>>();

		public List<InventoryProductOptionViewModel> Products { get; set; }
			= new List<InventoryProductOptionViewModel>();
	}

	// =============================================
	// بند جرد
	// =============================================

	public class InventoryCountLineViewModel
	{
		public int ProductId { get; set; }

		public string ProductName { get; set; } = string.Empty;

		public decimal SystemQuantity { get; set; }

		public decimal CountedQuantity { get; set; }
	}

	// =============================================
	// جرد مخزن
	// =============================================

	public class InventoryCountViewModel
	{
		[Range(1, int.MaxValue,
			ErrorMessage = "يجب اختيار المخزن.")]
		public int StoreId { get; set; }

		[MaxLength(
			1000,
			ErrorMessage = "الملاحظات لا يمكن أن تتجاوز 1000 حرف.")]
		public string? Notes { get; set; }

		public List<InventoryCountLineViewModel> Lines { get; set; }
			= new List<InventoryCountLineViewModel>();

		public List<KeyValuePair<int, string>> Stores { get; set; }
			= new List<KeyValuePair<int, string>>();
	}

	// =============================================
	// خيار منتج (للصحفة)
	// =============================================

	public class InventoryProductOptionViewModel
	{
		public int Id { get; set; }

		public string Name { get; set; } = string.Empty;

		public string? Code { get; set; }
	}

	// =============================================
	// سجل الحركات
	// =============================================

	public class InventoryTransactionsViewModel
	{
		public List<InventoryTransactionRowViewModel> Rows { get; set; }
			= new List<InventoryTransactionRowViewModel>();

		public int? StoreId { get; set; }

		public StockTransactionType? Type { get; set; }

		public List<KeyValuePair<int, string>> Stores { get; set; }
			= new List<KeyValuePair<int, string>>();
	}

	public class InventoryTransactionRowViewModel
	{
		public int Id { get; set; }

		public string TransactionNumber { get; set; } = string.Empty;

		public StockTransactionType Type { get; set; }

		public string? FromStoreName { get; set; }

		public string? ToStoreName { get; set; }

		public string? CreatedByUserName { get; set; }

		public DateTime CreatedAt { get; set; }

		public int ItemCount { get; set; }

		public string? Notes { get; set; }

		public List<InventoryTransactionLineRowViewModel> Items { get; set; }
			= new List<InventoryTransactionLineRowViewModel>();
	}

	public class InventoryTransactionLineRowViewModel
	{
		public string ProductName { get; set; } = string.Empty;

		public decimal Quantity { get; set; }

		public decimal UnitCost { get; set; }
	}

	// =============================================
	// دفعات المخزون (Stock Lots)
	// =============================================

	public class InventoryLotsViewModel
	{
		public List<InventoryLotRowViewModel> Rows { get; set; }
			= new List<InventoryLotRowViewModel>();

		public int? StoreId { get; set; }

		public int? ProductId { get; set; }

		public bool OnlyAvailable { get; set; } = true;

		public string? Search { get; set; }

		public decimal TotalRemainingValue { get; set; }

		public List<KeyValuePair<int, string>> Stores { get; set; }
			= new List<KeyValuePair<int, string>>();

		public List<KeyValuePair<int, string>> Products { get; set; }
			= new List<KeyValuePair<int, string>>();
	}

	public class InventoryLotRowViewModel
	{
		public int Id { get; set; }

		public string ProductName { get; set; } = string.Empty;

		public string? ProductCode { get; set; }

		public string StoreName { get; set; } = string.Empty;

		public string SupplierName { get; set; } = string.Empty;

		public DateTime PurchaseDate { get; set; }

		public decimal QuantityReceived { get; set; }

		public decimal QuantityRemaining { get; set; }

		public decimal UnitCost { get; set; }

		public decimal RemainingValue { get; set; }
	}
}
