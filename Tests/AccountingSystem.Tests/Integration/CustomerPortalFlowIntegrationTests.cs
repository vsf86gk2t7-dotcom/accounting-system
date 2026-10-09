using AccountingSystem.Data;
using AccountingSystem.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace AccountingSystem.Tests.Integration;

public class CustomerPortalFlowIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
	private readonly CustomWebApplicationFactory _factory;

	public CustomerPortalFlowIntegrationTests(CustomWebApplicationFactory factory)
	{
		_factory = factory;

		IntegrationTestHelpers.SeedCustomerWithUserAsync(_factory)
			.GetAwaiter().GetResult();
	}

	// 1. بدون تسجيل دخول → redirect to login
	[Fact]
	public async Task Index_WithoutAuth_RedirectsToLogin()
	{
		var client = IntegrationTestHelpers.CreateClient(_factory);

		var response = await client.GetAsync("/CustomerPortal/Index");

		response.StatusCode.Should().Be(HttpStatusCode.Redirect);
		response.Headers.Location?.ToString()
			.Should().Contain("/Account/Login");
	}

	// 2. Index مع تسجيل دخول العميل
	[Fact]
	public async Task Index_WithCustomerAuth_Returns200()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedCustomerClientAsync(_factory);

		var response = await client.GetAsync("/CustomerPortal/Index");

		response.StatusCode.Should().Be(
			HttpStatusCode.OK,
			$"Location={response.Headers.Location}");
	}

	// 3. Statement
	[Fact]
	public async Task Statement_WithCustomerAuth_Returns200()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedCustomerClientAsync(_factory);

		var response = await client.GetAsync("/CustomerPortal/Statement");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	// 4. Invoices
	[Fact]
	public async Task Invoices_WithCustomerAuth_Returns200()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedCustomerClientAsync(_factory);

		var response = await client.GetAsync("/CustomerPortal/Invoices");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	// 5. Admin بدون CustomerId → redirect to login
		// 5. Admin بدون CustomerId → redirect to login
		// 5. Admin بدون CustomerId → redirect to login
	//
	// ملاحظة: RedirectToAction("Login", "Account") ينتج URL "/" 
	// بسبب أن default route هو {controller=Account}/{action=Login}
	// و ASP.NET Core URL generation يختصر الـ URL في هذه الحالة.
	[Fact]
	public async Task Index_WithAdminAuth_RedirectsToLogin()
	{
		await IntegrationTestHelpers
			.SeedAdminWithPermissionsAsync(_factory);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/CustomerPortal/Index");

		response.StatusCode.Should().Be(HttpStatusCode.Redirect);

		var location = response.Headers.Location?.ToString() ?? "";

		(location == "/" || location.Contains("/Account/Login"))
			.Should().BeTrue(
				$"Admin should be redirected to login. Actual Location: {location}");
	}
}