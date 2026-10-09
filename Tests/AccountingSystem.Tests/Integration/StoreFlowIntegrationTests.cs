using AccountingSystem.Data;
using AccountingSystem.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace AccountingSystem.Tests.Integration;

public class StoreFlowIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
	private readonly CustomWebApplicationFactory _factory;

	public StoreFlowIntegrationTests(CustomWebApplicationFactory factory)
	{
		_factory = factory;

		IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
			_factory,
			"store.view",
			"store.create",
			"store.edit",
			"store.activate",
			"store.delete")
			.GetAwaiter().GetResult();
	}

	// 1. Index
	[Fact]
	public async Task Index_Returns200()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/Store/Index");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	// 2. Create
	[Fact]
	public async Task Create_Post_PersistsStore()
	{
		var branchId = await BranchStoreTestHelpers.SeedBranchAsync(_factory);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var storeName = $"مخزن {Guid.NewGuid():N}".Substring(0, 15);
		var storeId = await BranchStoreTestHelpers
			.CreateStoreViaHttpAsync(
				_factory, client,
				branchId: branchId,
				name: storeName,
				code: "ST-001");

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var store = await db.Stores.FindAsync(storeId);

		store.Should().NotBeNull();
		store!.Name.Should().Be(storeName);
		store.Code.Should().Be("ST-001");
		store.BranchId.Should().Be(branchId);
		store.IsActive.Should().BeTrue();
	}

	// 3. Edit
	[Fact]
	public async Task Edit_Post_UpdatesStore()
	{
		var storeId = await BranchStoreTestHelpers
			.SeedStoreAsync(_factory, name: "قبل التعديل");

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(
				client, $"/Store/Edit/{storeId}");

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var originalStore = await db.Stores.FindAsync(storeId);

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["Id"] = storeId.ToString(),
			["Name"] = "بعد التعديل",
			["Code"] = "ST-NEW",
			["BranchId"] = originalStore!.BranchId.ToString(),
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync($"/Store/Edit/{storeId}", form);

		response.StatusCode.Should().BeOneOf(
			new[] { HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther });

		using var scope2 = _factory.Services.CreateScope();
		var db2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var updated = await db2.Stores.FindAsync(storeId);

		updated!.Name.Should().Be("بعد التعديل");
		updated.Code.Should().Be("ST-NEW");
	}

	// 4. ToggleActive
	[Fact]
	public async Task ToggleActive_ChangesState()
	{
		var storeId = await BranchStoreTestHelpers
			.SeedStoreAsync(_factory, isActive: true);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(
				client, $"/Store/Edit/{storeId}");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync(
			$"/Store/ToggleActive/{storeId}", form);

		response.StatusCode.Should().BeOneOf(
			new[] { HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther });

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var store = await db.Stores.FindAsync(storeId);
		store!.IsActive.Should().BeFalse();
	}

	// 5. Duplicate Name in Same Branch
	[Fact]
	public async Task Create_DuplicateNameInSameBranch_ReturnsViewWithError()
	{
		var branchId = await BranchStoreTestHelpers.SeedBranchAsync(_factory);

		var storeName = $"مخزن مكرر {Guid.NewGuid():N}".Substring(0, 15);
		await BranchStoreTestHelpers.SeedStoreAsync(
			_factory, branchId: branchId, name: storeName);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/Store/Create");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["Name"] = storeName,
			["Code"] = string.Empty,
			["BranchId"] = branchId.ToString(),
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/Store/Create", form);

		response.StatusCode.Should().Be(HttpStatusCode.OK);

		// الأصلي فقط
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var storesWithName = await db.Stores
			.Where(x => x.Name == storeName && x.BranchId == branchId)
			.ToListAsync();

		storesWithName.Should().HaveCount(1);
	}

	// 6. Non-Existing
	[Fact]
	public async Task Edit_NonExisting_ReturnsNotFound()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/Store/Edit/999999");

		response.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	// 7. Same Name in Different Branch (مسموح)
	[Fact]
	public async Task Create_SameNameInDifferentBranch_Succeeds()
	{
		var branch1 = await BranchStoreTestHelpers.SeedBranchAsync(_factory);
		var branch2 = await BranchStoreTestHelpers.SeedBranchAsync(_factory);

		var storeName = $"مخزن مشترك {Guid.NewGuid():N}".Substring(0, 15);

		await BranchStoreTestHelpers.SeedStoreAsync(
			_factory, branchId: branch1, name: storeName);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var store2Id = await BranchStoreTestHelpers
			.CreateStoreViaHttpAsync(
				_factory, client,
				branchId: branch2,
				name: storeName);

		// نجح
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var store2 = await db.Stores.FindAsync(store2Id);
		store2.Should().NotBeNull();
		store2!.BranchId.Should().Be(branch2);
	}
}