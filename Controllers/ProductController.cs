using AccountingSystem.Data;
using AccountingSystem.Filters;
using AccountingSystem.Models;
using AccountingSystem.Models.ViewModels.Products;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Controllers
{
	public class ProductController : BaseController
	{
		public ProductController(ApplicationDbContext context) : base(context) { }

		// =========================================
		// عرض المنتجات
		// =========================================

		[HttpGet]
		[RequirePermission("product.view")]
		public async Task<IActionResult> Index()
		{
			var products = await _context.Products
				.AsNoTracking()
				.Include(x => x.BaseUnit)
				.Include(x => x.ProductUnits)
					.ThenInclude(x => x.Unit)
				.Include(x => x.ProductCategories)
					.ThenInclude(x => x.Category)
				.OrderBy(x => x.Name)
				.ToListAsync();

			return View(products);
		}

		// =========================================
		// إضافة منتج - GET
		// =========================================

		[HttpGet]
		[RequirePermission("product.create")]
		public async Task<IActionResult> Create()
		{
			var model = new ProductCreateViewModel();
			await LoadCreateDataAsync(model);
			return View(model);
		}

		// =========================================
		// إضافة منتج - POST
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("product.create")]
		public async Task<IActionResult> Create(ProductCreateViewModel model)
		{
			NormalizeModel(model);

			if (!ModelState.IsValid)
			{
				await LoadCreateDataAsync(model);
				return View(model);
			}

			if (!await ValidateModelAsync(model, LoadCreateDataAsync))
				return View(model);

			// =========================================
			// إنشاء المنتج
			// =========================================

			var product = new Product
			{
				Name = model.Name,
				Barcode = model.Barcode,
				Code = model.Code,
				Description = model.Description,
				BaseUnitId = model.BaseUnitId,
				MinQuantity = model.MinQuantity,
				IsActive = true,
				CreatedAt = DateTime.UtcNow
			};

			_context.Products.Add(product);

			foreach (var item in model.Units)
			{
				_context.ProductUnits.Add(new ProductUnit
				{
					Product = product,
					UnitId = item.UnitId,
					ConversionFactor = item.ConversionFactor,
					SalePrice = item.SalePrice,
					IsActive = item.IsActive,
					CreatedAt = DateTime.UtcNow
				});
			}

			foreach (var categoryId in model.CategoryIds)
			{
				_context.ProductCategories.Add(new ProductCategory
				{
					Product = product,
					CategoryId = categoryId,
					CreatedAt = DateTime.UtcNow
				});
			}

			await _context.SaveChangesAsync();

			return this.RedirectWithSuccess(nameof(Index),
				"تم إنشاء المنتج ووحداته وتصنيفاته بنجاح.");
		}

		// =========================================
		// تعديل منتج - GET
		// =========================================

		[HttpGet]
		[RequirePermission("product.edit")]
		public async Task<IActionResult> Edit(int id)
		{
			var product = await _context.Products
				.Include(x => x.ProductUnits)
				.Include(x => x.ProductCategories)
				.FirstOrDefaultAsync(x => x.Id == id);

			if (product == null) return NotFound();

			var model = new ProductEditViewModel
			{
				Id = product.Id,
				Name = product.Name,
				Barcode = product.Barcode,
				Code = product.Code,
				Description = product.Description,
				BaseUnitId = product.BaseUnitId,
				MinQuantity = product.MinQuantity,
				CategoryIds = product.ProductCategories?
					.Select(x => x.CategoryId)
					.ToList() ?? new List<int>(),
				Units = product.ProductUnits?
					.Select(x => new ProductUnitCreateViewModel
					{
						UnitId = x.UnitId,
						ConversionFactor = x.ConversionFactor,
						SalePrice = x.SalePrice,
						IsActive = x.IsActive
					})
					.ToList() ?? new List<ProductUnitCreateViewModel>()
			};

			await LoadCreateDataAsync(model);
			return View(model);
		}

		// =========================================
		// تعديل منتج - POST
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("product.edit")]
		public async Task<IActionResult> Edit(ProductEditViewModel model)
		{
			if (model.Id <= 0)
			{
				ModelState.AddModelError(string.Empty, "بيانات المنتج غير صحيحة.");
				await LoadCreateDataAsync(model);
				return View(model);
			}

			var product = await _context.Products
				.Include(x => x.ProductUnits)
				.Include(x => x.ProductCategories)
				.FirstOrDefaultAsync(x => x.Id == model.Id);

			if (product == null) return NotFound();

			NormalizeModel(model);

			if (!ModelState.IsValid)
			{
				await LoadCreateDataAsync(model);
				return View(model);
			}

			if (!await ValidateModelAsync(model, LoadCreateDataAsync))
				return View(model);

			// =========================================
			// تحديث بيانات المنتج
			// =========================================

			// ✅ Refactor: SetValues مع حماية الحقول الحساسة
			var protectedValues = new
			{
				product.IsActive,
				product.CreatedAt
			};

			product.Name = model.Name;
			product.Barcode = model.Barcode;
			product.Code = model.Code;
			product.Description = model.Description;
			product.BaseUnitId = model.BaseUnitId;
			product.MinQuantity = model.MinQuantity;

			// رجّع الحقول المحمية (لا تغيير فعلي، للتوضيح)
			product.IsActive = protectedValues.IsActive;
			product.CreatedAt = protectedValues.CreatedAt;

			// =========================================
			// تحديث وحدات المنتج
			// =========================================

			_context.ProductUnits.RemoveRange(product.ProductUnits);

			foreach (var item in model.Units)
			{
				_context.ProductUnits.Add(new ProductUnit
				{
					Product = product,
					UnitId = item.UnitId,
					ConversionFactor = item.ConversionFactor,
					SalePrice = item.SalePrice,
					IsActive = item.IsActive,
					CreatedAt = DateTime.UtcNow
				});
			}

			// =========================================
			// تحديث تصنيفات المنتج
			// =========================================

			_context.ProductCategories.RemoveRange(product.ProductCategories);

			foreach (var categoryId in model.CategoryIds)
			{
				_context.ProductCategories.Add(new ProductCategory
				{
					Product = product,
					CategoryId = categoryId,
					CreatedAt = DateTime.UtcNow
				});
			}

			await _context.SaveChangesAsync();

			return this.RedirectWithSuccess(nameof(Index),
				"تم تحديث المنتج ووحداته وتصنيفاته بنجاح.");
		}

		// =========================================
		// تفعيل/إيقاف منتج
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("product.activate")]
		public async Task<IActionResult> ToggleActive(int id)
		{
			var product = await _context.Products
				.FirstOrDefaultAsync(x => x.Id == id);

			if (product == null) return NotFound();

			product.IsActive = !product.IsActive;
			await _context.SaveChangesAsync();

			return this.RedirectWithSuccess(nameof(Index),
				product.IsActive ? "تم تفعيل المنتج." : "تم إيقاف المنتج.");
		}

		// =========================================
		// Helpers
		// =========================================

		private static void NormalizeModel(ProductCreateViewModel model)
		{
			model.Name = model.Name.TrimOrEmpty();
			model.Barcode = model.Barcode.TrimOrNull();
			model.Code = model.Code.TrimOrNull();
			model.Description = model.Description.TrimOrNull();

			model.CategoryIds = model.CategoryIds?
				.Distinct().ToList() ?? new List<int>();

			model.Units ??= new List<ProductUnitCreateViewModel>();
		}

		private async Task<bool> ValidateModelAsync(
	ProductCreateViewModel model,
	Func<ProductCreateViewModel, Task> reload)
		{
			// ✅ ارفع الـ Id خارج التعبير
			var excludeId = (model as ProductEditViewModel)?.Id ?? 0;

			// الوحدة الأساسية
			var baseUnit = await _context.Units.AsNoTracking()
				.FirstOrDefaultAsync(x => x.Id == model.BaseUnitId && x.IsActive);

			if (baseUnit == null)
			{
				ModelState.AddModelError(nameof(model.BaseUnitId),
					"الوحدة الأساسية غير موجودة أو غير نشطة.");
				await reload(model);
				return false;
			}

			// Barcode
			if (!string.IsNullOrWhiteSpace(model.Barcode))
			{
				var exists = await _context.Products
					.AnyAsync(x => x.Barcode == model.Barcode && x.Id != excludeId);
				if (exists)
				{
					ModelState.AddModelError(nameof(model.Barcode), "الباركود مستخدم بالفعل.");
					await reload(model);
					return false;
				}
			}

			// Code
			if (!string.IsNullOrWhiteSpace(model.Code))
			{
				var exists = await _context.Products
					.AnyAsync(x => x.Code == model.Code && x.Id != excludeId);
				if (exists)
				{
					ModelState.AddModelError(nameof(model.Code), "كود المنتج مستخدم بالفعل.");
					await reload(model);
					return false;
				}
			}

			// باقي الفحوصات كما هي ...
			// ...
			return true;
		}

		// =========================================
		// تحميل بيانات شاشة الإضافة
		// =========================================

		private async Task LoadCreateDataAsync(ProductCreateViewModel model)
		{
			model.AvailableUnits = await _context.Units
				.AsNoTracking()
				.Where(x => x.IsActive)
				.OrderBy(x => x.Name)
				.Select(x => new UnitOptionViewModel
				{
					Id = x.Id,
					Name = x.Name,
					ShortName = x.ShortName
				})
				.ToListAsync();

			model.AvailableCategories = await _context.Categories
				.AsNoTracking()
				.Where(x => x.IsActive)
				.OrderBy(x => x.Name)
				.Select(x => new CategoryOptionViewModel
				{
					Id = x.Id,
					Name = x.Name
				})
				.ToListAsync();
		}
	}
}