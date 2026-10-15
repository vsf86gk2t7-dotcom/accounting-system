using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models.ViewModels.Reports
{
	// =============================================
	// فلتر تاريخ مشترك
	// =============================================

	public class ReportDateFilterViewModel
	{
		[DataType(DataType.Date)]
		[Display(Name = "من تاريخ")]
		public DateTime FromDate { get; set; }
			= new DateTime(
				DateTime.Today.Year,
				DateTime.Today.Month,
				1);

		[DataType(DataType.Date)]
		[Display(Name = "إلى تاريخ")]
		public DateTime ToDate { get; set; }
			= DateTime.Today;
	}

	// =============================================
	// تقرير المبيعات
	// =============================================

	public class SalesReportViewModel : ReportDateFilterViewModel
	{
		public List<SalesReportRowViewModel> Rows { get; set; }
			= new List<SalesReportRowViewModel>();

		public decimal GrandTotal { get; set; }

		public int InvoiceCount { get; set; }
	}

	public class SalesReportRowViewModel
	{
		public string InvoiceNumber { get; set; } = string.Empty;

		public DateTime InvoiceDate { get; set; }

		public string CustomerName { get; set; } = string.Empty;

		public string StoreName { get; set; } = string.Empty;

		public SalesPaymentMethod PaymentMethod { get; set; }

		public string? PaymentDetails { get; set; }

		public decimal TotalAmount { get; set; }
	}

	// =============================================
	// تقرير المشتريات
	// =============================================

	public class PurchaseReportViewModel : ReportDateFilterViewModel
	{
		public List<PurchaseReportRowViewModel> Rows { get; set; }
			= new List<PurchaseReportRowViewModel>();

		public decimal GrandTotal { get; set; }

		public int InvoiceCount { get; set; }
	}

	public class PurchaseReportRowViewModel
	{
		public string InvoiceNumber { get; set; } = string.Empty;

		public DateTime InvoiceDate { get; set; }

		public string SupplierName { get; set; } = string.Empty;

		public string StoreName { get; set; } = string.Empty;

		public decimal TotalAmount { get; set; }
	}

	// =============================================
	// تقرير المخزون
	// =============================================

	public class InventoryReportViewModel
	{
		public int? StoreId { get; set; }

		public List<KeyValuePair<int, string>> Stores { get; set; }
			= new List<KeyValuePair<int, string>>();

		public List<InventoryReportRowViewModel> Rows { get; set; }
			= new List<InventoryReportRowViewModel>();

		public decimal GrandValue { get; set; }
	}

	public class InventoryReportRowViewModel
	{
		public int ProductId { get; set; }

		public string ProductName { get; set; } = string.Empty;

		public string? ProductCode { get; set; }

		public string StoreName { get; set; } = string.Empty;

		public decimal Quantity { get; set; }

		public decimal AverageCost { get; set; }

		public decimal Value { get; set; }
	}

	// =============================================
	// تقرير الخزينة
	// =============================================

	public class TreasuryReportViewModel : ReportDateFilterViewModel
	{
		public List<TreasuryReportRowViewModel> Rows { get; set; }
			= new List<TreasuryReportRowViewModel>();

		public decimal GrandIn { get; set; }

		public decimal GrandOut { get; set; }
	}

	public class TreasuryReportRowViewModel
	{
		public string TransactionNumber { get; set; } = string.Empty;

		public DateTime CreatedAt { get; set; }

		public string AccountName { get; set; } = string.Empty;

		public TreasuryTransactionType Type { get; set; }

		public string Side { get; set; } = string.Empty;

		public decimal Amount { get; set; }

		public string? PartyName { get; set; }

		public string? Reason { get; set; }
	}

	// =============================================
	// تقرير القيود المحاسبية
	// =============================================

	public class AccountingReportViewModel : ReportDateFilterViewModel
	{
		public List<AccountingReportRowViewModel> Rows { get; set; }
			= new List<AccountingReportRowViewModel>();

		public decimal GrandDebit { get; set; }

		public decimal GrandCredit { get; set; }
	}

	public class AccountingReportRowViewModel
	{
		public string EntryNumber { get; set; } = string.Empty;

		public DateTime EntryDate { get; set; }

		public string? Description { get; set; }

		public decimal Debit { get; set; }

		public decimal Credit { get; set; }

		public bool IsPosted { get; set; }
	}

	// =============================================
	// كشف حساب عميل
	// =============================================

	public class CustomerStatementViewModel : ReportDateFilterViewModel
	{
		[Range(1, int.MaxValue,
			ErrorMessage = "يجب اختيار العميل.")]
		public int CustomerId { get; set; }

		public List<KeyValuePair<int, string>> Customers { get; set; }
			= new List<KeyValuePair<int, string>>();

		public string CustomerName { get; set; } = string.Empty;

		public decimal OpeningBalance { get; set; }

		public decimal ClosingBalance { get; set; }

		public List<StatementRowViewModel> Rows { get; set; }
			= new List<StatementRowViewModel>();
	}

	// =============================================
	// كشف حساب مورد
	// =============================================

	public class SupplierStatementViewModel : ReportDateFilterViewModel
	{
		[Range(1, int.MaxValue,
			ErrorMessage = "يجب اختيار المورد.")]
		public int SupplierId { get; set; }

		public List<KeyValuePair<int, string>> Suppliers { get; set; }
			= new List<KeyValuePair<int, string>>();

		public string SupplierName { get; set; } = string.Empty;

		public decimal OpeningBalance { get; set; }

		public decimal ClosingBalance { get; set; }

		public List<StatementRowViewModel> Rows { get; set; }
			= new List<StatementRowViewModel>();
	}

	// =============================================
	// سطر كشف حساب
	// =============================================

	public class StatementRowViewModel
	{
		public DateTime Date { get; set; }

		public string Description { get; set; } = string.Empty;

		public decimal Debit { get; set; }

		public decimal Credit { get; set; }

		public decimal Balance { get; set; }
	}

	// =============================================
	// تقرير الأرباح
	// =============================================

	public class ProfitReportViewModel : ReportDateFilterViewModel
	{
		public decimal GrossSales { get; set; }

		public decimal SalesReturns { get; set; }

		public decimal NetSales { get; set; }

		public decimal CostOfGoodsSold { get; set; }

		public decimal GrossProfit { get; set; }

		public decimal Expenses { get; set; }

		public decimal NetProfit { get; set; }

		public decimal GrossMarginPercent { get; set; }
	}

	// =============================================
	// تقرير الربح لكل صنف
	// (التكلفة الفعلية للدفعات وليست المتوسط)
	// =============================================

	public class ProductProfitReportViewModel : ReportDateFilterViewModel
	{
		public List<ProductProfitRowViewModel> Rows { get; set; }
			= new List<ProductProfitRowViewModel>();

		public decimal GrandQuantity { get; set; }

		public decimal GrandRevenue { get; set; }

		public decimal GrandCost { get; set; }

		public decimal GrandProfit { get; set; }
	}

	public class ProductProfitRowViewModel
	{
		public int ProductId { get; set; }

		public string ProductName { get; set; } = string.Empty;

		public string ProductCode { get; set; } = string.Empty;

		public decimal QuantityInBaseUnit { get; set; }

		public decimal Revenue { get; set; }

		public decimal Cost { get; set; }

		public decimal Profit { get; set; }

		public decimal MarginPercent { get; set; }
	}

	// =============================================
	// تقرير المبيعات اليومي
	// =============================================

	public class DailySalesReportViewModel : ReportDateFilterViewModel
	{
		public List<DailySalesRowViewModel> Rows { get; set; }
			= new List<DailySalesRowViewModel>();

		public decimal GrandSales { get; set; }

		public decimal GrandReturns { get; set; }

		public decimal GrandNet { get; set; }

		public int InvoiceCount { get; set; }
	}

	public class DailySalesRowViewModel
	{
		public DateTime Date { get; set; }

		public int InvoiceCount { get; set; }

		public decimal Sales { get; set; }

		public decimal Returns { get; set; }

		public decimal Net { get; set; }
	}
}
