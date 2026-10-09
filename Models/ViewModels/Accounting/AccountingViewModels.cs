using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models.ViewModels.Accounting
{
	// =============================================
	// صفحة شجرة الحسابات
	// =============================================

	public class ChartAccountsViewModel
	{
		public List<ChartAccountRowViewModel> Rows { get; set; }
			= new List<ChartAccountRowViewModel>();

		public List<ChartAccountParentOptionViewModel> ParentOptions { get; set; }
			= new List<ChartAccountParentOptionViewModel>();
	}

	public class ChartAccountParentOptionViewModel
	{
		public int Id { get; set; }

		public string Name { get; set; } = string.Empty;

		public AccountType Type { get; set; }

		public int Depth { get; set; } = 0;
	}

	public class ChartAccountRowViewModel
	{
		public int Id { get; set; }

		public string? Code { get; set; }

		public string Name { get; set; } = string.Empty;

		public AccountType Type { get; set; }

		public bool IsActive { get; set; } = true;

		public bool IsSystem { get; set; } = false;

		public int Depth { get; set; } = 0;
	}

	public class ChartAccountCreateViewModel
	{
		[MaxLength(
			50,
			ErrorMessage = "الكود لا يمكن أن يتجاوز 50 حرف.")]
		[Display(Name = "كود الحساب")]
		public string? Code { get; set; }

		[Required(ErrorMessage = "اسم الحساب مطلوب.")]
		[MaxLength(
			200,
			ErrorMessage = "الاسم لا يمكن أن يتجاوز 200 حرف.")]
		[Display(Name = "اسم الحساب")]
		public string Name { get; set; } = string.Empty;

		[Display(Name = "نوع الحساب")]
		public AccountType Type { get; set; } = AccountType.Asset;

		[Display(Name = "الحساب الأب")]
		public int? ParentId { get; set; }

		public List<KeyValuePair<int, string>> Parents { get; set; }
			= new List<KeyValuePair<int, string>>();
	}

	// =============================================
	// قائمة القيود
	// =============================================

	public class JournalIndexViewModel
	{
		public List<JournalRowViewModel> Rows { get; set; }
			= new List<JournalRowViewModel>();

		public bool? PostedOnly { get; set; }
	}

	public class JournalRowViewModel
	{
		public int Id { get; set; }

		public string EntryNumber { get; set; } = string.Empty;

		public DateTime EntryDate { get; set; }

		public string? Description { get; set; }

		public bool IsPosted { get; set; }

		public DateTime? PostedAt { get; set; }

		public int LinesCount { get; set; }

		public decimal DebitTotal { get; set; }

		public decimal CreditTotal { get; set; }

		public string? CreatedByUserName { get; set; }
	}

	// =============================================
	// إنشاء / تعديل قيد
	// =============================================

	public class JournalEntryViewModel
	{
		public int Id { get; set; }

		[DataType(DataType.Date)]
		[Display(Name = "تاريخ القيد")]
		public DateTime EntryDate { get; set; } = DateTime.Today;

		[MaxLength(
			1000,
			ErrorMessage = "البيان لا يمكن أن يتجاوز 1000 حرف.")]
		[Display(Name = "بيان القيد")]
		public string? Description { get; set; }

		public List<JournalLineViewModel> Lines { get; set; }
			= new List<JournalLineViewModel>();

		public List<KeyValuePair<int, string>> Accounts { get; set; }
			= new List<KeyValuePair<int, string>>();
	}

	public class JournalLineViewModel
	{
		[Range(1, int.MaxValue,
			ErrorMessage = "يجب اختيار الحساب.")]
		public int ChartAccountId { get; set; }

		[Range(0, double.MaxValue,
			ErrorMessage = "المدين يجب ألا يكون سالبًا.")]
		public decimal Debit { get; set; }

		[Range(0, double.MaxValue,
			ErrorMessage = "الدائن يجب ألا يكون سالبًا.")]
		public decimal Credit { get; set; }
	}

	// =============================================
	// ميزان المراجعة
	// =============================================

	public class TrialBalanceViewModel
	{
		public List<TrialBalanceRowViewModel> Rows { get; set; }
			= new List<TrialBalanceRowViewModel>();

		public decimal TotalDebit { get; set; }

		public decimal TotalCredit { get; set; }
	}

	public class TrialBalanceRowViewModel
	{
		public string? Code { get; set; }

		public string Name { get; set; } = string.Empty;

		public AccountType Type { get; set; }

		public decimal Debit { get; set; }

		public decimal Credit { get; set; }

		public decimal Balance { get; set; }
	}

	// =============================================
	// دفتر الأستاذ
	// =============================================

	public class LedgerViewModel
	{
		public int AccountId { get; set; }

		public List<KeyValuePair<int, string>> Accounts { get; set; }
			= new List<KeyValuePair<int, string>>();

		public string AccountName { get; set; } = string.Empty;

		public AccountType AccountType { get; set; }

		[DataType(DataType.Date)]
		[Display(Name = "من تاريخ")]
		public DateTime FromDate { get; set; } = new DateTime(
			DateTime.Today.Year,
			DateTime.Today.Month,
			1);

		[DataType(DataType.Date)]
		[Display(Name = "إلى تاريخ")]
		public DateTime ToDate { get; set; } = DateTime.Today;

		public decimal OpeningBalance { get; set; }

		public decimal TotalDebit { get; set; }

		public decimal TotalCredit { get; set; }

		public decimal ClosingBalance { get; set; }

		public List<LedgerRowViewModel> Rows { get; set; }
			= new List<LedgerRowViewModel>();
	}

	public class LedgerRowViewModel
	{
		public DateTime Date { get; set; }

		public string EntryNumber { get; set; } = string.Empty;

		public string? Description { get; set; }

		public decimal Debit { get; set; }

		public decimal Credit { get; set; }

		public decimal Balance { get; set; }
	}

	// =============================================
	// قائمة الدخل
	// =============================================

	public class IncomeStatementViewModel
	{
		[DataType(DataType.Date)]
		[Display(Name = "من تاريخ")]
		public DateTime FromDate { get; set; } = new DateTime(
			DateTime.Today.Year, 1, 1);

		[DataType(DataType.Date)]
		[Display(Name = "إلى تاريخ")]
		public DateTime ToDate { get; set; } = DateTime.Today;

		public List<IncomeStatementLineViewModel> RevenueLines { get; set; }
			= new List<IncomeStatementLineViewModel>();

		public List<IncomeStatementLineViewModel> ExpenseLines { get; set; }
			= new List<IncomeStatementLineViewModel>();

		public List<IncomeStatementCategoryViewModel> ExpenseCategories { get; set; }
			= new List<IncomeStatementCategoryViewModel>();

		public decimal GrossSales { get; set; }

		public decimal TotalDiscounts { get; set; }

		public decimal TotalRevenue { get; set; }

		public decimal TotalExpenses { get; set; }

		public decimal NetIncome { get; set; }
	}

	public class IncomeStatementCategoryViewModel
	{
		public string Name { get; set; } = string.Empty;

		public List<IncomeStatementLineViewModel> Lines { get; set; }
			= new List<IncomeStatementLineViewModel>();

		public decimal Amount { get; set; }
	}

	public class IncomeStatementLineViewModel
	{
		public string? Code { get; set; }

		public string Name { get; set; } = string.Empty;

		public decimal Amount { get; set; }
	}

	// =============================================
	// الميزانية العمومية
	// =============================================

	public class BalanceSheetViewModel
	{
		[DataType(DataType.Date)]
		[Display(Name = "كما في تاريخ")]
		public DateTime AsOfDate { get; set; } = DateTime.Today;

		public List<BalanceSheetLineViewModel> Assets { get; set; }
			= new List<BalanceSheetLineViewModel>();

		public List<BalanceSheetLineViewModel> Liabilities { get; set; }
			= new List<BalanceSheetLineViewModel>();

		public List<BalanceSheetLineViewModel> Equity { get; set; }
			= new List<BalanceSheetLineViewModel>();

		public decimal TotalAssets { get; set; }

		public decimal TotalLiabilities { get; set; }

		public decimal TotalEquity { get; set; }

		public decimal NetIncome { get; set; }

		public decimal TotalLiabilitiesAndEquity { get; set; }

		public bool IsBalanced { get; set; }
	}

	public class BalanceSheetLineViewModel
	{
		public string? Code { get; set; }

		public string Name { get; set; } = string.Empty;

		public decimal Amount { get; set; }
	}
}
