using AccountingSystem.Data;
using AccountingSystem.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace AccountingSystem.Tests.Integration;

public class EmployeeAdvanceFlowIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
	private readonly CustomWebApplicationFactory _factory;

	public EmployeeAdvanceFlowIntegrationTests(CustomWebApplicationFactory factory)
	{
		_factory = factory;

		IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
			_factory,
			"advance.view",
			"advance.create")
			.GetAwaiter().GetResult();
	}

	// 1. Index
	[Fact]
	public async Task Index_Returns200()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/EmployeeAdvance/Index");

		response.StatusCode.Should().Be(
			HttpStatusCode.OK,
			$"Location={response.Headers.Location}");
	}

	// 2. Create GET
	[Fact]
	public async Task Create_Get_Returns200()
	{
		await EmployeeAdvanceTestHelpers.SeedAdvanceBaseDataAsync(_factory);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/EmployeeAdvance/Create");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	// 3. Create POST — valid
	[Fact]
	public async Task Create_Post_PersistsAdvance()
	{
		var baseData = await EmployeeAdvanceTestHelpers
			.SeedAdvanceBaseDataAsync(_factory);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var amount = 750m;
		var response = await EmployeeAdvanceTestHelpers
			.CreateAdvanceViaHttpAsync(
				_factory, client, baseData,
				amount: amount,
				reason: "سلفة طارئة");

		response.StatusCode.Should().BeOneOf(
			new[] { HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther },
			$"Status={response.StatusCode}, Location={response.Headers.Location}");

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var advance = await db.EmployeeAdvances
			.OrderByDescending(x => x.Id)
			.FirstOrDefaultAsync();

		advance.Should().NotBeNull();
		advance!.EmployeeId.Should().Be(baseData.EmployeeId);
		advance.Amount.Should().Be(amount);
		advance.Reason.Should().Be("سلفة طارئة");
		advance.CashAccountId.Should().Be(baseData.CashAccountId);
		advance.Status.Should().Be(AdvanceStatus.Paid);
	}

	// 4. Create POST — employee غير موجود
	[Fact]
	public async Task Create_WithInvalidEmployee_DoesNotPersist()
	{
		var baseData = await EmployeeAdvanceTestHelpers
			.SeedAdvanceBaseDataAsync(_factory);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var beforeCount = 0;
		using (var scope = _factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			beforeCount = await db.EmployeeAdvances.CountAsync();
		}

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/EmployeeAdvance/Create");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["employeeId"] = "999999",
			["amount"] = "100",
			["date"] = DateTime.Today.ToString("yyyy-MM-dd"),
			["reason"] = "موظف وهمي",
			["cashAccountId"] = baseData.CashAccountId.ToString(),
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/EmployeeAdvance/Create", form);

		response.StatusCode.Should().BeOneOf(
			new[] { HttpStatusCode.OK, HttpStatusCode.Redirect, HttpStatusCode.Found });

		using var scope2 = _factory.Services.CreateScope();
		var db2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var afterCount = await db2.EmployeeAdvances.CountAsync();
		afterCount.Should().Be(beforeCount);
	}

	// 5. Details — غير موجود
	[Fact]
	public async Task Details_NonExisting_ReturnsNotFound()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/EmployeeAdvance/Details/999999");

		response.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	// 6. Details — موجود
	[Fact]
	public async Task Details_ExistingAdvance_Returns200()
	{
		var baseData = await EmployeeAdvanceTestHelpers
			.SeedAdvanceBaseDataAsync(_factory);

		// نزرع سلفة مباشرة
		int advanceId;
		using (var scope = _factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			var advance = new EmployeeAdvance
			{
				EmployeeId = baseData.EmployeeId,
				Amount = 300,
				Date = DateTime.Today,
				Reason = "للتفاصيل",
				CashAccountId = baseData.CashAccountId,
				Status = AdvanceStatus.Paid,
				CreatedAt = DateTime.UtcNow
			};
			db.EmployeeAdvances.Add(advance);
			await db.SaveChangesAsync();
			advanceId = advance.Id;
		}

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync($"/EmployeeAdvance/Details/{advanceId}");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}
}