using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
	public class SalesInvoice
	{
		public int Id { get; set; }

		[Required]
		[MaxLength(50)]
		public string InvoiceNumber { get; set; } = string.Empty;

		public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;

		// =========================================
		// الفرع والمخزن
		// =========================================

		[Required]
		public int BranchId { get; set; }

		public Branch? Branch { get; set; }

		[Required]
		public int StoreId { get; set; }

		public Store? Store { get; set; }

		// =========================================
		// العميل
		// =========================================
		public enum StockIssueMethod
		{
			FIFO = 1,
			Manual = 2
		}
		public int? CustomerId { get; set; }

		public Customer? Customer { get; set; }

		// =========================================
		// المندوب صاحب الفاتورة (للعمولة والتقارير)
		// =========================================

		public int? SalesRepId { get; set; }

		public SalesRepProfile? SalesRep { get; set; }

		// =========================================
		// الحسابات
		// =========================================

		public decimal SubTotal { get; set; }

		public decimal DiscountAmount { get; set; }

		public decimal TaxAmount { get; set; }
		// =========================================
		// ضريبة المبيعات + ض.ق.م (منفصلتين)
		// =========================================
		public decimal SalesTaxRate { get; set; }
		public decimal SalesTaxAmount { get; set; }
		public decimal TaxRate { get; set; }        // معدل ض.ق.م

		public decimal TotalAmount { get; set; }

		// =========================================
		// طريقة الدفع
		// =========================================

		public SalesPaymentMethod PaymentMethod { get; set; }
			= SalesPaymentMethod.Cash;

		[MaxLength(500)]
		public string? PaymentDetails { get; set; }

		// =========================================
		// تاريخ الاستحقاق (للآجل فقط)
		// =========================================

		public DateTime? DueDate { get; set; }

		// =========================================
		// حالة الفاتورة
		// =========================================

		public SalesInvoiceStatus Status { get; set; }
			= SalesInvoiceStatus.Draft;

		[MaxLength(1000)]
		public string? Notes { get; set; }

		// =========================================
		// التواريخ
		// =========================================

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		public DateTime? ConfirmedAt { get; set; }
		
    // =========================================
    // ✅ RowVersion — حماية من التزامن
    //    (منع ترحيل نفس الفاتورة مرتين)
    // =========================================

    public byte[] RowVersion { get; set; } = null!;

		// =========================================
		// المستخدم الذي أنشأ الفاتورة
		// =========================================

		public int? CreatedByUserId { get; set; }

		public User? CreatedByUser { get; set; }

		// =========================================
		// التفاصيل
		// =========================================

		public ICollection<SalesInvoiceItem> Items { get; set; }
			= new List<SalesInvoiceItem>();

		// =========================================
		// فاتورة الشحن المرتبطة
		// =========================================

		public int? ShippingBillId { get; set; }

		public ShippingBill? ShippingBill { get; set; }
	}

	public enum SalesInvoiceStatus
	{
		Draft = 1,
		Confirmed = 2,
		Cancelled = 3
	}

	public enum SalesPaymentMethod
	{
		Cash = 1,
		BankTransfer = 2,
		Cheque = 3,
		Credit = 4,
		Wallet = 5
	}
}