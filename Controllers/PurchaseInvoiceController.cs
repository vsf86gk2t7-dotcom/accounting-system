using AccountingSystem.Data;
using AccountingSystem.Filters;
using AccountingSystem.Models;
using AccountingSystem.Models.ViewModels.Purchases;
using AccountingSystem.Models.ViewModels.Reports;
using AccountingSystem.Services;
using AccountingSystem.Services.EmployeeScope;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AccountingSystem.Controllers
{
	public class PurchaseInvoiceController : Controller
	{
		private readonly ApplicationDbContext _context;
		private readonly Services.WhatsApp.IWhatsAppService _whatsAppService;
		private readonly Services.Pdf.IPdfInvoiceService _pdfInvoiceService;
		private readonly IEmployeeScopeService _employeeScopeService;
		private readonly Services.Permissions.IPermissionService _permissionService;
		private readonly IPostingService _postingService;
		private readonly IDocumentNumberService _documentNumberService;
		private readonly INotificationService _notifications;

		// =========================================
		// عتبة الفاتورة الكبيرة
		// =========================================
		private const decimal LargePurchaseThreshold = 50_000m;

		public PurchaseInvoiceController(
			ApplicationDbContext context,
			Services.WhatsApp.IWhatsAppService whatsAppService,
			Services.Pdf.IPdfInvoiceService pdfInvoiceService,
			IEmployeeScopeService employeeScopeService,
			Services.Permissions.IPermissionService permissionService,
			IPostingService postingService,
			IDocumentNumberService documentNumberService,
			INotificationService notifications)
		{
			_context = context;
			_whatsAppService = whatsAppService;
			_pdfInvoiceService = pdfInvoiceService;
			_employeeScopeService = employeeScopeService;
			_permissionService = permissionService;
			_postingService = postingService;
			_documentNumberService = documentNumberService;
			_notifications = notifications;
		}

		// =========================================
		// Helper: بيانات الفاتورة للتقرير الضريبي
		// =========================================

		private sealed class InvoiceInfo
		{
			public int Id { get; set; }
			public string? SupplierName { get; set; }
			public string? StoreName { get; set; }
		}

		// =========================================
		// قائمة فواتير الشراء (مفلترة بالـScope)
		// =========================================

		[HttpGet]
		[RequirePermission("purchase.view")]
		public async Task<IActionResult> Index(int page = 1, int pageSize = 20)
		{
			var scope = await _employeeScopeService.GetScopeAsync();

			var query = _context.PurchaseInvoices
				.AsNoTracking()
				.Include(x => x.Supplier)
				.Include(x => x.Store)
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

			var pagedResult = new Models.ViewModels.Pagination.PagedResult<PurchaseInvoice>
			{
				Items = invoices,
				PageNumber = page,
				PageSize = pageSize,
				TotalCount = totalCount
			};

			return View(pagedResult);
		}

		// =========================================
		// تفاصيل فاتورة شراء
		// =========================================

		[HttpGet]
		[RequirePermission("purchase.view")]
		public async Task<IActionResult> Details(int id)
		{
			var invoice =
				await _context.PurchaseInvoices
					.AsNoTracking()
					.Include(x => x.Supplier)
					.Include(x => x.Store)
					.Include(x => x.Items)
						.ThenInclude(x => x.Product)
					.Include(x => x.Items)
						.ThenInclude(x => x.Unit)
					.Include(x => x.Items)
						.ThenInclude(x => x.StockLot)
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
		// تقرير ضريبي — ض.ق.م (مشتريات) + ضريبة المبيعات
		// =========================================

		[HttpGet]
		[RequirePermission("purchase.view")]
		public async Task<IActionResult> TaxReport(
			DateTime? fromDate,
			DateTime? toDate,
			int? storeId,
			int? supplierId)
		{
			var from = fromDate?.Date
				?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

			var to = toDate?.Date ?? DateTime.Today;

			if (from > to)
			{
				(from, to) = (to, from);
			}

			var scope = await _employeeScopeService.GetScopeAsync();

			var allowedStoreIds = scope.IsRestricted
				? scope.StoreIds.ToHashSet()
				: null;

			var vatPurchaseAccountId =
				await _context.ChartAccounts
					.AsNoTracking()
					.Where(x => x.Code == "1105" && x.IsActive)
					.Select(x => (int?)x.Id)
					.FirstOrDefaultAsync();

			var salesTaxAccountId =
				await _context.ChartAccounts
					.AsNoTracking()
					.Where(x => x.Code == "1106" && x.IsActive)
					.Select(x => (int?)x.Id)
					.FirstOrDefaultAsync();

			decimal vatOpening = 0;
			decimal salesTaxOpening = 0;

			if (vatPurchaseAccountId.HasValue)
			{
				var vatOpenQuery = _context.JournalEntryLines
					.AsNoTracking()
					.Where(x =>
						x.ChartAccountId == vatPurchaseAccountId.Value &&
						x.JournalEntry!.EntryDate < from);

				if (allowedStoreIds != null)
				{
					vatOpenQuery = vatOpenQuery.Where(x =>
						x.StoreId.HasValue && allowedStoreIds.Contains(x.StoreId.Value));
				}

				var vatOpen = await vatOpenQuery
					.GroupBy(x => 1)
					.Select(g => new
					{
						D = g.Sum(x => x.Debit),
						C = g.Sum(x => x.Credit)
					})
					.FirstOrDefaultAsync();

				if (vatOpen != null)
				{
					vatOpening = vatOpen.D - vatOpen.C;
				}
			}

			if (salesTaxAccountId.HasValue)
			{
				var stOpenQuery = _context.JournalEntryLines
					.AsNoTracking()
					.Where(x =>
						x.ChartAccountId == salesTaxAccountId.Value &&
						x.JournalEntry!.EntryDate < from);

				if (allowedStoreIds != null)
				{
					stOpenQuery = stOpenQuery.Where(x =>
						x.StoreId.HasValue && allowedStoreIds.Contains(x.StoreId.Value));
				}

				var stOpen = await stOpenQuery
					.GroupBy(x => 1)
					.Select(g => new
					{
						D = g.Sum(x => x.Debit),
						C = g.Sum(x => x.Credit)
					})
					.FirstOrDefaultAsync();

				if (stOpen != null)
				{
					salesTaxOpening = stOpen.D - stOpen.C;
				}
			}

			var accountIds = new List<int>();
			if (vatPurchaseAccountId.HasValue) accountIds.Add(vatPurchaseAccountId.Value);
			if (salesTaxAccountId.HasValue) accountIds.Add(salesTaxAccountId.Value);

			var linesQuery = _context.JournalEntryLines
				.AsNoTracking()
				.Include(x => x.JournalEntry)
				.Include(x => x.ChartAccount)
				.Include(x => x.Supplier)
				.Include(x => x.Store)
				.Where(x =>
					accountIds.Contains(x.ChartAccountId) &&
					x.JournalEntry!.EntryDate >= from &&
					x.JournalEntry!.EntryDate <= to);
			if (allowedStoreIds != null)
			{
				linesQuery = linesQuery.Where(x =>
					x.StoreId.HasValue && allowedStoreIds.Contains(x.StoreId.Value));
			}

			if (storeId.HasValue)
			{
				linesQuery = linesQuery.Where(x => x.StoreId == storeId.Value);
			}

			if (supplierId.HasValue)
			{
				linesQuery = linesQuery.Where(x => x.SupplierId == supplierId.Value);
			}

			var rawLines = await linesQuery
				.OrderBy(x => x.JournalEntry!.EntryDate)
				.ThenBy(x => x.JournalEntry!.Id)
				.Select(x => new
				{
					x.JournalEntryId,
					EntryNumber = x.JournalEntry!.EntryNumber,
					EntryDate = x.JournalEntry!.EntryDate,
					SourceType = x.JournalEntry!.SourceType,
					SourceId = x.JournalEntry!.SourceId,
					x.Description,
					LineSupplierName = x.Supplier != null ? x.Supplier.Name : null,
					LineStoreName = x.Store != null ? x.Store.Name : null,
					x.ChartAccountId,
					x.Debit,
					x.Credit
				})
				.ToListAsync();

			var invoiceIds = rawLines
				.Where(x =>
					(x.SourceType == JournalSourceType.PurchaseInvoice ||
					 x.SourceType == JournalSourceType.PurchaseCancel))
				.Select(x => x.SourceId)
				.Distinct()
				.ToList();

			var invoiceInfoMap =
				invoiceIds.Count > 0
					? await _context.PurchaseInvoices
						.AsNoTracking()
						.Where(x => invoiceIds.Contains(x.Id))
						.Select(x => new InvoiceInfo
						{
							Id = x.Id,
							SupplierName = x.Supplier != null
								? x.Supplier.Name
								: null,
							StoreName = x.Store != null
								? x.Store.Name
								: null
						})
						.ToDictionaryAsync(x => x.Id)
					: new Dictionary<int, InvoiceInfo>();

			var grouped = rawLines
				.GroupBy(x => x.JournalEntryId)
				.Select(g =>
				{
					var first = g.First();

					string? supplierName = first.LineSupplierName;

					if (supplierName == null &&
						first.SourceId.HasValue &&
						invoiceInfoMap.TryGetValue(first.SourceId.Value, out var infoSup))
					{
						supplierName = infoSup.SupplierName;
					}

					supplierName ??= g.Select(x => x.LineSupplierName)
						.FirstOrDefault(x => x != null);

					string? storeName = first.LineStoreName;

					if (storeName == null &&
						first.SourceId.HasValue &&
						invoiceInfoMap.TryGetValue(first.SourceId.Value, out var infoSt))
					{
						storeName = infoSt.StoreName;
					}

					storeName ??= g.Select(x => x.LineStoreName)
						.FirstOrDefault(x => x != null);

					var vatD = g.Where(x =>
							vatPurchaseAccountId.HasValue &&
							x.ChartAccountId == vatPurchaseAccountId.Value)
						.Sum(x => x.Debit);

					var vatC = g.Where(x =>
							vatPurchaseAccountId.HasValue &&
							x.ChartAccountId == vatPurchaseAccountId.Value)
						.Sum(x => x.Credit);

					var stD = g.Where(x =>
							salesTaxAccountId.HasValue &&
							x.ChartAccountId == salesTaxAccountId.Value)
						.Sum(x => x.Debit);

					var stC = g.Where(x =>
							salesTaxAccountId.HasValue &&
							x.ChartAccountId == salesTaxAccountId.Value)
						.Sum(x => x.Credit);

					return new TaxReportLineViewModel
					{
						JournalEntryId = first.JournalEntryId,
						EntryNumber = first.EntryNumber ?? "",
						EntryDate = first.EntryDate,
						SourceTypeName = GetSourceTypeName(first.SourceType),
						SourceId = first.SourceId,
						ReferenceNumber = $"#{first.SourceId}",
						Description = first.Description,
						SupplierName = supplierName,
						StoreName = storeName,
						VatDebit = vatD,
						VatCredit = vatC,
						SalesTaxDebit = stD,
						SalesTaxCredit = stC
					};
				})
				.OrderBy(x => x.EntryDate)
				.ThenBy(x => x.JournalEntryId)
				.ToList();

			decimal vatDebit = grouped.Sum(x => x.VatDebit);
			decimal vatCredit = grouped.Sum(x => x.VatCredit);
			decimal stDebit = grouped.Sum(x => x.SalesTaxDebit);
			decimal stCredit = grouped.Sum(x => x.SalesTaxCredit);

			var storesQuery = _context.Stores
				.AsNoTracking()
				.Include(x => x.Branch)
				.Where(x => x.IsActive && x.Branch != null && x.Branch.IsActive);

			storesQuery = _employeeScopeService.ApplyStoreFilter(storesQuery, scope);

			var availableStores = await storesQuery
				.OrderBy(x => x.Branch!.Name)
				.ThenBy(x => x.Name)
				.Select(x => new StoreOptionViewModel
				{
					Id = x.Id,
					BranchId = x.BranchId,
					Name = x.Name,
					BranchName = x.Branch!.Name
				})
				.ToListAsync();

			var availableSuppliers = await _context.Suppliers
				.AsNoTracking()
				.Where(x => x.IsActive)
				.OrderBy(x => x.Name)
				.Select(x => new SupplierOptionViewModel
				{
					Id = x.Id,
					Name = x.Name,
					Phone = x.Phone
				})
				.ToListAsync();

			var model = new TaxReportViewModel
			{
				FromDate = from,
				ToDate = to,
				StoreId = storeId,
				SupplierId = supplierId,

				VatOpeningBalance = vatOpening,
				VatDebit = vatDebit,
				VatCredit = vatCredit,
				VatClosingBalance = vatOpening + vatDebit - vatCredit,

				SalesTaxOpeningBalance = salesTaxOpening,
				SalesTaxDebit = stDebit,
				SalesTaxCredit = stCredit,
				SalesTaxClosingBalance = salesTaxOpening + stDebit - stCredit,

				Lines = grouped,
				AvailableStores = availableStores,
				AvailableSuppliers = availableSuppliers
			};

			return View(model);
		}

		// =========================================
		// إرسال فاتورة شراء واتساب PDF (يدوي)
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("purchase.view")]
		public async Task<IActionResult> SendWhatsApp(int id)
		{
			var invoice =
				await _context.PurchaseInvoices
					.AsNoTracking()
					.Include(x => x.Supplier)
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

			if (invoice.Supplier == null ||
				string.IsNullOrWhiteSpace(invoice.Supplier.Phone))
			{
				TempData["Error"] =
					"لا يوجد هاتف مسجل للمورد لإرسال الفاتورة.";

				return RedirectToAction(nameof(Details), new { id });
			}

			var pdfPath =
				await _pdfInvoiceService
					.GeneratePurchaseInvoicePdfAsync(invoice.Id);

			if (string.IsNullOrEmpty(pdfPath))
			{
				TempData["Error"] =
					"تعذر توليد ملف PDF للفاتورة.";

				return RedirectToAction(nameof(Details), new { id });
			}

			var fileName =
				$"فاتورة-شراء-{invoice.InvoiceNumber}.pdf";

			var ok =
				await _whatsAppService.SendDocumentAsync(
					invoice.Supplier.Phone,
					invoice.Supplier.Name,
					pdfPath,
					fileName,
					$"فاتورة شراء {invoice.InvoiceNumber} — الإجمالي: {invoice.TotalAmount:N2} ج.م");

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
					? "تم إرسال الفاتورة للمورد عبر واتساب."
					: "تعذر إرسال الفاتورة عبر واتساب. تأكد من إعداداتنا (Token/PhoneNumberId).";

			return RedirectToAction(nameof(Details), new { id });
		}

		// =========================================
		// إنشاء فاتورة شراء - GET
		// =========================================

		[HttpGet]
		[RequirePermission("purchase.create")]
		public async Task<IActionResult> Create()
		{
			var model = new PurchaseInvoiceCreateViewModel();

			model.InvoiceNumber =
				await _documentNumberService.GenerateNumberAsync("PUR", "PurchaseInvoices", DateTime.Today.ToString("yyyyMMdd"));

			await LoadCreateDataAsync(model);

			return View(model);
		}

		// =========================================
		// إنشاء فاتورة شراء - POST
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("purchase.create")]
		public async Task<IActionResult> Create(
			PurchaseInvoiceCreateViewModel model)
		{
			model.InvoiceNumber =
				model.InvoiceNumber?.Trim() ?? string.Empty;

			if (string.IsNullOrWhiteSpace(model.InvoiceNumber))
			{
				model.InvoiceNumber =
					await _documentNumberService.GenerateNumberAsync("PUR", "PurchaseInvoices", DateTime.Today.ToString("yyyyMMdd"));

				ModelState.Remove(nameof(model.InvoiceNumber));
			}

			model.Notes =
				string.IsNullOrWhiteSpace(model.Notes)
					? null
					: model.Notes.Trim();

			model.Items ??=
				new List<PurchaseInvoiceItemCreateViewModel>();

			if (!ModelState.IsValid)
			{
				await LoadCreateDataAsync(model);
				return View(model);
			}

			var invoiceNumberExists =
				await _context.PurchaseInvoices
					.AnyAsync(x =>
						x.InvoiceNumber == model.InvoiceNumber);

			if (invoiceNumberExists)
			{
				ModelState.AddModelError(
					nameof(model.InvoiceNumber),
					"رقم الفاتورة مستخدم بالفعل.");

				await LoadCreateDataAsync(model);
				return View(model);
			}

			var supplier = await _context.Suppliers
				.FirstOrDefaultAsync(x =>
					x.Id == model.SupplierId &&
					x.IsActive);

			if (supplier == null)
			{
				ModelState.AddModelError(
					nameof(model.SupplierId),
					"المورد غير موجود أو غير نشط.");

				await LoadCreateDataAsync(model);
				return View(model);
			}

			var store = await _context.Stores
				.Include(x => x.Branch)
				.FirstOrDefaultAsync(x =>
					x.Id == model.StoreId &&
					x.IsActive);

			if (store == null ||
				store.Branch == null ||
				!store.Branch.IsActive)
			{
				ModelState.AddModelError(
					nameof(model.StoreId),
					"المخزن غير موجود أو غير نشط.");

				await LoadCreateDataAsync(model);
				return View(model);
			}

			var scope = await _employeeScopeService.GetScopeAsync();

			if (!scope.CanAccessStore(store.Id))
			{
				ModelState.AddModelError(
					nameof(model.StoreId),
					"ليس لديك صلاحية على هذا المخزن.");

				await LoadCreateDataAsync(model);
				return View(model);
			}

			if (model.BranchId.HasValue &&
				store.BranchId != model.BranchId.Value)
			{
				ModelState.AddModelError(
					nameof(model.BranchId),
					"المخزن المختار لا يتبع الفرع المحدد.");

				await LoadCreateDataAsync(model);
				return View(model);
			}

			model.Items = model.Items
				.Where(x => x.ProductId > 0 && x.UnitId > 0)
				.ToList();

			if (!model.Items.Any())
			{
				ModelState.AddModelError(
					nameof(model.Items),
					"يجب إضافة بند واحد على الأقل.");

				await LoadCreateDataAsync(model);
				return View(model);
			}

			var productIds = model.Items
				.Select(x => x.ProductId)
				.Distinct()
				.ToList();

			var products = await _context.Products
				.Include(x => x.BaseUnit)
				.Include(x => x.ProductUnits)
					.ThenInclude(x => x.Unit)
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
				return View(model);
			}

			var invoice = new PurchaseInvoice
			{
				InvoiceNumber = model.InvoiceNumber,
				SupplierId = supplier.Id,
				StoreId = store.Id,
				InvoiceDate = model.InvoiceDate,
				DueDate = model.DueDate,
				Notes = model.Notes,
				DiscountAmount = model.DiscountAmount,
				TaxRate = model.TaxRate,
				TaxAmount = model.TaxAmount,
				SalesTaxRate = model.SalesTaxRate,
				SalesTaxAmount = model.SalesTaxAmount,
				Status = PurchaseInvoiceStatus.Draft,
				CreatedAt = DateTime.UtcNow
			};

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
						$"الوحدة المختارة غير متاحة للمنتج: {product.Name}");

					await LoadCreateDataAsync(model);
					return View(model);
				}

				// ═══════════════════════════════════════════════════════════
				// ✅ تحديد ConversionFactor الفعلي
				// ═══════════════════════════════════════════════════════════

				var unit = productUnit.Unit;

				if (unit == null)
				{
					ModelState.AddModelError(
						nameof(model.Items),
						$"الوحدة غير موجودة للمنتج: {product.Name}");

					await LoadCreateDataAsync(model);
					return View(model);
				}

				decimal conversionFactor;

				if (unit.Kind == UnitKind.Standard)
				{
					// ✅ وحدة ثابتة: القيمة من Unit مباشرة
					if (!unit.PiecesPerUnit.HasValue ||
						unit.PiecesPerUnit.Value <= 0)
					{
						ModelState.AddModelError(
							nameof(model.Items),
							$"الوحدة الثابتة '{unit.Name}' غير مُعرَّفة بشكل صحيح (عدد القطع مفقود).");

						await LoadCreateDataAsync(model);
						return View(model);
					}

					conversionFactor = unit.PiecesPerUnit.Value;
				}
				else // Variable
				{
					// ✅ وحدة متغيرة: الأولوية:
					//    1) ما كتبه المستخدم (ConversionFactor)
					//    2) آخر تعبئة لنفس المورد + الصنف + الوحدة
					//    3) ProductUnit.ConversionFactor
					//    4) ItemSetSize (توافق قديم)

					if (itemModel.ConversionFactor.HasValue &&
						itemModel.ConversionFactor.Value > 0)
					{
						conversionFactor = itemModel.ConversionFactor.Value;
					}
					else
					{
						var lastPacking = await _context.SupplierProductPackings
							.AsNoTracking()
							.Where(x => x.SupplierId == supplier.Id
									 && x.ProductId == product.Id
									 && x.UnitId == itemModel.UnitId)
							.OrderByDescending(x => x.LastUsedAt)
							.Select(x => (decimal?)x.ConversionFactor)
							.FirstOrDefaultAsync();

						if (lastPacking.HasValue && lastPacking.Value > 0)
						{
							conversionFactor = lastPacking.Value;
						}
						else if (productUnit.ConversionFactor > 0)
						{
							conversionFactor = productUnit.ConversionFactor;
						}
						else if (itemModel.ItemSetSize.HasValue &&
								 itemModel.ItemSetSize.Value > 0)
						{
							conversionFactor = itemModel.ItemSetSize.Value;
						}
						else
						{
							ModelState.AddModelError(
								nameof(model.Items),
								$"حدّد عدد القطع داخل الوحدة '{unit.Name}' للمنتج: {product.Name}");

							await LoadCreateDataAsync(model);
							return View(model);
						}
					}
				}

				// ═══════════════════════════════════════════════════════════
				// الحسابات النهائية
				// ═══════════════════════════════════════════════════════════
				//
				//   QuantityInBaseUnit  = Quantity × ConversionFactor
				//   LineTotal           = QuantityInBaseUnit × UnitPrice
				//   UnitCostInBaseUnit  = UnitPrice (سعر القطعة)
				//
				// مثال: 100 دستة × 12 × 60
				//   QuantityInBaseUnit  = 1200 قطعة
				//   LineTotal           = 1200 × 60 = 72,000
				//   UnitCostInBaseUnit  = 60 (سعر القطعة)
				// ═══════════════════════════════════════════════════════════

				var quantityInBaseUnit =
					itemModel.Quantity * conversionFactor;

				var lineTotal =
					quantityInBaseUnit * itemModel.UnitPrice;

				var unitCostInBaseUnit =
					itemModel.UnitPrice;

				subTotal += lineTotal;

				var invoiceItem = new PurchaseInvoiceItem
				{
					ProductId = product.Id,
					UnitId = itemModel.UnitId,
					ConversionFactor = conversionFactor,
					Quantity = itemModel.Quantity,
					QuantityInBaseUnit = quantityInBaseUnit,
					UnitPrice = itemModel.UnitPrice,
					UnitCostInBaseUnit = unitCostInBaseUnit,
					LineTotal = lineTotal
				};

				invoice.Items.Add(invoiceItem);

				// ═══════════════════════════════════════════════════════════
				// ✅ حفظ آخر تعبئة (للوحدات المتغيرة فقط)
				// ═══════════════════════════════════════════════════════════

				if (unit.Kind == UnitKind.Variable)
				{
					var packing = await _context.SupplierProductPackings
						.FirstOrDefaultAsync(x => x.SupplierId == supplier.Id
											   && x.ProductId == product.Id
											   && x.UnitId == itemModel.UnitId);

					if (packing == null)
					{
						_context.SupplierProductPackings.Add(new SupplierProductPacking
						{
							SupplierId = supplier.Id,
							ProductId = product.Id,
							UnitId = itemModel.UnitId,
							ConversionFactor = conversionFactor,
							LastUsedAt = DateTime.UtcNow
						});
					}
					else
					{
						packing.ConversionFactor = conversionFactor;
						packing.LastUsedAt = DateTime.UtcNow;
					}
				}
			}

			invoice.SubTotal = subTotal;
			decimal taxableAmount = subTotal - invoice.DiscountAmount;

			if (invoice.SalesTaxRate > 0)
			{
				invoice.SalesTaxAmount = Math.Round(
					taxableAmount * invoice.SalesTaxRate / 100m, 2);
			}
			else
			{
				invoice.SalesTaxAmount = 0;
			}

			if (invoice.TaxRate > 0)
			{
				invoice.TaxAmount = Math.Round(
					taxableAmount * invoice.TaxRate / 100m, 2);
			}
			else
			{
				invoice.TaxAmount = 0;
			}

			invoice.TotalAmount =
				taxableAmount +
				invoice.SalesTaxAmount +
				invoice.TaxAmount;

			_context.PurchaseInvoices.Add(invoice);

			const int maxRetries = 5;

			for (int attempt = 1;
				attempt <= maxRetries;
				attempt++)
			{
				try
				{
					await _context.SaveChangesAsync();

					if (Request.Form["saveAndConfirm"] == "true")
					{
						var uidRaw =
							User.FindFirstValue(
								System.Security.Claims
									.ClaimTypes.NameIdentifier);

						var canConfirm =
							int.TryParse(uidRaw, out var userId)
								? await _permissionService
									.HasPermissionAsync(
										userId,
										"purchase.confirm")
								: false;

						if (canConfirm)
						{
							return await Post(invoice.Id);
						}

						TempData["Error"] =
							"ليس لديك صلاحية ترحيل فاتورة الشراء.";

						return RedirectToAction(nameof(Index));
					}

					TempData["Success"] =
						"تم إنشاء فاتورة الشراء بنجاح (مسودة).";

					return RedirectToAction(nameof(Index));
				}
				catch (DbUpdateException ex)
					when (IsInvoiceNumberConflict(ex) &&
						attempt < maxRetries)
				{
					invoice.InvoiceNumber =
						await _documentNumberService.GenerateNumberAsync("PUR", "PurchaseInvoices", DateTime.Today.ToString("yyyyMMdd"));

					ModelState.Remove(
						nameof(model.InvoiceNumber));

					_context.Entry(invoice).State =
						EntityState.Detached;

					foreach (var item in invoice.Items)
					{
						_context.Entry(item).State =
							EntityState.Detached;
					}

					_context.PurchaseInvoices.Add(invoice);
				}
			}

			ModelState.AddModelError(
				nameof(model.InvoiceNumber),
				"تعذر حفظ الفاتورة بسبب تصادم في رقم الفاتورة، حاول مرة أخرى.");

			await LoadCreateDataAsync(model);
			return View(model);
		}

		// =========================================
		// ترحيل الفاتورة (Draft → Posted)
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("purchase.confirm")]
		public async Task<IActionResult> Post(int id)
		{
			var invoice = await _context.PurchaseInvoices
				.Include(x => x.Items)
				.Include(x => x.Supplier)
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

			if (invoice.Supplier == null)
			{
				TempData["Error"] = "المورد غير موجود.";
				return RedirectToAction(nameof(Index));
			}

			var supplier = invoice.Supplier;

			if (invoice.Status != PurchaseInvoiceStatus.Draft)
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

			var strategy =
				_context.Database.CreateExecutionStrategy();

			string? transactionError = null;

			await strategy.ExecuteAsync(
				async () =>
				{
					await using var transaction =
						await _context.Database.BeginTransactionAsync();

					// =========================================
					// ✅ (Idempotency) قفل صف الفاتورة
					// =========================================

					PurchaseInvoiceStatus? lockedStatus;

					if (_context.Database.IsRelational())
					{
						lockedStatus = await _context.PurchaseInvoices
							.FromSqlInterpolated($@"
                                SELECT * FROM [PurchaseInvoices]
                                WITH (UPDLOCK, ROWLOCK)
                                WHERE [Id] = {invoice.Id}")
							.AsNoTracking()
							.Select(x => (PurchaseInvoiceStatus?)x.Status)
							.FirstOrDefaultAsync();
					}
					else
					{
						lockedStatus = await _context.PurchaseInvoices
							.AsNoTracking()
							.Where(x => x.Id == invoice.Id)
							.Select(x => (PurchaseInvoiceStatus?)x.Status)
							.FirstOrDefaultAsync();
					}

					if (lockedStatus != PurchaseInvoiceStatus.Draft)
					{
						transactionError =
							"الفاتورة تم ترحيلها بالفعل من جلسة أخرى. " +
							"أعد فتح الصفحة لرؤية الحالة المحدثة.";

						return;
					}

					try
					{
						foreach (var item in invoice.Items)
						{
							// ═══════════════════════════════════════════════════════
							// ✅ StockLot يحفظ التكلفة الأساسية للقطعة
							//    UnitCost = UnitCostInBaseUnit (= UnitPrice)
							//    QuantityReceived = QuantityInBaseUnit
							// ═══════════════════════════════════════════════════════

							var lot = new StockLot
							{
								StoreId = invoice.StoreId,
								ProductId = item.ProductId,
								SupplierId = invoice.SupplierId,
								PurchaseInvoiceItemId = item.Id,
								QuantityReceived = item.QuantityInBaseUnit,
								QuantityRemaining = item.QuantityInBaseUnit,
								UnitCost = item.UnitCostInBaseUnit,
								PurchaseDate = invoice.InvoiceDate,
								CreatedAt = DateTime.UtcNow,
								IsActive = true
							};

							_context.StockLots.Add(lot);
						}

						invoice.Status = PurchaseInvoiceStatus.Posted;
						invoice.PostedAt = DateTime.UtcNow;

						await _context.SaveChangesAsync();

						var postingResult =
							await _postingService.PostPurchaseInvoiceAsync(
								invoice,
								CurrentUserId());

						if (!postingResult.Success)
						{
							await transaction.RollbackAsync();

							transactionError =
								"تعذر إنشاء القيد المحاسبي للفاتورة: " +
								postingResult.Error;

							await NotifyPostingFailedAsync(invoice, postingResult.Error);

							return;
						}

						await transaction.CommitAsync();
					}
					catch (Exception ex)
					{
						await transaction.RollbackAsync();

						transactionError =
							"تعذر ترحيل الفاتورة: " + ex.Message;

						await NotifyPostingFailedAsync(invoice, ex.Message);
					}
				});

			if (transactionError != null)
			{
				TempData["Error"] = transactionError;
				return RedirectToAction(nameof(Index));
			}

			await NotifyLargePurchaseIfNeededAsync(invoice, supplier.Name);

			if (!string.IsNullOrWhiteSpace(supplier.Phone))
			{
				try
				{
					var pdfPath =
						await _pdfInvoiceService
							.GeneratePurchaseInvoicePdfAsync(invoice.Id);

					if (!string.IsNullOrEmpty(pdfPath))
					{
						var fileName =
							$"فاتورة-شراء-{invoice.InvoiceNumber}.pdf";

						await _whatsAppService.SendDocumentAsync(
							supplier.Phone,
							supplier.Name,
							pdfPath,
							fileName,
							$"فاتورة شراء {invoice.InvoiceNumber} — الإجمالي: {invoice.TotalAmount:N2} ج.م");

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
				catch
				{
					// لا نوقف العملية
				}
			}

			TempData["Success"] =
				"تم ترحيل الفاتورة وإضافة الكميات إلى المخزون وترحيل القيد المحاسبي.";

			return RedirectToAction(nameof(Index));
		}

		// =========================================
		// إلغاء الفاتورة
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("purchase.cancel")]
		public async Task<IActionResult> Cancel(int id)
		{
			var invoice = await _context.PurchaseInvoices
				.Include(x => x.Items)
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

			if (invoice.Status == PurchaseInvoiceStatus.Cancelled)
			{
				TempData["Error"] =
					"الفاتورة مُلغاة بالفعل.";

				return RedirectToAction(nameof(Index));
			}

			if (invoice.Status == PurchaseInvoiceStatus.Draft)
			{
				invoice.Status = PurchaseInvoiceStatus.Cancelled;

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

					PurchaseInvoiceStatus? lockedStatus;

					if (_context.Database.IsRelational())
					{
						lockedStatus = await _context.PurchaseInvoices
							.FromSqlInterpolated($@"
                                SELECT * FROM [PurchaseInvoices]
                                WITH (UPDLOCK, ROWLOCK)
                                WHERE [Id] = {invoice.Id}")
							.AsNoTracking()
							.Select(x => (PurchaseInvoiceStatus?)x.Status)
							.FirstOrDefaultAsync();
					}
					else
					{
						lockedStatus = await _context.PurchaseInvoices
							.AsNoTracking()
							.Where(x => x.Id == invoice.Id)
							.Select(x => (PurchaseInvoiceStatus?)x.Status)
							.FirstOrDefaultAsync();
					}

					if (lockedStatus != PurchaseInvoiceStatus.Posted)
					{
						transactionError =
							"الفاتورة تم إلغاؤها بالفعل من جلسة أخرى. " +
							"أعد فتح الصفحة لرؤية الحالة المحدثة.";

						return;
					}

					try
					{
						var itemIds =
							invoice.Items
								.Select(x => x.Id)
								.ToList();

						var lots =
							await _context.StockLots
								.Where(x =>
									x.PurchaseInvoiceItemId.HasValue &&
									itemIds.Contains(
										x.PurchaseInvoiceItemId!.Value))
								.ToListAsync();

						if (lots.Count > 0)
						{
							var lotIds = lots.Select(x => x.Id).ToList();

							var issued =
								await _context.SalesInvoiceItemLots
									.CountAsync(x =>
										lotIds.Contains(x.StockLotId));

							var returned =
								await _context.SalesReturnItemLots
									.CountAsync(x =>
										lotIds.Contains(x.StockLotId));

							if (issued > 0 || returned > 0)
							{
								transactionError =
									"لا يمكن إلغاء هذه الفاتورة — بعض كمياتها صُرفت بمبيعات.";

								return;
							}
						}

						foreach (var lot in lots)
						{
							_context.StockLots.Remove(lot);
						}

						invoice.Status = PurchaseInvoiceStatus.Cancelled;

						await _context.SaveChangesAsync();

						var reversal =
							await _postingService
								.PostPurchaseInvoiceCancelAsync(
									invoice,
									CurrentUserId());

						if (!reversal.Success)
						{
							await transaction.RollbackAsync();

							transactionError =
								"تعذر إلغاء الفاتورة — لم يُرجع الحساب المحاسبي: " +
								reversal.Error;

							await NotifyPostingFailedAsync(
								invoice,
								"فشل القيد العكسي: " + reversal.Error);

							return;
						}

						await transaction.CommitAsync();

						TempData["Success"] =
							"تم إلغاء الفاتورة المُرحّلة وترحيل القيد العكسي " +
							reversal.EntryNumber +
							" وحذف الدفعات من المخزون.";
					}
					catch (Exception ex)
					{
						await transaction.RollbackAsync();

						transactionError =
							"تعذر إلغاء الفاتورة: " + ex.Message;

						await NotifyPostingFailedAsync(invoice, ex.Message);
					}
				});

			if (transactionError != null)
			{
				TempData["Error"] = transactionError;
			}

			return RedirectToAction(nameof(Index));
		}

		// =========================================
		// تحميل بيانات شاشة الإضافة
		// =========================================

		private async Task LoadCreateDataAsync(
			PurchaseInvoiceCreateViewModel model)
		{
			var scope = await _employeeScopeService.GetScopeAsync();

			model.CompanyLogoPath =
				await _context.Companies
					.AsNoTracking()
					.Where(x => x.IsActive)
					.OrderBy(x => x.Id)
					.Select(x => x.LogoPath)
					.FirstOrDefaultAsync();

			model.AvailableSuppliers =
				await _context.Suppliers
					.AsNoTracking()
					.Where(x => x.IsActive)
					.OrderBy(x => x.Name)
					.Select(x => new SupplierOptionViewModel
					{
						Id = x.Id,
						Name = x.Name,
						Phone = x.Phone
					})
					.ToListAsync();

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
				.Include(x => x.Branch)
				.Where(x =>
					x.IsActive &&
					x.Branch != null &&
					x.Branch.IsActive);

			storesQuery = _employeeScopeService
				.ApplyStoreFilter(storesQuery, scope);

			model.AvailableStores =
				await storesQuery
					.OrderBy(x => x.Branch!.Name)
					.ThenBy(x => x.Name)
					.Select(x => new StoreOptionViewModel
					{
						Id = x.Id,
						BranchId = x.BranchId,
						Name = x.Name,
						BranchName = x.Branch!.Name
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

			model.AvailableProducts =
				products.Select(x => new ProductOptionViewModel
				{
					Id = x.Id,
					Name = x.Name,
					Barcode = x.Barcode,
					BaseUnitId = x.BaseUnitId,
					BaseUnitName = x.BaseUnit?.Name ?? "-",
					Units = x.ProductUnits
						.Where(u => u.IsActive && u.Unit != null)
						.Select(u => new ProductUnitOptionViewModel
						{
							UnitId = u.UnitId,
							UnitName = u.Unit!.Name,
							ConversionFactor = u.ConversionFactor,

							// ✅ جديد: نوع الوحدة + عدد القطع
							Kind = (int)u.Unit.Kind,
							PiecesPerUnit = u.Unit.PiecesPerUnit
						})
						.ToList()
				})
				.ToList();
		}

		// =========================================
		// إضافة مورد سريع (AJAX)
		// =========================================

		[HttpPost]
		[RequirePermission("purchase.create")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> CreateQuickSupplier(
			string name,
			string? phone)
		{
			name = (name ?? "").Trim();
			phone = (phone ?? "").Trim();

			if (string.IsNullOrEmpty(name))
			{
				return Json(new { ok = false, message = "اسم المورد مطلوب." });
			}

			if (string.IsNullOrEmpty(phone))
			{
				return Json(new { ok = false, message = "رقم الهاتف مطلوب." });
			}

			var phoneExists = await _context.Suppliers
				.AnyAsync(x => x.Phone == phone);

			if (phoneExists)
			{
				return Json(new { ok = false, message = "رقم الهاتف مستخدم بالفعل لمورد آخر." });
			}

			var supplier = new Supplier
			{
				Name = name,
				Phone = phone,
				CreatedAt = DateTime.UtcNow,
				IsActive = true
			};

			_context.Suppliers.Add(supplier);
			await _context.SaveChangesAsync();

			var userId = _notifications.CurrentUserId(User);

			if (userId > 0)
			{
				await _notifications.CreateAsync(
					userId,
					"تم إضافة مورد جديد",
					$"المورد: {supplier.Name} — {supplier.Phone}",
					NotificationLevel.Normal,
					Url.Action("Edit", "Supplier", new { id = supplier.Id }),
					"bi-person-plus");
			}

			return Json(new
			{
				ok = true,
				id = supplier.Id,
				name = supplier.Name,
				phone = supplier.Phone
			});
		}

		// =========================================
		// إضافة مخزن سريع (AJAX)
		// =========================================

		[HttpPost]
		[RequirePermission("purchase.create")]
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
				branchId = store.BranchId,
				branchName = branch.Name
			});
		}

		// =========================================
		// إضافة وحدة جديدة للصنف
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("product.edit")]
		public async Task<IActionResult> AddUnitForProduct(
			int productId,
			string unitName,
			string unitShort,
			decimal conversionFactor,
			decimal salePrice,
			int unitKind = 1)   // ✅ 0 = Standard, 1 = Variable
		{
			var product = await _context.Products
				.FirstOrDefaultAsync(x => x.Id == productId);

			if (product == null)
			{
				return Json(new { ok = false, message = "الصنف غير موجود." });
			}

			unitName = unitName?.Trim() ?? string.Empty;
			unitShort = unitShort?.Trim() ?? string.Empty;

			if (string.IsNullOrWhiteSpace(unitName))
			{
				return Json(new { ok = false, message = "اسم الوحدة مطلوب." });
			}

			var kind = (UnitKind)unitKind;

			// ✅ للوحدات الثابتة: لازم عدد قطع > 0
			if (kind == UnitKind.Standard && conversionFactor <= 0)
			{
				return Json(new
				{
					ok = false,
					message = "الوحدة الثابتة يجب أن يكون لها عدد قطع أكبر من صفر."
				});
			}

			// ✅ للوحدات المتغيرة: مسموح 0 (يُملأ لاحقاً من الفاتورة)
			if (kind == UnitKind.Variable &&
				conversionFactor < 0)
			{
				return Json(new
				{
					ok = false,
					message = "معامل التحويل لا يمكن أن يكون سالباً."
				});
			}

			var unitNameNoSpaces = unitName.Replace(" ", string.Empty);

			var unit = await _context.Units
				.Where(x => x.Name != null &&
					x.Name.Replace(" ", string.Empty) == unitNameNoSpaces)
				.OrderBy(x => x.IsActive ? 0 : 1)
				.ThenBy(x => x.Id)
				.FirstOrDefaultAsync();

			if (unit == null)
			{
				unit = new Unit
				{
					Name = unitName,
					ShortName = string.IsNullOrWhiteSpace(unitShort)
						? unitName
						: unitShort,
					Kind = kind,
					PiecesPerUnit = kind == UnitKind.Standard
						? conversionFactor
						: null,
					IsActive = true,
					CreatedAt = DateTime.UtcNow
				};

				_context.Units.Add(unit);
				await _context.SaveChangesAsync();
			}

			var exists = await _context.ProductUnits
				.AnyAsync(x =>
					x.ProductId == product.Id &&
					x.UnitId == unit.Id);

			if (!exists)
			{
				_context.ProductUnits.Add(new ProductUnit
				{
					ProductId = product.Id,
					UnitId = unit.Id,
					ConversionFactor = conversionFactor,
					SalePrice = salePrice,
					IsActive = true,
					CreatedAt = DateTime.UtcNow
				});

				await _context.SaveChangesAsync();
			}
			else
			{
				var link = await _context.ProductUnits
					.FirstOrDefaultAsync(x =>
						x.ProductId == product.Id &&
						x.UnitId == unit.Id);

				if (link != null)
				{
					link.ConversionFactor = conversionFactor;
					link.SalePrice = salePrice;
					link.IsActive = true;
					await _context.SaveChangesAsync();
				}
			}

			return Json(new
			{
				ok = true,
				unitId = unit.Id,
				unitName = unit.Name,
				conversionFactor = conversionFactor,
				kind = (int)unit.Kind,
				piecesPerUnit = unit.PiecesPerUnit
			});
		}

		// =========================================
		// بيانات المورد: الرصيد + حالة البوابة
		// =========================================

		[HttpGet]
		[RequirePermission("purchase.view")]
		public async Task<IActionResult> GetSupplierInfo(int id)
		{
			if (id <= 0)
			{
				return Json(new { ok = false });
			}

			var supplier = await _context.Suppliers
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

			if (supplier == null)
			{
				return Json(new { ok = false });
			}

			var supplierAccountId = await _context.ChartAccounts
				.AsNoTracking()
				.Where(x => x.Code == "2102" && x.IsActive)
				.Select(x => (int?)x.Id)
				.FirstOrDefaultAsync();

			decimal balance = 0;

			if (supplierAccountId.HasValue)
			{
				var sums = await _context.JournalEntryLines
					.AsNoTracking()
					.Where(x =>
						x.SupplierId == id &&
						x.ChartAccountId == supplierAccountId.Value)
					.GroupBy(x => x.SupplierId)
					.Select(g => new
					{
						TotalDebit = g.Sum(x => x.Debit),
						TotalCredit = g.Sum(x => x.Credit)
					})
					.FirstOrDefaultAsync();

				if (sums != null)
				{
					balance = sums.TotalCredit - sums.TotalDebit;
				}
			}

			return Json(new
			{
				ok = true,
				id = supplier.Id,
				name = supplier.Name,
				phone = supplier.Phone,
				hasPortalAccount = supplier.HasPortalAccount,
				portalApproved = supplier.PortalApproved,
				portalRequested = supplier.PortalRequested,
				balance = balance
			});
		}

		// =========================================
		// ✅ جديد: آخر تعبئة استخدمها المورد لهذا الصنف/الوحدة
		// =========================================

		[HttpGet]
		[RequirePermission("purchase.create")]
		public async Task<IActionResult> GetLastPacking(
			int supplierId,
			int productId,
			int unitId)
		{
			if (supplierId <= 0 || productId <= 0 || unitId <= 0)
			{
				return Json(new { ok = false });
			}

			// 1) آخر تعبئة من جدول SupplierProductPacking
			var packing = await _context.SupplierProductPackings
				.AsNoTracking()
				.Where(x => x.SupplierId == supplierId
						 && x.ProductId == productId
						 && x.UnitId == unitId)
				.OrderByDescending(x => x.LastUsedAt)
				.Select(x => (decimal?)x.ConversionFactor)
				.FirstOrDefaultAsync();

			if (packing.HasValue && packing.Value > 0)
			{
				return Json(new
				{
					ok = true,
					conversionFactor = packing.Value,
					source = "packing"
				});
			}

			// 2) احتياطي: ProductUnit.ConversionFactor
			var productUnit = await _context.ProductUnits
				.AsNoTracking()
				.Where(x => x.ProductId == productId
						 && x.UnitId == unitId
						 && x.IsActive)
				.Select(x => (decimal?)x.ConversionFactor)
				.FirstOrDefaultAsync();

			if (productUnit.HasValue && productUnit.Value > 0)
			{
				return Json(new
				{
					ok = true,
					conversionFactor = productUnit.Value,
					source = "productUnit"
				});
			}

			return Json(new { ok = false });
		}

		// =========================================
		// Helpers: الإشعارات
		// =========================================

		/// <summary>
		/// إشعار عند ترحيل فاتورة شراء كبيرة (>= 50,000) — Medium
		/// </summary>
		private async Task NotifyLargePurchaseIfNeededAsync(
			PurchaseInvoice invoice,
			string? supplierName)
		{
			if (invoice.TotalAmount < LargePurchaseThreshold)
			{
				return;
			}

			var userId = _notifications.CurrentUserId(User);

			if (userId <= 0)
			{
				return;
			}

			await _notifications.CreateAsync(
				userId,
				"فاتورة شراء كبيرة",
				$"فاتورة {invoice.InvoiceNumber} — {invoice.TotalAmount:N2} ج.م — المورد: {supplierName ?? "-"}",
				NotificationLevel.Medium,
				Url.Action(nameof(Details), new { id = invoice.Id }),
				"bi-receipt");
		}

		/// <summary>
		/// إشعار حرج عند فشل ترحيل فاتورة شراء أو قيدها المحاسبي.
		/// </summary>
		private async Task NotifyPostingFailedAsync(
			PurchaseInvoice invoice,
			string? error)
		{
			var userId = _notifications.CurrentUserId(User);

			if (userId <= 0)
			{
				return;
			}

			await _notifications.CreateAsync(
				userId,
				"⚠️ فشل ترحيل فاتورة شراء",
				$"فاتورة {invoice.InvoiceNumber} — {error}",
				NotificationLevel.Critical,
				Url.Action(nameof(Details), new { id = invoice.Id }),
				"bi-x-octagon");
		}

		// =========================================
		// كشف تصادم رقم الفاتورة الوحيد
		// =========================================

		private static bool IsInvoiceNumberConflict(
			DbUpdateException ex)
		{
			if (ex.InnerException is
				Microsoft.Data.SqlClient.SqlException sqlEx)
			{
				return sqlEx.Number == 2601 ||
					sqlEx.Number == 2627;
			}

			return false;
		}

		// =========================================
		// اسم نوع مصدر القيد
		// =========================================

		private static string GetSourceTypeName(JournalSourceType type) =>
			type switch
			{
				JournalSourceType.SalesInvoice => "فاتورة بيع",
				JournalSourceType.SalesCancel => "إلغاء فاتورة بيع",
				JournalSourceType.PurchaseInvoice => "فاتورة شراء",
				JournalSourceType.PurchaseCancel => "إلغاء فاتورة شراء",
				JournalSourceType.SalesReturn => "مرتجع بيع",
				JournalSourceType.PurchaseReturn => "مرتجع شراء",
				_ => type.ToString()
			};

		// =========================================
		// المستخدم الحالي
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
	}
}