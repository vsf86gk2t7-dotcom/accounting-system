using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AccountingSystem.Tests.Integration;

public class SmokeIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
	private readonly CustomWebApplicationFactory _factory;

	public SmokeIntegrationTests(CustomWebApplicationFactory factory)
	{
		_factory = factory;
	}

	[Fact]
	public void App_StartsSuccessfully()
	{
		_factory.Services.Should().NotBeNull();
	}

	[Fact]
	public async Task Root_ReturnsAnyHttpResponse()
	{
		var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
		{
			AllowAutoRedirect = false
		});

		var response = await client.GetAsync("/");

		((int)response.StatusCode).Should().BeInRange(200, 499);
	}

	[Fact]
	public async Task LoginPage_IsReachable()
	{
		var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
		{
			AllowAutoRedirect = false
		});

		var response = await client.GetAsync("/Account/Login");

		((int)response.StatusCode).Should().BeInRange(200, 499);
	}
}