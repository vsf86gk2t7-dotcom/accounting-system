using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
// راتب موظف واحد في شهر
public class PayrollItem
{
public int Id { get; set; }

public int PayrollRunId { get; set; }
public PayrollRun? PayrollRun { get; set; }

public int EmployeeId { get; set; }
public Employee? Employee { get; set; }

// الراتب الأساسي (Employee.Salary)
public decimal BaseSalary { get; set; }

// ===== خصم الغياب =====
public int AbsenceDays { get; set; }
public decimal AbsenceDeduction { get; set; }

// ===== خصم التأخير =====
public int LateMinutes { get; set; }
public decimal LateDeduction { get; set; }

// ===== خصومات يدوية (من EmployeeDeductions) =====
public decimal ManualDeductions { get; set; }

// ===== السلف المخصومة =====
public decimal AdvancesDeduction { get; set; }

// ===== عمولات (للمناديب) =====
public decimal Commissions { get; set; }

// ===== مكافآت (يدوي) =====
public decimal Bonuses { get; set; }

// ===== الصافي =====
// = BaseSalary + Commissions + Bonuses
//   - AbsenceDeduction - LateDeduction
//   - ManualDeductions - AdvancesDeduction
public decimal NetSalary { get; set; }

[MaxLength(500)]
public string? Notes { get; set; }

public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
}