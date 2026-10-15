using AccountingSystem.Data;
using AccountingSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Services
{
public interface IEmployeeAdvanceService
{
Task<EmployeeAdvance> CreateAsync(
int employeeId,
decimal amount,
DateTime date,
string? reason,
int cashAccountId,
int? userId);
}

public class EmployeeAdvanceService(
ApplicationDbContext context,
IPostingService postingService) : IEmployeeAdvanceService
{
public async Task<EmployeeAdvance> CreateAsync(
int employeeId,
decimal amount,
DateTime date,
string? reason,
int cashAccountId,
int? userId)
{
if (amount <= 0)
throw new InvalidOperationException(
"قيمة السلفة يجب أن تكون أكبر من صفر.");

var employee = await context.Employees
.AsNoTracking()
.FirstOrDefaultAsync(x =>
x.Id == employeeId && x.IsActive)
?? throw new InvalidOperationException(
"الموظف غير موجود أو غير نشط.");

var cashAccount = await context.CashAccounts
.AsNoTracking()
.FirstOrDefaultAsync(x =>
x.Id == cashAccountId && x.IsActive)
?? throw new InvalidOperationException(
"الخزنة غير موجودة أو غير نشطة.");

var advance = new EmployeeAdvance
{
EmployeeId = employeeId,
Amount = amount,
Date = date.Date,
Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
CashAccountId = cashAccountId,
Status = AdvanceStatus.Paid,
CreatedByUserId = userId,
CreatedAt = DateTime.UtcNow
};

context.EmployeeAdvances.Add(advance);
await context.SaveChangesAsync();

// ترحيل القيد
advance.Employee = employee;

var postingResult = await postingService
.PostEmployeeAdvanceAsync(advance, userId);

if (!postingResult.Success)
{
// إلغاء السلفة لو فشل الترحيل
context.EmployeeAdvances.Remove(advance);
await context.SaveChangesAsync();

throw new InvalidOperationException(
"تعذر ترحيل السلفة: " + postingResult.Error);
}

advance.JournalEntryId = postingResult.JournalEntryId;
await context.SaveChangesAsync();

return advance;
}
}
}