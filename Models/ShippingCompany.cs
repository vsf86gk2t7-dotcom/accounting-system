using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
	public enum DeliveryMethod
	{
		InternalDriver = 1,
		ExternalCompany = 2
	}

	public enum ShippingBillStatus
	{
		Pending = 1,
		InTransit = 2,
		Delivered = 3,
		Cancelled = 4
	}

	public class ShippingCompany
	{
		public int Id { get; set; }

		[Required]
		[MaxLength(200)]
		public string Name { get; set; } = string.Empty;

		[MaxLength(50)]
		public string? Phone { get; set; }

		[MaxLength(200)]
		public string? Email { get; set; }

		[MaxLength(500)]
		public string? Address { get; set; }

		[MaxLength(100)]
		public string? AccountNumber { get; set; }

		public decimal ShippingRate { get; set; }

		public bool IsActive { get; set; } = true;

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	}
}
