using AccountingSystem.Data;
using AccountingSystem.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace AccountingSystem.Tests.Integration;

public class HrSetupFlowIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
	private readonly CustomWebApplicationFactory _factory;

	public HrSetupFlowIntegrationTests(CustomWebApplicationFactory factory)
	{
		_factory = factory;

		IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
			_factory,
			"hr.setup.view",
			"hr.setup.manage")
			.GetAwaiter().GetResult();
	}

	// 1. Index
	[Fact]
	public async Task Index_Returns200()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/HrSetup/Index");

		response.StatusCode.Should().Be(
			HttpStatusCode.OK,
			$"Location={response.Headers.Location}");
	}

	// 2. AddDepartment — valid
	[Fact]
	public async Task AddDepartment_ValidName_PersistsDepartment()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/HrSetup/Index");

		var name = $"قسم {Guid.NewGuid():N}".Substring(0, 20);

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["name"] = name,
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/HrSetup/AddDepartment", form);

		response.StatusCode.Should().BeOneOf(
			new[] { HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther });

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var dept = await db.Departments.FirstOrDefaultAsync(x => x.Name == name);
		dept.Should().NotBeNull();
		dept!.IsActive.Should().BeTrue();
	}

	// 3. AddDepartment — duplicate
	[Fact]
	public async Task AddDepartment_DuplicateName_DoesNotPersist()
	{
		var name = $"قسم مكرر {Guid.NewGuid():N}".Substring(0, 20);

		using (var scope = _factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			db.Departments.Add(new Department { Name = name, IsActive = true, CreatedAt = DateTime.UtcNow });
			await db.SaveChangesAsync();
		}

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/HrSetup/Index");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["name"] = name,
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/HrSetup/AddDepartment", form);

		response.StatusCode.Should().BeOneOf(
			new[] { HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther });

		using var scope2 = _factory.Services.CreateScope();
		var db2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var count = await db2.Departments.CountAsync(x => x.Name == name);
		count.Should().Be(1);
	}

	// 4. AddDepartment — empty name
	[Fact]
	public async Task AddDepartment_EmptyName_DoesNotPersist()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/HrSetup/Index");

		var beforeCount = 0;
		using (var scope = _factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			beforeCount = await db.Departments.CountAsync();
		}

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["name"] = "",
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/HrSetup/AddDepartment", form);

		response.StatusCode.Should().BeOneOf(
			new[] { HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther });

		using var scope2 = _factory.Services.CreateScope();
		var db2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var afterCount = await db2.Departments.CountAsync();
		afterCount.Should().Be(beforeCount);
	}

	// 5. AddPosition — valid
	[Fact]
	public async Task AddPosition_ValidName_PersistsPosition()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/HrSetup/Index");

		var name = $"وظيفة {Guid.NewGuid():N}".Substring(0, 20);

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["name"] = name,
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/HrSetup/AddPosition", form);

		response.StatusCode.Should().BeOneOf(
			new[] { HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther });

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var pos = await db.Positions.FirstOrDefaultAsync(x => x.Name == name);
		pos.Should().NotBeNull();
	}

	// 6. ToggleDepartment
	[Fact]
	public async Task ToggleDepartment_ChangesState()
	{
		int deptId;
		using (var scope = _factory.Services.CreateScope())
		{
			var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			var dept = new Department
			{
				Name = $"قسم {Guid.NewGuid():N}".Substring(0, 20),
				IsActive = true,
				CreatedAt = DateTime.UtcNow
			};
			db.Departments.Add(dept);
			await db.SaveChangesAsync();
			deptId = dept.Id;
		}

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/HrSetup/Index");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync(
			$"/HrSetup/ToggleDepartment/{deptId}", form);

		response.StatusCode.Should().BeOneOf(
			new[] { HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther });

		using var scope2 = _factory.Services.CreateScope();
		var db2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var updated = await db2.Departments.FindAsync(deptId);
		updated!.IsActive.Should().BeFalse();
	}
}