using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccountingSystem.Models
{
	/// <summary>
	/// يتذكر آخر تعبئة استخدمها مورد معيّن لصنف معيّن بوحدة معيّنة.
	/// يُستخدم لاقتراح القيمة تلقائياً في الفواتير القادمة.
	/// مثال: المورد (ليتو) + (كريب سادة) + (طقم) = 18 قطعة.
	/// </summary>
	[Table("SupplierProductPackings")]
	public class SupplierProductPacking
	{
		public int Id { get; set; }

		// =========================================
		// المورد
		// =========================================

		[Required]
		public int SupplierId { get; set; }

		public Supplier? Supplier { get; set; }

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
		// عدد القطع الفعلي
		// =========================================

		/// <summary>
		/// عدد القطع الفعلي الذي استخدمه المورد آخر مرة لهذا الصنف بهذه الوحدة.
		/// </summary>
		[Range(0.0001, 100000,
			ErrorMessage = "عدد القطع يجب أن يكون أكبر من صفر.")]
		public decimal ConversionFactor { get; set; }

		// =========================================

		public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;
	}
}