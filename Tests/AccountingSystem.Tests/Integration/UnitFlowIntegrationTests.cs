using AccountingSystem.Data;
using AccountingSystem.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace AccountingSystem.Tests.Integration;

public class UnitFlowIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
	private readonly CustomWebApplicationFactory _factory;

	public UnitFlowIntegrationTests(CustomWebApplicationFactory factory)
	{
		_factory = factory;

		// ✅ UnitController بيستخدم product.* مش unit.*
				IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
			_factory,
			"product.view",
			"product.create",
			"product.edit")
			.GetAwaiter().GetResult();
	}   // ⬅️ الـ } اللي كان ناقص

	// 1. Index
	[Fact]
	public async Task Index_Returns200()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/Unit/Index");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	// 2. Create
	[Fact]
	public async Task Create_Post_PersistsUnit()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var name = $"وحدة {Guid.NewGuid():N}".Substring(0, 15);
		var unitId = await UnitCategoryTestHelpers
			.CreateUnitViaHttpAsync(
				_factory, client,
				name: name,
				shortName: "قطعة");

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var unit = await db.Units.FindAsync(unitId);

		unit.Should().NotBeNull();
		unit!.Name.Should().Be(name);
		unit.ShortName.Should().Be("قطعة");
		unit.IsActive.Should().BeTrue();
	}

	// 3. Edit
	[Fact]
	public async Task Edit_Post_UpdatesUnit()
	{
		var unitId = await UnitCategoryTestHelpers
			.SeedUnitAsync(_factory, name: "قبل التعديل");

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(
				client, $"/Unit/Edit/{unitId}");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["Id"] = unitId.ToString(),
			["Name"] = "بعد التعديل",
			["ShortName"] = "جديد",
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync($"/Unit/Edit/{unitId}", form);

		response.StatusCode.Should().BeOneOf(
			new[] { HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther });

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var updated = await db.Units.FindAsync(unitId);

		updated!.Name.Should().Be("بعد التعديل");
		updated.ShortName.Should().Be("جديد");
	}

	// 4. ToggleActive
	[Fact]
	public async Task ToggleActive_ChangesState()
	{
		var unitId = await UnitCategoryTestHelpers
			.SeedUnitAsync(_factory, isActive: true);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(
				client, $"/Unit/Edit/{unitId}");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync(
			$"/Unit/ToggleActive/{unitId}", form);

		response.StatusCode.Should().BeOneOf(
			new[] { HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther });

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var unit = await db.Units.FindAsync(unitId);
		unit!.IsActive.Should().BeFalse();
	}

	// 5. Duplicate Name
	[Fact]
	public async Task Create_DuplicateName_ReturnsViewWithError()
	{
		var name = $"وحدة مكررة {Guid.NewGuid():N}".Substring(0, 15);
		await UnitCategoryTestHelpers.SeedUnitAsync(_factory, name: name);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/Unit/Create");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["Name"] = name,
			["ShortName"] = string.Empty,
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/Unit/Create", form);

		response.StatusCode.Should().Be(HttpStatusCode.OK);

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var unitsWithName = await db.Units
			.Where(x => x.Name == name)
			.ToListAsync();

		unitsWithName.Should().HaveCount(1);
	}

	// 6. Non-Existing
	[Fact]
	public async Task Edit_NonExisting_ReturnsNotFound()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/Unit/Edit/999999");

		response.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}
}