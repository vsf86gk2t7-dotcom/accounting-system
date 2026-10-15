using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
	// =========================================
	// نوع حركة الخزينة
	// =========================================

	public enum TreasuryTransactionType
	{
		Receive = 1, // إيصال قبض
		Pay = 2, // إذن صرف
		Transfer = 3, // تحويل بين المراكز
		Adjust = 4, // تسوية
		WalletTransfer = 5 // تحويل محفظة إلكترونية
	}

	// =========================================
	// نوع المحفظة الإلكترونية / المركز النقدي
	// يحدد رسوم التحويل المطبقة:
	// - فودافون/اتصالات: 1ج نفس الشبكة / 15ج شبكة أخرى
	// - انستا باي (بنك): 1% على التحويل منها
	// =========================================

	public enum WalletProvider
	{
		General = 0, // نقدي / عام (بدون رسوم محفظة)
		VodafoneCash = 1, // فودافون كاش
		EtisalatCash = 2, // اتصالات كاش
		InstaPay = 3, // انستا باي (بنك) — خصم 1%
		GeneralWallet = 4 // محفظة عامة أخرى (بدون رسوم محفظة)
	}

	// =========================================
	// مركز نقدي (خزنة / بنك)
	// =========================================

	public class CashAccount
	{
		public int Id { get; set; }

		[Required]
		[MaxLength(200)]
		public string Name { get; set; } = string.Empty;

		[MaxLength(100)]
		public string? AccountNumber { get; set; }

		// =========================================
		// الحدود المالية للمركز النقدي:
		// حد يومي وحد شهري (بدون قيمة = بلا حد)
		// =========================================

		public decimal? DailyLimit { get; set; }

		public decimal? MonthlyLimit { get; set; }

		// =========================================
		// نوع المحفظة (فودافون كاش / اتصالات كاش /
		// انستا باي / نقدي عام). يحدد قواعد رسوم
		// التحويل المطبقة على هذا المركز.
		// =========================================

		public WalletProvider Provider { get; set; }
			= WalletProvider.General;

		// =========================================
		// حساب شجرة الحسابات المرتبط بهذا المركز
		// (صندوق → 1101 / بنك → 1102 ...)
		// =========================================

		public int? ChartAccountId { get; set; }

		public ChartAccount? ChartAccount { get; set; }

		public bool IsActive { get; set; } = true;

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		public ICollection<TreasuryTransaction> Transactions { get; set; }
			= new List<TreasuryTransaction>();
	}

	// =========================================
	// حركة خزينة (سند قبض/صرف/تحويل/تسوية)
	// =========================================

	public class TreasuryTransaction
	{
		public int Id { get; set; }

		[Required]
		[MaxLength(50)]
		public string TransactionNumber { get; set; } = string.Empty;

		public TreasuryTransactionType Type { get; set; }

		// =========================================
		// المركز النقدي الأساسي
		// =========================================

		[Required]
		public int CashAccountId { get; set; }

		public CashAccount? CashAccount { get; set; }

		// =========================================
		// مركز التحويل الوجهة (لنوع التحويل)
		// =========================================

		public int? TransferToCashAccountId { get; set; }

		public CashAccount? TransferToCashAccount { get; set; }

		// =========================================
		// المبلغ
		// موجب دائمًا باستثناء تسوية سالبة
		// =========================================

		public decimal Amount { get; set; }

		// =========================================
		// الطرف (اختياري)
		// =========================================

		public int? CustomerId { get; set; }

		public Customer? Customer { get; set; }

		public int? SupplierId { get; set; }

		public Supplier? Supplier { get; set; }

		public int? EmployeeId { get; set; }

		public Employee? Employee { get; set; }

		// =========================================
		// الوصف
		// =========================================

		[MaxLength(1000)]
		public string? Reason { get; set; }

		// =========================================
		// مستند مرجعي (رقم فاتورة إلخ)
		// =========================================

		[MaxLength(100)]
		public string? ReferenceDocument { get; set; }

		// =========================================
		// هل تُرحّل هذه الحركة قيدًا محاسبيًا تلقائيًا؟
		// حركات الفواتير (تحصيل/إلغاء) تُعطَّل لأن
		// قيدها يُنشأ من فاتورة البيع نفسها مباشرة.
		// =========================================

		public bool AutoJournal { get; set; } = true;

		public int? CreatedByUserId { get; set; }

		public User? CreatedByUser { get; set; }

		// =========================================
		// رسوم المحافظ الإلكترونية (كما هو مقرر)
		// - تحويل المحفظة: 1ج نفس الشركة / 15ج شركة أخرى
		//   تُخصم من المرسل ويصل المستقبل صافيًا
		//   والفرق يُقيد داخل حركة الخزينة نفسها.
		// - استلام محفظة مع خصم 1%: المبلغ يُعرض
		//   كاملًا والأصل المحفظة يستقبل (المبلغ - 1%)
		//   والـ 1% يُحول لحساب «إيراد عمولات».
		// =========================================

		// هل الطرف المستقبل لنفس شركة المرسل؟
		public bool SameCompanyTransfer { get; set; }

		// رسوم التحويل المطبقة بالأرقام (1 / 15 أو قيمة 1%)
		public decimal? WalletFee { get; set; }

		// هل التعامل «استلام محفظة» مع خصم عمولة 1%؟
		public bool ApplyWalletIncomeCommission { get; set; }

		public decimal? WalletCommissionAmount { get; set; }

		// حساب «إيراد العمولات» الذي يصل إليه خصم الـ 1%
		// (يُدار كمركز نقدي في الخزينة)
		public int? WalletFeesAccountId { get; set; }

		public CashAccount? WalletFeesAccount { get; set; }

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	}
}
