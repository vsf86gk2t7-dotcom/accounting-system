using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models.ViewModels.Purchases
{
	public class PurchaseInvoiceCreateViewModel
	{
		// =========================================
		// بيانات الفاتورة
		// =========================================

		[Required(ErrorMessage = "رقم الفاتورة مطلوب.")]
		[MaxLength(50)]
		[Display(Name = "رقم الفاتورة")]
		public string InvoiceNumber { get; set; } = string.Empty;

		[Required(ErrorMessage = "يجب اختيار المورد.")]
		[Display(Name = "المورد")]
		public int SupplierId { get; set; }

		public decimal SubTotal { get; set; }

		public decimal TaxRate { get; set; }
		public decimal TotalAmount { get; set; }
		public decimal SalesTaxRate { get; set; } = 0;
		public decimal SalesTaxAmount { get; set; } = 0;

		[Display(Name = "الفرع")]
		public int? BranchId { get; set; }

		[Required(ErrorMessage = "يجب اختيار المخزن.")]
		[Display(Name = "المخزن")]
		public int StoreId { get; set; }

		[Display(Name = "تاريخ الفاتورة")]
		public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;

		[Display(Name = "تاريخ الاستحقاق")]
		public DateTime? DueDate { get; set; }

		[MaxLength(1000)]
		[Display(Name = "ملاحظات")]
		public string? Notes { get; set; }

		// =========================================
		// الإجماليات
		// =========================================

		[Display(Name = "الخصم")]
		public decimal DiscountAmount { get; set; }

		[Display(Name = "الضريبة")]
		public decimal TaxAmount { get; set; }

		// =========================================
		// البنود
		// =========================================

		public List<PurchaseInvoiceItemCreateViewModel> Items { get; set; }
			= new List<PurchaseInvoiceItemCreateViewModel>();

		// =========================================
		// بيانات للعرض
		// =========================================

		public List<SupplierOptionViewModel> AvailableSuppliers { get; set; }
			= new List<SupplierOptionViewModel>();

		public List<BranchOptionViewModel> AvailableBranches { get; set; }
			= new List<BranchOptionViewModel>();

		public List<StoreOptionViewModel> AvailableStores { get; set; }
			= new List<StoreOptionViewModel>();

		public List<ProductOptionViewModel> AvailableProducts { get; set; }
			= new List<ProductOptionViewModel>();

		public string? CompanyLogoPath { get; set; }
	}

	// =============================================
	// بند فاتورة الشراء
	// =============================================

	public class PurchaseInvoiceItemCreateViewModel
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
		// ✅ جديد: معامل التحويل الفعلي للبند
		//     - للوحدات الثابتة (Standard) يُتجاهَل
		//       ويُستبدل بـ Unit.PiecesPerUnit.
		//     - للوحدات المتغيرة (Variable) يُستخدم
		//       كما هو، وإذا كان صفراً يجرّب:
		//         SupplierProductPacking → ProductUnit
		// =========================================

		[Range(0, 1000000,
			ErrorMessage = "عدد القطع في الوحدة يجب ألا يكون سالبًا.")]
		[Display(Name = "عدد القطع في الوحدة")]
		public decimal? ConversionFactor { get; set; }

		// =========================================
		// احتياطي (للتوافق مع الشاشة القديمة)
		// يُستخدم فقط إذا لم يوجد ConversionFactor
		// =========================================

		[Range(0, 1000000,
			ErrorMessage = "عدد القطع في الطقم يجب ألا يكون سالبًا.")]
		[Display(Name = "عدد القطع في الطقم")]
		public decimal? ItemSetSize { get; set; }
	}

	// =============================================
	// قوائم الاختيار
	// =============================================

	public class SupplierOptionViewModel
	{
		public int Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public string? Phone { get; set; }
	}

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
		public string? BranchName { get; set; }
	}

	public class ProductOptionViewModel
	{
		public int Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public string? Barcode { get; set; }
		public int BaseUnitId { get; set; }
		public string BaseUnitName { get; set; } = string.Empty;
		public List<ProductUnitOptionViewModel> Units { get; set; }
			= new List<ProductUnitOptionViewModel>();
	}

	public class ProductUnitOptionViewModel
	{
		public int UnitId { get; set; }
		public string UnitName { get; set; } = string.Empty;
		public decimal ConversionFactor { get; set; }

		// ✅ جديد: نوع الوحدة (0 = Standard, 1 = Variable)
		public int Kind { get; set; } = 1;

		// ✅ جديد: عدد القطع للوحدات الثابتة فقط
		public decimal? PiecesPerUnit { get; set; }
	}
}