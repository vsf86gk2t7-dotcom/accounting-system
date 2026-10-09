using FluentAssertions;
using System.Net;

namespace AccountingSystem.Tests.Integration;

public class RoleAuthorizationIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
	private readonly CustomWebApplicationFactory _factory;

	public RoleAuthorizationIntegrationTests(CustomWebApplicationFactory factory)
	{
		_factory = factory;

		IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
			_factory,
			"user.view",
			"employee.view")
			.GetAwaiter().GetResult();

		IntegrationTestHelpers.SeedEmployeeWithPermissionsAsync(
			_factory,
			"employee.view")
			.GetAwaiter().GetResult();
	}

	// =========================================
	// AdminOnly
	// =========================================

	[Fact]
	public async Task Admin_AccessAdminController_Returns200()
	{
		var client = await IntegrationTestHelpers.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/Admin/RegistrationRequests");

		response.StatusCode.Should().Be(
			HttpStatusCode.OK,
			$"Location={response.Headers.Location}");
	}

	[Fact]
	public async Task Employee_AccessAdminController_RedirectsToAccessDenied()
	{
		var client = await IntegrationTestHelpers.CreateAuthenticatedEmployeeClientAsync(_factory);

		var response = await client.GetAsync("/Admin/RegistrationRequests");

		response.StatusCode.Should().Be(HttpStatusCode.Redirect);
		response.Headers.Location?.ToString()
			.Should().Contain("/Account/AccessDenied");
	}

	[Fact]
	public async Task Anonymous_AccessAdminController_RedirectsToLogin()
	{
		var client = IntegrationTestHelpers.CreateClient(_factory);

		var response = await client.GetAsync("/Admin/RegistrationRequests");

		response.StatusCode.Should().Be(HttpStatusCode.Redirect);
		response.Headers.Location?.ToString()
			.Should().Contain("/Account/Login");
	}

	// =========================================
	// RequirePermission
	// =========================================

	[Fact]
	public async Task Admin_AccessUserController_WithPermission_Returns200()
	{
		var client = await IntegrationTestHelpers.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/User/Index");

		response.StatusCode.Should().Be(
			HttpStatusCode.OK,
			$"Status={response.StatusCode}, Location={response.Headers.Location}");
	}

	[Fact]
	public async Task Employee_AccessUserController_WithoutPermission_RedirectsToAccessDenied()
	{
		var client = await IntegrationTestHelpers.CreateAuthenticatedEmployeeClientAsync(_factory);

		var response = await client.GetAsync("/User/Index");

		response.StatusCode.Should().Be(HttpStatusCode.Redirect);
		response.Headers.Location?.ToString()
			.Should().Contain("/Account/AccessDenied");
	}

	[Fact]
	public async Task Employee_AccessEmployeeController_WithPermission_Returns200()
	{
		var client = await IntegrationTestHelpers.CreateAuthenticatedEmployeeClientAsync(_factory);

		var response = await client.GetAsync("/Employee/Index");

		response.StatusCode.Should().Be(
			HttpStatusCode.OK,
			$"Status={response.StatusCode}, Location={response.Headers.Location}");
	}
}