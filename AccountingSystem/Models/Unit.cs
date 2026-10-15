using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
	public class Unit
	{
		public int Id { get; set; }

		[Required]
		[MaxLength(100)]
		public string Name { get; set; } = string.Empty;

		[MaxLength(20)]
		public string? ShortName { get; set; }

		public bool IsActive { get; set; } = true;

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	}
}
