using AccountingSystem.Data;
using AccountingSystem.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace AccountingSystem.Tests.Integration;

public class PurchaseInvoiceFlowIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    private static readonly string[] AllPurchasePermissions =
    {
        "purchase.view",
        "purchase.create",
        "purchase.confirm",
        "purchase.cancel"
    };

    public PurchaseInvoiceFlowIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;

        IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
                _factory, AllPurchasePermissions)
                .GetAwaiter().GetResult();
    }

    // =========================================
    // Index
    // =========================================

    [Fact]
    public async Task Index_Returns200()
    {
        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var response = await client.GetAsync("/PurchaseInvoice/Index");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =========================================
    // Create GET
    // =========================================

    [Fact]
    public async Task Create_Get_Returns200()
    {
        await PurchaseInvoiceTestHelpers.SeedPurchaseBaseDataAsync(_factory);

        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var response = await client.GetAsync("/PurchaseInvoice/Create");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =========================================
    // Create POST
    // =========================================

    [Fact]
    public async Task Create_Post_PersistsDraftInvoice()
    {
        var baseData = await PurchaseInvoiceTestHelpers
            .SeedPurchaseBaseDataAsync(_factory);

        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var invoiceId = await PurchaseInvoiceTestHelpers
            .CreateDraftPurchaseInvoiceViaHttpAsync(_factory, baseData, client);

        invoiceId.Should().BeGreaterThan(0);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var invoice = await db.PurchaseInvoices
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == invoiceId);

        invoice.Should().NotBeNull();
        invoice!.Items.Should().HaveCount(1);
        invoice.TotalAmount.Should().BeGreaterThan(0);
    }

    // =========================================
    // Details
    // =========================================

    [Fact]
    public async Task Details_ExistingInvoice_Returns200()
    {
        var baseData = await PurchaseInvoiceTestHelpers
            .SeedPurchaseBaseDataAsync(_factory);

        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var invoiceId = await PurchaseInvoiceTestHelpers
            .CreateDraftPurchaseInvoiceViaHttpAsync(_factory, baseData, client);

        var response = await client.GetAsync(
            $"/PurchaseInvoice/Details/{invoiceId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Details_NonExisting_ReturnsNotFound()
    {
        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var response = await client.GetAsync("/PurchaseInvoice/Details/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // =========================================
    // Post (confirm)
    // =========================================

    [Fact]
    public async Task Post_DraftInvoice_ChangesStatusToPosted()
    {
        var baseData = await PurchaseInvoiceTestHelpers
            .SeedPurchaseBaseDataAsync(_factory);

        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var invoiceId = await PurchaseInvoiceTestHelpers
            .CreateDraftPurchaseInvoiceViaHttpAsync(_factory, baseData, client);

        var token = await IntegrationTestHelpers
            .GetAntiForgeryTokenFromPageAsync(
                client,
                $"/PurchaseInvoice/Details/{invoiceId}");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = invoiceId.ToString(),
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync(
            $"/PurchaseInvoice/Post/{invoiceId}", form);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var invoice = await db.PurchaseInvoices.FindAsync(invoiceId);

        invoice!.Status.Should().Be(PurchaseInvoiceStatus.Posted);
    }

    [Fact]
    public async Task Post_NonExisting_ReturnsNotFound()
    {
        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var token = await IntegrationTestHelpers
            .GetAntiForgeryTokenFromPageAsync(client, "/PurchaseInvoice/Index");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = "999999",
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync("/PurchaseInvoice/Post/999999", form);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // =========================================
    // Cancel
    // =========================================

    [Fact]
    public async Task Cancel_DraftInvoice_ChangesStatusToCancelled()
    {
        var baseData = await PurchaseInvoiceTestHelpers
            .SeedPurchaseBaseDataAsync(_factory);

        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var invoiceId = await PurchaseInvoiceTestHelpers
            .CreateDraftPurchaseInvoiceViaHttpAsync(_factory, baseData, client);

        var token = await IntegrationTestHelpers
            .GetAntiForgeryTokenFromPageAsync(
                client,
                $"/PurchaseInvoice/Details/{invoiceId}");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync(
            $"/PurchaseInvoice/Cancel/{invoiceId}", form);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var invoice = await db.PurchaseInvoices.FindAsync(invoiceId);

        invoice!.Status.Should().Be(PurchaseInvoiceStatus.Cancelled);
    }

    // =========================================
    // TaxReport
    // =========================================

    [Fact]
    public async Task TaxReport_NoFilter_Returns200()
    {
        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var response = await client.GetAsync("/PurchaseInvoice/TaxReport");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TaxReport_WithDateRange_Returns200()
    {
        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var from = DateTime.Today.AddDays(-30).ToString("yyyy-MM-dd");
        var to = DateTime.Today.ToString("yyyy-MM-dd");

        var response = await client.GetAsync(
            $"/PurchaseInvoice/TaxReport?fromDate={from}&toDate={to}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =========================================
    // GetSupplierInfo (JSON)
    // =========================================

    [Fact]
    public async Task GetSupplierInfo_ExistingSupplier_Returns200()
    {
        var baseData = await PurchaseInvoiceTestHelpers
            .SeedPurchaseBaseDataAsync(_factory);

        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var response = await client.GetAsync(
            $"/PurchaseInvoice/GetSupplierInfo/{baseData.SupplierId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =========================================
    // Anonymous
    // =========================================

    [Theory]
    [InlineData("/PurchaseInvoice/Index")]
    [InlineData("/PurchaseInvoice/Create")]
    [InlineData("/PurchaseInvoice/TaxReport")]
    public async Task Anonymous_RedirectsToLogin(string url)
    {
        var client = IntegrationTestHelpers.CreateClient(_factory);
        var response = await client.GetAsync(url);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther);

        response.Headers.Location?.ToString().Should()
            .Match(l => l.Contains("Login") || l.Contains("Account"),
                $"URL={url}");
    }
}