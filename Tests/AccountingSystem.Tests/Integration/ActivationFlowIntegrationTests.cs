using FluentAssertions;
using System.Net;

namespace AccountingSystem.Tests.Integration;

public class ActivationFlowIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
	private readonly CustomWebApplicationFactory _factory;

	public ActivationFlowIntegrationTests(CustomWebApplicationFactory factory)
	{
		_factory = factory;
	}

	// 1. Customer GET — Anonymous (لا يحتاج تسجيل دخول)
	[Fact]
	public async Task Customer_Get_Returns200()
	{
		var client = IntegrationTestHelpers.CreateClient(_factory);

		var response = await client.GetAsync("/Activation/Customer");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	// 2. Supplier GET — Anonymous
	[Fact]
	public async Task Supplier_Get_Returns200()
	{
		var client = IntegrationTestHelpers.CreateClient(_factory);

		var response = await client.GetAsync("/Activation/Supplier");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	// 3. Account GET — Anonymous
	[Fact]
	public async Task Account_Get_Returns200()
	{
		var client = IntegrationTestHelpers.CreateClient(_factory);

		var response = await client.GetAsync("/Activation/Account");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	// 4. Customer POST — بيانات ناقصة
	[Fact]
	public async Task Customer_Post_WithEmptyData_ReturnsViewWithError()
	{
		var client = IntegrationTestHelpers.CreateClient(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/Activation/Customer");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["Phone"] = "",
			["ActivationCode"] = "",
			["Password"] = "",
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/Activation/Customer", form);

		// 200 → رجع الصفحة مع أخطاء validation
		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	// 5. Supplier POST — بيانات ناقصة
	[Fact]
	public async Task Supplier_Post_WithEmptyData_ReturnsViewWithError()
	{
		var client = IntegrationTestHelpers.CreateClient(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/Activation/Supplier");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["Phone"] = "",
			["ActivationCode"] = "",
			["Password"] = "",
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/Activation/Supplier", form);

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}
}