using AccountingSystem.Data;
using AccountingSystem.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AccountingSystem.Tests.Integration;

/// <summary>
/// Helper لاختبارات الموردين — بيمنع تكرار الكود
/// </summary>
public static class SupplierTestHelpers
{
	/// <summary>
	/// زرع مورد واحد في قاعدة البيانات — ترجّع الـ ID
	/// </summary>
	public static async Task<int> SeedSupplierAsync(
		CustomWebApplicationFactory factory,
		string name = "مورد اختبار",
		string? phone = null,
		bool isActive = true)
	{
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		phone ??= $"0100{Random.Shared.Next(1000000, 9999999)}";

		var supplier = new Supplier
		{
			Name = name,
			Phone = phone,
			IsActive = isActive,
			CreatedAt = DateTime.UtcNow
		};

		db.Suppliers.Add(supplier);
		await db.SaveChangesAsync();

		return supplier.Id;
	}

	/// <summary>
	/// إنشاء مورد عبر HTTP — ترجّع الـ supplier ID
	/// </summary>
	public static async Task<int> CreateSupplierViaHttpAsync(
		CustomWebApplicationFactory factory,
		HttpClient client,
		string name = "مورد HTTP",
		string? phone = null,
		string? email = null,
		string? address = null,
		decimal openingBalance = 0,
		int creditDays = 0)
	{
		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/Supplier/Create");

		phone ??= $"0100{Random.Shared.Next(1000000, 9999999)}";

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["Name"] = name,
			["Phone"] = phone,
			["Email"] = email ?? string.Empty,
			["Address"] = address ?? string.Empty,
			["OpeningBalance"] = openingBalance.ToString(
				System.Globalization.CultureInfo.InvariantCulture),
			["CreditDays"] = creditDays.ToString(),
			["IsActive"] = "true",
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/Supplier/Create", form);

		if (response.StatusCode != System.Net.HttpStatusCode.Redirect &&
			response.StatusCode != System.Net.HttpStatusCode.Found &&
			response.StatusCode != System.Net.HttpStatusCode.SeeOther)
		{
			var body = await response.Content.ReadAsStringAsync();
			throw new InvalidOperationException(
				$"CreateSupplierViaHttpAsync failed. " +
				$"Status={response.StatusCode}. " +
				$"Body preview: {body.Substring(0, Math.Min(500, body.Length))}");
		}

		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var supplier = await db.Suppliers
			.FirstOrDefaultAsync(x => x.Phone == phone);

		if (supplier == null)
		{
			throw new InvalidOperationException(
				$"Supplier with phone {phone} was not persisted.");
		}

		return supplier.Id;
	}
}