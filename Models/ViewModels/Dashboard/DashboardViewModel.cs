namespace AccountingSystem.Models.ViewModels.Dashboard
{
	public class DashboardViewModel
	{
		// === Summary Statistics ===
		public decimal GrossSales { get; set; }
		public decimal Discounts { get; set; }
		public decimal SalesReturns { get; set; }
		public decimal NetSales { get; set; }
		public decimal CostOfGoodsSold { get; set; }
		public decimal GrossProfit { get; set; }
		public decimal GrossMarginPercent { get; set; }

		// === Properties for Home/Index compatibility ===
		public string Range { get; set; } = "";
		public string RangeLabel { get; set; } = "";
		public DateTime From { get; set; }
		public DateTime To { get; set; }
		public bool IsAdmin { get; set; }
		public object Permissions { get; set; }

		public bool Can(string permission)
		{
			// Default implementation - overridden based on user role/context
			return true;
		}

		// === Permission properties for compatibility ===
		public bool CanSales { get; set; }
		public bool CanPurchases { get; set; }
		public bool CanAccounting { get; set; }
		public bool CanTreasury { get; set; }
		public bool CanCustomers { get; set; }
		public bool CanSuppliers { get; set; }
		public bool CanInventory { get; set; }

		// === Summary counts and totals ===
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
		public int OverdueSalesCount { get; set; }
		public decimal OverdueSalesAmount { get; set; }
		public int ConfirmedSales { get; set; }
		public int PostedPurchases { get; set; }
		public decimal SalesTotal { get; set; }
		public int SalesCount { get; set; }
		public decimal SalesPrevious { get; set; }
		public decimal PurchaseTotal { get; set; }
		public int PurchaseCount { get; set; }
		public decimal PurchasePrevious { get; set; }
		public decimal Revenue { get; set; }
		public decimal Expenses { get; set; }
		public decimal NetProfit { get; set; }
		public decimal NetProfitPrevious { get; set; }
		public decimal CashBalance { get; set; }
		public decimal TodayReceipts { get; set; }
		public decimal TodayPayments { get; set; }

		// === Top lists ===
		public List<NamedAmount> TopProducts { get; set; } = new();
		public List<NamedAmount> TopCustomers { get; set; } = new();
		public List<NamedAmount> TopDebtors { get; set; } = new();
		public List<NamedAmount> TopCreditors { get; set; } = new();
		public int UpcomingDueSalesCount { get; set; }

		// === Loss analysis ===
		public int LossInvoiceCount { get; set; }
		public decimal TotalLossAmount { get; set; }
		public Dictionary<string, decimal> LossReasons { get; set; } = new();
		public List<LossInvoiceViewModel> LossInvoices { get; set; } = new();

		// === Recent items ===
		public List<SalesInvoice> RecentSales { get; set; } = new();
		public List<PurchaseInvoice> RecentPurchases { get; set; } = new();
		public int DraftSales { get; set; }
		public int DraftPurchases { get; set; }

		// === Payment mix ===
		public List<NamedAmount> PaymentMix { get; set; } = new();

		// === Cash accounts ===
		public List<NamedAmount> CashAccounts { get; set; } = new();

		// === Low stock ===
public int LowStockProducts { get; set; }
		public DashboardItemViewModel LowStockItem { get; set; }
		public List<DashboardItemViewModel> LowStockList { get; set; } = new();

		// === Pending portal requests ===
		public int PendingPortalRequests { get; set; }

		// === Overdue purchases ===
		public int OverduePurchasesCount { get; set; }
		public decimal OverduePurchasesAmount { get; set; }

		// === Receivables and Payables ===
		public decimal Receivables { get; set; }
		public decimal Payables { get; set; }

		// === Stock ===
		public decimal StockValue { get; set; }
		public int OutOfStockProducts { get; set; }

		// === Trend ===
		public List<TrendPoint> Trend { get; set; } = new();

		// === Period ===
		public DateTime FromDate { get; set; }
		public DateTime ToDate { get; set; }
	}

	public class LossInvoiceViewModel
	{
		public string InvoiceNumber { get; set; } = string.Empty;
		public DateTime InvoiceDate { get; set; }
		public string CustomerName { get; set; } = string.Empty;
		public decimal GrossSale { get; set; }
		public decimal Discount { get; set; }
		public decimal NetSale { get; set; }
		public decimal COGS { get; set; }
		public decimal Profit { get; set; }
		public decimal MarginPercent { get; set; }
		public int ProductCount { get; set; }
	}

	public class NamedAmount
	{
		public int? Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public string? InvoiceNumber { get; set; }
		public string? Customer { get; set; }
		public DateTime? InvoiceDate { get; set; }
		public decimal Amount { get; set; }
		public decimal? TotalAmount { get; set; }
		public decimal? Discount { get; set; }
	}

	public class DashboardItemViewModel
	{
		public int Id { get; set; }
		public string ProductName { get; set; } = string.Empty;
		public decimal Quantity { get; set; }
		public decimal UnitPrice { get; set; }
		public decimal NetSale { get; set; }
		public decimal Cost { get; set; }
		public decimal Profit { get; set; }
		public decimal MarginPercent { get; set; }
		public decimal Remaining { get; set; }
		public string Name { get; set; } = string.Empty;
		public int Min { get; set; }
	}

	public class LossInvoiceDetailsViewModel
	{
		public string InvoiceNumber { get; set; } = string.Empty;
		public DateTime InvoiceDate { get; set; }
		public string CustomerName { get; set; } = string.Empty;
		public decimal SubTotal { get; set; }
		public decimal DiscountAmount { get; set; }
		public decimal NetSales { get; set; }
		public decimal TotalCOGS { get; set; }
		public decimal TotalProfit { get; set; }
		public List<DashboardItemViewModel> Items { get; set; } = new();
	}

	public class TrendPoint
	{
		public string Label { get; set; }
		public decimal Sales { get; set; }
		public decimal Purchases { get; set; }
	}

}