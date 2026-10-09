using AccountingSystem.Data;
using AccountingSystem.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AccountingSystem.Tests.Integration;

/// <summary>
/// Helper مشترك لاختبارات Branch و Store
/// </summary>
public static class BranchStoreTestHelpers
{
	// =========================================
	// Company
	// =========================================

	public static async Task<int> SeedCompanyAsync(
		CustomWebApplicationFactory factory,
		string? name = null)
	{
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		name ??= $"شركة اختبار {Guid.NewGuid():N}".Substring(0, 20);

		var company = new Company
		{
			Name = name,
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};

		db.Companies.Add(company);
		await db.SaveChangesAsync();

		return company.Id;
	}

	// =========================================
	// Branch
	// =========================================

	public static async Task<int> SeedBranchAsync(
		CustomWebApplicationFactory factory,
		int? companyId = null,
		string? name = null,
		bool isActive = true)
	{
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		if (companyId == null)
		{
			// ✅ جرّب استخدم شركة موجودة الأول (عشان Controller بياخد FirstOrDefault)
			var existingCompany = await db.Companies.FirstOrDefaultAsync();

			if (existingCompany == null)
			{
				// مفيش شركة → أنشئ واحدة
				existingCompany = new Company
				{
					Name = $"شركة {Guid.NewGuid():N}".Substring(0, 20),
					IsActive = true,
					CreatedAt = DateTime.UtcNow
				};
				db.Companies.Add(existingCompany);
				await db.SaveChangesAsync();
			}

			companyId = existingCompany.Id;
		}

		name ??= $"فرع {Guid.NewGuid():N}".Substring(0, 20);

		var branch = new Branch
		{
			CompanyId = companyId.Value,
			Name = name,
			IsActive = isActive,
			CreatedAt = DateTime.UtcNow
		};

		db.Branches.Add(branch);
		await db.SaveChangesAsync();

		return branch.Id;
	}

	public static async Task<int> CreateBranchViaHttpAsync(
		CustomWebApplicationFactory factory,
		HttpClient client,
		string? name = null,
		string? phone = null,
		string? address = null)
	{
		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/Branch/Create");

		name ??= $"فرع HTTP {Guid.NewGuid():N}".Substring(0, 20);

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["Name"] = name,
			["Phone"] = phone ?? string.Empty,
			["Address"] = address ?? string.Empty,
			["IsActive"] = "true",
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/Branch/Create", form);

		if (response.StatusCode != System.Net.HttpStatusCode.Redirect &&
			response.StatusCode != System.Net.HttpStatusCode.Found &&
			response.StatusCode != System.Net.HttpStatusCode.SeeOther)
		{
			var body = await response.Content.ReadAsStringAsync();
			throw new InvalidOperationException(
				$"CreateBranchViaHttpAsync failed. " +
				$"Status={response.StatusCode}. " +
				$"Body preview: {body.Substring(0, Math.Min(500, body.Length))}");
		}

		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var branch = await db.Branches
			.FirstOrDefaultAsync(x => x.Name == name);

		if (branch == null)
		{
			throw new InvalidOperationException(
				$"Branch with name {name} was not persisted.");
		}

		return branch.Id;
	}

	// =========================================
	// Store
	// =========================================

	public static async Task<int> SeedStoreAsync(
		CustomWebApplicationFactory factory,
		int? branchId = null,
		string? name = null,
		bool isActive = true)
	{
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		if (branchId == null)
		{
			// ✅ جرّب استخدم شركة موجودة الأول
			var existingCompany = await db.Companies.FirstOrDefaultAsync();

			if (existingCompany == null)
			{
				existingCompany = new Company
				{
					Name = $"شركة {Guid.NewGuid():N}".Substring(0, 20),
					IsActive = true,
					CreatedAt = DateTime.UtcNow
				};
				db.Companies.Add(existingCompany);
				await db.SaveChangesAsync();
			}

			// ✅ جرّب استخدم فرع موجود في نفس الشركة
			var existingBranch = await db.Branches
				.FirstOrDefaultAsync(x => x.CompanyId == existingCompany.Id);

			if (existingBranch == null)
			{
				existingBranch = new Branch
				{
					CompanyId = existingCompany.Id,
					Name = $"فرع {Guid.NewGuid():N}".Substring(0, 20),
					IsActive = true,
					CreatedAt = DateTime.UtcNow
				};
				db.Branches.Add(existingBranch);
				await db.SaveChangesAsync();
			}

			branchId = existingBranch.Id;
		}

		name ??= $"مخزن {Guid.NewGuid():N}".Substring(0, 20);

		var store = new Store
		{
			BranchId = branchId.Value,
			Name = name,
			IsActive = isActive,
			CreatedAt = DateTime.UtcNow
		};

		db.Stores.Add(store);
		await db.SaveChangesAsync();

		return store.Id;
	}

	public static async Task<int> CreateStoreViaHttpAsync(
		CustomWebApplicationFactory factory,
		HttpClient client,
		int branchId,
		string? name = null,
		string? code = null)
	{
		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/Store/Create");

		name ??= $"مخزن HTTP {Guid.NewGuid():N}".Substring(0, 20);

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["Name"] = name,
			["Code"] = code ?? string.Empty,
			["BranchId"] = branchId.ToString(),
			["IsActive"] = "true",
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/Store/Create", form);

		if (response.StatusCode != System.Net.HttpStatusCode.Redirect &&
			response.StatusCode != System.Net.HttpStatusCode.Found &&
			response.StatusCode != System.Net.HttpStatusCode.SeeOther)
		{
			var body = await response.Content.ReadAsStringAsync();
			throw new InvalidOperationException(
				$"CreateStoreViaHttpAsync failed. " +
				$"Status={response.StatusCode}. " +
				$"Body preview: {body.Substring(0, Math.Min(500, body.Length))}");
		}

		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		// ✅ ابحث بـ Name + BranchId (عشان نفس الاسم مسموح في فروع مختلفة)
		var store = await db.Stores
			.FirstOrDefaultAsync(x =>
				x.Name == name &&
				x.BranchId == branchId);

		if (store == null)
		{
			throw new InvalidOperationException(
				$"Store with name {name} in branch {branchId} was not persisted.");
		}

		return store.Id;
	}
}