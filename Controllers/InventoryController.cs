using AccountingSystem.Data;
using AccountingSystem.Filters;
using AccountingSystem.Models;
using AccountingSystem.Models.ViewModels.Inventory;
using AccountingSystem.Services.EmployeeScope;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AccountingSystem.Controllers
{
	public class InventoryController : Controller
	{
		private readonly ApplicationDbContext _context;
		private readonly IEmployeeScopeService _employeeScopeService;

		public InventoryController(
			ApplicationDbContext context,
			IEmployeeScopeService employeeScopeService)
		{
			_context = context;
			_employeeScopeService = employeeScopeService;
		}

		// =========================================
		// عرض الأرصدة
		// =========================================

		[HttpGet]
		[RequirePermission("inventory.view")]
		public async Task<IActionResult> Index(
			int? storeId,
			string? search)
		{
			var scope = await _employeeScopeService.GetScopeAsync();

			var model =
				new InventoryIndexViewModel
				{
					StoreId = storeId,
					Search = search?.Trim(),
					Stores = await LoadStoresAsync()
				};

			var lotsQuery =
				_context.StockLots
					.AsNoTracking()
					.Include(x => x.Product)
					.Include(x => x.Store)
					.Where(x => x.IsActive);

			if (scope.IsRestricted)
			{
				var allowedStores = scope.StoreIds.ToList();

				lotsQuery = allowedStores.Count == 0
					? lotsQuery.Where(_ => false)
					: lotsQuery.Where(x => allowedStores.Contains(x.StoreId));
			}

			if (storeId.HasValue && storeId.Value > 0)
			{
				lotsQuery = lotsQuery.Where(x => x.StoreId == storeId.Value);
			}

			if (!string.IsNullOrWhiteSpace(model.Search))
			{
				var term = model.Search;

				lotsQuery = lotsQuery.Where(x =>
					(x.Product != null &&
					 (x.Product.Name.Contains(term) ||
					  (x.Product.Code != null && x.Product.Code.Contains(term)) ||
					  (x.Product.Barcode != null && x.Product.Barcode.Contains(term)))) ||
					(x.Store != null && x.Store.Name.Contains(term)));
			}

			var lots = await lotsQuery.ToListAsync();

			var groups = lots.GroupBy(x => new { x.ProductId, x.StoreId });

			foreach (var group in groups)
			{
				var first = group.First();

				if (first.Product == null || first.Store == null)
				{
					continue;
				}

				var quantity = group.Sum(x => x.QuantityRemaining);
				var value = group.Sum(x => x.QuantityRemaining * x.UnitCost);

				model.Rows.Add(
					new InventoryBalanceRowViewModel
					{
						ProductId = first.ProductId,
						ProductName = first.Product.Name,
						ProductCode = first.Product.Code,
						Barcode = first.Product.Barcode,
						StoreId = first.StoreId,
						StoreName = first.Store.Name,
						Quantity = quantity,
						Value = value,
						AverageCost = quantity > 0 ? value / quantity : 0,
						MinQuantity = first.Product.MinQuantity
					});
			}

			model.Rows = model.Rows
				.OrderBy(x => x.ProductName)
				.ThenBy(x => x.StoreName)
				.ToList();

			return View(model);
		}

		// =========================================
		// دفعات المخزون
		// =========================================

		[HttpGet]
		[RequirePermission("inventory.view")]
		public async Task<IActionResult> Lots(
			int? storeId,
			int? productId,
			string? search,
			bool? onlyAvailable)
		{
			var scope = await _employeeScopeService.GetScopeAsync();

			var model =
				new InventoryLotsViewModel
				{
					StoreId = storeId,
					ProductId = productId,
					Search = search?.Trim(),
					OnlyAvailable = onlyAvailable ?? true,
					Stores = await LoadStoresAsync(),
					Products = await _context.Products
						.AsNoTracking()
						.Where(x => x.IsActive)
						.OrderBy(x => x.Name)
						.Select(x => new KeyValuePair<int, string>(x.Id, x.Name))
						.ToListAsync()
				};

			var query =
				_context.StockLots
					.AsNoTracking()
					.Include(x => x.Product)
					.Include(x => x.Store)
					.Include(x => x.Supplier)
					.AsQueryable();

			if (scope.IsRestricted)
			{
				var allowedStores = scope.StoreIds.ToList();

				query = allowedStores.Count == 0
					? query.Where(_ => false)
					: query.Where(x => allowedStores.Contains(x.StoreId));
			}

			if (model.StoreId.HasValue && model.StoreId.Value > 0)
			{
				query = query.Where(x => x.StoreId == model.StoreId.Value);
			}

			if (model.ProductId.HasValue && model.ProductId.Value > 0)
			{
				query = query.Where(x => x.ProductId == model.ProductId.Value);
			}

			if (model.OnlyAvailable)
			{
				query = query.Where(x => x.QuantityRemaining > 0);
			}

			if (!string.IsNullOrWhiteSpace(model.Search))
			{
				var term = model.Search;

				query = query.Where(x =>
					(x.Product != null &&
					 (x.Product.Name.Contains(term) ||
					  (x.Product.Code != null && x.Product.Code.Contains(term)) ||
					  (x.Product.Barcode != null && x.Product.Barcode.Contains(term)))) ||
					(x.Supplier != null && x.Supplier.Name.Contains(term)));
			}

			var lots = await query
				.OrderBy(x => x.PurchaseDate)
				.ThenBy(x => x.Id)
				.ToListAsync();

			foreach (var lot in lots)
			{
				model.Rows.Add(
					new InventoryLotRowViewModel
					{
						Id = lot.Id,
						ProductName = lot.Product?.Name ?? "-",
						ProductCode = lot.Product?.Code,
						StoreName = lot.Store?.Name ?? "-",
						SupplierName = lot.Supplier?.Name ?? "دفعة نظامية",
						PurchaseDate = lot.PurchaseDate,
						QuantityReceived = lot.QuantityReceived,
						QuantityRemaining = lot.QuantityRemaining,
						UnitCost = lot.UnitCost,
						RemainingValue = lot.QuantityRemaining * lot.UnitCost
					});

				model.TotalRemainingValue += lot.QuantityRemaining * lot.UnitCost;
			}

			return View(model);
		}

		// =========================================
		// استلام مخزون - GET
		// =========================================

		[HttpGet]
		[RequirePermission("inventory.receive")]
		public async Task<IActionResult> Receive()
		{
			var model = new InventoryReceiveViewModel();
			model.Items.Add(new InventoryReceiveItemViewModel());

			await LoadReceiveDataAsync(model);

			return View(model);
		}

		// =========================================
		// استلام مخزون - POST
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("inventory.receive")]
		public async Task<IActionResult> Receive(
			InventoryReceiveViewModel model)
		{
			model.Notes =
				string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim();

			model.Items =
				model.Items?
					.Where(x => x.ProductId > 0)
					.ToList()
				?? new List<InventoryReceiveItemViewModel>();

			if (!ModelState.IsValid || model.Items.Count == 0)
			{
				ModelState.AddModelError(string.Empty, "يجب إدخال بند واحد على الأقل.");
				await LoadReceiveDataAsync(model);
				return View(model);
			}

			var storeExists =
				await _context.Stores
					.AnyAsync(x => x.Id == model.StoreId && x.IsActive);

			if (!storeExists)
			{
				ModelState.AddModelError(nameof(model.StoreId), "المخزن غير موجود أو غير نشط.");
				await LoadReceiveDataAsync(model);
				return View(model);
			}

			if (!await _employeeScopeService.CanAccessStoreAsync(model.StoreId))
			{
				ModelState.AddModelError(nameof(model.StoreId), "ليس لديك صلاحية على هذا المخزن.");
				await LoadReceiveDataAsync(model);
				return View(model);
			}

			var supplierExists =
				await _context.Suppliers
					.AnyAsync(x => x.Id == model.SupplierId && x.IsActive);

			if (!supplierExists)
			{
				ModelState.AddModelError(nameof(model.SupplierId), "المورد غير موجود أو غير نشط.");
				await LoadReceiveDataAsync(model);
				return View(model);
			}

			var productIds = model.Items.Select(x => x.ProductId).Distinct().ToList();

			var validProducts =
				await _context.Products
					.Where(x => x.IsActive && productIds.Contains(x.Id))
					.Select(x => x.Id)
					.ToListAsync();

			if (validProducts.Count != productIds.Count)
			{
				ModelState.AddModelError(string.Empty, "يوجد منتج غير موجود أو غير نشط.");
				await LoadReceiveDataAsync(model);
				return View(model);
			}

			foreach (var item in model.Items)
			{
				if (item.Quantity <= 0)
				{
					ModelState.AddModelError(string.Empty, "الكمية يجب أن تكون أكبر من صفر.");
					await LoadReceiveDataAsync(model);
					return View(model);
				}
			}

			var transaction =
				new StockTransaction
				{
					TransactionNumber = await GenerateTransactionNumberAsync(StockTransactionType.Receive),
					Type = StockTransactionType.Receive,
					FromStoreId = null,
					ToStoreId = model.StoreId,
					CreatedByUserId = CurrentUserId(),
					Notes = model.Notes,
					CreatedAt = DateTime.UtcNow
				};

			_context.StockTransactions.Add(transaction);

			foreach (var item in model.Items)
			{
				var lot =
					new StockLot
					{
						StoreId = model.StoreId,
						ProductId = item.ProductId,
						SupplierId = model.SupplierId,
						PurchaseInvoiceItemId = null,
						QuantityReceived = item.Quantity,
						QuantityRemaining = item.Quantity,
						UnitCost = item.UnitCost,
						PurchaseDate = DateTime.UtcNow,
						CreatedAt = DateTime.UtcNow,
						IsActive = true
					};

				_context.StockLots.Add(lot);

				transaction.Items.Add(
					new StockTransactionItem
					{
						ProductId = item.ProductId,
						Quantity = item.Quantity,
						UnitCost = item.UnitCost,
						StockLotId = lot.Id,
						Notes = null
					});
			}

			var strategy = _context.Database.CreateExecutionStrategy();

			await strategy.ExecuteAsync(
				async () =>
				{
					await using var dbTransaction = await _context.Database.BeginTransactionAsync();
					await _context.SaveChangesAsync();
					await dbTransaction.CommitAsync();
				});

			TempData["Success"] = $"تم استلام المخزون بنجاح - مستند {transaction.TransactionNumber}.";

			return RedirectToAction(nameof(Index));
		}

		// =========================================
		// صرف مخزون - GET
		// =========================================

		[HttpGet]
		[RequirePermission("inventory.issue")]
		public async Task<IActionResult> Issue()
		{
			var model = new InventoryIssueViewModel();
			model.Items.Add(new InventoryItemLineViewModel());

			await LoadIssueDataAsync(model);

			return View(model);
		}

		// =========================================
		// صرف مخزون - POST
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("inventory.issue")]
		public async Task<IActionResult> Issue(
			InventoryIssueViewModel model)
		{
			model.Notes =
				string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim();

			model.Reason =
				string.IsNullOrWhiteSpace(model.Reason) ? null : model.Reason.Trim();

			model.Items =
				model.Items?
					.Where(x => x.ProductId > 0)
					.ToList()
				?? new List<InventoryItemLineViewModel>();

			if (!ModelState.IsValid || model.Items.Count == 0)
			{
				ModelState.AddModelError(string.Empty, "يجب إدخال بند واحد على الأقل.");
				await LoadIssueDataAsync(model);
				return View(model);
			}

			var storeExists =
				await _context.Stores
					.AnyAsync(x => x.Id == model.StoreId && x.IsActive);

			if (!storeExists)
			{
				ModelState.AddModelError(nameof(model.StoreId), "المخزن غير موجود أو غير نشط.");
				await LoadIssueDataAsync(model);
				return View(model);
			}

			if (!await _employeeScopeService.CanAccessStoreAsync(model.StoreId))
			{
				ModelState.AddModelError(nameof(model.StoreId), "ليس لديك صلاحية على هذا المخزن.");
				await LoadIssueDataAsync(model);
				return View(model);
			}

			var productIds = model.Items.Select(x => x.ProductId).Distinct().ToList();

			var validProducts =
				await _context.Products
					.Where(x => x.IsActive && productIds.Contains(x.Id))
					.Select(x => x.Id)
					.ToListAsync();

			if (validProducts.Count != productIds.Count)
			{
				ModelState.AddModelError(string.Empty, "يوجد منتج غير موجود أو غير نشط.");
				await LoadIssueDataAsync(model);
				return View(model);
			}

			// ✅ (N+1 fix) query واحد لكل المنتجات قبل الـ loop
			var issueProductIds =
				model.Items
					.Select(x => x.ProductId)
					.Distinct()
					.ToList();

			var availableByProductIssue =
				await _context.StockLots
					.Where(x =>
						issueProductIds.Contains(x.ProductId) &&
						x.StoreId == model.StoreId &&
						x.IsActive &&
						x.QuantityRemaining > 0)
					.GroupBy(x => x.ProductId)
					.Select(g => new
					{
						ProductId = g.Key,
						Available = g.Sum(x => x.QuantityRemaining)
					})
					.ToDictionaryAsync(x => x.ProductId, x => x.Available);

			foreach (var item in model.Items)
			{
				if (item.Quantity <= 0)
				{
					ModelState.AddModelError(string.Empty, "الكمية يجب أن تكون أكبر من صفر.");
					await LoadIssueDataAsync(model);
					return View(model);
				}

				availableByProductIssue.TryGetValue(item.ProductId, out var available);

				if (available < item.Quantity)
				{
					ModelState.AddModelError(string.Empty, $"الكمية المطلوبة أكبر من المتاح للمنتج.");
					await LoadIssueDataAsync(model);
					return View(model);
				}
			}

			var transaction =
				new StockTransaction
				{
					TransactionNumber = await GenerateTransactionNumberAsync(StockTransactionType.Issue),
					Type = StockTransactionType.Issue,
					FromStoreId = model.StoreId,
					ToStoreId = null,
					CreatedByUserId = CurrentUserId(),
					Notes = !string.IsNullOrWhiteSpace(model.Reason) ? model.Reason : model.Notes,
					CreatedAt = DateTime.UtcNow
				};

			_context.StockTransactions.Add(transaction);

			foreach (var item in model.Items)
			{
				var (consumed, cost) =
					await ConsumeFromLotsAsync(
						model.StoreId,
						item.ProductId,
						item.Quantity);

				transaction.Items.Add(
					new StockTransactionItem
					{
						ProductId = item.ProductId,
						Quantity = consumed,
						UnitCost = consumed > 0 ? cost / consumed : 0,
						StockLotId = null,
						Notes = null
					});
			}

			var strategy = _context.Database.CreateExecutionStrategy();
			string? transactionError = null;

			await strategy.ExecuteAsync(
				async () =>
				{
					await using var dbTransaction = await _context.Database.BeginTransactionAsync();

					try
					{
						await _context.SaveChangesAsync();
						await dbTransaction.CommitAsync();
					}
					catch (DbUpdateConcurrencyException)
					{
						await dbTransaction.RollbackAsync();
						transactionError = "تم تعديل المخزون من مستخدم آخر أثناء الحفظ. أعد المحاولة.";
					}
				});

			if (transactionError != null)
			{
				TempData["Error"] = transactionError;
				return RedirectToAction(nameof(Index));
			}

			TempData["Success"] = $"تم صرف المخزون بنجاح - مستند {transaction.TransactionNumber}.";

			return RedirectToAction(nameof(Index));
		}

		// =========================================
		// تحويل بين المخازن - GET
		// =========================================

		[HttpGet]
		[RequirePermission("inventory.transfer")]
		public async Task<IActionResult> Transfer()
		{
			var model = new InventoryTransferViewModel();
			model.Items.Add(new InventoryItemLineViewModel());

			await LoadTransferDataAsync(model);

			return View(model);
		}

		// =========================================
		// تحويل بين المخازن - POST
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("inventory.transfer")]
		public async Task<IActionResult> Transfer(
			InventoryTransferViewModel model)
		{
			model.Notes =
				string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim();

			model.Items =
				model.Items?
					.Where(x => x.ProductId > 0)
					.ToList()
				?? new List<InventoryItemLineViewModel>();

			if (model.FromStoreId == model.ToStoreId)
			{
				ModelState.AddModelError(string.Empty, "لا يمكن التحويل إلى نفس المخزن.");
				await LoadTransferDataAsync(model);
				return View(model);
			}

			if (!ModelState.IsValid || model.Items.Count == 0)
			{
				ModelState.AddModelError(string.Empty, "يجب إدخال بند واحد على الأقل.");
				await LoadTransferDataAsync(model);
				return View(model);
			}

			var storesExist =
				await _context.Stores
					.CountAsync(x =>
						x.IsActive &&
						(x.Id == model.FromStoreId || x.Id == model.ToStoreId));

			if (storesExist != 2)
			{
				ModelState.AddModelError(string.Empty, "أحد المخازن غير موجود أو غير نشط.");
				await LoadTransferDataAsync(model);
				return View(model);
			}

			if (!await _employeeScopeService.CanAccessStoreAsync(model.FromStoreId))
			{
				ModelState.AddModelError(string.Empty, "ليس لديك صلاحية على المخزن المصدر.");
				await LoadTransferDataAsync(model);
				return View(model);
			}

			if (!await _employeeScopeService.CanAccessStoreAsync(model.ToStoreId))
			{
				ModelState.AddModelError(string.Empty, "ليس لديك صلاحية على المخزن الوجهة.");
				await LoadTransferDataAsync(model);
				return View(model);
			}

			var productIds = model.Items.Select(x => x.ProductId).Distinct().ToList();

			var validProducts =
				await _context.Products
					.Where(x => x.IsActive && productIds.Contains(x.Id))
					.Select(x => x.Id)
					.ToListAsync();

			if (validProducts.Count != productIds.Count)
			{
				ModelState.AddModelError(string.Empty, "يوجد منتج غير موجود أو غير نشط.");
				await LoadTransferDataAsync(model);
				return View(model);
			}

			// ✅ (N+1 fix) query واحد لكل المنتجات قبل الـ loop
			var transferProductIds =
				model.Items
					.Select(x => x.ProductId)
					.Distinct()
					.ToList();

			var availableByProductTransfer =
				await _context.StockLots
					.Where(x =>
						transferProductIds.Contains(x.ProductId) &&
						x.StoreId == model.FromStoreId &&
						x.IsActive &&
						x.QuantityRemaining > 0)
					.GroupBy(x => x.ProductId)
					.Select(g => new
					{
						ProductId = g.Key,
						Available = g.Sum(x => x.QuantityRemaining)
					})
					.ToDictionaryAsync(x => x.ProductId, x => x.Available);

			foreach (var item in model.Items)
			{
				if (item.Quantity <= 0)
				{
					ModelState.AddModelError(string.Empty, "الكمية يجب أن تكون أكبر من صفر.");
					await LoadTransferDataAsync(model);
					return View(model);
				}

				availableByProductTransfer.TryGetValue(item.ProductId, out var available);

				if (available < item.Quantity)
				{
					ModelState.AddModelError(string.Empty, $"الكمية المطلوبة أكبر من المتاح في المخزن المصدر للمنتج.");
					await LoadTransferDataAsync(model);
					return View(model);
				}
			}

			var transaction =
				new StockTransaction
				{
					TransactionNumber = await GenerateTransactionNumberAsync(StockTransactionType.Transfer),
					Type = StockTransactionType.Transfer,
					FromStoreId = model.FromStoreId,
					ToStoreId = model.ToStoreId,
					CreatedByUserId = CurrentUserId(),
					Notes = model.Notes,
					CreatedAt = DateTime.UtcNow
				};

			_context.StockTransactions.Add(transaction);

			// ✅ (N+1 fix) جيب كل الـ source lots في query واحد
			var sourceLotsByProduct =
				await _context.StockLots
					.AsNoTracking()
					.Where(x =>
						transferProductIds.Contains(x.ProductId) &&
						x.StoreId == model.FromStoreId &&
						x.IsActive)
					.GroupBy(x => x.ProductId)
					.Select(g => new
					{
						ProductId = g.Key,
						SupplierId = g
							.OrderByDescending(x => x.CreatedAt)
							.Select(x => x.SupplierId)
							.FirstOrDefault()
					})
					.ToDictionaryAsync(x => x.ProductId, x => x.SupplierId);

			foreach (var item in model.Items)
			{
				var (consumed, cost) =
					await ConsumeFromLotsAsync(
						model.FromStoreId,
						item.ProductId,
						item.Quantity);

				var avgCost =
					consumed > 0 ? cost / consumed : 0;

				sourceLotsByProduct.TryGetValue(item.ProductId, out var supplierId);

				_context.StockLots.Add(
					new StockLot
					{
						StoreId = model.ToStoreId,
						ProductId = item.ProductId,
						SupplierId = supplierId,
						PurchaseInvoiceItemId = null,
						QuantityReceived = consumed,
						QuantityRemaining = consumed,
						UnitCost = avgCost,
						PurchaseDate = DateTime.UtcNow,
						CreatedAt = DateTime.UtcNow,
						IsActive = true
					});
			}

			var strategy = _context.Database.CreateExecutionStrategy();
			string? transactionError = null;

			await strategy.ExecuteAsync(
				async () =>
				{
					await using var dbTransaction = await _context.Database.BeginTransactionAsync();

					try
					{
						await _context.SaveChangesAsync();
						await dbTransaction.CommitAsync();
					}
					catch (DbUpdateConcurrencyException)
					{
						await dbTransaction.RollbackAsync();
						transactionError = "تم تعديل المخزون من مستخدم آخر أثناء الحفظ. أعد المحاولة.";
					}
				});

			if (transactionError != null)
			{
				TempData["Error"] = transactionError;
				return RedirectToAction(nameof(Index));
			}

			TempData["Success"] = $"تم تحويل المخزون بين المخازن بنجاح - مستند {transaction.TransactionNumber}.";

			return RedirectToAction(nameof(Index));
		}

		// =========================================
		// تسوية مخزون - GET
		// =========================================

		[HttpGet]
		[RequirePermission("inventory.adjust")]
		public async Task<IActionResult> Adjust()
		{
			var model = new InventoryAdjustViewModel();

			await LoadAdjustDataAsync(model);

			return View(model);
		}

		// =========================================
		// تسوية مخزون - POST
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("inventory.adjust")]
		public async Task<IActionResult> Adjust(
			InventoryAdjustViewModel model)
		{
			model.Reason = model.Reason?.Trim() ?? string.Empty;

			if (!ModelState.IsValid)
			{
				await LoadAdjustDataAsync(model);
				return View(model);
			}

			var storeExists =
				await _context.Stores
					.AnyAsync(x => x.Id == model.StoreId && x.IsActive);

			if (!storeExists)
			{
				ModelState.AddModelError(nameof(model.StoreId), "المخزن غير موجود أو غير نشط.");
				await LoadAdjustDataAsync(model);
				return View(model);
			}

			if (!await _employeeScopeService.CanAccessStoreAsync(model.StoreId))
			{
				ModelState.AddModelError(nameof(model.StoreId), "ليس لديك صلاحية على هذا المخزن.");
				await LoadAdjustDataAsync(model);
				return View(model);
			}

			var productExists =
				await _context.Products
					.AnyAsync(x => x.Id == model.ProductId && x.IsActive);

			if (!productExists)
			{
				ModelState.AddModelError(nameof(model.ProductId), "المنتج غير موجود أو غير نشط.");
				await LoadAdjustDataAsync(model);
				return View(model);
			}

			var currentQuantity =
				await _context.StockLots
					.Where(x =>
						x.ProductId == model.ProductId &&
						x.StoreId == model.StoreId &&
						x.IsActive)
					.SumAsync(x => x.QuantityRemaining);

			var delta = model.NewQuantity - currentQuantity;

			if (Math.Abs(delta) < 0.0000001m)
			{
				TempData["Success"] = "لا يوجد تغيير في الرصيد (الكمية الجديدة تطابق الحالية).";
				return RedirectToAction(nameof(Index));
			}

			var transaction =
				new StockTransaction
				{
					TransactionNumber = await GenerateTransactionNumberAsync(StockTransactionType.Adjust),
					Type = StockTransactionType.Adjust,
					FromStoreId = model.StoreId,
					ToStoreId = null,
					CreatedByUserId = CurrentUserId(),
					Notes = model.Reason,
					CreatedAt = DateTime.UtcNow
				};

			_context.StockTransactions.Add(transaction);

			if (delta > 0)
			{
				var lotCost =
					await AwaitLotCostForAddAsync(model.ProductId, model.StoreId);

				var lot =
					new StockLot
					{
						StoreId = model.StoreId,
						ProductId = model.ProductId,
						SupplierId = null,
						PurchaseInvoiceItemId = null,
						QuantityReceived = delta,
						QuantityRemaining = delta,
						UnitCost = lotCost,
						PurchaseDate = DateTime.UtcNow,
						CreatedAt = DateTime.UtcNow,
						IsActive = true
					};

				_context.StockLots.Add(lot);

				transaction.Items.Add(
					new StockTransactionItem
					{
						ProductId = model.ProductId,
						Quantity = delta,
						UnitCost = lotCost,
						StockLotId = lot.Id,
						Notes = model.Reason
					});
			}
			else
			{
				var needQty = -delta;

				var available =
					await _context.StockLots
						.Where(x =>
							x.ProductId == model.ProductId &&
							x.StoreId == model.StoreId &&
							x.IsActive &&
							x.QuantityRemaining > 0)
						.SumAsync(x => x.QuantityRemaining);

				if (available + 0.0000001m < needQty)
				{
					ModelState.AddModelError(string.Empty, "لا يمكن تقليل الرصيد بهذا المقدار؛ الكمية المتاحة أقل.");
					await LoadAdjustDataAsync(model);
					return View(model);
				}

				var (consumed, cost) =
					await ConsumeFromLotsAsync(model.StoreId, model.ProductId, needQty);

				transaction.Items.Add(
					new StockTransactionItem
					{
						ProductId = model.ProductId,
						Quantity = consumed,
						UnitCost = consumed > 0 ? cost / consumed : 0,
						StockLotId = null,
						Notes = model.Reason
					});
			}

			var strategy = _context.Database.CreateExecutionStrategy();
			string? transactionError = null;

			await strategy.ExecuteAsync(
				async () =>
				{
					await using var dbTransaction = await _context.Database.BeginTransactionAsync();

					try
					{
						await _context.SaveChangesAsync();
						await dbTransaction.CommitAsync();
					}
					catch (DbUpdateConcurrencyException)
					{
						await dbTransaction.RollbackAsync();
						transactionError = "تم تعديل المخزون من مستخدم آخر أثناء الحفظ. أعد المحاولة.";
					}
				});

			if (transactionError != null)
			{
				TempData["Error"] = transactionError;
				return RedirectToAction(nameof(Index));
			}

			TempData["Success"] = $"تمت تسوية رصيد المنتج بنجاح - مستند {transaction.TransactionNumber}.";

			return RedirectToAction(nameof(Index));
		}

		// =========================================
		// جرد مخزن - GET
		// =========================================

		[HttpGet]
		[RequirePermission("inventory.count")]
		public async Task<IActionResult> Count(int? storeId)
		{
			var model =
				new InventoryCountViewModel
				{
					StoreId = storeId ?? 0,
					Stores = await LoadStoresAsync()
				};

			if (storeId.HasValue && storeId.Value > 0)
			{
				if (!await _employeeScopeService.CanAccessStoreAsync(storeId.Value))
				{
					return RedirectToAction("AccessDenied", "Account");
				}

				                                // ✅ (EF InMemory-compatible) نجيب البيانات الأول
                                // وبعدين نعمل GroupBy في الذاكرة
                                var lots =
                                        await _context.StockLots
                                                .AsNoTracking()
                                                .Include(x => x.Product)
                                                .Where(x => x.IsActive && x.StoreId == storeId.Value)
                                                .ToListAsync();

                                var rows = lots.GroupBy(x => x.ProductId);

                                foreach (var group in rows)
                                {
                                        var first = group.First();

                                        if (first.Product == null)
                                        {
                                                continue;
                                        }

                                        model.Lines.Add(
                                                new InventoryCountLineViewModel
                                                {
                                                        ProductId = first.ProductId,
                                                        ProductName = first.Product.Name,
                                                        SystemQuantity = group.Sum(x => x.QuantityRemaining),
                                                        CountedQuantity = group.Sum(x => x.QuantityRemaining)
                                                });
                                }
                        }

                        return View(model);
                }

		// =========================================
		// جرد مخزن - POST
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("inventory.count")]
		public async Task<IActionResult> Count(
			InventoryCountViewModel model)
		{
			model.Notes =
				string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim();

			var storeExists =
				await _context.Stores
					.AnyAsync(x => x.Id == model.StoreId && x.IsActive);

			if (!storeExists)
			{
				ModelState.AddModelError(nameof(model.StoreId), "المخزن غير موجود أو غير نشط.");
				model.Stores = await LoadStoresAsync();
				return View(model);
			}

			if (!await _employeeScopeService.CanAccessStoreAsync(model.StoreId))
			{
				ModelState.AddModelError(nameof(model.StoreId), "ليس لديك صلاحية على هذا المخزن.");
				model.Stores = await LoadStoresAsync();
				return View(model);
			}

			model.Lines ??= new List<InventoryCountLineViewModel>();
			model.Lines = model.Lines.Where(x => x.ProductId > 0).ToList();

			var changed =
				new List<(int ProductId, decimal SystemQuantity, decimal CountedQuantity)>();

			var productIds = model.Lines
				.Where(x => x.ProductId > 0)
				.Select(x => x.ProductId)
				.Distinct()
				.ToList();

			var systemQuantities = productIds.Any()
				? await _context.StockLots
					.Where(x =>
						x.StoreId == model.StoreId &&
						x.IsActive &&
						productIds.Contains(x.ProductId))
					.GroupBy(x => x.ProductId)
					.ToDictionaryAsync(
						g => g.Key,
						g => g.Sum(x => x.QuantityRemaining))
				: new Dictionary<int, decimal>();

			foreach (var line in model.Lines)
			{
				var systemQuantity = systemQuantities.TryGetValue(line.ProductId, out var qty) ? qty : 0;

				if (Math.Abs(systemQuantity - line.CountedQuantity) < 0.0000001m)
				{
					continue;
				}

				if (line.CountedQuantity < 0)
				{
					continue;
				}

				changed.Add((line.ProductId, systemQuantity, line.CountedQuantity));
			}

			if (changed.Count == 0)
			{
				TempData["Success"] = "الجرد مطابق للرصيد؛ لا توجد فروقات.";
				return RedirectToAction(nameof(Index));
			}

			var transaction =
				new StockTransaction
				{
					TransactionNumber = await GenerateTransactionNumberAsync(StockTransactionType.Count),
					Type = StockTransactionType.Count,
					FromStoreId = model.StoreId,
					ToStoreId = null,
					CreatedByUserId = CurrentUserId(),
					Notes = model.Notes,
					CreatedAt = DateTime.UtcNow
				};

			_context.StockTransactions.Add(transaction);

			foreach (var (productId, systemQuantity, countedQuantity) in changed)
			{
				var delta = countedQuantity - systemQuantity;

				if (delta > 0)
				{
					var lotCost = await AwaitLotCostForAddAsync(productId, model.StoreId);

					var lot =
						new StockLot
						{
							StoreId = model.StoreId,
							ProductId = productId,
							SupplierId = null,
							PurchaseInvoiceItemId = null,
							QuantityReceived = delta,
							QuantityRemaining = delta,
							UnitCost = lotCost,
							PurchaseDate = DateTime.UtcNow,
							CreatedAt = DateTime.UtcNow,
							IsActive = true
						};

					_context.StockLots.Add(lot);

					transaction.Items.Add(
						new StockTransactionItem
						{
							ProductId = productId,
							Quantity = delta,
							UnitCost = lotCost,
							StockLotId = lot.Id,
							Notes = "فرق جرد موجب"
						});
				}
				else
				{
					var (consumed, cost) =
						await ConsumeFromLotsAsync(model.StoreId, productId, -delta);

					transaction.Items.Add(
						new StockTransactionItem
						{
							ProductId = productId,
							Quantity = consumed,
							UnitCost = consumed > 0 ? cost / consumed : 0,
							StockLotId = null,
							Notes = "فرق جرد سالب"
						});
				}
			}

			var strategy = _context.Database.CreateExecutionStrategy();
			string? transactionError = null;

			await strategy.ExecuteAsync(
				async () =>
				{
					await using var dbTransaction = await _context.Database.BeginTransactionAsync();

					try
					{
						await _context.SaveChangesAsync();
						await dbTransaction.CommitAsync();
					}
					catch (DbUpdateConcurrencyException)
					{
						await dbTransaction.RollbackAsync();
						transactionError = "تم تعديل المخزون من مستخدم آخر أثناء الحفظ. أعد المحاولة.";
					}
				});

			if (transactionError != null)
			{
				TempData["Error"] = transactionError;
				return RedirectToAction(nameof(Index));
			}

			TempData["Success"] = $"تم تنفيذ الجرد بنجاح - مستند {transaction.TransactionNumber}.";

			return RedirectToAction(nameof(Index));
		}

		// =========================================
		// الكمية المتاحة لمنتج في مخزن (AJAX)
		// =========================================

		[HttpGet]
		[RequirePermission("inventory.view")]
		public async Task<IActionResult> GetProductQuantity(
			int productId,
			int storeId)
		{
			if (productId <= 0 || storeId <= 0)
			{
				return Json(new { quantity = 0, value = 0m });
			}

			if (!await _employeeScopeService.CanAccessStoreAsync(storeId))
			{
				return Json(new { quantity = 0, value = 0m });
			}

			var lots =
				await _context.StockLots
					.AsNoTracking()
					.Where(x =>
						x.ProductId == productId &&
						x.StoreId == storeId &&
						x.IsActive)
					.ToListAsync();

			var quantity = lots.Sum(x => x.QuantityRemaining);
			var value = lots.Sum(x => x.QuantityRemaining * x.UnitCost);

			return Json(new { quantity, value });
		}

		// =========================================
		// سجل الحركات
		// =========================================

		[HttpGet]
		[RequirePermission("inventory.transaction.view")]
		public async Task<IActionResult> Transactions(
			int? storeId,
			StockTransactionType? type)
		{
			var scope = await _employeeScopeService.GetScopeAsync();

			var model =
				new InventoryTransactionsViewModel
				{
					StoreId = storeId,
					Type = type,
					Stores = await LoadStoresAsync()
				};

			var query =
				_context.StockTransactions
					.AsNoTracking()
					.Include(x => x.FromStore)
					.Include(x => x.ToStore)
					.Include(x => x.CreatedByUser)
					.Include(x => x.Items)
						.ThenInclude(x => x.Product)
					.AsQueryable();

			if (scope.IsRestricted)
			{
				var allowedStores = scope.StoreIds.ToList();

				query = allowedStores.Count == 0
					? query.Where(_ => false)
					: query.Where(x =>
						(x.FromStoreId.HasValue && allowedStores.Contains(x.FromStoreId.Value)) ||
						(x.ToStoreId.HasValue && allowedStores.Contains(x.ToStoreId.Value)));
			}

			if (storeId.HasValue && storeId.Value > 0)
			{
				query = query.Where(x =>
					x.FromStoreId == storeId.Value ||
					x.ToStoreId == storeId.Value);
			}

			if (type.HasValue)
			{
				query = query.Where(x => x.Type == type.Value);
			}

			var transactions =
				await query
					.OrderByDescending(x => x.CreatedAt)
					.ThenByDescending(x => x.Id)
					.Take(AppConstants.DefaultPageSize)
					.ToListAsync();

			foreach (var transaction in transactions)
			{
				model.Rows.Add(
					new InventoryTransactionRowViewModel
					{
						Id = transaction.Id,
						TransactionNumber = transaction.TransactionNumber,
						Type = transaction.Type,
						FromStoreName = transaction.FromStore?.Name,
						ToStoreName = transaction.ToStore?.Name,
						CreatedByUserName = transaction.CreatedByUser?.Phone,
						CreatedAt = transaction.CreatedAt,
						ItemCount = transaction.Items.Count,
						Notes = transaction.Notes,
						Items = transaction.Items
							.Select(x =>
								new InventoryTransactionLineRowViewModel
								{
									ProductName = x.Product?.Name ?? "-",
									Quantity = x.Quantity,
									UnitCost = x.UnitCost
								})
							.ToList()
					});
			}

			return View(model);
		}

		// =========================================
		// كشف حركة منتج (دخول/خروج)
		// =========================================

		[HttpGet]
		[RequirePermission("inventory.view")]
		public async Task<IActionResult> ProductMovement(
			int? productId,
			int? storeId,
			DateTime? fromDate,
			DateTime? toDate)
		{
			var model = new InventoryProductMovementViewModel
			{
				ProductId = productId,
				StoreId = storeId,
				FromDate = fromDate,
				ToDate = toDate,
				Products = await LoadProductsAsync(),
				Stores = await LoadStoresAsync()
			};

			if (productId is > 0)
			{
				var product =
					await _context.Products
						.AsNoTracking()
						.FirstOrDefaultAsync(x => x.Id == productId);

				if (product != null)
				{
					model.ProductName = product.Name;
				}

				var events =
					new List<(DateTime Date, string Doc, string Source, string TypeLabel,
						string TypeBadge, string Dir, bool? Inc, string? Store,
						string? Counter, decimal Qty, decimal Cost, string? Notes)>();

				var transactions =
					await _context.StockTransactions
						.AsNoTracking()
						.Include(x => x.FromStore)
						.Include(x => x.ToStore)
						.Include(x => x.Items)
						.Where(x => x.Items.Any(i => i.ProductId == productId.Value))
						.OrderBy(x => x.CreatedAt)
						.ThenBy(x => x.Id)
						.ToListAsync();

				int selectedStore = storeId.GetValueOrDefault();

				foreach (var transaction in transactions)
				{
					foreach (var item in transaction.Items.Where(i => i.ProductId == productId.Value))
					{
						var subjectNotes =
							string.IsNullOrWhiteSpace(item.Notes) ? transaction.Notes : item.Notes;

						var flows =
							new List<(string dir, bool inc, string? store, string? counter, int? storeId)>();

						switch (transaction.Type)
						{
							case StockTransactionType.Receive:
								flows.Add(("دخول", true, transaction.ToStore?.Name, transaction.FromStore?.Name, transaction.ToStoreId));
								break;

							case StockTransactionType.Issue:
								flows.Add(("خروج", false, transaction.FromStore?.Name, transaction.ToStore?.Name, transaction.FromStoreId));
								break;

							case StockTransactionType.Transfer:
								flows.Add(("خروج", false, transaction.FromStore?.Name, transaction.ToStore?.Name, transaction.FromStoreId));
								flows.Add(("دخول", true, transaction.ToStore?.Name, transaction.FromStore?.Name, transaction.ToStoreId));
								break;

							case StockTransactionType.Adjust:
							case StockTransactionType.Count:
								if (subjectNotes?.Contains("فرق جرد موجب") == true)
								{
									flows.Add(("دخول", true, transaction.FromStore?.Name, null, transaction.FromStoreId));
								}
								else if (subjectNotes?.Contains("فرق جرد سالب") == true)
								{
									flows.Add(("خروج", false, transaction.FromStore?.Name, null, transaction.FromStoreId));
								}
								else
								{
									flows.Add(("غير محدد", false, transaction.FromStore?.Name, null, transaction.FromStoreId));
								}
								break;
						}

						foreach (var flow in flows)
						{
							if (selectedStore != 0 && flow.storeId != selectedStore)
							{
								continue;
							}

							events.Add(
								(transaction.CreatedAt,
									transaction.TransactionNumber,
									"مخزون يدوي",
									transaction.Type switch
									{
										StockTransactionType.Receive => "استلام",
										StockTransactionType.Issue => "صرف",
										StockTransactionType.Transfer => "تحويل",
										StockTransactionType.Adjust => "تسوية",
										StockTransactionType.Count => "جرد",
										_ => "حركة"
									},
									transaction.Type switch
									{
										StockTransactionType.Receive => "bg-success",
										StockTransactionType.Issue => "bg-danger",
										StockTransactionType.Transfer => "bg-primary",
										StockTransactionType.Adjust => "bg-warning text-dark",
										StockTransactionType.Count => "bg-secondary",
										_ => "bg-dark"
									},
									flow.dir,
									flow.dir == "غير محدد" ? (bool?)null : flow.inc,
									flow.store,
									flow.counter,
									item.Quantity,
									item.UnitCost,
									subjectNotes));
						}
					}
				}

				var purchaseLots =
					await _context.StockLots
						.AsNoTracking()
						.Include(x => x.Store)
						.Include(x => x.Supplier)
						.Include(x => x.PurchaseInvoiceItem)
						.ThenInclude(x => x!.PurchaseInvoice)
						.Where(x =>
							x.ProductId == productId.Value &&
							x.PurchaseInvoiceItemId != null &&
							x.IsActive)
						.ToListAsync();

				foreach (var lot in purchaseLots)
				{
					if (selectedStore != 0 && lot.StoreId != selectedStore)
					{
						continue;
					}

					var purchaseDoc =
						lot.PurchaseInvoiceItem?.PurchaseInvoice?.InvoiceNumber ?? $"دفعة {lot.Id}";

					events.Add(
						(lot.CreatedAt,
							purchaseDoc,
							"شراء",
							"شراء",
							"bg-success",
							"دخول",
							true,
							lot.Store?.Name,
							lot.Supplier?.Name,
							lot.QuantityReceived,
							lot.UnitCost,
							$"استلام شراء - دفعة {lot.Id}"));
				}

				var saleAllocations =
					await _context.SalesInvoiceItemLots
						.AsNoTracking()
						.Include(x => x.StockLot)
						.ThenInclude(x => x!.Store)
						.Include(x => x.SalesInvoiceItem)
						.ThenInclude(x => x!.SalesInvoice)
						.ThenInclude(x => x!.Customer)
						.Where(x =>
							x.SalesInvoiceItem != null &&
							x.SalesInvoiceItem.SalesInvoice != null &&
							x.SalesInvoiceItem.ProductId == productId.Value &&
							x.SalesInvoiceItem.SalesInvoice.Status != SalesInvoiceStatus.Cancelled)
						.ToListAsync();

				foreach (var alloc in saleAllocations)
				{
					var lot = alloc.StockLot;
					var invoice = alloc.SalesInvoiceItem?.SalesInvoice;

					if (selectedStore != 0 && lot?.StoreId != selectedStore)
					{
						continue;
					}

					events.Add(
						(invoice?.InvoiceDate ?? DateTime.UtcNow,
							invoice?.InvoiceNumber ?? $"بيع {alloc.Id}",
							"بيع",
							"فاتورة بيع",
							"bg-danger",
							"خروج",
							false,
							lot?.Store?.Name,
							invoice?.Customer?.Name,
							alloc.QuantityBaseUnit,
							alloc.UnitCost,
							invoice?.Notes));
				}

				decimal openingBalance = 0;

				if (fromDate != null)
				{
					foreach (var e in events.Where(x => x.Date < fromDate.Value).OrderBy(x => x.Date).ThenBy(x => x.Doc))
					{
						if (e.Inc == true) openingBalance += e.Qty;
						else if (e.Inc == false) openingBalance -= e.Qty;
					}
				}

				var visible = events
					.Where(x => fromDate == null || x.Date >= fromDate.Value)
					.Where(x => toDate == null || x.Date <= toDate.Value.AddDays(1))
					.OrderBy(x => x.Date)
					.ThenBy(x => x.Doc)
					.ToList();

				var rows = new List<InventoryProductMovementRowViewModel>();

				decimal runningBalance = openingBalance;
				decimal totalIn = 0;
				decimal totalOut = 0;

				foreach (var e in visible)
				{
					if (e.Inc == true)
					{
						runningBalance += e.Qty;
						totalIn += e.Qty;
					}
					else if (e.Inc == false)
					{
						runningBalance -= e.Qty;
						totalOut += e.Qty;
					}

					rows.Add(
						new InventoryProductMovementRowViewModel
						{
							CreatedAt = e.Date,
							TransactionNumber = e.Doc,
							Source = e.Source,
							TypeLabel = e.TypeLabel,
							TypeBadgeClass = e.TypeBadge,
							DirectionLabel = e.Dir,
							IsIncrease = e.Inc,
							MovementStoreName = e.Store,
							CounterStoreName = e.Counter,
							Quantity = e.Qty,
							UnitCost = e.Cost,
							TotalCost = e.Qty * e.Cost,
							Notes = e.Notes,
							RunningBalance = runningBalance
						});
				}

				model.Rows = rows;
				model.OpeningBalance = openingBalance;
				model.TotalIn = totalIn;
				model.TotalOut = totalOut;
				model.NetBalance = totalIn - totalOut;
			}

			return View(model);
		}

		// =========================================
		// استهلاك دفعات (FIFO)
		// =========================================

		private async Task<(decimal Consumed, decimal Cost)>
			ConsumeFromLotsAsync(
				int storeId,
				int productId,
				decimal quantity)
		{
			var lots =
				await _context.StockLots
					.Where(x =>
						x.ProductId == productId &&
						x.StoreId == storeId &&
						x.IsActive &&
						x.QuantityRemaining > 0)
					.OrderBy(x => x.PurchaseDate)
					.ThenBy(x => x.Id)
					.ToListAsync();

			var need = quantity;
			var consumed = 0m;
			var totalCost = 0m;

			foreach (var lot in lots)
			{
				if (need <= 0)
				{
					break;
				}

				var take = Math.Min(lot.QuantityRemaining, need);

				lot.QuantityRemaining -= take;

				totalCost += take * lot.UnitCost;
				consumed += take;
				need -= take;
			}

			return (consumed, totalCost);
		}

		// =========================================
		// تكلفة للدفعة الجديدة (زيادة رصيد)
		// =========================================

		private async Task<decimal> AwaitLotCostForAddAsync(
			int productId,
			int storeId)
		{
			var avgCost =
				await _context.StockLots
					.Where(x =>
						x.ProductId == productId &&
						x.StoreId == storeId &&
						x.IsActive &&
						x.QuantityRemaining > 0)
					.SumAsync(x => (decimal?)(x.QuantityRemaining * x.UnitCost));

			var totalQuantity =
				await _context.StockLots
					.Where(x =>
						x.ProductId == productId &&
						x.StoreId == storeId &&
						x.IsActive &&
						x.QuantityRemaining > 0)
					.SumAsync(x => (decimal?)x.QuantityRemaining) ?? 0;

			if (totalQuantity > 0)
			{
				return (avgCost ?? 0) / totalQuantity;
			}

			var lastLot =
				await _context.StockLots
					.AsNoTracking()
					.Where(x => x.ProductId == productId && x.IsActive)
					.OrderByDescending(x => x.CreatedAt)
					.FirstOrDefaultAsync();

			return lastLot?.UnitCost ?? 0;
		}

		// =========================================
		// رقم مستند تلقائي
		// =========================================

		private async Task<string> GenerateTransactionNumberAsync(
			StockTransactionType type)
		{
			var prefix =
				type switch
				{
					StockTransactionType.Receive => "RCV",
					StockTransactionType.Issue => "ISS",
					StockTransactionType.Transfer => "TRF",
					StockTransactionType.Adjust => "ADJ",
					StockTransactionType.Count => "CNT",
					_ => "MOV"
				};

			var datePart = DateTime.Today.ToString("yyyyMMdd");
			var searchPrefix = $"{prefix}-{datePart}-";

			var lastNumber =
				await _context.StockTransactions
					.AsNoTracking()
					.Where(x =>
						x.Type == type &&
						x.TransactionNumber.StartsWith(searchPrefix))
					.OrderByDescending(x => x.TransactionNumber)
					.Select(x => x.TransactionNumber)
					.FirstOrDefaultAsync();

			var nextSeq = 1;

			if (!string.IsNullOrEmpty(lastNumber))
			{
				var parts = lastNumber.Split('-');

				if (parts.Length == 3 && int.TryParse(parts[2], out var last))
				{
					nextSeq = last + 1;
				}
			}

			return $"{searchPrefix}{nextSeq:0000}";
		}

		// =========================================
		// المستخدم الحالي
		// =========================================

		private int? CurrentUserId()
		{
			var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

			if (int.TryParse(value, out var id))
			{
				return id;
			}

			return null;
		}

		// =========================================
		// تحميل قوائم الشاشات (مفلترة بالـScope)
		// =========================================

		private async Task<List<KeyValuePair<int, string>>> LoadStoresAsync()
		{
			var scope = await _employeeScopeService.GetScopeAsync();

			var query = _context.Stores
				.AsNoTracking()
				.Where(x => x.IsActive);

			query = _employeeScopeService.ApplyStoreFilter(query, scope);

			return await query
				.OrderBy(x => x.Name)
				.Select(x => new KeyValuePair<int, string>(x.Id, x.Name))
				.ToListAsync();
		}

		private async Task<List<KeyValuePair<int, string>>> LoadSuppliersAsync()
		{
			return await _context.Suppliers
				.AsNoTracking()
				.Where(x => x.IsActive)
				.OrderBy(x => x.Name)
				.Select(x => new KeyValuePair<int, string>(x.Id, x.Name))
				.ToListAsync();
		}

		private async Task<List<InventoryProductOptionViewModel>> LoadProductsAsync()
		{
			return await _context.Products
				.AsNoTracking()
				.Where(x => x.IsActive)
				.OrderBy(x => x.Name)
				.Select(x =>
					new InventoryProductOptionViewModel
					{
						Id = x.Id,
						Name = x.Name,
						Code = x.Code
					})
				.ToListAsync();
		}

		private async Task LoadReceiveDataAsync(InventoryReceiveViewModel model)
		{
			model.Stores = await LoadStoresAsync();
			model.Suppliers = await LoadSuppliersAsync();
			model.Products = await LoadProductsAsync();
		}

		private async Task LoadIssueDataAsync(InventoryIssueViewModel model)
		{
			model.Stores = await LoadStoresAsync();
			model.Products = await LoadProductsAsync();
		}

		private async Task LoadTransferDataAsync(InventoryTransferViewModel model)
		{
			model.Stores = await LoadStoresAsync();
			model.Products = await LoadProductsAsync();
		}

		private async Task LoadAdjustDataAsync(InventoryAdjustViewModel model)
		{
			model.Stores = await LoadStoresAsync();
			model.Products = await LoadProductsAsync();
		}
	}
}