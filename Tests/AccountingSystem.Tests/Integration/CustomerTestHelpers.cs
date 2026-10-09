using AccountingSystem.Data;
using AccountingSystem.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AccountingSystem.Tests.Integration;

/// <summary>
/// Helper لاختبارات العملاء — بيمنع تكرار الكود
/// </summary>
public static class CustomerTestHelpers
{
	/// <summary>
	/// زرع عميل واحد في قاعدة البيانات — ترجّع الـ ID
	/// </summary>
	public static async Task<int> SeedCustomerAsync(
		CustomWebApplicationFactory factory,
		string name = "عميل اختبار",
		string? phone = null,
		bool isActive = true)
	{
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		phone ??= $"0100{Random.Shared.Next(1000000, 9999999)}";

		var customer = new Customer
		{
			Name = name,
			Phone = phone,
			IsActive = isActive,
			CreatedAt = DateTime.UtcNow
		};

		db.Customers.Add(customer);
		await db.SaveChangesAsync();

		return customer.Id;
	}

	/// <summary>
	/// إنشاء عميل عبر HTTP — ترجّع الـ customer ID
	/// </summary>
	public static async Task<int> CreateCustomerViaHttpAsync(
		CustomWebApplicationFactory factory,
		HttpClient client,
		string name = "عميل HTTP",
		string? phone = null,
		string? email = null,
		string? address = null,
		decimal openingBalance = 0,
		int creditDays = 0)
	{
		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/Customer/Create");

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

		var response = await client.PostAsync("/Customer/Create", form);

		if (response.StatusCode != System.Net.HttpStatusCode.Redirect &&
			response.StatusCode != System.Net.HttpStatusCode.Found &&
			response.StatusCode != System.Net.HttpStatusCode.SeeOther)
		{
			var body = await response.Content.ReadAsStringAsync();
			throw new InvalidOperationException(
				$"CreateCustomerViaHttpAsync failed. " +
				$"Status={response.StatusCode}. " +
				$"Body preview: {body.Substring(0, Math.Min(500, body.Length))}");
		}

		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var customer = await db.Customers
			.FirstOrDefaultAsync(x => x.Phone == phone);

		if (customer == null)
		{
			throw new InvalidOperationException(
				$"Customer with phone {phone} was not persisted.");
		}

		return customer.Id;
	}
}