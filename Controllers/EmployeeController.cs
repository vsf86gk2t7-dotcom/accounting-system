using AccountingSystem.Data;
using AccountingSystem.Filters;
using AccountingSystem.Models;
using AccountingSystem.Services.QrCode;
using AccountingSystem.Services.WhatsApp;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace AccountingSystem.Controllers
{
	// ⚠️ تم إزالة [AdminOnly]
	// نعتمد الآن على RequirePermission فقط (الأدمن يعدّي تلقائياً)
	public class EmployeeController : BaseController
	{
		private readonly PasswordHasher<User> _passwordHasher;
		private readonly IWhatsAppService _whatsAppService;
		private readonly IQrCodeService _qrCodeService;

		public EmployeeController(
			ApplicationDbContext context,
			IWhatsAppService whatsAppService,
			IQrCodeService qrCodeService) : base(context)
		{
			_passwordHasher = new PasswordHasher<User>();
			_whatsAppService = whatsAppService;
			_qrCodeService = qrCodeService;
		}

		// =========================================
		// Helper: تحميل قوائم الأقسام والمسميات
		// =========================================
		private async Task LoadLookupsAsync()
		{
			ViewBag.Departments = await _context.Departments
				.AsNoTracking()
				.Where(x => x.IsActive)
				.OrderBy(x => x.Name)
				.ToListAsync();

			ViewBag.Positions = await _context.Positions
				.AsNoTracking()
				.Where(x => x.IsActive)
				.OrderBy(x => x.Name)
				.ToListAsync();
		}

		// =========================================
		// Helper: التحقق من الرقم القومي
		// =========================================
		private static bool IsValidNationalId(string? id)
			=> string.IsNullOrEmpty(id)
			   || (id.Length == 14 && id.All(char.IsDigit));

		// =========================================
		// قائمة الموظفين
		// =========================================

		[HttpGet]
		[RequirePermission("employee.view")]
		public async Task<IActionResult> Index(
			string? search,
			int? roleId,
			int? branchId,
			string? status)
		{
			var query = _context.Employees
				.AsNoTracking()
				.Include(x => x.Users).ThenInclude(x => x.Role)
				.Include(x => x.EmployeeBranches).ThenInclude(x => x.Branch)
				.Include(x => x.EmployeeStores).ThenInclude(x => x.Store)
				.AsQueryable();

			if (!string.IsNullOrWhiteSpace(search))
			{
				search = search.Trim();
				query = query.Where(x =>
					x.Name.Contains(search) ||
					(x.Phone != null && x.Phone.Contains(search)) ||
					(x.Email != null && x.Email.Contains(search)));
			}

			if (roleId.HasValue && roleId.Value > 0)
				query = query.Where(x => x.Users.Any(u => u.RoleId == roleId.Value));

			if (branchId.HasValue && branchId.Value > 0)
				query = query.Where(x => x.EmployeeBranches.Any(b => b.BranchId == branchId.Value));

			if (!string.IsNullOrWhiteSpace(status))
			{
				switch (status)
				{
					case "active": query = query.Where(x => x.IsActive); break;
					case "inactive": query = query.Where(x => !x.IsActive); break;
					case "pending":
						query = query.Where(x => x.PortalRequested && !x.HasPortalAccount);
						break;
				}
			}

			var employees = await query.OrderBy(x => x.Name).ToListAsync();

			var allEmployees = await _context.Employees.AsNoTracking().ToListAsync();

			ViewBag.TotalCount = allEmployees.Count;
			ViewBag.ActiveCount = allEmployees.Count(x => x.IsActive);
			ViewBag.InactiveCount = allEmployees.Count(x => !x.IsActive);
			ViewBag.PendingCount = allEmployees.Count(x => x.PortalRequested && !x.HasPortalAccount);

			ViewBag.AvailableRoles = await _context.Roles
				.AsNoTracking()
				.Where(x => x.IsActive && x.Code != "Admin")
				.OrderBy(x => x.Name)
				.ToListAsync();

			ViewBag.AvailableBranches = await _context.Branches
				.AsNoTracking()
				.Where(x => x.IsActive)
				.OrderBy(x => x.Name)
				.ToListAsync();

			ViewBag.Search = search;
			ViewBag.RoleId = roleId;
			ViewBag.BranchId = branchId;
			ViewBag.Status = status;

			return View(employees);
		}

		// =========================================
		// إضافة موظف - GET
		// =========================================

		[HttpGet]
		[RequirePermission("employee.create")]
		public async Task<IActionResult> Create()
		{
			await LoadLookupsAsync();
			return View(new Employee { HireDate = DateTime.Today });
		}

		// =========================================
		// إضافة موظف - POST
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("employee.create")]
		public async Task<IActionResult> Create(Employee model)
		{
			model.Name = model.Name.TrimOrEmpty();
			model.Phone = model.Phone.TrimOrNull();
			model.Email = model.Email.TrimOrNull();
			model.NationalId = model.NationalId.TrimOrNull();
			model.Address = model.Address.TrimOrNull();

			if (!IsValidNationalId(model.NationalId))
				ModelState.AddModelError(nameof(model.NationalId),
					"الرقم القومي يجب أن يكون 14 رقماً.");

			if (model.NationalId != null &&
				await _context.Employees.AnyAsync(x => x.NationalId == model.NationalId))
				ModelState.AddModelError(nameof(model.NationalId),
					"الرقم القومي مسجل لموظف آخر.");

			if (!ModelState.IsValid)
			{
				await LoadLookupsAsync();
				return View(model);
			}

			if (await _context.Employees.AnyAsync(x => x.Phone == model.Phone))
			{
				ModelState.AddModelError(nameof(model.Phone),
					"رقم الهاتف مستخدم بالفعل لموظف آخر.");
				await LoadLookupsAsync();
				return View(model);
			}

			if (model.Phone != null &&
				await _context.Users.AnyAsync(x => x.Phone == model.Phone))
			{
				ModelState.AddModelError(nameof(model.Phone),
					"رقم الهاتف مستخدم بالفعل في حساب مستخدم آخر.");
				await LoadLookupsAsync();
				return View(model);
			}

			_context.Employees.Add(new Employee
			{
				Name = model.Name,
				JobTitle = model.JobTitle,
				Phone = model.Phone,
				Email = model.Email,
				Salary = model.Salary,
				NationalId = model.NationalId,
				HireDate = model.HireDate,
				BirthDate = model.BirthDate,
				Address = model.Address,
				EmployeeType = model.EmployeeType,
				DepartmentId = model.DepartmentId,
				PositionId = model.PositionId,
				IsActive = true,
				CreatedAt = DateTime.UtcNow
			});

			await _context.SaveChangesAsync();

			return this.RedirectWithSuccess(nameof(Index), "تم إنشاء الموظف بنجاح.");
		}

		// =========================================
		// تفاصيل الموظف
		// =========================================

		[HttpGet]
		[RequirePermission("employee.view")]
		public async Task<IActionResult> Details(int id)
		{
			var employee = await _context.Employees
				.AsNoTracking()
				.Include(x => x.Users).ThenInclude(x => x.Role)
				.Include(x => x.EmployeeBranches).ThenInclude(x => x.Branch)
				.Include(x => x.EmployeeStores).ThenInclude(x => x.Store)
				.Include(x => x.Department)
				.Include(x => x.Position)
				.FirstOrDefaultAsync(x => x.Id == id);

			if (employee == null) return NotFound();

			var user = employee.Users.FirstOrDefault();
			ViewBag.User = user;

			if (user != null && user.RoleId.HasValue)
			{
				ViewBag.RolePermissions = await _context.RolePermissions
					.AsNoTracking()
					.Include(x => x.Permission)
					.Where(x => x.RoleId == user.RoleId.Value && x.IsGranted)
					.Select(x => x.Permission)
					.ToListAsync();
			}
			else
			{
				ViewBag.RolePermissions = new List<AccountingSystem.Models.Permission>();
			}

			ViewBag.Branches = employee.EmployeeBranches
				.Select(x => x.Branch).Where(x => x != null).ToList();

			ViewBag.Stores = employee.EmployeeStores
				.Select(x => x.Store).Where(x => x != null).ToList();

			return View(employee);
		}

		// =========================================
		// تعديل موظف - GET
		// =========================================

		[HttpGet]
		[RequirePermission("employee.edit")]
		public async Task<IActionResult> Edit(int id)
		{
			var employee = await _context.Employees.FirstOrDefaultAsync(x => x.Id == id);

			if (employee == null) return NotFound();

			await LoadLookupsAsync();

			return View(employee);
		}

		// =========================================
		// تعديل موظف - POST
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("employee.edit")]
		public async Task<IActionResult> Edit(int id, Employee model)
		{
			if (id != model.Id) return BadRequest();

			model.Name = model.Name.TrimOrEmpty();
			model.Phone = model.Phone.TrimOrNull();
			model.Email = model.Email.TrimOrNull();
			model.NationalId = model.NationalId.TrimOrNull();
			model.Address = model.Address.TrimOrNull();

			if (!IsValidNationalId(model.NationalId))
				ModelState.AddModelError(nameof(model.NationalId),
					"الرقم القومي يجب أن يكون 14 رقماً.");

			if (model.NationalId != null &&
				await _context.Employees.AnyAsync(x =>
					x.NationalId == model.NationalId && x.Id != id))
				ModelState.AddModelError(nameof(model.NationalId),
					"الرقم القومي مسجل لموظف آخر.");

			if (!ModelState.IsValid)
			{
				await LoadLookupsAsync();
				return View(model);
			}

			var employee = await _context.Employees.FirstOrDefaultAsync(x => x.Id == id);
			if (employee == null) return NotFound();

			if (await _context.Employees.AnyAsync(x =>
					x.Phone == model.Phone && x.Id != id))
			{
				ModelState.AddModelError(nameof(model.Phone),
					"رقم الهاتف مستخدم بالفعل لموظف آخر.");
				await LoadLookupsAsync();
				return View(model);
			}

			employee.Name = model.Name;
			employee.JobTitle = model.JobTitle;
			employee.Phone = model.Phone;
			employee.Email = model.Email;
			employee.Salary = model.Salary;

			employee.NationalId = model.NationalId;
			employee.HireDate = model.HireDate;
			employee.BirthDate = model.BirthDate;
			employee.Address = model.Address;
			employee.EmployeeType = model.EmployeeType;
			employee.DepartmentId = model.DepartmentId;
			employee.PositionId = model.PositionId;

			await _context.SaveChangesAsync();

			return this.RedirectWithSuccess(nameof(Index), "تم تعديل بيانات الموظف بنجاح.");
		}

		// =========================================
		// تفعيل / تعطيل الموظف
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("employee.activate")]
		public Task<IActionResult> ToggleActive(int id)
			=> ToggleActiveAsync(id, _context.Employees, "الموظف");

		// =========================================
		// طلب حساب بوابة الموظف
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("employee.portal.manage")]
		public async Task<IActionResult> RequestPortal(int id)
		{
			var employee = await _context.Employees.FirstOrDefaultAsync(x => x.Id == id);
			if (employee == null) return NotFound();

			if (!employee.IsActive)
				return this.RedirectWithError(nameof(Index),
					"لا يمكن إنشاء حساب لموظف غير نشط.");

			if (string.IsNullOrWhiteSpace(employee.Phone))
				return this.RedirectWithError(nameof(Index),
					"يجب تسجيل رقم هاتف الموظف أولاً.");

			if (employee.PortalRequested)
				return this.RedirectWithError(nameof(Index),
					"تم طلب حساب دخول لهذا الموظف بالفعل.");

			if (await _context.Users.AnyAsync(x => x.Phone == employee.Phone))
				return this.RedirectWithError(nameof(Index),
					"رقم هاتف الموظف مستخدم بالفعل في حساب مستخدم آخر.");

			employee.PortalRequested = true;
			employee.PortalApproved = false;
			employee.HasPortalAccount = false;

			await _context.SaveChangesAsync();

			return this.RedirectWithSuccess(nameof(Index), "تم إرسال طلب حساب الدخول.");
		}

		// =========================================
		// قائمة طلبات حسابات الموظفين
		// =========================================

		[HttpGet]
		[RequirePermission("employee.portal.manage")]
		public async Task<IActionResult> PortalRequests()
		{
			var employees = await _context.Employees
				.AsNoTracking()
				.Where(x => x.PortalRequested)
				.OrderBy(x => x.PortalApproved).ThenBy(x => x.Name)
				.ToListAsync();

			ViewBag.AvailableRoles = await _context.Roles
				.AsNoTracking()
				.Where(x => x.IsActive && x.Code != "Admin")
				.OrderBy(x => x.Name)
				.ToListAsync();

			return View(employees);
		}

		// =========================================
		// موافقة الإدارة
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("employee.portal.manage")]
		public async Task<IActionResult> ApprovePortal(int id)
		{
			var employee = await _context.Employees.FirstOrDefaultAsync(x => x.Id == id);
			if (employee == null) return NotFound();

			if (!employee.PortalRequested)
				return this.RedirectWithError(nameof(PortalRequests),
					"هذا الموظف لم يطلب حساب دخول.");

			if (employee.PortalApproved)
				return this.RedirectWithError(nameof(PortalRequests),
					"تمت الموافقة على هذا الطلب من قبل.");

			employee.PortalApproved = true;
			await _context.SaveChangesAsync();

			return this.RedirectWithSuccess(nameof(PortalRequests),
				"تمت الموافقة على حساب الموظف.");
		}

		// =========================================
		// إنشاء حساب Portal + كود + QR
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("employee.portal.manage")]
		public async Task<IActionResult> CreatePortalAccount(int id, int roleId)
		{
			var employee = await _context.Employees.FirstOrDefaultAsync(x => x.Id == id);
			if (employee == null) return NotFound();

			if (!employee.PortalRequested)
				return this.RedirectWithError(nameof(PortalRequests),
					"الموظف لم يطلب إنشاء حساب.");

			if (!employee.PortalApproved)
				return this.RedirectWithError(nameof(PortalRequests),
					"لا يمكن إنشاء الحساب قبل موافقة الإدارة.");

			if (employee.HasPortalAccount)
				return this.RedirectWithError(nameof(PortalRequests),
					"حساب البوابة موجود بالفعل.");

			var phone = employee.Phone!.Trim();

			if (await _context.Users.AnyAsync(x => x.Phone == phone))
				return this.RedirectWithError(nameof(PortalRequests),
					"رقم هاتف الموظف مرتبط بالفعل بحساب مستخدم.");

			var activationCode = RandomNumberGenerator
				.GetInt32(100000, 1000000).ToString();

			var role = await _context.Roles.FirstOrDefaultAsync(x =>
				x.Id == roleId && x.IsActive && x.Code != "Admin");

			if (role == null)
				return this.RedirectWithError(nameof(PortalRequests),
					"يجب اختيار دور صالح للموظف.");

			var user = new User
			{
				Phone = phone,
				PasswordHash = null,
				UserType = UserType.Employee,
				Status = UserStatus.Approved,
				IsActive = true,
				IsPasswordSet = false,
				EmployeeId = employee.Id,
				RoleId = role.Id,
				CreatedAt = DateTime.UtcNow,
				ActivationCodeExpiresAt = DateTime.UtcNow.AddHours(24)
			};

			user.ActivationCodeHash = _passwordHasher.HashPassword(user, activationCode);

			_context.Users.Add(user);
			employee.HasPortalAccount = true;
			employee.PortalApproved = true;

			await _context.SaveChangesAsync();

			var whatsappSent = await _whatsAppService.SendActivationCodeAsync(
				employee.Phone!, employee.Name, activationCode);

			TempData[whatsappSent ? "Success" : "Error"] = whatsappSent
				? $"تم إنشاء حساب الموظف {employee.Name} وتجهيز رسالة التفعيل عبر WhatsApp."
				: "تم إنشاء حساب الموظف، لكن تعذر إرسال كود التفعيل.";

			var activationUrl =
				$"{Request.Scheme}://{Request.Host}/Activation/Account?phone={phone}";

			TempData["ActivationPhone"] = phone;
			TempData["ActivationCode"] = activationCode;
			TempData["ActivationUrl"] = activationUrl;
			TempData["ActivationQr"] = _qrCodeService.GenerateQrCode(activationUrl);

			return RedirectToAction(nameof(PortalRequests));
		}
	}
}