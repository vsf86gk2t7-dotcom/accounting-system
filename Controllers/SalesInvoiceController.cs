using AccountingSystem.Data;
using AccountingSystem.Filters;
using AccountingSystem.Models;
using AccountingSystem.Models.ViewModels.Sales;
using AccountingSystem.Services;
using AccountingSystem.Services.EmployeeScope;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using static AccountingSystem.Models.SalesInvoice;

namespace AccountingSystem.Controllers
{
	public class SalesInvoiceController : Controller
	{
		private readonly ApplicationDbContext _context;
		private readonly IEmployeeScopeService _employeeScopeService;
		private readonly Services.Pdf.IPdfInvoiceService _pdfInvoiceService;
		private readonly Services.WhatsApp.IWhatsAppService _whatsAppService;
		private readonly Services.Permissions.IPermissionService _permissionService;
		private readonly IPostingService _postingService;
		private readonly IInvoiceReminderService _reminderService;
		private readonly IDocumentNumberService _documentNumberService;
		private readonly ISalesRepService _salesRepService;

		public SalesInvoiceController(
			ApplicationDbContext context,
			IEmployeeScopeService employeeScopeService,
			Services.Pdf.IPdfInvoiceService pdfInvoiceService,
			Services.WhatsApp.IWhatsAppService whatsAppService,
			Services.Permissions.IPermissionService permissionService,
			IPostingService postingService,
			IInvoiceReminderService reminderService,
			IDocumentNumberService documentNumberService,
			ISalesRepService salesRepService)
		{
			_context = context;
			_employeeScopeService = employeeScopeService;
			_pdfInvoiceService = pdfInvoiceService;
			_whatsAppService = whatsAppService;
			_permissionService = permissionService;
			_postingService = postingService;
			_reminderService = reminderService;
			_documentNumberService = documentNumberService;
			_salesRepService = salesRepService;
		}

		// =========================================
		// قائمة فواتير البيع (مفلترة بالـScope)
		// =========================================

		[HttpGet]
		[RequirePermission("sales.view")]
		public async Task<IActionResult> Index(int page = 1, int pageSize = 20)
		{
			var scope = await _employeeScopeService.GetScopeAsync();

			var query = _context.SalesInvoices
				.AsNoTracking()
				.Include(x => x.Branch)
				.Include(x => x.Store)
				.Include(x => x.Customer)
				.AsQueryable();

			if (scope.IsRestricted)
			{
				var storeIds = scope.StoreIds.ToList();

				query = storeIds.Count == 0
					? query.Where(_ => false)
					: query.Where(x => storeIds.Contains(x.StoreId));
			}

			var totalCount = await query.CountAsync();

			var invoices = await query
				.OrderByDescending(x => x.InvoiceDate)
				.ThenByDescending(x => x.Id)
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
				.ToListAsync();

			var pagedResult = new Models.ViewModels.Pagination.PagedResult<SalesInvoice>
			{
				Items = invoices,
				PageNumber = page,
				PageSize = pageSize,
				TotalCount = totalCount
			};

			return View(pagedResult);
		}

		// =========================================
		// إنشاء فاتورة بيع - GET
		// =========================================

		[HttpGet]
		[RequirePermission("sales.create")]
		public async Task<IActionResult> Create()
		{
			var model = new SalesInvoiceCreateViewModel();

			model.InvoiceNumber = await _documentNumberService.GenerateNumberAsync("SAL", "SalesInvoices", DateTime.Today.ToString("yyyyMMdd"));

			await LoadCreateDataAsync(model);

			return View(model);
		}

		// =========================================
		// شاشة الكاشير (متجاوبة للموبايل)
		// =========================================

		[HttpGet]
		[RequirePermission("sales.create")]
		public async Task<IActionResult> Pos()
		{
			var model = new SalesInvoiceCreateViewModel();

			model.InvoiceNumber = await _documentNumberService.GenerateNumberAsync("SAL", "SalesInvoices", DateTime.Today.ToString("yyyyMMdd"));

			await LoadCreateDataAsync(model);

			return View(model);
		}

		// =========================================
		// حفظ فاتورة الكاشير - POST
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("sales.create")]
		public async Task<IActionResult> Pos(SalesInvoiceCreateViewModel model)
		{
			return await SaveDraftAsync(model, "Pos");
		}

		// =========================================
		// حفظ فاتورة بيع جديدة - POST
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("sales.create")]
		public async Task<IActionResult> Create(SalesInvoiceCreateViewModel model)
		{
			return await SaveDraftAsync(model, "Create");
		}

		private async Task<IActionResult> SaveDraftAsync(
			SalesInvoiceCreateViewModel model,
			string failView)
		{
			model.InvoiceNumber =
				model.InvoiceNumber?.Trim() ?? string.Empty;

			if (string.IsNullOrWhiteSpace(model.InvoiceNumber))
			{
				model.InvoiceNumber = await _documentNumberService.GenerateNumberAsync("SAL", "SalesInvoices", DateTime.Today.ToString("yyyyMMdd"));
				ModelState.Remove(nameof(model.InvoiceNumber));
			}

			model.Notes =
				string.IsNullOrWhiteSpace(model.Notes)
					? null
					: model.Notes.Trim();

			model.Items ??=
				new List<SalesInvoiceItemCreateViewModel>();

			if (!ModelState.IsValid)
			{
				await LoadCreateDataAsync(model);
				return View(failView, model);
			}

			// =========================================
			// رقم الفاتورة غير مكرر
			// =========================================

			var invoiceNumberExists =
				await _context.SalesInvoices
					.AnyAsync(x =>
						x.InvoiceNumber == model.InvoiceNumber);

			if (invoiceNumberExists)
			{
				ModelState.AddModelError(
					nameof(model.InvoiceNumber),
					"رقم الفاتورة مستخدم بالفعل.");

				await LoadCreateDataAsync(model);
				return View(failView, model);
			}

			// =========================================
			// الـScope
			// =========================================

			var scope = await _employeeScopeService.GetScopeAsync();

			// =========================================
			// الفرع
			// =========================================

			var branch = await _context.Branches
				.FirstOrDefaultAsync(x =>
					x.Id == model.BranchId &&
					x.IsActive);

			if (branch == null || !scope.CanAccessBranch(branch.Id))
			{
				ModelState.AddModelError(
					nameof(model.BranchId),
					"الفرع غير موجود أو ليس لديك صلاحية عليه.");

				await LoadCreateDataAsync(model);
				return View(failView, model);
			}

			// =========================================
			// المخزن
			// =========================================

			var store = await _context.Stores
				.FirstOrDefaultAsync(x =>
					x.Id == model.StoreId &&
					x.IsActive &&
					x.BranchId == model.BranchId);

			if (store == null || !scope.CanAccessStore(store.Id))
			{
				ModelState.AddModelError(
					nameof(model.StoreId),
					"المخزن غير موجود أو ليس لديك صلاحية عليه.");

				await LoadCreateDataAsync(model);
				return View(failView, model);
			}

			// =========================================
			// المندوب: من المستخدم أو من مخزن العهدة
			// =========================================

			var repId = await _salesRepService.GetCurrentRepIdAsync();

			// ✅ لو المستخدم مش مندوب، نجيب المندوب من مخزن العهدة
			if (repId == null)
			{
				repId = await _context.SalesRepProfiles
					.AsNoTracking()
					.Where(p => p.IsActive && p.CustodyStoreId == store.Id)
					.Select(p => (int?)p.Id)
					.FirstOrDefaultAsync();
			}

			// =========================================
			// العميل (اختياري)
			// =========================================

			if (model.CustomerId.HasValue)
			{
				var customerExists =
					await _context.Customers
						.AnyAsync(x =>
							x.Id == model.CustomerId.Value &&
							x.IsActive &&
							(repId == null || x.SalesRepId == repId));

				if (!customerExists)
				{
					ModelState.AddModelError(
						nameof(model.CustomerId),
						"العميل غير موجود أو غير نشط.");

					await LoadCreateDataAsync(model);
					return View(failView, model);
				}
			}

			// =========================================
			// طريقة الدفع (إجباري باختيار صريح)
			// =========================================

			if (string.IsNullOrWhiteSpace(Request.Form["PaymentMethod"]))
			{
				ModelState.AddModelError(
					nameof(model.PaymentMethod),
					"لابد من اختيار طريقة الدفع.");

				await LoadCreateDataAsync(model);
				return View(failView, model);
			}

			// =========================================
			// البنود
			// =========================================

			model.Items = model.Items
				.Where(x => x.ProductId > 0 && x.UnitId > 0)
				.ToList();

			if (!model.Items.Any())
			{
				ModelState.AddModelError(
					nameof(model.Items),
					"يجب إضافة بند واحد على الأقل.");

				await LoadCreateDataAsync(model);
				return View(failView, model);
			}

			// =========================================
			// تجميع المنتجات
			// =========================================

			var productIds = model.Items
				.Select(x => x.ProductId)
				.Distinct()
				.ToList();

			var products = await _context.Products
				.Include(x => x.ProductUnits)
				.Where(x =>
					x.IsActive &&
					productIds.Contains(x.Id))
				.ToListAsync();

			if (products.Count != productIds.Count)
			{
				ModelState.AddModelError(
					nameof(model.Items),
					"يوجد منتج غير موجود أو غير نشط.");

				await LoadCreateDataAsync(model);
				return View(failView, model);
			}

			var canOverridePrice = CanOverridePrice();

			// =========================================
			// إنشاء الفاتورة والبنود
			// =========================================

			var invoice = new SalesInvoice
			{
				InvoiceNumber = model.InvoiceNumber,
				BranchId = branch.Id,
				StoreId = store.Id,
				CustomerId = model.CustomerId,
				InvoiceDate = model.InvoiceDate,
				DueDate = model.DueDate,
				Notes = model.Notes,
				DiscountAmount = model.DiscountAmount,
				SalesTaxRate = model.SalesTaxRate,
				TaxRate = model.TaxRate,
				PaymentMethod = model.PaymentMethod,
				PaymentDetails = model.PaymentDetails,
				Status = SalesInvoiceStatus.Draft,
				SalesRepId = repId,
				CreatedAt = DateTime.UtcNow
			};

			var userIdValue = User.FindFirstValue(
				ClaimTypes.NameIdentifier);

			if (int.TryParse(userIdValue, out var userId))
			{
				invoice.CreatedByUserId = userId;
			}

			decimal subTotal = 0;

			foreach (var itemModel in model.Items)
			{
				var product = products
					.First(x => x.Id == itemModel.ProductId);

				var productUnit = product.ProductUnits
					.FirstOrDefault(x =>
						x.UnitId == itemModel.UnitId &&
						x.IsActive);

				if (productUnit == null)
				{
					ModelState.AddModelError(
						nameof(model.Items),
						$"الوحدة غير متاحة للمنتج: {product.Name}");

					await LoadCreateDataAsync(model);
					return View(failView, model);
				}

				var conversionFactor = productUnit.ConversionFactor;

				if (conversionFactor <= 0)
				{
					ModelState.AddModelError(
						nameof(model.Items),
						$"معامل التحويل غير صحيح للمنتج: {product.Name}");

					await LoadCreateDataAsync(model);
					return View(failView, model);
				}

				var serverUnitPrice = productUnit.SalePrice;

				if (canOverridePrice && itemModel.UnitPrice > 0)
				{
					serverUnitPrice = itemModel.UnitPrice;
				}

				var quantityInBaseUnit =
					itemModel.Quantity * conversionFactor;

				var lineTotal =
					itemModel.Quantity * serverUnitPrice;

				subTotal += lineTotal;

				invoice.Items.Add(new SalesInvoiceItem
				{
					ProductId = product.Id,
					UnitId = itemModel.UnitId,
					ConversionFactor = conversionFactor,
					Quantity = itemModel.Quantity,
					QuantityInBaseUnit = quantityInBaseUnit,
					UnitPrice = serverUnitPrice,
					TotalPrice = lineTotal,
					IssueMethod = itemModel.IssueMethod,
					SelectedStockLotId = itemModel.SelectedStockLotId
				});
			}

			invoice.SubTotal = subTotal;
			decimal taxableAmount = subTotal - invoice.DiscountAmount;

			if (invoice.SalesTaxRate > 0)
				invoice.SalesTaxAmount = Math.Round(taxableAmount * invoice.SalesTaxRate / 100m, 2);
			else
				invoice.SalesTaxAmount = 0;

			if (invoice.TaxRate > 0)
				invoice.TaxAmount = Math.Round(taxableAmount * invoice.TaxRate / 100m, 2);
			else
				invoice.TaxAmount = 0;

			invoice.TotalAmount =
				taxableAmount
				+ invoice.SalesTaxAmount
				+ invoice.TaxAmount;

			_context.SalesInvoices.Add(invoice);

			try
			{
				await _context.SaveChangesAsync();
			}
			catch (DbUpdateException ex)
			{
				var innerMessage =
					ex.InnerException?.Message ??
					ex.Message;

				TempData["Error"] =
					"تعذر حفظ الفاتورة: " +
					innerMessage;

				await LoadCreateDataAsync(model);
				return View(failView, model);
			}
			catch (InvalidOperationException)
			{
				TempData["Error"] =
					"تعذر حفظ الفاتورة: تداخل في العمليات. أعد المحاولة.";

				await LoadCreateDataAsync(model);
				return View(failView, model);
			}

			TempData["Success"] =
				"تم إنشاء فاتورة البيع بنجاح (مسودة).";

			if (Request.Form["saveAndConfirm"] == "true")
			{
				var canConfirm =
					await _permissionService.HasPermissionAsync(
						userId,
						"sales.confirm");

				if (!canConfirm)
				{
					TempData["Error"] =
						"ليس لديك صلاحية ترحيل الفاتورة.";

					return RedirectToAction(nameof(Index));
				}

				return await Confirm(invoice.Id, model.CashAccountId);
			}

			return RedirectToAction(nameof(Index));
		}

		// =========================================
		// تعديل فاتورة - GET
		// =========================================

		[HttpGet]
		[RequirePermission("sales.edit")]
		public async Task<IActionResult> Edit(int id)
		{
			var invoice = await _context.SalesInvoices
				.Include(x => x.Items)
				.ThenInclude(x => x.Product)
				.Include(x => x.Items)
				.ThenInclude(x => x.Unit)
				.FirstOrDefaultAsync(x => x.Id == id);

			if (invoice == null)
			{
				return NotFound();
			}

			var scope = await _employeeScopeService.GetScopeAsync();

			if (!scope.CanAccessStore(invoice.StoreId))
			{
				TempData["Error"] =
					"ليس لديك صلاحية على مخزن هذه الفاتورة.";

				return RedirectToAction(nameof(Index));
			}

			if (invoice.Status == SalesInvoiceStatus.Cancelled)
			{
				TempData["Error"] =
					"لا يمكن تعديل فاتورة ملغاة.";

				return RedirectToAction(nameof(Index));
			}

			int? cashAccountId = null;

			if (invoice.PaymentMethod != SalesPaymentMethod.Credit)
			{
				cashAccountId = await _context.TreasuryTransactions
					.AsNoTracking()
					.Where(x =>
						x.Type == TreasuryTransactionType.Receive &&
						x.ReferenceDocument == invoice.InvoiceNumber &&
						x.CustomerId == invoice.CustomerId)
					.OrderByDescending(x => x.CreatedAt)
					.Select(x => (int?)x.CashAccountId)
					.FirstOrDefaultAsync();
			}

			var model = new SalesInvoiceCreateViewModel
			{
				InvoiceNumber = invoice.InvoiceNumber,
				BranchId = invoice.BranchId,
				StoreId = invoice.StoreId,
				CustomerId = invoice.CustomerId,
				InvoiceDate = invoice.InvoiceDate,
				DueDate = invoice.DueDate,
				Notes = invoice.Notes,
				DiscountAmount = invoice.DiscountAmount,
				SalesTaxRate = invoice.SalesTaxRate,
				SalesTaxAmount = invoice.SalesTaxAmount,
				TaxRate = invoice.TaxRate,
				TaxAmount = invoice.TaxAmount,
				PaymentMethod = invoice.PaymentMethod,
				PaymentDetails = invoice.PaymentDetails,
				CashAccountId = cashAccountId,
				Items = invoice.Items
					.Select(x => new SalesInvoiceItemCreateViewModel
					{
						ProductId = x.ProductId,
						UnitId = x.UnitId,
						Quantity = x.Quantity,
						UnitPrice = x.UnitPrice,
						IssueMethod = x.IssueMethod,
						SelectedStockLotId = x.SelectedStockLotId
					})
					.ToList()
			};

			await LoadCreateDataAsync(model);

			ViewData["FormAction"] = "Edit";
			ViewData["InvoiceId"] = invoice.Id.ToString();

			return View("Create", model);
		}

		// =========================================
		// تعديل فاتورة - POST
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("sales.edit")]
		public async Task<IActionResult> Edit(
			int id,
			SalesInvoiceCreateViewModel model)
		{
			var invoice = await _context.SalesInvoices
				.Include(x => x.Items)
					.ThenInclude(x => x.LotAllocations)
				.Include(x => x.Customer)
				.FirstOrDefaultAsync(x => x.Id == id);

			if (invoice == null)
				return NotFound();

			if (invoice.Status == SalesInvoiceStatus.Cancelled)
			{
				TempData["Error"] = "لا يمكن تعديل فاتورة ملغاة.";
				return RedirectToAction(nameof(Index));
			}

			var scope = await _employeeScopeService.GetScopeAsync();

			if (!scope.CanAccessStore(invoice.StoreId))
			{
				TempData["Error"] = "ليس لديك صلاحية على مخزن هذه الفاتورة.";
				return RedirectToAction(nameof(Index));
			}

			model.Items ??= new List<SalesInvoiceItemCreateViewModel>();

			model.Items = model.Items
				.Where(x => x.ProductId > 0 && x.UnitId > 0)
				.ToList();

			model.Notes = string.IsNullOrWhiteSpace(model.Notes)
				? null
				: model.Notes.Trim();

			if (string.IsNullOrWhiteSpace(model.InvoiceNumber))
			{
				model.InvoiceNumber = await _documentNumberService.GenerateNumberAsync("SAL", "SalesInvoices", DateTime.Today.ToString("yyyyMMdd"));
				ModelState.Remove(nameof(model.InvoiceNumber));
			}

			ViewData["FormAction"] = "Edit";
			ViewData["InvoiceId"] = id.ToString();

			if (!ModelState.IsValid)
			{
				await LoadCreateDataAsync(model);
				return View("Create", model);
			}

			if (!model.Items.Any())
			{
				ModelState.AddModelError(
					nameof(model.Items),
					"يجب إضافة بند واحد على الأقل.");

				await LoadCreateDataAsync(model);
				return View("Create", model);
			}

			var branch = await _context.Branches
				.FirstOrDefaultAsync(x => x.Id == model.BranchId && x.IsActive);

			if (branch == null || !scope.CanAccessBranch(branch.Id))
			{
				ModelState.AddModelError(nameof(model.BranchId),
					"الفرع غير موجود أو ليس لديك صلاحية عليه.");

				await LoadCreateDataAsync(model);
				return View("Create", model);
			}

			var store = await _context.Stores
				.FirstOrDefaultAsync(x =>
					x.Id == model.StoreId &&
					x.IsActive &&
					x.BranchId == model.BranchId);

			if (store == null || !scope.CanAccessStore(store.Id))
			{
				ModelState.AddModelError(nameof(model.StoreId),
					"المخزن غير موجود أو ليس لديك صلاحية عليه.");

				await LoadCreateDataAsync(model);
				return View("Create", model);
			}

			if (model.CustomerId.HasValue)
			{
				var customerExists = await _context.Customers
					.AnyAsync(x => x.Id == model.CustomerId.Value && x.IsActive);

				if (!customerExists)
				{
					ModelState.AddModelError(nameof(model.CustomerId),
						"العميل غير موجود أو غير نشط.");

					await LoadCreateDataAsync(model);
					return View("Create", model);
				}
			}

			if (string.IsNullOrWhiteSpace(Request.Form["PaymentMethod"]))
			{
				ModelState.AddModelError(nameof(model.PaymentMethod),
					"لابد من اختيار طريقة الدفع.");

				await LoadCreateDataAsync(model);
				return View("Create", model);
			}

			var productIds = model.Items
				.Select(x => x.ProductId)
				.Distinct()
				.ToList();

			var products = await _context.Products
				.Include(x => x.ProductUnits)
				.Where(x => x.IsActive && productIds.Contains(x.Id))
				.ToListAsync();

			if (products.Count != productIds.Count)
			{
				ModelState.AddModelError(nameof(model.Items),
					"يوجد منتج غير موجود أو غير نشط.");

				await LoadCreateDataAsync(model);
				return View("Create", model);
			}

			var canOverridePrice = CanOverridePrice();
			var newItems = new List<SalesInvoiceItem>();
			decimal subTotal = 0;

			foreach (var itemModel in model.Items)
			{
				var product = products.First(x => x.Id == itemModel.ProductId);

				var productUnit = product.ProductUnits
					.FirstOrDefault(x => x.UnitId == itemModel.UnitId && x.IsActive);

				if (productUnit == null)
				{
					ModelState.AddModelError(nameof(model.Items),
						$"الوحدة غير متاحة للمنتج: {product.Name}");

					await LoadCreateDataAsync(model);
					return View("Create", model);
				}

				var conversionFactor = productUnit.ConversionFactor;

				if (conversionFactor <= 0)
				{
					ModelState.AddModelError(nameof(model.Items),
						$"معامل التحويل غير صحيح للمنتج: {product.Name}");

					await LoadCreateDataAsync(model);
					return View("Create", model);
				}

				var serverUnitPrice = productUnit.SalePrice;

				if (canOverridePrice && itemModel.UnitPrice > 0)
					serverUnitPrice = itemModel.UnitPrice;

				var quantityInBaseUnit = itemModel.Quantity * conversionFactor;
				var lineTotal = itemModel.Quantity * serverUnitPrice;
				subTotal += lineTotal;

				newItems.Add(new SalesInvoiceItem
				{
					ProductId = product.Id,
					UnitId = itemModel.UnitId,
					ConversionFactor = conversionFactor,
					Quantity = itemModel.Quantity,
					QuantityInBaseUnit = quantityInBaseUnit,
					UnitPrice = serverUnitPrice,
					TotalPrice = lineTotal,
					IssueMethod = itemModel.IssueMethod,
					SelectedStockLotId = itemModel.SelectedStockLotId
				});
			}

			var wasConfirmed = invoice.Status == SalesInvoiceStatus.Confirmed;
			var oldPaymentMethod = invoice.PaymentMethod;

			var strategy =
				_context.Database.CreateExecutionStrategy();

			string? transactionError = null;

			await strategy.ExecuteAsync(
				async () =>
				{
					await using var transaction =
						await _context.Database.BeginTransactionAsync();

					try
					{
						if (wasConfirmed)
						{
							var totalCost = invoice.Items
								.SelectMany(i => i.LotAllocations)
								.Sum(sl => sl.TotalCost);

							foreach (var item in invoice.Items)
							{
								foreach (var alloc in item.LotAllocations)
								{
									if (alloc.StockLot != null)
										alloc.StockLot.QuantityRemaining += alloc.QuantityBaseUnit;
								}
							}

							if (oldPaymentMethod != SalesPaymentMethod.Credit)
							{
								var treasuryEntry = await _context.TreasuryTransactions
									.Where(x =>
										x.Type == TreasuryTransactionType.Receive &&
										x.ReferenceDocument == invoice.InvoiceNumber &&
										x.CustomerId == invoice.CustomerId)
									.OrderByDescending(x => x.CreatedAt)
									.FirstOrDefaultAsync();

								if (treasuryEntry != null)
								{
									_context.TreasuryTransactions.Add(new TreasuryTransaction
									{
										TransactionNumber = await GenerateReceiptNumberAsync(),
										Type = TreasuryTransactionType.Pay,
										CashAccountId = treasuryEntry.CashAccountId,
										CustomerId = invoice.CustomerId,
										Amount = treasuryEntry.Amount,
										Reason = "تعديل فاتورة بيع — إبطال التحصيل القديم",
										ReferenceDocument = invoice.InvoiceNumber,
										AutoJournal = false,
										CreatedByUserId = CurrentUserId(),
										CreatedAt = DateTime.UtcNow
									});
								}
							}

							var reversal = await _postingService
								.PostSalesInvoiceCancelAsync(
									invoice, totalCost, CurrentUserId());

							if (!reversal.Success)
							{
								await transaction.RollbackAsync();

								transactionError =
									"تعذر عكس قيود الفاتورة قبل التعديل: " +
									reversal.Error;

								return;
							}
						}

						var oldItems = invoice.Items.ToList();

						foreach (var oldItem in oldItems)
						{
							_context.SalesInvoiceItemLots
								.RemoveRange(oldItem.LotAllocations);
						}

						_context.SalesInvoiceItems.RemoveRange(oldItems);

						invoice.Items.Clear();

						invoice.BranchId = model.BranchId;
						invoice.StoreId = model.StoreId;
						invoice.CustomerId = model.CustomerId;
						invoice.InvoiceDate = model.InvoiceDate;
						invoice.DueDate = model.DueDate;
						invoice.Notes = model.Notes;
						invoice.DiscountAmount = model.DiscountAmount;
						invoice.SalesTaxRate = model.SalesTaxRate;
						invoice.TaxRate = model.TaxRate;
						invoice.PaymentMethod = model.PaymentMethod;
						invoice.PaymentDetails = model.PaymentDetails;
						invoice.SubTotal = subTotal;

						decimal taxableAmount = subTotal - invoice.DiscountAmount;

						invoice.SalesTaxAmount = invoice.SalesTaxRate > 0
							? Math.Round(taxableAmount * invoice.SalesTaxRate / 100m, 2)
							: 0;

						invoice.TaxAmount = invoice.TaxRate > 0
							? Math.Round(taxableAmount * invoice.TaxRate / 100m, 2)
							: 0;

						invoice.TotalAmount =
							taxableAmount
							+ invoice.SalesTaxAmount
							+ invoice.TaxAmount;

						invoice.Status = SalesInvoiceStatus.Draft;
						invoice.ConfirmedAt = null;

						foreach (var newItem in newItems)
							invoice.Items.Add(newItem);

						await _context.SaveChangesAsync();
						await transaction.CommitAsync();
					}
					catch (DbUpdateException ex)
					{
						await transaction.RollbackAsync();

						transactionError =
							"تعذر حفظ الفاتورة بعد التعديل: " +
							(ex.InnerException?.Message ?? ex.Message);
					}
					catch (InvalidOperationException)
					{
						await transaction.RollbackAsync();

						transactionError =
							"تعذر حفظ الفاتورة بعد التعديل: تداخل في العمليات. أعد المحاولة.";
					}
					catch
					{
						await transaction.RollbackAsync();
						throw;
					}
				});

			if (transactionError != null)
			{
				TempData["Error"] = transactionError;
				return RedirectToAction(nameof(Details), new { id = invoice.Id });
			}

			if (Request.Form["saveAndConfirm"] == "true")
			{
				var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
				var canConfirm = await _permissionService.HasPermissionAsync(
					int.TryParse(userIdValue, out var uid) ? uid : 0,
					"sales.confirm");

				if (!canConfirm)
				{
					TempData["Error"] = "ليس لديك صلاحية ترحيل الفاتورة.";
					return RedirectToAction(nameof(Details), new { id = invoice.Id });
				}

				return await Confirm(invoice.Id, model.CashAccountId);
			}

			TempData["Success"] =
				"تم تعديل الفاتورة بنجاح" +
				(wasConfirmed
					? " (أُعيدت المخزون، أُبطل التحصيل، وعُكست القيود ثم أُعيد بناء البنود)."
					: ".");

			return RedirectToAction(nameof(Details), new { id = invoice.Id });
		}

		// =========================================
		// ترحيل الفاتورة (Draft → Confirmed)
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("sales.confirm")]
		public async Task<IActionResult> Confirm(int id, int? cashAccountId = null)
		{
			var invoice = await _context.SalesInvoices
				.Include(x => x.Items)
				.Include(x => x.Customer)
				.FirstOrDefaultAsync(x => x.Id == id);

			if (invoice == null)
			{
				return NotFound();
			}

			if (!cashAccountId.HasValue)
			{
				var tempCashId = TempData["CashAccountId"]?.ToString();
				if (tempCashId != null &&
					int.TryParse(tempCashId, out var parsed))
				{
					cashAccountId = parsed;
				}
			}

			// =========================================
			// المندوب: تثبيت خزنة العهدة تلقائياً
			// =========================================

			var currentRepId = await _salesRepService.GetCurrentRepIdAsync();

			if (currentRepId.HasValue)
			{
				var repProfile = await _context.SalesRepProfiles
					.AsNoTracking()
					.FirstOrDefaultAsync(p => p.Id == currentRepId.Value);

				if (repProfile?.CashAccountId.HasValue == true)
				{
					cashAccountId = repProfile.CashAccountId.Value;
				}

				if (repProfile?.CustodyStoreId.HasValue == true &&
					repProfile.CustodyStoreId.Value != invoice.StoreId)
				{
					TempData["Error"] =
						"ليس لديك صلاحية البيع من هذا المخزن.";

					return RedirectToAction(nameof(Index));
				}
			}

			// =========================================
			// fallback لمستخدم غير مندوب
			// =========================================

			if (!cashAccountId.HasValue)
			{
				var firstCash =
					await _context.CashAccounts
						.Where(x => x.IsActive)
						.OrderBy(x => x.Id)
						.FirstOrDefaultAsync();

				cashAccountId = firstCash?.Id;
			}

			if (!cashAccountId.HasValue)
			{
				TempData["Error"] =
					"لا توجد خزنة نشطة للترحيل. أضف خزنة أولاً.";

				return RedirectToAction(nameof(Details), new { id });
			}

			var scope = await _employeeScopeService.GetScopeAsync();

			if (!scope.CanAccessStore(invoice.StoreId))
			{
				TempData["Error"] =
					"ليس لديك صلاحية على مخزن هذه الفاتورة.";

				return RedirectToAction(nameof(Index));
			}

			if (invoice.Status != SalesInvoiceStatus.Draft)
			{
				TempData["Error"] =
					"لا يمكن ترحيل الفاتورة إلا وهي في حالة مسودة.";

				return RedirectToAction(nameof(Index));
			}

			if (!invoice.Items.Any())
			{
				TempData["Error"] =
					"لا يمكن ترحيل فاتورة بدون بنود.";

				return RedirectToAction(nameof(Index));
			}

			var productIds = invoice.Items.Select(x => x.ProductId).Distinct().ToList();
			var products = await _context.Products
				.Where(x => productIds.Contains(x.Id))
				.ToDictionaryAsync(x => x.Id);

			var fifoProductIds = invoice.Items
				.Where(x => x.IssueMethod == StockIssueMethod.FIFO)
				.Select(x => x.ProductId)
				.Distinct()
				.ToList();

			var availableQuantities = new Dictionary<int, decimal>();

if (fifoProductIds.Any())
{
	// اجلب البيانات أولاً (بدون GroupBy على السيرفر)
	var fifoStockRows = await _context.StockLots
		.AsNoTracking()
		.Where(x =>
			x.StoreId == invoice.StoreId &&
			fifoProductIds.Contains(x.ProductId) &&
			x.IsActive &&
			x.QuantityRemaining > 0)
		.Select(x => new { x.ProductId, x.QuantityRemaining })
		.ToListAsync();

	// GroupBy على الـ client (يعمل على كل providers)
	availableQuantities = fifoStockRows
		.GroupBy(x => x.ProductId)
		.ToDictionary(
			g => g.Key,
			g => g.Sum(x => x.QuantityRemaining));
}

			var manualLotIds = invoice.Items
				.Where(x => x.IssueMethod == StockIssueMethod.Manual && x.SelectedStockLotId.HasValue)
				.Select(x => x.SelectedStockLotId!.Value)
				.Distinct()
				.ToList();

			var manualLots = manualLotIds.Any()
				? await _context.StockLots
					.Where(x => manualLotIds.Contains(x.Id))
					.ToDictionaryAsync(x => x.Id)
				: new Dictionary<int, StockLot>();

			foreach (var item in invoice.Items)
			{
				if (!products.TryGetValue(item.ProductId, out var product))
				{
					TempData["Error"] =
						"يوجد منتج غير موجود في الفاتورة.";

					return RedirectToAction(nameof(Index));
				}

				var quantityInBaseUnit = item.QuantityInBaseUnit;

				if (item.IssueMethod == StockIssueMethod.Manual)
				{
					if (!item.SelectedStockLotId.HasValue)
					{
						TempData["Error"] =
							$"يجب اختيار دفعة للمنتج: {product.Name}";

						return RedirectToAction(nameof(Index));
					}

					if (!manualLots.TryGetValue(item.SelectedStockLotId.Value, out var lot) ||
						lot.StoreId != invoice.StoreId ||
						lot.ProductId != item.ProductId ||
						!lot.IsActive)
					{
						TempData["Error"] =
							$"الدفعة المختارة غير صحيحة للمنتج: {product.Name}";

						return RedirectToAction(nameof(Index));
					}

					if (lot.QuantityRemaining < quantityInBaseUnit)
					{
						TempData["Error"] =
							$"الكمية غير كافية في الدفعة المختارة للمنتج: {product.Name}. " +
							$"المتاح: {lot.QuantityRemaining} — المطلوب: {quantityInBaseUnit}";

						return RedirectToAction(nameof(Index));
					}
				}
				else
				{
					var availableQuantity = availableQuantities.TryGetValue(item.ProductId, out var qty) ? qty : 0;

					if (availableQuantity < quantityInBaseUnit)
					{
						TempData["Error"] =
							$"الكمية غير متوفرة في المخزون للمنتج: {product.Name}. " +
							$"المتاح: {availableQuantity} — المطلوب: {quantityInBaseUnit}";

						return RedirectToAction(nameof(Index));
					}
				}
			}

			// =========================================
			// ✅ جديد: تحذير البيع بخسارة
			// =========================================
			var lossWarning = "";

			foreach (var item in invoice.Items)
			{
				// احسب صافي البيع البنصي (تخصيص الخصم proportionally)
				decimal itemNetSale = item.TotalPrice;
				if (invoice.SubTotal > 0 && invoice.DiscountAmount > 0)
				{
					itemNetSale =
						item.TotalPrice -
						(invoice.DiscountAmount * item.TotalPrice / invoice.SubTotal);
				}

				// احسب التكلفة بناءً على طريقة الصرف
				decimal itemCost = 0;

				if (item.IssueMethod == StockIssueMethod.Manual && item.SelectedStockLotId.HasValue)
				{
					var lot = await _context.StockLots
						.FirstAsync(x => x.Id == item.SelectedStockLotId.Value);
					itemCost = item.QuantityInBaseUnit * lot.UnitCost;
				}
				else
				{
					// تكلفة推算 من أقل دفعات FIFO (تقدير)
					var estimatedCost = await _context.StockLots
						.Where(x =>
							x.StoreId == invoice.StoreId &&
							x.ProductId == item.ProductId &&
							x.IsActive &&
							x.QuantityRemaining > 0)
						.OrderBy(x => x.PurchaseDate)
						.ThenBy(x => x.Id)
						.Take(1)
						.Select(x => x.UnitCost)
						.FirstOrDefaultAsync();

					if (estimatedCost > 0)
					{
						itemCost = item.QuantityInBaseUnit * estimatedCost;
					}
				}

				if (itemCost > 0 && itemNetSale < itemCost)
				{
					var expectedLoss = itemCost - itemNetSale;
					var marginPercent = itemNetSale > 0
						? Math.Round((itemNetSale - itemCost) / itemNetSale * 100m, 2)
						: -100m;

					lossWarning +=
						$"<li>{item.Product?.Name ?? "منتج غيرknown"}: " +
						$"البيع {itemNetSale.ToString("N2")} — التكلفة {itemCost.ToString("N2")} — " +
						$"الخسارة المتوقعة {expectedLoss.ToString("N2")} — الهامش {marginPercent.ToString("N2")}%</li>";
				}
			}

			if (!string.IsNullOrEmpty(lossWarning))
			{
				TempData["LossWarning"] = lossWarning;
				TempData["HasLossWarning"] = "1";

				var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
				var hasOverrideLossPermission =
					await _permissionService.HasPermissionAsync(
						int.TryParse(userIdValue, out var uid) ? uid : 0,
						"sales.override_loss");

				if (!hasOverrideLossPermission)
				{
					TempData["Error"] =
						"لا يمكن ترحيل هذه الفاتورة: البيع بسعر أقل من التكلفة يتطلب صلاحية 'التجاوز على خسارة البيع'. " +
						"الخسارة المتوقعة ستترتب على حساب المنشأة.";
					return RedirectToAction(nameof(Details), new { id = invoice.Id });
				}
			}

			var strategy =
				_context.Database.CreateExecutionStrategy();

			string? transactionError = null;

			await strategy.ExecuteAsync(
				async () =>
				{
					await using var transaction =
						await _context.Database.BeginTransactionAsync();
						         // =========================================
                                // ✅ (Idempotency) قفل صف الفاتورة جوه الـ transaction
                                //
                                // لو طلبين Confirm وصلوا هنا في نفس اللحظة،
                                // الأول هياخد القفل والتاني هيتعلّق. لما الأول
                                // يخلّص ويـ commit، التاني هيقرأ Status=Confirmed
                                // ويرجع بأمان بدون إنشاء قيد مكرر.
                                // =========================================

                               // =========================================
// قفل الصف (UPDLOCK) — SQL Server فقط
// InMemory مش بيدعمه، فنستخدم قراءة عادية
// =========================================

SalesInvoiceStatus? lockedStatus;
Console.WriteLine($"[PROVIDER-NAME] '{_context.Database.ProviderName}'");
var isInMemory = !_context.Database.IsRelational();
if (isInMemory)
{
    lockedStatus = await _context.SalesInvoices
        .AsNoTracking()
        .Where(x => x.Id == invoice.Id)
        .Select(x => (SalesInvoiceStatus?)x.Status)
        .FirstOrDefaultAsync();
}
else
{
    lockedStatus = await _context.SalesInvoices
        .FromSqlInterpolated($@"
            SELECT * FROM [SalesInvoices]
            WITH (UPDLOCK, ROWLOCK)
            WHERE [Id] = {invoice.Id}")
        .AsNoTracking()
        .Select(x => (SalesInvoiceStatus?)x.Status)
        .FirstOrDefaultAsync();
}

if (lockedStatus != SalesInvoiceStatus.Draft)
{
    transactionError =
        "الفاتورة تم ترحيلها بالفعل من جلسة أخرى. " +
        "أعد فتح الصفحة لرؤية الحالة المحدثة.";

    return;
}

					decimal totalCostOfGoodsSold = 0;

					foreach (var item in invoice.Items)
					{
						var quantityInBaseUnit = item.QuantityInBaseUnit;

						if (item.IssueMethod == StockIssueMethod.Manual &&
							item.SelectedStockLotId.HasValue)
						{
							var lot = await _context.StockLots
								.FirstAsync(x =>
									x.Id == item.SelectedStockLotId.Value);

							_context.SalesInvoiceItemLots.Add(
								new SalesInvoiceItemLot
								{
									SalesInvoiceItemId = item.Id,
									StockLotId = lot.Id,
									QuantityBaseUnit = quantityInBaseUnit,
									UnitCost = lot.UnitCost,
									TotalCost = quantityInBaseUnit * lot.UnitCost
								});

							lot.QuantityRemaining -= quantityInBaseUnit;
							totalCostOfGoodsSold += quantityInBaseUnit * lot.UnitCost;
						}
						else
						{
							var remainingToIssue = quantityInBaseUnit;

							var lots = await _context.StockLots
								.Where(x =>
									x.StoreId == invoice.StoreId &&
									x.ProductId == item.ProductId &&
									x.IsActive &&
									x.QuantityRemaining > 0)
								.OrderBy(x => x.PurchaseDate)
								.ThenBy(x => x.Id)
								.ToListAsync();

							foreach (var lot in lots)
							{
								if (remainingToIssue <= 0)
								{
									break;
								}

								var issueFromLot = Math.Min(
									lot.QuantityRemaining,
									remainingToIssue);

								_context.SalesInvoiceItemLots.Add(
									new SalesInvoiceItemLot
									{
										SalesInvoiceItemId = item.Id,
										StockLotId = lot.Id,
										QuantityBaseUnit = issueFromLot,
										UnitCost = lot.UnitCost,
										TotalCost = issueFromLot * lot.UnitCost
									});

								lot.QuantityRemaining -= issueFromLot;
								remainingToIssue -= issueFromLot;
								totalCostOfGoodsSold += issueFromLot * lot.UnitCost;
							}

							if (remainingToIssue > 0)
							{
								transactionError =
									"حدث خطأ أثناء صرف المخزون.";

								return;
							}
						}
					}

					invoice.Status = SalesInvoiceStatus.Confirmed;
					invoice.ConfirmedAt = DateTime.UtcNow;

					var postingResult =
						await _postingService.PostSalesInvoiceAsync(
							invoice,
							totalCostOfGoodsSold,
							CurrentUserId(),
							cashAccountId);

					if (!postingResult.Success)
					{
						transactionError =
							"تعذر اعتماد الفاتورة — لم يُراجع الحساب المحاسبي: " +
							postingResult.Error;

						return;
					}

					try
					{
						await _context.SaveChangesAsync();
					}
					catch (DbUpdateConcurrencyException)
					{
						transactionError =
							"تم تعديل الكميات من مستخدم آخر أثناء الحفظ. " +
							"أعد المحاولة — لن يتم ترحيل الفاتورة.";

						return;
					}
					catch (DbUpdateException ex)
					{
						transactionError =
							"تعذر اعتماد الفاتورة: " +
							(ex.InnerException?.Message ?? ex.Message);

						return;
					}
					catch (InvalidOperationException)
					{
						transactionError =
							"تعذر اعتماد الفاتورة: تداخل في العمليات. أعد المحاولة.";

						return;
					}

					await transaction.CommitAsync();
				});

		if (transactionError != null)
{
    Console.WriteLine($"[CONFIRM-ERROR] {transactionError}");
    TempData["Error"] = transactionError;
    return RedirectToAction(nameof(Index));
}

			TempData["Success"] =
				"تم اعتماد الفاتورة وصرف الكميات من المخزون.";

			if (invoice.Customer != null &&
				!string.IsNullOrWhiteSpace(invoice.Customer.Phone))
			{
				try
				{
					var pdfPath =
						await _pdfInvoiceService
							.GenerateSalesInvoicePdfAsync(invoice.Id);

					if (!string.IsNullOrEmpty(pdfPath))
					{
						var fileName =
							$"فاتورة-{invoice.InvoiceNumber}.pdf";

						var whatsappOk =
							await _whatsAppService.SendDocumentAsync(
								invoice.Customer.Phone,
								invoice.Customer.Name,
								pdfPath,
								fileName,
								$"فاتورة بيع {invoice.InvoiceNumber} — الإجمالي: {invoice.TotalAmount:N2} ج.م");

						TempData["Success"] +=
							whatsappOk
								? " وتم إرسال الفاتورة للعميل عبر واتساب."
								: " (تعذر إرسال واتساب للعميل).";

						try
						{
							System.IO.File.Delete(pdfPath);
						}
						catch
						{
							// حذف مؤقت
						}
					}
				}
				catch (Exception whatsappEx)
				{
					TempData["Success"] +=
						" (تعذر إرسال واتساب للعميل: " +
						whatsappEx.Message + ").";
				}
			}

			return RedirectToAction(nameof(Index));
		}

		// =========================================
		// إلغاء الفاتورة
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("sales.cancel")]
		public async Task<IActionResult> Cancel(int id)
		{
			var invoice = await _context.SalesInvoices
				.Include(x => x.Items)
				.Include(x => x.Customer)
				.Include(x => x.Items)
					.ThenInclude(i => i.LotAllocations)
						.ThenInclude(sl => sl.StockLot)
				.FirstOrDefaultAsync(x => x.Id == id);

			if (invoice == null)
			{
				return NotFound();
			}

			var scope = await _employeeScopeService.GetScopeAsync();

			if (!scope.CanAccessStore(invoice.StoreId))
			{
				TempData["Error"] =
					"ليس لديك صلاحية على مخزن هذه الفاتورة.";

				return RedirectToAction(nameof(Index));
			}

			if (invoice.Status == SalesInvoiceStatus.Cancelled)
			{
				TempData["Error"] =
					"الفاتورة مُلغاة بالفعل.";

				return RedirectToAction(nameof(Index));
			}

			if (invoice.Status == SalesInvoiceStatus.Draft)
			{
				invoice.Status = SalesInvoiceStatus.Cancelled;

				await _context.SaveChangesAsync();

				TempData["Success"] = "تم إلغاء الفاتورة.";

				return RedirectToAction(nameof(Index));
			}

			var strategy =
				_context.Database.CreateExecutionStrategy();

			string? transactionError = null;

			await strategy.ExecuteAsync(
				async () =>
				{
					await using var transaction =
						await _context.Database.BeginTransactionAsync();

					try
					{
						var totalCost = invoice.Items
							.SelectMany(i => i.LotAllocations)
							.Sum(sl => sl.TotalCost);

						foreach (var item in invoice.Items)
							foreach (var itemLot in item.LotAllocations)
							{
								if (itemLot.StockLot != null)
								{
									itemLot.StockLot.QuantityRemaining +=
										itemLot.QuantityBaseUnit;
								}
							}

						foreach (var item in invoice.Items)
						{
							var lots = item.LotAllocations.ToList();

							foreach (var sl in lots)
							{
								_context.SalesInvoiceItemLots.Remove(sl);
							}
						}

						if (invoice.PaymentMethod != SalesPaymentMethod.Credit)
						{
							var treasuryEntry =
								await _context.TreasuryTransactions
									.Where(x =>
										x.Type == TreasuryTransactionType.Receive &&
										x.ReferenceDocument == invoice.InvoiceNumber &&
										x.CustomerId == invoice.CustomerId)
									.OrderByDescending(x => x.CreatedAt)
									.FirstOrDefaultAsync();

							if (treasuryEntry != null)
							{
								_context.TreasuryTransactions.Add(
									new TreasuryTransaction
									{
										TransactionNumber =
											await GenerateReceiptNumberAsync(),
										Type =
											TreasuryTransactionType.Pay,
										CashAccountId =
											treasuryEntry.CashAccountId,
										CustomerId = invoice.CustomerId,
										Amount = treasuryEntry.Amount,
										Reason = "إلغاء تحصيل فاتورة بيع",
										ReferenceDocument =
											invoice.InvoiceNumber,
										AutoJournal = false,
										CreatedByUserId = CurrentUserId(),
										CreatedAt = DateTime.UtcNow
									});
							}
						}

						var reversal =
							await _postingService.PostSalesInvoiceCancelAsync(
								invoice,
								totalCost,
								CurrentUserId());

						if (!reversal.Success)
						{
							transactionError =
								"تعذر إلغاء الفاتورة — لم يُرجع الحساب المحاسبي: " +
								reversal.Error;

							return;
						}

						invoice.Status = SalesInvoiceStatus.Cancelled;

						await _context.SaveChangesAsync();

						await transaction.CommitAsync();

						TempData["Success"] =
							"تم إلغاء الفاتورة المعتمدة وترحيل القيد العكسي " +
							reversal.EntryNumber +
							" وإرجاع الكميات للمخزون.";
					}
					catch (Exception ex)
					{
						await transaction.RollbackAsync();

						transactionError =
							"تعذر إلغاء الفاتورة: " + ex.Message;
					}
				});

			if (transactionError != null)
			{
				TempData["Error"] = transactionError;
			}

			return RedirectToAction(nameof(Index));
		}

		// =========================================
		// اختبار اتصال واتساب
		// =========================================

		[HttpGet]
		public async Task<IActionResult> TestWhatsApp(string phone)
		{
			phone = string.IsNullOrWhiteSpace(phone)
				? "01116957655"
				: phone;

			var result =
				await _whatsAppService.SendTestAsync(phone);

			TempData[result.Ok ? "Success" : "Error"] =
				result.Details;

			return RedirectToAction(nameof(Index));
		}

		// =========================================
		// إرسال فاتورة بيع واتساب PDF (يدوي)
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("sales.view")]
		public async Task<IActionResult> SendWhatsApp(int id)
		{
			var invoice =
				await _context.SalesInvoices
					.AsNoTracking()
					.Include(x => x.Customer)
					.FirstOrDefaultAsync(x => x.Id == id);

			if (invoice == null)
			{
				return NotFound();
			}

			var scope = await _employeeScopeService.GetScopeAsync();

			if (!scope.CanAccessStore(invoice.StoreId))
			{
				TempData["Error"] =
					"ليس لديك صلاحية على مخزن هذه الفاتورة.";

				return RedirectToAction(nameof(Index));
			}

			if (invoice.Customer == null ||
				string.IsNullOrWhiteSpace(invoice.Customer.Phone))
			{
				TempData["Error"] =
					"لا يوجد هاتف مسجل للعميل لإرسال الفاتورة.";

				return RedirectToAction(nameof(Details), new { id });
			}

			var pdfPath =
				await _pdfInvoiceService
					.GenerateSalesInvoicePdfAsync(invoice.Id);

			if (string.IsNullOrEmpty(pdfPath))
			{
				TempData["Error"] =
					"تعذر توليد ملف PDF للفاتورة.";

				return RedirectToAction(nameof(Details), new { id });
			}

			var fileName =
				$"فاتورة-{invoice.InvoiceNumber}.pdf";

			var ok =
				await _whatsAppService.SendDocumentAsync(
					invoice.Customer.Phone,
					invoice.Customer.Name,
					pdfPath,
					fileName,
					$"فاتورة بيع {invoice.InvoiceNumber} — الإجمالي: {invoice.TotalAmount:N2} ج.م");

			try
			{
				System.IO.File.Delete(pdfPath);
			}
			catch
			{
				// حذف مؤقت
			}

			TempData[ok ? "Success" : "Error"] =
				ok
					? "تم إرسال الفاتورة للعميل عبر واتساب."
					: "تعذر إرسال الفاتورة عبر واتساب. تأكد من إعداداتك (Token/PhoneNumberId).";

			return RedirectToAction(nameof(Details), new { id });
		}

		// =========================================
		// إرسال تذكير دفع عبر واتساب
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("sales.confirm")]
		public async Task<IActionResult> SendReminder(int id)
		{
			var invoice = await _context.SalesInvoices
				.AsNoTracking()
				.Include(x => x.Customer)
				.FirstOrDefaultAsync(x => x.Id == id);

			if (invoice == null)
			{
				return NotFound();
			}

			var scope = await _employeeScopeService.GetScopeAsync();

			if (!scope.CanAccessStore(invoice.StoreId))
			{
				TempData["Error"] = "ليس لديك صلاحية على مخزن هذه الفاتورة.";
				return RedirectToAction(nameof(Details), new { id });
			}

			if (invoice.Customer == null ||
				string.IsNullOrWhiteSpace(invoice.Customer.Phone))
			{
				TempData["Error"] = "لا يوجد هاتف مسجل للعميل.";
				return RedirectToAction(nameof(Details), new { id });
			}

			try
			{
				await _reminderService
					.SendManualReminderAsync(id, 1, CurrentUserId());

				TempData["Success"] = "تم إرسال تذكير الدفع للعميل عبر واتساب.";
			}
			catch (Exception ex)
			{
				TempData["Error"] = "تعذر إرسال التذكير: " + ex.Message;
			}

			return RedirectToAction(nameof(Details), new { id });
		}

		// =========================================
		// تفاصيل فاتورة بيع
		// =========================================

		[HttpGet]
		[RequirePermission("sales.view")]
		public async Task<IActionResult> Details(int id)
		{
			var invoice =
				await _context.SalesInvoices
					.AsNoTracking()
					.Include(x => x.Branch)
					.Include(x => x.Store)
					.Include(x => x.Customer)
					.Include(x => x.CreatedByUser)
					.Include(x => x.Items)
						.ThenInclude(x => x.Product)
					.Include(x => x.Items)
						.ThenInclude(x => x.Unit)
					.Include(x => x.Items)
						.ThenInclude(x => x.LotAllocations)
							.ThenInclude(x => x.StockLot)
								.ThenInclude(x => x!.Supplier)
					.FirstOrDefaultAsync(x => x.Id == id);

			if (invoice == null)
			{
				return NotFound();
			}

			var scope = await _employeeScopeService.GetScopeAsync();

			if (!scope.CanAccessStore(invoice.StoreId))
			{
				return RedirectToAction("AccessDenied", "Account");
			}

			return View(invoice);
		}

		// =========================================
		// جلب الدفعات المتاحة (AJAX)
		// =========================================

		[HttpGet]
		[RequirePermission("sales.create")]
		public async Task<IActionResult> GetAvailableLots(
			int productId,
			int storeId)
		{
			if (productId <= 0 || storeId <= 0)
			{
				return Json(new List<object>());
			}

			var scope = await _employeeScopeService.GetScopeAsync();

			if (!scope.CanAccessStore(storeId))
			{
				return Json(new List<object>());
			}

			var lots = await _context.StockLots
				.AsNoTracking()
				.Include(x => x.Supplier)
				.Where(x =>
					x.ProductId == productId &&
					x.StoreId == storeId &&
					x.IsActive &&
					x.QuantityRemaining > 0)
				.OrderBy(x => x.PurchaseDate)
				.ThenBy(x => x.Id)
				.Select(x => new
				{
					id = x.Id,
					supplierName = x.Supplier != null
						? x.Supplier.Name
						: "دفعة نظامية",
					purchaseDate = x.PurchaseDate.ToString("yyyy-MM-dd"),
					quantityRemaining = x.QuantityRemaining,
					unitCost = x.UnitCost
				})
				.ToListAsync();

			return Json(lots);
		}

		// =========================================
		// تحميل بيانات شاشة الإضافة (مفلترة بالـScope)
		// =========================================

		private async Task LoadCreateDataAsync(
			SalesInvoiceCreateViewModel model)
		{
			var scope = await _employeeScopeService.GetScopeAsync();

			model.CompanyLogoPath =
				await _context.Companies
					.AsNoTracking()
					.Where(x => x.IsActive)
					.OrderBy(x => x.Id)
					.Select(x => x.LogoPath)
					.FirstOrDefaultAsync();

			var branchesQuery = _context.Branches
				.AsNoTracking()
				.Where(x => x.IsActive);

			branchesQuery = _employeeScopeService
				.ApplyBranchFilter(branchesQuery, scope);

			model.AvailableBranches = await branchesQuery
				.OrderBy(x => x.Name)
				.Select(x => new BranchOptionViewModel
				{
					Id = x.Id,
					Name = x.Name
				})
				.ToListAsync();

			var storesQuery = _context.Stores
				.AsNoTracking()
				.Where(x =>
					x.IsActive &&
					x.Branch != null &&
					x.Branch.IsActive);

			storesQuery = _employeeScopeService
				.ApplyStoreFilter(storesQuery, scope);

			model.AvailableStores = await storesQuery
				.OrderBy(x => x.Name)
				.Select(x => new StoreOptionViewModel
				{
					Id = x.Id,
					BranchId = x.BranchId,
					Name = x.Name
				})
				.ToListAsync();

			var currentRepId = await _salesRepService.GetCurrentRepIdAsync();

			model.AvailableCustomers =
				await _context.Customers
					.AsNoTracking()
					.Where(x => x.IsActive &&
						(currentRepId == null || x.SalesRepId == currentRepId))
					.OrderBy(x => x.Name)
					.Select(x => new CustomerOptionViewModel
					{
						Id = x.Id,
						Name = x.Name,
						Phone = x.Phone
					})
					.ToListAsync();

			var products = await _context.Products
				.AsNoTracking()
				.Include(x => x.BaseUnit)
				.Include(x => x.ProductUnits)
					.ThenInclude(x => x.Unit)
				.Where(x => x.IsActive)
				.OrderBy(x => x.Name)
				.ToListAsync();

			model.AvailableCashAccounts =
				await _context.CashAccounts
					.AsNoTracking()
					.Where(x => x.IsActive)
					.OrderBy(x => x.Name)
					.Select(x =>
						new KeyValuePair<int, string>(
							x.Id,
							x.Name))
					.ToListAsync();

			model.AvailableProducts =
				products.Select(x => new SalesProductOptionViewModel
				{
					Id = x.Id,
					Name = x.Name,
					Barcode = x.Barcode,
					BaseUnitId = x.BaseUnitId,
					BaseUnitName = x.BaseUnit?.Name ?? "-",
					Units = x.ProductUnits
						.Where(u => u.IsActive)
						.Select(u => new SalesProductUnitOptionViewModel
						{
							UnitId = u.UnitId,
							UnitName = u.Unit?.Name ?? "-",
							ConversionFactor = u.ConversionFactor,
							SalePrice = u.SalePrice
						})
						.ToList()
				})
				.ToList();
		}

		// =========================================
		// صلاحية تعديل السعر
		// =========================================

		private bool CanOverridePrice()
		{
			if (User.IsInRole("Admin"))
			{
				return true;
			}

			return User.HasClaim(
				"Permission",
				"sales.price.override");
		}

		private async Task<string> GenerateReceiptNumberAsync()
		{
			var datePart =
				DateTime.Today.ToString("yyyyMMdd");

			var prefix = $"RCP-{datePart}-";

			var lastNumber = await _context.TreasuryTransactions
				.AsNoTracking()
				.Where(x =>
					x.Type ==
						TreasuryTransactionType.Receive &&
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
		// إضافة عميل سريع (AJAX) من شاشة الفاتورة
		// اسم + هاتف فقط — دون مغادرة الصفحة
		// =========================================

		[HttpPost]
		[RequirePermission("sales.create")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> CreateQuickCustomer(
			string name,
			string? phone)
		{
			name = (name ?? "").Trim();
			phone = (phone ?? "").Trim();

			if (string.IsNullOrEmpty(name))
			{
				return Json(new { ok = false, message = "اسم العميل مطلوب." });
			}

			if (string.IsNullOrEmpty(phone))
			{
				return Json(new { ok = false, message = "رقم الهاتف مطلوب." });
			}

			var phoneExists = await _context.Customers
				.AnyAsync(x => x.Phone == phone);

			if (phoneExists)
			{
				return Json(new { ok = false, message = "رقم الهاتف مستخدم بالفعل لعميل آخر." });
			}

			// ✅ تثبيت العميل الجديد على المندوب (لو المستخدم مندوب)
			var repId = await _salesRepService.GetCurrentRepIdAsync();

			var customer = new Customer
			{
				Name = name,
				Phone = phone,
				SalesRepId = repId,
				CreatedAt = DateTime.UtcNow,
				IsActive = true
			};

			_context.Customers.Add(customer);
			await _context.SaveChangesAsync();

			return Json(new
			{
				ok = true,
				id = customer.Id,
				name = customer.Name,
				phone = customer.Phone
			});
		}

		// =========================================
		// بيانات العميل: الرصيد + بوابة العميل
		// =========================================

		[HttpGet]
		[RequirePermission("sales.view")]
		public async Task<IActionResult> GetCustomerInfo(int id)
		{
			if (id <= 0)
				return Json(new { ok = false });

			var customer = await _context.Customers
				.AsNoTracking()
				.Where(x => x.Id == id)
				.Select(x => new
				{
					x.Id,
					x.Name,
					x.Phone,
					x.HasPortalAccount,
					x.PortalApproved,
					x.PortalRequested
				})
				.FirstOrDefaultAsync();

			if (customer == null)
				return Json(new { ok = false });

			// ═══════════════════════════════════════
			// الرصيد: من حساب العملاء (1103) فقط
			// (تجاهل السطور الأخرى التي تحمل نفس CustomerId)
			// ═══════════════════════════════════════

			var customerAccountId = await _context.ChartAccounts
				.AsNoTracking()
				.Where(x => x.Code == "1103" && x.IsActive)
				.Select(x => (int?)x.Id)
				.FirstOrDefaultAsync();

			decimal balance = 0;

			if (customerAccountId.HasValue)
			{
				var sums = await _context.JournalEntryLines
					.AsNoTracking()
					.Where(x =>
						x.CustomerId == id &&
						x.ChartAccountId == customerAccountId.Value)
					.GroupBy(x => x.CustomerId)
					.Select(g => new
					{
						TotalDebit = g.Sum(x => x.Debit),
						TotalCredit = g.Sum(x => x.Credit)
					})
					.FirstOrDefaultAsync();

				if (sums != null)
				{
					// حساب العملاء = أصل
					// الرصيد المدين = كم لنا عنده
					balance = sums.TotalDebit - sums.TotalCredit;
				}
			}

			return Json(new
			{
				ok = true,
				id = customer.Id,
				name = customer.Name,
				phone = customer.Phone,
				balance = balance,
				hasPortalAccount = customer.HasPortalAccount,
				portalApproved = customer.PortalApproved,
				portalRequested = customer.PortalRequested
			});
		}

		// =========================================
		// إضافة مخزن سريع (AJAX) من شاشة الفاتورة
		// اسم + فرع — دون مغادرة الصفحة
		// =========================================

		[HttpPost]
		[RequirePermission("sales.create")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> CreateQuickStore(
			string name,
			int branchId)
		{
			name = (name ?? "").Trim();

			if (string.IsNullOrEmpty(name))
			{
				return Json(new { ok = false, message = "اسم المخزن مطلوب." });
			}

			var branch = await _context.Branches
				.FirstOrDefaultAsync(x =>
					x.Id == branchId &&
					x.IsActive);

			if (branch == null)
			{
				return Json(new { ok = false, message = "الفرع المحدد غير موجود أو غير نشط." });
			}

			var storeExists = await _context.Stores
				.AnyAsync(x =>
					x.BranchId == branchId &&
					x.Name == name);

			if (storeExists)
			{
				return Json(new { ok = false, message = "اسم المخزن موجود بالفعل داخل هذا الفرع." });
			}

			var store = new Store
			{
				BranchId = branchId,
				Name = name,
				CreatedAt = DateTime.UtcNow,
				IsActive = true
			};

			_context.Stores.Add(store);
			await _context.SaveChangesAsync();

			return Json(new
			{
				ok = true,
				id = store.Id,
				name = store.Name,
				branchId = store.BranchId
			});
		}

		// =========================================
		// إضافة مركز نقدي سريع (AJAX) من شاشة الفاتورة
		// اسم فقط — بيربط تلقائي بحساب الصندوق (1101)
		// =========================================

		[HttpPost]
		[RequirePermission("sales.create")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> CreateQuickCashAccount(string name)
		{
			name = (name ?? "").Trim();

			if (string.IsNullOrEmpty(name))
			{
				return Json(new { ok = false, message = "اسم المركز النقدي مطلوب." });
			}

			var exists = await _context.CashAccounts
				.AnyAsync(x => x.Name == name);

			if (exists)
			{
				return Json(new { ok = false, message = "يوجد مركز نقدي بنفس الاسم بالفعل." });
			}

			var cashBoxId = await _context.ChartAccounts
				.Where(x => x.Code == "1101" && x.IsActive)
				.Select(x => (int?)x.Id)
				.FirstOrDefaultAsync();

			if (cashBoxId == null)
			{
				return Json(new { ok = false, message = "حساب الصندوق (1101) غير موجود في شجرة الحسابات." });
			}

			var cashAccount = new CashAccount
			{
				Name = name,
				ChartAccountId = cashBoxId,
				IsActive = true,
				CreatedAt = DateTime.UtcNow
			};

			_context.CashAccounts.Add(cashAccount);
			await _context.SaveChangesAsync();

			return Json(new
			{
				ok = true,
				id = cashAccount.Id,
				name = cashAccount.Name
			});
		}
	}
}