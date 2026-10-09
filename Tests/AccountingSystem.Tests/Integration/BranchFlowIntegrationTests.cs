using AccountingSystem.Data;
using AccountingSystem.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace AccountingSystem.Tests.Integration;

public class BranchFlowIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
	private readonly CustomWebApplicationFactory _factory;

	public BranchFlowIntegrationTests(CustomWebApplicationFactory factory)
	{
		_factory = factory;

		IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
			_factory,
			"branch.view",
			"branch.create",
			"branch.edit",
			"branch.activate",
			"branch.delete")
			.GetAwaiter().GetResult();
	}

	// 1. Index
	[Fact]
	public async Task Index_Returns200()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/Branch/Index");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	// 2. Create
	[Fact]
	public async Task Create_Post_PersistsBranch()
	{
		// لازم Company موجودة
		await BranchStoreTestHelpers.SeedCompanyAsync(_factory);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var branchName = $"فرع {Guid.NewGuid():N}".Substring(0, 15);
		var branchId = await BranchStoreTestHelpers
			.CreateBranchViaHttpAsync(_factory, client, name: branchName);

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var branch = await db.Branches.FindAsync(branchId);

		branch.Should().NotBeNull();
		branch!.Name.Should().Be(branchName);
		branch.IsActive.Should().BeTrue();
	}

	// 3. Edit
	[Fact]
	public async Task Edit_Post_UpdatesBranch()
	{
		var branchId = await BranchStoreTestHelpers
			.SeedBranchAsync(_factory, name: "قبل التعديل");

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(
				client, $"/Branch/Edit/{branchId}");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["Id"] = branchId.ToString(),
			["Name"] = "بعد التعديل",
			["Phone"] = "01000000001",
			["Address"] = "عنوان جديد",
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync($"/Branch/Edit/{branchId}", form);

		response.StatusCode.Should().BeOneOf(
			new[] { HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther });

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var updated = await db.Branches.FindAsync(branchId);

		updated!.Name.Should().Be("بعد التعديل");
		updated.Phone.Should().Be("01000000001");
		updated.Address.Should().Be("عنوان جديد");
	}

	// 4. ToggleActive
	[Fact]
	public async Task ToggleActive_ChangesState()
	{
		var branchId = await BranchStoreTestHelpers
			.SeedBranchAsync(_factory, isActive: true);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(
				client, $"/Branch/Edit/{branchId}");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync(
			$"/Branch/ToggleActive/{branchId}", form);

		response.StatusCode.Should().BeOneOf(
			new[] { HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther });

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var branch = await db.Branches.FindAsync(branchId);
		branch!.IsActive.Should().BeFalse();
	}

	// 5. Duplicate Name
	[Fact]
	public async Task Create_DuplicateName_ReturnsViewWithError()
	{
		await BranchStoreTestHelpers.SeedCompanyAsync(_factory);

		var branchName = $"فرع مكرر {Guid.NewGuid():N}".Substring(0, 15);
		await BranchStoreTestHelpers.SeedBranchAsync(_factory, name: branchName);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/Branch/Create");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["Name"] = branchName,
			["Phone"] = string.Empty,
			["Address"] = string.Empty,
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/Branch/Create", form);

		response.StatusCode.Should().Be(HttpStatusCode.OK);

		// الأصلي فقط
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var branchesWithName = await db.Branches
			.Where(x => x.Name == branchName)
			.ToListAsync();

		branchesWithName.Should().HaveCount(1);
	}

	// 6. Non-Existing
	[Fact]
	public async Task Edit_NonExisting_ReturnsNotFound()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/Branch/Edit/999999");

		response.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	// 7. Delete with Stores
	[Fact]
	public async Task Delete_BranchWithStores_Fails()
	{
		var branchId = await BranchStoreTestHelpers
			.SeedBranchAsync(_factory);

		// أضف مخزن للفرع
		await BranchStoreTestHelpers.SeedStoreAsync(_factory, branchId: branchId);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(
				client, $"/Branch/Edit/{branchId}");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync($"/Branch/Delete/{branchId}", form);

		// رجع redirect (نجاح أو خطأ)
		response.StatusCode.Should().BeOneOf(
			new[] { HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther });

		// الفرع لسه موجود
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var branch = await db.Branches.FindAsync(branchId);
		branch.Should().NotBeNull();
	}
}