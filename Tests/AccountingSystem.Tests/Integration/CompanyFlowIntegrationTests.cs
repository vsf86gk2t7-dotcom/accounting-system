using AccountingSystem.Data;
using AccountingSystem.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace AccountingSystem.Tests.Integration;

public class CompanyFlowIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    private static readonly string[] CompanyPermissions =
    {
        "company.view",
        "company.create",
        "company.edit",
        "company.activate",
        "company.delete"
    };

    public CompanyFlowIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<int> SeedCompanyAsync(string? name = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var company = new Company
        {
            Name = name ?? "شركة " + Guid.NewGuid().ToString("N")[..8],
            Phone = "0225551234",
            Email = "info@test.com",
            Address = "القاهرة",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        db.Companies.Add(company);
        await db.SaveChangesAsync();
        return company.Id;
    }

    [Fact]
    public async Task Index_Returns200()
    {
        await IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
            _factory, CompanyPermissions);
        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var response = await client.GetAsync("/Company/Index");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_Get_Returns200()
    {
        await IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
            _factory, CompanyPermissions);
        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var response = await client.GetAsync("/Company/Create");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

        [Fact]
    public async Task Create_Post_PersistsCompany()
    {
        // ✅ امسح أي شركات موجودة (Controller بيسمح بشركة واحدة بس)
        using (var cleanupScope = _factory.Services.CreateScope())
        {
            var cleanupDb = cleanupScope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();
            cleanupDb.Companies.RemoveRange(cleanupDb.Companies);
            await cleanupDb.SaveChangesAsync();
        }

        await IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
            _factory, CompanyPermissions);
        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var token = await IntegrationTestHelpers
            .GetAntiForgeryTokenFromPageAsync(client, "/Company/Index");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = "شركة جديدة للاختبار",
            ["Phone"] = "0227778888",
            ["Email"] = "new@company.com",
            ["Address"] = "الجيزة",
            ["TaxNumber"] = "TAX-999",
            ["CommercialRegister"] = "CR-999",
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync("/Company/Create", form);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var company = await db.Companies
            .FirstOrDefaultAsync(x => x.Phone == "0227778888");

        company.Should().NotBeNull();
        company!.Name.Should().Contain("شركة جديدة");
    }

    [Fact]
    public async Task Create_EmptyName_Returns200()
    {
        await IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
            _factory, CompanyPermissions);
        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var token = await IntegrationTestHelpers
            .GetAntiForgeryTokenFromPageAsync(client, "/Company/Index");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = "",
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync("/Company/Create", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Edit_Get_Existing_Returns200()
    {
        await IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
            _factory, CompanyPermissions);
        var id = await SeedCompanyAsync();
        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var response = await client.GetAsync($"/Company/Edit/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Edit_Get_NonExisting_ReturnsNotFound()
    {
        await IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
            _factory, CompanyPermissions);
        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var response = await client.GetAsync("/Company/Edit/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Edit_Post_UpdatesCompany()
    {
        await IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
            _factory, CompanyPermissions);
        var id = await SeedCompanyAsync("شركة قديمة");

        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var token = await IntegrationTestHelpers
            .GetAntiForgeryTokenFromPageAsync(client, $"/Company/Edit/{id}");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = id.ToString(),
            ["Name"] = "شركة بعد التعديل",
            ["Phone"] = "0229990000",
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync($"/Company/Edit/{id}", form);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var company = await db.Companies.FindAsync(id);

        company!.Name.Should().Be("شركة بعد التعديل");
    }

    [Fact]
    public async Task ToggleActive_ChangesState()
    {
        await IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
            _factory, CompanyPermissions);
        var id = await SeedCompanyAsync();

        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var token = await IntegrationTestHelpers
            .GetAntiForgeryTokenFromPageAsync(client, "/Company/Index");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync($"/Company/ToggleActive/{id}", form);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var company = await db.Companies.FindAsync(id);

        company!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_CompanyWithoutBranches_RemovesCompany()
    {
        await IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
            _factory, CompanyPermissions);
        var id = await SeedCompanyAsync();

        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var token = await IntegrationTestHelpers
            .GetAntiForgeryTokenFromPageAsync(client, "/Company/Index");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync($"/Company/Delete/{id}", form);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var company = await db.Companies.FindAsync(id);

        company.Should().BeNull();
    }
}