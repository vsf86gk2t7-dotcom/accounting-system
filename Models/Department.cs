using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
public class Department
{
public int Id { get; set; }

[Required]
[MaxLength(100)]
public string Name { get; set; } = string.Empty;

public bool IsActive { get; set; } = true;

public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

public ICollection<Employee> Employees { get; set; }
= new List<Employee>();
}
}