using AccountingSystem.Data;
using AccountingSystem.Filters;
using AccountingSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Controllers
{
	public class CategoryController : BaseController
	{
		public CategoryController(ApplicationDbContext context) : base(context) { }

		private static void Normalize(Category model)
		{
			model.Name = model.Name.TrimOrEmpty();
			model.Description = model.Description.TrimOrNull();
		}

		[HttpGet, RequirePermission("product.view")]
		public async Task<IActionResult> Index()
			=> View(await _context.Categories.AsNoTracking()
				.OrderBy(x => x.Name).ToListAsync());

		[HttpPost, ValidateAntiForgeryToken]
		public async Task<IActionResult> CreateAjax(string name, string? description)
		{
			if (string.IsNullOrWhiteSpace(name))
				return Json(new { success = false, message = "��� ������� �����" });

			var category = new Category
			{
				Name = name.Trim(),
				Description = description?.Trim()
			};

			_context.Categories.Add(category);
			await _context.SaveChangesAsync();

			return Json(new
			{
				success = true,
				id = category.Id,
				name = category.Name,
				description = category.Description ?? ""
			});
		}

		[HttpGet, RequirePermission("product.create")]
		public IActionResult Create() => View();

		[HttpPost, ValidateAntiForgeryToken, RequirePermission("product.create")]
		public async Task<IActionResult> Create(Category model)
		{
			Normalize(model);
			if (!ModelState.IsValid) return View(model);

			if (await _context.Categories.AnyAsync(x => x.Name == model.Name))
			{
				ModelState.AddModelError(nameof(model.Name), "��� ������� ������ ������.");
				return View(model);
			}

			_context.Categories.Add(new Category
			{
				Name = model.Name,
				Description = model.Description,
				IsActive = true,
				CreatedAt = DateTime.UtcNow
			});
			await _context.SaveChangesAsync();

			return this.RedirectWithSuccess(nameof(Index), "�� ����� ������� �����.");
		}

		[HttpGet, RequirePermission("product.edit")]
		public async Task<IActionResult> Edit(int id)
		{
			var category = await _context.Categories.FindAsync(id);
			return category == null ? NotFound() : View(category);
		}

		[HttpPost, ValidateAntiForgeryToken, RequirePermission("product.edit")]
		public async Task<IActionResult> Edit(int id, Category model)
		{
			if (id != model.Id) return BadRequest();

			Normalize(model);
			if (!ModelState.IsValid) return View(model);

			if (await _context.Categories.AnyAsync(x =>
					x.Name == model.Name && x.Id != model.Id))
			{
				ModelState.AddModelError(nameof(model.Name), "��� ������� ������ ������.");
				return View(model);
			}

			var category = await _context.Categories.FindAsync(id);
			if (category == null) return NotFound();

						// ✅ Refactor: SetValues مع حماية الحقول الحساسة
			var protectedValues = new
			{
				category.IsActive,
				category.CreatedAt
			};

			_context.Entry(category).CurrentValues.SetValues(model);

			category.IsActive = protectedValues.IsActive;
			category.CreatedAt = protectedValues.CreatedAt;

			await _context.SaveChangesAsync();
			return this.RedirectWithSuccess(nameof(Index), "�� ����� ������� �����.");
		}

		[HttpPost, ValidateAntiForgeryToken, RequirePermission("product.edit")]
		public Task<IActionResult> ToggleActive(int id)
	=> ToggleActiveAsync(id, _context.Categories, "�������");
	}
}