using AccountingSystem.Data;
using AccountingSystem.Filters;
using AccountingSystem.Models;
using AccountingSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AccountingSystem.Controllers
{
public class EmployeeAdvanceController : BaseController
{
private readonly IEmployeeAdvanceService _advanceService;

public EmployeeAdvanceController(
ApplicationDbContext context,
IEmployeeAdvanceService advanceService) : base(context)
{
_advanceService = advanceService;
}

private int? CurrentUserId()
{
var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
return int.TryParse(value, out var id) ? id : null;
}

// =========================================
// قائمة السلف
// =========================================

[HttpGet]
[RequirePermission("advance.view")]
public async Task<IActionResult> Index(int? employeeId)
{
var query = _context.EmployeeAdvances
.AsNoTracking()
.Include(x => x.Employee)
.Include(x => x.CashAccount)
.AsQueryable();

if (employeeId.HasValue && employeeId.Value > 0)
{
query = query.Where(x => x.EmployeeId == employeeId.Value);
}

var advances = await query
.OrderByDescending(x => x.Date)
.ToListAsync();

ViewBag.Employees = await _context.Employees
.AsNoTracking()
.Where(x => x.IsActive)
.OrderBy(x => x.Name)
.Select(x => new { x.Id, x.Name })
.ToListAsync();

ViewBag.EmployeeId = employeeId;

return View(advances);
}

// =========================================
// صرف سلفة — GET
// =========================================

[HttpGet]
[RequirePermission("advance.create")]
public async Task<IActionResult> Create()
{
ViewBag.Employees = await _context.Employees
.AsNoTracking()
.Where(x => x.IsActive && x.Salary > 0)
.OrderBy(x => x.Name)
.Select(x => new { x.Id, x.Name, x.Salary })
.ToListAsync();

ViewBag.CashAccounts = await _context.CashAccounts
.AsNoTracking()
.Where(x => x.IsActive)
.OrderBy(x => x.Name)
.Select(x => new { x.Id, x.Name })
.ToListAsync();

return View();
}

// =========================================
// صرف سلفة — POST
// =========================================

[HttpPost]
[ValidateAntiForgeryToken]
[RequirePermission("advance.create")]
public async Task<IActionResult> Create(
int employeeId,
decimal amount,
DateTime date,
string? reason,
int cashAccountId)
{
try
{
await _advanceService.CreateAsync(
employeeId,
amount,
date,
reason,
cashAccountId,
CurrentUserId());

TempData["Success"] = "تم صرف السلفة وترحيل قيدها.";
return RedirectToAction(nameof(Index));
}
catch (Exception ex)
{
TempData["Error"] = ex.Message;

ViewBag.Employees = await _context.Employees
.AsNoTracking()
.Where(x => x.IsActive && x.Salary > 0)
.OrderBy(x => x.Name)
.Select(x => new { x.Id, x.Name, x.Salary })
.ToListAsync();

ViewBag.CashAccounts = await _context.CashAccounts
.AsNoTracking()
.Where(x => x.IsActive)
.OrderBy(x => x.Name)
.Select(x => new { x.Id, x.Name })
.ToListAsync();

return View();
}
}

// =========================================
// تفاصيل سلفة
// =========================================

[HttpGet]
[RequirePermission("advance.view")]
public async Task<IActionResult> Details(int id)
{
var advance = await _context.EmployeeAdvances
.AsNoTracking()
.Include(x => x.Employee)
.Include(x => x.CashAccount)
.Include(x => x.JournalEntry)
.FirstOrDefaultAsync(x => x.Id == id);

if (advance == null) return NotFound();

return View(advance);
}
}
}