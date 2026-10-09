using System.Security.Claims;
using AccountingSystem.Data;
using AccountingSystem.Filters;
using AccountingSystem.Models;
using AccountingSystem.Models.ViewModels.Accounting;
using AccountingSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Controllers
{
	public class AccountingController : Controller
	{
		private readonly ApplicationDbContext _context;
		private readonly ISequenceService _sequence;

		public AccountingController(
			ApplicationDbContext context,
			ISequenceService sequence)
		{
			_context = context;
			_sequence = sequence;
		}

		// =========================================
		// شجرة الحسابات
		// =========================================

		[HttpGet]
		[RequirePermission("accounting.view")]
		public async Task<IActionResult> Accounts()
		{
			var model =
				new ChartAccountsViewModel();

			var accounts =
				await _context.ChartAccounts
					.AsNoTracking()
					.OrderBy(x => x.Name)
					.ToListAsync();

			// =========================================
			// بناء الشجرة ترتيبًا هرميًا
			// (الجذور أولًا ثم الفروع تحت آبائها)
			// =========================================

			var rows = new List<ChartAccountRowViewModel>();

			// الأبناء مرتبة بأبهم — بلا مفاتيح null
			// (Dictionary لا يجيز مفتاحًا فارغًا)
			var byParent = accounts
				.Where(x => x.ParentId.HasValue)
				.GroupBy(x => x.ParentId!.Value)
				.ToDictionary(g => g.Key, g => g.ToList());

			var roots = accounts
				.Where(x => !x.ParentId.HasValue)
				.ToList();

			var resolved = new HashSet<int>();

			void Walk(ChartAccount node, int depth)
			{
				if (!resolved.Add(node.Id))
				{
					return;
				}

				rows.Add(new ChartAccountRowViewModel
				{
					Id = node.Id,
					Code = node.Code,
					Name = node.Name,
					Type = node.Type,
					IsActive = node.IsActive,
					IsSystem = node.IsSystem,
					Depth = depth
				});

				if (byParent.TryGetValue(node.Id, out var children))
				{
					foreach (var child in children
						.OrderBy(x => x.Code ?? x.Name))
					{
						Walk(child, depth + 1);
					}
				}
			}

			foreach (var root in roots
				.OrderBy(x => x.Code ?? x.Name))
			{
				Walk(root, 0);
			}

			// إضافة أي حسابات يتيمة (بلا أب) لم تُعرض بعد
			foreach (var orphan in accounts
				.Where(x => !resolved.Contains(x.Id))
				.OrderBy(x => x.Code ?? x.Name))
			{
				Walk(orphan, 0);
			}

			model.Rows = rows;

			// =========================================
			// خيارات الأب (هيراركي) لصفحة الإضافة
			// =========================================

			model.ParentOptions = rows
				.Select(x => new ChartAccountParentOptionViewModel
				{
					Id = x.Id,
					Name = x.Name,
					Type = x.Type,
					Depth = x.Depth
				})
				.ToList();

			return View(model);
		}

		// =========================================
		// إضافة حساب - POST
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("accounting.manage")]
		public async Task<IActionResult> CreateAccount(
			ChartAccountCreateViewModel model)
		{
			model.Name = model.Name?.Trim() ?? string.Empty;
			model.Code =
				string.IsNullOrWhiteSpace(model.Code)
					? null
					: model.Code.Trim();

			if (!ModelState.IsValid)
			{
				TempData["Error"] =
					"يرجى التحقق من بيانات الحساب.";

				return RedirectToAction(nameof(Accounts));
			}

			var nameExists =
				await _context.ChartAccounts
					.AnyAsync(x => x.Name == model.Name);

			if (nameExists)
			{
				TempData["Error"] =
					"يوجد حساب بنفس الاسم.";

				return RedirectToAction(nameof(Accounts));
			}

			if (!string.IsNullOrWhiteSpace(model.Code))
			{
				var codeExists =
					await _context.ChartAccounts
						.AnyAsync(x => x.Code == model.Code);

				if (codeExists)
				{
					TempData["Error"] =
						"الكود مستخدم بالفعل.";

					return RedirectToAction(nameof(Accounts));
				}
			}

			// =========================================
			// التحقق من الحساب الأب إن وُجد
			// =========================================

			if (model.ParentId.HasValue)
			{
				var parent = await _context.ChartAccounts
					.AsNoTracking()
					.FirstOrDefaultAsync(
						x => x.Id == model.ParentId.Value &&
							x.IsActive);

				if (parent == null)
				{
					TempData["Error"] =
						"الحساب الأب المختار غير موجود أو غير نشط.";

					return RedirectToAction(nameof(Accounts));
				}

				// =========================================
				// منع إضافة حساب فرعي مباشرة تحت حساب
				// ذي طبيعة مخالفة (الأب يأخذ نفس الطبيعة)
				// =========================================

				if (parent.Type != model.Type)
				{
					TempData["Error"] =
						$"لا يمكن إضافة حساب من نوع «{model.Type}» " +
						$"تحت حساب أب من نوع «{parent.Type}». " +
						"يجب أن يطابق الحساب الفرعي طبيعة الأب.";

					return RedirectToAction(nameof(Accounts));
				}
			}

			_context.ChartAccounts.Add(
				new ChartAccount
				{
					Code = model.Code,
					Name = model.Name,
					Type = model.Type,
					ParentId = model.ParentId,
					IsActive = true,
					CreatedAt = DateTime.UtcNow
				});

			await _context.SaveChangesAsync();

			TempData["Success"] =
				$"تمت إضافة الحساب «{model.Name}».";

			return RedirectToAction(nameof(Accounts));
		}

		// =========================================
		// تفعيل/إيقاف حساب
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("accounting.manage")]
		public async Task<IActionResult> ToggleAccount(int id)
		{
			var account =
				await _context.ChartAccounts
					.FirstOrDefaultAsync(x => x.Id == id);

			if (account == null)
			{
				return NotFound();
			}

			// =========================================
			// الحسابات النظامية لا يجوز إيقافها
			// =========================================

			if (account.IsSystem)
			{
				TempData["Error"] =
					"لا يمكن إيقاف حساب نظامي («" +
					account.Name +
					"») لأنه أساسي للدورة المحاسبية.";

				return RedirectToAction(nameof(Accounts));
			}

			if (account.IsActive &&
				await _context.ChartAccounts
					.AnyAsync(x =>
						x.ParentId == account.Id &&
						x.IsActive))
			{
				TempData["Error"] =
					"لا يمكن إيقاف حساب رئيسي يحتوي على حسابات فرعية نشطة.";

				return RedirectToAction(nameof(Accounts));
			}

			account.IsActive = !account.IsActive;

			await _context.SaveChangesAsync();

			TempData["Success"] =
				account.IsActive
					? "تم تفعيل الحساب."
					: "تم إيقاف الحساب.";

			return RedirectToAction(nameof(Accounts));
		}

		// =========================================
		// قائمة القيود
		// =========================================

		[HttpGet]
		[RequirePermission("accounting.journal.view")]
		public async Task<IActionResult> Journal(
			bool? postedOnly)
		{
			var model =
				new JournalIndexViewModel
				{
					PostedOnly = postedOnly
				};

			var query =
				_context.JournalEntries
					.AsNoTracking()
					.Include(x => x.Lines)
					.Include(x => x.CreatedByUser)
					.AsQueryable();

			if (postedOnly.HasValue &&
				postedOnly.Value)
			{
				query =
					query.Where(x => x.IsPosted);
			}

			var entries =
				await query
					.OrderByDescending(x => x.EntryDate)
					.ThenByDescending(x => x.Id)
					.Take(200)
					.ToListAsync();

			foreach (var entry in entries)
			{
				var debits =
					entry.Lines.Sum(x => x.Debit);

				var credits =
					entry.Lines.Sum(x => x.Credit);

				model.Rows.Add(
					new JournalRowViewModel
					{
						Id = entry.Id,
						EntryNumber = entry.EntryNumber,
						EntryDate = entry.EntryDate,
						Description = entry.Description,
						IsPosted = entry.IsPosted,
						PostedAt = entry.PostedAt,
						LinesCount = entry.Lines.Count,
						DebitTotal = debits,
						CreditTotal = credits,
						CreatedByUserName =
							entry.CreatedByUser?.Phone
					});
			}

			return View(model);
		}

		// =========================================
		// إنشاء قيد - GET
		// =========================================

		[HttpGet]
		[RequirePermission("accounting.journal.create")]
		public async Task<IActionResult> Create()
		{
			var model =
				new JournalEntryViewModel();

			model.Lines.Add(
				new JournalLineViewModel());

			await LoadAccountsDataAsync(model);

			return View(model);
		}

		// =========================================
		// إنشاء قيد - POST
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("accounting.journal.create")]
		public async Task<IActionResult> Create(
			JournalEntryViewModel model)
		{
			model.Description =
				string.IsNullOrWhiteSpace(model.Description)
					? null
					: model.Description.Trim();

			var conflictingLine =
				model.Lines?.FirstOrDefault(x =>
					x != null &&
					x.ChartAccountId > 0 &&
					x.Debit > 0 &&
					x.Credit > 0);

			if (conflictingLine != null)
			{
				ModelState.AddModelError(
					string.Empty,
					"لا يجوز إدخال مدين ودائن في نفس السطر المالي.");

				await LoadAccountsDataAsync(model);

				return View(model);
			}

			model.Lines =
				model.Lines?
						.Where(x =>
							x != null &&
							x.ChartAccountId > 0 &&
							(x.Debit > 0) ^ (x.Credit > 0))
						.ToList()
					?? new List<JournalLineViewModel>();

			if (!ModelState.IsValid ||
				model.Lines.Count < 2)
			{
				ModelState.AddModelError(
					string.Empty,
					"يجب إدخال قيد بحد أدنى سطرين (مدين ودائن).");

				await LoadAccountsDataAsync(model);

				return View(model);
			}

			var debitTotal =
				model.Lines.Sum(x => x.Debit);

			var creditTotal =
				model.Lines.Sum(x => x.Credit);

			if (Math.Abs(debitTotal - creditTotal) > AppConstants.BalanceTolerance)
			{
				ModelState.AddModelError(
					string.Empty,
					"مجموع المدين يجب أن يساوي مجموع الدائن.");

				await LoadAccountsDataAsync(model);

				return View(model);
			}

			var accountIds =
				model.Lines
					.Select(x => x.ChartAccountId)
					.Distinct()
					.ToList();

			var validCount =
				await _context.ChartAccounts
					.CountAsync(x =>
						x.IsActive &&
						accountIds.Contains(x.Id));

			if (validCount != accountIds.Count)
			{
				ModelState.AddModelError(
					string.Empty,
					"يوجد حساب غير موجود أو غير نشط.");

				await LoadAccountsDataAsync(model);

				return View(model);
			}

			var entry =
				new JournalEntry
				{
					EntryNumber =
						await GenerateEntryNumberAsync(),
					EntryDate = model.EntryDate,
					Description = model.Description,
					IsPosted = false,
					CreatedByUserId = CurrentUserId(),
					CreatedAt = DateTime.UtcNow
				};

			_context.JournalEntries.Add(entry);

			foreach (var line in model.Lines)
			{
				entry.Lines.Add(
					new JournalEntryLine
					{
						ChartAccountId = line.ChartAccountId,
						Debit = line.Debit,
						Credit = line.Credit
					});
			}

			await _context.SaveChangesAsync();

			TempData["Success"] =
				$"تم إنشاء القيد {entry.EntryNumber} كمسودة.";

			return RedirectToAction(nameof(Journal));
		}

		// =========================================
		// تعديل قيد - GET (مسودة فقط)
		// =========================================

		[HttpGet]
		[RequirePermission("accounting.journal.edit")]
		public async Task<IActionResult> Edit(int id)
		{
			var entry =
				await _context.JournalEntries
					.Include(x => x.Lines)
					.FirstOrDefaultAsync(x => x.Id == id);

			if (entry == null)
			{
				return NotFound();
			}

			if (entry.IsPosted)
			{
				TempData["Error"] =
					"لا يمكن تعديل قيد معتمد.";

				return RedirectToAction(nameof(Journal));
			}

			var model =
				new JournalEntryViewModel
				{
					Id = entry.Id,
					EntryDate = entry.EntryDate,
					Description = entry.Description,
					Lines =
						entry.Lines
							.Select(x =>
								new JournalLineViewModel
								{
									ChartAccountId =
										x.ChartAccountId,
									Debit = x.Debit,
									Credit = x.Credit
								})
							.ToList()
				};

			if (model.Lines.Count == 0)
			{
				model.Lines.Add(
					new JournalLineViewModel());
			}

			await LoadAccountsDataAsync(model);

			return View("Create", model);
		}

		// =========================================
		// تعديل قيد - POST (مسودة فقط)
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("accounting.journal.edit")]
		public async Task<IActionResult> Edit(
			JournalEntryViewModel model)
		{
			var entry =
				await _context.JournalEntries
					.Include(x => x.Lines)
					.FirstOrDefaultAsync(x => x.Id == model.Id);

			if (entry == null)
			{
				return NotFound();
			}

			if (entry.IsPosted)
			{
				TempData["Error"] =
					"لا يمكن تعديل قيد معتمد.";

				return RedirectToAction(nameof(Journal));
			}

			model.Description =
				string.IsNullOrWhiteSpace(model.Description)
					? null
					: model.Description.Trim();

			var conflictingLine =
				model.Lines?.FirstOrDefault(x =>
					x != null &&
					x.ChartAccountId > 0 &&
					x.Debit > 0 &&
					x.Credit > 0);

			if (conflictingLine != null)
			{
				ModelState.AddModelError(
					string.Empty,
					"لا يجوز إدخال مدين ودائن في نفس السطر المالي.");

				await LoadAccountsDataAsync(model);

				return View("Edit", model);
			}

			model.Lines =
				model.Lines?
						.Where(x =>
							x != null &&
							x.ChartAccountId > 0 &&
							(x.Debit > 0) ^ (x.Credit > 0))
						.ToList()
					?? new List<JournalLineViewModel>();

			if (!ModelState.IsValid ||
				model.Lines.Count < 2)
			{
				ModelState.AddModelError(
					string.Empty,
					"يجب إدخال قيد بحد أدنى سطرين (مدين ودائن).");

				await LoadAccountsDataAsync(model);

				return View("Create", model);
			}

			var debitTotal =
				model.Lines.Sum(x => x.Debit);

			var creditTotal =
				model.Lines.Sum(x => x.Credit);

			if (Math.Abs(debitTotal - creditTotal) > AppConstants.BalanceTolerance)
			{
				ModelState.AddModelError(
					string.Empty,
					"مجموع المدين يجب أن يساوي مجموع الدائن.");

				await LoadAccountsDataAsync(model);

				return View("Create", model);
			}

			var accountIds =
				model.Lines
					.Select(x => x.ChartAccountId)
					.Distinct()
					.ToList();

			var validCount =
				await _context.ChartAccounts
					.CountAsync(x =>
						x.IsActive &&
						accountIds.Contains(x.Id));

			if (validCount != accountIds.Count)
			{
				ModelState.AddModelError(
					string.Empty,
					"يوجد حساب غير موجود أو غير نشط.");

				await LoadAccountsDataAsync(model);

				return View("Create", model);
			}

			entry.EntryDate = model.EntryDate;
			entry.Description = model.Description;

			_context.JournalEntryLines.RemoveRange(entry.Lines);

			foreach (var line in model.Lines)
			{
				_context.JournalEntryLines.Add(
					new JournalEntryLine
					{
						JournalEntryId = entry.Id,
						ChartAccountId = line.ChartAccountId,
						Debit = line.Debit,
						Credit = line.Credit
					});
			}

			await _context.SaveChangesAsync();

			TempData["Success"] =
				$"تم تحديث القيد {entry.EntryNumber}.";

			return RedirectToAction(nameof(Journal));
		}

		// =========================================
		// اعتماد قيد
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("accounting.journal.approve")]
		public async Task<IActionResult> Approve(int id)
		{
			var entry =
				await _context.JournalEntries
					.FirstOrDefaultAsync(x => x.Id == id);

			if (entry == null)
			{
				return NotFound();
			}

			if (entry.IsPosted)
			{
				TempData["Error"] =
					"القيد معتمد بالفعل.";

				return RedirectToAction(nameof(Journal));
			}

			var debits =
				await _context.JournalEntryLines
					.Where(x => x.JournalEntryId == id)
					.SumAsync(x => x.Debit);

			var credits =
				await _context.JournalEntryLines
					.Where(x => x.JournalEntryId == id)
					.SumAsync(x => x.Credit);

			if (Math.Abs(debits - credits) > AppConstants.BalanceTolerance)
			{
				TempData["Error"] =
					"لا يمكن اعتماد قيد غير متوازن.";

				return RedirectToAction(nameof(Journal));
			}

			entry.IsPosted = true;
			entry.PostedAt = DateTime.UtcNow;

			await _context.SaveChangesAsync();

			TempData["Success"] =
				$"تم اعتماد القيد {entry.EntryNumber}.";

			return RedirectToAction(nameof(Journal));
		}

		// =========================================
		// حذف قيد مسودة
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("accounting.journal.edit")]
		public async Task<IActionResult> Delete(int id)
		{
			var entry =
				await _context.JournalEntries
					.FirstOrDefaultAsync(x => x.Id == id);

			if (entry == null)
			{
				return NotFound();
			}

			if (entry.IsPosted)
			{
				TempData["Error"] =
					"لا يمكن حذف قيد معتمد.";

				return RedirectToAction(nameof(Journal));
			}

			_context.JournalEntries.Remove(entry);

			await _context.SaveChangesAsync();

			TempData["Success"] =
				"تم حذف القيد المسودة.";

			return RedirectToAction(nameof(Journal));
		}

		// =========================================
		// ميزان المراجعة
		// =========================================

		[HttpGet]
		[RequirePermission("report.accounting")]
		public async Task<IActionResult> TrialBalance()
		{
			var model =
				new TrialBalanceViewModel();

			var lines =
				await _context.JournalEntryLines
					.AsNoTracking()
					.Include(x => x.ChartAccount)
					.Where(x => x.JournalEntry!.IsPosted)
					.ToListAsync();

			var groups =
				lines
					.Where(x => x.ChartAccount != null)
					.GroupBy(x => x.ChartAccountId);

			foreach (var group in groups)
			{
				var account =
					group.First().ChartAccount!;

				if (account == null)
				{
					continue;
				}

				var debit =
					group.Sum(x => x.Debit);

				var credit =
					group.Sum(x => x.Credit);

				model.Rows.Add(
					new TrialBalanceRowViewModel
					{
						Code = account.Code,
						Name = account.Name,
						Type = account.Type,
						Debit = debit,
						Credit = credit,
						Balance = debit - credit
					});

				model.TotalDebit += debit;
				model.TotalCredit += credit;
			}

			model.Rows =
				model.Rows
					.OrderBy(x => x.Name)
					.ToList();

			return View(model);
		}

		// =========================================
		// دفتر الأستاذ
		// =========================================

		[HttpGet]
		[RequirePermission("accounting.view")]
		public async Task<IActionResult> Ledger(
			int? accountId,
			DateTime? fromDate,
			DateTime? toDate)
		{
			var model =
				new LedgerViewModel
				{
					AccountId = accountId ?? 0,
					FromDate =
						(fromDate ??
							new DateTime(
								DateTime.Today.Year,
								DateTime.Today.Month,
								1)).Date,
					ToDate =
						(toDate ?? DateTime.Today).Date
				};

			model.Accounts =
				await _context.ChartAccounts
					.AsNoTracking()
					.Where(x => !x.Children.Any())
					.OrderBy(x => x.Name)
					.Select(x =>
						new KeyValuePair<int, string>(
							x.Id,
							x.Name))
					.ToListAsync();

			if (model.AccountId <= 0 &&
				model.Accounts.Count > 0)
			{
				model.AccountId = model.Accounts[0].Key;
			}

			var account =
				await _context.ChartAccounts
					.AsNoTracking()
					.FirstOrDefaultAsync(x =>
						x.Id == model.AccountId);

			if (account == null)
			{
				return View(model);
			}

			model.AccountName = account.Name;
			model.AccountType = account.Type;

			var from =
				model.FromDate;

			var to =
				model.ToDate.AddDays(1);

			var opening =
				await _context.JournalEntryLines
					.AsNoTracking()
					.Where(x =>
						x.ChartAccountId == model.AccountId &&
						x.JournalEntry!.IsPosted &&
						x.JournalEntry.EntryDate < from)
					.SumAsync(x => x.Debit - x.Credit);

			model.OpeningBalance = opening;

			var lines =
				await _context.JournalEntryLines
					.AsNoTracking()
					.Include(x => x.JournalEntry)
					.Where(x =>
						x.ChartAccountId == model.AccountId &&
						x.JournalEntry!.IsPosted &&
						x.JournalEntry.EntryDate >= from &&
						x.JournalEntry.EntryDate < to)
					.OrderBy(x => x.JournalEntry!.EntryDate)
					.ThenBy(x => x.Id)
					.ToListAsync();

			var balance =
				opening;

			foreach (var line in lines)
			{
				balance += line.Debit - line.Credit;

				model.TotalDebit += line.Debit;
				model.TotalCredit += line.Credit;

				model.Rows.Add(
					new LedgerRowViewModel
					{
						Date = line.JournalEntry!.EntryDate,
						EntryNumber =
							line.JournalEntry.EntryNumber,
						Description =
							line.JournalEntry.Description,
						Debit = line.Debit,
						Credit = line.Credit,
						Balance = balance
					});
			}

			model.ClosingBalance = balance;

			return View(model);
		}

		// =========================================
		// قائمة الدخل
		// =========================================

		[HttpGet]
		[RequirePermission("report.accounting")]
		public async Task<IActionResult> IncomeStatement(
			DateTime? fromDate,
			DateTime? toDate)
		{
			var model =
				new IncomeStatementViewModel
				{
					FromDate =
						fromDate ??
							new DateTime(DateTime.Today.Year, 1, 1),
					ToDate =
						toDate ?? DateTime.Today
				};

			var from =
				model.FromDate.Date;

			var to =
				model.ToDate.Date.AddDays(1);

			var lines =
				await _context.JournalEntryLines
					.AsNoTracking()
					.Include(x => x.ChartAccount)
					.Where(x =>
						x.JournalEntry!.IsPosted &&
						x.JournalEntry.EntryDate >= from &&
						x.JournalEntry.EntryDate < to)
					.ToListAsync();

			var groups =
				lines
					.Where(x => x.ChartAccount != null)
					.GroupBy(x => x.ChartAccountId);

			foreach (var group in groups)
			{
				var account =
					group.First().ChartAccount!;

				if (account.Type == AccountType.Revenue)
				{
					var amount =
						group.Sum(x => x.Credit - x.Debit);

					model.RevenueLines.Add(
						new IncomeStatementLineViewModel
						{
							Code = account.Code,
							Name = account.Name,
							Amount = amount
						});

					model.GrossSales += amount;
					model.TotalRevenue += amount;
				}
				else if (account.Type == AccountType.ContraRevenue)
				{
					var amount =
						group.Sum(x => x.Debit - x.Credit);

					model.TotalDiscounts += amount;
					model.TotalRevenue -= amount;
				}
				else if (account.Type == AccountType.Expense)
				{
					var amount =
						group.Sum(x => x.Debit - x.Credit);

					var line =
						new IncomeStatementLineViewModel
						{
							Code = account.Code,
							Name = account.Name,
							Amount = amount
						};

					model.ExpenseLines.Add(line);

					var category =
						CategorizeExpense(
							account.Code,
							account.Name);

					var current =
						model.ExpenseCategories
							.FirstOrDefault(x =>
								x.Name == category);

					if (current == null)
					{
						current =
							new IncomeStatementCategoryViewModel
							{
								Name = category
							};

						model.ExpenseCategories.Add(current);
					}

					current.Lines.Add(line);
					current.Amount += amount;

					model.TotalExpenses += amount;
				}
			}

			model.RevenueLines =
				model.RevenueLines
					.OrderBy(x => x.Name)
					.ToList();

			model.ExpenseLines =
				model.ExpenseLines
					.OrderBy(x => x.Name)
					.ToList();

			model.ExpenseCategories =
				model.ExpenseCategories
					.OrderBy(x =>
						ExpenseCategoryOrder(x.Name))
					.ToList();

			model.NetIncome =
				model.TotalRevenue - model.TotalExpenses;

			return View(model);
		}

		// =========================================
		// الميزانية العمومية
		// =========================================

		[HttpGet]
		[RequirePermission("report.accounting")]
		public async Task<IActionResult> BalanceSheet(
			DateTime? asOfDate)
		{
			var model =
				new BalanceSheetViewModel
				{
					AsOfDate =
						(asOfDate ?? DateTime.Today).Date
				};

			var to =
				model.AsOfDate.AddDays(1);

			var lines =
				await _context.JournalEntryLines
					.AsNoTracking()
					.Include(x => x.ChartAccount)
					.Where(x =>
						x.JournalEntry!.IsPosted &&
						x.JournalEntry.EntryDate < to)
					.ToListAsync();

			var groups =
				lines
					.Where(x => x.ChartAccount != null)
					.GroupBy(x => x.ChartAccountId);

			decimal revenue = 0;
			decimal expense = 0;

			foreach (var group in groups)
			{
				var account =
					group.First().ChartAccount!;

				var debit =
					group.Sum(x => x.Debit);

				var credit =
					group.Sum(x => x.Credit);

				switch (account.Type)
				{
					case AccountType.Asset:
						var asset = debit - credit;
						model.Assets.Add(
							new BalanceSheetLineViewModel
							{
								Code = account.Code,
								Name = account.Name,
								Amount = asset
							});
						model.TotalAssets += asset;
						break;

					case AccountType.Liability:
						var liability = credit - debit;
						model.Liabilities.Add(
							new BalanceSheetLineViewModel
							{
								Code = account.Code,
								Name = account.Name,
								Amount = liability
							});
						model.TotalLiabilities += liability;
						break;

					case AccountType.Equity:
						var equity = credit - debit;
						model.Equity.Add(
							new BalanceSheetLineViewModel
							{
								Code = account.Code,
								Name = account.Name,
								Amount = equity
							});
						model.TotalEquity += equity;
						break;

					case AccountType.Revenue:
						revenue += credit - debit;
						break;

					case AccountType.Expense:
						expense += debit - credit;
						break;
				}
			}

			model.NetIncome = revenue - expense;

			model.TotalEquity += model.NetIncome;

			model.TotalLiabilitiesAndEquity =
				model.TotalLiabilities + model.TotalEquity;

			model.IsBalanced =
				Math.Abs(
					model.TotalAssets -
					model.TotalLiabilitiesAndEquity) < AppConstants.BudgetTolerance;

			model.Assets =
				model.Assets.OrderBy(x => x.Name).ToList();

			model.Liabilities =
				model.Liabilities.OrderBy(x => x.Name).ToList();

			model.Equity =
				model.Equity.OrderBy(x => x.Name).ToList();

			return View(model);
		}

		// =========================================
		// أدوات مساعدة
		// =========================================

		private async Task LoadAccountsDataAsync(
			JournalEntryViewModel model)
		{
			model.Accounts =
				await _context.ChartAccounts
					.AsNoTracking()
					.Where(x => x.IsActive)
					.OrderBy(x => x.Name)
					.Select(x =>
						new KeyValuePair<int, string>(
							x.Id,
							x.Name))
					.ToListAsync();
		}

		private async Task<string> GenerateEntryNumberAsync()
		{
			return await _sequence.NextFormattedAsync(
				"JRN",
				"JE",
				DateTime.Today,
				digits: 4);
		}

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

		// =========================================
		// تصنيف المصروفات في قائمة الدخل
		// =========================================

		private static string CategorizeExpense(
			string? code,
			string name)
		{
			var value =
				(code + " " + name).Trim();

			var keywords = new[]
			{
				new[]
				{
					"تكلفة البضاعة",
					"تكلفة المبيعات",
					"تكلفة مخزون",
					"المشتريات"
				},
				new[]
				{
					"أجور",
					"رواتب",
					"مرتبات",
					"تأمينات اجتماعية",
					"بدلات",
					"عمولات بيع"
				},
				new[]
				{
					"إيجار",
					"كهرباء",
					"مياه",
					"هاتف",
					"إنترنت",
					"اتصالات",
					"قرطاسية",
					"لوازم مكتبية",
					"صيانة",
					"نظافة",
					"استشارات",
					"مصاريف إدارية",
					"إداري",
					"عمومية",
					"تأمينات عامة",
					"إهلاك",
					"استهلاك"
				},
				new[]
				{
					"دعاية",
					"إعلان",
					"تسويق",
					"مبيعات وتسويق",
					"توزيع",
					"مصاريف نقل",
					"شحن",
					"سفر"
				},
				new[]
				{
					"فوائد",
					"قروض",
					"عمولات بنك",
					"مصاريف بنكية",
					"تمويل"
				},
				new[]
				{
					"ضرائب",
					"رسوم",
					"غرامات",
					"تبرعات",
					"خسائر"
				}
			};

			for (var i = 0; i < keywords.Length; i++)
			{
				foreach (var key in keywords[i])
				{
					if (value.Contains(
						key,
						StringComparison.OrdinalIgnoreCase))
					{
						return keywordCategories[i];
					}
				}
			}

			return "مصروفات أخرى";
		}

		private static readonly string[] keywordCategories = new[]
		{
			"تكلفة المبيعات",
			"مصروفات تشغيلية وأجور",
			"مصروفات إدارية وعمومية",
			"مصروفات بيعية وتسويقية",
			"مصروفات تمويلية",
			"مصروفات أخرى"
		};

		private static int ExpenseCategoryOrder(
			string category)
		{
			var index =
				Array.IndexOf(
					keywordCategories,
					category);

			return index < 0
				? keywordCategories.Length
				: index;
		}
	}
}