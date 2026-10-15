namespace AccountingSystem.Models.ViewModels.Dashboard
{
	public class DashboardViewModel
	{
		// =========================================
		// الفترة المختارة
		// =========================================

		public string Range { get; set; } = "month";
		public string RangeLabel { get; set; } = string.Empty;
		public DateTime From { get; set; }
		public DateTime To { get; set; }

		// =========================================
		// صلاحيات العرض
		// =========================================

		public bool IsAdmin { get; set; }
		public HashSet<string> Permissions { get; set; } = new();

		public bool Can(string code) => IsAdmin || Permissions.Contains(code);

		public bool CanSales { get; set; }
		public bool CanPurchases { get; set; }
		public bool CanAccounting { get; set; }
		public bool CanTreasury { get; set; }
		public bool CanCustomers { get; set; }
		public bool CanSuppliers { get; set; }
		public bool CanInventory { get; set; }

		// =========================================
		// الأعداد الأساسية
		// =========================================

		public int Companies { get; set; }
		public int Branches { get; set; }
		public int Stores { get; set; }
		public int Products { get; set; }
		public int Categories { get; set; }
		public int Units { get; set; }
		public int Customers { get; set; }
		public int Suppliers { get; set; }
		public int Employees { get; set; }
		public int Users { get; set; }
		public int SalesInvoices { get; set; }
		public int PurchaseInvoices { get; set; }
		public int ConfirmedSales { get; set; }
		public int PostedPurchases { get; set; }

		// =========================================
		// مؤشرات الفترة
		// =========================================

		public decimal SalesTotal { get; set; }
		public decimal SalesPrevious { get; set; }
		public int SalesCount { get; set; }

		public decimal PurchaseTotal { get; set; }
		public decimal PurchasePrevious { get; set; }
		public int PurchaseCount { get; set; }

		public decimal Revenue { get; set; }
		public decimal Expenses { get; set; }
		public decimal NetProfit { get; set; }
		public decimal NetProfitPrevious { get; set; }

		// =========================================
		// أرصدة لحظية
		// =========================================

		public decimal CashBalance { get; set; }
		public decimal TodayReceipts { get; set; }
		public decimal TodayPayments { get; set; }
		public List<NamedAmount> CashAccounts { get; set; } = new();

		public decimal Receivables { get; set; }
		public decimal Payables { get; set; }
		public List<NamedAmount> TopDebtors { get; set; } = new();
		public List<NamedAmount> TopCreditors { get; set; } = new();

		// =========================================
		// المخزون
		// =========================================

		public decimal StockValue { get; set; }
		public int LowStockProducts { get; set; }
		public int OutOfStockProducts { get; set; }
		public List<LowStockItem> LowStockList { get; set; } = new();

		// =========================================
		// تنبيهات
		// =========================================

		public int DraftSales { get; set; }
		public int DraftPurchases { get; set; }
		public int OverdueSalesCount { get; set; }
		public decimal OverdueSalesAmount { get; set; }
		public int OverduePurchasesCount { get; set; }
		public decimal OverduePurchasesAmount { get; set; }
		public int UpcomingDueSalesCount { get; set; }
		public int PendingPortalRequests { get; set; }

		// =========================================
		// الرسوم البيانية
		// =========================================

		public List<TrendPoint> Trend { get; set; } = new();
		public List<NamedAmount> TopProducts { get; set; } = new();
		public List<NamedAmount> TopCustomers { get; set; } = new();
		public List<NamedAmount> PaymentMix { get; set; } = new();

		// =========================================
		// أحدث السجلات
		// =========================================

		public List<SalesInvoice> RecentSales { get; set; } = new();
		public List<PurchaseInvoice> RecentPurchases { get; set; } = new();
	}

	public class NamedAmount
	{
		public int? Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public decimal Amount { get; set; }
	}

	public class LowStockItem
	{
		public int Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public decimal Remaining { get; set; }
		public decimal Min { get; set; }
	}

	public class TrendPoint
	{
		public string Label { get; set; } = string.Empty;
		public decimal Sales { get; set; }
		public decimal Purchases { get; set; }
	}
}