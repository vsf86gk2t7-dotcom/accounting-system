using AccountingSystem.Data;
using AccountingSystem.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace AccountingSystem.Tests.Integration;

public class AdminFlowIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AdminFlowIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // =========================================
    // Auth — Anonymous
    // =========================================

    [Theory]
    [InlineData("/Admin/Backups")]
    [InlineData("/Admin/RegistrationRequests")]
    [InlineData("/Admin/PasswordResetRequests")]
    public async Task Anonymous_RedirectsToLogin(string url)
    {
        var client = IntegrationTestHelpers.CreateClient(_factory);
        var response = await client.GetAsync(url);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther);

        var location = response.Headers.Location?.ToString() ?? "";
        location.Should().Match(l =>
            l.Contains("Login") || l.Contains("Account"),
            $"URL={url} Location={location}");
    }

    // =========================================
    // Auth — Non-admin (Employee)
    // =========================================

    [Theory]
    [InlineData("/Admin/Backups")]
    [InlineData("/Admin/RegistrationRequests")]
    [InlineData("/Admin/PasswordResetRequests")]
    public async Task NonAdmin_RedirectsToAccessDeniedOrLogin(string url)
    {
        await IntegrationTestHelpers.SeedEmployeeWithPermissionsAsync(_factory);
        var client = await IntegrationTestHelpers
            .CreateAuthenticatedEmployeeClientAsync(_factory);

        var response = await client.GetAsync(url);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther);

        var location = response.Headers.Location?.ToString() ?? "";
        location.Should().Match(l =>
            l.Contains("AccessDenied") || l.Contains("Login"),
            $"URL={url} Location={location}");
    }

    // =========================================
    // Backups
    // =========================================

    [Fact]
    public async Task Backups_AsAdmin_Returns200()
    {
        await IntegrationTestHelpers.SeedAdminWithPermissionsAsync(_factory);
        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var response = await client.GetAsync("/Admin/Backups");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateBackup_InMemory_RedirectsBackWithError()
    {
        await IntegrationTestHelpers.SeedAdminWithPermissionsAsync(_factory);
        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var token = await IntegrationTestHelpers
            .GetAntiForgeryTokenFromPageAsync(client, "/Admin/Backups");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync("/Admin/CreateBackup", form);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther);
        response.Headers.Location?.ToString().Should().Contain("Backups");
    }

    [Fact]
    public async Task DownloadBackup_NonExistingFile_ReturnsNotFound()
    {
        await IntegrationTestHelpers.SeedAdminWithPermissionsAsync(_factory);
        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var response = await client.GetAsync(
            "/Admin/DownloadBackup?fileName=nonexistent.bak");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DownloadBackup_PathTraversal_ReturnsNotFound()
    {
        await IntegrationTestHelpers.SeedAdminWithPermissionsAsync(_factory);
        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var response = await client.GetAsync(
            "/Admin/DownloadBackup?fileName=..%2F..%2Fappsettings.json");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // =========================================
    // RegistrationRequests
    // =========================================

    [Fact]
    public async Task RegistrationRequests_Empty_Returns200()
    {
        await IntegrationTestHelpers.SeedAdminWithPermissionsAsync(_factory);
        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var response = await client.GetAsync("/Admin/RegistrationRequests");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RegistrationRequests_FilterCustomers_Returns200()
    {
        await IntegrationTestHelpers.SeedAdminWithPermissionsAsync(_factory);
        await SeedPortalRequestedAsync(customer: "c1", supplier: "s1");

        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var response = await client.GetAsync(
            "/Admin/RegistrationRequests?type=customer");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RegistrationRequests_FilterSuppliers_Returns200()
    {
        await IntegrationTestHelpers.SeedAdminWithPermissionsAsync(_factory);
        await SeedPortalRequestedAsync(customer: "c2", supplier: "s2");

        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var response = await client.GetAsync(
            "/Admin/RegistrationRequests?type=supplier");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =========================================
    // PasswordResetRequests
    // =========================================

    [Fact]
    public async Task PasswordResetRequests_NoFilter_Returns200()
    {
        await IntegrationTestHelpers.SeedAdminWithPermissionsAsync(_factory);
        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var response = await client.GetAsync("/Admin/PasswordResetRequests");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("completed")]
    [InlineData("expired")]
    [InlineData("sent")]
    public async Task PasswordResetRequests_AllFilters_Return200(string filter)
    {
        await IntegrationTestHelpers.SeedAdminWithPermissionsAsync(_factory);
        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var response = await client.GetAsync(
            $"/Admin/PasswordResetRequests?filter={filter}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =========================================
    // MarkAsSent
    // =========================================

    [Fact]
    public async Task MarkAsSent_ExistingRequest_UpdatesAndRedirects()
    {
        await IntegrationTestHelpers.SeedAdminWithPermissionsAsync(_factory);

        int requestId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var req = new PasswordResetRequest
            {
                Phone = "01099990011",
                UserName = "01099990011",
                UserType = UserType.Admin,
                CodeHash = "fake-hash",
                RequestedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                IsSent = false,
                IsCompleted = false
            };
            db.PasswordResetRequests.Add(req);
            await db.SaveChangesAsync();
            requestId = req.Id;
        }

        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var token = await IntegrationTestHelpers
            .GetAntiForgeryTokenFromPageAsync(client, "/Admin/PasswordResetRequests");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync(
            $"/Admin/MarkAsSent/{requestId}", form);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var req = await db.PasswordResetRequests.FindAsync(requestId);

            req!.IsSent.Should().BeTrue();
            req.SentAt.Should().NotBeNull();
            req.SentBy.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public async Task MarkAsSent_NonExisting_ReturnsNotFound()
    {
        await IntegrationTestHelpers.SeedAdminWithPermissionsAsync(_factory);
        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var token = await IntegrationTestHelpers
            .GetAntiForgeryTokenFromPageAsync(client, "/Admin/PasswordResetRequests");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync("/Admin/MarkAsSent/999999", form);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // =========================================
    // Private helpers
    // =========================================

    private async Task SeedPortalRequestedAsync(string customer, string supplier)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        db.Customers.Add(new Customer
        {
            Name = customer,
            Phone = "0108888" + Random.Shared.Next(1000, 9999),
            IsActive = true,
            PortalRequested = true,
            PortalApproved = false,
            HasPortalAccount = false,
            CreatedAt = DateTime.UtcNow
        });

        db.Suppliers.Add(new Supplier
        {
            Name = supplier,
            Phone = "0107777" + Random.Shared.Next(1000, 9999),
            IsActive = true,
            PortalRequested = true,
            PortalApproved = false,
            HasPortalAccount = false,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }
}