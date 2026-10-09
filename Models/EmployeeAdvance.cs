using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
// حالة السلفة
public enum AdvanceStatus
{
Paid = 1,       // مصروفة، لسه ما اتخصمتش
Deducted = 2,   // اتخصمت في راتب
Cancelled = 3   // ملغاة
}

// سلفة موظف
public class EmployeeAdvance
{
public int Id { get; set; }

public int EmployeeId { get; set; }
public Employee? Employee { get; set; }

public decimal Amount { get; set; }

public DateTime Date { get; set; } = DateTime.Today;

[MaxLength(500)]
public string? Reason { get; set; }

// من أي خزنة
public int CashAccountId { get; set; }
public CashAccount? CashAccount { get; set; }

public AdvanceStatus Status { get; set; } = AdvanceStatus.Paid;

// لما تتخصم في راتب، نسجل معرّف سطر الراتب
public int? PayrollItemId { get; set; }
public PayrollItem? PayrollItem { get; set; }

public DateTime? DeductedAt { get; set; }

// القيد المحاسبي للصرف
public int? JournalEntryId { get; set; }
public JournalEntry? JournalEntry { get; set; }

public int? CreatedByUserId { get; set; }
public User? CreatedByUser { get; set; }

public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
}