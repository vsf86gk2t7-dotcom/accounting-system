using AccountingSystem.Data;
using AccountingSystem.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AccountingSystem.Tests.Integration;

/// <summary>
/// Helper مشترك لاختبارات Unit و Category
/// </summary>
public static class UnitCategoryTestHelpers
{
	// =========================================
	// Unit
	// =========================================

	public static async Task<int> SeedUnitAsync(
		CustomWebApplicationFactory factory,
		string? name = null,
		string? shortName = null,
		bool isActive = true)
	{
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		name ??= $"وحدة {Guid.NewGuid():N}".Substring(0, 15);

		var unit = new Unit
		{
			Name = name,
			ShortName = shortName,
			IsActive = isActive,
			CreatedAt = DateTime.UtcNow
		};

		db.Units.Add(unit);
		await db.SaveChangesAsync();

		return unit.Id;
	}

	public static async Task<int> CreateUnitViaHttpAsync(
		CustomWebApplicationFactory factory,
		HttpClient client,
		string? name = null,
		string? shortName = null)
	{
		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/Unit/Create");

		name ??= $"وحدة HTTP {Guid.NewGuid():N}".Substring(0, 15);

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["Name"] = name,
			["ShortName"] = shortName ?? string.Empty,
			["IsActive"] = "true",
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/Unit/Create", form);

		if (response.StatusCode != System.Net.HttpStatusCode.Redirect &&
			response.StatusCode != System.Net.HttpStatusCode.Found &&
			response.StatusCode != System.Net.HttpStatusCode.SeeOther)
		{
			var body = await response.Content.ReadAsStringAsync();
			throw new InvalidOperationException(
				$"CreateUnitViaHttpAsync failed. " +
				$"Status={response.StatusCode}. " +
				$"Body preview: {body.Substring(0, Math.Min(500, body.Length))}");
		}

		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var unit = await db.Units
			.FirstOrDefaultAsync(x => x.Name == name);

		if (unit == null)
		{
			throw new InvalidOperationException(
				$"Unit with name {name} was not persisted.");
		}

		return unit.Id;
	}

	// =========================================
	// Category
	// =========================================

	public static async Task<int> SeedCategoryAsync(
		CustomWebApplicationFactory factory,
		string? name = null,
		string? description = null,
		bool isActive = true)
	{
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		name ??= $"تصنيف {Guid.NewGuid():N}".Substring(0, 15);

		var category = new Category
		{
			Name = name,
			Description = description,
			IsActive = isActive,
			CreatedAt = DateTime.UtcNow
		};

		db.Categories.Add(category);
		await db.SaveChangesAsync();

		return category.Id;
	}

	public static async Task<int> CreateCategoryViaHttpAsync(
		CustomWebApplicationFactory factory,
		HttpClient client,
		string? name = null,
		string? description = null)
	{
		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/Category/Create");

		name ??= $"تصنيف HTTP {Guid.NewGuid():N}".Substring(0, 15);

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["Name"] = name,
			["Description"] = description ?? string.Empty,
			["IsActive"] = "true",
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/Category/Create", form);

		if (response.StatusCode != System.Net.HttpStatusCode.Redirect &&
			response.StatusCode != System.Net.HttpStatusCode.Found &&
			response.StatusCode != System.Net.HttpStatusCode.SeeOther)
		{
			var body = await response.Content.ReadAsStringAsync();
			throw new InvalidOperationException(
				$"CreateCategoryViaHttpAsync failed. " +
				$"Status={response.StatusCode}. " +
				$"Body preview: {body.Substring(0, Math.Min(500, body.Length))}");
		}

		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var category = await db.Categories
			.FirstOrDefaultAsync(x => x.Name == name);

		if (category == null)
		{
			throw new InvalidOperationException(
				$"Category with name {name} was not persisted.");
		}

		return category.Id;
	}
}