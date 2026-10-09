using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
	// =========================================
	// نوع الحساب في شجرة الحسابات
	// =========================================

	public enum AccountType
	{
		Asset = 1,        // أصول
		Liability = 2,    // خصوم (التزامات)
		Equity = 3,       // حقوق الملكية
		Revenue = 4,      // إيرادات
		Expense = 5,      // مصروفات
		ContraRevenue = 6 // خصومات المبيعات (حساب عكسي للإيرادات)
	}

	// =========================================
	// مصدر القيد المحاسبي
	// =========================================

	public enum JournalSourceType
{
Manual = 0,
SalesInvoice = 1,
SalesCancel = 2,
PurchaseInvoice = 3,
PurchaseCancel = 4,
SalesReturn = 5,
PurchaseReturn = 6,
Treasury = 7,
EmployeeAdvance = 8,
Payroll = 9,
PayrollPayment = 10
}

// =========================================
// نوع القيد داخل نفس المصدر
//
// - Main: القيد الأساسي (إيراد/ذمم/ضريبة/خزنة).
// - Cogs: قيد التكلفة (COGS + خروج المخزون).
//
// بيسمح لكل مصدر (SourceType, SourceId) بوجود
// قيدين مختلفين، وبيستخدم في الـ Unique Index
// لمنع إنشاء قيد مكرر لنفس المصدر.
// =========================================

public enum JournalEntryKind
{
    Main = 0,
    Cogs = 1
}
	// =========================================
	// شجرة الحسابات
	// =========================================

	public class ChartAccount
	{
		public int Id { get; set; }

		[MaxLength(50)]
		public string? Code { get; set; }

		[Required]
		[MaxLength(200)]
		public string Name { get; set; } = string.Empty;

		public AccountType Type { get; set; } = AccountType.Asset;

		public int? ParentId { get; set; }

		public ChartAccount? Parent { get; set; }

		public ICollection<ChartAccount> Children { get; set; }
			= new List<ChartAccount>();

		public bool IsActive { get; set; } = true;

		// ✅ الحسابات النظامية لا يمكن حذفها
		public bool IsSystem { get; set; } = false;

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	}

	// =========================================
	// القيد المحاسبي (Header)
	// =========================================

	public class JournalEntry
	{
		public int Id { get; set; }

		[Required]
		[MaxLength(50)]
		public string EntryNumber { get; set; } = string.Empty;

		public DateTime EntryDate { get; set; }

		[MaxLength(1000)]
		public string? Description { get; set; }

		public JournalSourceType SourceType { get; set; }
			= JournalSourceType.Manual;

		public int? SourceId { get; set; }
		  // ✅ نوع القيد (Main / Cogs) — بيمنع التكرار
    public JournalEntryKind EntryKind { get; set; }
            = JournalEntryKind.Main;

		public bool IsPosted { get; set; }

		public DateTime? PostedAt { get; set; }

		public int? CreatedByUserId { get; set; }

		public User? CreatedByUser { get; set; }

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		public ICollection<JournalEntryLine> Lines { get; set; }
			= new List<JournalEntryLine>();
	}

	// =========================================
	// سطر القيد (Line)
	// =========================================

	public class JournalEntryLine
	{
		public int Id { get; set; }

		public int JournalEntryId { get; set; }

		public JournalEntry? JournalEntry { get; set; }

		public int ChartAccountId { get; set; }

		public ChartAccount? ChartAccount { get; set; }

		public decimal Debit { get; set; }

		public decimal Credit { get; set; }

		[MaxLength(1000)]
		public string? Description { get; set; }

		public int? CustomerId { get; set; }

		public Customer? Customer { get; set; }

		public int? SupplierId { get; set; }

		public Supplier? Supplier { get; set; }

		public int? StoreId { get; set; }

		public Store? Store { get; set; }
	}
}