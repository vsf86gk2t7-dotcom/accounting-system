using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
	public class SalesReturnInvoiceItem
	{
		public int Id { get; set; }

		[Required]
		public int SalesReturnInvoiceId { get; set; }
		public SalesReturnInvoice? SalesReturnInvoice { get; set; }

		// ✅ البند الأصلي
		public int? SalesInvoiceItemId { get; set; }
		public SalesInvoiceItem? SalesInvoiceItem { get; set; }

		[Required]
		public int ProductId { get; set; }
		public Product? Product { get; set; }

		public int UnitId { get; set; }
		public Unit? Unit { get; set; }

		public decimal ConversionFactor { get; set; }

		public decimal Quantity { get; set; }
		public decimal QuantityInBaseUnit { get; set; }

		public decimal UnitPrice { get; set; }
		public decimal UnitCost { get; set; }

		public decimal Total { get; set; }

		// ✅ تخصيصات الدفعات
		public ICollection<SalesReturnItemLot> LotAllocations { get; set; }
			= new List<SalesReturnItemLot>();
	}
}
