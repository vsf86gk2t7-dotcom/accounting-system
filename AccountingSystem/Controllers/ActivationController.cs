using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Controllers
{
	[AllowAnonymous]
	public class ActivationController : Controller
	{
		private readonly ApplicationDbContext _context;
		private readonly PasswordHasher<User> _passwordHasher;

		public ActivationController(
			ApplicationDbContext context)
		{
			_context = context;
			_passwordHasher = new PasswordHasher<User>();
		}

		[HttpGet]
		public IActionResult Customer()
		{
			return View();
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Customer(
			ActivateCustomerAccountViewModel model)
		{
			if (!ModelState.IsValid)
				return View(model);

			var result = await ActivateAccountAsync(
				model.Phone,
				model.ActivationCode,
				model.Password,
				UserType.Customer,
				user => user.CustomerId.HasValue && user.Customer != null);

			if (!result.Success)
			{
				ModelState.AddModelError("", result.Error!);
				return View(model);
			}

			TempData["Success"] = "تم تفعيل الحساب بنجاح. يمكنك تسجيل الدخول الآن.";
			return RedirectToAction("Login", "Account");
		}

		[HttpGet]
		public IActionResult Supplier()
		{
			return View();
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Supplier(
			ActivateSupplierAccountViewModel model)
		{
			if (!ModelState.IsValid)
				return View(model);

			var result = await ActivateAccountAsync(
				model.Phone,
				model.ActivationCode,
				model.Password,
				UserType.Supplier,
				user => user.SupplierId.HasValue && user.Supplier != null);

			if (!result.Success)
			{
				ModelState.AddModelError("", result.Error!);
				return View(model);
			}

			TempData["Success"] = "تم تفعيل الحساب بنجاح. يمكنك تسجيل الدخول الآن.";
			return RedirectToAction("Login", "Account");
		}

		[HttpGet]
		public IActionResult Account()
		{
			return View();
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Account(
			ActivateAccountViewModel model)
		{
			if (!ModelState.IsValid)
				return View(model);

			var result = await ActivateAccountAsync(
				model.Phone,
				model.ActivationCode,
				model.Password,
				null,
				user => user.UserType switch
				{
					UserType.Customer => user.CustomerId.HasValue && user.Customer != null,
					UserType.Supplier => user.SupplierId.HasValue && user.Supplier != null,
					UserType.Employee => user.EmployeeId.HasValue && user.Employee != null,
					_ => false
				});

			if (!result.Success)
			{
				ModelState.AddModelError("", result.Error!);
				return View(model);
			}

			TempData["Success"] = "تم تفعيل الحساب بنجاح. يمكنك تسجيل الدخول الآن.";
			return RedirectToAction("Login", "Account");
		}

		private async Task<(bool Success, string? Error)> ActivateAccountAsync(
			string phone,
			string activationCode,
			string password,
			UserType? userType,
			Func<User, bool> validateLinkedEntity)
		{
			var user = await _context.Users
				.Include(x => x.Customer)
				.Include(x => x.Supplier)
				.Include(x => x.Employee)
				.FirstOrDefaultAsync(x =>
					x.Phone == phone &&
					(userType == null || x.UserType == userType));

			if (user == null)
				return (false, "رقم الهاتف أو كود التفعيل غير صحيح.");

			if (!validateLinkedEntity(user))
				return (false, "بيانات الحساب غير مكتملة.");

			if (user.Status != UserStatus.Approved || !user.IsActive)
				return (false, "هذا الحساب غير مفعّل بعد.");

			if (user.IsPasswordSet || !string.IsNullOrWhiteSpace(user.PasswordHash))
				return (false, "تم تعيين كلمة المرور مسبقًا.");

			if (string.IsNullOrWhiteSpace(user.ActivationCodeHash) ||
				!user.ActivationCodeExpiresAt.HasValue)
				return (false, "لا يوجد كود تفعيل صالح.");

			if (DateTime.UtcNow > user.ActivationCodeExpiresAt.Value)
				return (false, "انتهت صلاحية كود التفعيل. اطلب كودًا جديدًا.");

			var codeResult = _passwordHasher.VerifyHashedPassword(
				user,
				user.ActivationCodeHash,
				activationCode);

			if (codeResult == PasswordVerificationResult.Failed)
				return (false, "كود التفعيل غير صحيح.");

			user.PasswordHash = _passwordHasher.HashPassword(user, password);
			user.IsPasswordSet = true;
			user.ActivationCodeHash = null;
			user.ActivationCodeExpiresAt = null;

			await _context.SaveChangesAsync();

			return (true, null);
		}
	}
}
