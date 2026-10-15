using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models.ViewModels.Treasury
{
	// =============================================
	// ���� ���� ����
	// =============================================

	public class CashAccountBalanceViewModel
	{
		public int Id { get; set; }

		public string Name { get; set; } = string.Empty;

		public string? AccountNumber { get; set; }

		public decimal Balance { get; set; }

		public int TransactionCount { get; set; }

		public bool IsActive { get; set; } = true;

		public decimal? DailyLimit { get; set; }

		public decimal? MonthlyLimit { get; set; }

		// =============================================
		// ������� ������ (������� ������� �� ������)
		// ���� ����� ������ / ����� ������
		// =============================================

		public decimal DailyConsumed { get; set; }

		public decimal MonthlyConsumed { get; set; }
	}

	// =============================================
	// ������ �������� �������
	// =============================================

	public class TreasuryIndexViewModel
	{
		public List<CashAccountBalanceViewModel> Accounts { get; set; }
			= new List<CashAccountBalanceViewModel>();

		public List<TreasuryTransactionRowViewModel> RecentTransactions { get; set; }
			= new List<TreasuryTransactionRowViewModel>();

		public decimal TotalBalance { get; set; }

		public int PageNumber { get; set; } = 1;
		public int PageSize { get; set; } = 20;
		public int TotalCount { get; set; }
		public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
		public bool HasPreviousPage => PageNumber > 1;
		public bool HasNextPage => PageNumber < TotalPages;
	}

	// =============================================
	// ���� ����� ���� ����
	// =============================================

	public class CashAccountCreateViewModel
	{
		[Required(ErrorMessage = "��� ������ �����.")]
		[MaxLength(
			200,
			ErrorMessage = "����� �� ���� �� ������ 200 ���.")]
		[Display(Name = "��� ������")]
		public string Name { get; set; } = string.Empty;

		[MaxLength(
			100,
			ErrorMessage = "��� ������ �� ���� �� ������ 100 ���.")]
		[Display(Name = "��� ������")]
		public string? AccountNumber { get; set; }

		[Display(Name = "��� �������")]
		[Required(
			ErrorMessage = "��� ������ ��� �������.")]
		public WalletProvider? Provider { get; set; }

		[Display(Name = "���� ������")]
		[Range(
			0,
			double.MaxValue,
			ErrorMessage = "���� ������ ��� �� ���� ���� �����.")]
		public decimal? DailyLimit { get; set; }

		[Display(Name = "���� ������")]
		[Range(
			0,
			double.MaxValue,
			ErrorMessage = "���� ������ ��� �� ���� ���� �����.")]
		public decimal? MonthlyLimit { get; set; }
	}

	// =============================================
	// ����� �������
	// =============================================

	public class TreasuryTransactionsViewModel
	{
		public List<TreasuryTransactionRowViewModel> Rows { get; set; }
			= new List<TreasuryTransactionRowViewModel>();

		public int? AccountId { get; set; }

		public TreasuryTransactionType? Type { get; set; }

		public List<KeyValuePair<int, string>> Accounts { get; set; }
			= new List<KeyValuePair<int, string>>();
	}

	public class TreasuryTransactionRowViewModel
	{
		public int Id { get; set; }

		public string TransactionNumber { get; set; } = string.Empty;

		public TreasuryTransactionType Type { get; set; }

		public string AccountName { get; set; } = string.Empty;

		public string? TransferToAccountName { get; set; }

		public decimal Amount { get; set; }

		public string? PartyName { get; set; }

		public string? Reason { get; set; }

		public string? ReferenceDocument { get; set; }

		public string? CreatedByUserName { get; set; }

		public DateTime CreatedAt { get; set; }
	}

	// =============================================
	// ����� ���
	// =============================================

	public class TreasuryReceiveViewModel
	{
		[Range(1, int.MaxValue,
			ErrorMessage = "��� ������ ������ ������.")]
		public int CashAccountId { get; set; }

		[Range(0.01, double.MaxValue,
			ErrorMessage = "������ ��� �� ���� ���� �� ���.")]
		public decimal Amount { get; set; }

		public int? CustomerId { get; set; }

		public int? SupplierId { get; set; }

		public int? EmployeeId { get; set; }

		[MaxLength(
			1000,
			ErrorMessage = "������ �� ���� �� ������ 1000 ���.")]
		public string? Reason { get; set; }

		[MaxLength(
			100,
			ErrorMessage = "������� ������� �� ���� �� ������ 100 ���.")]
		public string? ReferenceDocument { get; set; }

		public List<KeyValuePair<int, string>> Accounts { get; set; }
			= new List<KeyValuePair<int, string>>();

		public List<KeyValuePair<int, string>> Customers { get; set; }
			= new List<KeyValuePair<int, string>>();

		public List<KeyValuePair<int, string>> Suppliers { get; set; }
			= new List<KeyValuePair<int, string>>();

		public List<KeyValuePair<int, string>> Employees { get; set; }
			= new List<KeyValuePair<int, string>>();

		public List<KeyValuePair<int, string>> ExpenseAccounts { get; set; }
			= new List<KeyValuePair<int, string>>();

		// ����� ����� �� ����� �������� (�������/�������):
		// ������ ���� ������ �����ǡ ���� ��� ������
		// ���� ����� 1% ������� ����� ����� ��������.
		[Display(Name = "��� 1% �� ����� ��������")]
		public bool ApplyCommission { get; set; }

		// ����� ������ ������� ��� ������� (��� GET ���) �
		// ������� �� ����� ���� ����� ������ ����� ��������.
		public Dictionary<int, int> AccountsProviders { get; set; }
			= new Dictionary<int, int>();

		// ���� ������ ������� (��� GET ���) � ����� ���� ������
		// ������� �������/������� ��� ������ ������� �� �������.
		public Dictionary<int, CashAccountBalanceViewModel> AccountCards { get; set; }
			= new Dictionary<int, CashAccountBalanceViewModel>();

		// =========================================
		// ���� ������� ���� ����� ��� ���
		// (��� ���� �� � �� ����/����/����)
		// =========================================

		[Display(Name = "���� ������� ����")]
		public int? OtherIncomeAccountId { get; set; }

		public List<KeyValuePair<int, string>> AvailableIncomeAccounts { get; set; }
			= new List<KeyValuePair<int, string>>();
	}

	// =============================================
	// ��� ���
	// =============================================

	public class TreasuryPayViewModel
	{
		// ============================================================
		// ���� ������� ������� (���� B5):
		// �������� ��� ����� ���� (�� ���� ��� ���� ��� ����) �
		// ������ ����� ������ ��� ����� ������ ��� �����:
		//   ����  [���� ������� �������]  (Expense)
		//   ����  [������ ������ / �������]
		// �������: ��� �� ����� ���� ����� ����ǡ ����� �����
		// ������ ����� ��� �����. ��� ���� "������ �������"
		// ���� ���� ����� ����� ����� �� ����� ��������.
		// ============================================================

		public int? ExpenseAccountId { get; set; }


		[Range(1, int.MaxValue,
			ErrorMessage = "��� ������ ������ ������.")]
		public int CashAccountId { get; set; }

		[Range(0.01, double.MaxValue,
			ErrorMessage = "������ ��� �� ���� ���� �� ���.")]
		public decimal Amount { get; set; }

		public int? CustomerId { get; set; }

		public int? SupplierId { get; set; }

		public int? EmployeeId { get; set; }

		[MaxLength(
			1000,
			ErrorMessage = "������ �� ���� �� ������ 1000 ���.")]
		public string? Reason { get; set; }

		[MaxLength(
			100,
			ErrorMessage = "������� ������� �� ���� �� ������ 100 ���.")]
		public string? ReferenceDocument { get; set; }

		public List<KeyValuePair<int, string>> Accounts { get; set; }
			= new List<KeyValuePair<int, string>>();

		public List<KeyValuePair<int, string>> Customers { get; set; }
			= new List<KeyValuePair<int, string>>();

		public List<KeyValuePair<int, string>> Suppliers { get; set; }
			= new List<KeyValuePair<int, string>>();

		public List<KeyValuePair<int, string>> Employees { get; set; }
			= new List<KeyValuePair<int, string>>();

		public List<KeyValuePair<int, string>> ExpenseAccounts { get; set; }
			= new List<KeyValuePair<int, string>>();

		// ����� ������ ������� ��� ������� (��� GET ���) �
		// ������� �� ������ ������ ��� �������.
		public Dictionary<int, int> AccountsProviders { get; set; }
			= new Dictionary<int, int>();

		// ���� ������ ������� (��� GET ���) � ����� ���� ������
		// ������� �������/������� ��� ������ ������� �� �������.
		public Dictionary<int, CashAccountBalanceViewModel> AccountCards { get; set; }
			= new Dictionary<int, CashAccountBalanceViewModel>();

		// ============================================================
		// ���� ����� ��� ��� ������ ������:
		// - ���� / ����� ����: ���� ����.
		// - ����� ���: ���� ������ �������� (�� ���� ���� �������)
		//   ���� ���� 1% �� ������.
		// - ����� ������ (�������/�������): ���� 1� ��� ���� �����
		//   ������� �� ��� �����ɡ �15� ��� ���� ���� ����.
		// ============================================================

		[Display(Name = "���� ������� �� ����� ���")]
		public decimal? WalletFee { get; set; }

		[Display(Name = "��� ����� �������")]
		public WalletProvider? RecipientWalletProvider { get; set; }
	}

	// =============================================
	// ����� �����
	// =============================================

	public class TreasuryAdjustViewModel
	{
		[Range(1, int.MaxValue,
			ErrorMessage = "��� ������ ������ ������.")]
		public int CashAccountId { get; set; }

		[Display(Name = "����� ����� (�����)")]
		public bool IsIncrease { get; set; } = true;

		[Range(0.01, double.MaxValue,
			ErrorMessage = "���� ������� ��� �� ���� ���� �� ���.")]
		public decimal Amount { get; set; }

		[Required(ErrorMessage = "��� ������� �����.")]
		[MaxLength(
			1000,
			ErrorMessage = "����� �� ���� �� ������ 1000 ���.")]
		public string Reason { get; set; } = string.Empty;

		public List<KeyValuePair<int, string>> Accounts { get; set; }
			= new List<KeyValuePair<int, string>>();
	}

	// =============================================
	// ������� ������ �������
	// =============================================

	public class TreasuryDailyCloseViewModel
	{
		[DataType(DataType.Date)]
		[Display(Name = "�����")]
		public DateTime Date { get; set; } = DateTime.Today;

		public List<TreasuryDailyCloseRowViewModel> Rows { get; set; }
			= new List<TreasuryDailyCloseRowViewModel>();

		public List<TreasuryTransactionRowViewModel> Transactions { get; set; }
			= new List<TreasuryTransactionRowViewModel>();

		public decimal TotalOpening { get; set; }

		public decimal TotalIn { get; set; }

		public decimal TotalOut { get; set; }

		public decimal TotalClosing { get; set; }
	}

	public class TreasuryDailyCloseRowViewModel
	{
		public int CashAccountId { get; set; }

		public string AccountName { get; set; } = string.Empty;

		public decimal Opening { get; set; }

		public decimal TotalIn { get; set; }

		public decimal TotalOut { get; set; }

		public decimal Closing { get; set; }

		public int Count { get; set; }
	}

	// =============================================
	// ����� ��� ������� (��� + �����)
	// =============================================

	public class TreasuryReceiptViewModel
	{
		public int Id { get; set; }

		public string TransactionNumber { get; set; } = string.Empty;

		public TreasuryTransactionType Type { get; set; }

		public string CashAccountName { get; set; } = string.Empty;

		public string? TransferToCashAccountName { get; set; }

		public string? PartyName { get; set; }

		public string? Reason { get; set; }

		public string? ReferenceDocument { get; set; }

		public decimal Amount { get; set; }

		public decimal AmountInWords { get; set; }

		public string? CreatedByUserName { get; set; }

		public DateTime CreatedAt { get; set; }

		public TreasuryFinanceEntityViewModel Company { get; set; }
			= new TreasuryFinanceEntityViewModel();
	}

	public class TreasuryFinanceEntityViewModel
	{
		public string Name { get; set; } = string.Empty;

		public string? Phone { get; set; }

		public string? Address { get; set; }

		public string? LogoPath { get; set; }
	}
}
