using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
	public class SalesReturnInvoice
	{
		public int Id { get; set; }

		[Required]
		[MaxLength(50)]
		public string InvoiceNumber { get; set; } = string.Empty;

		// ✅ الفاتورة الأصلية
		public int? OriginalSalesInvoiceId { get; set; }
		public SalesInvoice? OriginalSalesInvoice { get; set; }

		[Required]
		public int CustomerId { get; set; }
		public Customer? Customer { get; set; }

		[Required]
		public int StoreId { get; set; }
		public Store? Store { get; set; }

		public int? BranchId { get; set; }
		public Branch? Branch { get; set; }

		public DateTime ReturnDate { get; set; } = DateTime.UtcNow;
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		[MaxLength(500)]
		public string? Reason { get; set; }

		// ✅ رد نقدي
		public bool RefundToCash { get; set; } = false;
		public int? RefundCashAccountId { get; set; }
		public CashAccount? RefundCashAccount { get; set; }
		public decimal RefundAmount { get; set; }

		public decimal TotalAmount { get; set; }

		public bool IsPosted { get; set; } = true;

		public int? CreatedByUserId { get; set; }
		public User? CreatedByUser { get; set; }
		
    // =========================================
    // ✅ RowVersion — حماية من التزامن
    // =========================================

    public byte[] RowVersion { get; set; } = null!;

		public ICollection<SalesReturnInvoiceItem> Items { get; set; }
			= new List<SalesReturnInvoiceItem>();
	}
}
