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
					return Json(new { success = false, message = "«”„ «·ÊÕœ… „ÿ·Ê»" });

				if (await _context.Units.AnyAsync(u => u.Name == name))
					return Json(new { success = false, message = "«·ÊÕœ… „ÊÃÊœ… »«·›⁄·" });

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
				return Json(new { success = false, message = "ÕœÀ Œÿ√: " + ex.Message });
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
				ModelState.AddModelError(nameof(model.Name), "«”„ «·ÊÕœ… „” Œœ„ »«·›⁄·.");
				return View(model);
			}

			if (model.ShortName != null &&
				await _context.Units.AnyAsync(x => x.ShortName == model.ShortName))
			{
				ModelState.AddModelError(nameof(model.ShortName), "«Œ ’«— «·ÊÕœ… „” Œœ„ »«·›⁄·.");
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

			return this.RedirectWithSuccess(nameof(Index), " „ ≈‰‘«¡ «·ÊÕœ… »‰Ã«Õ.");
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
				ModelState.AddModelError(nameof(model.Name), "«”„ «·ÊÕœ… „” Œœ„ »«·›⁄·.");
				return View(model);
			}

			if (model.ShortName != null &&
				await _context.Units.AnyAsync(x =>
					x.ShortName == model.ShortName && x.Id != model.Id))
			{
				ModelState.AddModelError(nameof(model.ShortName), "«Œ ’«— «·ÊÕœ… „” Œœ„ »«·›⁄·.");
				return View(model);
			}

			var unit = await _context.Units.FindAsync(id);
			if (unit == null) return NotFound();

			unit.Name = model.Name;
			unit.ShortName = model.ShortName;

			await _context.SaveChangesAsync();
			return this.RedirectWithSuccess(nameof(Index), " „  ⁄œÌ· «·ÊÕœ… »‰Ã«Õ.");
		}

		[HttpPost, ValidateAntiForgeryToken, RequirePermission("product.edit")]
		public Task<IActionResult> ToggleActive(int id)
	=> ToggleActiveAsync(id, _context.Units, "«·ÊÕœ…");
	}
}