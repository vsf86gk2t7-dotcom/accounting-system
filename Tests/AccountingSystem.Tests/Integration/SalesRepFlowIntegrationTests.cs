using FluentAssertions;
using System.Net;

namespace AccountingSystem.Tests.Integration;

public class SalesRepFlowIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
	private readonly CustomWebApplicationFactory _factory;

	public SalesRepFlowIntegrationTests(CustomWebApplicationFactory factory)
	{
		_factory = factory;

		IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
			_factory,
			"salesrep.view",
			"salesrep.manage")
			.GetAwaiter().GetResult();
	}

	// 1. Index
	[Fact]
	public async Task Index_Returns200()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/SalesRep/Index");

		response.StatusCode.Should().Be(
			HttpStatusCode.OK,
			$"Location={response.Headers.Location}");
	}

	// 2. Create GET
	[Fact]
	public async Task Create_Get_Returns200()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/SalesRep/Create");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	// 3. Dashboard (قد يعمل redirect لو الأدمن مش مندوب)
	[Fact]
	public async Task Dashboard_ReturnsRedirectOr200()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/SalesRep/Dashboard");

		((int)response.StatusCode).Should().BeInRange(
			200, 399,
			$"Status={response.StatusCode}, Location={response.Headers.Location}");
	}

	// 4. MyCustomers
	[Fact]
	public async Task MyCustomers_ReturnsRedirectOr200()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/SalesRep/MyCustomers");

		((int)response.StatusCode).Should().BeInRange(
			200, 399,
			$"Status={response.StatusCode}, Location={response.Headers.Location}");
	}

	// 5. MyInvoices
	[Fact]
	public async Task MyInvoices_ReturnsRedirectOr200()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/SalesRep/MyInvoices");

		((int)response.StatusCode).Should().BeInRange(
			200, 399,
			$"Status={response.StatusCode}, Location={response.Headers.Location}");
	}

	// 6. Details — non-existing
	[Fact]
	public async Task Details_NonExisting_ReturnsNotFound()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/SalesRep/Details/999999");

		response.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}
}