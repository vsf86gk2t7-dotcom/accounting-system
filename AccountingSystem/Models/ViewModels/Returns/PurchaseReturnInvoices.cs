using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
	// =========================================
	// ظپط§طھظˆط±ط© ظ…ط±طھط¬ط¹ ط´ط±ط§ط،
	// ط§ظ„ظ…ظˆط±ط¯ ظٹط³طھظ„ظ… ط§ظ„ط£طµظ†ط§ظپ ظ…ط±ط© ط£ط®ط±ظ‰ ظˆظٹظڈط®طµظ…
	// ط§ظ„ظƒظ…ظٹط§طھ طھظڈط®طµظ… ظ…ظ† ط§ظ„ظ…ط®ط²ظˆظ†
	// =========================================

	public class PurchaseReturnInvoice
	{
		public int Id { get; set; }

		[Required]
		[MaxLength(50)]
		public string InvoiceNumber { get; set; } = string.Empty;

		[Required]
		public int SupplierId { get; set; }

		public Supplier? Supplier { get; set; }

		[Required]
		public int StoreId { get; set; }

		public Store? Store { get; set; }

		public int? BranchId { get; set; }

		public Branch? Branch { get; set; }

		public int? OriginalPurchaseInvoiceId { get; set; }

		public PurchaseInvoice? OriginalPurchaseInvoice { get; set; }

		public DateTime ReturnDate { get; set; } = DateTime.Today;

		[MaxLength(1000)]
		public string? Reason { get; set; }

		public decimal TotalAmount { get; set; }

		public bool IsPosted { get; set; } = true;

		public int? CreatedByUserId { get; set; }

		public User? CreatedByUser { get; set; }

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		public ICollection<PurchaseReturnInvoiceItem> Items { get; set; }
			= new List<PurchaseReturnInvoiceItem>();
	}

	public class PurchaseReturnInvoiceItem
	{
		public int Id { get; set; }

		[Required]
		public int PurchaseReturnInvoiceId { get; set; }

		public PurchaseReturnInvoice? PurchaseReturnInvoice { get; set; }

		[Required]
		public int ProductId { get; set; }

		public Product? Product { get; set; }

		public int? PurchaseInvoiceItemId { get; set; }

		public PurchaseInvoiceItem? PurchaseInvoiceItem { get; set; }

		[Range(0.000001, double.MaxValue,
			ErrorMessage = "ط§ظ„ظƒظ…ظٹط© ظٹط¬ط¨ ط£ظ† طھظƒظˆظ† ط£ظƒط¨ط± ظ…ظ† طµظپط±.")]
		public decimal Quantity { get; set; }

		public decimal UnitCost { get; set; }

		public decimal Total { get; set; }
	}
}
