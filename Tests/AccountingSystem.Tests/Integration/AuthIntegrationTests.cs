using FluentAssertions;
using System.Net;

namespace AccountingSystem.Tests.Integration;

public class AuthIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
	private readonly CustomWebApplicationFactory _factory;

	public AuthIntegrationTests(CustomWebApplicationFactory factory)
	{
		_factory = factory;

		IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
			_factory,
			"employee.view")
			.GetAwaiter().GetResult();
	}

	[Fact]
	public async Task LoginPage_IsReachable_AndContainsAntiForgeryToken()
	{
		var client = IntegrationTestHelpers.CreateClient(_factory);

		var response = await client.GetAsync("/Account/Login");

		response.StatusCode.Should().Be(HttpStatusCode.OK);

		var token = await IntegrationTestHelpers.GetAntiForgeryTokenAsync(client);
		token.Should().NotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task Login_WithValidCredentials_SetsAccessTokenCookie()
	{
		var client = IntegrationTestHelpers.CreateClient(_factory);

		var response = await IntegrationTestHelpers.PostLoginAsync(
			client,
			IntegrationTestHelpers.AdminPhone,
			IntegrationTestHelpers.AdminPassword);

		response.StatusCode.Should().BeOneOf(
			HttpStatusCode.Redirect,
			HttpStatusCode.Found,
			HttpStatusCode.SeeOther);

		var setCookies = response.Headers
			.Where(h => h.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase))
			.SelectMany(h => h.Value)
			.ToList();

		setCookies.Should().Contain(c =>
			c.StartsWith(IntegrationTestHelpers.AccessTokenCookieName));
	}

	[Fact]
	public async Task Login_WithWrongPassword_ReturnsLoginView()
	{
		var client = IntegrationTestHelpers.CreateClient(_factory);

		var response = await IntegrationTestHelpers.PostLoginAsync(
			client,
			IntegrationTestHelpers.AdminPhone,
			"WrongPassword@@@");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	[Fact]
	public async Task ProtectedEndpoint_WithoutAuth_RedirectsToLogin()
	{
		var client = IntegrationTestHelpers.CreateClient(_factory);

		var response = await client.GetAsync("/Employee/Index");

		response.StatusCode.Should().Be(HttpStatusCode.Redirect);
		response.Headers.Location?.ToString()
			.Should().Contain("/Account/Login");
	}

	[Fact]
	public async Task ProtectedEndpoint_AfterLogin_ReturnsSuccess()
	{
		var client = IntegrationTestHelpers.CreateClient(_factory);

		var loginResponse = await IntegrationTestHelpers.PostLoginAsync(
			client,
			IntegrationTestHelpers.AdminPhone,
			IntegrationTestHelpers.AdminPassword);

		loginResponse.StatusCode.Should().BeOneOf(
			HttpStatusCode.Redirect,
			HttpStatusCode.Found,
			HttpStatusCode.SeeOther);

		var response = await client.GetAsync("/Employee/Index");

		((int)response.StatusCode).Should().BeInRange(200, 399);
	}
}