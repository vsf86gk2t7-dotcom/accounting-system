using FluentAssertions;
using System.Net;

namespace AccountingSystem.Tests.Integration;

public class InventoryFlowIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
	private readonly CustomWebApplicationFactory _factory;

	public InventoryFlowIntegrationTests(CustomWebApplicationFactory factory)
	{
		_factory = factory;

		IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
			_factory,
			"inventory.view",
			"inventory.receive",
			"inventory.issue",
			"inventory.transfer",
			"inventory.adjust",
			"inventory.count",
			"inventory.transaction.view")
			.GetAwaiter().GetResult();
	}

	// 1. Index
	[Fact]
	public async Task Index_Returns200()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/Inventory/Index");

		response.StatusCode.Should().Be(
			HttpStatusCode.OK,
			$"Location={response.Headers.Location}");
	}

	// 2. Lots
	[Fact]
	public async Task Lots_Returns200()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/Inventory/Lots");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	// 3. Receive GET
	[Fact]
	public async Task Receive_Get_Returns200()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/Inventory/Receive");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	// 4. Issue GET
	[Fact]
	public async Task Issue_Get_Returns200()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/Inventory/Issue");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	// 5. Transfer GET
	[Fact]
	public async Task Transfer_Get_Returns200()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/Inventory/Transfer");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	// 6. Adjust GET
	[Fact]
	public async Task Adjust_Get_Returns200()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/Inventory/Adjust");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	// 7. Transactions
	[Fact]
	public async Task Transactions_Returns200()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/Inventory/Transactions");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}
}