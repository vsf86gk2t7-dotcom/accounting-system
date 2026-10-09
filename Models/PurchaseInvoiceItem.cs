using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
	public class PurchaseInvoiceItem
	{
		public int Id { get; set; }

		// =========================================
		// الفاتورة
		// =========================================

		[Required]
		public int PurchaseInvoiceId { get; set; }

		public PurchaseInvoice? PurchaseInvoice { get; set; }

		// =========================================
		// المنتج
		// =========================================

		[Required]
		public int ProductId { get; set; }

		public Product? Product { get; set; }

		// =========================================
		// الوحدة التي تم الشراء بها
		// =========================================

		[Required]
		public int UnitId { get; set; }

		public Unit? Unit { get; set; }

		// =========================================
		// Snapshot لمعامل التحويل وقت الشراء
		// (لا يتأثر بتغيير ProductUnit لاحقًا)
		// =========================================

		public decimal ConversionFactor { get; set; }

		// =========================================
		// الكمية بوحدة الشراء
		// =========================================

		public decimal Quantity { get; set; }

		// =========================================
		// الكمية بالوحدة الأساسية (قطعة)
		// = Quantity × ConversionFactor
		// =========================================

		public decimal QuantityInBaseUnit { get; set; }

		// =========================================
		// سعر الوحدة (بوحدة الشراء)
		// =========================================

		public decimal UnitPrice { get; set; }

		// =========================================
		// تكلفة الوحدة الأساسية
		// = UnitPrice ÷ ConversionFactor
		// =========================================

		public decimal UnitCostInBaseUnit { get; set; }

		// =========================================
		// إجمالي البند
		// = Quantity × UnitPrice
		// =========================================

		public decimal LineTotal { get; set; }

		// =========================================
		// الدفعة الناتجة (بعد الترحيل)
		// =========================================

		public StockLot? StockLot { get; set; }
	}
}
