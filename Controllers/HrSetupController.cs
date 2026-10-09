using AccountingSystem.Data;
using AccountingSystem.Filters;
using AccountingSystem.Models;
using AccountingSystem.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Controllers
{
// إعدادات الموارد البشرية: الأقسام والمسميات الوظيفية
public class HrSetupController : BaseController
{
public HrSetupController(ApplicationDbContext context) : base(context) { }

[HttpGet]
[RequirePermission("hr.setup.view")]
public async Task<IActionResult> Index()
{
var vm = new HrSetupViewModel
{
Departments = await _context.Departments
.AsNoTracking()
.OrderBy(x => x.Name)
.Select(x => new HrSetupItem
{
Id = x.Id,
Name = x.Name,
IsActive = x.IsActive,
EmployeesCount = x.Employees.Count
})
.ToListAsync(),

Positions = await _context.Positions
.AsNoTracking()
.OrderBy(x => x.Name)
.Select(x => new HrSetupItem
{
Id = x.Id,
Name = x.Name,
IsActive = x.IsActive,
EmployeesCount = x.Employees.Count
})
.ToListAsync()
};

return View(vm);
}

// ---------- الأقسام ----------

[HttpPost]
[ValidateAntiForgeryToken]
[RequirePermission("hr.setup.manage")]
public async Task<IActionResult> AddDepartment(string? name)
{
name = name?.Trim();

if (string.IsNullOrEmpty(name) || name.Length > 100)
return this.RedirectWithError(nameof(Index), "اسم القسم مطلوب (حتى 100 حرف).");

if (await _context.Departments.AnyAsync(x => x.Name == name))
return this.RedirectWithError(nameof(Index), "هذا القسم موجود بالفعل.");

_context.Departments.Add(new Department { Name = name });
await _context.SaveChangesAsync();

return this.RedirectWithSuccess(nameof(Index), "تمت إضافة القسم.");
}

[HttpPost]
[ValidateAntiForgeryToken]
[RequirePermission("hr.setup.manage")]
public Task<IActionResult> ToggleDepartment(int id)
=> ToggleActiveAsync(id, _context.Departments, "القسم");

// ---------- المسميات الوظيفية ----------

[HttpPost]
[ValidateAntiForgeryToken]
[RequirePermission("hr.setup.manage")]
public async Task<IActionResult> AddPosition(string? name)
{
name = name?.Trim();

if (string.IsNullOrEmpty(name) || name.Length > 100)
return this.RedirectWithError(nameof(Index), "المسمى الوظيفي مطلوب (حتى 100 حرف).");

if (await _context.Positions.AnyAsync(x => x.Name == name))
return this.RedirectWithError(nameof(Index), "هذا المسمى موجود بالفعل.");

_context.Positions.Add(new Position { Name = name });
await _context.SaveChangesAsync();

return this.RedirectWithSuccess(nameof(Index), "تمت إضافة المسمى الوظيفي.");
}

[HttpPost]
[ValidateAntiForgeryToken]
[RequirePermission("hr.setup.manage")]
public Task<IActionResult> TogglePosition(int id)
=> ToggleActiveAsync(id, _context.Positions, "المسمى الوظيفي");
}
}