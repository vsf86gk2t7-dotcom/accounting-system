using System.ComponentModel.DataAnnotations;
using AccountingSystem.Models;

namespace AccountingSystem.ViewModels
{
public class SalesRepListItem
{
public int Id { get; set; }
public string EmployeeName { get; set; } = string.Empty;
public string? Phone { get; set; }
public CommissionType CommissionType { get; set; }
public decimal CommissionRate { get; set; }
public decimal MonthlyTarget { get; set; }
public decimal MonthSales { get; set; }
public int CustomersCount { get; set; }
public string? StoreName { get; set; }
public bool IsActive { get; set; }
}

public class SalesRepCreateViewModel
{
[Required(ErrorMessage = "اختر الموظف.")]
public int EmployeeId { get; set; }

[Required(ErrorMessage = "اختر الفرع.")]
public int BranchId { get; set; }

public CommissionType CommissionType { get; set; }
= CommissionType.PercentOfSales;

[Range(0, 1000000, ErrorMessage = "قيمة العمولة غير صحيحة.")]
public decimal CommissionRate { get; set; }

[Range(0, 1000000000, ErrorMessage = "الهدف غير صحيح.")]
public decimal MonthlyTarget { get; set; }
}

public class SalesRepEditViewModel
{
public int Id { get; set; }
public string EmployeeName { get; set; } = string.Empty;

public CommissionType CommissionType { get; set; }

[Range(0, 1000000, ErrorMessage = "قيمة العمولة غير صحيحة.")]
public decimal CommissionRate { get; set; }

[Range(0, 1000000000, ErrorMessage = "الهدف غير صحيح.")]
public decimal MonthlyTarget { get; set; }
}

public class SalesRepDetailsViewModel
{
public SalesRepProfile Rep { get; set; } = null!;
public decimal MonthSales { get; set; }
public int MonthInvoicesCount { get; set; }
public List<Customer> Customers { get; set; } = new();
public List<SalesInvoice> LastInvoices { get; set; } = new();
}

public class SalesRepDashboardViewModel
{
public string RepName { get; set; } = string.Empty;
public string? EmployeePhone { get; set; }
public decimal TodaySales { get; set; }
public decimal MonthSales { get; set; }
public decimal CustodyBalance { get; set; }
public decimal CashBalance { get; set; }
public decimal CommissionEarned { get; set; }
public CommissionType CommissionType { get; set; }
public decimal CommissionRate { get; set; }
public decimal MonthlyTarget { get; set; }
public List<SalesInvoice> RecentInvoices { get; set; } = new();
}
}