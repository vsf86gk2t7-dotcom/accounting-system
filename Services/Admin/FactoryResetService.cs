using AccountingSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Services.Admin
{
	public class FactoryResetService
	{
		private readonly ApplicationDbContext _db;
		private readonly ILogger<FactoryResetService> _logger;

		public FactoryResetService(ApplicationDbContext db,
								   ILogger<FactoryResetService> logger)
		{
			_db = db;
			_logger = logger;
		}

		public async Task FactoryResetAsync(string adminUserId)
		{
			await using var tx = await _db.Database.BeginTransactionAsync();

			try
			{
				// ==========================================================
				// المرحلة 1 — الأوراق التي لا يعتمد عليها أي جدول
				// ==========================================================
				await _db.Database.ExecuteSqlRawAsync(@"
                    DELETE FROM Attendances;
                    DELETE FROM EmployeeDeductions;
                    DELETE FROM EmployeeBranches;
                    DELETE FROM EmployeeStores;
                    DELETE FROM LeaveRequests;
                    DELETE FROM SalesInvoiceItemLots;
                    DELETE FROM SalesReturnItemLots;
                    DELETE FROM ShippingBills;
                    DELETE FROM StockTransactionItems;
                    DELETE FROM UserPermissions;
                    DELETE FROM ProductCategories;
                    DELETE FROM ProductUnits;
                    DELETE FROM AppNotifications;
                    DELETE FROM PasswordResetRequests;
                    DELETE FROM PaymentReminders;
                    DELETE FROM PurchaseReturnInvoiceItems;
                    DELETE FROM TreasuryTransactions;
                    DELETE FROM SupplierProductPackings;
                    DELETE FROM EmployeeAdvances;
                    DELETE FROM JournalEntryLines;
                ");

				// ==========================================================
				// المرحلة 2
				// ==========================================================
				await _db.Database.ExecuteSqlRawAsync(@"
                    DELETE FROM PayrollItems;
                    DELETE FROM SalesReturnInvoiceItems;
                    DELETE FROM StockTransactions;
                    DELETE FROM PurchaseReturnInvoices;
                ");

				// ==========================================================
				// المرحلة 3
				// ==========================================================
				await _db.Database.ExecuteSqlRawAsync(@"
                    DELETE FROM PayrollRuns;
                    DELETE FROM SalesInvoiceItems;
                    DELETE FROM SalesReturnInvoices;
                ");

				// ==========================================================
				// المرحلة 4 — StockLots قبل PurchaseInvoiceItems
				// ==========================================================
				await _db.Database.ExecuteSqlRawAsync(@"
                    DELETE FROM StockLots;
                    DELETE FROM PurchaseInvoiceItems;
                    DELETE FROM SalesInvoices;
                    DELETE FROM JournalEntries;
                ");

				// ==========================================================
				// المرحلة 5
				// ==========================================================
				await _db.Database.ExecuteSqlRawAsync(@"
                    DELETE FROM Products;
                    DELETE FROM PurchaseInvoices;
                ");

				// ==========================================================
				// المرحلة 6 — Units بعد Products (Products.BaseUnitId → Units)
				// ==========================================================
				await _db.Database.ExecuteSqlRawAsync(@"
                    DELETE FROM Categories;
                    DELETE FROM Units;
                ");

				// ==========================================================
				// المرحلة 7 — المستخدمون (نستثني الأدمن الحالي)
				// ==========================================================
				await _db.Database.ExecuteSqlRawAsync(@"
                    UPDATE Users
                    SET CustomerId = NULL,
                        SupplierId = NULL,
                        EmployeeId = NULL
                    WHERE Id = {0};

                    DELETE FROM Users WHERE Id <> {0};
                ", adminUserId);

				// ==========================================================
				// المرحلة 8
				// ==========================================================
				await _db.Database.ExecuteSqlRawAsync(@"
                    DELETE FROM Customers;
                    DELETE FROM Suppliers;
                ");

				// ==========================================================
				// المرحلة 9 — SalesRepProfiles قبل Employees
				// ==========================================================
				await _db.Database.ExecuteSqlRawAsync(@"
                    DELETE FROM SalesRepProfiles;
                    DELETE FROM Employees;
                ");

				// ==========================================================
				// المرحلة 10
				// ==========================================================
				await _db.Database.ExecuteSqlRawAsync(@"
                    DELETE FROM Departments;
                    DELETE FROM Positions;
                    DELETE FROM CashAccounts;
                    DELETE FROM Stores;
                ");

				// ==========================================================
				// المرحلة 11 — كسر المرجع الذاتي في ChartAccounts
				// ==========================================================
				await _db.Database.ExecuteSqlRawAsync(@"
                    UPDATE ChartAccounts SET ParentId = NULL;
                    DELETE FROM ChartAccounts;
                ");

				// ==========================================================
				// المرحلة 12 — ShippingCompanies بعد ShippingBills
				// ==========================================================
				await _db.Database.ExecuteSqlRawAsync(@"
                    DELETE FROM Branches;
                    DELETE FROM ShippingCompanies;
                ");

				// ==========================================================
				// المرحلة 13 — آخر مرحلة
				// ==========================================================
				await _db.Database.ExecuteSqlRawAsync(@"
                    DELETE FROM Companies;
                    DELETE FROM AuditLogs;
                    DELETE FROM FiscalPeriods;
                    DELETE FROM SequenceCounters;
                ");

				await tx.CommitAsync();

				_logger.LogWarning(
					"Factory Reset executed. Admin={AdminId}, Time={Time}",
					adminUserId, DateTime.UtcNow);
			}
			catch (Exception ex)
			{
				await tx.RollbackAsync();
				_logger.LogError(ex, "Factory Reset failed");
				throw;
			}
		}
	}
}