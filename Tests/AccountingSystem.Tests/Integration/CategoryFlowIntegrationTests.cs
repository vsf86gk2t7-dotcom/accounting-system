using AccountingSystem.Data;
using AccountingSystem.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace AccountingSystem.Tests.Integration;

public class CategoryFlowIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
	private readonly CustomWebApplicationFactory _factory;

	public CategoryFlowIntegrationTests(CustomWebApplicationFactory factory)
	{
		_factory = factory;

				IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
			_factory,
			"product.view",
			"product.create",
			"product.edit")
			.GetAwaiter().GetResult();
	}

	// 1. Index
	[Fact]
	public async Task Index_Returns200()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/Category/Index");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	// 2. Create
	[Fact]
	public async Task Create_Post_PersistsCategory()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var name = $"تصنيف {Guid.NewGuid():N}".Substring(0, 15);
		var categoryId = await UnitCategoryTestHelpers
			.CreateCategoryViaHttpAsync(
				_factory, client,
				name: name,
				description: "وصف التصنيف");

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var category = await db.Categories.FindAsync(categoryId);

		category.Should().NotBeNull();
		category!.Name.Should().Be(name);
		category.Description.Should().Be("وصف التصنيف");
		category.IsActive.Should().BeTrue();
	}

	// 3. Edit
	[Fact]
	public async Task Edit_Post_UpdatesCategory()
	{
		var categoryId = await UnitCategoryTestHelpers
			.SeedCategoryAsync(_factory, name: "قبل التعديل");

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(
				client, $"/Category/Edit/{categoryId}");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["Id"] = categoryId.ToString(),
			["Name"] = "بعد التعديل",
			["Description"] = "وصف جديد",
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync($"/Category/Edit/{categoryId}", form);

		response.StatusCode.Should().BeOneOf(
			new[] { HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther });

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var updated = await db.Categories.FindAsync(categoryId);

		updated!.Name.Should().Be("بعد التعديل");
		updated.Description.Should().Be("وصف جديد");
	}

	// 4. ToggleActive
	[Fact]
	public async Task ToggleActive_ChangesState()
	{
		var categoryId = await UnitCategoryTestHelpers
			.SeedCategoryAsync(_factory, isActive: true);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(
				client, $"/Category/Edit/{categoryId}");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync(
			$"/Category/ToggleActive/{categoryId}", form);

		response.StatusCode.Should().BeOneOf(
			new[] { HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther });

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var category = await db.Categories.FindAsync(categoryId);
		category!.IsActive.Should().BeFalse();
	}

	// 5. Duplicate Name
	[Fact]
	public async Task Create_DuplicateName_ReturnsViewWithError()
	{
		var name = $"تصنيف مكرر {Guid.NewGuid():N}".Substring(0, 15);
		await UnitCategoryTestHelpers.SeedCategoryAsync(_factory, name: name);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/Category/Create");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["Name"] = name,
			["Description"] = string.Empty,
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/Category/Create", form);

		response.StatusCode.Should().Be(HttpStatusCode.OK);

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var categoriesWithName = await db.Categories
			.Where(x => x.Name == name)
			.ToListAsync();

		categoriesWithName.Should().HaveCount(1);
	}

	// 6. Non-Existing
	[Fact]
	public async Task Edit_NonExisting_ReturnsNotFound()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/Category/Edit/999999");

		response.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}
}