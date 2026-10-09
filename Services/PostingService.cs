using AccountingSystem.Data;
using AccountingSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Services
{
	// =========================================
	// نتائج محاولة ترحيل قيد
	// =========================================

	public sealed class JournalPostingResult
	{
		public bool Success { get; set; }

		public string? Error { get; set; }

		public int? JournalEntryId { get; set; }

		public string? EntryNumber { get; set; }

		public static JournalPostingResult Ok(
			int journalEntryId,
			string entryNumber) =>
				new()
				{
					Success = true,
					JournalEntryId = journalEntryId,
					EntryNumber = entryNumber
				};

		public static JournalPostingResult Fail(string error) =>
			new()
			{
				Success = false,
				Error = error
			};
	}

	// =========================================
	// سطر قيد يُدخل على الخدمة
	// =========================================

	public sealed class JournalLineRequest
	{
		public int ChartAccountId { get; set; }

		public decimal Debit { get; set; }

		public decimal Credit { get; set; }

		public int? CustomerId { get; set; }

		public int? SupplierId { get; set; }

		public int? StoreId { get; set; }

		public string? Description { get; set; }
	}

	// =========================================
	// محرك القيود التلقائية
	// =========================================

	        public interface IPostingService
        {
                Task<JournalPostingResult> PostAsync(
                        JournalSourceType sourceType,
                        int sourceId,
                        DateTime entryDate,
                        string description,
                        IReadOnlyList<JournalLineRequest> lines,
                        bool isPosted,
                        int? userId,
                        JournalEntryKind entryKind = JournalEntryKind.Main);

		Task<JournalPostingResult> PostSalesInvoiceAsync(
			SalesInvoice invoice,
			decimal costOfGoodsSold,
			int? userId,
			int? cashAccountId = null);

		Task<JournalPostingResult> PostSalesInvoiceCancelAsync(
			SalesInvoice invoice,
			decimal costOfGoodsSold,
			int? userId);

		Task<JournalPostingResult> PostPurchaseInvoiceAsync(
			PurchaseInvoice invoice,
			int? userId);

		Task<JournalPostingResult> PostPurchaseInvoiceCancelAsync(
			PurchaseInvoice invoice,
			int? userId);

		Task<JournalPostingResult> PostSalesReturnAsync(
			SalesReturnInvoice invoice,
			decimal costOfGoodsSold,
			int? userId);

		Task<JournalPostingResult> PostPurchaseReturnAsync(
			PurchaseReturnInvoice invoice,
			int? userId);

		Task<JournalPostingResult> PostEmployeeAdvanceAsync(
			EmployeeAdvance advance,
			int? userId);

		Task<JournalPostingResult> PostPayrollAccrualAsync(
			PayrollRun run,
			int? userId);

		Task<JournalPostingResult> PostPayrollPaymentAsync(
			PayrollRun run,
			int cashAccountId,
			int? userId);
	}

	public sealed class PostingService(
		ApplicationDbContext context,
		ISequenceService sequence) : IPostingService
	{
		// =========================================
		// أكواد الحسابات النظامية
		// =========================================

		private const string ACC_INVENTORY = ChartAccountCodes.Inventory;
		private const string ACC_VAT_PURCHASE = ChartAccountCodes.VatPurchase;
		private const string ACC_SALES_TAX_PURCHASE = ChartAccountCodes.SalesTaxPurchase;
		private const string ACC_SUPPLIERS = ChartAccountCodes.Suppliers;
		private const string ACC_VAT_SALES = ChartAccountCodes.VatSales;
		private const string ACC_SALES_TAX_PAYABLE = ChartAccountCodes.SalesTaxPayable;

		// =========================================
		// إنشاء قيد عام مع التحقق
		// =========================================

		                public async Task<JournalPostingResult> PostAsync(
                        JournalSourceType sourceType,
                        int sourceId,
                        DateTime entryDate,
                        string description,
                        IReadOnlyList<JournalLineRequest> lines,
                        bool isPosted,
                        int? userId,
                        JournalEntryKind entryKind = JournalEntryKind.Main)
                {
			if (lines == null || lines.Count < 2)
			{
				return JournalPostingResult.Fail(
					"يجب أن يحتوي القيد على سطرين على الأقل.");
			}

			if (lines.Any(x => x.Debit > 0 && x.Credit > 0))
			{
				return JournalPostingResult.Fail(
					"لا يجوز أن يحتوي سطر واحد على مدين ودائن معًا.");
			}

			if (lines.Any(x => x.Debit <= 0 && x.Credit <= 0))
			{
				return JournalPostingResult.Fail(
					"يوجد سطر بدون قيمة مدين أو دائن.");
			}

			var debit = lines.Sum(x => x.Debit);
			var credit = lines.Sum(x => x.Credit);

			if (Math.Abs(debit - credit) > AppConstants.BalanceTolerance)
			{
				return JournalPostingResult.Fail(
					$"القيد غير متوازن (مدين: {debit:N2} — دائن: {credit:N2}).");
			}

			var activePeriod = await context.FiscalPeriods
				.Where(x => x.StartDate <= entryDate.Date &&
							x.EndDate >= entryDate.Date &&
							!x.IsClosed)
				.FirstOrDefaultAsync();

			if (activePeriod == null)
			{
				return JournalPostingResult.Fail(
					"لا توجد فترة محاسبية مفتوحة لهذا التاريخ.");
			}

			var accountIds = lines.Select(x => x.ChartAccountId).Distinct().ToList();

			var accounts = await context.ChartAccounts
				.AsNoTracking()
				.Where(x => accountIds.Contains(x.Id))
				.ToDictionaryAsync(x => x.Id);

			var parentIds = accounts.Values
				.Where(x => x.ParentId.HasValue)
				.Select(x => x.ParentId!.Value)
				.ToHashSet();

			var activeChildren = await context.ChartAccounts
				.AsNoTracking()
				.Where(x => x.IsActive && accountIds.Contains(x.ParentId ?? 0))
				.Select(x => x.ParentId!.Value)
				.ToHashSetAsync();

			foreach (var line in lines)
			{
				if (!accounts.TryGetValue(line.ChartAccountId, out var account))
				{
					return JournalPostingResult.Fail(
						$"الحساب {line.ChartAccountId} غير موجود.");
				}

				if (!account.IsActive)
				{
					return JournalPostingResult.Fail(
						$"الحساب '{account.Name}' غير نشط.");
				}

				if (activeChildren.Contains(line.ChartAccountId))
				{
					return JournalPostingResult.Fail(
						$"لا يجوز التسجيل على حساب '{account.Name}' لأنه حساب أب.");
				}
			}

			var entry = new JournalEntry
			{
				 EntryNumber = string.Empty,
                                EntryDate = entryDate.Date,
                                Description = description,
                                SourceType = sourceType,
                                SourceId = sourceId,
                                EntryKind = entryKind,
                                IsPosted = isPosted,
                                PostedAt = isPosted ? DateTime.UtcNow : null,
                                CreatedByUserId = userId,
                                CreatedAt = DateTime.UtcNow
			};

			foreach (var line in lines)
			{
				entry.Lines.Add(
					new JournalEntryLine
					{
						ChartAccountId = line.ChartAccountId,
						Debit = Math.Abs(line.Debit),
						Credit = Math.Abs(line.Credit),
						CustomerId = line.CustomerId,
						SupplierId = line.SupplierId,
						StoreId = line.StoreId,
						Description = line.Description
					});
			}

			entry.EntryNumber =
				await sequence.NextFormattedAsync(
					"JRN",
					"JE",
					entryDate.Date,
					4);

			try
			{
				context.JournalEntries.Add(entry);
				await context.SaveChangesAsync();
			}
			catch (DbUpdateException ex)
			{
				var innerMessage = ex.InnerException?.Message ?? ex.Message;
				return JournalPostingResult.Fail(
					$"تعذر حفظ القيد المحاسبي: {innerMessage}");
			}

			return JournalPostingResult.Ok(entry.Id, entry.EntryNumber);
		}

		// =========================================
		// قيد فاتورة البيع
		// =========================================

		public async Task<JournalPostingResult> PostSalesInvoiceAsync(
			SalesInvoice invoice,
			decimal costOfGoodsSold,
			int? userId,
			int? cashAccountId = null)
		{
			if (invoice.Id <= 0)
			{
				return JournalPostingResult.Fail(
					"يجب حفظ الفاتورة أولًا قبل ترحيل قيدها.");
			}

			var salesAccount = await GetOrCreateSalesAccountAsync();

			var grossRevenue = invoice.SubTotal;
			var discountAmount = invoice.DiscountAmount > 0 ? invoice.DiscountAmount : 0;
			var netRevenue = Math.Max(0, grossRevenue - discountAmount);

			decimal salesTaxAmount =
				invoice.SalesTaxAmount > 0 ? invoice.SalesTaxAmount : 0;

			decimal vatAmount =
				invoice.TaxAmount > 0 ? invoice.TaxAmount : 0;

			decimal receivable =
				netRevenue + vatAmount + salesTaxAmount;

			CashAccount? cashAccountForReceipt = null;

			if (invoice.PaymentMethod == SalesPaymentMethod.Credit)
			{
				var customerAccount = await GetCustomerAccountIdAsync();
				var debitLine = new JournalLineRequest
				{
					ChartAccountId = customerAccount,
					Debit = receivable,
					CustomerId = invoice.CustomerId,
					Description = "ذمم عملاء — فاتورة بيع آجلة"
				};
				var revenueLines = new List<JournalLineRequest> { debitLine };

				var result = await PostSalesRevenueAsync(
					invoice, revenueLines, salesAccount.Id, grossRevenue,
					discountAmount, vatAmount, salesTaxAmount, userId);

				if (!result.Success)
					return result;

				if (costOfGoodsSold > 0)
				{
					var cogsResult = await PostCostOfGoodsSoldAsync(
						invoice, costOfGoodsSold, userId,
						"تكلفة فاتورة بيع " + invoice.InvoiceNumber,
						"خروج المخزون");

					if (!cogsResult.Success)
						return cogsResult;
				}

				return JournalPostingResult.Ok(
					result.JournalEntryId!.Value,
					result.EntryNumber!);
			}
			else
			{
				if (cashAccountId.HasValue)
				{
					cashAccountForReceipt =
						await context.CashAccounts
							.FirstOrDefaultAsync(x =>
								x.Id == cashAccountId.Value &&
								x.IsActive);
				}

				if (cashAccountForReceipt == null)
				{
					cashAccountForReceipt =
						await GetOrCreateActiveCashAccountAsync();
				}

				if (cashAccountForReceipt == null)
				{
					return JournalPostingResult.Fail(
						"لا يوجد مركز نقدي (خزنة/بنك) مرتبط بفاتورة البيع.");
				}

				var cashChartId = cashAccountForReceipt.ChartAccountId;

				if (cashChartId == null)
				{
					var cashDefault = await GetAccountAsync(
						ChartAccountCodes.Cash, AccountType.Asset, "الصندوق");
					cashChartId = cashDefault?.Id;
				}

				if (cashChartId == null)
				{
					return JournalPostingResult.Fail(
						"لا يوجد حساب صندوق مرتبط بالمركز النقدي.");
				}

				var debitLine = new JournalLineRequest
				{
					ChartAccountId = cashChartId.Value,
					Debit = receivable,
					Description = "تحصيل مبيعات نقدية"
				};
				var revenueLines = new List<JournalLineRequest> { debitLine };

				var result = await PostSalesRevenueAsync(
					invoice, revenueLines, salesAccount.Id, grossRevenue,
					discountAmount, vatAmount, salesTaxAmount, userId);

				if (!result.Success)
					return result;

				if (cashAccountForReceipt != null)
				{
					var receipt = new TreasuryTransaction
					{
						TransactionNumber =
							await GenerateTreasuryNumberAsync(
								TreasuryTransactionType.Receive),
						Type = TreasuryTransactionType.Receive,
						CashAccountId = cashAccountForReceipt.Id,
						Amount = receivable,
						CustomerId = invoice.CustomerId,
						Reason = "تحصيل فاتورة بيع " + invoice.InvoiceNumber,
						ReferenceDocument = invoice.InvoiceNumber,
						CreatedByUserId = userId,
						CreatedAt = DateTime.UtcNow
					};

					context.TreasuryTransactions.Add(receipt);
					await context.SaveChangesAsync();
				}

				if (costOfGoodsSold > 0)
				{
					var cogsResult = await PostCostOfGoodsSoldAsync(
						invoice, costOfGoodsSold, userId,
						"تكلفة فاتورة بيع " + invoice.InvoiceNumber,
						"خروج المخزون");

					if (!cogsResult.Success)
						return cogsResult;
				}

				return JournalPostingResult.Ok(
					result.JournalEntryId!.Value,
					result.EntryNumber!);
			}
		}

		// =========================================
		// قيد عكسي لإلغاء فاتورة بيع
		// =========================================

		public async Task<JournalPostingResult> PostSalesInvoiceCancelAsync(
			SalesInvoice invoice,
			decimal costOfGoodsSold,
			int? userId)
		{
			if (invoice.Id <= 0)
			{
				return JournalPostingResult.Fail(
					"يجب حفظ الفاتورة أولًا قبل ترحيل قيد الإلغاء.");
			}

			var salesAccount = await GetOrCreateSalesAccountAsync();

			var grossRevenue = invoice.SubTotal;
			var discountAmount = invoice.DiscountAmount > 0 ? invoice.DiscountAmount : 0;
			var netRevenue = Math.Max(0, grossRevenue - discountAmount);

			decimal salesTaxAmount =
				invoice.SalesTaxAmount > 0 ? invoice.SalesTaxAmount : 0;

			decimal vatAmount =
				invoice.TaxAmount > 0 ? invoice.TaxAmount : 0;

			decimal receivable =
				netRevenue + vatAmount + salesTaxAmount;

			var reversalLines = new List<JournalLineRequest>();

			if (invoice.PaymentMethod == SalesPaymentMethod.Credit)
			{
				reversalLines.Add(
					new JournalLineRequest
					{
						ChartAccountId = await GetCustomerAccountIdAsync(),
						Credit = receivable,
						CustomerId = invoice.CustomerId,
						Description = "عكس ذمم العملاء — إلغاء فاتورة بيع"
					});
			}
			else
			{
				var cashChartId = await GetCashAccountChartIdAsync();

				if (cashChartId == null)
				{
					return JournalPostingResult.Fail(
						"لا يوجد مركز نقدي (خزنة/بنك) مرتبط بفاتورة البيع.");
				}

				reversalLines.Add(
					new JournalLineRequest
					{
						ChartAccountId = cashChartId.Value,
						Credit = receivable,
						Description = "إلغاء تحصيل مبيعات نقدية"
					});
			}

			reversalLines.Add(
				new JournalLineRequest
				{
					ChartAccountId = salesAccount.Id,
					Debit = grossRevenue,
					Description = "إلغاء إيراد المبيعات"
				});

			if (discountAmount > 0)
			{
				var discountAccount = await context.ChartAccounts
					.FirstOrDefaultAsync(x =>
						x.Code == ChartAccountCodes.SalesDiscounts &&
						x.Type == AccountType.ContraRevenue &&
						x.IsActive);

				if (discountAccount != null)
				{
					reversalLines.Add(
						new JournalLineRequest
						{
							ChartAccountId = discountAccount.Id,
							Credit = discountAmount,
							Description = "إلغاء خصم مسموح به — فاتورة بيع"
						});
				}
			}

			if (vatAmount > 0)
			{
				var vatAccount = await context.ChartAccounts
					.FirstOrDefaultAsync(x =>
						x.Code == ACC_VAT_SALES &&
						x.Type == AccountType.Liability &&
						x.IsActive);

				if (vatAccount == null)
				{
					return JournalPostingResult.Fail(
						$"حساب ضريبة القيمة المضافة ({ACC_VAT_SALES}) غير موجود.");
				}

				reversalLines.Add(
					new JournalLineRequest
					{
						ChartAccountId = vatAccount.Id,
						Debit = vatAmount,
						Description = "إلغاء ضريبة القيمة المضافة على المبيعات"
					});
			}

			if (salesTaxAmount > 0)
			{
				var salesTaxAccount = await context.ChartAccounts
					.FirstOrDefaultAsync(x =>
						x.Code == ACC_SALES_TAX_PAYABLE &&
						x.Type == AccountType.Liability &&
						x.IsActive);

				if (salesTaxAccount == null)
				{
					return JournalPostingResult.Fail(
						$"حساب ضريبة المبيعات ({ACC_SALES_TAX_PAYABLE}) غير موجود.");
				}

				reversalLines.Add(
					new JournalLineRequest
					{
						ChartAccountId = salesTaxAccount.Id,
						Debit = salesTaxAmount,
						Description = "إلغاء ضريبة المبيعات على الفاتورة"
					});
			}

			var revenueResult = await PostAsync(
				JournalSourceType.SalesCancel,
				invoice.Id,
				invoice.InvoiceDate.Date,
				"قيد إلغاء فاتورة بيع " + invoice.InvoiceNumber,
				reversalLines,
				isPosted: true,
				userId);

			if (!revenueResult.Success)
			{
				return revenueResult;
			}

			if (costOfGoodsSold > 0)
			{
				var cogsResult = await PostCostOfGoodsSoldAsync(
					invoice, costOfGoodsSold, userId,
					"عكس تكلفة فاتورة بيع " + invoice.InvoiceNumber,
					"إعادة المخزون للمخزن");

				if (!cogsResult.Success)
					return cogsResult;
			}

			return JournalPostingResult.Ok(
				revenueResult.JournalEntryId!.Value,
				revenueResult.EntryNumber!);
		}

		// =========================================
		// قيد فاتورة الشراء:
		// - مدين: المخزون (1104) بقيمة البضاعة
		// - مدين: ض.ق.م مشتريات (1105) إن وُجدت
		// - مدين: ضريبة المبيعات (1106) إن وُجدت
		// - دائن: الموردون (2102) بإجمالي مستحق
		// =========================================

		public async Task<JournalPostingResult> PostPurchaseInvoiceAsync(
			PurchaseInvoice invoice,
			int? userId)
		{
			if (invoice.Id <= 0)
			{
				return JournalPostingResult.Fail(
					"يجب حفظ الفاتورة أولًا قبل ترحيل قيدها.");
			}

			if (invoice.StoreId == 0)
			{
				return JournalPostingResult.Fail(
					"الفاتورة بلا مخزن — لا يمكن ترحيل قيدها.");
			}

			var inventoryAccount = await GetAccountAsync(
				ACC_INVENTORY,
				AccountType.Asset,
				"حساب المخزون (1104)");

			if (inventoryAccount == null)
			{
				return JournalPostingResult.Fail(
					$"حساب المخزون ({ACC_INVENTORY}) غير موجود في شجرة الحسابات.");
			}

			var suppliersAccount = await GetAccountAsync(
				ACC_SUPPLIERS,
				AccountType.Liability,
				"حساب الموردون (2102)");

			if (suppliersAccount == null)
			{
				return JournalPostingResult.Fail(
					$"حساب الموردون ({ACC_SUPPLIERS}) غير موجود في شجرة الحسابات.");
			}

			decimal goodsCost =
				Math.Max(0, invoice.SubTotal - invoice.DiscountAmount);

			decimal salesTaxAmount =
				invoice.SalesTaxAmount > 0 ? invoice.SalesTaxAmount : 0;

			decimal vatAmount =
				invoice.TaxAmount > 0 ? invoice.TaxAmount : 0;

			decimal payable =
				goodsCost + salesTaxAmount + vatAmount;

			if (payable <= 0)
			{
				return JournalPostingResult.Fail(
					"الفاتورة بقيمة صفرية — لا يمكن ترحيلها.");
			}

			var lines = new List<JournalLineRequest>
			{
				new()
				{
					ChartAccountId = inventoryAccount.Id,
					Debit = goodsCost,
					StoreId = invoice.StoreId,
					SupplierId = invoice.SupplierId,
					Description = "استلام مخزون — فاتورة شراء"
				}
			};

			if (vatAmount > 0)
			{
				var inputVatAccount = await GetAccountAsync(
					ACC_VAT_PURCHASE,
					AccountType.Asset,
					"ضريبة القيمة المضافة (المشتريات)");

				if (inputVatAccount == null)
				{
					return JournalPostingResult.Fail(
						$"حساب ض.ق.م للمشتريات ({ACC_VAT_PURCHASE}) غير موجود.");
				}

				lines.Add(
					new JournalLineRequest
					{
						ChartAccountId = inputVatAccount.Id,
						Debit = vatAmount,
						SupplierId = invoice.SupplierId,
						StoreId = invoice.StoreId,
						Description = "ضريبة قيمة مضافة (مشتريات)"
					});
			}

			if (salesTaxAmount > 0)
			{
				var salesTaxAccount = await GetAccountAsync(
					ACC_SALES_TAX_PURCHASE,
					AccountType.Asset,
					"ضريبة المبيعات (المشتريات)");

				if (salesTaxAccount == null)
				{
					return JournalPostingResult.Fail(
						$"حساب ضريبة المبيعات ({ACC_SALES_TAX_PURCHASE}) غير موجود.");
				}

				lines.Add(
					new JournalLineRequest
					{
						ChartAccountId = salesTaxAccount.Id,
						Debit = salesTaxAmount,
						SupplierId = invoice.SupplierId,
						StoreId = invoice.StoreId,
						Description = "ضريبة مبيعات (مشتريات)"
					});
			}

			lines.Add(
				new JournalLineRequest
				{
					ChartAccountId = suppliersAccount.Id,
					Credit = payable,
					SupplierId = invoice.SupplierId,
					StoreId = invoice.StoreId,
					Description = "ذمم موردين — فاتورة شراء"
				});

			return await PostAsync(
				JournalSourceType.PurchaseInvoice,
				invoice.Id,
				invoice.InvoiceDate.Date,
				"قيد فاتورة شراء " + invoice.InvoiceNumber,
				lines,
				isPosted: true,
				userId);
		}

		// =========================================
		// قيد عكسي لإلغاء فاتورة شراء مُرحّلة
		// =========================================

		public async Task<JournalPostingResult> PostPurchaseInvoiceCancelAsync(
			PurchaseInvoice invoice,
			int? userId)
		{
			if (invoice.Id <= 0)
			{
				return JournalPostingResult.Fail(
					"يجب حفظ الفاتورة أولًا قبل ترحيل قيد الإلغاء.");
			}

			var inventoryAccount = await GetAccountAsync(
				ACC_INVENTORY,
				AccountType.Asset,
				"حساب المخزون (1104)");

			if (inventoryAccount == null)
			{
				return JournalPostingResult.Fail(
					$"حساب المخزون ({ACC_INVENTORY}) غير موجود.");
			}

			var suppliersAccount = await GetAccountAsync(
				ACC_SUPPLIERS,
				AccountType.Liability,
				"حساب الموردون (2102)");

			if (suppliersAccount == null)
			{
				return JournalPostingResult.Fail(
					$"حساب الموردون ({ACC_SUPPLIERS}) غير موجود.");
			}

			decimal goodsCost =
				Math.Max(0, invoice.SubTotal - invoice.DiscountAmount);

			decimal salesTaxAmount =
				invoice.SalesTaxAmount > 0 ? invoice.SalesTaxAmount : 0;

			decimal vatAmount =
				invoice.TaxAmount > 0 ? invoice.TaxAmount : 0;

			decimal payable =
				goodsCost + salesTaxAmount + vatAmount;

			var lines = new List<JournalLineRequest>
			{
				new()
				{
					ChartAccountId = suppliersAccount.Id,
					Debit = payable,
					SupplierId = invoice.SupplierId,
					StoreId = invoice.StoreId,
					Description = "عكس ذمم الموردين — إلغاء فاتورة شراء"
				},
				new()
				{
					ChartAccountId = inventoryAccount.Id,
					Credit = goodsCost,
					StoreId = invoice.StoreId,
					SupplierId = invoice.SupplierId,
					Description = "إلغاء استلام المخزون"
				}
			};

			if (vatAmount > 0)
			{
				var inputVatAccount = await GetAccountAsync(
					ACC_VAT_PURCHASE,
					AccountType.Asset,
					"ضريبة القيمة المضافة (المشتريات)");

				if (inputVatAccount == null)
				{
					return JournalPostingResult.Fail(
						$"حساب ض.ق.م للمشتريات ({ACC_VAT_PURCHASE}) غير موجود.");
				}

				lines.Add(
					new JournalLineRequest
					{
						ChartAccountId = inputVatAccount.Id,
						Credit = vatAmount,
						SupplierId = invoice.SupplierId,
						StoreId = invoice.StoreId,
						Description = "عكس ض.ق.م للمشتريات"
					});
			}

			if (salesTaxAmount > 0)
			{
				var salesTaxAccount = await GetAccountAsync(
					ACC_SALES_TAX_PURCHASE,
					AccountType.Asset,
					"ضريبة المبيعات (المشتريات)");

				if (salesTaxAccount == null)
				{
					return JournalPostingResult.Fail(
						$"حساب ضريبة المبيعات ({ACC_SALES_TAX_PURCHASE}) غير موجود.");
				}

				lines.Add(
					new JournalLineRequest
					{
						ChartAccountId = salesTaxAccount.Id,
						Credit = salesTaxAmount,
						SupplierId = invoice.SupplierId,
						StoreId = invoice.StoreId,
						Description = "عكس ضريبة المبيعات (مشتريات)"
					});
			}

			return await PostAsync(
				JournalSourceType.PurchaseCancel,
				invoice.Id,
				invoice.InvoiceDate.Date,
				"قيد إلغاء فاتورة شراء " + invoice.InvoiceNumber,
				lines,
				isPosted: true,
				userId);
		}

		// =========================================
		// قيد مرتجع بيع
		// =========================================

		public async Task<JournalPostingResult> PostSalesReturnAsync(
			SalesReturnInvoice invoice,
			decimal costOfGoodsSold,
			int? userId)
		{
			if (invoice.Id <= 0)
			{
				return JournalPostingResult.Fail(
					"يجب حفظ مرتجع البيع أولًا قبل ترحيل قيده.");
			}

			var salesReturnsAccount = await GetAccountAsync(
				ChartAccountCodes.Revenue,
				AccountType.Revenue,
				"حساب مردودات المبيعات (4001)");

			if (salesReturnsAccount == null)
			{
				return JournalPostingResult.Fail(
					"حساب المبيعات (4001) غير موجود في شجرة الحسابات.");
			}

			var lines = new List<JournalLineRequest>();

			decimal netReturn = Math.Max(0, invoice.TotalAmount);

			if (netReturn <= 0)
			{
				return JournalPostingResult.Fail(
					"مرتجع بيع بقيمة صفرية — لا يمكن ترحيل قيده.");
			}

			if (invoice.RefundToCash &&
				invoice.RefundCashAccountId.HasValue)
			{
				var refundCashAccount = await context.CashAccounts
					.FirstOrDefaultAsync(x =>
						x.Id == invoice.RefundCashAccountId.Value &&
						x.IsActive);

				if (refundCashAccount == null ||
					refundCashAccount.ChartAccountId == null)
				{
					return JournalPostingResult.Fail(
						"حساب الرد النقدي غير موجود أو غير مرتبط بحساب خزينة.");
				}

				lines.Add(
					new JournalLineRequest
					{
						ChartAccountId = refundCashAccount.ChartAccountId.Value,
						Credit = netReturn,
						CustomerId = invoice.CustomerId,
						Description = "رد نقدي — مرتجع بيع"
					});
			}
			else
			{
				var customersAccount = await GetAccountAsync(
					ChartAccountCodes.Customers,
					AccountType.Asset,
					"حساب العملاء (1103)");

				if (customersAccount == null)
				{
					return JournalPostingResult.Fail(
						"حساب العملاء (1103) غير موجود.");
				}

				lines.Add(
					new JournalLineRequest
					{
						ChartAccountId = customersAccount.Id,
						Credit = netReturn,
						CustomerId = invoice.CustomerId,
						Description = "ذمم عملاء — مرتجع بيع"
					});
			}

			lines.Add(
				new JournalLineRequest
				{
					ChartAccountId = salesReturnsAccount.Id,
					Debit = netReturn,
					Description = "مردودات مبيعات"
				});

			var revenueResult = await PostAsync(
				JournalSourceType.SalesReturn,
				invoice.Id,
				invoice.ReturnDate.Date,
				"قيد مرتجع بيع " + invoice.InvoiceNumber,
				lines,
				isPosted: true,
				userId);

			if (!revenueResult.Success)
			{
				return revenueResult;
			}

			if (costOfGoodsSold > 0)
			{
				var cogsAccount = await GetAccountAsync(
					ChartAccountCodes.CostOfGoodsSold,
					AccountType.Expense,
					"حساب تكلفة البضاعة المباعة (5001)");

				if (cogsAccount == null)
				{
					return JournalPostingResult.Fail(
						"حساب تكلفة البضاعة المباعة (5001) غير موجود.");
				}

				var inventoryAccount = await GetAccountAsync(
					ACC_INVENTORY,
					AccountType.Asset,
					"حساب المخزون (1104)");

				if (inventoryAccount == null)
				{
					return JournalPostingResult.Fail(
						$"حساب المخزون ({ACC_INVENTORY}) غير موجود.");
				}

				var cogsResult = await PostAsync(
					JournalSourceType.SalesReturn,
					invoice.Id,
					invoice.ReturnDate.Date,
					"عكس تكلفة مرتجع بيع " + invoice.InvoiceNumber,
					new List<JournalLineRequest>
					{
						new()
						{
							ChartAccountId = inventoryAccount.Id,
							Debit = costOfGoodsSold,
							StoreId = invoice.StoreId,
							Description = "إعادة المخزون للدفعات"
						},
						new()
						{
							ChartAccountId = cogsAccount.Id,
							Credit = costOfGoodsSold,
							Description = "عكس تكلفة البضاعة المباعة"
						}
					},
									isPosted: true,
					userId,
					JournalEntryKind.Cogs);

				if (!cogsResult.Success)
				{
					return cogsResult;
				}
			}

			return JournalPostingResult.Ok(
				revenueResult.JournalEntryId!.Value,
				revenueResult.EntryNumber!);
		}

		// =========================================
		// قيد مرتجع شراء
		// =========================================

		public async Task<JournalPostingResult> PostPurchaseReturnAsync(
			PurchaseReturnInvoice invoice,
			int? userId)
		{
			if (invoice.Id <= 0)
			{
				return JournalPostingResult.Fail(
					"يجب حفظ مرتجع الشراء أولًا قبل ترحيل قيده.");
			}

			var inventoryAccount = await GetAccountAsync(
				ACC_INVENTORY,
				AccountType.Asset,
				"حساب المخزون (1104)");

			if (inventoryAccount == null)
			{
				return JournalPostingResult.Fail(
					$"حساب المخزون ({ACC_INVENTORY}) غير موجود.");
			}

			var suppliersAccount = await GetAccountAsync(
				ACC_SUPPLIERS,
				AccountType.Liability,
				"حساب الموردون (2102)");

			if (suppliersAccount == null)
			{
				return JournalPostingResult.Fail(
					$"حساب الموردون ({ACC_SUPPLIERS}) غير موجود.");
			}

			var lines = new List<JournalLineRequest>();

			var goodsCost = Math.Max(0, invoice.TotalAmount);

			if (goodsCost <= 0)
			{
				return JournalPostingResult.Fail(
					"مرتجع شراء بقيمة صفرية — لا يمكن ترحيل قيده.");
			}

			lines.Add(
				new JournalLineRequest
				{
					ChartAccountId = suppliersAccount.Id,
					Debit = goodsCost,
					SupplierId = invoice.SupplierId,
					StoreId = invoice.StoreId,
					Description = "تخفيض ذمم الموردين — مرتجع شراء"
				});

			lines.Add(
				new JournalLineRequest
				{
					ChartAccountId = inventoryAccount.Id,
					Credit = goodsCost,
					StoreId = invoice.StoreId,
					SupplierId = invoice.SupplierId,
					Description = "إخراج المخزون — مرتجع شراء"
				});

			return await PostAsync(
				JournalSourceType.PurchaseReturn,
				invoice.Id,
				invoice.ReturnDate.Date,
				"قيد مرتجع شراء " + invoice.InvoiceNumber,
				lines,
				isPosted: true,
				userId);
		}

		// =========================================
		// Helpers
		// =========================================
		// =========================================
		// قيد صرف سلفة موظف
		// مدين: سلف موظفين (1107)
		// دائن: الصندوق / الخزنة
		// =========================================

		public async Task<JournalPostingResult> PostEmployeeAdvanceAsync(
			EmployeeAdvance advance,
			int? userId)
		{
			if (advance.Id <= 0)
			{
				return JournalPostingResult.Fail(
					"يجب حفظ السلفة أولاً قبل ترحيل قيدها.");
			}

			if (advance.Amount <= 0)
			{
				return JournalPostingResult.Fail(
					"قيمة السلفة يجب أن تكون أكبر من صفر.");
			}

			var advanceAccount = await GetAccountAsync(
				ChartAccountCodes.EmployeeAdvances,
				AccountType.Asset,
				"سلف موظفين");

			if (advanceAccount == null)
			{
				return JournalPostingResult.Fail(
					"حساب سلف الموظفين (1107) غير موجود في شجرة الحسابات.");
			}

			var cashAccount = await context.CashAccounts
				.AsNoTracking()
				.FirstOrDefaultAsync(x => x.Id == advance.CashAccountId);

			if (cashAccount?.ChartAccountId == null)
			{
				return JournalPostingResult.Fail(
					"الخزنة المختارة غير مرتبطة بحساب محاسبي.");
			}

			var lines = new List<JournalLineRequest>
			{
				new()
				{
					ChartAccountId = advanceAccount.Id,
					Debit = advance.Amount,
					Description = "سلفة موظف — " + (advance.Employee?.Name ?? "")
				},
				new()
				{
					ChartAccountId = cashAccount.ChartAccountId.Value,
					Credit = advance.Amount,
					Description = "صرف سلفة نقدية"
				}
			};

			return await PostAsync(
				JournalSourceType.EmployeeAdvance,
				advance.Id,
				advance.Date.Date,
				"قيد صرف سلفة موظف",
				lines,
				isPosted: true,
				userId);
		}

		// =========================================
		// قيد استحقاق الرواتب
		// مدين: مصروف رواتب (5101)
		// دائن: رواتب مستحقة (2105)
		// =========================================

		public async Task<JournalPostingResult> PostPayrollAccrualAsync(
			PayrollRun run,
			int? userId)
		{
			if (run.Id <= 0)
			{
				return JournalPostingResult.Fail(
					"يجب حفظ شهر الرواتب أولاً.");
			}

			if (run.TotalNet <= 0)
			{
				return JournalPostingResult.Fail(
					"لا يمكن ترحيل قيد رواتب بقيمة صفرية.");
			}

			var expenseAccount = await GetAccountAsync(
				ChartAccountCodes.PayrollExpense,
				AccountType.Expense,
				"مصروف رواتب");

			if (expenseAccount == null)
			{
				return JournalPostingResult.Fail(
					"حساب مصروف الرواتب (5101) غير موجود في شجرة الحسابات.");
			}

			var payableAccount = await GetAccountAsync(
				ChartAccountCodes.PayrollPayable,
				AccountType.Liability,
				"رواتب مستحقة");

			if (payableAccount == null)
			{
				return JournalPostingResult.Fail(
					"حساب الرواتب المستحقة (2105) غير موجود في شجرة الحسابات.");
			}

			var lines = new List<JournalLineRequest>
			{
				new()
				{
					ChartAccountId = expenseAccount.Id,
					Debit = run.TotalNet,
					Description = "مصروف رواتب " + run.PeriodName
				},
				new()
				{
					ChartAccountId = payableAccount.Id,
					Credit = run.TotalNet,
					Description = "رواتب مستحقة " + run.PeriodName
				}
			};

			return await PostAsync(
				JournalSourceType.Payroll,
				run.Id,
				run.PeriodEnd.Date,
				"قيد استحقاق رواتب " + run.PeriodName,
				lines,
				isPosted: true,
				userId);
		}

		// =========================================
		// قيد صرف الرواتب
		// مدين: رواتب مستحقة (2105)
		// دائن: الصندوق / البنك
		// =========================================

		public async Task<JournalPostingResult> PostPayrollPaymentAsync(
			PayrollRun run,
			int cashAccountId,
			int? userId)
		{
			if (run.Id <= 0)
			{
				return JournalPostingResult.Fail(
					"يجب حفظ شهر الرواتب أولاً.");
			}

			if (run.TotalNet <= 0)
			{
				return JournalPostingResult.Fail(
					"لا يمكن صرف رواتب بقيمة صفرية.");
			}

			var payableAccount = await GetAccountAsync(
				ChartAccountCodes.PayrollPayable,
				AccountType.Liability,
				"رواتب مستحقة");

			if (payableAccount == null)
			{
				return JournalPostingResult.Fail(
					"حساب الرواتب المستحقة (2105) غير موجود.");
			}

			var cashAccount = await context.CashAccounts
				.AsNoTracking()
				.FirstOrDefaultAsync(x => x.Id == cashAccountId);

			if (cashAccount?.ChartAccountId == null)
			{
				return JournalPostingResult.Fail(
					"الخزنة المختارة غير مرتبطة بحساب محاسبي.");
			}

			var lines = new List<JournalLineRequest>
			{
				new()
				{
					ChartAccountId = payableAccount.Id,
					Debit = run.TotalNet,
					Description = "سداد رواتب مستحقة " + run.PeriodName
				},
				new()
				{
					ChartAccountId = cashAccount.ChartAccountId.Value,
					Credit = run.TotalNet,
					Description = "صرف رواتب من " + cashAccount.Name
				}
			};

			return await PostAsync(
				JournalSourceType.PayrollPayment,
				run.Id,
				DateTime.Today,
				"قيد صرف رواتب " + run.PeriodName,
				lines,
				isPosted: true,
				userId);
		}

		private async Task<int> GetCustomerAccountIdAsync()
		{
			var account = await context.ChartAccounts
				.FirstOrDefaultAsync(x =>
					x.Code == ChartAccountCodes.Customers &&
					x.Type == AccountType.Asset &&
					x.IsActive);

			return account?.Id ?? throw new InvalidOperationException(
				"حساب العملاء (1103) غير موجود في شجرة الحسابات.");
		}

		private async Task<CashAccount?> GetOrCreateActiveCashAccountAsync()
		{
			var cashAccount = await context.CashAccounts
				.Where(x => x.IsActive)
				.OrderBy(x => x.Id)
				.FirstOrDefaultAsync();

			if (cashAccount != null)
			{
				return cashAccount;
			}

			var cashBox = await GetAccountAsync(
				ChartAccountCodes.Cash,
				AccountType.Asset,
				"الصندوق");

			if (cashBox == null)
			{
				return null;
			}

			var created = new CashAccount
			{
				Name = "الصندوق الرئيسي",
				ChartAccountId = cashBox.Id,
				IsActive = true,
				CreatedAt = DateTime.UtcNow
			};

			context.CashAccounts.Add(created);
			await context.SaveChangesAsync();

			return created;
		}

		private async Task<int?> GetCashAccountChartIdAsync()
		{
			var cashAccount = await GetOrCreateActiveCashAccountAsync();

			if (cashAccount == null)
			{
				return null;
			}

			if (cashAccount.ChartAccountId.HasValue)
			{
				return cashAccount.ChartAccountId.Value;
			}

			var cashDefault = await GetAccountAsync(
				ChartAccountCodes.Cash,
				AccountType.Asset,
				"الصندوق");

			return cashDefault?.Id;
		}

		private async Task<string> GenerateTreasuryNumberAsync(
			TreasuryTransactionType type)
		{
			var prefix =
				type switch
				{
					TreasuryTransactionType.Receive => "RCP",
					TreasuryTransactionType.Pay => "PAY",
					TreasuryTransactionType.Transfer => "TRF",
					TreasuryTransactionType.Adjust => "ADJ",
					TreasuryTransactionType.WalletTransfer => "WLT",
					_ => "TRX"
				};

			var datePart = DateTime.Today.ToString("yyyyMMdd");
			var searchPrefix = $"{prefix}-{datePart}-";

			var lastNumber =
				await context.TreasuryTransactions
					.AsNoTracking()
					.Where(x => x.TransactionNumber.StartsWith(searchPrefix))
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

			return $"{searchPrefix}{nextSeq:0000}";
		}

		private async Task<JournalPostingResult> PostSalesRevenueAsync(
			SalesInvoice invoice,
			List<JournalLineRequest> revenueLines,
			int salesAccountId,
			decimal grossRevenue,
			decimal discountAmount,
			decimal vatAmount,
			decimal salesTaxAmount,
			int? userId)
		{
			revenueLines.Add(new JournalLineRequest
			{
				ChartAccountId = salesAccountId,
				Credit = grossRevenue,
				Description = "إيراد مبيعات"
			});

			if (discountAmount > 0)
			{
				var discountAccount = await context.ChartAccounts
					.FirstOrDefaultAsync(x =>
						x.Code == ChartAccountCodes.SalesDiscounts &&
						x.Type == AccountType.ContraRevenue &&
						x.IsActive);

				if (discountAccount == null)
				{
					return JournalPostingResult.Fail(
						$"حساب خصومات المبيعات ({ChartAccountCodes.SalesDiscounts}) غير موجود.");
				}

				revenueLines.Add(new JournalLineRequest
				{
					ChartAccountId = discountAccount.Id,
					Debit = discountAmount,
					Description = "خصم مسموح به — فاتورة بيع"
				});
			}

			if (vatAmount > 0)
			{
				var vatAccount = await context.ChartAccounts
					.FirstOrDefaultAsync(x =>
						x.Code == ACC_VAT_SALES &&
						x.Type == AccountType.Liability &&
						x.IsActive);

				if (vatAccount == null)
				{
					return JournalPostingResult.Fail(
						$"حساب ضريبة القيمة المضافة ({ACC_VAT_SALES}) غير موجود.");
				}

				revenueLines.Add(new JournalLineRequest
				{
					ChartAccountId = vatAccount.Id,
					Credit = vatAmount,
					Description = "ضريبة قيمة مضافة على المبيعات"
				});
			}

			if (salesTaxAmount > 0)
			{
				var salesTaxAccount = await context.ChartAccounts
					.FirstOrDefaultAsync(x =>
						x.Code == ACC_SALES_TAX_PAYABLE &&
						x.Type == AccountType.Liability &&
						x.IsActive);

				if (salesTaxAccount == null)
				{
					return JournalPostingResult.Fail(
						$"حساب ضريبة المبيعات ({ACC_SALES_TAX_PAYABLE}) غير موجود.");
				}

				revenueLines.Add(new JournalLineRequest
				{
					ChartAccountId = salesTaxAccount.Id,
					Credit = salesTaxAmount,
					Description = "ضريبة مبيعات على الفاتورة"
				});
			}

			return await PostAsync(
				JournalSourceType.SalesInvoice,
				invoice.Id,
				invoice.InvoiceDate.Date,
				"قيد فاتورة بيع " + invoice.InvoiceNumber,
				revenueLines,
				isPosted: true,
				userId);
		}

		private async Task<JournalPostingResult> PostCostOfGoodsSoldAsync(
			SalesInvoice invoice,
			decimal costOfGoodsSold,
			int? userId,
			string description,
			string inventoryDescription)
		{
			var cogsAccount = await context.ChartAccounts
				.FirstOrDefaultAsync(x =>
					x.Code == ChartAccountCodes.CostOfGoodsSold &&
					x.Type == AccountType.Expense &&
					x.IsActive);

			if (cogsAccount == null)
			{
				return JournalPostingResult.Fail(
					"حساب تكلفة البضاعة المباعة (5001) غير موجود.");
			}

			var inventoryAccount = await context.ChartAccounts
				.FirstOrDefaultAsync(x =>
					x.Code == ACC_INVENTORY &&
					x.Type == AccountType.Asset &&
					x.IsActive);

			if (inventoryAccount == null)
			{
				return JournalPostingResult.Fail(
					$"حساب المخزون ({ACC_INVENTORY}) غير موجود.");
			}

			if (invoice.StoreId == 0)
			{
				return JournalPostingResult.Fail(
					"الفاتورة بلا مخزن — لا يمكن حساب التكلفة.");
			}

			return await PostAsync(
				JournalSourceType.SalesInvoice,
				invoice.Id,
				invoice.InvoiceDate.Date,
				description,
				new List<JournalLineRequest>
				{
					new()
					{
						ChartAccountId = cogsAccount.Id,
						Debit = costOfGoodsSold,
						Description = "تكلفة البضاعة المباعة"
					},
					new()
					{
						ChartAccountId = inventoryAccount.Id,
						Credit = costOfGoodsSold,
						StoreId = invoice.StoreId,
						Description = inventoryDescription
					}
				},
				isPosted: true,
				userId,
				JournalEntryKind.Cogs);
		}

		private async Task<ChartAccount> GetOrCreateSalesAccountAsync()
		{
			var existing = await context.ChartAccounts
				.AsNoTracking()
				.FirstOrDefaultAsync(x =>
					x.Code == ChartAccountCodes.Revenue &&
					x.Type == AccountType.Revenue &&
					x.IsActive);

			if (existing != null)
			{
				return existing;
			}

			var revenueChart = await context.ChartAccounts
				.AsNoTracking()
				.FirstOrDefaultAsync(x =>
					x.Code == "4" &&
					x.Type == AccountType.Revenue &&
					x.IsActive);

			if (revenueChart == null)
			{
				revenueChart = new ChartAccount
				{
					Code = "4",
					Name = "الإيرادات",
					Type = AccountType.Revenue,
					IsActive = true,
					CreatedAt = DateTime.UtcNow
				};

				context.ChartAccounts.Add(revenueChart);
				await context.SaveChangesAsync();
			}

			var sales = await context.ChartAccounts
				.FirstOrDefaultAsync(x =>
					x.Code == ChartAccountCodes.Revenue &&
					x.Type == AccountType.Revenue);

			if (sales == null)
			{
				sales = new ChartAccount
				{
					Code = ChartAccountCodes.Revenue,
					Name = "المبيعات",
					Type = AccountType.Revenue,
					ParentId = revenueChart.Id,
					IsActive = true,
					CreatedAt = DateTime.UtcNow
				};

				context.ChartAccounts.Add(sales);
				await context.SaveChangesAsync();
			}

			return sales;
		}

		private async Task<ChartAccount?> GetAccountAsync(
			string code,
			AccountType type,
			string defaultDescription)
		{
			var account = await context.ChartAccounts
				.FirstOrDefaultAsync(x =>
					x.Code == code &&
					x.Type == type &&
					x.IsActive);

			if (account != null)
			{
				return account;
			}

			var created = new ChartAccount
			{
				Code = code,
				Name = defaultDescription,
				Type = type,
				IsActive = true
			};

			context.ChartAccounts.Add(created);
			await context.SaveChangesAsync();

			return created;
		}
	}
}