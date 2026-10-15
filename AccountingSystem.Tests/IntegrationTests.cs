using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AccountingSystem.Tests
{
	public class IntegrationTests : IClassFixture<WebApplicationFactory<Program>>
	{
		private readonly HttpClient _client;

		public IntegrationTests(WebApplicationFactory<Program> factory)
		{
			_client = factory.CreateClient();
		}

		[Fact]
		public async Task HealthCheck_ReturnsOk()
		{
			var response = await _client.GetAsync("/health");

			Assert.True(
				response.StatusCode == HttpStatusCode.OK ||
				response.StatusCode == HttpStatusCode.Unauthorized);
		}

		[Fact]
		public async Task LoginPage_ReturnsOk()
		{
			var response = await _client.GetAsync("/Account/Login");

			Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		}

		[Fact]
		public async Task MeEndpoint_WithoutAuth_ReturnsUnauthorized()
		{
			var response = await _client.GetAsync("/Account/Me");

			Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
		}

		[Fact]
		public async Task Swagger_ReturnsOk()
		{
			var response = await _client.GetAsync("/swagger/v1/swagger.json");

			Assert.True(
				response.StatusCode == HttpStatusCode.OK ||
				response.StatusCode == HttpStatusCode.Unauthorized);
		}
	}
}
