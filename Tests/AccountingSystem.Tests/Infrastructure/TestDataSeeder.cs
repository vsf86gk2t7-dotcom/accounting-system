using AccountingSystem.Data;
using AccountingSystem.Models;

namespace AccountingSystem.Tests.Infrastructure;

/// <summary>
/// بيبذر البيانات الأساسية لكل اختبار:
/// - Chart of Accounts (الحسابات النظامية)
/// - Fiscal Period (فترة مالية مفتوحة)
/// - Admin Role + Permissions
/// - Store + Company + Branch
/// </summary>
public static class TestDataSeeder
{
	public static async Task SeedBasicAsync(ApplicationDbContext context)
	{
		// =========================================
		// Fiscal Period مفتوحة
		// =========================================

		if (!context.FiscalPeriods.Any())
		{
			context.FiscalPeriods.Add(new FiscalPeriod
			{
				PeriodName = DateTime.UtcNow.Year.ToString(),
				StartDate = new DateTime(DateTime.UtcNow.Year, 1, 1),
				EndDate = new DateTime(DateTime.UtcNow.Year, 12, 31),
				IsClosed = false,
				CreatedAt = DateTime.UtcNow
			});
		}

		// =========================================
		// Chart of Accounts الأساسية
		// =========================================

		if (!context.ChartAccounts.Any())
		{
			context.ChartAccounts.AddRange(
				new ChartAccount
				{
					Code = "1",
					Name = "الأصول",
					Type = AccountType.Asset,
					IsSystem = true,
					IsActive = true,
					CreatedAt = DateTime.UtcNow
				},
				new ChartAccount
				{
					Code = "1101",
					Name = "الصندوق",
					Type = AccountType.Asset,
					IsSystem = true,
					IsActive = true,
					CreatedAt = DateTime.UtcNow
				},
				new ChartAccount
				{
					Code = "1103",
					Name = "العملاء",
					Type = AccountType.Asset,
					IsSystem = true,
					IsActive = true,
					CreatedAt = DateTime.UtcNow
				},
				new ChartAccount
				{
					Code = "1104",
					Name = "المخزون",
					Type = AccountType.Asset,
					IsSystem = true,
					IsActive = true,
					CreatedAt = DateTime.UtcNow
				},
				new ChartAccount
				{
					Code = "1105",
					Name = "ض.ق.م مشتريات",
					Type = AccountType.Asset,
					IsSystem = true,
					IsActive = true,
					CreatedAt = DateTime.UtcNow
				},
				new ChartAccount
				{
					Code = "2101",
					Name = "ض.ق.م المبيعات",
					Type = AccountType.Liability,
					IsSystem = true,
					IsActive = true,
					CreatedAt = DateTime.UtcNow
				},
				new ChartAccount
				{
					Code = "2102",
					Name = "الموردون",
					Type = AccountType.Liability,
					IsSystem = true,
					IsActive = true,
					CreatedAt = DateTime.UtcNow
				},
				new ChartAccount
				{
					Code = "4001",
					Name = "المبيعات",
					Type = AccountType.Revenue,
					IsSystem = true,
					IsActive = true,
					CreatedAt = DateTime.UtcNow
				},
				new ChartAccount
				{
					Code = "5001",
					Name = "تكلفة البضاعة المباعة",
					Type = AccountType.Expense,
					IsSystem = true,
					IsActive = true,
					CreatedAt = DateTime.UtcNow
				}
			);
		}

		await context.SaveChangesAsync();
	}

	public static async Task<Store> SeedStoreAsync(ApplicationDbContext context)
	{
		var company = new Company
		{
			Name = "Test Company",
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};

		context.Companies.Add(company);
		await context.SaveChangesAsync();

		var branch = new Branch
		{
			Name = "Test Branch",
			CompanyId = company.Id,
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};

		context.Branches.Add(branch);
		await context.SaveChangesAsync();

		var store = new Store
		{
			Name = "Test Store",
			BranchId = branch.Id,
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};

		context.Stores.Add(store);
		await context.SaveChangesAsync();

		return store;
	}

	public static async Task<Product> SeedProductAsync(
		ApplicationDbContext context,
		string name = "Test Product",
		string code = "TP001")
	{
		var unit = new Unit
		{
			Name = "قطعة",
			ShortName = "ق",
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};

		context.Units.Add(unit);
		await context.SaveChangesAsync();

		var product = new Product
		{
			Name = name,
			Code = code,
			Barcode = Guid.NewGuid().ToString("N").Substring(0, 13),
			BaseUnitId = unit.Id,
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};

		context.Products.Add(product);
		await context.SaveChangesAsync();

		return product;
	}
}