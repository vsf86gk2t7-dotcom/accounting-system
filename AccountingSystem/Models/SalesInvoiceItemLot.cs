using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
	public class SalesInvoiceItemLot
	{
		public int Id { get; set; }

		// =========================================
		// سطر الفاتورة
		// =========================================

		[Required]
		public int SalesInvoiceItemId { get; set; }

		public SalesInvoiceItem? SalesInvoiceItem { get; set; }

		// =========================================
		// الدفعة المصروف منها
		// =========================================

		[Required]
		public int StockLotId { get; set; }

		public StockLot? StockLot { get; set; }

		// =========================================
		// الكمية المصروفة
		// بالوحدة الأساسية
		// =========================================

		public decimal QuantityBaseUnit { get; set; }

		// =========================================
		// تكلفة الوحدة وقت البيع
		// يتم حفظها لضمان ثبات تكلفة المبيعات
		// =========================================

		public decimal UnitCost { get; set; }

		// =========================================
		// إجمالي تكلفة الجزء المصروف
		// =========================================

		public decimal TotalCost { get; set; }
	}
}
