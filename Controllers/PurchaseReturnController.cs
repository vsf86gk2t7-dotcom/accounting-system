using System.Security.Claims;
using AccountingSystem.Data;
using AccountingSystem.Filters;
using AccountingSystem.Models;
using AccountingSystem.Models.ViewModels.Returns;
using AccountingSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Controllers
{
	public class PurchaseReturnController : Controller
	{
		private readonly ApplicationDbContext _context;
		private readonly IPostingService _postingService;
		private readonly IDocumentNumberService _documentNumberService;

		public PurchaseReturnController(
			ApplicationDbContext context,
			IPostingService postingService,
			IDocumentNumberService documentNumberService)
		{
			_context = context;
			_postingService = postingService;
			_documentNumberService = documentNumberService;
		}

		// =========================================
		// سجل مرتجعات الشراء
		// =========================================

		[HttpGet]
		[RequirePermission("purchase.return.view")]
		public async Task<IActionResult> Index()
		{
			var model =
				new ReturnListViewModel
				{
					Type = "purchase"
				};

			var invoices =
				await _context.PurchaseReturnInvoices
					.AsNoTracking()
					.Include(x => x.Supplier)
					.Include(x => x.Store)
					.Include(x => x.Items)
					.OrderByDescending(x => x.ReturnDate)
					.ThenByDescending(x => x.Id)
					.Take(AppConstants.DefaultPageSize)
					.ToListAsync();

			foreach (var invoice in invoices)
			{
				model.Rows.Add(
					new ReturnRowViewModel
					{
						Id = invoice.Id,
						InvoiceNumber = invoice.InvoiceNumber,
						PartyName =
							invoice.Supplier?.Name ?? "-",
						StoreName =
							invoice.Store?.Name ?? "-",
						ReturnDate = invoice.ReturnDate,
						TotalAmount = invoice.TotalAmount,
						ItemCount = invoice.Items.Count,
						Reason = invoice.Reason
					});
			}

			return View(model);
		}

		// =========================================
		// إنشاء مرتجع شراء - GET
		// =========================================

		[HttpGet]
		[RequirePermission("purchase.return.create")]
		public async Task<IActionResult> Create()
		{
			var model =
				new PurchaseReturnViewModel();

			model.Items.Add(
				new PurchaseReturnItemViewModel());

			await LoadDataAsync(model);

			return View(model);
		}

		// =========================================
		// إنشاء مرتجع شراء - POST
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("purchase.return.create")]
		public async Task<IActionResult> Create(
			PurchaseReturnViewModel model)
		{
			model.Reason =
				string.IsNullOrWhiteSpace(model.Reason)
					? null
					: model.Reason.Trim();

			model.Items =
				model.Items?
						.Where(x => x.ProductId > 0)
						.ToList()
					?? new List<PurchaseReturnItemViewModel>();

			if (!ModelState.IsValid ||
				model.Items.Count == 0)
			{
				ModelState.AddModelError(
					string.Empty,
					"يجب إدخال بند واحد على الأقل.");

				await LoadDataAsync(model);

				return View(model);
			}

			var storeBranchId =
				await _context.Stores
					.Where(x =>
						x.Id == model.StoreId &&
						x.IsActive)
					.Select(x => (int?)x.BranchId)
					.FirstOrDefaultAsync();

			var storeExists =
				await _context.Stores
					.AnyAsync(x =>
						x.Id == model.StoreId &&
						x.IsActive);

			if (!storeExists)
			{
				ModelState.AddModelError(
					nameof(model.StoreId),
					"المخزن غير موجود أو غير نشط.");

				await LoadDataAsync(model);

				return View(model);
			}

			var supplierExists =
				await _context.Suppliers
					.AnyAsync(x =>
						x.Id == model.SupplierId &&
						x.IsActive);

			if (!supplierExists)
			{
				ModelState.AddModelError(
					nameof(model.SupplierId),
					"المورد غير موجود أو غير نشط.");

				await LoadDataAsync(model);

				return View(model);
			}

			var productIds =
				model.Items
					.Select(x => x.ProductId)
					.Distinct()
					.ToList();

			var validProducts =
				await _context.Products
					.Where(x =>
						x.IsActive &&
						productIds.Contains(x.Id))
					.Select(x => x.Id)
					.ToListAsync();

			if (validProducts.Count != productIds.Count)
			{
				ModelState.AddModelError(
					string.Empty,
					"يوجد منتج غير موجود أو غير نشط.");

				await LoadDataAsync(model);

				return View(model);
			}

			var invoice =
				new PurchaseReturnInvoice
				{
				InvoiceNumber =
					await _documentNumberService.GenerateNumberAsync("PRT", "PurchaseReturnInvoices", DateTime.Today.ToString("yyyyMMdd")),
					SupplierId = model.SupplierId,
					StoreId = model.StoreId,
					BranchId = storeBranchId,
					ReturnDate = model.ReturnDate,
					Reason = model.Reason,
					IsPosted = true,
					CreatedByUserId = CurrentUserId(),
					CreatedAt = DateTime.UtcNow
				};

			_context.PurchaseReturnInvoices.Add(invoice);
                        // ✅ (N+1 fix) query واحد لكل المنتجات قبل الـ loop
                        var returnProductIds =
                                model.Items
                                        .Select(x => x.ProductId)
                                        .Distinct()
                                        .ToList();

                        var lotsByProduct =
                                await _context.StockLots
                                        .Where(x =>
                                                x.StoreId == model.StoreId &&
                                                returnProductIds.Contains(x.ProductId) &&
                                                x.IsActive &&
                                                x.QuantityRemaining > 0)
                                        .OrderBy(x => x.PurchaseDate)
                                        .ThenBy(x => x.Id)
                                        .ToListAsync();

                        var lotsByProductDict =
                                lotsByProduct
                                        .GroupBy(x => x.ProductId)
                                        .ToDictionary(g => g.Key, g => g.ToList());
			foreach (var item in model.Items)
			{
				if (item.Quantity <= 0)
				{
					ModelState.AddModelError(
						string.Empty,
						"الكمية يجب أن تكون أكبر من صفر.");

					await LoadDataAsync(model);

					return View(model);
				}

				// =================================
				// استهلاك المخزون بالـ FIFO
				// =================================

			                                lotsByProductDict.TryGetValue(item.ProductId, out var lots);

                                lots ??= new List<StockLot>();

				var available =
					lots.Sum(x => x.QuantityRemaining);

				if (item.Quantity > available)
				{
					ModelState.AddModelError(
						string.Empty,
						"الكمية المطلوب إرجاعها أكبر من المتاح بالمخزن.");

					await LoadDataAsync(model);

					return View(model);
				}

				var toReturn = item.Quantity;
				var totalCost = 0m;

				foreach (var lot in lots)
				{
					if (toReturn <= 0)
					{
						break;
					}

					var consume =
						Math.Min(toReturn, lot.QuantityRemaining);

					lot.QuantityRemaining -= consume;
					toReturn -= consume;
					totalCost += consume * lot.UnitCost;
				}

				// =================================
				// تجميع التكلفة الفعلية في المحاور
				// =================================

				var unitCost =
					totalCost / item.Quantity;

				invoice.Items.Add(
					new PurchaseReturnInvoiceItem
					{
						ProductId = item.ProductId,
						Quantity = item.Quantity,
						UnitCost = unitCost,
						Total = totalCost
					});

				invoice.TotalAmount += totalCost;
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

					try
					{
						await _context.SaveChangesAsync();

						var postingResult =
							await _postingService.PostPurchaseReturnAsync(
								invoice,
								CurrentUserId());

						if (!postingResult.Success)
						{
							await transaction.RollbackAsync();
							transactionError =
								"فشل ترحيل قيد مرتجع الشراء: " +
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
							"فشل في تسجيل مرتجع الشراء: " + ex.Message;
					}
				});

			if (transactionError != null)
			{
				TempData["Error"] = transactionError;
				return RedirectToAction(nameof(Index));
			}

			TempData["Success"] =
				$"تم تسجيل مرتجع الشراء {invoice.InvoiceNumber} وسحب الأصناف من المخزون.";

			return RedirectToAction(nameof(Index));
		}

		// =========================================
		// أدوات مساعدة
		// =========================================

		private async Task LoadDataAsync(
			PurchaseReturnViewModel model)
		{
			model.Suppliers =
				await _context.Suppliers
					.AsNoTracking()
					.Where(x => x.IsActive)
					.OrderBy(x => x.Name)
					.Select(x =>
						new KeyValuePair<int, string>(
							x.Id,
							x.Name))
					.ToListAsync();

			model.Stores =
				await _context.Stores
					.AsNoTracking()
					.Where(x => x.IsActive)
					.OrderBy(x => x.Name)
					.Select(x =>
						new KeyValuePair<int, string>(
							x.Id,
							x.Name))
					.ToListAsync();

			model.Products =
				await _context.Products
					.AsNoTracking()
					.Where(x => x.IsActive)
					.OrderBy(x => x.Name)
					.Select(x =>
						new ReturnProductOptionViewModel
						{
							Id = x.Id,
							Name = x.Name,
							Code = x.Code
						})
					.ToListAsync();
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
	}
}
