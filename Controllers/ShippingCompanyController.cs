using AccountingSystem.Data;
using AccountingSystem.Filters;
using AccountingSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Controllers
{
	public class ShippingCompanyController : BaseController
	{
		public ShippingCompanyController(ApplicationDbContext context)
			: base(context) { }

		private static void Normalize(ShippingCompany model)
		{
			model.Name = model.Name.TrimOrEmpty();
			model.Phone = model.Phone.TrimOrNull();
			model.Email = model.Email.TrimOrNull();
			model.Address = model.Address.TrimOrNull();
			model.AccountNumber = model.AccountNumber.TrimOrNull();
		}

		[HttpGet, RequirePermission("shipping.company.manage")]
		public async Task<IActionResult> Index()
			=> View(await _context.ShippingCompanies
				.AsNoTracking()
				.OrderByDescending(x => x.IsActive)
				.ThenBy(x => x.Name)
				.ToListAsync());

		[HttpGet, RequirePermission("shipping.company.manage")]
		public IActionResult Create() => View();

		[HttpPost, ValidateAntiForgeryToken, RequirePermission("shipping.company.manage")]
		public async Task<IActionResult> Create(ShippingCompany model)
		{
			Normalize(model);

			if (!ModelState.IsValid)
			{
				return View(model);
			}

			if (await _context.ShippingCompanies
					.AnyAsync(x => x.Name == model.Name))
			{
				ModelState.AddModelError(
					nameof(model.Name),
					"يوجد شركة شحن بنفس الاسم بالفعل.");

				return View(model);
			}

			if (model.ShippingRate < 0)
			{
				ModelState.AddModelError(
					nameof(model.ShippingRate),
					"سعر الشحن لا يمكن أن يكون سالبًا.");

				return View(model);
			}

			_context.ShippingCompanies.Add(new ShippingCompany
			{
				Name = model.Name,
				Phone = model.Phone,
				Email = model.Email,
				Address = model.Address,
				AccountNumber = model.AccountNumber,
				ShippingRate = model.ShippingRate,
				IsActive = true,
				CreatedAt = DateTime.UtcNow
			});

			await _context.SaveChangesAsync();

			return this.RedirectWithSuccess(
				nameof(Index),
				"تمت إضافة شركة الشحن بنجاح.");
		}

		[HttpGet, RequirePermission("shipping.company.manage")]
		public async Task<IActionResult> Edit(int id)
		{
			var company = await _context.ShippingCompanies.FindAsync(id);

			return company == null ? NotFound() : View(company);
		}

		[HttpPost, ValidateAntiForgeryToken, RequirePermission("shipping.company.manage")]
		public async Task<IActionResult> Edit(int id, ShippingCompany model)
		{
			if (id != model.Id) return BadRequest();

			Normalize(model);

			if (!ModelState.IsValid)
			{
				return View(model);
			}

			if (await _context.ShippingCompanies
					.AnyAsync(x => x.Name == model.Name && x.Id != model.Id))
			{
				ModelState.AddModelError(
					nameof(model.Name),
					"يوجد شركة شحن بنفس الاسم بالفعل.");

				return View(model);
			}

			if (model.ShippingRate < 0)
			{
				ModelState.AddModelError(
					nameof(model.ShippingRate),
					"سعر الشحن لا يمكن أن يكون سالبًا.");

				return View(model);
			}

			var company = await _context.ShippingCompanies.FindAsync(id);

			if (company == null) return NotFound();

			company.Name = model.Name;
			company.Phone = model.Phone;
			company.Email = model.Email;
			company.Address = model.Address;
			company.AccountNumber = model.AccountNumber;
			company.ShippingRate = model.ShippingRate;

			await _context.SaveChangesAsync();

			return this.RedirectWithSuccess(
				nameof(Index),
				"تم حفظ التعديلات بنجاح.");
		}

		[HttpPost, ValidateAntiForgeryToken, RequirePermission("shipping.company.manage")]
		public Task<IActionResult> ToggleActive(int id)
			=> ToggleActiveAsync(id, _context.ShippingCompanies, "شركة الشحن");
	}
}
