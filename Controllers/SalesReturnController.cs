using AccountingSystem.Data;
using AccountingSystem.Filters;
using AccountingSystem.Models;
using AccountingSystem.Models.ViewModels.Returns;
using AccountingSystem.Services.EmployeeScope;
using AccountingSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AccountingSystem.Controllers
{
	[Authorize]
	public class SalesReturnController : Controller
	{
		private readonly ApplicationDbContext _context;
		private readonly IEmployeeScopeService _employeeScopeService;
		private readonly IPostingService _postingService;
		private readonly IDocumentNumberService _documentNumberService;

		public SalesReturnController(
			ApplicationDbContext context,
			IEmployeeScopeService employeeScopeService,
			IPostingService postingService,
			IDocumentNumberService documentNumberService)
		{
			_context = context;
			_employeeScopeService = employeeScopeService;
			_postingService = postingService;
			_documentNumberService = documentNumberService;
		}

		// =========================================
		// عرض المرتجعات
		// =========================================

		[HttpGet]
		[RequirePermission("sales.return.view")]
		public async Task<IActionResult> Index()
		{
			var scope = await _employeeScopeService.GetScopeAsync();

			var query = _context.SalesReturnInvoices
				.AsNoTracking()
				.Include(x => x.Customer)
				.Include(x => x.Store)
				.Include(x => x.OriginalSalesInvoice)
				.Include(x => x.Items)
				.AsQueryable();

			if (scope.IsRestricted)
			{
				var storeIds = scope.StoreIds.ToList();

				query = storeIds.Count == 0
					? query.Where(_ => false)
					: query.Where(x => storeIds.Contains(x.StoreId));
			}

			var invoices = await query
				.OrderByDescending(x => x.ReturnDate)
				.ThenByDescending(x => x.Id)
				.Take(AppConstants.DefaultPageSize)
				.ToListAsync();

			var model = new ReturnListViewModel
			{
				Type = "sales"
			};

			foreach (var invoice in invoices)
			{
				model.Rows.Add(
					new ReturnRowViewModel
					{
						Id = invoice.Id,
						InvoiceNumber = invoice.InvoiceNumber,
						PartyName = invoice.Customer?.Name ?? "-",
						StoreName = invoice.Store?.Name ?? "-",
						ReturnDate = invoice.ReturnDate,
						TotalAmount = invoice.TotalAmount,
						ItemCount = invoice.Items.Count,
						Reason = invoice.Reason
					});
			}

			return View(model);
		}

		// =========================================
		// إنشاء مرتجع - GET
		// =========================================

		[HttpGet]
		[RequirePermission("sales.return.create")]
		public async Task<IActionResult> Create(
			int? invoiceId,
			int? customerId)
		{
			var model = new SalesReturnViewModel();

			await LoadLookupsAsync(model);

			if (invoiceId.HasValue && invoiceId.Value > 0)
			{
				var ok = await LoadInvoiceForReturnAsync(
					model,
					invoiceId.Value);

				if (!ok)
				{
					TempData["Error"] =
						"الفاتورة غير متاحة للمرتجع.";

					return RedirectToAction(nameof(Create));
				}
			}
			else if (customerId.HasValue && customerId.Value > 0)
			{
				await LoadCustomerInvoicesAsync(
					model,
					customerId.Value);
			}

			return View(model);
		}

		// =========================================
		// إنشاء مرتجع - POST
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("sales.return.create")]
		public async Task<IActionResult> Create(
			SalesReturnViewModel model)
		{
			model.Reason =
				string.IsNullOrWhiteSpace(model.Reason)
					? null
					: model.Reason.Trim();

			// =====================================
			// التحقق من المدخلات
			// =====================================

			if (!model.OriginalSalesInvoiceId.HasValue ||
				model.OriginalSalesInvoiceId.Value <= 0)
			{
				ModelState.AddModelError(
					nameof(model.OriginalSalesInvoiceId),
					"يجب اختيار فاتورة البيع الأصلية.");
			}

			model.Items = model.Items
				.Where(x => x.Quantity > 0)
				.ToList();

			if (model.Items.Count == 0)
			{
				ModelState.AddModelError(
					string.Empty,
					"يجب إدخال بند واحد على الأقل بكمية أكبر من صفر.");
			}

			if (model.RefundToCash &&
				(!model.RefundCashAccountId.HasValue ||
				 model.RefundCashAccountId.Value <= 0))
			{
				ModelState.AddModelError(
					nameof(model.RefundCashAccountId),
					"يجب اختيار خزنة الرد النقدي.");
			}

			if (!ModelState.IsValid)
			{
				await ReloadForErrorAsync(model);
				return View(model);
			}

			// =====================================
			// تحميل الفاتورة الأصلية
			// =====================================

			var originalInvoice = await _context.SalesInvoices
				.Include(x => x.Items)
					.ThenInclude(x => x.LotAllocations)
						.ThenInclude(x => x.StockLot)
				.FirstOrDefaultAsync(x =>
					x.Id == model.OriginalSalesInvoiceId!.Value);

			if (originalInvoice == null ||
				originalInvoice.Status != SalesInvoiceStatus.Confirmed)
			{
				ModelState.AddModelError(
					nameof(model.OriginalSalesInvoiceId),
					"الفاتورة الأصلية غير متاحة للمرتجع.");

				await ReloadForErrorAsync(model);
				return View(model);
			}

			// التحقق من صلاحية الوصول للمخزن
			var scope = await _employeeScopeService.GetScopeAsync();

			if (!scope.CanAccessStore(originalInvoice.StoreId))
			{
				TempData["Error"] =
					"ليس لديك صلاحية على مخزن هذه الفاتورة.";

				return RedirectToAction(nameof(Index));
			}

			// =====================================
			// إنشاء المرتجع
			// =====================================

			var returnInvoice = new SalesReturnInvoice
			{
				InvoiceNumber = await _documentNumberService.GenerateNumberAsync("SRT", "SalesReturnInvoices", DateTime.Today.ToString("yyyyMMdd")),
				OriginalSalesInvoiceId = originalInvoice.Id,
				CustomerId = originalInvoice.CustomerId ?? 0,
				StoreId = originalInvoice.StoreId,
				BranchId = originalInvoice.BranchId,
				ReturnDate = model.ReturnDate,
				Reason = model.Reason,
				IsPosted = true,
				RefundToCash = model.RefundToCash,
				RefundCashAccountId = model.RefundToCash
					? model.RefundCashAccountId
					: null,
				CreatedByUserId = CurrentUserId(),
				CreatedAt = DateTime.UtcNow
			};

			if (returnInvoice.CustomerId == 0)
			{
				TempData["Error"] =
					"الفاتورة الأصلية ليس لها عميل — لا يمكن إنشاء مرتجع بيع.";

				return RedirectToAction(nameof(Index));
			}

			decimal totalReturn = 0;

			// =====================================
			// معالجة البنود
			// =====================================

			decimal totalCostOfGoodsSold = 0;

			// ✅ (N+1 fix) query واحد لكل البنود قبل الـ loop
			var saleItemIds =
				model.Items
					.Select(x => x.SalesInvoiceItemId)
					.Distinct()
					.ToList();

			var alreadyReturnedByItem =
				await _context.SalesReturnInvoiceItems
					.Where(x =>
						x.SalesInvoiceItemId.HasValue &&
						saleItemIds.Contains(x.SalesInvoiceItemId.Value))
					.GroupBy(x => x.SalesInvoiceItemId!.Value)
					.Select(g => new
					{
						ItemId = g.Key,
						Qty = g.Sum(x => x.QuantityInBaseUnit)
					})
					.ToDictionaryAsync(x => x.ItemId, x => x.Qty);

			foreach (var itemModel in model.Items)
			{
				var saleItem = originalInvoice.Items
					.FirstOrDefault(x =>
						x.Id == itemModel.SalesInvoiceItemId);

				if (saleItem == null)
				{
					ModelState.AddModelError(
						string.Empty,
						"يوجد بند غير موجود في الفاتورة الأصلية.");

					await ReloadForErrorAsync(model);
					return View(model);
				}

				// الكمية المرتجعة سابقًا من نفس البند
				alreadyReturnedByItem.TryGetValue(saleItem.Id, out var alreadyReturned);

				var returnable =
					saleItem.QuantityInBaseUnit - alreadyReturned;

				var requested =
					itemModel.Quantity * saleItem.ConversionFactor;

				if (requested > returnable + 0.0001m)
				{
					ModelState.AddModelError(
						string.Empty,
						$"الكمية المطلوبة أكبر من المتاح للإرجاع. " +
						$"المتاح: {returnable} — المطلوب: {requested}");

					await ReloadForErrorAsync(model);
					return View(model);
				}

				// =================================
				// إنشاء بند المرتجع
				// =================================

				var returnItem = new SalesReturnInvoiceItem
				{
					SalesInvoiceItemId = saleItem.Id,
					ProductId = saleItem.ProductId,
					UnitId = saleItem.UnitId,
					ConversionFactor = saleItem.ConversionFactor,
					Quantity = itemModel.Quantity,
					QuantityInBaseUnit = requested,
					UnitPrice = saleItem.UnitPrice,
					Total = itemModel.Quantity * saleItem.UnitPrice
				};

				// =================================
				// تخصيص الإرجاع على الـ Lots (LIFO)
				//
				// ✅ (إصلاح) الكمية المرتجعة سابقًا من كل lot
				// بدون ده، الإرجاع التاني لنفس الفاتورة كان
				// بيخصم من نفس الـ lot الأول بدل ما يكمل
				// على الـ lots اللي لسه فيها كمية.
				// =================================

				var alreadyReturnedByLot =
					await _context.SalesReturnItemLots
						.Where(x =>
							x.SalesReturnInvoiceItem!
								.SalesInvoiceItemId ==
							saleItem.Id)
						.GroupBy(x => x.StockLotId)
						.Select(g => new
						{
							StockLotId = g.Key,
							Qty = g.Sum(x =>
								x.QuantityBaseUnit)
						})
						.ToDictionaryAsync(
							x => x.StockLotId,
							x => x.Qty);

				var remaining = requested;
				decimal weightedCost = 0;

				var allocations = saleItem.LotAllocations
					.OrderByDescending(x => x.Id)
					.ToList();

				foreach (var alloc in allocations)
				{
					if (remaining <= 0.0001m)
					{
						break;
					}

					// خصم الكمية المرتجعة سابقًا من اللوت ده
					alreadyReturnedByLot.TryGetValue(
						alloc.StockLotId,
						out var alreadyFromThisLot);

					var availableFromLot =
						alloc.QuantityBaseUnit -
						alreadyFromThisLot;

					// اللوت ده خلص — نروح للي بعده
					if (availableFromLot <= 0.0001m)
					{
						continue;
					}

					var fromLot = Math.Min(
						availableFromLot,
						remaining);

					var lot = await _context.StockLots
						.FirstOrDefaultAsync(x =>
							x.Id == alloc.StockLotId);

					if (lot == null)
					{
						ModelState.AddModelError(
							string.Empty,
							"لم يتم العثور على دفعة المخزون.");
						await ReloadForErrorAsync(model);
						return View(model);
					}

					// زيادة الكمية في Lot المرتجع
					lot.QuantityRemaining += fromLot;

					returnItem.LotAllocations.Add(
						new SalesReturnItemLot
						{
							StockLotId = lot.Id,
							QuantityBaseUnit = fromLot,
							UnitCost = alloc.UnitCost,
							TotalCost = fromLot * alloc.UnitCost
						});

					weightedCost += fromLot * alloc.UnitCost;
					// ✅ (إصلاح) شيلنا totalCostOfGoodsSold من هنا
					//    — كان بيتراكم غلط جوه الـ loop
					remaining -= fromLot;
				}

				if (remaining > 0.0001m)
				{
					ModelState.AddModelError(
						string.Empty,
						"لا يمكن إرجاع الكمية المطلوبة — بعض الدفعات غير متوفرة.");
					await ReloadForErrorAsync(model);
					return View(model);
				}

				// ✅ (إصلاح) نضيف التكلفة مرة واحدة بعد ما نخلص كل الـ lots
				totalCostOfGoodsSold += weightedCost;

				returnItem.UnitCost = requested > 0
					? weightedCost / requested
					: 0;

				totalReturn += returnItem.Total;

				returnInvoice.Items.Add(returnItem);
			}

			// =====================================
			// الإجماليات
			// =====================================

			returnInvoice.TotalAmount = totalReturn;
			returnInvoice.RefundAmount =
				model.RefundToCash ? totalReturn : 0;

			_context.SalesReturnInvoices.Add(returnInvoice);

			// =====================================
			// إنشاء حركة خزينة للرد النقدي
			// =====================================

			if (model.RefundToCash &&
				model.RefundCashAccountId.HasValue)
			{
				_context.TreasuryTransactions.Add(
					new TreasuryTransaction
					{
						TransactionNumber =
							await GenerateRefundNumberAsync(),
						Type = TreasuryTransactionType.Pay,
						CashAccountId =
							model.RefundCashAccountId.Value,
						CustomerId = returnInvoice.CustomerId,
						Amount = totalReturn,
						Reason = "رد نقدي — مرتجع بيع",
						ReferenceDocument =
							returnInvoice.InvoiceNumber,
						AutoJournal = false,
						CreatedByUserId = CurrentUserId(),
						CreatedAt = DateTime.UtcNow
					});
			}

			// =====================================
			// الحفظ + الترحيل + معالجة التزامن
			// =====================================

			var strategy =
				_context.Database.CreateExecutionStrategy();

			string? transactionError = null;

			await strategy.ExecuteAsync(
				async () =>
				{
					await using var transaction =
						await _context.Database.BeginTransactionAsync();

					// =========================================
					// ✅ (Idempotency) قفل الفاتورة الأصلية
					//
					// يمنع طلبين مرتجع متزامنين على نفس
					// الفاتورة من تجاوز الكمية الأصلية.
					// =========================================

										// =========================================
					// قفل الصف (UPDLOCK) — SQL Server فقط
					// InMemory: قراءة عادية
					// =========================================

					SalesInvoiceStatus? lockedOriginal;

					if (_context.Database.IsRelational())
					{
						lockedOriginal = await _context.SalesInvoices
							.FromSqlInterpolated($@"
								SELECT * FROM [SalesInvoices]
								WITH (UPDLOCK, ROWLOCK)
								WHERE [Id] = {originalInvoice.Id}")
							.AsNoTracking()
							.Select(x => (SalesInvoiceStatus?)x.Status)
							.FirstOrDefaultAsync();
					}
					else
					{
						lockedOriginal = await _context.SalesInvoices
							.AsNoTracking()
							.Where(x => x.Id == originalInvoice.Id)
							.Select(x => (SalesInvoiceStatus?)x.Status)
							.FirstOrDefaultAsync();
					}

					if (lockedOriginal != SalesInvoiceStatus.Confirmed)
					{
						transactionError =
							"الفاتورة الأصلية لم تعد مؤهلة للمرتجع. " +
							"أعد فتح الصفحة.";

						return;
					}

					// =========================================
					// ✅ (Idempotency) إعادة فحص الكميات المرتجعة
					// بعد أخذ القفل — طلب تاني كان واقف مستني
					// القفل ممكن يكون اتسجل قبله.
					// =========================================

					var itemIds =
						returnInvoice.Items
							.Select(x => x.SalesInvoiceItemId)
							.ToList();

					var alreadyReturnedNow =
						await _context.SalesReturnInvoiceItems
							.Where(x =>
								x.SalesInvoiceItemId.HasValue &&
								itemIds.Contains(x.SalesInvoiceItemId.Value))
							.GroupBy(x => x.SalesInvoiceItemId!.Value)
							.Select(g => new
							{
								ItemId = g.Key,
								Qty = g.Sum(x => x.QuantityInBaseUnit)
							})
							.ToDictionaryAsync(x => x.ItemId, x => x.Qty);

					foreach (var rItem in returnInvoice.Items)
					{
						var originalItem = originalInvoice.Items
							.First(x => x.Id == rItem.SalesInvoiceItemId);

						alreadyReturnedNow.TryGetValue(
							rItem.SalesInvoiceItemId ?? 0,
							out var alreadyInDb);

						if (alreadyInDb + rItem.QuantityInBaseUnit >
							originalItem.QuantityInBaseUnit + 0.0001m)
						{
							transactionError =
								$"الكمية المرتجعة للمنتج تجاوزت المتاح " +
								$"من جلسة أخرى. الرجاء إعادة المحاولة.";

							return;
						}
					}

					try
					{
						await _context.SaveChangesAsync();

						var postingResult =
							await _postingService.PostSalesReturnAsync(
								returnInvoice,
								totalCostOfGoodsSold,
								CurrentUserId());

						if (!postingResult.Success)
						{
							await transaction.RollbackAsync();
							transactionError =
								"فشل ترحيل قيد مرتجع البيع: " +
								(postingResult.Error ?? "unknown");
							return;
						}

						await transaction.CommitAsync();
					}
					catch (DbUpdateConcurrencyException)
					{
						await transaction.RollbackAsync();
						transactionError =
							"تم تعديل المخزون من مستخدم آخر أثناء الحفظ. أعد المحاولة.";
					}
					catch (Exception ex)
					{
						await transaction.RollbackAsync();
						transactionError =
							"حدث خطأ أثناء الحفظ: " + ex.Message;
					}
				});

			if (transactionError != null)
			{
				TempData["Error"] = transactionError;
				return RedirectToAction(nameof(Index));
			}

			TempData["Success"] =
				$"تم إنشاء مرتجع البيع رقم {returnInvoice.InvoiceNumber} وترحيل القيود بنجاح.";

			return RedirectToAction(nameof(Index));
		}

		// =========================================
		// تفاصيل المرتجع
		// =========================================

		[HttpGet]
		[RequirePermission("sales.return.view")]
		public async Task<IActionResult> Details(int id)
		{
			var returnInvoice = await _context.SalesReturnInvoices
				.AsNoTracking()
				.Include(x => x.Customer)
				.Include(x => x.Store)
				.Include(x => x.OriginalSalesInvoice)
				.Include(x => x.RefundCashAccount)
				.Include(x => x.CreatedByUser)
				.Include(x => x.Items)
					.ThenInclude(x => x.Product)
				.Include(x => x.Items)
					.ThenInclude(x => x.Unit)
				.Include(x => x.Items)
					.ThenInclude(x => x.LotAllocations)
						.ThenInclude(x => x.StockLot)
				.FirstOrDefaultAsync(x => x.Id == id);

			if (returnInvoice == null)
			{
				return NotFound();
			}

			var scope = await _employeeScopeService.GetScopeAsync();

			if (!scope.CanAccessStore(returnInvoice.StoreId))
			{
				return RedirectToAction("AccessDenied", "Account");
			}

			return View(returnInvoice);
		}

		// =========================================
		// AJAX - فواتير العميل
		// =========================================

		[HttpGet]
		[RequirePermission("sales.return.create")]
		public async Task<IActionResult> GetCustomerInvoices(
			int customerId)
		{
			if (customerId <= 0)
			{
				return Json(new List<object>());
			}

			var scope = await _employeeScopeService.GetScopeAsync();

			var query = _context.SalesInvoices
				.AsNoTracking()
				.Where(x =>
					x.CustomerId == customerId &&
					x.Status == SalesInvoiceStatus.Confirmed);

			if (scope.IsRestricted)
			{
				var storeIds = scope.StoreIds.ToList();

				query = storeIds.Count == 0
					? query.Where(_ => false)
					: query.Where(x => storeIds.Contains(x.StoreId));
			}

			var invoices = await query
				.OrderByDescending(x => x.InvoiceDate)
				.ThenByDescending(x => x.Id)
				.Take(50)
				.Select(x => new
				{
					invoiceId = x.Id,
					invoiceNumber = x.InvoiceNumber,
					invoiceDate = x.InvoiceDate.ToString("yyyy-MM-dd"),
					totalAmount = x.TotalAmount
				})
				.ToListAsync();

			return Json(invoices);
		}

		// =========================================
		// تحميل القوائم
		// =========================================

		private async Task LoadLookupsAsync(SalesReturnViewModel model)
		{
			model.Customers = await _context.Customers
				.AsNoTracking()
				.Where(x => x.IsActive)
				.OrderBy(x => x.Name)
				.Select(x => new KeyValuePair<int, string>(x.Id, x.Name))
				.ToListAsync();

			var scope = await _employeeScopeService.GetScopeAsync();

			var storesQuery = _context.Stores
				.AsNoTracking()
				.Where(x => x.IsActive);

			storesQuery = _employeeScopeService
				.ApplyStoreFilter(storesQuery, scope);

			model.Stores = await storesQuery
				.OrderBy(x => x.Name)
				.Select(x => new KeyValuePair<int, string>(x.Id, x.Name))
				.ToListAsync();

			model.CashAccounts = await _context.CashAccounts
				.AsNoTracking()
				.Where(x => x.IsActive)
				.OrderBy(x => x.Name)
				.Select(x => new KeyValuePair<int, string>(x.Id, x.Name))
				.ToListAsync();
		}

		private async Task<bool> LoadInvoiceForReturnAsync(
			SalesReturnViewModel model,
			int invoiceId)
		{
			var invoice = await _context.SalesInvoices
				.AsNoTracking()
				.Include(x => x.Items)
					.ThenInclude(x => x.Product)
				.Include(x => x.Items)
					.ThenInclude(x => x.Unit)
				.FirstOrDefaultAsync(x =>
					x.Id == invoiceId &&
					x.Status == SalesInvoiceStatus.Confirmed);

			if (invoice == null)
			{
				return false;
			}

			var scope = await _employeeScopeService.GetScopeAsync();

			if (!scope.CanAccessStore(invoice.StoreId))
			{
				return false;
			}

			model.OriginalSalesInvoiceId = invoice.Id;
			model.OriginalInvoiceNumber = invoice.InvoiceNumber;
			model.OriginalInvoiceDate = invoice.InvoiceDate;
			model.OriginalInvoiceTotal = invoice.TotalAmount;
			model.CustomerId = invoice.CustomerId ?? 0;
			model.StoreId = invoice.StoreId;

			// ✅ (N+1 fix) query واحد لكل بنود الفاتورة
			var invoiceItemIds =
				invoice.Items
					.Select(x => x.Id)
					.ToList();

			var alreadyReturnedByItem =
				await _context.SalesReturnInvoiceItems
					.Where(x =>
						x.SalesInvoiceItemId.HasValue &&
						invoiceItemIds.Contains(x.SalesInvoiceItemId.Value))
					.GroupBy(x => x.SalesInvoiceItemId!.Value)
					.Select(g => new
					{
						ItemId = g.Key,
						Qty = g.Sum(x => x.QuantityInBaseUnit)
					})
					.ToDictionaryAsync(x => x.ItemId, x => x.Qty);

			foreach (var item in invoice.Items)
			{
				alreadyReturnedByItem.TryGetValue(item.Id, out var alreadyReturned);

				var returnable =
					item.QuantityInBaseUnit - alreadyReturned;

				if (returnable <= 0)
				{
					continue;
				}

				model.Items.Add(new SalesReturnItemViewModel
				{
					SalesInvoiceItemId = item.Id,
					ProductId = item.ProductId,
					ProductName = item.Product?.Name,
					UnitId = item.UnitId,
					UnitName = item.Unit?.Name,
					ConversionFactor = item.ConversionFactor,
					SoldQuantity = item.Quantity,
					AlreadyReturnedQuantity =
						item.ConversionFactor > 0
							? alreadyReturned / item.ConversionFactor
							: 0,
					ReturnableQuantity =
						item.ConversionFactor > 0
							? returnable / item.ConversionFactor
							: 0,
					UnitPrice = item.UnitPrice,
					UnitCost = 0,
					Total = 0
				});
			}

			return model.Items.Count > 0;
		}

		private async Task LoadCustomerInvoicesAsync(
			SalesReturnViewModel model,
			int customerId)
		{
			var scope = await _employeeScopeService.GetScopeAsync();

			var query = _context.SalesInvoices
				.AsNoTracking()
				.Include(x => x.Items)
				.Where(x =>
					x.CustomerId == customerId &&
					x.Status == SalesInvoiceStatus.Confirmed);

			if (scope.IsRestricted)
			{
				var storeIds = scope.StoreIds.ToList();

				query = storeIds.Count == 0
					? query.Where(_ => false)
					: query.Where(x => storeIds.Contains(x.StoreId));
			}

			var invoices = await query
				.OrderByDescending(x => x.InvoiceDate)
				.Take(50)
				.ToListAsync();

			model.AvailableInvoices = invoices
				.Select(x => new CustomerInvoiceOptionViewModel
				{
					InvoiceId = x.Id,
					InvoiceNumber = x.InvoiceNumber,
					InvoiceDate = x.InvoiceDate,
					TotalAmount = x.TotalAmount,
					ItemCount = x.Items.Count
				})
				.ToList();
		}

		private async Task ReloadForErrorAsync(SalesReturnViewModel model)
		{
			await LoadLookupsAsync(model);

			if (model.OriginalSalesInvoiceId.HasValue)
			{
				var temp = new SalesReturnViewModel();

				await LoadLookupsAsync(temp);
				await LoadInvoiceForReturnAsync(
					temp,
					model.OriginalSalesInvoiceId.Value);

				model.OriginalInvoiceNumber = temp.OriginalInvoiceNumber;
				model.OriginalInvoiceDate = temp.OriginalInvoiceDate;
				model.OriginalInvoiceTotal = temp.OriginalInvoiceTotal;

				foreach (var item in temp.Items)
				{
					var posted = model.Items
						.FirstOrDefault(x =>
							x.SalesInvoiceItemId ==
							item.SalesInvoiceItemId);

					if (posted != null)
					{
						item.Quantity = posted.Quantity;
					}
				}

				model.Items = temp.Items;
			}
			else if (model.CustomerId > 0)
			{
				await LoadCustomerInvoicesAsync(model, model.CustomerId);
			}
		}

		private async Task<string> GenerateRefundNumberAsync()
		{
			var datePart = DateTime.Today.ToString("yyyyMMdd");

			var prefix = $"PAY-{datePart}-";

			var lastNumber = await _context.TreasuryTransactions
				.AsNoTracking()
				.Where(x =>
					x.Type == TreasuryTransactionType.Pay &&
					x.TransactionNumber.StartsWith(prefix))
				.OrderByDescending(x => x.TransactionNumber)
				.Select(x => x.TransactionNumber)
				.FirstOrDefaultAsync();

			var nextSeq = 1;

			if (!string.IsNullOrEmpty(lastNumber))
			{
				var parts = lastNumber.Split('-');

				if (parts.Length == 3 &&
					int.TryParse(parts[2], out var last))
				{
					nextSeq = last + 1;
				}
			}

			return $"{prefix}{nextSeq:0000}";
		}

		private int? CurrentUserId()
		{
			var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

			return int.TryParse(value, out var id) ? id : null;
		}
	}
}