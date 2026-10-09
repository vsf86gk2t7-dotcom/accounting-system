using AccountingSystem.Data;
using AccountingSystem.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace AccountingSystem.Tests.Integration;

/// <summary>
/// اختبارات تكامل للعملاء — 6 سيناريوهات أساسية
/// </summary>
public class CustomerFlowIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
	private readonly CustomWebApplicationFactory _factory;

	public CustomerFlowIntegrationTests(CustomWebApplicationFactory factory)
	{
		_factory = factory;

		IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
			_factory,
			"customer.view",
			"customer.create",
			"customer.edit",
			"customer.activate",
			"customer.portal.manage")
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

		var response = await client.GetAsync("/Customer/Index");

		response.StatusCode.Should().Be(
			HttpStatusCode.OK,
			$"Location={response.Headers.Location}");
	}

	// =========================================
	// 2. Create via HTTP
	// =========================================

	[Fact]
	public async Task Create_Post_PersistsCustomer()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var phone = $"0100{Random.Shared.Next(1000000, 9999999)}";

		var customerId = await CustomerTestHelpers
			.CreateCustomerViaHttpAsync(
				_factory, client,
				name: "عميل جديد",
				phone: phone,
				email: "customer@test.com",
				creditDays: 30);

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var customer = await db.Customers.FindAsync(customerId);

		customer.Should().NotBeNull();
		customer!.Name.Should().Be("عميل جديد");
		customer.Phone.Should().Be(phone);
		customer.Email.Should().Be("customer@test.com");
		customer.CreditDays.Should().Be(30);
		customer.IsActive.Should().BeTrue();
		customer.PortalRequested.Should().BeFalse();
		customer.PortalApproved.Should().BeFalse();
		customer.HasPortalAccount.Should().BeFalse();
	}

	// =========================================
	// 3. Edit
	// =========================================

	[Fact]
	public async Task Edit_Post_UpdatesCustomer()
	{
		var customerId = await CustomerTestHelpers
			.SeedCustomerAsync(_factory, name: "قبل التعديل");

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(
				client, $"/Customer/Edit/{customerId}");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["Id"] = customerId.ToString(),
			["Name"] = "بعد التعديل",
			["Phone"] = "01234567891",
			["Email"] = "updated@test.com",
			["Address"] = "عنوان جديد",
			["OpeningBalance"] = "500",
			["CreditDays"] = "15",
			["IsActive"] = "true",
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync(
			$"/Customer/Edit/{customerId}", form);

		response.StatusCode.Should().BeOneOf(
			new[] { HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther });

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var updated = await db.Customers.FindAsync(customerId);

		updated!.Name.Should().Be("بعد التعديل");
		updated.Phone.Should().Be("01234567891");
		updated.Email.Should().Be("updated@test.com");
		updated.CreditDays.Should().Be(15);
	}

	// =========================================
	// 4. ToggleActive
	// =========================================

	[Fact]
	public async Task ToggleActive_ExistingCustomer_ChangesState()
	{
		var customerId = await CustomerTestHelpers
			.SeedCustomerAsync(_factory, isActive: true);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(
				client, $"/Customer/Edit/{customerId}");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync(
			$"/Customer/ToggleActive/{customerId}", form);

		response.StatusCode.Should().BeOneOf(
			new[] { HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther });

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var customer = await db.Customers.FindAsync(customerId);

		customer!.IsActive.Should().BeFalse();
	}

	// =========================================
	// 5. Request Portal
	// =========================================

	[Fact]
	public async Task RequestPortal_ExistingCustomer_SetsPortalRequested()
	{
		var customerId = await CustomerTestHelpers
			.SeedCustomerAsync(_factory);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(
				client, $"/Customer/Edit/{customerId}");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync(
			$"/Customer/RequestPortal/{customerId}", form);

		response.StatusCode.Should().BeOneOf(
			new[] { HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther });

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var customer = await db.Customers.FindAsync(customerId);

		customer!.PortalRequested.Should().BeTrue();
	}

	// =========================================
	// 6. Non-Existing Customer
	// =========================================

	[Fact]
	public async Task Edit_NonExistingCustomer_ReturnsNotFound()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/Customer/Edit/999999");

		response.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}
}