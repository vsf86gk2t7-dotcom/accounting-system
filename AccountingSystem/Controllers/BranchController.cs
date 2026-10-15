using AccountingSystem.Data;
using AccountingSystem.Filters;
using AccountingSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Controllers
{
	public class BranchController : BaseController
	{
		public BranchController(ApplicationDbContext context) : base(context) { }

		[HttpGet, RequirePermission("branch.view")]
		public async Task<IActionResult> Index()
			=> View(await _context.Branches.AsNoTracking()
				.Include(x => x.Company).OrderBy(x => x.Name).ToListAsync());

		[HttpGet, RequirePermission("branch.create")]
		public async Task<IActionResult> Create()
		{
			if (!await _context.Companies.AnyAsync())
				return this.RedirectWithError("Index", "Company",
					"íÌÈ ÅäÔÇÁ ÇáÔÑßÉ ÃæáğÇ ŞÈá ÅÖÇİÉ ÇáİÑæÚ.");
			return View();
		}

		[HttpPost, ValidateAntiForgeryToken, RequirePermission("branch.create")]
		public async Task<IActionResult> Create(Branch branch)
		{
			if (!ModelState.IsValid) return View(branch);

			var company = await _context.Companies.FirstOrDefaultAsync();
			if (company == null)
				return this.RedirectWithError("Index", "Company",
					"áÇ ÊæÌÏ ÔÑßÉ ãÓÌáÉ İí ÇáäÙÇã.");

			branch.CompanyId = company.Id;
			branch.Name = branch.Name.Trim();
			branch.CreatedAt = DateTime.UtcNow;
			branch.IsActive = true;

			if (await _context.Branches.AnyAsync(x =>
					x.CompanyId == branch.CompanyId && x.Name == branch.Name))
			{
				ModelState.AddModelError("Name", "ÇÓã ÇáİÑÚ ãæÌæÏ ÈÇáİÚá.");
				return View(branch);
			}

			_context.Branches.Add(branch);
			await _context.SaveChangesAsync();

			return this.RedirectWithSuccess(nameof(Index), "Êã ÅäÔÇÁ ÇáİÑÚ ÈäÌÇÍ.");
		}

		[HttpGet, RequirePermission("branch.edit")]
		public async Task<IActionResult> Edit(int id)
		{
			var branch = await _context.Branches.FindAsync(id);
			return branch == null ? NotFound() : View(branch);
		}

		[HttpPost, ValidateAntiForgeryToken, RequirePermission("branch.edit")]
		public async Task<IActionResult> Edit(int id, Branch model)
		{
			if (id != model.Id) return BadRequest();
			if (!ModelState.IsValid) return View(model);

			var branch = await _context.Branches.FindAsync(id);
			if (branch == null) return NotFound();

			branch.Name = model.Name.Trim();
			branch.Phone = model.Phone;
			branch.Address = model.Address;

			await _context.SaveChangesAsync();
			return this.RedirectWithSuccess(nameof(Index), "Êã ÊÚÏíá ÇáİÑÚ ÈäÌÇÍ.");
		}

		[HttpPost, ValidateAntiForgeryToken, RequirePermission("branch.activate")]
		public Task<IActionResult> ToggleActive(int id)
	=> ToggleActiveAsync(id, _context.Branches, "ÇáİÑÚ");
		[HttpPost, ValidateAntiForgeryToken, RequirePermission("branch.delete")]
		public Task<IActionResult> Delete(int id)
	=> DeleteAsync(
		id,
		_context.Branches,
		"ÇáİÑÚ",
		async b => await _context.Stores.AnyAsync(s => s.BranchId == b.Id),
		"áÇ íãßä ÍĞİ ÇáİÑÚ áÃäå íÍÊæí Úáì ãÎÇÒä. íãßäß ÊÚØíáå ÈÏáğÇ ãä ÍĞİå.");
	}
}