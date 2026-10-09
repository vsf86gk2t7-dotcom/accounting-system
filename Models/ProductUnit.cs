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

		/// <summary>
		/// معامل التحويل إلى الوحدة الأساسية.
		/// 
		/// - للوحدات الثابتة (Standard): يُستخدم كقيمة افتراضية فقط،
		///   والقيمة الفعلية تُقرأ من Unit.PiecesPerUnit.
		/// 
		/// - للوحدات المتغيرة (Variable): يمثل اقتراحاً/آخر قيمة استُخدمت،
		///   لكن يمكن تجاوزه في كل فاتورة على حدة.
		/// </summary>
		[Range(0.000001, double.MaxValue,
			ErrorMessage = "معامل التحويل يجب أن يكون أكبر من صفر.")]
		public decimal ConversionFactor { get; set; } = 1;

		// =========================================
		// سعر البيع لهذه الوحدة
		// =========================================

		public decimal SalePrice { get; set; }

		public bool IsActive { get; set; } = true;

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	}
}