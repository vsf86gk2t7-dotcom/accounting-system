using AccountingSystem.Data;
using AccountingSystem.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;

namespace AccountingSystem.Tests.Integration;

public class EmployeeCrudIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
	private readonly CustomWebApplicationFactory _factory;

	public EmployeeCrudIntegrationTests(CustomWebApplicationFactory factory)
	{
		_factory = factory;

		IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
			_factory,
			"employee.view",
			"employee.create",
			"employee.edit",
			"employee.activate")
			.GetAwaiter().GetResult();
	}

	
[Fact]
public async Task Index_WithPermission_Returns200()
{
	var client = await IntegrationTestHelpers.CreateAuthenticatedClientAsync(_factory);

	// جرّب 3 مسارات مختلفة
	var homeResp = await client.GetAsync("/Home/Index");
	var accountResp = await client.GetAsync("/Account/Login");
	var employeeResp = await client.GetAsync("/Employee/Index");

	// اقرأ bodies
	var homeBody = await homeResp.Content.ReadAsStringAsync();
	var accountBody = await accountResp.Content.ReadAsStringAsync();
	var employeeBody = await employeeResp.Content.ReadAsStringAsync();

	// اطبع النتايج
	var summary =
		$"HOME: {homeResp.StatusCode} | " +
		$"ACCOUNT: {accountResp.StatusCode} | " +
		$"EMPLOYEE: {employeeResp.StatusCode}\n" +
		$"EMPLOYEE BODY (first 500): " +
		$"{employeeBody.Substring(0, Math.Min(500, employeeBody.Length))}";

	employeeResp.StatusCode.Should().Be(
		HttpStatusCode.OK,
		summary);
}

	[Fact]
	public async Task Create_Get_Returns200()
	{
		var client = await IntegrationTestHelpers.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/Employee/Create");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	[Fact]
	public async Task Create_Post_ValidData_PersistsEmployee()
	{
		var client = await IntegrationTestHelpers.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/Employee/Create");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["Name"] = "Test Employee Integration",
			["Phone"] = "01111111111",
			["JobTitle"] = "Tester",
			["Email"] = "test@example.com",
			["Salary"] = "5000",
			["HireDate"] = DateTime.Today.ToString("yyyy-MM-dd"),
			["EmployeeType"] = "1",
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/Employee/Create", form);

		// المتوقع: redirect بعد النجاح
		response.StatusCode.Should().BeOneOf(
			HttpStatusCode.Redirect,
			HttpStatusCode.Found,
			HttpStatusCode.SeeOther);

		// تحقق إن الموظف اتحفظ فعلاً
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var saved = await db.Employees
			.FirstOrDefaultAsync(x => x.Phone == "01111111111");

		saved.Should().NotBeNull();
		saved!.Name.Should().Be("Test Employee Integration");
	}

[Fact]
public async Task Details_ExistingEmployee_Returns200()
{
	var employeeId = await SeedEmployeeDirectlyAsync();
	var client = await IntegrationTestHelpers.CreateAuthenticatedClientAsync(_factory);
	var response = await client.GetAsync($"/Employee/Details/{employeeId}");

	response.StatusCode.Should().Be(
		HttpStatusCode.OK,
		$"Redirect Location: {response.Headers.Location}");
}

	[Fact]
	public async Task Details_NotExistingEmployee_Returns404()
	{
		var client = await IntegrationTestHelpers.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/Employee/Details/999999");

		response.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task Edit_Get_Returns200()
	{
		var employeeId = await SeedEmployeeDirectlyAsync();

		var client = await IntegrationTestHelpers.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync($"/Employee/Edit/{employeeId}");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	[Fact]
	public async Task Edit_Post_UpdatesEmployee()
	{
		var employeeId = await SeedEmployeeDirectlyAsync();

		var client = await IntegrationTestHelpers.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, $"/Employee/Edit/{employeeId}");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["Id"] = employeeId.ToString(),
			["Name"] = "Updated Name",
			["Phone"] = "02222222222",
			["JobTitle"] = "Senior Tester",
			["Salary"] = "7000",
			["HireDate"] = DateTime.Today.ToString("yyyy-MM-dd"),
			["EmployeeType"] = "1",
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync($"/Employee/Edit/{employeeId}", form);

		response.StatusCode.Should().BeOneOf(
			HttpStatusCode.Redirect,
			HttpStatusCode.Found,
			HttpStatusCode.SeeOther);

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var updated = await db.Employees.FindAsync(employeeId);
		updated!.Name.Should().Be("Updated Name");
	}

	[Fact]
	public async Task ToggleActive_ExistingEmployee_ChangesState()
	{
		var employeeId = await SeedEmployeeDirectlyAsync(isActive: true);

		var client = await IntegrationTestHelpers.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, $"/Employee/Details/{employeeId}");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync($"/Employee/ToggleActive/{employeeId}", form);

		response.StatusCode.Should().BeOneOf(
			HttpStatusCode.Redirect,
			HttpStatusCode.Found,
			HttpStatusCode.SeeOther);

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var updated = await db.Employees.FindAsync(employeeId);
		updated!.IsActive.Should().BeFalse();
	}

	private async Task<int> SeedEmployeeDirectlyAsync(bool isActive = true)
	{
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var employee = new Employee
		{
			Name = "Seeded Employee",
			Phone = $"01{Random.Shared.Next(100000000, 999999999)}",
			JobTitle = "Seeded",
			Salary = 3000,
			IsActive = isActive,
			EmployeeType = EmployeeType.Office,
			CreatedAt = DateTime.UtcNow
		};

		db.Employees.Add(employee);
		await db.SaveChangesAsync();
		return employee.Id;
	}
}