namespace AccountingSystem.Models
{
	public class ProductCategory
	{
		public int ProductId { get; set; }

		public Product Product { get; set; } = null!;

		public int CategoryId { get; set; }

		public Category Category { get; set; } = null!;

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	}
}
