using System.ComponentModel.DataAnnotations;
using static AccountingSystem.Models.SalesInvoice;

namespace AccountingSystem.Models.ViewModels.Sales
{
	public class SalesInvoiceCreateViewModel
	{
		// =========================================
		// بيانات الفاتورة
		// =========================================

		[Required(ErrorMessage = "رقم الفاتورة مطلوب.")]
		[MaxLength(50)]
		[Display(Name = "رقم الفاتورة")]
		public string InvoiceNumber { get; set; } = string.Empty;

		[Required(ErrorMessage = "يجب اختيار الفرع.")]
		[Display(Name = "الفرع")]
		public int BranchId { get; set; }

		[Required(ErrorMessage = "يجب اختيار المخزن.")]
		[Display(Name = "المخزن")]
		public int StoreId { get; set; }

		[Required(ErrorMessage = "لابد من اختيار اسم العميل.")]
		[Display(Name = "اسم العميل *")]
		public int? CustomerId { get; set; }

		[Display(Name = "تاريخ الفاتورة")]
		public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;

		[MaxLength(1000)]
		[Display(Name = "ملاحظات")]
		public string? Notes { get; set; }
		public decimal SalesTaxRate { get; set; }
		public decimal SalesTaxAmount { get; set; }
		public decimal TaxRate { get; set; }
		// =========================================
		// الإجماليات
		// =========================================

		[Display(Name = "الخصم")]
		public decimal DiscountAmount { get; set; }

		[Display(Name = "الضريبة")]
		public decimal TaxAmount { get; set; }

		// =========================================
		// طريقة الدفع
		// =========================================

		[Display(Name = "طريقة الدفع")]
		public SalesPaymentMethod PaymentMethod { get; set; }
			= SalesPaymentMethod.Cash;

		[MaxLength(500)]
		[Display(Name = "تفاصيل الدفع")]
		public string? PaymentDetails { get; set; }

		// =========================================
		// تاريخ الاستحقاق (للآجل فقط)
		// =========================================

		[Display(Name = "تاريخ الاستحقاق")]
		public DateTime? DueDate { get; set; }

		// =========================================
		// المركز النقدي (للدفع النقدي/التحويل)
		// =========================================

		[Display(Name = "المركز النقدي")]
		public int? CashAccountId { get; set; }

		public List<KeyValuePair<int, string>> AvailableCashAccounts { get; set; }
			= new List<KeyValuePair<int, string>>();

		// =========================================
		// البنود
		// =========================================

		public List<SalesInvoiceItemCreateViewModel> Items { get; set; }
			= new List<SalesInvoiceItemCreateViewModel>();

		// =========================================
		// بيانات للعرض
		// =========================================

		public List<BranchOptionViewModel> AvailableBranches { get; set; }
			= new List<BranchOptionViewModel>();

		public List<StoreOptionViewModel> AvailableStores { get; set; }
			= new List<StoreOptionViewModel>();

		public List<CustomerOptionViewModel> AvailableCustomers { get; set; }
			= new List<CustomerOptionViewModel>();

		public List<SalesProductOptionViewModel> AvailableProducts { get; set; }
			= new List<SalesProductOptionViewModel>();

		public string? CompanyLogoPath { get; set; }
	}

	// =============================================
	// بند فاتورة البيع
	// =============================================

	public class SalesInvoiceItemCreateViewModel
	{
		[Required]
		public int ProductId { get; set; }

		[Required]
		public int UnitId { get; set; }

		[Range(0.000001, double.MaxValue,
			ErrorMessage = "الكمية يجب أن تكون أكبر من صفر.")]
		public decimal Quantity { get; set; }

		[Range(0, double.MaxValue,
			ErrorMessage = "السعر يجب ألا يكون سالبًا.")]
		public decimal UnitPrice { get; set; }

		// =========================================
		// طريقة الصرف
		// =========================================

		[Display(Name = "طريقة الصرف")]
		public StockIssueMethod IssueMethod { get; set; }
			= StockIssueMethod.FIFO;

		// =========================================
		// الدفعة المختارة (لو Manual)
		// =========================================

		[Display(Name = "الدفعة")]
		public int? SelectedStockLotId { get; set; }
	}
	// =============================================
	// قوائم الاختيار
	// =============================================

	public class BranchOptionViewModel
	{
		public int Id { get; set; }
		public string Name { get; set; } = string.Empty;
	}

	public class StoreOptionViewModel
	{
		public int Id { get; set; }
		public int BranchId { get; set; }
		public string Name { get; set; } = string.Empty;
	}

	public class CustomerOptionViewModel
	{
		public int Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public string Phone { get; set; } = string.Empty;
	}

	public class SalesProductOptionViewModel
	{
		public int Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public string? Barcode { get; set; }
		public int BaseUnitId { get; set; }
		public string BaseUnitName { get; set; } = string.Empty;
		public List<SalesProductUnitOptionViewModel> Units { get; set; }
			= new List<SalesProductUnitOptionViewModel>();
	}

	public class SalesProductUnitOptionViewModel
	{
		public int UnitId { get; set; }
		public string UnitName { get; set; } = string.Empty;
		public decimal ConversionFactor { get; set; }
		public decimal SalePrice { get; set; }
	}
}
