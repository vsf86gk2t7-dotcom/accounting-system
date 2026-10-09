using AccountingSystem.Data;
using AccountingSystem.Filters;
using AccountingSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Controllers
{
	public class UnitController : BaseController
	{
		public UnitController(ApplicationDbContext context) : base(context) { }

		private static void Normalize(Unit model)
		{
			model.Name = model.Name.TrimOrEmpty();
			model.ShortName = model.ShortName.TrimOrNull();
		}

		[HttpGet, RequirePermission("product.view")]
		public async Task<IActionResult> Index()
			=> View(await _context.Units.AsNoTracking().OrderBy(x => x.Name).ToListAsync());

		[HttpPost, ValidateAntiForgeryToken]
		public async Task<IActionResult> CreateAjax(string name, string shortName)
		{
			try
			{
				name = (name ?? "").Trim();
				shortName = (shortName ?? "").Trim();

				if (string.IsNullOrWhiteSpace(name))
					return Json(new { success = false, message = "اسم الوحدة مطلوب." });

				if (await _context.Units.AnyAsync(u => u.Name == name))
					return Json(new { success = false, message = "الوحدة موجودة بالفعل." });

				var unit = new Unit
				{
					Name = name,
					ShortName = string.IsNullOrWhiteSpace(shortName) ? null : shortName,
					CreatedAt = DateTime.UtcNow
				};

				_context.Units.Add(unit);
				await _context.SaveChangesAsync();

				return Json(new
				{
					success = true,
					id = unit.Id,
					name = unit.Name,
					shortName = unit.ShortName
				});
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = "حدث خطأ: " + ex.Message });
			}
		}

		[HttpGet, RequirePermission("product.create")]
		public IActionResult Create() => View();

		[HttpPost, ValidateAntiForgeryToken, RequirePermission("product.create")]
		public async Task<IActionResult> Create(Unit model)
		{
			Normalize(model);
			if (!ModelState.IsValid) return View(model);

			if (await _context.Units.AnyAsync(x => x.Name == model.Name))
			{
				ModelState.AddModelError(nameof(model.Name), "اسم الوحدة موجود بالفعل.");
				return View(model);
			}

			if (model.ShortName != null &&
				await _context.Units.AnyAsync(x => x.ShortName == model.ShortName))
			{
				ModelState.AddModelError(nameof(model.ShortName), "الرمز المختصر موجود بالفعل.");
				return View(model);
			}

			_context.Units.Add(new Unit
			{
				Name = model.Name,
				ShortName = model.ShortName,
				IsActive = true,
				CreatedAt = DateTime.UtcNow
			});
			await _context.SaveChangesAsync();

			return this.RedirectWithSuccess(nameof(Index), "تم إنشاء الوحدة بنجاح.");
		}

		[HttpGet, RequirePermission("product.edit")]
		public async Task<IActionResult> Edit(int id)
		{
			var unit = await _context.Units.FindAsync(id);
			return unit == null ? NotFound() : View(unit);
		}

		[HttpPost, ValidateAntiForgeryToken, RequirePermission("product.edit")]
		public async Task<IActionResult> Edit(int id, Unit model)
		{
			if (id != model.Id) return BadRequest();

			Normalize(model);
			if (!ModelState.IsValid) return View(model);

			if (await _context.Units.AnyAsync(x =>
					x.Name == model.Name && x.Id != model.Id))
			{
				ModelState.AddModelError(nameof(model.Name), "اسم الوحدة موجود بالفعل.");
				return View(model);
			}

			if (model.ShortName != null &&
				await _context.Units.AnyAsync(x =>
					x.ShortName == model.ShortName && x.Id != model.Id))
			{
				ModelState.AddModelError(nameof(model.ShortName), "الرمز المختصر موجود بالفعل.");
				return View(model);
			}

			var unit = await _context.Units.FindAsync(id);
			if (unit == null) return NotFound();

			// ✅ Refactor: SetValues مع حماية الحقول الحساسة
			var protectedValues = new
			{
				unit.IsActive,
				unit.CreatedAt
			};

			_context.Entry(unit).CurrentValues.SetValues(model);

			unit.IsActive = protectedValues.IsActive;
			unit.CreatedAt = protectedValues.CreatedAt;

			await _context.SaveChangesAsync();
			return this.RedirectWithSuccess(nameof(Index), "تم تعديل الوحدة بنجاح.");
		}

		[HttpPost, ValidateAntiForgeryToken, RequirePermission("product.edit")]
		public Task<IActionResult> ToggleActive(int id)
			=> ToggleActiveAsync(id, _context.Units, "الوحدة");
	}
}