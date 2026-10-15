using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
	public class Product
	{
		public int Id { get; set; }

		[Required]
		[MaxLength(200)]
		public string Name { get; set; } = string.Empty;

		[MaxLength(100)]
		public string? Barcode { get; set; }

		[MaxLength(100)]
		public string? Code { get; set; }

		[MaxLength(1000)]
		public string? Description { get; set; }

		public bool IsActive { get; set; } = true;

		// =========================================
		// الحد الأدنى للمخزون (تنبيه إعادة الطلب)
		// =========================================

		public decimal MinQuantity { get; set; } = 0;

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
		public int BaseUnitId { get; set; }
		public Unit? BaseUnit { get; set; }
		// =========================================
		// وحدات المنتج
		// =========================================

		public ICollection<ProductUnit> ProductUnits { get; set; }
			= new List<ProductUnit>();
		public ICollection<ProductCategory> ProductCategories { get; set; }
= new List<ProductCategory>();
		// =========================================
		// دفعات المخزون
		// =========================================

		public ICollection<StockLot> StockLots { get; set; }
			= new List<StockLot>();
	}
}
