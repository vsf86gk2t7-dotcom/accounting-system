using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models.ViewModels.Returns
{
	// =========================================
	// ViewModel إنشاء مرتجع بيع
	// =========================================

	public class SalesReturnViewModel
	{
		// =========================================
		// الفاتورة الأصلية
		// =========================================

		public int? OriginalSalesInvoiceId { get; set; }

		public string? OriginalInvoiceNumber { get; set; }

		public DateTime? OriginalInvoiceDate { get; set; }

		public decimal? OriginalInvoiceTotal { get; set; }

		// =========================================
		// العميل والمخزن
		// =========================================

		public int CustomerId { get; set; }

		public int StoreId { get; set; }

		// =========================================
		// البيانات
		// =========================================

		public DateTime ReturnDate { get; set; } = DateTime.UtcNow;

		[MaxLength(500)]
		public string? Reason { get; set; }

		// =========================================
		// رد نقدي
		// =========================================

		public bool RefundToCash { get; set; }

		public int? RefundCashAccountId { get; set; }

		public decimal RefundAmount { get; set; }

		// =========================================
		// البنود
		// =========================================

		public List<SalesReturnItemViewModel> Items { get; set; }
			= new List<SalesReturnItemViewModel>();

		// =========================================
		// القوائم
		// =========================================

		public List<KeyValuePair<int, string>> Customers { get; set; }
			= new List<KeyValuePair<int, string>>();

		public List<KeyValuePair<int, string>> Stores { get; set; }
			= new List<KeyValuePair<int, string>>();

		public List<KeyValuePair<int, string>> CashAccounts { get; set; }
			= new List<KeyValuePair<int, string>>();

		// =========================================
		// فواتير العميل المتاحة للإرجاع
		// =========================================

		public List<CustomerInvoiceOptionViewModel> AvailableInvoices
		{
			get;
			set;
		} = new List<CustomerInvoiceOptionViewModel>();
	}

	// =========================================
	// خيار فاتورة العميل
	// =========================================

	public class CustomerInvoiceOptionViewModel
	{
		public int InvoiceId { get; set; }

		public string InvoiceNumber { get; set; } = string.Empty;

		public DateTime InvoiceDate { get; set; }

		public decimal TotalAmount { get; set; }

		public int ItemCount { get; set; }
	}

	// =========================================
	// بند المرتجع
	// =========================================

	public class SalesReturnItemViewModel
	{
		// =========================================
		// البند الأصلي
		// =========================================

		public int SalesInvoiceItemId { get; set; }

		public int ProductId { get; set; }

		public string? ProductName { get; set; }

		public int UnitId { get; set; }

		public string? UnitName { get; set; }

		public decimal ConversionFactor { get; set; }

		// =========================================
		// الكميات
		// =========================================

		public decimal SoldQuantity { get; set; }

		public decimal AlreadyReturnedQuantity { get; set; }

		public decimal ReturnableQuantity { get; set; }

		public decimal Quantity { get; set; }

		// =========================================
		// السعر والتكلفة
		// =========================================

		public decimal UnitPrice { get; set; }

		public decimal UnitCost { get; set; }

		public decimal Total { get; set; }
	}

	// =========================================
	// قوائم عرض المرتجعات (Index)
	// =========================================

	public class ReturnListViewModel
	{
		public string Type { get; set; } = string.Empty;

		public List<ReturnRowViewModel> Rows { get; set; }
			= new List<ReturnRowViewModel>();
	}

	public class ReturnRowViewModel
	{
		public int Id { get; set; }

		public string InvoiceNumber { get; set; } = string.Empty;

		public string PartyName { get; set; } = string.Empty;

		public string StoreName { get; set; } = string.Empty;

		public DateTime ReturnDate { get; set; }

		public decimal TotalAmount { get; set; }

		public int ItemCount { get; set; }

		public string? Reason { get; set; }
	}
}
