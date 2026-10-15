using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
// حالة شهر الرواتب
public enum PayrollRunStatus
{
Draft = 1,       // مسودة
Approved = 2,    // معتمد (ترحّل قيد الاستحقاق)
Paid = 3,        // مصروف (ترحّل قيد الصرف)
Cancelled = 4    // ملغي
}

// شهر رواتب
public class PayrollRun
{
public int Id { get; set; }

[Required]
[MaxLength(20)]
public string PeriodName { get; set; } = string.Empty;  // "2026-10"

public DateTime PeriodStart { get; set; }
public DateTime PeriodEnd { get; set; }

public PayrollRunStatus Status { get; set; }
= PayrollRunStatus.Draft;

// إجماليات
public decimal TotalGross { get; set; }
public decimal TotalAbsenceDeduction { get; set; }
public decimal TotalLateDeduction { get; set; }
public decimal TotalManualDeductions { get; set; }
public decimal TotalAdvances { get; set; }
public decimal TotalCommissions { get; set; }
public decimal TotalBonuses { get; set; }
public decimal TotalNet { get; set; }

// القيود المحاسبية
public int? JournalEntryId { get; set; }
public JournalEntry? JournalEntry { get; set; }

public int? PaymentJournalEntryId { get; set; }
public JournalEntry? PaymentJournalEntry { get; set; }

// من أي خزنة اتصرف
public int? PaymentCashAccountId { get; set; }
public CashAccount? PaymentCashAccount { get; set; }

public int? CreatedByUserId { get; set; }
public User? CreatedByUser { get; set; }

public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
public DateTime? ApprovedAt { get; set; }
public DateTime? PaidAt { get; set; }
public DateTime? CancelledAt { get; set; }

[MaxLength(1000)]
public string? Notes { get; set; }

public ICollection<PayrollItem> Items { get; set; }
= new List<PayrollItem>();
}
}