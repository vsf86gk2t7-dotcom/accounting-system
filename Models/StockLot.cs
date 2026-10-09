using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
	public class StockLot
	{
		public int Id { get; set; }

		[Required]
		public int StoreId { get; set; }

		public Store? Store { get; set; }

		[Required]
		public int ProductId { get; set; }

		public Product? Product { get; set; }

		// =========================================
		// المورد (اختياري)
		// null يعني: دفعة نظامية
		// (تسوية / جرد / تحويل / افتتاحي)
		// =========================================

		public int? SupplierId { get; set; }

		public Supplier? Supplier { get; set; }

		public int? PurchaseInvoiceItemId { get; set; }

		public PurchaseInvoiceItem? PurchaseInvoiceItem { get; set; }

		public decimal QuantityReceived { get; set; }

		public decimal QuantityRemaining { get; set; }

		public decimal UnitCost { get; set; }

		public DateTime PurchaseDate { get; set; }

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		public bool IsActive { get; set; } = true;

		// =========================================
		// للتحكم في التزامن (منع Overselling)
		// SQL Server يديرها تلقائيًا كـ rowversion
		//
		// ملاحظة: الإعداد الفعلي في ApplicationDbContext
		//         عبر .IsRowVersion()
		// =========================================

		public byte[] RowVersion { get; set; } = null!;
	}
}