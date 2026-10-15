using AccountingSystem.Data;
using AccountingSystem.Filters;
using AccountingSystem.Models;
using AccountingSystem.Models.ViewModels.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AccountingSystem.Controllers
{
	public class UserController : BaseController
	{
		public UserController(ApplicationDbContext context) : base(context) { }

		// =========================================
		// ⁄—÷ «·„” Œœ„Ì‰
		// =========================================

		[HttpGet]
		[RequirePermission("user.view")]
		public async Task<IActionResult> Index()
		{
			var users = await _context.Users
				.AsNoTracking()
				.Include(x => x.Role)
				.Include(x => x.Employee)
				.Include(x => x.Customer)
				.Include(x => x.Supplier)
				.OrderBy(x => x.Id)
				.ToListAsync();

			return View(users);
		}

		// =========================================
		//  ⁄ÿÌ· /  ›⁄Ì· „” Œœ„
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("user.activate")]
		public async Task<IActionResult> ToggleActive(int id)
		{
			var user = await _context.Users.FindAsync(id);
			if (user == null) return NotFound();

			var currentUserIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

			if (int.TryParse(currentUserIdValue, out var currentUserId)
				&& user.Id == currentUserId)
			{
				return this.RedirectWithError(nameof(Index),
					"·« Ì„ﬂ‰ﬂ  ⁄ÿÌ· Õ”«»ﬂ.");
			}

			user.IsActive = !user.IsActive;
			await _context.SaveChangesAsync();

			return this.RedirectWithSuccess(nameof(Index),
				user.IsActive ? " „  ›⁄Ì· «·„” Œœ„." : " „  ⁄ÿÌ· «·„” Œœ„.");
		}

		// =========================================
		// ≈÷«›… „ÊŸ›
		// =========================================

		[HttpGet]
		[RequirePermission("employee.create")]
		public async Task<IActionResult> Create()
		{
			var model = new EmployeeCreateViewModel();
			await LoadCreateDataAsync(model);
			return View(model);
		}

		// =========================================
		// ≈‰‘«¡ «·„ÊŸ› Ê«·Õ”«»
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("employee.create")]
		public async Task<IActionResult> Create(EmployeeCreateViewModel model)
		{
			// =========================================
			//  ‰ŸÌ› «·»Ì«‰« 
			// =========================================

			model.Name = model.Name.TrimOrEmpty();
			model.Phone = model.Phone.TrimOrEmpty();
			model.Email = model.Email.TrimOrNull();
			model.JobTitle = model.JobTitle.TrimOrNull();

			model.BranchIds = model.BranchIds?.Distinct().ToList() ?? new List<int>();
			model.StoreIds = model.StoreIds?.Distinct().ToList() ?? new List<int>();

			if (!ModelState.IsValid)
			{
				await LoadCreateDataAsync(model);
				return View(model);
			}

			// =========================================
			// «· √ﬂœ √‰ —ﬁ„ «·Â« › €Ì— „” Œœ„
			// =========================================

			if (await _context.Users.AnyAsync(x => x.Phone == model.Phone))
			{
				ModelState.AddModelError(nameof(model.Phone),
					"—ﬁ„ «·Â« › „” Œœ„ »«·›⁄·.");
				await LoadCreateDataAsync(model);
				return View(model);
			}

			// =========================================
			// «· √ﬂœ „‰ ÊÃÊœ «·œÊ—
			// =========================================

			var role = await _context.Roles
				.AsNoTracking()
				.FirstOrDefaultAsync(x => x.Id == model.RoleId && x.IsActive);

			if (role == null)
			{
				ModelState.AddModelError(nameof(model.RoleId),
					"«·œÊ— «·„Õœœ €Ì— „ÊÃÊœ √Ê €Ì— ‰‘ÿ.");
				await LoadCreateDataAsync(model);
				return View(model);
			}

			if (role.Code == "Admin")
			{
				ModelState.AddModelError(nameof(model.RoleId),
					"·« Ì„ﬂ‰ ≈‰‘«¡ Õ”«» „œÌ— «·‰Ÿ«„ „‰ ‘«‘… «·„ÊŸ›Ì‰.");
				await LoadCreateDataAsync(model);
				return View(model);
			}

			// =========================================
			// Ã·» «·›—Ê⁄ «·„Õœœ…
			// =========================================

			var branches = await _context.Branches
				.Where(x => x.IsActive && model.BranchIds.Contains(x.Id))
				.ToListAsync();

			if (branches.Count != model.BranchIds.Count)
			{
				ModelState.AddModelError(nameof(model.BranchIds),
					"ÌÊÃœ ›—⁄ €Ì— „ÊÃÊœ √Ê €Ì— ‰‘ÿ.");
				await LoadCreateDataAsync(model);
				return View(model);
			}

			// =========================================
			// Ã·» «·„Œ«“‰ «·„Õœœ…
			// =========================================

			var stores = await _context.Stores
				.Where(x => x.IsActive && model.StoreIds.Contains(x.Id))
				.ToListAsync();

			if (stores.Count != model.StoreIds.Count)
			{
				ModelState.AddModelError(nameof(model.StoreIds),
					"ÌÊÃœ „Œ“‰ €Ì— „ÊÃÊœ √Ê €Ì— ‰‘ÿ.");
				await LoadCreateDataAsync(model);
				return View(model);
			}

			// =========================================
			// «· Õﬁﬁ „‰ √‰ «·„Œ«“‰  «»⁄… ··›—Ê⁄
			// =========================================

			var selectedBranchIds = branches.Select(x => x.Id).ToHashSet();

			if (stores.Any(x => !selectedBranchIds.Contains(x.BranchId)))
			{
				ModelState.AddModelError(nameof(model.StoreIds),
					"ÌÊÃœ „Œ“‰ ·« Ì »⁄ √Õœ «·›—Ê⁄ «·„Õœœ….");
				await LoadCreateDataAsync(model);
				return View(model);
			}

			// =========================================
			// ≈‰‘«¡ «·„ÊŸ›
			// =========================================

			var employee = new Employee
			{
				Name = model.Name,
				JobTitle = model.JobTitle,
				Phone = model.Phone,
				Email = model.Email,
				Salary = model.Salary,
				IsActive = true,
				CreatedAt = DateTime.UtcNow
			};

			_context.Employees.Add(employee);
			await _context.SaveChangesAsync();

			// =========================================
			// —»ÿ «·„ÊŸ› »«·›—Ê⁄
			// =========================================

			foreach (var branch in branches)
			{
				_context.EmployeeBranches.Add(new EmployeeBranch
				{
					EmployeeId = employee.Id,
					BranchId = branch.Id,
					CreatedAt = DateTime.UtcNow
				});
			}

			// =========================================
			// —»ÿ «·„ÊŸ› »«·„Œ«“‰
			// =========================================

			foreach (var store in stores)
			{
				_context.EmployeeStores.Add(new EmployeeStore
				{
					EmployeeId = employee.Id,
					StoreId = store.Id,
					CreatedAt = DateTime.UtcNow
				});
			}

			await _context.SaveChangesAsync();

			// =========================================
			// ≈‰‘«¡ Õ”«» «·„” Œœ„
			// =========================================

			var user = new User
			{
				Phone = model.Phone,
				UserType = UserType.Employee,
				RoleId = role.Id,
				EmployeeId = employee.Id,
				Status = UserStatus.Approved,
				IsActive = true,
				IsPasswordSet = true,
				CreatedAt = DateTime.UtcNow
			};

			var passwordHasher = new PasswordHasher<User>();
			user.PasswordHash = passwordHasher.HashPassword(user, model.Password);

			_context.Users.Add(user);
			await _context.SaveChangesAsync();

			return this.RedirectWithSuccess(nameof(Index),
				" „ ≈‰‘«¡ «·„ÊŸ› ÊÕ”«» «·œŒÊ· »‰Ã«Õ.");
		}

		// =========================================
		//  Õ„Ì· »Ì«‰«  ‘«‘… «·≈÷«›…
		// =========================================

		private async Task LoadCreateDataAsync(EmployeeCreateViewModel model)
		{
			model.Roles = await _context.Roles
				.AsNoTracking()
				.Where(x => x.IsActive && x.Code != "Admin")
				.OrderBy(x => x.Name)
				.Select(x => new RoleOptionViewModel
				{
					Id = x.Id,
					Name = x.Name,
					Code = x.Code
				})
				.ToListAsync();

			model.Branches = await _context.Branches
				.AsNoTracking()
				.Where(x => x.IsActive)
				.OrderBy(x => x.Name)
				.Select(x => new BranchOptionViewModel
				{
					Id = x.Id,
					Name = x.Name,
					IsSelected = model.BranchIds.Contains(x.Id)
				})
				.ToListAsync();

			model.Stores = await _context.Stores
				.AsNoTracking()
				.Where(x => x.IsActive)
				.OrderBy(x => x.BranchId).ThenBy(x => x.Name)
				.Select(x => new StoreOptionViewModel
				{
					Id = x.Id,
					BranchId = x.BranchId,
					Name = x.Name,
					Code = x.Code,
					IsSelected = model.StoreIds.Contains(x.Id)
				})
				.ToListAsync();
		}
	}
}