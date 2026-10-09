using AccountingSystem.Data;
using AccountingSystem.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace AccountingSystem.Tests.Integration;

public class EmployeeDeductionFlowIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
	private readonly CustomWebApplicationFactory _factory;

	public EmployeeDeductionFlowIntegrationTests(CustomWebApplicationFactory factory)
	{
		_factory = factory;

		IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
			_factory,
			"employee.view",
			"employee.edit")
			.GetAwaiter().GetResult();
	}

	// Helper
	private async Task<int> SeedEmployeeAsync()
	{
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var employee = new Employee
		{
			Name = $"موظف خصم {Guid.NewGuid():N}".Substring(0, 20),
			Phone = $"0100{Random.Shared.Next(1000000, 9999999)}",
			Salary = 5000,
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		db.Employees.Add(employee);
		await db.SaveChangesAsync();

		return employee.Id;
	}

	// 1. Index
	[Fact]
	public async Task Index_Returns200()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/EmployeeDeduction/Index");

		response.StatusCode.Should().Be(
			HttpStatusCode.OK,
			$"Location={response.Headers.Location}");
	}

	// 2. Create GET
	[Fact]
	public async Task Create_Get_Returns200()
	{
		await SeedEmployeeAsync();

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/EmployeeDeduction/Create");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	// 3. Create POST — valid
	[Fact]
	public async Task Create_Post_PersistsDeduction()
	{
		var employeeId = await SeedEmployeeAsync();

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/EmployeeDeduction/Create");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["EmployeeId"] = employeeId.ToString(),
			["Amount"] = "250",
			["Reason"] = "خصم تأخير",
			["Date"] = DateTime.Today.ToString("yyyy-MM-dd"),
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/EmployeeDeduction/Create", form);

		response.StatusCode.Should().BeOneOf(
			new[] { HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther },
			$"Status={response.StatusCode}, Location={response.Headers.Location}");

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var deduction = await db.EmployeeDeductions
			.OrderByDescending(x => x.Id)
			.FirstOrDefaultAsync();

		deduction.Should().NotBeNull();
		deduction!.EmployeeId.Should().Be(employeeId);
		deduction.Amount.Should().Be(250);
		deduction.Reason.Should().Be("خصم تأخير");
	}

	// 4. Create POST — Amount = 0
	[Fact]
	public async Task Create_WithZeroAmount_ReturnsViewWithError()
	{
		var employeeId = await SeedEmployeeAsync();

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/EmployeeDeduction/Create");

		var beforeCount = 0;
		using (var scope = _factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			beforeCount = await db.EmployeeDeductions.CountAsync();
		}

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["EmployeeId"] = employeeId.ToString(),
			["Amount"] = "0",
			["Reason"] = "صفر",
			["Date"] = DateTime.Today.ToString("yyyy-MM-dd"),
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/EmployeeDeduction/Create", form);

		response.StatusCode.Should().Be(HttpStatusCode.OK);

		using var scope2 = _factory.Services.CreateScope();
		var db2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var afterCount = await db2.EmployeeDeductions.CountAsync();
		afterCount.Should().Be(beforeCount);
	}

	// 5. Create POST — Reason فاضي
	[Fact]
	public async Task Create_WithEmptyReason_ReturnsViewWithError()
	{
		var employeeId = await SeedEmployeeAsync();

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/EmployeeDeduction/Create");

		var beforeCount = 0;
		using (var scope = _factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			beforeCount = await db.EmployeeDeductions.CountAsync();
		}

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["EmployeeId"] = employeeId.ToString(),
			["Amount"] = "100",
			["Reason"] = "",
			["Date"] = DateTime.Today.ToString("yyyy-MM-dd"),
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/EmployeeDeduction/Create", form);

		response.StatusCode.Should().Be(HttpStatusCode.OK);

		using var scope2 = _factory.Services.CreateScope();
		var db2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var afterCount = await db2.EmployeeDeductions.CountAsync();
		afterCount.Should().Be(beforeCount);
	}

	// 6. Create POST — employee غير موجود
	//
	// ✅ بعد إضافة validation في الـ Controller، الموظف غير الموجود
	// يُرفض مع رسالة خطأ، ولا يُحفظ أي خصم.
	[Fact]
	public async Task Create_WithNonExistingEmployee_ReturnsViewWithError()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/EmployeeDeduction/Create");

		var beforeCount = 0;
		using (var scope = _factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			beforeCount = await db.EmployeeDeductions.CountAsync();
		}

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["EmployeeId"] = "999999",
			["Amount"] = "100",
			["Reason"] = "موظف وهمي",
			["Date"] = DateTime.Today.ToString("yyyy-MM-dd"),
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/EmployeeDeduction/Create", form);

		// ✅ validation يرجّع الـ View مع رسالة خطأ (200)
		response.StatusCode.Should().Be(HttpStatusCode.OK);

		// ✅ لا يُحفظ أي خصم
		using var scope2 = _factory.Services.CreateScope();
		var db2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var afterCount = await db2.EmployeeDeductions.CountAsync();
		afterCount.Should().Be(beforeCount);
	}
}