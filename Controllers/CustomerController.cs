using AccountingSystem.Data;
using AccountingSystem.Filters;
using AccountingSystem.Models;
using AccountingSystem.Services;
using AccountingSystem.Services.QrCode;
using AccountingSystem.Services.WhatsApp;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace AccountingSystem.Controllers
{
	public class CustomerController : BaseController
	{
		private readonly PasswordHasher<User> _passwordHasher;
		private readonly IWhatsAppService _whatsAppService;
		private readonly IQrCodeService _qrCodeService;
		private readonly ISalesRepService _salesRepService;
		private readonly INotificationService _notifications;

		public CustomerController(
			ApplicationDbContext context,
			IWhatsAppService whatsAppService,
			IQrCodeService qrCodeService,
			ISalesRepService salesRepService,
			INotificationService notifications) : base(context)
		{
			_passwordHasher = new PasswordHasher<User>();
			_whatsAppService = whatsAppService;
			_qrCodeService = qrCodeService;
			_salesRepService = salesRepService;
			_notifications = notifications;
		}

		[HttpGet, RequirePermission("customer.view")]
		public async Task<IActionResult> Index(string? search)
		{
			var query = _context.Customers
				.AsNoTracking()
				.Include(x => x.SalesRep)
					.ThenInclude(r => r!.Employee)
				.AsQueryable();

			// ✅ المندوب يشوف عملاءه فقط
			var repId = await _salesRepService.GetCurrentRepIdAsync();

			if (repId.HasValue)
			{
				query = query.Where(x => x.SalesRepId == repId.Value);
			}

			if (!string.IsNullOrWhiteSpace(search))
			{
				search = search.Trim();
				query = query.Where(x =>
					x.Name.Contains(search) ||
					x.Phone.Contains(search) ||
					(x.Email != null && x.Email.Contains(search)));
			}

			ViewBag.Search = search;
			ViewBag.IsRepView = repId.HasValue;

			return View(await query.OrderBy(x => x.Name).ToListAsync());
		}

		// =========================================
		// Helper: تحميل الشركات
		// =========================================

		private async Task LoadCompaniesAsync(int? selected = null)
		{
			ViewData["Companies"] = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
				await _context.Companies.AsNoTracking().OrderBy(x => x.Name).ToListAsync(),
				"Id", "Name", selected);
		}

		// =========================================
		// Helper: تحميل المناديب
		// =========================================

		private async Task LoadRepsAsync(int? selected = null)
		{
			ViewData["Reps"] = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
				await _context.SalesRepProfiles
					.AsNoTracking()
					.Include(p => p.Employee)
					.Where(p => p.IsActive && p.Employee != null)
					.OrderBy(p => p.Employee!.Name)
					.Select(p => new
					{
						Id = p.Id,
						Name = p.Employee!.Name
					})
					.ToListAsync(),
				"Id", "Name", selected);
		}

		// =========================================
		// Create - GET
		// =========================================

		[HttpGet, RequirePermission("customer.create")]
		public async Task<IActionResult> Create()
		{
			// ✅ المندوب: يثبّت العميل على نفسه تلقائياً
			var repId = await _salesRepService.GetCurrentRepIdAsync();

			await LoadCompaniesAsync();
			await LoadRepsAsync(repId);

			return View(new Customer
			{
				SalesRepId = repId,
				IsActive = true,
				CreditDays = 0
			});
		}

		// =========================================
		// Create - POST
		// =========================================

		[HttpPost, ValidateAntiForgeryToken, RequirePermission("customer.create")]
		public async Task<IActionResult> Create(Customer customer)
		{
			await LoadCompaniesAsync(customer.CompanyId);
			await LoadRepsAsync(customer.SalesRepId);

			if (!ModelState.IsValid) return View(customer);

			customer.Phone = customer.Phone.Trim();

			if (await _context.Customers.AnyAsync(x => x.Phone == customer.Phone))
			{
				ModelState.AddModelError("Phone", "رقم الهاتف مستخدم بالفعل لعميل آخر.");
				return View(customer);
			}

			// ✅ المندوب: يجبر العميل على نفسه (لو حاول يغيّره)
			var repId = await _salesRepService.GetCurrentRepIdAsync();
			if (repId.HasValue)
			{
				customer.SalesRepId = repId.Value;
			}

			customer.CreatedAt = DateTime.UtcNow;
			customer.IsActive = true;
			customer.PortalRequested = false;
			customer.PortalApproved = false;
			customer.HasPortalAccount = false;

			_context.Customers.Add(customer);
			await _context.SaveChangesAsync();

			// ⬇️⬇️⬇️ إشعار: تم إضافة عميل جديد ⬇️⬇️⬇️
			var userId = _notifications.CurrentUserId(User);

			if (userId > 0)
			{
				await _notifications.CreateAsync(
					userId,
					"تم إضافة عميل جديد",
					$"العميل: {customer.Name} — {customer.Phone}",
					NotificationLevel.Normal,
					Url.Action(nameof(Edit), new { id = customer.Id }),
					"bi-person-plus");
			}
			// ⬆️⬆️⬆️ نهاية الإشعار ⬆️⬆️⬆️

			return this.RedirectWithSuccess(nameof(Index), "تم إضافة العميل بنجاح.");
		}

		// =========================================
		// Edit - GET
		// =========================================

		[HttpGet, RequirePermission("customer.edit")]
		public async Task<IActionResult> Edit(int id)
		{
			var customer = await _context.Customers.FindAsync(id);
			if (customer == null) return NotFound();

			// ✅ المندوب: ما يفتحش إلا عملاءه
			var repId = await _salesRepService.GetCurrentRepIdAsync();
			if (repId.HasValue && customer.SalesRepId != repId.Value)
			{
				return RedirectToAction("AccessDenied", "Account");
			}

			await LoadCompaniesAsync(customer.CompanyId);
			await LoadRepsAsync(customer.SalesRepId);

			return View(customer);
		}

		// =========================================
		// Edit - POST
		// =========================================

		[HttpPost, ValidateAntiForgeryToken, RequirePermission("customer.edit")]
		public async Task<IActionResult> Edit(int id, Customer model)
		{
			if (id != model.Id) return BadRequest();

			await LoadCompaniesAsync(model.CompanyId);
			await LoadRepsAsync(model.SalesRepId);

			if (!ModelState.IsValid)
			{
				return View(model);
			}

			model.Phone = model.Phone.Trim();

			if (await _context.Customers.AnyAsync(x => x.Phone == model.Phone && x.Id != id))
			{
				ModelState.AddModelError("Phone", "رقم الهاتف مستخدم بالفعل لعميل آخر.");
				return View(model);
			}

			var customer = await _context.Customers.FindAsync(id);
			if (customer == null) return NotFound();

			// ✅ المندوب: ما يعدّلش إلا عملاءه
			var repId = await _salesRepService.GetCurrentRepIdAsync();
			if (repId.HasValue && customer.SalesRepId != repId.Value)
			{
				return RedirectToAction("AccessDenied", "Account");
			}

			// =========================================
			// ✅ Refactor: SetValues مع حماية الحقول الحساسة
			// =========================================

			// 1) احفظ القيم الحساسة قبل النسخ
			var protectedValues = new
			{
				customer.PortalRequested,
				customer.PortalApproved,
				customer.HasPortalAccount,
				customer.CreatedAt,
				customer.IsActive,
				customer.SalesRepId      // ⬅️ له منطق خاص (المندوب)
			};

			// 2) انسخ كل الحقول من model → customer
			_context.Entry(customer).CurrentValues.SetValues(model);

			// 3) رجّع القيم الحساسة
			customer.PortalRequested = protectedValues.PortalRequested;
			customer.PortalApproved = protectedValues.PortalApproved;
			customer.HasPortalAccount = protectedValues.HasPortalAccount;
			customer.CreatedAt = protectedValues.CreatedAt;
			customer.IsActive = protectedValues.IsActive;

			// 4) SalesRepId: احترم قاعدة المندوب
			customer.SalesRepId = repId.HasValue
				? protectedValues.SalesRepId         // المندوب: احتفظ بالقيمة القديمة
				: model.SalesRepId;                  // الأدمن: استخدم الجديدة

			await _context.SaveChangesAsync();

			// ⬇️⬇️⬇️ إشعار: تم تعديل بيانات عميل ⬇️⬇️⬇️
			var userId = _notifications.CurrentUserId(User);

			if (userId > 0)
			{
				await _notifications.CreateAsync(
					userId,
					"تم تعديل بيانات عميل",
					$"العميل: {customer.Name}",
					NotificationLevel.Normal,
					Url.Action(nameof(Edit), new { id = customer.Id }),
					"bi-pencil-square");
			}
			// ⬆️⬆️⬆️ نهاية الإشعار ⬆️⬆️⬆️

			return this.RedirectWithSuccess(nameof(Index), "تم تعديل بيانات العميل بنجاح.");
		}

		// =========================================
		// Toggle Active
		// =========================================

		[HttpPost, ValidateAntiForgeryToken, RequirePermission("customer.activate")]
		public Task<IActionResult> ToggleActive(int id)
			=> ToggleActiveAsync(id, _context.Customers, "العميل");

		// =========================================
		// Request Portal
		// =========================================

		[HttpPost, ValidateAntiForgeryToken, RequirePermission("customer.portal.manage")]
		public async Task<IActionResult> RequestPortal(int id)
		{
			var customer = await _context.Customers.FirstOrDefaultAsync(x => x.Id == id);
			if (customer == null) return NotFound();

			if (!customer.IsActive)
				return this.RedirectWithError(nameof(Index), "لا يمكن إنشاء حساب لعميل غير نشط.");

			if (string.IsNullOrWhiteSpace(customer.Phone))
				return this.RedirectWithError(nameof(Index), "يجب تسجيل رقم هاتف العميل أولاً.");

			if (customer.PortalRequested)
				return this.RedirectWithError(nameof(Index), "تم طلب حساب دخول لهذا العميل بالفعل.");

			customer.PortalRequested = true;
			customer.PortalApproved = false;
			customer.HasPortalAccount = false;

			await _context.SaveChangesAsync();

			// ⬇️⬇️⬇️ إشعار: طلب حساب بوابة (متوسط) ⬇️⬇️⬇️
			var userId = _notifications.CurrentUserId(User);

			if (userId > 0)
			{
				await _notifications.CreateAsync(
					userId,
					"طلب حساب بوابة جديد",
					$"العميل {customer.Name} — {customer.Phone}",
					NotificationLevel.Medium,
					Url.Action(nameof(PortalRequests)),
					"bi-hourglass-split");
			}
			// ⬆️⬆️⬆️ نهاية الإشعار ⬆️⬆️⬆️

			return this.RedirectWithSuccess(nameof(Index),
				"تم إرسال طلب حساب الدخول، وهو الآن في انتظار موافقة الإدارة.");
		}

		// =========================================
		// Portal Requests
		// =========================================

		[HttpGet]
		public async Task<IActionResult> PortalRequests()
			=> View(await _context.Customers.AsNoTracking()
				.Where(x => x.PortalRequested)
				.OrderBy(x => x.PortalApproved).ThenBy(x => x.Name)
				.ToListAsync());

		// =========================================
		// Approve Portal
		// =========================================

		[HttpPost, ValidateAntiForgeryToken]
		public async Task<IActionResult> ApprovePortal(int id)
		{
			var customer = await _context.Customers.FirstOrDefaultAsync(x => x.Id == id);
			if (customer == null) return NotFound();

			if (!customer.PortalRequested)
				return this.RedirectWithError(nameof(PortalRequests), "هذا العميل لم يطلب حساب دخول.");

			if (customer.PortalApproved)
				return this.RedirectWithError(nameof(PortalRequests), "تمت الموافقة على هذا الطلب من قبل.");

			customer.PortalApproved = true;
			await _context.SaveChangesAsync();

			return this.RedirectWithSuccess(nameof(PortalRequests), "تمت الموافقة على حساب العميل.");
		}

		// =========================================
		// Create Portal Account
		// =========================================

		[HttpPost, ValidateAntiForgeryToken]
		public async Task<IActionResult> CreatePortalAccount(int id)
		{
			var customer = await _context.Customers.FirstOrDefaultAsync(x => x.Id == id);
			if (customer == null) return NotFound();

			if (!customer.PortalRequested)
				return this.RedirectWithError(nameof(PortalRequests), "العميل لم يطلب إنشاء حساب.");

			if (!customer.PortalApproved)
				return this.RedirectWithError(nameof(PortalRequests), "لا يمكن إنشاء الحساب قبل موافقة الإدارة.");

			if (customer.HasPortalAccount)
				return this.RedirectWithError(nameof(PortalRequests), "حساب البوابة موجود بالفعل.");

			var phone = customer.Phone.Trim();

			if (await _context.Users.AnyAsync(x => x.Phone == phone))
				return this.RedirectWithError(nameof(PortalRequests),
					"رقم هاتف العميل مرتبط بالفعل بحساب مستخدم.");

			var activationCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

			var user = new User
			{
				Phone = phone,
				PasswordHash = null,
				UserType = UserType.Customer,
				Status = UserStatus.Approved,
				IsActive = true,
				IsPasswordSet = false,
				CustomerId = customer.Id,
				CreatedAt = DateTime.UtcNow,
				ActivationCodeExpiresAt = DateTime.UtcNow.AddHours(24)
			};
			user.ActivationCodeHash = _passwordHasher.HashPassword(user, activationCode);

			_context.Users.Add(user);
			customer.HasPortalAccount = true;
			await _context.SaveChangesAsync();

			var whatsappSent = await _whatsAppService.SendActivationCodeAsync(
				customer.Phone, customer.Name, activationCode);

			TempData[whatsappSent ? "Success" : "Error"] = whatsappSent
				? $"تم إنشاء حساب العميل {customer.Name} وتجهيز رسالة التفعيل عبر WhatsApp."
				: "تم إنشاء حساب العميل، لكن تعذر إرسال كود التفعيل عبر WhatsApp.";

			var activationUrl = $"{Request.Scheme}://{Request.Host}/Activation/Account?phone={phone}";

			TempData["ActivationPhone"] = phone;
			TempData["ActivationCode"] = activationCode;
			TempData["ActivationUrl"] = activationUrl;
			TempData["ActivationQr"] = _qrCodeService.GenerateQrCode(activationUrl);

			return RedirectToAction(nameof(PortalRequests));
		}
	}
}