using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
	public class Category
	{
		public int Id { get; set; }

		[Required]
		[MaxLength(200)]
		public string Name { get; set; } = string.Empty;

		[MaxLength(1000)]
		public string? Description { get; set; }

		public bool IsActive { get; set; } = true;

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		// =========================================
		// المنتجات المرتبطة بالتصنيف
		// =========================================

		public ICollection<ProductCategory> ProductCategories { get; set; }
			= new List<ProductCategory>();
	}
}
