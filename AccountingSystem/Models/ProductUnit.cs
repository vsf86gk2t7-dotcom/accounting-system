using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
	public class ProductUnit
	{
		public int Id { get; set; }

		// =========================================
		// المنتج
		// =========================================

		[Required]
		public int ProductId { get; set; }

		public Product? Product { get; set; }

		// =========================================
		// الوحدة
		// =========================================

		[Required]
		public int UnitId { get; set; }

		public Unit? Unit { get; set; }

		// =========================================
		// معامل التحويل إلى الوحدة الأساسية
		// =========================================

		[Range(0.000001, double.MaxValue)]
		public decimal ConversionFactor { get; set; } = 1;

		// =========================================
		// سعر البيع لهذه الوحدة
		// =========================================

		public decimal SalePrice { get; set; }

		public bool IsActive { get; set; } = true;

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	}
}
