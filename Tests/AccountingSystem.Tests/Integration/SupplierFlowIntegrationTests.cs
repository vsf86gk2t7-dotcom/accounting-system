using AccountingSystem.Data;
using AccountingSystem.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace AccountingSystem.Tests.Integration;

/// <summary>
/// اختبارات تكامل للموردين — 6 سيناريوهات أساسية
/// </summary>
public class SupplierFlowIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
	private readonly CustomWebApplicationFactory _factory;

	public SupplierFlowIntegrationTests(CustomWebApplicationFactory factory)
	{
		_factory = factory;

		IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
			_factory,
			"supplier.view",
			"supplier.create",
			"supplier.edit",
			"supplier.activate",
			"supplier.portal.manage")
			.GetAwaiter().GetResult();
	}

	// =========================================
	// 1. Index
	// =========================================

	[Fact]
	public async Task Index_Returns200()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/Supplier/Index");

		response.StatusCode.Should().Be(
			HttpStatusCode.OK,
			$"Location={response.Headers.Location}");
	}

	// =========================================
	// 2. Create via HTTP
	// =========================================

	[Fact]
	public async Task Create_Post_PersistsSupplier()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var phone = $"0100{Random.Shared.Next(1000000, 9999999)}";

		var supplierId = await SupplierTestHelpers
			.CreateSupplierViaHttpAsync(
				_factory, client,
				name: "مورد جديد",
				phone: phone,
				email: "supplier@test.com",
				creditDays: 30);

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var supplier = await db.Suppliers.FindAsync(supplierId);

		supplier.Should().NotBeNull();
		supplier!.Name.Should().Be("مورد جديد");
		supplier.Phone.Should().Be(phone);
		supplier.Email.Should().Be("supplier@test.com");
		supplier.CreditDays.Should().Be(30);
		supplier.IsActive.Should().BeTrue();
		supplier.PortalRequested.Should().BeFalse();
		supplier.PortalApproved.Should().BeFalse();
		supplier.HasPortalAccount.Should().BeFalse();
	}

	// =========================================
	// 3. Edit
	// =========================================

	[Fact]
	public async Task Edit_Post_UpdatesSupplier()
	{
		var supplierId = await SupplierTestHelpers
			.SeedSupplierAsync(_factory, name: "قبل التعديل");

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(
				client, $"/Supplier/Edit/{supplierId}");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["Id"] = supplierId.ToString(),
			["Name"] = "بعد التعديل",
			["Phone"] = "01234567890",
			["Email"] = "updated@test.com",
			["Address"] = "عنوان جديد",
			["OpeningBalance"] = "500",
			["CreditDays"] = "15",
			["IsActive"] = "true",
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync(
			$"/Supplier/Edit/{supplierId}", form);

		response.StatusCode.Should().BeOneOf(
			new[] { HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther });

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var updated = await db.Suppliers.FindAsync(supplierId);

		updated!.Name.Should().Be("بعد التعديل");
		updated.Phone.Should().Be("01234567890");
		updated.Email.Should().Be("updated@test.com");
		updated.CreditDays.Should().Be(15);
	}

	// =========================================
	// 4. ToggleActive
	// =========================================

	[Fact]
	public async Task ToggleActive_ExistingSupplier_ChangesState()
	{
		var supplierId = await SupplierTestHelpers
			.SeedSupplierAsync(_factory, isActive: true);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(
				client, $"/Supplier/Edit/{supplierId}");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync(
			$"/Supplier/ToggleActive/{supplierId}", form);

		response.StatusCode.Should().BeOneOf(
			new[] { HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther });

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var supplier = await db.Suppliers.FindAsync(supplierId);

		supplier!.IsActive.Should().BeFalse();
	}

	// =========================================
	// 5. Request Portal
	// =========================================

	[Fact]
	public async Task RequestPortal_ExistingSupplier_SetsPortalRequested()
	{
		var supplierId = await SupplierTestHelpers
			.SeedSupplierAsync(_factory);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(
				client, $"/Supplier/Edit/{supplierId}");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync(
			$"/Supplier/RequestPortal/{supplierId}", form);

		response.StatusCode.Should().BeOneOf(
			new[] { HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther });

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var supplier = await db.Suppliers.FindAsync(supplierId);

		supplier!.PortalRequested.Should().BeTrue();
	}

	// =========================================
	// 6. Non-Existing Supplier
	// =========================================

	[Fact]
	public async Task Edit_NonExistingSupplier_ReturnsNotFound()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/Supplier/Edit/999999");

		response.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}
}