using AccountingSystem.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Controllers
{
	public abstract class BaseController : Controller
	{
		protected readonly ApplicationDbContext _context;

		protected BaseController(ApplicationDbContext context)
		{
			_context = context;
		}

		// =========================================
		// ToggleActiveAsync
		// =========================================
		protected async Task<IActionResult> ToggleActiveAsync<T>(
			int id,
			DbSet<T> dbSet,
			string entityLabel,
			string actionName = "Index") where T : class
		{
			var entity = await dbSet.FindAsync(id);
			if (entity == null) return NotFound();

			var prop = typeof(T).GetProperty("IsActive");
			if (prop == null || prop.PropertyType != typeof(bool))
				throw new InvalidOperationException(
					$"النوع {typeof(T).Name} لا يحتوي على خاصية IsActive من نوع bool.");

			var wasActive = (bool)prop.GetValue(entity)!;
			prop.SetValue(entity, !wasActive);

			await _context.SaveChangesAsync();

			TempData["Success"] = !wasActive
				? $"تم تفعيل {entityLabel}."
				: $"تم تعطيل {entityLabel}.";

			return RedirectToAction(actionName);
		}

		// =========================================
		// DeleteAsync (مع فحص الاعتماديات)
		// =========================================
		protected async Task<IActionResult> DeleteAsync<T>(
			int id,
			DbSet<T> dbSet,
			string entityLabel,
			Func<T, Task<bool>> hasDependencies,
			string dependencyMessage,
			string actionName = "Index") where T : class
		{
			var entity = await dbSet.FindAsync(id);
			if (entity == null) return NotFound();

			if (await hasDependencies(entity))
			{
				TempData["Error"] = dependencyMessage;
				return RedirectToAction(actionName);
			}

			dbSet.Remove(entity);
			await _context.SaveChangesAsync();

			TempData["Success"] = $"تم حذف {entityLabel} بنجاح.";
			return RedirectToAction(actionName);
		}
	}
}