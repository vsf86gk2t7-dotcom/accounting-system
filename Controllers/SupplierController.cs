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
	public class SupplierController : BaseController
	{
		private readonly PasswordHasher<User> _passwordHasher;
		private readonly IWhatsAppService _whatsAppService;
		private readonly IQrCodeService _qrCodeService;

		public SupplierController(
			ApplicationDbContext context,
			IWhatsAppService whatsAppService,
			IQrCodeService qrCodeService) : base(context)
		{
			_passwordHasher = new PasswordHasher<User>();
			_whatsAppService = whatsAppService;
			_qrCodeService = qrCodeService;
		}

		private async Task LoadCompaniesAsync()
			=> ViewData["Companies"] = await _context.Companies.AsNoTracking()
				.OrderBy(x => x.Name).ToListAsync();

		private void NormalizeSupplier(Supplier model)
		{
			model.Name = model.Name.TrimOrEmpty();
			model.Phone = model.Phone.TrimOrNull();
			model.Email = model.Email.TrimOrNull();
			model.Address = model.Address.TrimOrNull();
		}

		[HttpGet, RequirePermission("supplier.view")]
		public async Task<IActionResult> Index()
			=> View(await _context.Suppliers.AsNoTracking()
				.OrderBy(x => x.Name).ToListAsync());

		[HttpGet, RequirePermission("supplier.create")]
		public async Task<IActionResult> Create()
		{
			await LoadCompaniesAsync();
			return View(new Supplier());
		}

		[HttpPost, ValidateAntiForgeryToken, RequirePermission("supplier.create")]
		public async Task<IActionResult> Create(Supplier model)
		{
			NormalizeSupplier(model);

			if (!ModelState.IsValid) return View(model);

			if (await _context.Suppliers.AnyAsync(x => x.Name == model.Name))
			{
				ModelState.AddModelError(nameof(model.Name), "اسم المورد مستخدم بالفعل.");
				return View(model);
			}

			if (model.Phone != null)
			{
				if (await _context.Suppliers.AnyAsync(x => x.Phone == model.Phone))
				{
					ModelState.AddModelError(nameof(model.Phone), "رقم الهاتف مستخدم بالفعل لمورد آخر.");
					return View(model);
				}
				if (await _context.Users.AnyAsync(x => x.Phone == model.Phone))
				{
					ModelState.AddModelError(nameof(model.Phone), "رقم الهاتف مستخدم بالفعل في حساب مستخدم آخر.");
					return View(model);
				}
			}

			_context.Suppliers.Add(new Supplier
			{
				Name = model.Name,
				Phone = model.Phone,
				Email = model.Email,
				Address = model.Address,
				OpeningBalance = model.OpeningBalance,
				CreditDays = model.CreditDays,
				CompanyId = model.CompanyId,
				IsActive = true,
				CreatedAt = DateTime.UtcNow
			});
			await _context.SaveChangesAsync();

			return this.RedirectWithSuccess(nameof(Index), "تم إنشاء المورد بنجاح.");
		}

		[HttpGet, RequirePermission("supplier.edit")]
		public async Task<IActionResult> Edit(int id)
		{
			var supplier = await _context.Suppliers.FirstOrDefaultAsync(x => x.Id == id);
			if (supplier == null) return NotFound();
			await LoadCompaniesAsync();
			return View(supplier);
		}

		[HttpPost, ValidateAntiForgeryToken, RequirePermission("supplier.edit")]
		public async Task<IActionResult> Edit(int id, Supplier model)
		{
			if (id != model.Id) return BadRequest();

			NormalizeSupplier(model);

			if (!ModelState.IsValid) return View(model);

			var supplier = await _context.Suppliers.FirstOrDefaultAsync(x => x.Id == id);
			if (supplier == null) return NotFound();

			if (await _context.Suppliers.AnyAsync(x => x.Name == model.Name && x.Id != id))
			{
				ModelState.AddModelError(nameof(model.Name), "اسم المورد مستخدم بالفعل.");
				return View(model);
			}

			if (model.Phone != null)
			{
				if (await _context.Suppliers.AnyAsync(x => x.Phone == model.Phone && x.Id != id))
				{
					ModelState.AddModelError(nameof(model.Phone), "رقم الهاتف مستخدم بالفعل لمورد آخر.");
					return View(model);
				}
				if (await _context.Users.AnyAsync(x => x.Phone == model.Phone && x.SupplierId != id))
				{
					ModelState.AddModelError(nameof(model.Phone), "رقم الهاتف مستخدم بالفعل في حساب مستخدم آخر.");
					return View(model);
				}
			}

			// =========================================
			// ✅ Refactor: SetValues مع حماية الحقول الحساسة
			// =========================================

			// 1) احفظ القيم الحساسة قبل النسخ
			var protectedValues = new
			{
				supplier.PortalRequested,
				supplier.PortalApproved,
				supplier.HasPortalAccount,
				supplier.CreatedAt,
				supplier.IsActive
			};

			// 2) انسخ كل الحقول من model → supplier
			_context.Entry(supplier).CurrentValues.SetValues(model);

			// 3) رجّع القيم الحساسة
			supplier.PortalRequested = protectedValues.PortalRequested;
			supplier.PortalApproved = protectedValues.PortalApproved;
			supplier.HasPortalAccount = protectedValues.HasPortalAccount;
			supplier.CreatedAt = protectedValues.CreatedAt;
			supplier.IsActive = protectedValues.IsActive;

			await _context.SaveChangesAsync();
			return this.RedirectWithSuccess(nameof(Index), "تم تعديل بيانات المورد بنجاح.");
		}

		[HttpPost, ValidateAntiForgeryToken, RequirePermission("supplier.activate")]
		public Task<IActionResult> ToggleActive(int id)
			=> ToggleActiveAsync(id, _context.Suppliers, "المورد");

		[HttpPost, ValidateAntiForgeryToken, RequirePermission("supplier.portal.manage")]
		public async Task<IActionResult> RequestPortal(int id)
		{
			var supplier = await _context.Suppliers.FirstOrDefaultAsync(x => x.Id == id);
			if (supplier == null) return NotFound();

			if (!supplier.IsActive)
				return this.RedirectWithError(nameof(Index), "لا يمكن إنشاء حساب لمورد غير نشط.");
			if (string.IsNullOrWhiteSpace(supplier.Phone))
				return this.RedirectWithError(nameof(Index), "يجب تسجيل رقم هاتف المورد أولاً.");
			if (supplier.PortalRequested)
				return this.RedirectWithError(nameof(Index), "تم طلب حساب دخول لهذا المورد بالفعل.");

			if (await _context.Users.AnyAsync(x => x.Phone == supplier.Phone))
				return this.RedirectWithError(nameof(Index),
					"رقم هاتف المورد مستخدم بالفعل في حساب مستخدم آخر. لا يمكن إنشاء حساب بوابة له.");

			supplier.PortalRequested = true;
			supplier.PortalApproved = false;
			supplier.HasPortalAccount = false;
			await _context.SaveChangesAsync();

			return this.RedirectWithSuccess(nameof(Index),
				"تم إرسال طلب حساب الدخول، وهو الآن في انتظار موافقة الإدارة.");
		}

		[HttpGet, RequirePermission("supplier.portal.manage")]
		public async Task<IActionResult> PortalRequests()
			=> View(await _context.Suppliers.AsNoTracking()
				.Where(x => x.PortalRequested)
				.OrderBy(x => x.PortalApproved).ThenBy(x => x.Name)
				.ToListAsync());

		[HttpPost, ValidateAntiForgeryToken, RequirePermission("supplier.portal.manage")]
		public async Task<IActionResult> ApprovePortal(int id)
		{
			var supplier = await _context.Suppliers.FirstOrDefaultAsync(x => x.Id == id);
			if (supplier == null) return NotFound();

			if (!supplier.PortalRequested)
				return this.RedirectWithError(nameof(PortalRequests), "هذا المورد لم يطلب حساب دخول.");
			if (supplier.PortalApproved)
				return this.RedirectWithError(nameof(PortalRequests), "تمت الموافقة على هذا الطلب من قبل.");

			supplier.PortalApproved = true;
			await _context.SaveChangesAsync();

			return this.RedirectWithSuccess(nameof(PortalRequests), "تمت الموافقة على حساب المورد.");
		}

		[HttpPost, ValidateAntiForgeryToken, RequirePermission("supplier.portal.manage")]
		public async Task<IActionResult> CreatePortalAccount(int id)
		{
			var supplier = await _context.Suppliers.FirstOrDefaultAsync(x => x.Id == id);
			if (supplier == null) return NotFound();

			if (!supplier.PortalRequested)
				return this.RedirectWithError(nameof(PortalRequests), "المورد لم يطلب إنشاء حساب.");
			if (!supplier.PortalApproved)
				return this.RedirectWithError(nameof(PortalRequests), "لا يمكن إنشاء الحساب قبل موافقة الإدارة.");
			if (supplier.HasPortalAccount)
				return this.RedirectWithError(nameof(PortalRequests), "حساب البوابة موجود بالفعل.");

			var phone = supplier.Phone!.Trim();

			if (await _context.Users.AnyAsync(x => x.Phone == phone))
				return this.RedirectWithError(nameof(PortalRequests),
					"رقم هاتف المورد مرتبط بالفعل بحساب مستخدم.");

			var activationCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

			var user = new User
			{
				Phone = phone,
				PasswordHash = null,
				UserType = UserType.Supplier,
				Status = UserStatus.Approved,
				IsActive = true,
				IsPasswordSet = false,
				SupplierId = supplier.Id,
				CreatedAt = DateTime.UtcNow,
				ActivationCodeExpiresAt = DateTime.UtcNow.AddHours(24)
			};
			user.ActivationCodeHash = _passwordHasher.HashPassword(user, activationCode);

			_context.Users.Add(user);
			supplier.HasPortalAccount = true;
			await _context.SaveChangesAsync();

			var whatsappSent = await _whatsAppService.SendActivationCodeAsync(
				supplier.Phone!, supplier.Name, activationCode);

			TempData[whatsappSent ? "Success" : "Error"] = whatsappSent
				? $"تم إنشاء حساب المورد {supplier.Name} وتجهيز رسالة التفعيل عبر WhatsApp."
				: "تم إنشاء حساب المورد، لكن تعذر إرسال كود التفعيل عبر WhatsApp.";

			var activationUrl = $"{Request.Scheme}://{Request.Host}/Activation/Account?phone={phone}";

			TempData["ActivationPhone"] = phone;
			TempData["ActivationCode"] = activationCode;
			TempData["ActivationUrl"] = activationUrl;
			TempData["ActivationQr"] = _qrCodeService.GenerateQrCode(activationUrl);

			return RedirectToAction(nameof(PortalRequests));
		}
	}
}