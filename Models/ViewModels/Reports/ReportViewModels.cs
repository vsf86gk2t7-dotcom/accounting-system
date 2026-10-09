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
	// تقرير أرصدة العملاء
	// =============================================

	public class CustomerBalancesReportViewModel
		: ReportDateFilterViewModel
	{
		public List<CustomerBalanceRowViewModel> Customers { get; set; }
			= new List<CustomerBalanceRowViewModel>();

		public decimal GrandOpening { get; set; }

		public decimal GrandDebit { get; set; }

		public decimal GrandCredit { get; set; }

		public decimal GrandBalance { get; set; }
	}

	public class CustomerBalanceRowViewModel
	{
		public int CustomerId { get; set; }

		public string CustomerName { get; set; } = string.Empty;

		public decimal OpeningBalance { get; set; }

		public decimal Debit { get; set; }

		public decimal Credit { get; set; }

		public decimal Balance { get; set; }
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
	// تقرير أرصدة الموردين
	// =============================================

	public class SupplierBalancesReportViewModel
		: ReportDateFilterViewModel
	{
		public List<SupplierBalanceRowViewModel> Suppliers { get; set; }
			= new List<SupplierBalanceRowViewModel>();

		public decimal GrandOpening { get; set; }

		public decimal GrandDebit { get; set; }

		public decimal GrandCredit { get; set; }

		public decimal GrandBalance { get; set; }
	}

	public class SupplierBalanceRowViewModel
	{
		public int SupplierId { get; set; }

		public string SupplierName { get; set; } = string.Empty;

		public decimal OpeningBalance { get; set; }

		public decimal Debit { get; set; }

		public decimal Credit { get; set; }

		public decimal Balance { get; set; }
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

		// =========================================
		// ✅ جديد: معلومات الحركة (للدبل كليك)
		// =========================================

		/// <summary>
		/// نوع الحركة (نص للعرض والتمييز):
		/// - "PurchaseInvoice"  → فاتورة شراء
		/// - "PurchaseReturn"   → مرتجع شراء
		/// - "TreasuryPayment"  → سداد / دفعة
		/// - "SalesInvoice"     → فاتورة بيع
		/// - "SalesReturn"      → مرتجع بيع
		/// - "CustomerPayment"  → تحصيل من عميل
		/// - "Manual"           → قيد يدوي
		/// </summary>
		public string? SourceType { get; set; }

		/// <summary>Id الحركة الأصلية (فاتورة / مرتجع / دفعة).</summary>
		public int? SourceId { get; set; }

		/// <summary>رقم المستند المعروض (مثلاً PUR-20261008-0001).</summary>
		public string? ReferenceNumber { get; set; }

		/// <summary>
		/// رابط صفحة التفاصيل — جاهز من السيرفر.
		/// يُفتح في تويب جديد عند الدبل كليك.
		/// </summary>
		public string? DetailsUrl { get; set; }
	}

	// =============================================
	// تقرير الأرباح
	// =============================================

public class ProfitReportViewModel : ReportDateFilterViewModel
{
	// === Requested Order: Profit Report Items ===

	/// <summary>
	/// إجمالي المبيعات قبل الضرائب
	/// </summary>
	public decimal SalesBeforeTax { get; set; }

	/// <summary>
	/// خصومات المبيعات
	/// </summary>
	public decimal Discounts { get; set; }

	/// <summary>
	/// صافي المبيعات بعد الخصومات وقبل الضرائب
	/// </summary>
	public decimal NetSales { get; set; }

	/// <summary>
	/// تكلفة البضاعة المباعة، وفق تكلفة المخزون actual
	/// </summary>
	public decimal CostOfGoodsSold { get; set; }

	/// <summary>
	/// مجمل الربح أو الخسارة
	/// </summary>
	public decimal GrossProfit { get; set; }

	/// <summary>
	/// المصروفات التشغيلية
	/// </summary>
	public decimal Expenses { get; set; }

	/// <summary>
	/// صافي الربح أو الخسارة
	/// </summary>
	public decimal NetProfit { get; set; }

	/// <summary>
	/// هامش الربح الإجمالي
	/// </summary>
	public decimal GrossMarginPercent { get; set; }

	/// <summary>
	/// مبيعات مرتجعة (إذا كانت مطلوبة منفصلة)
	/// </summary>
	public decimal SalesReturns { get; set; }
}

	// =============================================
	// تقرير الربح لكل صنف
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

	// =============================================
	// تقرير ربحية الفاتورة
	// =============================================

	public class InvoiceProfitReportViewModel : ReportDateFilterViewModel
	{
		public List<InvoiceProfitRowViewModel> Rows { get; set; }
			= new List<InvoiceProfitRowViewModel>();

		public decimal TotalGrossSales { get; set; }

		public decimal TotalDiscounts { get; set; }

		public decimal TotalNetSales { get; set; }

		public decimal TotalCOGS { get; set; }

		public decimal TotalProfit { get; set; }

		public decimal OverallMarginPercent { get; set; }

		public int InvoiceCount { get; set; }
	}

	public class InvoiceProfitRowViewModel
	{
		public string InvoiceNumber { get; set; } = string.Empty;

		public DateTime InvoiceDate { get; set; }

		public string CustomerName { get; set; } = string.Empty;

		public decimal GrossSales { get; set; }

		public decimal Discount { get; set; }

		public decimal NetSales { get; set; }

		public decimal COGS { get; set; }

		public decimal Profit { get; set; }

		public decimal MarginPercent { get; set; }

		public List<InvoiceProfitItemRowViewModel> Items { get; set; }
			= new List<InvoiceProfitItemRowViewModel>();
	}

	public class InvoiceProfitItemRowViewModel
	{
		public string ProductName { get; set; } = string.Empty;

		public decimal Quantity { get; set; }

		public decimal NetSale { get; set; }

		public decimal Cost { get; set; }

		public decimal Profit { get; set; }

		public decimal MarginPercent { get; set; }
	}
}