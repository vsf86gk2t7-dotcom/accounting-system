using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
	// =========================================
	// أنواع حركات المخزون المُسجلة يدويًا
	// =========================================

	public enum StockTransactionType
	{
		Receive = 1, // استلام
		Issue = 2, // صرف
		Transfer = 3, // تحويل بين المخازن
		Adjust = 4, // تسوية
		Count = 5 // جرد
	}

	public class StockTransaction
	{
		public int Id { get; set; }

		// =========================================
		// رقم المستند
		// =========================================

		[Required]
		[MaxLength(50)]
		public string TransactionNumber { get; set; } = string.Empty;

		// =========================================
		// نوع الحركة
		// =========================================

		public StockTransactionType Type { get; set; }

		// =========================================
		// المخزن المصدر (صرف، تحويل، تسوية، جرد)
		// =========================================

		public int? FromStoreId { get; set; }

		public Store? FromStore { get; set; }

		// =========================================
		// المخزن الوجهة (استلام، تحويل)
		// =========================================

		public int? ToStoreId { get; set; }

		public Store? ToStore { get; set; }

		// =========================================
		// المستخدم المنفذ
		// =========================================

		public int? CreatedByUserId { get; set; }

		public User? CreatedByUser { get; set; }

		[MaxLength(1000)]
		public string? Notes { get; set; }

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		// =========================================
		// البنود
		// =========================================

		public ICollection<StockTransactionItem> Items { get; set; }
			= new List<StockTransactionItem>();
	}

	public class StockTransactionItem
	{
		public int Id { get; set; }

		[Required]
		public int StockTransactionId { get; set; }

		public StockTransaction? StockTransaction { get; set; }

		[Required]
		public int ProductId { get; set; }

		public Product? Product { get; set; }

		// =========================================
		// الكمية بالوحدة الأساسية (موجبة دائمًا)
		// =========================================

		[Range(0.000001, double.MaxValue,
			ErrorMessage = "الكمية يجب أن تكون أكبر من صفر.")]
		public decimal Quantity { get; set; }

		// =========================================
		// تكلفة الوحدة الأساسية
		// =========================================

		public decimal UnitCost { get; set; }

		// =========================================
		// الدفعة المتأثرة (تدقيق)
		// =========================================

		public int? StockLotId { get; set; }

		public StockLot? StockLot { get; set; }

		[MaxLength(1000)]
		public string? Notes { get; set; }
	}
}
