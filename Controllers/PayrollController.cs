using AccountingSystem.Data;
using AccountingSystem.Filters;
using AccountingSystem.Models;
using AccountingSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AccountingSystem.Controllers
{
public class PayrollController : BaseController
{
private readonly IPayrollService _payrollService;
private readonly IPostingService _postingService;

public PayrollController(
ApplicationDbContext context,
IPayrollService payrollService,
IPostingService postingService) : base(context)
{
_payrollService = payrollService;
_postingService = postingService;
}

private int? CurrentUserId()
{
var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
return int.TryParse(value, out var id) ? id : null;
}

// =========================================
// قائمة أشهر الرواتب
// =========================================

[HttpGet]
[RequirePermission("payroll.view")]
public async Task<IActionResult> Index()
{
var runs = await _context.PayrollRuns
.AsNoTracking()
.OrderByDescending(x => x.PeriodEnd)
.ToListAsync();

return View(runs);
}

// =========================================
// إنشاء شهر جديد — GET
// =========================================

[HttpGet]
[RequirePermission("payroll.manage")]
public IActionResult Create()
{
var today = DateTime.Today;
var firstDay = new DateTime(today.Year, today.Month, 1);
var lastDay = firstDay.AddMonths(1).AddDays(-1);

ViewBag.PeriodName = today.ToString("yyyy-MM");
ViewBag.PeriodStart = firstDay.ToString("yyyy-MM-dd");
ViewBag.PeriodEnd = lastDay.ToString("yyyy-MM-dd");

return View();
}

// =========================================
// إنشاء شهر جديد — POST
// =========================================

[HttpPost]
[ValidateAntiForgeryToken]
[RequirePermission("payroll.manage")]
public async Task<IActionResult> Create(
string periodName,
DateTime periodStart,
DateTime periodEnd)
{
try
{
var run = await _payrollService.CreateDraftAsync(
periodName,
periodStart,
periodEnd,
CurrentUserId());

TempData["Success"] =
$"تم إنشاء مسودة رواتب '{periodName}'.";

return RedirectToAction(nameof(Details), new { id = run.Id });
}
catch (Exception ex)
{
TempData["Error"] = ex.Message;

ViewBag.PeriodName = periodName;
ViewBag.PeriodStart = periodStart.ToString("yyyy-MM-dd");
ViewBag.PeriodEnd = periodEnd.ToString("yyyy-MM-dd");

return View();
}
}

// =========================================
// تفاصيل شهر
// =========================================

[HttpGet]
[RequirePermission("payroll.view")]
public async Task<IActionResult> Details(int id)
{
var run = await _context.PayrollRuns
.AsNoTracking()
.Include(x => x.Items)
.ThenInclude(i => i.Employee)
.FirstOrDefaultAsync(x => x.Id == id);

if (run == null) return NotFound();

return View(run);
}

// =========================================
// إعادة حساب المسودة
// =========================================

[HttpPost]
[ValidateAntiForgeryToken]
[RequirePermission("payroll.manage")]
public async Task<IActionResult> Recalculate(int id)
{
try
{
await _payrollService.RecalculateAsync(id);
TempData["Success"] = "تم إعادة الحساب.";
}
catch (Exception ex)
{
TempData["Error"] = ex.Message;
}

return RedirectToAction(nameof(Details), new { id });
}

// =========================================
// اعتماد الشهر (قيد استحقاق)
// =========================================

[HttpPost]
[ValidateAntiForgeryToken]
[RequirePermission("payroll.approve")]
public async Task<IActionResult> Approve(int id)
{
var run = await _context.PayrollRuns
.FirstOrDefaultAsync(x => x.Id == id);

if (run == null) return NotFound();

if (run.Status != PayrollRunStatus.Draft)
{
TempData["Error"] = "لا يمكن اعتماد إلا المسودة.";
return RedirectToAction(nameof(Details), new { id });
}

var result = await _postingService
.PostPayrollAccrualAsync(run, CurrentUserId());

if (!result.Success)
{
TempData["Error"] =
"تعذر الاعتماد: " + result.Error;
return RedirectToAction(nameof(Details), new { id });
}

run.Status = PayrollRunStatus.Approved;
run.ApprovedAt = DateTime.UtcNow;
run.JournalEntryId = result.JournalEntryId;
await _context.SaveChangesAsync();

TempData["Success"] =
$"تم اعتماد الشهر وقيد الاستحقاق رقم {result.EntryNumber}.";

return RedirectToAction(nameof(Details), new { id });
}

// =========================================
// صرف الشهر — GET
// =========================================

[HttpGet]
[RequirePermission("payroll.pay")]
public async Task<IActionResult> Pay(int id)
{
var run = await _context.PayrollRuns
.AsNoTracking()
.FirstOrDefaultAsync(x => x.Id == id);

if (run == null) return NotFound();

if (run.Status != PayrollRunStatus.Approved)
{
TempData["Error"] = "لا يمكن الصرف إلا بعد الاعتماد.";
return RedirectToAction(nameof(Details), new { id });
}

ViewBag.RunId = id;
ViewBag.TotalNet = run.TotalNet;
ViewBag.PeriodName = run.PeriodName;

ViewBag.CashAccounts = await _context.CashAccounts
.AsNoTracking()
.Where(x => x.IsActive)
.OrderBy(x => x.Name)
.Select(x => new { x.Id, x.Name })
.ToListAsync();

return View();
}

// =========================================
// صرف الشهر — POST
// =========================================

[HttpPost]
[ValidateAntiForgeryToken]
[RequirePermission("payroll.pay")]
public async Task<IActionResult> Pay(int id, int cashAccountId)
{
var run = await _context.PayrollRuns
.FirstOrDefaultAsync(x => x.Id == id);

if (run == null) return NotFound();

if (run.Status != PayrollRunStatus.Approved)
{
TempData["Error"] = "لا يمكن الصرف إلا بعد الاعتماد.";
return RedirectToAction(nameof(Details), new { id });
}

var result = await _postingService
.PostPayrollPaymentAsync(run, cashAccountId, CurrentUserId());

if (!result.Success)
{
TempData["Error"] = "تعذر الصرف: " + result.Error;
return RedirectToAction(nameof(Details), new { id });
}

// خصم من الخزنة
_context.TreasuryTransactions.Add(new TreasuryTransaction
{
TransactionNumber =
$"PAY-PR-{run.Id}-{DateTime.Now:yyyyMMddHHmmss}",
Type = TreasuryTransactionType.Pay,
CashAccountId = cashAccountId,
Amount = run.TotalNet,
Reason = $"صرف رواتب {run.PeriodName}",
ReferenceDocument = run.PeriodName,
AutoJournal = false,
CreatedByUserId = CurrentUserId(),
CreatedAt = DateTime.UtcNow
});

run.Status = PayrollRunStatus.Paid;
run.PaidAt = DateTime.UtcNow;
run.PaymentJournalEntryId = result.JournalEntryId;
run.PaymentCashAccountId = cashAccountId;

// تحديث السلف المخصومة
var advances = await _context.EmployeeAdvances
.Where(x => x.Status == AdvanceStatus.Paid)
.ToListAsync();

foreach (var item in run.Items)
{
if (item.AdvancesDeduction > 0)
{
var empAdvances = advances
.Where(a => a.EmployeeId == item.EmployeeId)
.OrderBy(a => a.Date)
.ToList();

var remaining = item.AdvancesDeduction;

foreach (var adv in empAdvances)
{
if (remaining <= 0) break;

adv.Status = AdvanceStatus.Deducted;
adv.PayrollItemId = item.Id;
adv.DeductedAt = DateTime.UtcNow;
remaining -= adv.Amount;
}
}
}

await _context.SaveChangesAsync();

TempData["Success"] =
$"تم صرف الرواتب — قيد الصرف {result.EntryNumber}.";

return RedirectToAction(nameof(Details), new { id });
}

// =========================================
// حذف المسودة
// =========================================

[HttpPost]
[ValidateAntiForgeryToken]
[RequirePermission("payroll.manage")]
public async Task<IActionResult> Delete(int id)
{
try
{
await _payrollService.DeleteDraftAsync(id);
TempData["Success"] = "تم حذف المسودة.";
}
catch (Exception ex)
{
TempData["Error"] = ex.Message;
}

return RedirectToAction(nameof(Index));
}
}
}