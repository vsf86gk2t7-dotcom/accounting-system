namespace AccountingSystem.Models.ViewModels.Inventory
{
	// =============================================
	// صف حركة (دخول/خروج) داخل كشف المنتج
	// =============================================

	public class InventoryProductMovementRowViewModel
	{
		public DateTime CreatedAt { get; set; }

		public string TransactionNumber { get; set; } = string.Empty;

		public string TypeLabel { get; set; } = string.Empty;

		public string TypeBadgeClass { get; set; } = "bg-dark";

		public string? Source { get; set; }

		public string? DirectionLabel { get; set; }

		public bool? IsIncrease { get; set; }

		public string? MovementStoreName { get; set; }

		public string? CounterStoreName { get; set; }

		public decimal Quantity { get; set; }

		public decimal UnitCost { get; set; }

		public decimal TotalCost { get; set; }

		public string? Notes { get; set; }

		public decimal RunningBalance { get; set; }
	}

	// =============================================
	// كشف حركة منتج (فلاتر + صفوف + مجاميع)
	// =============================================

	public class InventoryProductMovementViewModel
	{
		public int? ProductId { get; set; }

		public int? StoreId { get; set; }

		public DateTime? FromDate { get; set; }

		public DateTime? ToDate { get; set; }

		public List<InventoryProductOptionViewModel> Products { get; set; }
			= new List<InventoryProductOptionViewModel>();

		public List<KeyValuePair<int, string>> Stores { get; set; }
			= new List<KeyValuePair<int, string>>();

		public List<InventoryProductMovementRowViewModel> Rows { get; set; }
			= new List<InventoryProductMovementRowViewModel>();

		public string? ProductName { get; set; }

		public decimal TotalIn { get; set; }

		public decimal TotalOut { get; set; }

		public decimal OpeningBalance { get; set; }

		public decimal NetBalance { get; set; }
	}
}
