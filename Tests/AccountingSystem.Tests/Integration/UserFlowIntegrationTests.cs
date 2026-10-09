using AccountingSystem.Data;
using AccountingSystem.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace AccountingSystem.Tests.Integration;

public class UserFlowIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
	private readonly CustomWebApplicationFactory _factory;

	public UserFlowIntegrationTests(CustomWebApplicationFactory factory)
	{
		_factory = factory;

		IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
			_factory,
			"user.view",
			"user.activate",
			"employee.create",
			"employee.view")
			.GetAwaiter().GetResult();
	}

	// 1. Index
	[Fact]
	public async Task Index_Returns200()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/User/Index");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	// 2. Create GET
	[Fact]
	public async Task Create_Get_Returns200()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/User/Create");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	// 3. Create POST — بيانات ناقصة
	[Fact]
	public async Task Create_WithEmptyPhone_ReturnsViewWithError()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/User/Create");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["Name"] = "مستخدم اختبار",
			["Phone"] = "",       // ⬅️ فاضي
			["Password"] = "Test@123456",
			["RoleId"] = "1",
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/User/Create", form);

		response.StatusCode.Should().Be(HttpStatusCode.OK);

		// تأكد مفيش مستخدم اتسجل
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var any = await db.Users
			.AnyAsync(x => x.FullName == "مستخدم اختبار");

		any.Should().BeFalse();
	}

	// 4. ToggleActive — غير موجود
	[Fact]
	public async Task ToggleActive_NonExisting_ReturnsNotFound()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/User/Index");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync(
			"/User/ToggleActive/999999", form);

		response.StatusCode.Should().Be(
			HttpStatusCode.NotFound,
			$"Expected NotFound for non-existing user, got {response.StatusCode}");
	}

	// 5. ToggleActive — موجود
	[Fact]
	public async Task ToggleActive_ExistingUser_ChangesState()
	{
		// زرع مستخدم مباشرة
		int userId;
		using (var scope = _factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

			var user = new User
			{
				Phone = $"0111{Random.Shared.Next(1000000, 9999999)}",
				UserType = UserType.Employee,
				Status = UserStatus.Approved,
				IsActive = true,
				IsPasswordSet = true,
				SecurityStamp = Guid.NewGuid().ToString(),
				CreatedAt = DateTime.UtcNow
			};

			db.Users.Add(user);
			await db.SaveChangesAsync();
			userId = user.Id;
		}

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/User/Index");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync(
			$"/User/ToggleActive/{userId}", form);

		response.StatusCode.Should().BeOneOf(
			new[] { HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther });

		using var scope2 = _factory.Services.CreateScope();
		var db2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var updated = await db2.Users.FindAsync(userId);
		updated!.IsActive.Should().BeFalse();
	}

	// 6. بدون صلاحية
	[Fact]
	public async Task Create_WithoutPermission_Redirects()
	{
		// أنشئ مستخدم موظف بصلاحيات محدودة
		var employeeId = await IntegrationTestHelpers
			.SeedEmployeeWithPermissionsAsync(_factory, "employee.view");

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedEmployeeClientAsync(_factory);

		var response = await client.GetAsync("/User/Create");

		response.StatusCode.Should().Be(HttpStatusCode.Redirect);
		response.Headers.Location?.ToString()
			.Should().Contain("/Account/AccessDenied");
	}
}