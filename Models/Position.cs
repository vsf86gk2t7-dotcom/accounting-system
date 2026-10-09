using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
// المسمى الوظيفي (اسمه Position عشان لا يتعارض مع Employee.JobTitle القديمة)
public class Position
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