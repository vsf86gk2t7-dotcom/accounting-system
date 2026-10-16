using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
	public enum PurchaseInvoiceStatus
	{
		Draft = 0,
		Posted = 1,
		Cancelled = 2
	}

	public enum PurchasePaymentMethod
	{
		Cash = 1,
		BankTransfer = 2,
		Cheque = 3,
		Credit = 4,
		Wallet = 5
	}

	public class PurchaseInvoice
	{
		public int Id { get; set; }

		// =========================================
		// رقم الفاتورة
		// =========================================

		[Required]
		[MaxLength(50)]
		public string InvoiceNumber { get; set; } = string.Empty;

		// =========================================
		// المورد
		// =========================================

		public decimal SalesTaxRate { get; set; }
		public decimal SalesTaxAmount { get; set; }

		[Required]
		public int SupplierId { get; set; }

		public Supplier? Supplier { get; set; }

		// =========================================
		// المخزن المستلم
		// =========================================

		[Required]
		public int StoreId { get; set; }

		public Store? Store { get; set; }

		// =========================================
		// التواريخ
		// =========================================

		public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		public DateTime? PostedAt { get; set; }

		// =========================================
		// ملاحظات
		// =========================================

		[MaxLength(1000)]
		public string? Notes { get; set; }

		// =========================================
		// الإجماليات
		// =========================================

		public decimal SubTotal { get; set; }

		public decimal DiscountAmount { get; set; }

		// نسبة ضريبة القيمة المضافة (مثال: 14)
		public decimal TaxRate { get; set; }

		// قيمة ضريبة القيمة المضافة المحتسبة
		public decimal TaxAmount { get; set; }

		// الإجمالي النهائي (SubTotal - Discount + TaxAmount)
		public decimal TotalAmount { get; set; }

		// =========================================
		// طريقة الدفع
		// =========================================

		public PurchasePaymentMethod PaymentMethod { get; set; }
			= PurchasePaymentMethod.Credit;

		// الخزنة/البنك المستخدم في حالة الدفع النقدي (اختياري)
		public int? CashAccountId { get; set; }

		public CashAccount? CashAccount { get; set; }

		[MaxLength(500)]
		public string? PaymentDetails { get; set; }

		// =========================================
		// تاريخ الاستحقاق (للفواتير الآجلة)
		// =========================================

		public DateTime? DueDate { get; set; }

		// =========================================
		// الحالة
		// =========================================

		public PurchaseInvoiceStatus Status { get; set; }
			= PurchaseInvoiceStatus.Draft;

		// =========================================
		// البنود
		// =========================================

		public ICollection<PurchaseInvoiceItem> Items { get; set; }
			= new List<PurchaseInvoiceItem>();
	}
}