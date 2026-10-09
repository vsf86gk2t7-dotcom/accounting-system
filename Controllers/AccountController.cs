using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Services.Jwt;
using AccountingSystem.Services.WhatsApp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AccountingSystem.Controllers
{
	public class AccountController : Controller
	{
		private const string AccessTokenCookie =
			"AccountingSystem.AccessToken";

		private const string RefreshTokenCookie =
			"AccountingSystem.RefreshToken";

		private readonly ApplicationDbContext _context;
		private readonly PasswordHasher<User> _passwordHasher;
		private readonly IJwtTokenService _jwtTokenService;
		private readonly IWhatsAppService _whatsAppService;
		private readonly ILogger<AccountController> _logger;

		public AccountController(
			ApplicationDbContext context,
			IJwtTokenService jwtTokenService,
			IWhatsAppService whatsAppService,
			ILogger<AccountController> logger)
		{
			_context = context;
			_passwordHasher = new PasswordHasher<User>();
			_jwtTokenService = jwtTokenService;
			_whatsAppService = whatsAppService;
			_logger = logger;
		}

		private CookieOptions AccessTokenCookieOptions(
			DateTimeOffset? expires)
		{
			var isProduction =
				!HttpContext.RequestServices
					.GetRequiredService<
						Microsoft.AspNetCore.Hosting.IWebHostEnvironment>()
					.IsDevelopment();

			return new CookieOptions
			{
				HttpOnly = true,
				Secure = Request.IsHttps || isProduction,
				SameSite = SameSiteMode.Strict,
				Expires = expires,
				IsEssential = true,
				Path = "/"
			};
		}

		private CookieOptions RefreshTokenCookieOptions(
			DateTimeOffset? expires)
		{
			var isProduction =
				!HttpContext.RequestServices
					.GetRequiredService<
						Microsoft.AspNetCore.Hosting.IWebHostEnvironment>()
					.IsDevelopment();

			return new CookieOptions
			{
				HttpOnly = true,
				Secure = Request.IsHttps || isProduction,
				SameSite = SameSiteMode.Strict,
				Expires = expires,
				IsEssential = true,
				Path = "/"
			};
		}

		// =========================================
		// Login
		// =========================================

		[HttpGet]
		[AllowAnonymous]
		public IActionResult Login()
		{
			return View();
		}

		[HttpPost]
		[AllowAnonymous]
		[ValidateAntiForgeryToken]
		[EnableRateLimiting("LoginPolicy")]
		public async Task<IActionResult> Login(
			string username,
			string password)
		{
			if (string.IsNullOrWhiteSpace(username) ||
				string.IsNullOrWhiteSpace(password))
			{
				ViewBag.Error =
					"من فضلك أدخل رقم الهاتف وكلمة المرور.";

				return View();
			}

			var phone = username.Trim();

			var user = await _context.Users
				.FirstOrDefaultAsync(x => x.Phone == phone);

			if (user == null)
			{
				ViewBag.Error =
					"رقم الهاتف أو كلمة المرور غير صحيحة.";

				return View();
			}

			if (user.LockoutUntil.HasValue &&
				user.LockoutUntil.Value > DateTime.UtcNow)
			{
				var remaining =
					(int)(user.LockoutUntil.Value -
						DateTime.UtcNow).TotalMinutes;
				ViewBag.Error =
					$"تم قفل الحساب مؤقتًا. حاول بعد {remaining} دقيقة.";

				return View();
			}

			if (!user.IsActive)
			{
				ViewBag.Error =
					"هذا الحساب غير نشط.";

				return View();
			}

			if (user.Status != UserStatus.Approved)
			{
				ViewBag.Error = "هذا الحساب غير مفعّل بعد.";

				return View();
			}

			if (!user.IsPasswordSet ||
				string.IsNullOrWhiteSpace(user.PasswordHash))
			{
				if (user.UserType == UserType.Customer)
				{
					ViewBag.Error =
						"تمت الموافقة على حسابك، " +
						"لكن يجب تفعيل الحساب وتعيين كلمة المرور أولاً.";

					ViewBag.ActivationPhone =
						user.Phone;

					return View();
				}

				ViewBag.Error =
					"هذا الحساب غير جاهز لتسجيل الدخول.";

				return View();
			}

			var passwordResult =
				_passwordHasher.VerifyHashedPassword(
					user,
					user.PasswordHash,
					password);

			if (passwordResult ==
				PasswordVerificationResult.Failed)
			{
				user.FailedLoginAttempts++;

				if (user.FailedLoginAttempts >= AppConstants.MaxFailedLoginAttempts)
				{
					user.LockoutUntil = DateTime.UtcNow.AddMinutes(AppConstants.LockoutDurationMinutes);
				}

				_context.Users.Update(user);
				await _context.SaveChangesAsync();

				_logger.LogWarning(
					"Failed login attempt for phone {Phone}. Attempt {Attempt}",
					phone, user.FailedLoginAttempts);

				ViewBag.Error =
					"رقم الهاتف أو كلمة المرور غير صحيحة.";

				return View();
			}

			// ✅ SuccessRehashNeeded: نُعيد التشفير تلقائيًا
			if (passwordResult ==
				PasswordVerificationResult.SuccessRehashNeeded)
			{
				user.PasswordHash =
					_passwordHasher.HashPassword(user, password);
			}

			if (user.FailedLoginAttempts > 0 ||
				user.LockoutUntil.HasValue)
			{
				user.FailedLoginAttempts = 0;
				user.LockoutUntil = null;
				user.SecurityStamp = Guid.NewGuid().ToString();
			}

			await _context.SaveChangesAsync();

			_logger.LogInformation(
				"User {Phone} logged in successfully", user.Phone);

			// =========================================
			// إنشاء JWT
			// =========================================

			var token =
				_jwtTokenService.CreateToken(user);

			var refreshToken =
				_jwtTokenService.CreateRefreshToken(user);

			Response.Cookies.Append(
				AccessTokenCookie,
				token,
				AccessTokenCookieOptions(
					DateTimeOffset.UtcNow.Add(
						_jwtTokenService.GetAccessTokenLifetime())));

			Response.Cookies.Append(
				RefreshTokenCookie,
				refreshToken,
				RefreshTokenCookieOptions(
					DateTimeOffset.UtcNow.Add(
						_jwtTokenService.GetRefreshTokenLifetime())));

			// =========================================
			// التوجيه حسب نوع المستخدم
			// =========================================

			switch (user.UserType)
			{
				case UserType.Admin:

					return RedirectToAction(
						"Index",
						"Home");

				case UserType.Employee:

					return RedirectToAction(
						"Index",
						"Home");

				case UserType.Customer:

					return RedirectToAction(
						"Index",
						"CustomerPortal");

				case UserType.Supplier:

					return RedirectToAction(
						"Index",
						"SupplierPortal");

				default:

					Response.Cookies.Delete(
						AccessTokenCookie);

					ViewBag.Error =
						"نوع المستخدم غير معروف.";

					return View();
			}
		}

		// =========================================
		// Refresh Token — ✅ مُصحَّح بالكامل
		// =========================================

		[HttpPost]
		[AllowAnonymous]
		[ValidateAntiForgeryToken]
		public IActionResult Refresh()
		{
			if (!Request.Cookies.TryGetValue(
					RefreshTokenCookie,
					out var refreshToken) ||
				string.IsNullOrWhiteSpace(refreshToken))
			{
				return Unauthorized();
			}

			// ✅ استخرج الـ UserId من الـ Refresh Token نفسه
			// (لأن Access Token منتهي الصلاحية → User غير مُصادق عليه)
			if (!_jwtTokenService.TryGetUserIdFromRefreshToken(
					refreshToken,
					out var userIdInt))
			{
				return Unauthorized();
			}

			var user = _context.Users
				.AsNoTracking()
				.FirstOrDefault(x => x.Id == userIdInt);

			if (user == null || !user.IsActive)
			{
				return Unauthorized();
			}

			if (!_jwtTokenService.ValidateRefreshToken(
					user,
					refreshToken))
			{
				return Unauthorized();
			}

			var newToken = _jwtTokenService.CreateToken(user);
			var newRefreshToken = _jwtTokenService.CreateRefreshToken(user);

			Response.Cookies.Append(
				AccessTokenCookie,
				newToken,
				AccessTokenCookieOptions(
					DateTimeOffset.UtcNow.Add(
						_jwtTokenService.GetAccessTokenLifetime())));

			Response.Cookies.Append(
				RefreshTokenCookie,
				newRefreshToken,
				RefreshTokenCookieOptions(
					DateTimeOffset.UtcNow.Add(
						_jwtTokenService.GetRefreshTokenLifetime())));

			return Ok(new { success = true });
		}

		// =========================================
		// Logout
		// =========================================

		[HttpPost]
		[AllowAnonymous]
		[ValidateAntiForgeryToken]
		public IActionResult Logout()
		{
			Response.Cookies.Delete(
				AccessTokenCookie,
				AccessTokenCookieOptions(null));

			Response.Cookies.Delete(
				RefreshTokenCookie,
				RefreshTokenCookieOptions(null));

			return RedirectToAction(
				nameof(Login));
		}

		// =========================================
		// Access Denied
		// =========================================

		[HttpGet]
		[AllowAnonymous]
		public IActionResult AccessDenied()
		{
			return View();
		}

		// =========================================
		// معلومات المستخدم الحالي
		// =========================================

		[HttpGet]
		public IActionResult Me()
		{
			if (User.Identity?.IsAuthenticated != true)
			{
				return Unauthorized();
			}

			return Ok(new
			{
				UserId = User.FindFirstValue(
					ClaimTypes.NameIdentifier),

				Phone = User.FindFirstValue(
					ClaimTypes.MobilePhone),

				Role = User.FindFirstValue(
					ClaimTypes.Role)
			});
		}

		// =========================================
		// صفحة التسجيل
		// =========================================

		[HttpGet]
		[AllowAnonymous]
		public IActionResult Register()
		{
			if (User.Identity?.IsAuthenticated == true)
			{
				return RedirectToAction("Index", "Home");
			}

			return View(new Models.ViewModels.RegisterViewModel());
		}

		// =========================================
		// تنفيذ التسجيل
		// =========================================

		[HttpPost]
		[AllowAnonymous]
		[ValidateAntiForgeryToken]
		[EnableRateLimiting("ForgotPasswordPolicy")]
		public async Task<IActionResult> Register(
			Models.ViewModels.RegisterViewModel model)
		{
			if (!ModelState.IsValid)
			{
				return View(model);
			}

			var phone = model.Phone.Trim();
			var name = model.Name.Trim();
			var email = string.IsNullOrWhiteSpace(model.Email)
				? null
				: model.Email.Trim();
			var address = string.IsNullOrWhiteSpace(model.Address)
				? null
				: model.Address.Trim();

			var phoneInUsers =
				await _context.Users
					.AnyAsync(x => x.Phone == phone);

			if (phoneInUsers)
			{
				ModelState.AddModelError(
					nameof(model.Phone),
					"رقم الهاتف مستخدم بالفعل في حساب آخر.");

				return View(model);
			}

			switch (model.AccountType)
			{
				case Models.ViewModels.RegisterType.Customer:

					var customerPhoneExists =
						await _context.Customers
							.AnyAsync(x => x.Phone == phone);

					if (customerPhoneExists)
					{
						ModelState.AddModelError(
							nameof(model.Phone),
							"رقم الهاتف مستخدم بالفعل لعميل آخر.");

						return View(model);
					}

					_context.Customers.Add(new Customer
					{
						Name = name,
						Phone = phone,
						Email = email,
						Address = address,
						IsActive = true,
						PortalRequested = true,
						PortalApproved = false,
						HasPortalAccount = false,
						CreatedAt = DateTime.UtcNow
					});

					break;

				case Models.ViewModels.RegisterType.Supplier:

					var supplierPhoneExists =
						await _context.Suppliers
							.AnyAsync(x => x.Phone == phone);

					if (supplierPhoneExists)
					{
						ModelState.AddModelError(
							nameof(model.Phone),
							"رقم الهاتف مستخدم بالفعل لمورد آخر.");

						return View(model);
					}

					_context.Suppliers.Add(new Supplier
					{
						Name = name,
						Phone = phone,
						Email = email,
						Address = address,
						IsActive = true,
						PortalRequested = true,
						PortalApproved = false,
						HasPortalAccount = false,
						CreatedAt = DateTime.UtcNow
					});

					break;

				case Models.ViewModels.RegisterType.Employee:

					var employeePhoneExists =
						await _context.Employees
							.AnyAsync(x => x.Phone == phone);

					if (employeePhoneExists)
					{
						ModelState.AddModelError(
							nameof(model.Phone),
							"رقم الهاتف مستخدم بالفعل لموظف آخر.");

						return View(model);
					}

					_context.Employees.Add(new Employee
					{
						Name = name,
						Phone = phone,
						Email = email,
						IsActive = true,
						PortalRequested = true,
						PortalApproved = false,
						HasPortalAccount = false,
						CreatedAt = DateTime.UtcNow
					});

					break;

				default:

					ModelState.AddModelError(
						nameof(model.AccountType),
						"نوع الحساب غير صحيح.");

					return View(model);
			}

			await _context.SaveChangesAsync();

			TempData["RegisterSuccess"] =
				"تم إرسال طلب التسجيل بنجاح. " +
				"سيتم مراجعته من الإدارة وإرسال كود التفعيل لك.";

			return RedirectToAction(nameof(RegisterSuccess));
		}

		// =========================================
		// صفحة نجاح التسجيل
		// =========================================

		[HttpGet]
		[AllowAnonymous]
		public IActionResult RegisterSuccess()
		{
			return View();
		}

		// =========================================
		// نسيت كلمة المرور - GET
		// =========================================

		[HttpGet]
		[AllowAnonymous]
		public IActionResult ForgotPassword()
		{
			return View(new Models.ViewModels.ForgotPasswordViewModel());
		}

		// =========================================
		// نسيت كلمة المرور - POST
		// =========================================

		[HttpPost]
		[AllowAnonymous]
		[ValidateAntiForgeryToken]
		[EnableRateLimiting("ForgotPasswordPolicy")]
		public async Task<IActionResult> ForgotPassword(
			Models.ViewModels.ForgotPasswordViewModel model)
		{
			if (!ModelState.IsValid)
			{
				return View(model);
			}

			var phone = model.Phone.Trim();

			var user = await _context.Users
				.FirstOrDefaultAsync(x => x.Phone == phone);

			if (user == null)
			{
				TempData["Info"] =
					"إذا كان الرقم مسجّلًا، سيصلك كود التحقق.";

				return RedirectToAction(nameof(ForgotPassword));
			}

			if (!user.IsActive || !user.IsPasswordSet)
			{
				TempData["Info"] =
					"إذا كان الرقم مسجّلًا، سيصلك كود التحقق.";

				return RedirectToAction(nameof(ForgotPassword));
			}

			if (user.LockoutUntil.HasValue &&
				user.LockoutUntil.Value > DateTime.UtcNow)
			{
				var remaining =
					(int)(user.LockoutUntil.Value -
						DateTime.UtcNow).TotalMinutes;
				TempData["Error"] =
					$"تم قفل الحساب مؤقتًا. حاول بعد {remaining} دقيقة.";
				return RedirectToAction(nameof(ForgotPassword));
			}

			var code = System.Security.Cryptography
				.RandomNumberGenerator
				.GetInt32(100000, 1000000)
				.ToString();

			var expiresAt = DateTime.UtcNow.AddMinutes(AppConstants.ActivationCodeExpiryMinutes);

			// ✅ hash واحد فقط، نستخدمه في المكانين
			var codeHash = _passwordHasher.HashPassword(user, code);

			user.ActivationCodeHash = codeHash;
			user.ActivationCodeExpiresAt = expiresAt;

			var resetUrl =
				$"{Request.Scheme}://{Request.Host}/Account/ResetPassword?phone={phone}";

			var resetRequest = new PasswordResetRequest
			{
				Phone = phone,
				UserName = user.Phone,
				UserType = user.UserType,
				CodeHash = codeHash,
				Attempts = 0,
				RequestedAt = DateTime.UtcNow,
				ExpiresAt = expiresAt,
				IsSent = false,
				IsCompleted = false
			};

			_context.PasswordResetRequests.Add(resetRequest);

			await _context.SaveChangesAsync();

			await _whatsAppService.SendPasswordResetAsync(
				phone,
				user.Phone,
				code,
				resetUrl,
				expiresAt);

			TempData["Success"] =
				"تم إرسال كود التحقق. تحقق من الواتساب.";

			return RedirectToAction(
				nameof(ResetPassword),
				new { phone = phone });
		}

		// =========================================
		// تعيين كلمة مرور جديدة - GET
		// =========================================

		[HttpGet]
		[AllowAnonymous]
		public IActionResult ResetPassword(string? phone)
		{
			if (string.IsNullOrWhiteSpace(phone))
			{
				return RedirectToAction(nameof(ForgotPassword));
			}

			var model = new Models.ViewModels.ResetPasswordViewModel
			{
				Phone = phone
			};

			return View(model);
		}

		// =========================================
		// تعيين كلمة مرور جديدة - POST
		// =========================================

		[HttpPost]
		[AllowAnonymous]
		[ValidateAntiForgeryToken]
		[EnableRateLimiting("ForgotPasswordPolicy")]
		public async Task<IActionResult> ResetPassword(
			Models.ViewModels.ResetPasswordViewModel model)
		{
			if (!ModelState.IsValid)
			{
				return View(model);
			}

			if (!System.Text.RegularExpressions.Regex.IsMatch(
					model.Password,
					"^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d).{8,}$"))
			{
				ModelState.AddModelError(
					nameof(model.Password),
					"كلمة المرور يجب أن تحتوي على حرف كبير وحرف صغير ورقم على الأقل.");

				return View(model);
			}

			var phone = model.Phone.Trim();
			var code = model.VerificationCode.Trim();

			var user = await _context.Users
				.FirstOrDefaultAsync(x => x.Phone == phone);

			if (user == null)
			{
				ModelState.AddModelError(
					"",
					"رقم الهاتف أو الكود غير صحيح.");

				return View(model);
			}

			if (user.LockoutUntil.HasValue &&
				user.LockoutUntil.Value > DateTime.UtcNow)
			{
				var remaining =
					(int)(user.LockoutUntil.Value -
						DateTime.UtcNow).TotalMinutes;
				ModelState.AddModelError(
					"",
					$"تم قفل الحساب مؤقتًا. حاول بعد {remaining} دقيقة.");

				return View(model);
			}

			var resetRequest = await _context.PasswordResetRequests
				.Where(x => x.Phone == phone && !x.IsCompleted)
				.OrderByDescending(x => x.Id)
				.FirstOrDefaultAsync();

			if (resetRequest == null)
			{
				ModelState.AddModelError(
					"",
					"لا يوجد كود تحقق صالح. اطلب كودًا جديدًا.");
				return View(model);
			}

			if (DateTime.UtcNow > resetRequest.ExpiresAt)
			{
				ModelState.AddModelError(
					"",
					"انتهت صلاحية الكود. اطلب كودًا جديدًا.");

				return View(model);
			}

			if (resetRequest.LockedUntil.HasValue &&
				resetRequest.LockedUntil.Value > DateTime.UtcNow)
			{
				ModelState.AddModelError(
					"",
					"تم قفل الحساب مؤقتًا. حاول لاحقًا.");
				return View(model);
			}

			var codeResult =
				_passwordHasher.VerifyHashedPassword(
					user,
					resetRequest.CodeHash,
					code);

			if (codeResult == PasswordVerificationResult.Failed)
			{
				resetRequest.Attempts++;

				if (resetRequest.Attempts >= AppConstants.MaxVerificationAttempts)
				{
					resetRequest.LockedUntil = DateTime.UtcNow.AddMinutes(AppConstants.LockoutDurationMinutes);
					user.LockoutUntil = DateTime.UtcNow.AddMinutes(AppConstants.LockoutDurationMinutes);
				}

				_context.PasswordResetRequests.Update(resetRequest);
				await _context.SaveChangesAsync();

				ModelState.AddModelError(
					"",
					"كود التحقق غير صحيح.");

				return View(model);
			}

			user.PasswordHash =
				_passwordHasher.HashPassword(user, model.Password);

			user.IsPasswordSet = true;
			user.FailedLoginAttempts = 0;
			user.LockoutUntil = null;
			user.SecurityStamp = Guid.NewGuid().ToString();

			user.ActivationCodeHash = null;
			user.ActivationCodeExpiresAt = null;

			resetRequest.IsCompleted = true;
			resetRequest.CompletedAt = DateTime.UtcNow;

			await _context.SaveChangesAsync();

			TempData["Success"] =
				"تم تعيين كلمة المرور الجديدة بنجاح. يمكنك تسجيل الدخول الآن.";

			return RedirectToAction(nameof(Login));
		}
	}
}