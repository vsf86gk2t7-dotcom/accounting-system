using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
	public class Role
	{
		public int Id { get; set; }

		[Required]
		[MaxLength(100)]
		public string Name { get; set; } = string.Empty;

		[Required]
		[MaxLength(100)]
		public string Code { get; set; } = string.Empty;

		[MaxLength(500)]
		public string? Description { get; set; }

		public bool IsActive { get; set; } = true;

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		public ICollection<RolePermission> RolePermissions { get; set; }
			= new List<RolePermission>();
	}
}
