using AccountingSystem.Data;
using AccountingSystem.Filters;
using AccountingSystem.Models;
using AccountingSystem.Services;
using AccountingSystem.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Controllers
{
public class SalesRepController : BaseController
{
private readonly ISalesRepService _repService;

public SalesRepController(ApplicationDbContext context, ISalesRepService repService) : base(context)
{
_repService = repService;
}

private static DateTime MonthStart()
=> new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

private static bool RateIsValid(CommissionType type, decimal rate)
=> type == CommissionType.FixedPerInvoice || rate <= 100;

// =========================================
// القائمة
// =========================================
[HttpGet]
[RequirePermission("salesrep.view")]
public async Task<IActionResult> Index()
{
var monthStart = MonthStart();

var items = await _context.SalesRepProfiles
.AsNoTracking()
.OrderBy(p => p.Employee!.Name)
.Select(p => new SalesRepListItem
{
Id = p.Id,
EmployeeName = p.Employee!.Name,
Phone = p.Employee.Phone,
CommissionType = p.CommissionType,
CommissionRate = p.CommissionRate,
MonthlyTarget = p.MonthlyTarget,
StoreName = p.CustodyStore != null ? p.CustodyStore.Name : null,
IsActive = p.IsActive,
CustomersCount = p.Customers.Count,
MonthSales = p.Invoices
.Where(i => i.Status == SalesInvoiceStatus.Confirmed
&& i.InvoiceDate >= monthStart)
.Sum(i => (decimal?)i.TotalAmount) ?? 0
})
.ToListAsync();

return View(items);
}

// =========================================
// إضافة مندوب
// =========================================
[HttpGet]
[RequirePermission("salesrep.manage")]
public async Task<IActionResult> Create()
{
await LoadCreateListsAsync();
return View(new SalesRepCreateViewModel());
}

[HttpPost]
[ValidateAntiForgeryToken]
[RequirePermission("salesrep.manage")]
public async Task<IActionResult> Create(SalesRepCreateViewModel model)
{
if (!RateIsValid(model.CommissionType, model.CommissionRate))
ModelState.AddModelError(nameof(model.CommissionRate),
"النسبة المئوية يجب ألا تزيد عن 100.");

var employee = await _context.Employees
.FirstOrDefaultAsync(x => x.Id == model.EmployeeId && x.IsActive);

if (employee == null)
ModelState.AddModelError(nameof(model.EmployeeId), "الموظف غير موجود أو غير نشط.");
else if (await _context.SalesRepProfiles.AnyAsync(x => x.EmployeeId == employee.Id))
ModelState.AddModelError(nameof(model.EmployeeId), "هذا الموظف مندوب بالفعل.");

var branch = await _context.Branches
.FirstOrDefaultAsync(x => x.Id == model.BranchId && x.IsActive);

if (branch == null)
ModelState.AddModelError(nameof(model.BranchId), "الفرع غير موجود.");

var parent = await _context.ChartAccounts
.FirstOrDefaultAsync(x => x.Code == "11");

if (parent == null)
ModelState.AddModelError(string.Empty,
"حساب الأصول المتداولة (كود 11) غير موجود في شجرة الحسابات.");

if (!ModelState.IsValid)
{
await LoadCreateListsAsync();
return View(model);
}

var strategy = _context.Database.CreateExecutionStrategy();
int newId = 0;

await strategy.ExecuteAsync(async () =>
{
await using var tx = await _context.Database.BeginTransactionAsync();

var codes = await _context.ChartAccounts
.Where(x => x.Code != null && x.Code.Length == 4 && x.Code.StartsWith("11"))
.Select(x => x.Code!)
.ToListAsync();

var maxCode = codes
.Select(c => int.TryParse(c, out var n) ? n : 0)
.DefaultIfEmpty(0)
.Max();

var chartAccount = new ChartAccount
{
Code = (Math.Max(1109, maxCode) + 1).ToString(),
Name = $"عهدة نقدية - {employee!.Name}",
Type = AccountType.Asset,
ParentId = parent!.Id,
IsSystem = false,
IsActive = true
};

var cashAccount = new CashAccount
{
Name = $"عهدة نقدية - {employee.Name}",
Provider = WalletProvider.General,
ChartAccount = chartAccount,
IsActive = true
};

var store = new Store
{
BranchId = branch!.Id,
Name = $"عهدة - {employee.Name}",
Code = $"REP-{employee.Id}",
IsActive = true
};

_context.CashAccounts.Add(cashAccount);
_context.Stores.Add(store);

if (!await _context.EmployeeBranches.AnyAsync(x =>
x.EmployeeId == employee.Id && x.BranchId == branch.Id))
{
_context.EmployeeBranches.Add(new EmployeeBranch
{
EmployeeId = employee.Id,
BranchId = branch.Id
});
}

_context.EmployeeStores.Add(new EmployeeStore
{
EmployeeId = employee.Id,
Store = store
});

var profile = new SalesRepProfile
{
EmployeeId = employee.Id,
CommissionType = model.CommissionType,
CommissionRate = model.CommissionRate,
MonthlyTarget = model.MonthlyTarget,
CustodyStore = store,
CashAccount = cashAccount
};

_context.SalesRepProfiles.Add(profile);

employee.EmployeeType = EmployeeType.SalesRep;

await _context.SaveChangesAsync();
await tx.CommitAsync();

newId = profile.Id;
});

return this.RedirectWithSuccess(nameof(Index),
"تم إنشاء المندوب مع مخزن العهدة والخزنة النقدية.");
}

// =========================================
// تعديل العمولة والهدف
// =========================================
[HttpGet]
[RequirePermission("salesrep.manage")]
public async Task<IActionResult> Edit(int id)
{
var rep = await _context.SalesRepProfiles
.AsNoTracking()
.Include(x => x.Employee)
.FirstOrDefaultAsync(x => x.Id == id);

if (rep == null) return NotFound();

return View(new SalesRepEditViewModel
{
Id = rep.Id,
EmployeeName = rep.Employee?.Name ?? string.Empty,
CommissionType = rep.CommissionType,
CommissionRate = rep.CommissionRate,
MonthlyTarget = rep.MonthlyTarget
});
}

[HttpPost]
[ValidateAntiForgeryToken]
[RequirePermission("salesrep.manage")]
public async Task<IActionResult> Edit(int id, SalesRepEditViewModel model)
{
if (id != model.Id) return BadRequest();

if (!RateIsValid(model.CommissionType, model.CommissionRate))
ModelState.AddModelError(nameof(model.CommissionRate),
"النسبة المئوية يجب ألا تزيد عن 100.");

if (!ModelState.IsValid) return View(model);

var rep = await _context.SalesRepProfiles.FirstOrDefaultAsync(x => x.Id == id);
if (rep == null) return NotFound();

rep.CommissionType = model.CommissionType;
rep.CommissionRate = model.CommissionRate;
rep.MonthlyTarget = model.MonthlyTarget;

await _context.SaveChangesAsync();

return this.RedirectWithSuccess(nameof(Index), "تم تحديث بيانات المندوب.");
}

[HttpPost]
[ValidateAntiForgeryToken]
[RequirePermission("salesrep.manage")]
public Task<IActionResult> ToggleActive(int id)
=> ToggleActiveAsync(id, _context.SalesRepProfiles, "المندوب");

// =========================================
// تفاصيل المندوب
// =========================================
[HttpGet]
[RequirePermission("salesrep.view")]
public async Task<IActionResult> Details(int id)
{
var rep = await _context.SalesRepProfiles
.AsNoTracking()
.Include(x => x.Employee)
.Include(x => x.CustodyStore)
.Include(x => x.CashAccount)
.FirstOrDefaultAsync(x => x.Id == id);

if (rep == null) return NotFound();

var monthStart = MonthStart();

var monthQuery = _context.SalesInvoices
.AsNoTracking()
.Where(i => i.SalesRepId == id
&& i.Status == SalesInvoiceStatus.Confirmed
&& i.InvoiceDate >= monthStart);

return View(new SalesRepDetailsViewModel
{
Rep = rep,
MonthSales = await monthQuery.SumAsync(i => (decimal?)i.TotalAmount) ?? 0,
MonthInvoicesCount = await monthQuery.CountAsync(),
Customers = await _context.Customers
.AsNoTracking()
.Where(c => c.SalesRepId == id)
.OrderBy(c => c.Name)
.Take(100)
.ToListAsync(),
LastInvoices = await _context.SalesInvoices
.AsNoTracking()
.Include(i => i.Customer)
.Where(i => i.SalesRepId == id)
.OrderByDescending(i => i.InvoiceDate)
.ThenByDescending(i => i.Id)
.Take(10)
.ToListAsync()
});
}

// =========================================
// لوحة تحكم المندوب
// =========================================
[HttpGet]
[RequirePermission("salesrep.view")]
public async Task<IActionResult> Dashboard()
{
var rep = await _repService.GetCurrentRepProfileAsync();
if (rep == null)
return RedirectToAction("AccessDenied", "Account");

var model = new SalesRepDashboardViewModel
{
RepName = rep.Employee?.Name ?? "مندوب",
EmployeePhone = rep.Employee?.Phone,
TodaySales = await _repService.GetTodaySalesAsync(),
MonthSales = await _repService.GetMonthSalesAsync(),
CustodyBalance = await _repService.GetCustodyBalanceAsync(),
CashBalance = await _repService.GetCashBalanceAsync(),
CommissionEarned = await _repService.GetCommissionEarnedAsync(),
CommissionType = rep.CommissionType,
CommissionRate = rep.CommissionRate,
MonthlyTarget = rep.MonthlyTarget,
RecentInvoices = await _repService.GetRecentInvoicesAsync(10)
};

return View(model);
}

// =========================================
// عملائي
// =========================================
[HttpGet]
[RequirePermission("salesrep.view")]
public async Task<IActionResult> MyCustomers()
{
var repId = await _repService.GetCurrentRepIdAsync();
if (repId == null)
return RedirectToAction("AccessDenied", "Account");

var customers = await _repService.GetMyCustomersAsync();
return View(customers);
}

// =========================================
// فواتيري
// =========================================
[HttpGet]
[RequirePermission("salesrep.view")]
public async Task<IActionResult> MyInvoices()
{
var repId = await _repService.GetCurrentRepIdAsync();
if (repId == null)
return RedirectToAction("AccessDenied", "Account");

var invoices = await _context.SalesInvoices
.AsNoTracking()
.Include(i => i.Customer)
.Where(i => i.SalesRepId == repId.Value)
.OrderByDescending(i => i.InvoiceDate)
.ThenByDescending(i => i.Id)
.ToListAsync();

return View(invoices);
}

// =========================================
// قوائم الاختيار
// =========================================
private async Task LoadCreateListsAsync()
{
ViewBag.Employees = await _context.Employees
.AsNoTracking()
.Where(x => x.IsActive
&& !_context.SalesRepProfiles.Any(p => p.EmployeeId == x.Id))
.OrderBy(x => x.Name)
.ToListAsync();

ViewBag.Branches = await _context.Branches
.AsNoTracking()
.Where(x => x.IsActive)
.OrderBy(x => x.Name)
.ToListAsync();
}
}
}