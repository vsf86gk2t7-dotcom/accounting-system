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
		// ط¹ط±ط¶ ط§ظ„ط£ط±طµط¯ط©
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

			// âœ… ظپظ„طھط±ط© ط§ظ„ظ…ط®ط§ط²ظ† ط§ظ„ظ…ط³ظ…ظˆط­ط©
			if (scope.IsRestricted)
			{
				var allowedStores = scope.StoreIds.ToList();

				lotsQuery = allowedStores.Count == 0
					? lotsQuery.Where(_ => false)
					: lotsQuery.Where(x => allowedStores.Contains(x.StoreId));
			}

			if (storeId.HasValue &&
				storeId.Value > 0)
			{
				lotsQuery =
					lotsQuery.Where(
						x => x.StoreId == storeId.Value);
			}

			if (!string.IsNullOrWhiteSpace(model.Search))
			{
				var term = model.Search;

				lotsQuery =
					lotsQuery.Where(x =>
						(x.Product != null &&
						 (x.Product.Name.Contains(term) ||
						  (x.Product.Code != null &&
						   x.Product.Code.Contains(term)) ||
						  (x.Product.Barcode != null &&
						   x.Product.Barcode.Contains(term)))) ||
						(x.Store != null &&
						 x.Store.Name.Contains(term)));
			}

			var lots =
				await lotsQuery.ToListAsync();

			var groups =
				lots
					.GroupBy(x => new
					{
						x.ProductId,
						x.StoreId
					});

			foreach (var group in groups)
			{
				var first =
					group.First();

				if (first.Product == null ||
					first.Store == null)
				{
					continue;
				}

				var quantity =
					group.Sum(x => x.QuantityRemaining);

				var value =
					group.Sum(
						x => x.QuantityRemaining * x.UnitCost);

				model.Rows.Add(
					new InventoryBalanceRowViewModel
					{
						ProductId =
							first.ProductId,
						ProductName =
							first.Product.Name,
						ProductCode =
							first.Product.Code,
						Barcode =
							first.Product.Barcode,
						StoreId =
							first.StoreId,
						StoreName =
							first.Store.Name,
						Quantity =
							quantity,
						Value =
							value,
						AverageCost =
							quantity > 0
								? value / quantity
								: 0,
						MinQuantity =
							first.Product.MinQuantity
					});
			}

			model.Rows =
				model.Rows
					.OrderBy(x => x.ProductName)
					.ThenBy(x => x.StoreName)
					.ToList();

			return View(model);
		}

		// =========================================
		// ط¯ظپط¹ط§طھ ط§ظ„ظ…ط®ط²ظˆظ†
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
					Products =
						await _context.Products
							.AsNoTracking()
							.Where(x => x.IsActive)
							.OrderBy(x => x.Name)
							.Select(x =>
								new KeyValuePair<int, string>(
									x.Id,
									x.Name))
							.ToListAsync()
				};

			var query =
				_context.StockLots
					.AsNoTracking()
					.Include(x => x.Product)
					.Include(x => x.Store)
					.Include(x => x.Supplier)
					.AsQueryable();

			// âœ… ظپظ„طھط±ط© ط§ظ„ظ…ط®ط§ط²ظ† ط§ظ„ظ…ط³ظ…ظˆط­ط©
			if (scope.IsRestricted)
			{
				var allowedStores = scope.StoreIds.ToList();

				query = allowedStores.Count == 0
					? query.Where(_ => false)
					: query.Where(x => allowedStores.Contains(x.StoreId));
			}

			if (model.StoreId.HasValue &&
				model.StoreId.Value > 0)
			{
				query =
					query.Where(x =>
						x.StoreId == model.StoreId.Value);
			}

			if (model.ProductId.HasValue &&
				model.ProductId.Value > 0)
			{
				query =
					query.Where(x =>
						x.ProductId == model.ProductId.Value);
			}

			if (model.OnlyAvailable)
			{
				query =
					query.Where(x =>
						x.QuantityRemaining > 0);
			}

			if (!string.IsNullOrWhiteSpace(model.Search))
			{
				var term = model.Search;

				query =
					query.Where(x =>
						(x.Product != null &&
						 (x.Product.Name.Contains(term) ||
						  (x.Product.Code != null &&
						   x.Product.Code.Contains(term)) ||
						  (x.Product.Barcode != null &&
						   x.Product.Barcode.Contains(term)))) ||
						(x.Supplier != null &&
						 x.Supplier.Name.Contains(term)));
			}

			var lots =
				await query
					.OrderBy(x => x.PurchaseDate)
					.ThenBy(x => x.Id)
					.ToListAsync();

			foreach (var lot in lots)
			{
				model.Rows.Add(
					new InventoryLotRowViewModel
					{
						Id = lot.Id,
						ProductName =
							lot.Product?.Name ?? "-",
						ProductCode =
							lot.Product?.Code,
						StoreName =
							lot.Store?.Name ?? "-",
						SupplierName =
							lot.Supplier?.Name ?? "ط¯ظپط¹ط© ظ†ط¸ط§ظ…ظٹط©",
						PurchaseDate = lot.PurchaseDate,
						QuantityReceived =
							lot.QuantityReceived,
						QuantityRemaining =
							lot.QuantityRemaining,
						UnitCost = lot.UnitCost,
						RemainingValue =
							lot.QuantityRemaining * lot.UnitCost
					});

				model.TotalRemainingValue +=
					lot.QuantityRemaining * lot.UnitCost;
			}

			return View(model);
		}

		// =========================================
		// ط§ط³طھظ„ط§ظ… ظ…ط®ط²ظˆظ† - GET
		// =========================================

		[HttpGet]
		[RequirePermission("inventory.receive")]
		public async Task<IActionResult> Receive()
		{
			var model =
				new InventoryReceiveViewModel();

			model.Items.Add(
				new InventoryReceiveItemViewModel());

			await LoadReceiveDataAsync(model);

			return View(model);
		}

		// =========================================
		// ط§ط³طھظ„ط§ظ… ظ…ط®ط²ظˆظ† - POST
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("inventory.receive")]
		public async Task<IActionResult> Receive(
			InventoryReceiveViewModel model)
		{
			model.Notes =
				string.IsNullOrWhiteSpace(model.Notes)
					? null
					: model.Notes.Trim();

			model.Items =
				model.Items?
						.Where(x => x.ProductId > 0)
						.ToList()
					?? new List<InventoryReceiveItemViewModel>();

			if (!ModelState.IsValid ||
				model.Items.Count == 0)
			{
				ModelState.AddModelError(
					string.Empty,
					"ظٹط¬ط¨ ط¥ط¯ط®ط§ظ„ ط¨ظ†ط¯ ظˆط§ط­ط¯ ط¹ظ„ظ‰ ط§ظ„ط£ظ‚ظ„.");

				await LoadReceiveDataAsync(model);

				return View(model);
			}

			// =========================================
			// ط§ظ„طھط­ظ‚ظ‚ ظ…ظ† ظˆط¬ظˆط¯ ط§ظ„ظ…ط®ط²ظ† ظˆط§ظ„ظ…ظˆط±ط¯
			// =========================================

			var storeExists =
				await _context.Stores
					.AnyAsync(x =>
						x.Id == model.StoreId &&
						x.IsActive);

			if (!storeExists)
			{
				ModelState.AddModelError(
					nameof(model.StoreId),
					"ط§ظ„ظ…ط®ط²ظ† ط؛ظٹط± ظ…ظˆط¬ظˆط¯ ط£ظˆ ط؛ظٹط± ظ†ط´ط·.");

				await LoadReceiveDataAsync(model);

				return View(model);
			}

			// âœ… طھط­ظ‚ظ‚ ظ…ظ† طµظ„ط§ط­ظٹط© ط§ظ„ظˆطµظˆظ„ ظ„ظ„ظ…ط®ط²ظ†
			if (!await _employeeScopeService
					.CanAccessStoreAsync(model.StoreId))
			{
				ModelState.AddModelError(
					nameof(model.StoreId),
					"ظ„ظٹط³ ظ„ط¯ظٹظƒ طµظ„ط§ط­ظٹط© ط¹ظ„ظ‰ ظ‡ط°ط§ ط§ظ„ظ…ط®ط²ظ†.");

				await LoadReceiveDataAsync(model);

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
					"ط§ظ„ظ…ظˆط±ط¯ ط؛ظٹط± ظ…ظˆط¬ظˆط¯ ط£ظˆ ط؛ظٹط± ظ†ط´ط·.");

				await LoadReceiveDataAsync(model);

				return View(model);
			}

			// =========================================
			// ط§ظ„طھط­ظ‚ظ‚ ظ…ظ† ط§ظ„ظ…ظ†طھط¬ط§طھ
			// =========================================

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
					"ظٹظˆط¬ط¯ ظ…ظ†طھط¬ ط؛ظٹط± ظ…ظˆط¬ظˆط¯ ط£ظˆ ط؛ظٹط± ظ†ط´ط·.");

				await LoadReceiveDataAsync(model);

				return View(model);
			}

			foreach (var item in model.Items)
			{
				if (item.Quantity <= 0)
				{
					ModelState.AddModelError(
						string.Empty,
						"ط§ظ„ظƒظ…ظٹط© ظٹط¬ط¨ ط£ظ† طھظƒظˆظ† ط£ظƒط¨ط± ظ…ظ† طµظپط±.");

					await LoadReceiveDataAsync(model);

					return View(model);
				}
			}

			// =========================================
			// ط¥ظ†ط´ط§ط، ط§ظ„ط­ط±ظƒط©
			// =========================================

			var transaction =
				new StockTransaction
				{
					TransactionNumber =
						await GenerateTransactionNumberAsync(
							StockTransactionType.Receive),
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
						StockLot = lot,
						Notes = null
					});
			}

			var strategy =
				_context.Database.CreateExecutionStrategy();

			await strategy.ExecuteAsync(
				async () =>
				{
					await using var dbTransaction =
						await _context.Database.BeginTransactionAsync();

					await _context.SaveChangesAsync();
					await dbTransaction.CommitAsync();
				});

			TempData["Success"] =
				$"طھظ… ط§ط³طھظ„ط§ظ… ط§ظ„ظ…ط®ط²ظˆظ† ط¨ظ†ط¬ط§ط­ - ظ…ط³طھظ†ط¯ {transaction.TransactionNumber}.";

			return RedirectToAction(nameof(Index));
		}

		// =========================================
		// طµط±ظپ ظ…ط®ط²ظˆظ† - GET
		// =========================================

		[HttpGet]
		[RequirePermission("inventory.issue")]
		public async Task<IActionResult> Issue()
		{
			var model =
				new InventoryIssueViewModel();

			model.Items.Add(
				new InventoryItemLineViewModel());

			await LoadIssueDataAsync(model);

			return View(model);
		}

		// =========================================
		// طµط±ظپ ظ…ط®ط²ظˆظ† - POST
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("inventory.issue")]
		public async Task<IActionResult> Issue(
			InventoryIssueViewModel model)
		{
			model.Notes =
				string.IsNullOrWhiteSpace(model.Notes)
					? null
					: model.Notes.Trim();

			model.Reason =
				string.IsNullOrWhiteSpace(model.Reason)
					? null
					: model.Reason.Trim();

			model.Items =
				model.Items?
						.Where(x => x.ProductId > 0)
						.ToList()
					?? new List<InventoryItemLineViewModel>();

			if (!ModelState.IsValid ||
				model.Items.Count == 0)
			{
				ModelState.AddModelError(
					string.Empty,
					"ظٹط¬ط¨ ط¥ط¯ط®ط§ظ„ ط¨ظ†ط¯ ظˆط§ط­ط¯ ط¹ظ„ظ‰ ط§ظ„ط£ظ‚ظ„.");

				await LoadIssueDataAsync(model);

				return View(model);
			}

			var storeExists =
				await _context.Stores
					.AnyAsync(x =>
						x.Id == model.StoreId &&
						x.IsActive);

			if (!storeExists)
			{
				ModelState.AddModelError(
					nameof(model.StoreId),
					"ط§ظ„ظ…ط®ط²ظ† ط؛ظٹط± ظ…ظˆط¬ظˆط¯ ط£ظˆ ط؛ظٹط± ظ†ط´ط·.");

				await LoadIssueDataAsync(model);

				return View(model);
			}

			// âœ… طھط­ظ‚ظ‚ ظ…ظ† طµظ„ط§ط­ظٹط© ط§ظ„ظˆطµظˆظ„ ظ„ظ„ظ…ط®ط²ظ†
			if (!await _employeeScopeService
					.CanAccessStoreAsync(model.StoreId))
			{
				ModelState.AddModelError(
					nameof(model.StoreId),
					"ظ„ظٹط³ ظ„ط¯ظٹظƒ طµظ„ط§ط­ظٹط© ط¹ظ„ظ‰ ظ‡ط°ط§ ط§ظ„ظ…ط®ط²ظ†.");

				await LoadIssueDataAsync(model);

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
					"ظٹظˆط¬ط¯ ظ…ظ†طھط¬ ط؛ظٹط± ظ…ظˆط¬ظˆط¯ ط£ظˆ ط؛ظٹط± ظ†ط´ط·.");

				await LoadIssueDataAsync(model);

				return View(model);
			}

			// =========================================
			// ط§ظ„طھط­ظ‚ظ‚ ظ…ظ† طھظˆظپط± ط§ظ„ظƒظ…ظٹط§طھ ظ‚ط¨ظ„ ط§ظ„طھط؛ظٹظٹط±
			// =========================================

			foreach (var item in model.Items)
			{
				if (item.Quantity <= 0)
				{
					ModelState.AddModelError(
						string.Empty,
						"ط§ظ„ظƒظ…ظٹط© ظٹط¬ط¨ ط£ظ† طھظƒظˆظ† ط£ظƒط¨ط± ظ…ظ† طµظپط±.");

					await LoadIssueDataAsync(model);

					return View(model);
				}

				var available =
					await _context.StockLots
						.Where(x =>
							x.ProductId == item.ProductId &&
							x.StoreId == model.StoreId &&
							x.IsActive &&
							x.QuantityRemaining > 0)
						.SumAsync(x => x.QuantityRemaining);

				if (available < item.Quantity)
				{
					ModelState.AddModelError(
						string.Empty,
						$"ط§ظ„ظƒظ…ظٹط© ط§ظ„ظ…ط·ظ„ظˆط¨ط© ط£ظƒط¨ط± ظ…ظ† ط§ظ„ظ…طھط§ط­ ظ„ظ„ظ…ظ†طھط¬.");

					await LoadIssueDataAsync(model);

					return View(model);
				}
			}

			// =========================================
			// ط¥ظ†ط´ط§ط، ط§ظ„ط­ط±ظƒط©
			// =========================================

			var transaction =
				new StockTransaction
				{
					TransactionNumber =
						await GenerateTransactionNumberAsync(
							StockTransactionType.Issue),
					Type = StockTransactionType.Issue,
					FromStoreId = model.StoreId,
					ToStoreId = null,
					CreatedByUserId = CurrentUserId(),
					Notes = !string.IsNullOrWhiteSpace(model.Reason)
						? model.Reason
						: model.Notes,
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
						UnitCost = consumed > 0
							? cost / consumed
							: 0,
						StockLotId = null,
						Notes = null
					});
			}

			var strategy =
				_context.Database.CreateExecutionStrategy();

			string? transactionError = null;

			await strategy.ExecuteAsync(
				async () =>
				{
					await using var dbTransaction =
						await _context.Database.BeginTransactionAsync();

					try
					{
						await _context.SaveChangesAsync();
						await dbTransaction.CommitAsync();
					}
					catch (DbUpdateConcurrencyException)
					{
						await dbTransaction.RollbackAsync();
						transactionError =
							"طھظ… طھط¹ط¯ظٹظ„ ط§ظ„ظ…ط®ط²ظˆظ† ظ…ظ† ظ…ط³طھط®ط¯ظ… ط¢ط®ط± ط£ط«ظ†ط§ط، ط§ظ„ط­ظپط¸. ط£ط¹ط¯ ط§ظ„ظ…ط­ط§ظˆظ„ط©.";
					}
				});

			if (transactionError != null)
			{
				TempData["Error"] = transactionError;
				return RedirectToAction(nameof(Index));
			}

			TempData["Success"] =
				$"طھظ… طµط±ظپ ط§ظ„ظ…ط®ط²ظˆظ† ط¨ظ†ط¬ط§ط­ - ظ…ط³طھظ†ط¯ {transaction.TransactionNumber}.";

			return RedirectToAction(nameof(Index));
		}

		// =========================================
		// طھط­ظˆظٹظ„ ط¨ظٹظ† ط§ظ„ظ…ط®ط§ط²ظ† - GET
		// =========================================

		[HttpGet]
		[RequirePermission("inventory.transfer")]
		public async Task<IActionResult> Transfer()
		{
			var model =
				new InventoryTransferViewModel();

			model.Items.Add(
				new InventoryItemLineViewModel());

			await LoadTransferDataAsync(model);

			return View(model);
		}

		// =========================================
		// طھط­ظˆظٹظ„ ط¨ظٹظ† ط§ظ„ظ…ط®ط§ط²ظ† - POST
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("inventory.transfer")]
		public async Task<IActionResult> Transfer(
			InventoryTransferViewModel model)
		{
			model.Notes =
				string.IsNullOrWhiteSpace(model.Notes)
					? null
					: model.Notes.Trim();

			model.Items =
				model.Items?
						.Where(x => x.ProductId > 0)
						.ToList()
					?? new List<InventoryItemLineViewModel>();

			if (model.FromStoreId == model.ToStoreId)
			{
				ModelState.AddModelError(
					string.Empty,
					"ظ„ط§ ظٹظ…ظƒظ† ط§ظ„طھط­ظˆظٹظ„ ط¥ظ„ظ‰ ظ†ظپط³ ط§ظ„ظ…ط®ط²ظ†.");

				await LoadTransferDataAsync(model);

				return View(model);
			}

			if (!ModelState.IsValid ||
				model.Items.Count == 0)
			{
				ModelState.AddModelError(
					string.Empty,
					"ظٹط¬ط¨ ط¥ط¯ط®ط§ظ„ ط¨ظ†ط¯ ظˆط§ط­ط¯ ط¹ظ„ظ‰ ط§ظ„ط£ظ‚ظ„.");

				await LoadTransferDataAsync(model);

				return View(model);
			}

			var storesExist =
				await _context.Stores
					.CountAsync(x =>
						x.IsActive &&
						(x.Id == model.FromStoreId ||
						 x.Id == model.ToStoreId));

			if (storesExist != 2)
			{
				ModelState.AddModelError(
					string.Empty,
					"ط£ط­ط¯ ط§ظ„ظ…ط®ط§ط²ظ† ط؛ظٹط± ظ…ظˆط¬ظˆط¯ ط£ظˆ ط؛ظٹط± ظ†ط´ط·.");

				await LoadTransferDataAsync(model);

				return View(model);
			}

			// âœ… طھط­ظ‚ظ‚ ظ…ظ† طµظ„ط§ط­ظٹط© ط§ظ„ظˆطµظˆظ„ ظ„ظ„ظ…ط®ط²ظ†ظٹظ†
			if (!await _employeeScopeService
					.CanAccessStoreAsync(model.FromStoreId))
			{
				ModelState.AddModelError(
					string.Empty,
					"ظ„ظٹط³ ظ„ط¯ظٹظƒ طµظ„ط§ط­ظٹط© ط¹ظ„ظ‰ ط§ظ„ظ…ط®ط²ظ† ط§ظ„ظ…طµط¯ط±.");

				await LoadTransferDataAsync(model);

				return View(model);
			}

			if (!await _employeeScopeService
					.CanAccessStoreAsync(model.ToStoreId))
			{
				ModelState.AddModelError(
					string.Empty,
					"ظ„ظٹط³ ظ„ط¯ظٹظƒ طµظ„ط§ط­ظٹط© ط¹ظ„ظ‰ ط§ظ„ظ…ط®ط²ظ† ط§ظ„ظˆط¬ظ‡ط©.");

				await LoadTransferDataAsync(model);

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
					"ظٹظˆط¬ط¯ ظ…ظ†طھط¬ ط؛ظٹط± ظ…ظˆط¬ظˆط¯ ط£ظˆ ط؛ظٹط± ظ†ط´ط·.");

				await LoadTransferDataAsync(model);

				return View(model);
			}

			foreach (var item in model.Items)
			{
				if (item.Quantity <= 0)
				{
					ModelState.AddModelError(
						string.Empty,
						"ط§ظ„ظƒظ…ظٹط© ظٹط¬ط¨ ط£ظ† طھظƒظˆظ† ط£ظƒط¨ط± ظ…ظ† طµظپط±.");

					await LoadTransferDataAsync(model);

					return View(model);
				}

				var available =
					await _context.StockLots
						.Where(x =>
							x.ProductId == item.ProductId &&
							x.StoreId == model.FromStoreId &&
							x.IsActive &&
							x.QuantityRemaining > 0)
						.SumAsync(x => x.QuantityRemaining);

				if (available < item.Quantity)
				{
					ModelState.AddModelError(
						string.Empty,
						$"ط§ظ„ظƒظ…ظٹط© ط§ظ„ظ…ط·ظ„ظˆط¨ط© ط£ظƒط¨ط± ظ…ظ† ط§ظ„ظ…طھط§ط­ ظپظٹ ط§ظ„ظ…ط®ط²ظ† ط§ظ„ظ…طµط¯ط± ظ„ظ„ظ…ظ†طھط¬.");

					await LoadTransferDataAsync(model);

					return View(model);
				}
			}

			// =========================================
			// ط¥ظ†ط´ط§ط، ط§ظ„ط­ط±ظƒط©
			// =========================================

			var transaction =
				new StockTransaction
				{
					TransactionNumber =
						await GenerateTransactionNumberAsync(
							StockTransactionType.Transfer),
					Type = StockTransactionType.Transfer,
					FromStoreId = model.FromStoreId,
					ToStoreId = model.ToStoreId,
					CreatedByUserId = CurrentUserId(),
					Notes = model.Notes,
					CreatedAt = DateTime.UtcNow
				};

			_context.StockTransactions.Add(transaction);

			foreach (var item in model.Items)
			{
				var (consumed, cost) =
					await ConsumeFromLotsAsync(
						model.FromStoreId,
						item.ProductId,
						item.Quantity);

				var avgCost =
					consumed > 0
						? cost / consumed
						: 0;

				var sourceLot =
					await _context.StockLots
						.Where(x =>
							x.ProductId == item.ProductId &&
							x.StoreId == model.FromStoreId &&
							x.IsActive)
						.OrderByDescending(x => x.CreatedAt)
						.FirstOrDefaultAsync();

				_context.StockLots.Add(
					new StockLot
					{
						StoreId = model.ToStoreId,
						ProductId = item.ProductId,
						SupplierId = sourceLot?.SupplierId,
						PurchaseInvoiceItemId = null,
						QuantityReceived = consumed,
						QuantityRemaining = consumed,
						UnitCost = avgCost,
						PurchaseDate = DateTime.UtcNow,
						CreatedAt = DateTime.UtcNow,
						IsActive = true
					});
			}

			var strategy =
				_context.Database.CreateExecutionStrategy();

			string? transactionError = null;

			await strategy.ExecuteAsync(
				async () =>
				{
					await using var dbTransaction =
						await _context.Database.BeginTransactionAsync();

					try
					{
						await _context.SaveChangesAsync();
						await dbTransaction.CommitAsync();
					}
					catch (DbUpdateConcurrencyException)
					{
						await dbTransaction.RollbackAsync();
						transactionError =
							"طھظ… طھط¹ط¯ظٹظ„ ط§ظ„ظ…ط®ط²ظˆظ† ظ…ظ† ظ…ط³طھط®ط¯ظ… ط¢ط®ط± ط£ط«ظ†ط§ط، ط§ظ„ط­ظپط¸. ط£ط¹ط¯ ط§ظ„ظ…ط­ط§ظˆظ„ط©.";
					}
				});

			if (transactionError != null)
			{
				TempData["Error"] = transactionError;
				return RedirectToAction(nameof(Index));
			}

			TempData["Success"] =
				$"طھظ… طھط­ظˆظٹظ„ ط§ظ„ظ…ط®ط²ظˆظ† ط¨ظٹظ† ط§ظ„ظ…ط®ط§ط²ظ† ط¨ظ†ط¬ط§ط­ - ظ…ط³طھظ†ط¯ {transaction.TransactionNumber}.";

			return RedirectToAction(nameof(Index));
		}

		// =========================================
		// طھط³ظˆظٹط© ظ…ط®ط²ظˆظ† - GET
		// =========================================

		[HttpGet]
		[RequirePermission("inventory.adjust")]
		public async Task<IActionResult> Adjust()
		{
			var model =
				new InventoryAdjustViewModel();

			await LoadAdjustDataAsync(model);

			return View(model);
		}

		// =========================================
		// طھط³ظˆظٹط© ظ…ط®ط²ظˆظ† - POST
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("inventory.adjust")]
		public async Task<IActionResult> Adjust(
			InventoryAdjustViewModel model)
		{
			model.Reason =
				model.Reason?.Trim() ?? string.Empty;

			if (!ModelState.IsValid)
			{
				await LoadAdjustDataAsync(model);

				return View(model);
			}

			var storeExists =
				await _context.Stores
					.AnyAsync(x =>
						x.Id == model.StoreId &&
						x.IsActive);

			if (!storeExists)
			{
				ModelState.AddModelError(
					nameof(model.StoreId),
					"ط§ظ„ظ…ط®ط²ظ† ط؛ظٹط± ظ…ظˆط¬ظˆط¯ ط£ظˆ ط؛ظٹط± ظ†ط´ط·.");

				await LoadAdjustDataAsync(model);

				return View(model);
			}

			// âœ… طھط­ظ‚ظ‚ ظ…ظ† طµظ„ط§ط­ظٹط© ط§ظ„ظˆطµظˆظ„ ظ„ظ„ظ…ط®ط²ظ†
			if (!await _employeeScopeService
					.CanAccessStoreAsync(model.StoreId))
			{
				ModelState.AddModelError(
					nameof(model.StoreId),
					"ظ„ظٹط³ ظ„ط¯ظٹظƒ طµظ„ط§ط­ظٹط© ط¹ظ„ظ‰ ظ‡ط°ط§ ط§ظ„ظ…ط®ط²ظ†.");

				await LoadAdjustDataAsync(model);

				return View(model);
			}

			var productExists =
				await _context.Products
					.AnyAsync(x =>
						x.Id == model.ProductId &&
						x.IsActive);

			if (!productExists)
			{
				ModelState.AddModelError(
					nameof(model.ProductId),
					"ط§ظ„ظ…ظ†طھط¬ ط؛ظٹط± ظ…ظˆط¬ظˆط¯ ط£ظˆ ط؛ظٹط± ظ†ط´ط·.");

				await LoadAdjustDataAsync(model);

				return View(model);
			}

			// =========================================
			// ط­ط³ط§ط¨ ط§ظ„ظƒظ…ظٹط© ط§ظ„ط­ط§ظ„ظٹط© ظˆط³ظ„ظˆظƒ ط§ظ„طھط¹ط¯ظٹظ„
			// =========================================

			var currentQuantity =
				await _context.StockLots
					.Where(x =>
						x.ProductId == model.ProductId &&
						x.StoreId == model.StoreId &&
						x.IsActive)
					.SumAsync(x => x.QuantityRemaining);

			var delta =
				model.NewQuantity - currentQuantity;

			if (Math.Abs(delta) < 0.0000001m)
			{
				TempData["Success"] =
					"ظ„ط§ ظٹظˆط¬ط¯ طھط؛ظٹظٹط± ظپظٹ ط§ظ„ط±طµظٹط¯ (ط§ظ„ظƒظ…ظٹط© ط§ظ„ط¬ط¯ظٹط¯ط© طھط·ط§ط¨ظ‚ ط§ظ„ط­ط§ظ„ظٹط©).";

				return RedirectToAction(nameof(Index));
			}

			var transaction =
				new StockTransaction
				{
					TransactionNumber =
						await GenerateTransactionNumberAsync(
							StockTransactionType.Adjust),
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
				// ط²ظٹط§ط¯ط© ط±طµظٹط¯
				var lotCost =
					await AwaitLotCostForAddAsync(
						model.ProductId,
						model.StoreId);

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
						StockLot = lot,
						Notes = model.Reason
					});
			}
			else
			{
				// ظ†ظ‚طµ ط±طµظٹط¯ (ط§ط³طھظ‡ظ„ط§ظƒ FIFO)
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
					ModelState.AddModelError(
						string.Empty,
						"ظ„ط§ ظٹظ…ظƒظ† طھظ‚ظ„ظٹظ„ ط§ظ„ط±طµظٹط¯ ط¨ظ‡ط°ط§ ط§ظ„ظ…ظ‚ط¯ط§ط±ط› ط§ظ„ظƒظ…ظٹط© ط§ظ„ظ…طھط§ط­ط© ط£ظ‚ظ„.");

					await LoadAdjustDataAsync(model);

					return View(model);
				}

				var (consumed, cost) =
					await ConsumeFromLotsAsync(
						model.StoreId,
						model.ProductId,
						needQty);

				transaction.Items.Add(
					new StockTransactionItem
					{
						ProductId = model.ProductId,
						Quantity = consumed,
						UnitCost = consumed > 0
							? cost / consumed
							: 0,
						StockLotId = null,
						Notes = model.Reason
					});
			}

			var strategy =
				_context.Database.CreateExecutionStrategy();

			string? transactionError = null;

			await strategy.ExecuteAsync(
				async () =>
				{
					await using var dbTransaction =
						await _context.Database.BeginTransactionAsync();

					try
					{
						await _context.SaveChangesAsync();
						await dbTransaction.CommitAsync();
					}
					catch (DbUpdateConcurrencyException)
					{
						await dbTransaction.RollbackAsync();
						transactionError =
							"طھظ… طھط¹ط¯ظٹظ„ ط§ظ„ظ…ط®ط²ظˆظ† ظ…ظ† ظ…ط³طھط®ط¯ظ… ط¢ط®ط± ط£ط«ظ†ط§ط، ط§ظ„ط­ظپط¸. ط£ط¹ط¯ ط§ظ„ظ…ط­ط§ظˆظ„ط©.";
					}
				});

			if (transactionError != null)
			{
				TempData["Error"] = transactionError;
				return RedirectToAction(nameof(Index));
			}

			TempData["Success"] =
				$"طھظ…طھ طھط³ظˆظٹط© ط±طµظٹط¯ ط§ظ„ظ…ظ†طھط¬ ط¨ظ†ط¬ط§ط­ - ظ…ط³طھظ†ط¯ {transaction.TransactionNumber}.";

			return RedirectToAction(nameof(Index));
		}

		// =========================================
		// ط¬ط±ط¯ ظ…ط®ط²ظ† - GET
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

			if (storeId.HasValue &&
				storeId.Value > 0)
			{
				// âœ… طھط­ظ‚ظ‚ ظ…ظ† طµظ„ط§ط­ظٹط© ط§ظ„ظˆطµظˆظ„ ظ„ظ„ظ…ط®ط²ظ†
				if (!await _employeeScopeService
						.CanAccessStoreAsync(storeId.Value))
				{
					return RedirectToAction("AccessDenied", "Account");
				}

				var rows =
					await _context.StockLots
						.AsNoTracking()
						.Include(x => x.Product)
						.Where(x =>
							x.IsActive &&
							x.StoreId == storeId.Value)
						.GroupBy(x => x.ProductId)
						.ToListAsync();

				foreach (var group in rows)
				{
					var first =
						group.First();

					if (first.Product == null)
					{
						continue;
					}

					model.Lines.Add(
						new InventoryCountLineViewModel
						{
							ProductId =
								first.ProductId,
							ProductName =
								first.Product.Name,
							SystemQuantity =
								group.Sum(x => x.QuantityRemaining),
							CountedQuantity =
								group.Sum(x => x.QuantityRemaining)
						});
				}
			}

			return View(model);
		}

		// =========================================
		// ط¬ط±ط¯ ظ…ط®ط²ظ† - POST
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("inventory.count")]
		public async Task<IActionResult> Count(
			InventoryCountViewModel model)
		{
			model.Notes =
				string.IsNullOrWhiteSpace(model.Notes)
					? null
					: model.Notes.Trim();

			var storeExists =
				await _context.Stores
					.AnyAsync(x =>
						x.Id == model.StoreId &&
						x.IsActive);

			if (!storeExists)
			{
				ModelState.AddModelError(
					nameof(model.StoreId),
					"ط§ظ„ظ…ط®ط²ظ† ط؛ظٹط± ظ…ظˆط¬ظˆط¯ ط£ظˆ ط؛ظٹط± ظ†ط´ط·.");

				model.Stores = await LoadStoresAsync();

				return View(model);
			}

			// âœ… طھط­ظ‚ظ‚ ظ…ظ† طµظ„ط§ط­ظٹط© ط§ظ„ظˆطµظˆظ„ ظ„ظ„ظ…ط®ط²ظ†
			if (!await _employeeScopeService
					.CanAccessStoreAsync(model.StoreId))
			{
				ModelState.AddModelError(
					nameof(model.StoreId),
					"ظ„ظٹط³ ظ„ط¯ظٹظƒ طµظ„ط§ط­ظٹط© ط¹ظ„ظ‰ ظ‡ط°ط§ ط§ظ„ظ…ط®ط²ظ†.");

				model.Stores = await LoadStoresAsync();

				return View(model);
			}

			model.Lines ??=
				new List<InventoryCountLineViewModel>();

			model.Lines =
				model.Lines
					.Where(x => x.ProductId > 0)
					.ToList();

			var changed =
				new List<(int ProductId,
					decimal SystemQuantity,
					decimal CountedQuantity)>();

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

				if (Math.Abs(systemQuantity - line.CountedQuantity)
					< 0.0000001m)
				{
					continue;
				}

				if (line.CountedQuantity < 0)
				{
					continue;
				}

				changed.Add(
					(line.ProductId,
						systemQuantity,
						line.CountedQuantity));
			}

			if (changed.Count == 0)
			{
				TempData["Success"] =
					"ط§ظ„ط¬ط±ط¯ ظ…ط·ط§ط¨ظ‚ ظ„ظ„ط±طµظٹط¯ط› ظ„ط§ طھظˆط¬ط¯ ظپط±ظˆظ‚ط§طھ.";

				return RedirectToAction(nameof(Index));
			}

			// =========================================
			// ط¥ظ†ط´ط§ط، ط­ط±ظƒط© ط¬ط±ط¯
			// =========================================

			var transaction =
				new StockTransaction
				{
					TransactionNumber =
						await GenerateTransactionNumberAsync(
							StockTransactionType.Count),
					Type = StockTransactionType.Count,
					FromStoreId = model.StoreId,
					ToStoreId = null,
					CreatedByUserId = CurrentUserId(),
					Notes = model.Notes,
					CreatedAt = DateTime.UtcNow
				};

			_context.StockTransactions.Add(transaction);

			foreach (var (productId,
				systemQuantity,
				countedQuantity) in changed)
			{
				var delta =
					countedQuantity - systemQuantity;

				if (delta > 0)
				{
					var lotCost =
						await AwaitLotCostForAddAsync(
							productId,
							model.StoreId);

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
							StockLot = lot,
							Notes = "ظپط±ظ‚ ط¬ط±ط¯ ظ…ظˆط¬ط¨"
						});
				}
				else
				{
					var (consumed, cost) =
						await ConsumeFromLotsAsync(
							model.StoreId,
							productId,
							-delta);

					transaction.Items.Add(
						new StockTransactionItem
						{
							ProductId = productId,
							Quantity = consumed,
							UnitCost = consumed > 0
								? cost / consumed
								: 0,
							StockLotId = null,
							Notes = "ظپط±ظ‚ ط¬ط±ط¯ ط³ط§ظ„ط¨"
						});
				}
			}

			var strategy =
				_context.Database.CreateExecutionStrategy();

			string? transactionError = null;

			await strategy.ExecuteAsync(
				async () =>
				{
					await using var dbTransaction =
						await _context.Database.BeginTransactionAsync();

					try
					{
						await _context.SaveChangesAsync();
						await dbTransaction.CommitAsync();
					}
					catch (DbUpdateConcurrencyException)
					{
						await dbTransaction.RollbackAsync();
						transactionError =
							"طھظ… طھط¹ط¯ظٹظ„ ط§ظ„ظ…ط®ط²ظˆظ† ظ…ظ† ظ…ط³طھط®ط¯ظ… ط¢ط®ط± ط£ط«ظ†ط§ط، ط§ظ„ط­ظپط¸. ط£ط¹ط¯ ط§ظ„ظ…ط­ط§ظˆظ„ط©.";
					}
				});

			if (transactionError != null)
			{
				TempData["Error"] = transactionError;
				return RedirectToAction(nameof(Index));
			}

			TempData["Success"] =
				$"طھظ… طھظ†ظپظٹط° ط§ظ„ط¬ط±ط¯ ط¨ظ†ط¬ط§ط­ - ظ…ط³طھظ†ط¯ {transaction.TransactionNumber}.";

			return RedirectToAction(nameof(Index));
		}

		// =========================================
		// ط§ظ„ظƒظ…ظٹط© ط§ظ„ظ…طھط§ط­ط© ظ„ظ…ظ†طھط¬ ظپظٹ ظ…ط®ط²ظ† (AJAX)
		// =========================================

		[HttpGet]
		[RequirePermission("inventory.view")]
		public async Task<IActionResult> GetProductQuantity(
			int productId,
			int storeId)
		{
			if (productId <= 0 || storeId <= 0)
			{
				return Json(new
				{
					quantity = 0,
					value = 0m
				});
			}

			// âœ… طھط­ظ‚ظ‚ ظ…ظ† طµظ„ط§ط­ظٹط© ط§ظ„ظˆطµظˆظ„ ظ„ظ„ظ…ط®ط²ظ†
			if (!await _employeeScopeService
					.CanAccessStoreAsync(storeId))
			{
				return Json(new
				{
					quantity = 0,
					value = 0m
				});
			}

			var lots =
				await _context.StockLots
					.AsNoTracking()
					.Where(x =>
						x.ProductId == productId &&
						x.StoreId == storeId &&
						x.IsActive)
					.ToListAsync();

			var quantity =
				lots.Sum(x => x.QuantityRemaining);

			var value =
				lots.Sum(
					x => x.QuantityRemaining * x.UnitCost);

			return Json(new
			{
				quantity = quantity,
				value = value
			});
		}

		// =========================================
		// ط³ط¬ظ„ ط§ظ„ط­ط±ظƒط§طھ
		// =========================================

		[HttpGet]
		// =========================================
		// ظƒط´ظپ ط­ط±ظƒط© ظ…ظ†طھط¬ (ط¯ط®ظˆظ„/ط®ط±ظˆط¬)
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
					new List<(DateTime Date,
						string Doc,
						string Source,
						string TypeLabel,
						string TypeBadge,
						string Dir,
						bool? Inc,
						string? Store,
						string? Counter,
						decimal Qty,
						decimal Cost,
						string? Notes)>();

				var transactions =
					await _context.StockTransactions
						.AsNoTracking()
						.Include(x => x.FromStore)
						.Include(x => x.ToStore)
						.Include(x => x.Items)
						.Where(x =>
							x.Items.Any(i =>
								i.ProductId == productId.Value))
						.OrderBy(x => x.CreatedAt)
						.ThenBy(x => x.Id)
						.ToListAsync();

				int selectedStore = storeId.GetValueOrDefault();

				// -------------------------------------
				// 1) ط­ط±ظƒط§طھ ط§ظ„ظ…ط®ط²ظˆظ† ط§ظ„ظٹط¯ظˆظٹط©
				// -------------------------------------

				foreach (var transaction in transactions)
				{
					foreach (var item in
						transaction.Items.Where(i =>
							i.ProductId == productId.Value))
					{
						var subjectNotes =
							string.IsNullOrWhiteSpace(item.Notes)
								? transaction.Notes
								: item.Notes;

						var flows =
							new List<(
								string dir,
								bool inc,
								string? store,
								string? counter,
								int? storeId)>();

						switch (transaction.Type)
						{
							case StockTransactionType.Receive:
								flows.Add(
									("ط¯ط®ظˆظ„", true,
										transaction.ToStore?.Name,
										transaction.FromStore?.Name,
										transaction.ToStoreId));
								break;

							case StockTransactionType.Issue:
								flows.Add(
									("ط®ط±ظˆط¬", false,
										transaction.FromStore?.Name,
										transaction.ToStore?.Name,
										transaction.FromStoreId));
								break;

							case StockTransactionType.Transfer:
								flows.Add(
									("ط®ط±ظˆط¬", false,
										transaction.FromStore?.Name,
										transaction.ToStore?.Name,
										transaction.FromStoreId));
								flows.Add(
									("ط¯ط®ظˆظ„", true,
										transaction.ToStore?.Name,
										transaction.FromStore?.Name,
										transaction.ToStoreId));
								break;

							case StockTransactionType.Adjust:
							case StockTransactionType.Count:
								if (subjectNotes?
										.Contains("ظپط±ظ‚ ط¬ط±ط¯ ظ…ظˆط¬ط¨") == true)
								{
									flows.Add(
										("ط¯ط®ظˆظ„", true,
											transaction.FromStore?.Name,
											null,
											transaction.FromStoreId));
								}
								else if (subjectNotes?
											.Contains("ظپط±ظ‚ ط¬ط±ط¯ ط³ط§ظ„ط¨") == true)
								{
									flows.Add(
										("ط®ط±ظˆط¬", false,
											transaction.FromStore?.Name,
											null,
											transaction.FromStoreId));
								}
								else
								{
									flows.Add(
										("ط؛ظٹط± ظ…ط­ط¯ط¯", false,
											transaction.FromStore?.Name,
											null,
											transaction.FromStoreId));
								}
								break;
						}

						foreach (var flow in flows)
						{
							if (selectedStore != 0 &&
								flow.storeId != selectedStore)
							{
								continue;
							}

							events.Add(
								(transaction.CreatedAt,
									transaction.TransactionNumber,
									"ظ…ط®ط²ظˆظ† ظٹط¯ظˆظٹ",
									transaction.Type switch
									{
										StockTransactionType.Receive => "ط§ط³طھظ„ط§ظ…",
										StockTransactionType.Issue => "طµط±ظپ",
										StockTransactionType.Transfer => "طھط­ظˆظٹظ„",
										StockTransactionType.Adjust => "طھط³ظˆظٹط©",
										StockTransactionType.Count => "ط¬ط±ط¯",
										_ => "ط­ط±ظƒط©"
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
									flow.dir == "ط؛ظٹط± ظ…ط­ط¯ط¯"
										? (bool?)null
										: flow.inc,
									flow.store,
									flow.counter,
									item.Quantity,
									item.UnitCost,
									subjectNotes));
						}
					}
				}

				// -------------------------------------
				// 2) ط¯ط®ظˆظ„ ط§ظ„ظ…ط´طھط±ظٹط§طھ ط¹ط¨ط± ط§ظ„ط¯ظڈظپظژط¹
				// (ط¯ظپط¹ظژط§طھ ظ…ط±طھط¨ط·ط© ط¨ط¨ظ†ط¯ ط´ط±ط§ط، ظپط¹ظ„ظٹ)
				// -------------------------------------

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
					if (selectedStore != 0 &&
						lot.StoreId != selectedStore)
					{
						continue;
					}

					var purchaseDoc =
						lot.PurchaseInvoiceItem?
							.PurchaseInvoice?.InvoiceNumber ??
						$"ط¯ظپط¹ط© {lot.Id}";

					events.Add(
						(lot.CreatedAt,
							purchaseDoc,
							"ط´ط±ط§ط،",
							"ط´ط±ط§ط،",
							"bg-success",
							"ط¯ط®ظˆظ„",
							true,
							lot.Store?.Name,
							lot.Supplier?.Name,
							lot.QuantityReceived,
							lot.UnitCost,
							$"ط§ط³طھظ„ط§ظ… ط´ط±ط§ط، - ط¯ظپط¹ط© {lot.Id}"));
				}

				// -------------------------------------
				// 3) ط®ط±ظˆط¬ ط§ظ„ظ…ط¨ظٹط¹ط§طھ ط¹ط¨ط± طھط®طµظٹطµط§طھ ط§ظ„ط¯ظڈظپظژط¹
				// -------------------------------------

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
							x.SalesInvoiceItem.ProductId ==
								productId.Value &&
							x.SalesInvoiceItem.SalesInvoice.Status !=
								SalesInvoiceStatus.Cancelled)
						.ToListAsync();

				foreach (var alloc in saleAllocations)
				{
					var lot = alloc.StockLot;
					var invoice =
						alloc.SalesInvoiceItem?.SalesInvoice;

					if (selectedStore != 0 &&
						lot?.StoreId != selectedStore)
					{
						continue;
					}

					events.Add(
						(invoice?.InvoiceDate ??
							DateTime.UtcNow,
							invoice?.InvoiceNumber ??
								$"ط¨ظٹط¹ {alloc.Id}",
							"ط¨ظٹط¹",
							"ظپط§طھظˆط±ط© ط¨ظٹط¹",
							"bg-danger",
							"ط®ط±ظˆط¬",
							false,
							lot?.Store?.Name,
							invoice?.Customer?.Name,
							alloc.QuantityBaseUnit,
							alloc.UnitCost,
							invoice?.Notes));
				}

				// -------------------------------------
				// ط§ظ„ط±طµظٹط¯ ط§ظ„ظ…ط±ط­ظ‘ظ„ ظ‚ط¨ظ„ طھط§ط±ظٹط® ط§ظ„ط¨ط¯ط§ظٹط©
				// -------------------------------------

				decimal openingBalance = 0;

				if (fromDate != null)
				{
					foreach (var e in events
						.Where(x => x.Date < fromDate.Value)
						.OrderBy(x => x.Date)
						.ThenBy(x => x.Doc))
					{
						if (e.Inc == true)
						{
							openingBalance += e.Qty;
						}
						else if (e.Inc == false)
						{
							openingBalance -= e.Qty;
						}
					}
				}

				// -------------------------------------
				// ط­ط±ظƒط§طھ ط§ظ„ظپطھط±ط© + ط§ظ„ط±طµظٹط¯ ط§ظ„طھط±ط§ظƒظ…ظٹ
				// -------------------------------------

				var visible = events
					.Where(x =>
						fromDate == null ||
						x.Date >= fromDate.Value)
					.Where(x =>
						toDate == null ||
						x.Date <= toDate.Value.AddDays(1))
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

			// âœ… ظپظ„طھط±ط© ط§ظ„ظ…ط®ط§ط²ظ† ط§ظ„ظ…ط³ظ…ظˆط­ط©
			if (scope.IsRestricted)
			{
				var allowedStores = scope.StoreIds.ToList();

				query = allowedStores.Count == 0
					? query.Where(_ => false)
					: query.Where(x =>
						(x.FromStoreId.HasValue &&
						 allowedStores.Contains(x.FromStoreId.Value)) ||
						(x.ToStoreId.HasValue &&
						 allowedStores.Contains(x.ToStoreId.Value)));
			}

			if (storeId.HasValue &&
				storeId.Value > 0)
			{
				query =
					query.Where(x =>
						x.FromStoreId == storeId.Value ||
						x.ToStoreId == storeId.Value);
			}

			if (type.HasValue)
			{
				query =
					query.Where(x => x.Type == type.Value);
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
						TransactionNumber =
							transaction.TransactionNumber,
						Type = transaction.Type,
						FromStoreName =
							transaction.FromStore?.Name,
						ToStoreName =
							transaction.ToStore?.Name,
						CreatedByUserName =
							transaction.CreatedByUser?.Phone,
						CreatedAt = transaction.CreatedAt,
						ItemCount =
							transaction.Items.Count,
						Notes = transaction.Notes,
						Items =
							transaction.Items
								.Select(x =>
									new InventoryTransactionLineRowViewModel
									{
										ProductName =
											x.Product?.Name ?? "-",
										Quantity = x.Quantity,
										UnitCost = x.UnitCost
									})
								.ToList()
					});
			}

			return View(model);
		}

		// =========================================
		// ط§ط³طھظ‡ظ„ط§ظƒ ط¯ظپط¹ط§طھ (FIFO)
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

				var take =
					Math.Min(lot.QuantityRemaining, need);

				lot.QuantityRemaining -= take;

				totalCost += take * lot.UnitCost;

				consumed += take;

				need -= take;
			}

			return (consumed, totalCost);
		}

		// =========================================
		// طھظƒظ„ظپط© ظ„ظ„ط¯ظپط¹ط© ط§ظ„ط¬ط¯ظٹط¯ط© (ط²ظٹط§ط¯ط© ط±طµظٹط¯)
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
					.SumAsync(x =>
						(decimal?)(x.QuantityRemaining *
							x.UnitCost));

			var totalQuantity =
				await _context.StockLots
					.Where(x =>
						x.ProductId == productId &&
						x.StoreId == storeId &&
						x.IsActive &&
						x.QuantityRemaining > 0)
					.SumAsync(x =>
						(decimal?)x.QuantityRemaining) ?? 0;

			if (totalQuantity > 0)
			{
				return (avgCost ?? 0) / totalQuantity;
			}

			var lastLot =
				await _context.StockLots
					.AsNoTracking()
					.Where(x =>
						x.ProductId == productId &&
						x.IsActive)
					.OrderByDescending(x => x.CreatedAt)
					.FirstOrDefaultAsync();

			return lastLot?.UnitCost ?? 0;
		}

		// =========================================
		// ط±ظ‚ظ… ظ…ط³طھظ†ط¯ طھظ„ظ‚ط§ط¦ظٹ
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

			var datePart =
				DateTime.Today.ToString("yyyyMMdd");

			var searchPrefix =
				$"{prefix}-{datePart}-";

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

				if (parts.Length == 3 &&
					int.TryParse(parts[2], out var last))
				{
					nextSeq = last + 1;
				}
			}

			return
				$"{searchPrefix}{nextSeq:0000}";
		}

		// =========================================
		// ط§ظ„ظ…ط³طھط®ط¯ظ… ط§ظ„ط­ط§ظ„ظٹ
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

		// =========================================
		// طھط­ظ…ظٹظ„ ظ‚ظˆط§ط¦ظ… ط§ظ„ط´ط§ط´ط§طھ (ظ…ظپظ„طھط±ط© ط¨ط§ظ„ظ€Scope)
		// =========================================

		private async Task<List<KeyValuePair<int, string>>>
			LoadStoresAsync()
		{
			var scope = await _employeeScopeService.GetScopeAsync();

			var query = _context.Stores
				.AsNoTracking()
				.Where(x => x.IsActive);

			query = _employeeScopeService
				.ApplyStoreFilter(query, scope);

			return await query
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

		private async Task<List<InventoryProductOptionViewModel>>
			LoadProductsAsync()
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

		private async Task LoadReceiveDataAsync(
			InventoryReceiveViewModel model)
		{
			model.Stores = await LoadStoresAsync();
			model.Suppliers = await LoadSuppliersAsync();
			model.Products = await LoadProductsAsync();
		}

		private async Task LoadIssueDataAsync(
			InventoryIssueViewModel model)
		{
			model.Stores = await LoadStoresAsync();
			model.Products = await LoadProductsAsync();
		}

		private async Task LoadTransferDataAsync(
			InventoryTransferViewModel model)
		{
			model.Stores = await LoadStoresAsync();
			model.Products = await LoadProductsAsync();
		}

		private async Task LoadAdjustDataAsync(
			InventoryAdjustViewModel model)
		{
			model.Stores = await LoadStoresAsync();
			model.Products = await LoadProductsAsync();
		}
	}
}
