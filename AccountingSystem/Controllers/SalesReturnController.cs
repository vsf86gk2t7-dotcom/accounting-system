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
		// ��� ������� �����
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
		// ����� ����� - GET
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
						"�������� ��� ������ �� ��� ������.";

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
		// ����� ����� - POST
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
			// ������ �������
			// =====================================

			if (!model.OriginalSalesInvoiceId.HasValue ||
				model.OriginalSalesInvoiceId.Value <= 0)
			{
				ModelState.AddModelError(
					nameof(model.OriginalSalesInvoiceId),
					"��� ������ ������ ����� �������.");
			}

			model.Items = model.Items
				.Where(x => x.Quantity > 0)
				.ToList();

			if (model.Items.Count == 0)
			{
				ModelState.AddModelError(
					string.Empty,
					"��� ����� ��� ���� ��� ����� ����� ���� �� ���.");
			}

			if (model.RefundToCash &&
				(!model.RefundCashAccountId.HasValue ||
				 model.RefundCashAccountId.Value <= 0))
			{
				ModelState.AddModelError(
					nameof(model.RefundCashAccountId),
					"��� ������ ���� ���� ����.");
			}

			if (!ModelState.IsValid)
			{
				await ReloadForErrorAsync(model);
				return View(model);
			}

			// =====================================
			// �������� �������
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
					"�������� ������� ��� ������ �� ��� ������.");

				await ReloadForErrorAsync(model);
				return View(model);
			}

			// ? ������ �� ������ ������
			var scope = await _employeeScopeService.GetScopeAsync();

			if (!scope.CanAccessStore(originalInvoice.StoreId))
			{
				TempData["Error"] =
					"��� ���� ������ ��� ���� ��� ��������.";

				return RedirectToAction(nameof(Index));
			}

			// =====================================
			// ���� �������
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
					"�������� ������� ��� ��� ���� � �� ���� ����� �����.";

				return RedirectToAction(nameof(Index));
			}

			decimal totalReturn = 0;

			// =====================================
			// ������ ������
			// =====================================

			decimal totalCostOfGoodsSold = 0;

			foreach (var itemModel in model.Items)
			{
				var saleItem = originalInvoice.Items
					.FirstOrDefault(x =>
						x.Id == itemModel.SalesInvoiceItemId);

				if (saleItem == null)
				{
					ModelState.AddModelError(
						string.Empty,
						"��� ��� ���� �� �������� �������.");

					await ReloadForErrorAsync(model);
					return View(model);
				}

				// ������� ��������� ������ ���� �����
				var alreadyReturned =
					await _context.SalesReturnInvoiceItems
						.Where(x =>
							x.SalesInvoiceItemId == saleItem.Id)
						.SumAsync(x =>
							(decimal?)x.QuantityInBaseUnit) ?? 0;

				var returnable =
					saleItem.QuantityInBaseUnit - alreadyReturned;

				var requested =
					itemModel.Quantity * saleItem.ConversionFactor;

				if (requested > returnable + 0.0001m)
				{
					ModelState.AddModelError(
						string.Empty,
						$"������ �������� ����� ���� �� ������ �������. " +
						$"������: {returnable} � �������: {requested}");

					await ReloadForErrorAsync(model);
					return View(model);
				}

				// =================================
				// ����� ��� �������
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
				// ����� ������ ��� ���Lots ������� (LIFO)
				// =================================

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

					var fromLot = Math.Min(
						alloc.QuantityBaseUnit,
						remaining);

					var lot = await _context.StockLots
						.FirstOrDefaultAsync(x =>
							x.Id == alloc.StockLotId);

					if (lot == null)
					{
						ModelState.AddModelError(
							string.Empty,
							"���� ����� ��� ������ �� �������.");

						await ReloadForErrorAsync(model);
						return View(model);
					}

					// ? ����� ������ ���Lot ������
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
					totalCostOfGoodsSold += weightedCost;
					remaining -= fromLot;
				}

				if (remaining > 0.0001m)
				{
					ModelState.AddModelError(
						string.Empty,
						"���� ����� ������ ������� ��� ������� �������.");

					await ReloadForErrorAsync(model);
					return View(model);
				}

				returnItem.UnitCost = requested > 0
					? weightedCost / requested
					: 0;

				totalReturn += returnItem.Total;

				returnInvoice.Items.Add(returnItem);
			}

			// =====================================
			// ����������
			// =====================================

			returnInvoice.TotalAmount = totalReturn;
			returnInvoice.RefundAmount =
				model.RefundToCash ? totalReturn : 0;

			_context.SalesReturnInvoices.Add(returnInvoice);

			// =====================================
			// ? ����� �� ���� �� �������
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
						Reason = "�� ���� � ����� ���",
						ReferenceDocument =
							returnInvoice.InvoiceNumber,
						AutoJournal = false,
						CreatedByUserId = CurrentUserId(),
						CreatedAt = DateTime.UtcNow
					});
			}

			// =====================================
			// ����� + ������� + ������ �������
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
							await _postingService.PostSalesReturnAsync(
								returnInvoice,
								totalCostOfGoodsSold,
								CurrentUserId());

						if (!postingResult.Success)
						{
							await transaction.RollbackAsync();
							transactionError =
								"��� ����� ��� ����� �����: " +
								(postingResult.Error ?? "unknown");
							return;
						}

						await transaction.CommitAsync();
					}
					catch (DbUpdateConcurrencyException)
					{
						await transaction.RollbackAsync();
						transactionError =
							"�� ����� ������� �� ������ ��� ����� �����. ��� ��������.";
					}
					catch (Exception ex)
					{
						await transaction.RollbackAsync();
						transactionError =
							"��� �� ����� �������: " + ex.Message;
					}
				});

			if (transactionError != null)
			{
				TempData["Error"] = transactionError;
				return RedirectToAction(nameof(Index));
			}

			TempData["Success"] =
				$"�� ����� ����� ����� {returnInvoice.InvoiceNumber} ������ ������� ������� �������.";

			return RedirectToAction(nameof(Index));
		}

		// =========================================
		// ������ �����
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
		// AJAX � ������ ������
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
		// ����� ������
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

			foreach (var item in invoice.Items)
			{
				var alreadyReturned =
					await _context.SalesReturnInvoiceItems
						.Where(x => x.SalesInvoiceItemId == item.Id)
						.SumAsync(x =>
							(decimal?)x.QuantityInBaseUnit) ?? 0;

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
