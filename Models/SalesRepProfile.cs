using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
// طريقة احتساب عمولة المندوب
public enum CommissionType
{
PercentOfSales = 1,        // نسبة من المبيعات المؤكدة
PercentOfCollections = 2,  // نسبة من المبالغ المحصّلة
FixedPerInvoice = 3        // مبلغ ثابت لكل فاتورة
}

// بروفايل المندوب: مربوط بموظف واحد (1:1)
public class SalesRepProfile
{
public int Id { get; set; }

public int EmployeeId { get; set; }
public Employee? Employee { get; set; }

public CommissionType CommissionType { get; set; }
= CommissionType.PercentOfSales;

// نسبة مئوية (0-100) أو مبلغ ثابت حسب نوع العمولة
public decimal CommissionRate { get; set; }

public decimal MonthlyTarget { get; set; }

// مخزن العهدة (بضاعة المندوب)
public int? CustodyStoreId { get; set; }
public Store? CustodyStore { get; set; }

// خزنة العهدة النقدية (تحصيلات المندوب)
public int? CashAccountId { get; set; }
public CashAccount? CashAccount { get; set; }

public bool IsActive { get; set; } = true;

public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

public ICollection<Customer> Customers { get; set; }
= new List<Customer>();

public ICollection<SalesInvoice> Invoices { get; set; }
= new List<SalesInvoice>();
}
}