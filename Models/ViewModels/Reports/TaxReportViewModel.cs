using AccountingSystem.Models.ViewModels.Purchases;  // ← مهم: يجيب الـ Store/Supplier options

namespace AccountingSystem.Models.ViewModels.Reports
{
	public class TaxReportViewModel
	{
		// ملخص ض.ق.م
		public decimal VatOpeningBalance { get; set; }
		public decimal VatDebit { get; set; }
		public decimal VatCredit { get; set; }
		public decimal VatClosingBalance { get; set; }

		// ملخص ضريبة المبيعات
		public decimal SalesTaxOpeningBalance { get; set; }
		public decimal SalesTaxDebit { get; set; }
		public decimal SalesTaxCredit { get; set; }
		public decimal SalesTaxClosingBalance { get; set; }

		// الفلتر المستخدم
		public DateTime FromDate { get; set; }
		public DateTime ToDate { get; set; }
		public int? StoreId { get; set; }
		public int? SupplierId { get; set; }

		// التفاصيل
		public List<TaxReportLineViewModel> Lines { get; set; } = new();

		// قوائم الفلترة — من الـ Purchases namespace
		public List<StoreOptionViewModel> AvailableStores { get; set; } = new();
		public List<SupplierOptionViewModel> AvailableSuppliers { get; set; } = new();
	}

	public class TaxReportLineViewModel
	{
		public int JournalEntryId { get; set; }
		public string EntryNumber { get; set; } = string.Empty;
		public DateTime EntryDate { get; set; }
		public string SourceTypeName { get; set; } = string.Empty;

		// ⚠️ خليها nullable عشان تتوافق مع JournalEntry.SourceId
		public int? SourceId { get; set; }

		public string ReferenceNumber { get; set; } = string.Empty;
		public string? Description { get; set; }
		public string? SupplierName { get; set; }
		public string? StoreName { get; set; }

		public decimal VatDebit { get; set; }
		public decimal VatCredit { get; set; }
		public decimal SalesTaxDebit { get; set; }
		public decimal SalesTaxCredit { get; set; }
	}
}