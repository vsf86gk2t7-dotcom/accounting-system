using System.ComponentModel.DataAnnotations;
using static AccountingSystem.Models.SalesInvoice;

namespace AccountingSystem.Models
{
	public class SalesInvoiceItem
	{
		public int Id { get; set; }

		// =========================================
		// الفاتورة
		// =========================================

		[Required]
		public int SalesInvoiceId { get; set; }

		public SalesInvoice? SalesInvoice { get; set; }

		// =========================================
		// المنتج
		// =========================================

		[Required]
		public int ProductId { get; set; }

		public Product? Product { get; set; }

		// =========================================
		// الوحدة (اللي اتباع بيها)
		// =========================================

		[Required]
		public int UnitId { get; set; }

		public Unit? Unit { get; set; }

		// =========================================
		// Snapshot لمعامل التحويل وقت البيع
		// (لا يتأثر بتغيير ProductUnit لاحقًا)
		// =========================================

		public decimal ConversionFactor { get; set; }

		// =========================================
		// الكمية بوحدة البيع
		// =========================================

		public decimal Quantity { get; set; }

		// =========================================
		// الكمية بالوحدة الأساسية (قطعة)
		// = Quantity × ConversionFactor
		// =========================================

		public decimal QuantityInBaseUnit { get; set; }

		// =========================================
		// سعر الوحدة (بوحدة البيع)
		// =========================================

		public decimal UnitPrice { get; set; }

		// =========================================
		// إجمالي البند
		// = Quantity × UnitPrice
		// =========================================

		public decimal TotalPrice { get; set; }

		// =========================================
		// طريقة صرف المخزون
		// =========================================

		public StockIssueMethod IssueMethod { get; set; }
			= StockIssueMethod.FIFO;

		// =========================================
		// الدفعة المختارة (لو Manual)
		// =========================================

		public int? SelectedStockLotId { get; set; }

		public StockLot? SelectedStockLot { get; set; }

		// =========================================
		// ملاحظات
		// =========================================

		[MaxLength(500)]
		public string? Notes { get; set; }

		// =========================================
		// تخصيصات الدفعات (FIFO / Manual)
		// =========================================

		public ICollection<SalesInvoiceItemLot> LotAllocations { get; set; }
			= new List<SalesInvoiceItemLot>();
	}
}
