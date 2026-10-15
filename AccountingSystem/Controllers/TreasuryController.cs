using System.Security.Claims;
using AccountingSystem.Data;
using AccountingSystem.Filters;
using AccountingSystem.Models;
using AccountingSystem.Models.ViewModels.Treasury;
using AccountingSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Controllers
{
	public class TreasuryController : Controller
	{
		private readonly ApplicationDbContext _context;
		private readonly IPostingService _postingService;
		private readonly IDocumentNumberService _documentNumberService;

		public TreasuryController(
			ApplicationDbContext context,
			IPostingService postingService,
			IDocumentNumberService documentNumberService)
		{
			_context = context;
			_postingService = postingService;
			_documentNumberService = documentNumberService;
		}

		// =========================================
		// الصفحة الرئيسية للخزينة
		// =========================================

		[HttpGet]
		[RequirePermission("treasury.view")]
		public async Task<IActionResult> Index(int page = 1, int pageSize = 20)
		{
			var model =
				new TreasuryIndexViewModel();

			var accounts =
				await _context.CashAccounts
					.AsNoTracking()
					.OrderBy(x => x.Name)
					.ToListAsync();

			// الحساب الداخلي «إيراد العمولات» لا يُعرض كمركز
			// في صفحة الخزينة (يدار أوتوماتيكيًا فقط).
			accounts =
				accounts
					.Where(x =>
						x.Name != "إيراد العمولات")
					.ToList();

			var transactionsQuery = _context.TreasuryTransactions
				.AsNoTracking()
				.Include(x => x.CashAccount)
				.Include(x => x.TransferToCashAccount)
				.Include(x => x.CreatedByUser)
				.AsQueryable();

			var totalCount = await transactionsQuery.CountAsync();

			var transactions = await transactionsQuery
				.OrderByDescending(x => x.CreatedAt)
				.ThenByDescending(x => x.Id)
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
				.ToListAsync();

			var monthStart =
				new DateTime(
					DateTime.Today.Year,
					DateTime.Today.Month,
					1);

			var dayStart =
				DateTime.Today;

			// حركات الشهر الحالي لحساب استهلاك الحدود
			// (المبالغ الخارجة من كل مركز خلال اليوم/الشهر).
			var monthRows =
				await _context.TreasuryTransactions
					.AsNoTracking()
					.Where(x => x.CreatedAt >= monthStart)
					.ToListAsync();

			var accountIds = accounts.Select(x => x.Id).ToList();

			var allTransactions = await _context.TreasuryTransactions
				.AsNoTracking()
				.Where(x => accountIds.Contains(x.CashAccountId) ||
							(x.TransferToCashAccountId.HasValue && accountIds.Contains(x.TransferToCashAccountId.Value)))
				.ToListAsync();

			var balanceDict = new Dictionary<int, decimal>();
			var countDict = new Dictionary<int, int>();

			foreach (var account in accounts)
			{
				var accountTransactions = allTransactions
					.Where(x => x.CashAccountId == account.Id ||
								x.TransferToCashAccountId == account.Id)
					.ToList();

				decimal balance = 0;
				foreach (var t in accountTransactions)
				{
					if (t.CashAccountId == account.Id)
					{
						balance += t.Type switch
						{
							TreasuryTransactionType.Receive => t.Amount,
							TreasuryTransactionType.Pay => -t.Amount,
							TreasuryTransactionType.Transfer => -t.Amount,
							TreasuryTransactionType.WalletTransfer => -t.Amount,
							TreasuryTransactionType.Adjust => t.Amount,
							_ => 0m
						};
					}
					if (t.TransferToCashAccountId == account.Id)
					{
						balance += t.Amount;
					}
				}

				balanceDict[account.Id] = balance;
				countDict[account.Id] = accountTransactions.Count;
			}

			var outflowByAccount = new Dictionary<int, (decimal Daily, decimal Monthly)>();
			foreach (var t in monthRows)
			{
				if (!accountIds.Contains(t.CashAccountId))
					continue;

				var outflow =
					t.Type switch
					{
						TreasuryTransactionType.Receive => -t.Amount,
						TreasuryTransactionType.Pay => t.Amount,
						TreasuryTransactionType.Transfer => t.Amount,
						TreasuryTransactionType.WalletTransfer => t.Amount,
						TreasuryTransactionType.Adjust => t.Amount < 0 ? -t.Amount : 0m,
						_ => 0m
					};

				if (outflow <= 0)
					continue;

				outflowByAccount.TryGetValue(t.CashAccountId, out var current);
				current.Monthly += outflow;
				if (t.CreatedAt >= dayStart)
					current.Daily += outflow;
				outflowByAccount[t.CashAccountId] = current;
			}

			foreach (var account in accounts)
			{
				outflowByAccount.TryGetValue(account.Id, out var outflow);

				model.Accounts.Add(
					new CashAccountBalanceViewModel
					{
						Id = account.Id,
						Name = account.Name,
						AccountNumber = account.AccountNumber,
						Balance = balanceDict[account.Id],
						TransactionCount = countDict[account.Id],
						IsActive = account.IsActive,
						DailyLimit = account.DailyLimit,
						MonthlyLimit = account.MonthlyLimit,
						DailyConsumed = outflow.Daily,
						MonthlyConsumed = outflow.Monthly
					});

				model.TotalBalance += balanceDict[account.Id];
			}

			model.RecentTransactions =
				transactions
					.Select(MapTransaction)
					.ToList();

			model.PageNumber = page;
			model.PageSize = pageSize;
			model.TotalCount = totalCount;

			return View(model);
		}

		// =========================================
		// الإغلاق اليومي للخزينة
		// =========================================

		[HttpGet]
		[RequirePermission("treasury.view")]
		public async Task<IActionResult> DailyClose(
			DateTime? date)
		{
			var day = (date ?? DateTime.Today).Date;
			var dayEnd = day.AddDays(1);

			var model =
				new TreasuryDailyCloseViewModel
				{
					Date = day
				};

			var accounts =
				await _context.CashAccounts
					.AsNoTracking()
					.Where(x => x.IsActive)
					.OrderBy(x => x.Name)
					.ToListAsync();

			var transactions =
				await _context.TreasuryTransactions
					.AsNoTracking()
					.Include(x => x.CashAccount)
					.Include(x => x.TransferToCashAccount)
					.Include(x => x.CreatedByUser)
					.Include(x => x.Customer)
					.Include(x => x.Supplier)
					.Include(x => x.Employee)
					.Where(x => x.CreatedAt < dayEnd)
					.OrderBy(x => x.CreatedAt)
					.ToListAsync();

			foreach (var account in accounts)
			{
				decimal opening = 0;
				decimal totalIn = 0;
				decimal totalOut = 0;
				var count = 0;

				foreach (var t in transactions)
				{
					var isDay = t.CreatedAt >= day;

if (t.CashAccountId == account.Id)
				{
					var signed =
						t.Type switch
						{
							TreasuryTransactionType.Receive =>
								t.Amount,
							TreasuryTransactionType.Pay =>
								-t.Amount,
							TreasuryTransactionType.Transfer =>
								-t.Amount,
							// رسوم تحويل المحفظة تُخصم من المحفظة المصدرة
							TreasuryTransactionType.WalletTransfer =>
								-t.Amount,
							_ => t.Amount
						};

						if (isDay)
						{
							if (signed >= 0)
							{
								totalIn += signed;
							}
							else
							{
								totalOut += -signed;
							}

							count++;
						}
						else
						{
							opening += signed;
						}
					}
					else if (
						t.TransferToCashAccountId == account.Id)
					{
						if (isDay)
						{
							totalIn += t.Amount;
							count++;
						}
						else
						{
							opening += t.Amount;
						}
					}
				}

				var closing = opening + totalIn - totalOut;

				model.Rows.Add(
					new TreasuryDailyCloseRowViewModel
					{
						CashAccountId = account.Id,
						AccountName = account.Name,
						Opening = opening,
						TotalIn = totalIn,
						TotalOut = totalOut,
						Closing = closing,
						Count = count
					});

				model.TotalOpening += opening;
				model.TotalIn += totalIn;
				model.TotalOut += totalOut;
				model.TotalClosing += closing;
			}

			model.Transactions =
				transactions
					.Where(x => x.CreatedAt >= day)
					.OrderByDescending(x => x.CreatedAt)
					.ThenByDescending(x => x.Id)
					.Select(MapTransaction)
					.ToList();

			return View(model);
		}

		// =========================================
		// إضافة مركز نقدي - POST
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("treasury.account.manage")]
		public async Task<IActionResult> CreateAccount(
			CashAccountCreateViewModel model)
		{
			model.Name = model.Name?.Trim() ?? string.Empty;
			model.AccountNumber =
				string.IsNullOrWhiteSpace(model.AccountNumber)
					? null
					: model.AccountNumber.Trim();

			if (!ModelState.IsValid)
			{
				TempData["Error"] =
					"يرجى التحقق من بيانات المركز النقدي.";

				return RedirectToAction(nameof(Index));
			}

			if (!model.Provider.HasValue)
			{
				TempData["Error"] =
					"يجب اختيار نوع المحفظة.";

				return RedirectToAction(nameof(Index));
			}

			// يُسمح بمراكز نقدية متعددة لنفس المالك بنفس الاسم
			// (محافظ إلكترونية وحسابات بنكية متعددة)، مع اشتراط
			// عدم تكرار الاسم ورقم الحساب معًا (الاسم + الرقم).

			if (!string.IsNullOrWhiteSpace(model.AccountNumber))
			{
				var nameNumberExists =
					await _context.CashAccounts
						.AnyAsync(x =>
							x.Name == model.Name &&
							x.AccountNumber == model.AccountNumber);

				if (nameNumberExists)
				{
					TempData["Error"] =
						"يوجد مركز نقدي بنفس الاسم ورقم الحساب.";

					return RedirectToAction(nameof(Index));
				}
			}
			else
			{
				// بدون رقم حساب: يُمنع فقط التطابق الكامل بنفس الاسم
				// وبلا رقم حساب أيضًا.
				var bareNameExists =
					await _context.CashAccounts
						.AnyAsync(x =>
							x.Name == model.Name &&
							x.AccountNumber == null);

				if (bareNameExists)
				{
					TempData["Error"] =
						"يوجد مركز نقدي بنفس الاسم بدون رقم حساب.";

					return RedirectToAction(nameof(Index));
				}
			}

			_context.CashAccounts.Add(
				new CashAccount
				{
					Name = model.Name,
					AccountNumber = model.AccountNumber,
					Provider = model.Provider!.Value,
					DailyLimit = model.DailyLimit,
					MonthlyLimit = model.MonthlyLimit,
					IsActive = true,
					CreatedAt = DateTime.UtcNow
				});

			await _context.SaveChangesAsync();

			TempData["Success"] =
				$"تمت إضافة المركز النقدي «{model.Name}».";

			return RedirectToAction(nameof(Index));
		}

		// =========================================
		// قائمة الحركات
		// =========================================

		[HttpGet]
		[RequirePermission("treasury.view")]
		public async Task<IActionResult> Transactions(
			int? accountId,
			TreasuryTransactionType? type)
		{
			var model =
				new TreasuryTransactionsViewModel
				{
					AccountId = accountId,
					Type = type,
					Accounts =
						await LoadAccountsAsync()
				};

			var query =
				_context.TreasuryTransactions
					.AsNoTracking()
					.Include(x => x.CashAccount)
					.Include(x => x.TransferToCashAccount)
					.Include(x => x.Customer)
					.Include(x => x.Supplier)
					.Include(x => x.Employee)
					.Include(x => x.CreatedByUser)
					.AsQueryable();

			if (accountId.HasValue &&
				accountId.Value > 0)
			{
				query =
					query.Where(x =>
						x.CashAccountId == accountId.Value ||
						x.TransferToCashAccountId == accountId.Value);
			}

			if (type.HasValue)
			{
				query =
					query.Where(x => x.Type == type.Value);
			}

			var rows =
				await query
					.OrderByDescending(x => x.CreatedAt)
					.ThenByDescending(x => x.Id)
					.Take(AppConstants.DefaultPageSize)
					.ToListAsync();

			model.Rows =
				rows
					.Select(MapTransaction)
					.ToList();

			return View(model);
		}

		// =========================================
		// إيصال سند خزينة (عرض + طباعة)
		// =========================================

		[HttpGet]
		[RequirePermission("treasury.view")]
		public async Task<IActionResult> Receipt(int id)
		{
			var entry =
				await _context.TreasuryTransactions
					.AsNoTracking()
					.Include(x => x.CashAccount)
					.Include(x => x.TransferToCashAccount)
					.Include(x => x.Customer)
					.Include(x => x.Supplier)
					.Include(x => x.Employee)
					.Include(x => x.CreatedByUser)
					.FirstOrDefaultAsync(x =>
						x.Id == id);

			if (entry == null)
			{
				return NotFound();
			}

			var company =
				await _context.Companies
					.AsNoTracking()
					.OrderBy(x => x.Id)
					.FirstOrDefaultAsync();

			var partyName =
				entry.Customer?.Name ??
				entry.Supplier?.Name ??
				(entry.Employee != null
					? $"موظف: {entry.Employee.Name}"
					: null);

			var model =
				new TreasuryReceiptViewModel
				{
					Id = entry.Id,
					TransactionNumber = entry.TransactionNumber,
					Type = entry.Type,
					CashAccountName =
						entry.CashAccount?.Name ?? "-",
					TransferToCashAccountName =
						entry.TransferToCashAccount?.Name,
					PartyName = partyName,
					Reason = entry.Reason,
					ReferenceDocument =
						entry.ReferenceDocument,
					Amount = entry.Amount,
					CreatedByUserName =
						entry.CreatedByUser?.Phone,
					CreatedAt = entry.CreatedAt,
					Company = new TreasuryFinanceEntityViewModel
					{
						Name =
							company?.Name ?? "الشركة",
						Phone = company?.Phone,
						Address = company?.Address,
						LogoPath = company?.LogoPath
					}
				};

			return View(model);
		}

		// =========================================
		// إيصال قبض - GET
		// =========================================

		[HttpGet]
		[RequirePermission("treasury.receive")]
		public async Task<IActionResult> Receive()
		{
			var model =
				new TreasuryReceiveViewModel();

			await LoadPartyDataAsync(model);

			model.AccountsProviders =
				await _context.CashAccounts
					.AsNoTracking()
					.Select(x =>
						new { x.Id, x.Provider })
					.ToDictionaryAsync(
						x => x.Id,
						x => (int)x.Provider);

			return View(model);
		}

		// =========================================
		// إيصال قبض - POST
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("treasury.receive")]
		public async Task<IActionResult> Receive(
			TreasuryReceiveViewModel model)
		{
			model.Reason =
				string.IsNullOrWhiteSpace(model.Reason)
					? null
					: model.Reason.Trim();

			model.ReferenceDocument =
				string.IsNullOrWhiteSpace(model.ReferenceDocument)
					? null
					: model.ReferenceDocument.Trim();

			if (model.OtherIncomeAccountId == 0)
			{
				model.OtherIncomeAccountId = null;
			}

			if (!ModelState.IsValid)
			{
				await LoadPartyDataAsync(model);

				return View(model);
			}

			if (model.CustomerId == null &&
				model.SupplierId == null &&
				model.EmployeeId == null &&
				model.OtherIncomeAccountId == null)
			{
				ModelState.AddModelError(
					string.Empty,
					"يجب اختيار طرف واحد على الأقل (عميل/مورد/موظف) أو حساب إيراد حر.");

				await LoadPartyDataAsync(model);

				return View(model);
			}

			var account = await _context.CashAccounts
				.FirstOrDefaultAsync(x =>
					x.Id == model.CashAccountId &&
					x.IsActive);

			if (account == null)
			{
				ModelState.AddModelError(
					nameof(model.CashAccountId),
					"المركز النقدي غير موجود أو غير نشط.");

				await LoadPartyDataAsync(model);

				return View(model);
			}

			// =========================================
			// التسجيل والترحيل في معاملة واحدة:
			// الحركة + قيد الخزينة (مدين نقدي / دائن طرف)
			// =========================================

			// سياسة القبض من محفظة التليفون (فودافون/اتصالات):
			// العميل يدفع كاملًا، ويُخصم 1% لحساب إيراد العمولات.
			var isPhoneWallet =
				account.Provider == WalletProvider.VodafoneCash ||
				account.Provider == WalletProvider.EtisalatCash;

			decimal commission = 0m;

			if (isPhoneWallet && model.ApplyCommission)
			{
				// العمولة = أقرب عدد صحيح من 1%
				// (مثال: 505 → 5 وبذلك يصبح صافي العميل 500)
				commission =
					Math.Round(
						model.Amount * AppConstants.CommissionRate,
						MidpointRounding.AwayFromZero);
			}

			string? postError = null;
			string? receiveError = null;

			// التنفيذ داخل استراتيجية إعادة المحاولة (EnableRetryOnFailure)
			// لأنها لا تدعم معاملة يدوية مباشرة.
			var strategy =
				_context.Database.CreateExecutionStrategy();

			await strategy.ExecuteAsync(
				async () =>
				{
					await using var transaction =
						await _context.Database.BeginTransactionAsync();

					try
					{
						var entry =
							new TreasuryTransaction
							{
							TransactionNumber =
								await _documentNumberService.GenerateNumberAsync("RCP", "TreasuryTransactions", DateTime.Today.ToString("yyyyMMdd")),
								Type = TreasuryTransactionType.Receive,
								CashAccountId = model.CashAccountId,
								Amount = model.Amount,
								CustomerId = model.CustomerId,
								SupplierId = model.SupplierId,
								EmployeeId = model.EmployeeId,
								ApplyWalletIncomeCommission = commission > 0,
								WalletCommissionAmount =
									commission > 0 ? commission : (decimal?)null,
								Reason = model.Reason,
								ReferenceDocument = model.ReferenceDocument,
								CreatedByUserId = CurrentUserId(),
								CreatedAt = DateTime.UtcNow
							};

						_context.TreasuryTransactions.Add(entry);

						await _context.SaveChangesAsync();

						// القيد المحاسبي للقبض (إن كان للطرف حساب)
						var err =
							await PostTreasuryJournalAsync(
								entry,
								CurrentUserId(),
								null,
								model.OtherIncomeAccountId);

						if (err != null)
						{
							await transaction.RollbackAsync();

							postError = err;
							return;
						}

						await transaction.CommitAsync();
					}
					catch (Exception ex)
					{
						await transaction.RollbackAsync();

						receiveError = ex.Message;
					}
				});

			if (postError != null)
			{
				await LoadPartyDataAsync(model);

				ModelState.AddModelError(
					string.Empty,
					postError);

				return View(model);
			}

			if (receiveError != null)
			{
				TempData["Error"] =
					"تعذر تسجيل الإيصال: " + receiveError;

				return RedirectToAction(nameof(Index));
			}

			TempData["Success"] =
				commission > 0
					? $"تم تسجيل إيصال قبض بقيمة {model.Amount.ToString("N2")} " +
					  $"على المحفظة كاملة، وخصم 1% ({commission.ToString("N2")}) " +
					  "من حساب العميل لصالح إيراد العمولات."
					: $"تم تسجيل إيصال قبض بقيمة {model.Amount.ToString("N2")}.";

			return RedirectToAction(nameof(Index));
		}

		// =========================================
		// إذن صرف - GET
		// =========================================

		[HttpGet]
		[RequirePermission("treasury.pay")]
		public async Task<IActionResult> Pay()
		{
			var model =
				new TreasuryPayViewModel();

			await LoadPartyDataAsyncPay(model);

			model.AccountsProviders =
				await _context.CashAccounts
					.AsNoTracking()
					.Select(x =>
						new { x.Id, x.Provider })
					.ToDictionaryAsync(
						x => x.Id,
						x => (int)x.Provider);

			return View(model);
		}

		// =========================================
		// إذن صرف - POST
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("treasury.pay")]
		public async Task<IActionResult> Pay(
			TreasuryPayViewModel model)
		{
			model.Reason =
				string.IsNullOrWhiteSpace(model.Reason)
					? null
					: model.Reason.Trim();

			model.ReferenceDocument =
				string.IsNullOrWhiteSpace(model.ReferenceDocument)
					? null
					: model.ReferenceDocument.Trim();

			if (!ModelState.IsValid)
			{
				await LoadPartyDataAsyncPay(model);

				return View(model);
			}

			var account = await _context.CashAccounts
				.FirstOrDefaultAsync(x =>
					x.Id == model.CashAccountId &&
					x.IsActive);

			if (account == null)
			{
				ModelState.AddModelError(
					nameof(model.CashAccountId),
					"المركز النقدي غير موجود أو غير نشط.");

				await LoadPartyDataAsyncPay(model);

				return View(model);
			}

			// =========================================
			// رسوم الصرف حسب نوع المركز النقدي:
			// - نقدي / محفظة عامة: بدون رسوم.
			// - انستا باي: رسوم يكتبها المستخدم (نص فارغ بدون
			//   افتراضي) وبحد أقصى 1% من المبلغ.
			// - محفظة موبايل (فودافون/اتصالات): 1ج لنفس الشبكة
			//   و15ج لشبكة أخرى حسب نوع محفظة المستلم.
			// =========================================

			decimal fee = 0m;

			switch (account.Provider)
			{
				case WalletProvider.InstaPay:
				{
				var maxFee =
					Math.Round(
						model.Amount * AppConstants.CommissionRate,
						2);

					if (!model.WalletFee.HasValue ||
						model.WalletFee.Value <= 0)
					{
						ModelState.AddModelError(
							nameof(model.WalletFee),
							"يجب إدخال رسوم التحويل من انستا باي.");
					}
					else if (model.WalletFee.Value > maxFee)
					{
						ModelState.AddModelError(
							nameof(model.WalletFee),
							$"رسوم انستا باي لا يمكن أن تتجاوز " +
							$"1% من المبلغ ({maxFee.ToString("N2")}).");
					}
					else
					{
						fee = model.WalletFee.Value;
					}

					break;
				}

				case WalletProvider.VodafoneCash:
				case WalletProvider.EtisalatCash:
				{
					if (!model.RecipientWalletProvider.HasValue)
					{
						ModelState.AddModelError(
							nameof(model.RecipientWalletProvider),
							"يجب اختيار نوع محفظة المستلم " +
							"لتحديد رسوم التحويل.");
					}
					else
					{
					fee =
						model.RecipientWalletProvider.Value ==
							account.Provider
							? AppConstants.WalletTransferSameNetworkFee
							: AppConstants.WalletTransferOtherNetworkFee;
					}

					break;
				}

				default:
					// نقدي / محفظة عامة — بدون رسوم
					break;
			}

			if (!ModelState.IsValid)
			{
				await LoadPartyDataAsyncPay(model);

				return View(model);
			}

			var balance =
				await CalculateBalanceAsync(model.CashAccountId);

			var totalOut = model.Amount + fee;

			if (balance < totalOut)
			{
				ModelState.AddModelError(
					string.Empty,
					$"الرصيد المتاح {balance.ToString("N2")} غير كافي " +
					$"(المطلوب {totalOut.ToString("N2")} شاملة الرسوم).");

				await LoadPartyDataAsyncPay(model);

				return View(model);
			}

			// =========================================
			// تسجيل الصرف والترحيل في معاملة واحدة:
			// الحركة + قيد الخزينة (مدين طرف / دائن نقدي)
			// =========================================

			string? postError = null;
			string? payError = null;

			var strategy =
				_context.Database.CreateExecutionStrategy();

			await strategy.ExecuteAsync(
				async () =>
				{
					await using var transaction =
						await _context.Database.BeginTransactionAsync();

					try
					{
						var entry =
							new TreasuryTransaction
							{
							TransactionNumber =
								await _documentNumberService.GenerateNumberAsync("PAY", "TreasuryTransactions", DateTime.Today.ToString("yyyyMMdd")),
								Type = TreasuryTransactionType.Pay,
								CashAccountId = model.CashAccountId,
								Amount = model.Amount,
								CustomerId = model.CustomerId,
								SupplierId = model.SupplierId,
								EmployeeId = model.EmployeeId,
								Reason = model.Reason,
								ReferenceDocument = model.ReferenceDocument,
								CreatedByUserId = CurrentUserId(),
								CreatedAt = DateTime.UtcNow
							};

						_context.TreasuryTransactions.Add(entry);

						// الرسوم تُحول لحساب «إيراد العمولات»
						if (fee > 0)
						{
							var feesAccount =
								await EnsureWalletFeesAccountAsync();

							_context.TreasuryTransactions.Add(
								new TreasuryTransaction
								{
									TransactionNumber =
										await _documentNumberService.GenerateNumberAsync("WLT", "TreasuryTransactions", DateTime.Today.ToString("yyyyMMdd")),
									Type = TreasuryTransactionType.WalletTransfer,
									CashAccountId = model.CashAccountId,
									TransferToCashAccountId = feesAccount.Id,
									Amount = fee,
									WalletFeesAccountId = feesAccount.Id,
									SameCompanyTransfer =
										account.Provider is
											WalletProvider.VodafoneCash or
											WalletProvider.EtisalatCash &&
										model.RecipientWalletProvider ==
											account.Provider,
									WalletFee = fee,
									Reason =
										$"رسوم صرف محفظة ({model.Amount.ToString("N2")})",
									CreatedByUserId = CurrentUserId(),
									CreatedAt = DateTime.UtcNow
								});
						}

						await _context.SaveChangesAsync();

						// القيد المحاسبي للصرف (إن كان للطرف حساب)
						var err =
							await PostTreasuryJournalAsync(
								entry,
								CurrentUserId(),
								model.ExpenseAccountId);

						if (err != null)
						{
							await transaction.RollbackAsync();

							postError = err;
							return;
						}

						await transaction.CommitAsync();
					}
					catch (Exception ex)
					{
						await transaction.RollbackAsync();

						payError = ex.Message;
					}
				});

			if (postError != null)
			{
				await LoadPartyDataAsyncPay(model);

				ModelState.AddModelError(
					string.Empty,
					postError);

				return View(model);
			}

			if (payError != null)
			{
				TempData["Error"] =
					"تعذر تسجيل إذن الصرف: " + payError;

				return RedirectToAction(nameof(Index));
			}

			TempData["Success"] =
				fee > 0
					? $"تم تسجيل إذن صرف بقيمة {model.Amount.ToString("N2")} " +
					  $"ورسوم تحويل {fee.ToString("N2")}."
					: $"تم تسجيل إذن صرف بقيمة {model.Amount.ToString("N2")}.";

			return RedirectToAction(nameof(Index));
		}

		// =========================================
		// تسوية خزينة - GET
		// =========================================

		[HttpGet]
		[RequirePermission("treasury.adjust")]
		public async Task<IActionResult> Adjust()
		{
			var model =
				new TreasuryAdjustViewModel
				{
					Accounts = await LoadAccountsAsync()
				};

			return View(model);
		}

		// =========================================
		// تسوية خزينة - POST
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("treasury.adjust")]
		public async Task<IActionResult> Adjust(
			TreasuryAdjustViewModel model)
		{
			model.Reason =
				model.Reason?.Trim() ?? string.Empty;

			if (!ModelState.IsValid)
			{
				model.Accounts = await LoadAccountsAsync();

				return View(model);
			}

			var account = await _context.CashAccounts
				.FirstOrDefaultAsync(x =>
					x.Id == model.CashAccountId &&
					x.IsActive);

			if (account == null)
			{
				ModelState.AddModelError(
					nameof(model.CashAccountId),
					"المركز النقدي غير موجود أو غير نشط.");

				model.Accounts = await LoadAccountsAsync();

				return View(model);
			}

			var signedAmount =
				model.IsIncrease
					? model.Amount
					: -model.Amount;

			if (!model.IsIncrease)
			{
				var balance =
					await CalculateBalanceAsync(model.CashAccountId);

				if (balance < model.Amount)
				{
					ModelState.AddModelError(
						string.Empty,
						"لا يمكن تسوية نقص أكبر من الرصيد الحالي.");

					model.Accounts = await LoadAccountsAsync();

					return View(model);
				}
			}

			_context.TreasuryTransactions.Add(
				new TreasuryTransaction
				{
					TransactionNumber =
						await _documentNumberService.GenerateNumberAsync("ADJ", "TreasuryTransactions", DateTime.Today.ToString("yyyyMMdd")),
					Type = TreasuryTransactionType.Adjust,
					CashAccountId = model.CashAccountId,
					Amount = signedAmount,
					Reason = model.Reason,
					CreatedByUserId = CurrentUserId(),
					CreatedAt = DateTime.UtcNow
				});

			await _context.SaveChangesAsync();

			TempData["Success"] =
				$"تمت تسوية الخزينة بمبلغ {model.Amount.ToString("N2")}.";

			return RedirectToAction(nameof(Index));
		}

		// =========================================
		// حساب رصيد مركز
		// =========================================

		private async Task<decimal> CalculateBalanceAsync(
			int accountId)
		{
			var transactions =
				await _context.TreasuryTransactions
					.AsNoTracking()
					.Where(x =>
						x.CashAccountId == accountId ||
						x.TransferToCashAccountId == accountId)
					.ToListAsync();

			decimal balance = 0;

			foreach (var t in transactions)
			{
				if (t.CashAccountId == accountId)
				{
					balance +=
						t.Type switch
						{
							TreasuryTransactionType.Receive =>
								t.Amount,
							TreasuryTransactionType.Pay =>
								-t.Amount,
							TreasuryTransactionType.Transfer =>
								-t.Amount,
							// رسوم تحويل المحفظة تُخصم من المحفظة المصدرة
							TreasuryTransactionType.WalletTransfer =>
								-t.Amount,
							_ => t.Amount
						};
				}
				else
				{
					// حركة واردة من مركز آخر
					balance += t.Amount;
				}
			}

			return balance;
		}

		// =========================================
		// التحويل لعرض صف
		// =========================================

		private TreasuryTransactionRowViewModel MapTransaction(
			TreasuryTransaction t)
		{
			var partyName =
				t.Customer?.Name ??
				t.Supplier?.Name ??
				(t.Employee != null
					? $"موظف: {t.Employee.Name}"
					: null);

			return new TreasuryTransactionRowViewModel
			{
				Id = t.Id,
				TransactionNumber = t.TransactionNumber,
				Type = t.Type,
				AccountName = t.CashAccount?.Name ?? "-",
				TransferToAccountName =
					t.TransferToCashAccount?.Name,
				Amount = t.Amount,
				PartyName = partyName,
				Reason = t.Reason,
				ReferenceDocument = t.ReferenceDocument,
				CreatedByUserName = t.CreatedByUser?.Phone,
				CreatedAt = t.CreatedAt
			};
		}

		// =========================================
		// ترحيل قيد الخزينة لحركة قبض/صرف يدوي.
		// يرجع null عند النجاح أو رسالة الخطأ عند الفشل.
		// - يُنشئ القيد فقط عندما يكون للطرف حساب محاسبي
		//   (عميل 1103 / مورد 2102) وللمركز النقدي حساب مرتبط
		// - حركات الموظفين والتحويلات والتسويات والمحافظ
		//   خارج نطاق الترحيل حاليًا (بلا حسابات مقابلة)
		// - حركات الفواتير (AutoJournal=false) لا تُرحَّل
		//   لأن قيدها يُنشأ من فاتورة البيع نفسها
		// =========================================

		private async Task<string?> PostTreasuryJournalAsync(
			TreasuryTransaction entry,
			int? userId,
			int? expenseChartAccountId = null,
			int? incomeChartAccountId = null)
		{
			if (!entry.AutoJournal)
			{
				return null;
			}

			if (entry.Type != TreasuryTransactionType.Receive &&
				entry.Type != TreasuryTransactionType.Pay)
			{
				return null;
			}

			var cashChartId =
				await ResolveCashChartIdAsync(
					entry.CashAccountId);

			if (cashChartId == null)
			{
				return null;
			}

			// حساب الطرف (عميل 1103 / مورد 2102)
			string partyCode;
			AccountType partyType;
			string partyLabel;

			if (entry.SupplierId.HasValue)
			{
				partyCode = "2102";
				partyType = AccountType.Liability;
				partyLabel = "الموردون";
			}
			else if (entry.CustomerId.HasValue)
			{
				partyCode = "1103";
				partyType = AccountType.Asset;
				partyLabel = "العملاء";
			}
			else if (expenseChartAccountId.HasValue)
			{
				// الصرف الحر بلا عميل/مورد/موظف:
				// قيد مباشر صريح بدل الحفظ الصامت بلا قيد —
				//   مدين  [حساب المصروف المختار]
				//   دائن  [المركز النقدي / الخزينة]
				partyCode = expenseChartAccountId.Value.ToString();
				partyType = AccountType.Expense;
				partyLabel = "حساب المصروف";
			}
			else if (entry.Type == TreasuryTransactionType.Receive &&
				incomeChartAccountId.HasValue)
			{
				// القبض الحر بلا طرف:
				// دائن حساب الإيراد المختار
				partyCode = incomeChartAccountId.Value.ToString();
				partyType = AccountType.Revenue;
				partyLabel = "حساب الإيراد";
			}
			else
			{
				// =========================================
				// ممنوع الحفظ الصامت: إن لم يُحدَّد طرف ولو
				// مصروفٌ حر، نُرجع خطأً صريحًا ليُعرض للمستخدم
				// بدل حفظ السند بلا أي قيد محاسبي (بيانات
				// "فقرة صامتة" لا تُرحَّل إلى دفاتر المحاسبة).
				// =========================================
				return "لا يمكن ترحيل الصرف: يجب تحديد عميل أو مورد أو موظف أو حساب مصروف.";
			}

			var partyAccount =
				await _context.ChartAccounts
					.AsNoTracking()
					.FirstOrDefaultAsync(x =>
						expenseChartAccountId.HasValue
							? x.Id == expenseChartAccountId.Value
								&& x.IsActive
							: incomeChartAccountId.HasValue
								? x.Id == incomeChartAccountId.Value
									&& x.IsActive
								: x.Code == partyCode
									&& x.Type == partyType
									&& x.IsActive);

			if (partyAccount == null)
			{
				if (expenseChartAccountId.HasValue)
				{
					return "حساب المصروف المختار غير موجود في شجرة الحسابات.";
				}

				if (incomeChartAccountId.HasValue)
				{
					return "حساب الإيراد المختار غير موجود في شجرة الحسابات.";
				}

				return "حساب " + partyLabel +
					" غير موجود في شجرة الحسابات.";
			}

			var lines = new List<JournalLineRequest>();

			if (entry.Type == TreasuryTransactionType.Receive)
			{
				// قبض: مدين نقدي (المحفظة كاملة) / دائن الطرف.
				// عند خصم 1% من محفظة التليفون:
				//   دائن العميل = المبلغ - العمولة
				//   دائن إيراد العمولات = العمولة
				lines.Add(
					new JournalLineRequest
					{
						ChartAccountId = cashChartId.Value,
						Debit = entry.Amount,
						Description = "قبض نقدي"
					});

				var commission =
					entry.ApplyWalletIncomeCommission &&
					entry.WalletCommissionAmount.HasValue
						? entry.WalletCommissionAmount.Value
						: 0m;

				var customerCredit =
					Math.Max(0m, entry.Amount - commission);

				var commissionAccount =
					commission > 0
						? await EnsureWalletIncomeChartAccountAsync()
						: null;

				if (commission > 0 && commissionAccount == null)
				{
					return "حساب «إيراد العمولات» غير موجود في شجرة الحسابات.";
				}

				lines.Add(
					new JournalLineRequest
					{
						ChartAccountId = partyAccount.Id,
						Credit = customerCredit,
						CustomerId = entry.CustomerId,
						SupplierId = entry.SupplierId,
						Description = "تسوية ذمم " + partyLabel
					});

				if (commission > 0 &&
					commissionAccount != null)
				{
					lines.Add(
						new JournalLineRequest
						{
							ChartAccountId = commissionAccount.Id,
							Credit = commission,
							Description = "خصم 1% من محفظة التليفون"
						});
				}
			}
			else
			{
				// صرف: مدين الطرف / دائن نقدي
				lines.Add(
					new JournalLineRequest
					{
						ChartAccountId = partyAccount.Id,
						Debit = entry.Amount,
						CustomerId = entry.CustomerId,
						SupplierId = entry.SupplierId,
						Description = "تسوية ذمم " + partyLabel
					});

				lines.Add(
					new JournalLineRequest
					{
						ChartAccountId = cashChartId.Value,
						Credit = entry.Amount,
						Description = "صرف نقدي"
					});
			}

			var result =
				await _postingService.PostAsync(
					JournalSourceType.Treasury,
					entry.Id,
					entry.CreatedAt.Date,
					"سند " +
					(entry.Type == TreasuryTransactionType.Receive
						? "قبض"
						: "صرف") +
					" " +
					entry.TransactionNumber,
					lines,
					isPosted: true,
					userId);

			return result.Success ? null : result.Error;
		}

		// =========================================
		// الحساب المحاسبي للمركز النقدي:
		// الحساب المرتبط بالمركز نفسه، وإن لم يُربط
		// نبحث عن أول مركز مربوط ثم احتياطيًا الصندوق 1101.
		// =========================================

		private async Task<int?> ResolveCashChartIdAsync(
			int cashAccountId)
		{
			var cash = await _context.CashAccounts
				.AsNoTracking()
				.FirstOrDefaultAsync(x =>
					x.Id == cashAccountId);

			if (cash == null)
			{
				return null;
			}

			if (cash.ChartAccountId.HasValue)
			{
				return cash.ChartAccountId.Value;
			}

			// أول مركز نشط مربوط بحساب
			var linked =
				await _context.CashAccounts
					.AsNoTracking()
					.Where(x =>
						x.IsActive &&
						x.ChartAccountId.HasValue)
					.OrderBy(x => x.Id)
					.Select(x => x.ChartAccountId)
					.FirstOrDefaultAsync();

			if (linked.HasValue)
			{
				return linked.Value;
			}

			// احتياطي: الصندوق 1101
			var cashBox =
				await _context.ChartAccounts
					.AsNoTracking()
					.FirstOrDefaultAsync(x =>
						x.Code == "1101" &&
						x.IsActive);

			return cashBox?.Id;
		}

		// =========================================
		// أدوات مساعدة
		// =========================================

		private int? CurrentUserId()
		{
			var value =
				User.FindFirstValue(
					ClaimTypes.NameIdentifier);

			if (int.TryParse(value, out var id))
			{
				return id;
			}

			return null;
		}

		private async Task<List<KeyValuePair<int, string>>>
			LoadAccountsAsync()
		{
			return await _context.CashAccounts
				.AsNoTracking()
				.Where(x => x.IsActive)
				.OrderBy(x => x.Name)
				.Select(x =>
					new KeyValuePair<int, string>(
						x.Id,
						string.IsNullOrWhiteSpace(x.AccountNumber)
							? x.Name
							: x.Name + " — " + x.AccountNumber))
				.ToListAsync();
		}

		private async Task<ChartAccount?>
			EnsureWalletIncomeChartAccountAsync()
		{
			var incomeChart =
				await _context.ChartAccounts
					.AsNoTracking()
					.FirstOrDefaultAsync(x =>
						x.Code == "4" &&
						x.Type == AccountType.Revenue &&
						x.IsActive);

			if (incomeChart == null)
			{
				return null;
			}

			var existing =
				await _context.ChartAccounts
					.AsNoTracking()
					.FirstOrDefaultAsync(x =>
						x.Name == "إيراد العمولات" &&
						x.Type == AccountType.Revenue);

			if (existing != null)
			{
				return existing;
			}

			var usedCodes =
				await _context.ChartAccounts
					.AsNoTracking()
					.Where(x => x.Code != null &&
						x.Code.StartsWith("400"))
					.Select(x => x.Code)
					.ToListAsync();

			var nextNumber = 1;

			while (usedCodes.Contains("400" + nextNumber))
			{
				nextNumber++;
			}

			var account =
				new ChartAccount
				{
					Code = "400" + nextNumber,
					Name = "إيراد العمولات",
					Type = AccountType.Revenue,
					ParentId = incomeChart.Id,
					IsActive = true,
					CreatedAt = DateTime.UtcNow
				};

			_context.ChartAccounts.Add(account);

			await _context.SaveChangesAsync();

			return account;
		}

		private async Task<CashAccount>
			EnsureWalletFeesAccountAsync()
		{
			var existing =
				await _context.CashAccounts
					.AsNoTracking()
					.FirstOrDefaultAsync(x =>
						x.Name == "إيراد العمولات");

			if (existing != null)
			{
				return existing;
			}

			var account =
				new CashAccount
				{
					Name = "إيراد العمولات",
					IsActive = true,
					CreatedAt = DateTime.UtcNow
				};

			_context.CashAccounts.Add(account);

			await _context.SaveChangesAsync();

			return account;
		}

		private async Task<List<KeyValuePair<int, string>>>
			LoadCustomersAsync()
		{
			return await _context.Customers
				.AsNoTracking()
				.Where(x => x.IsActive)
				.OrderBy(x => x.Name)
				.Select(x =>
					new KeyValuePair<int, string>(
						x.Id,
						x.Name))
				.ToListAsync();
		}

		private async Task<List<KeyValuePair<int, string>>>
			LoadSuppliersAsync()
		{
			return await _context.Suppliers
				.AsNoTracking()
				.Where(x => x.IsActive)
				.OrderBy(x => x.Name)
				.Select(x =>
					new KeyValuePair<int, string>(
						x.Id,
						x.Name))
				.ToListAsync();
		}

		private async Task<List<KeyValuePair<int, string>>>
			LoadEmployeesAsync()
		{
			return await _context.Employees
				.AsNoTracking()
				.Where(x => x.IsActive)
				.OrderBy(x => x.Name)
				.Select(x =>
					new KeyValuePair<int, string>(
						x.Id,
						x.Name))
				.ToListAsync();
		}

		private async Task LoadPartyDataAsync(
			TreasuryReceiveViewModel model)
		{
			model.Accounts = await LoadAccountsAsync();
			model.Customers = await LoadCustomersAsync();
			model.Suppliers = await LoadSuppliersAsync();
			model.Employees = await LoadEmployeesAsync();
			model.ExpenseAccounts = await LoadExpenseAccountsAsync();
			model.AvailableIncomeAccounts = await LoadIncomeAccountsAsync();

			model.AccountCards =
				await LoadAccountCardsAsync();
		}

		// =============================================
		// كروت تفاصيل المراكز (الرصيد + استهلاك الحدود):
		// تُعرض في نماذج القبض/الصرف عند اختيار المحفظة.
		// الحساب الداخلي «إيراد العمولات» يُستبعد.
		// =============================================

		private async Task<Dictionary<int, CashAccountBalanceViewModel>>
			LoadAccountCardsAsync()
		{
			var accounts =
				await _context.CashAccounts
					.AsNoTracking()
					.Where(x => x.Name != "إيراد العمولات")
					.ToListAsync();

			var monthStart =
				new DateTime(
					DateTime.Today.Year,
					DateTime.Today.Month,
					1);

			var dayStart =
				DateTime.Today;

			var monthRows =
				await _context.TreasuryTransactions
					.AsNoTracking()
					.Where(x => x.CreatedAt >= monthStart)
					.ToListAsync();

			var result =
				new Dictionary<int, CashAccountBalanceViewModel>();

			foreach (var account in accounts)
			{
				decimal dailyOut = 0;
				decimal monthlyOut = 0;

				foreach (var t in monthRows)
				{
					if (t.CashAccountId != account.Id)
					{
						continue;
					}

					var outflow =
						t.Type switch
						{
							TreasuryTransactionType.Pay =>
								t.Amount,
							TreasuryTransactionType.Transfer =>
								t.Amount,
							TreasuryTransactionType.WalletTransfer =>
								t.Amount,
							TreasuryTransactionType.Adjust =>
								t.Amount < 0 ? -t.Amount : 0m,
							_ => 0m
						};

					if (outflow <= 0)
					{
						continue;
					}

					monthlyOut += outflow;

					if (t.CreatedAt >= dayStart)
					{
						dailyOut += outflow;
					}
				}

				result[account.Id] =
					new CashAccountBalanceViewModel
					{
						Id = account.Id,
						Name = account.Name,
						AccountNumber = account.AccountNumber,
						Balance = await CalculateBalanceAsync(account.Id),
						TransactionCount =
							await _context.TreasuryTransactions
								.CountAsync(x =>
									x.CashAccountId == account.Id ||
									x.TransferToCashAccountId == account.Id),
						IsActive = account.IsActive,
						DailyLimit = account.DailyLimit,
						MonthlyLimit = account.MonthlyLimit,
						DailyConsumed = dailyOut,
						MonthlyConsumed = monthlyOut
					};
			}

			return result;
		}

		// =============================================
		// حساب المصروفات للصرف الحر:
		// قائمة حسابات من نوع "مصروف" (Expense) نشطة،
		// تُعرض في واجهة الصرف لاختيار حساب المصروف
		// المقابل عند الصرف بلا طرف (لا عميل/مورد/موظف).
		// =============================================
		private async Task<List<KeyValuePair<int, string>>>
			LoadExpenseAccountsAsync()
		{
			return await _context.ChartAccounts
				.AsNoTracking()
				.Where(x =>
					x.Type == AccountType.Expense &&
					x.IsActive)
				.OrderBy(x => x.Name)
				.Select(x =>
					new KeyValuePair<int, string>(
						x.Id,
						x.Name))
				.ToListAsync();
		}

		private async Task LoadPartyDataAsyncPay(
			TreasuryPayViewModel model)
		{
			model.Accounts = await LoadAccountsAsync();
			model.Customers = await LoadCustomersAsync();
			model.Suppliers = await LoadSuppliersAsync();
			model.Employees = await LoadEmployeesAsync();
			await LoadExpenseAccountsAsync(model);

			model.AccountsProviders =
				await _context.CashAccounts
					.AsNoTracking()
					.Select(x =>
						new { x.Id, x.Provider })
					.ToDictionaryAsync(
						x => x.Id,
						x => (int)x.Provider);

			model.AccountCards =
				await LoadAccountCardsAsync();
		}

		// =============================================
		// قائمة حسابات المصروف (للقائمة المنسدلة في واجهة
		// سند الصرف) — تُستخدم عند الصرف الحر بلا طرف
		// لبناء قيد: مدين [المصروف] / دائن [الخزينة].
		// =============================================
		private async Task<List<KeyValuePair<int, string>>>
			LoadIncomeAccountsAsync()
		{
			return await _context.ChartAccounts
				.AsNoTracking()
				.Where(x =>
					x.Type == AccountType.Revenue &&
					x.IsActive)
				.OrderBy(x => x.Name)
				.Select(x =>
					new KeyValuePair<int, string>(
						x.Id,
						x.Name))
				.ToListAsync();
		}

		private async Task LoadExpenseAccountsAsync(
			TreasuryPayViewModel model)
		{
			model.ExpenseAccounts =
				await _context.ChartAccounts
					.AsNoTracking()
					.Where(x =>
						x.Type == AccountType.Expense &&
						x.IsActive)
					.OrderBy(x => x.Name)
					.Select(x =>
						new KeyValuePair<int, string>(
							x.Id,
							x.Name))
					.ToListAsync();
		}
	}
}
