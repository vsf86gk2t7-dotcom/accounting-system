using AccountingSystem.Data;
using AccountingSystem.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace AccountingSystem.Tests.Integration;

public class SalesInvoiceFlowIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    private static readonly string[] AllSalesPermissions =
    {
        "sales.view",
        "sales.create",
        "sales.edit",
        "sales.confirm",
        "sales.cancel"
    };

    public SalesInvoiceFlowIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;

        // ✅ Seed once — كل الاختبارات تستخدم نفس الأدمن
        IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
                _factory, AllSalesPermissions)
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

        var response = await client.GetAsync("/SalesInvoice/Index");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =========================================
    // Create GET
    // =========================================

    [Fact]
    public async Task Create_Get_Returns200()
    {
        await SalesInvoiceTestHelpers.SeedInvoiceBaseDataAsync(_factory);

        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var response = await client.GetAsync("/SalesInvoice/Create");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =========================================
    // Pos GET
    // =========================================

    [Fact]
    public async Task Pos_Get_Returns200()
    {
        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var response = await client.GetAsync("/SalesInvoice/Pos");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =========================================
    // Create POST — draft
    // =========================================

    [Fact]
    public async Task Create_Post_PersistsDraftInvoice()
    {
        var baseData = await SalesInvoiceTestHelpers
            .SeedInvoiceBaseDataAsync(_factory);

        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var invoiceId = await SalesInvoiceTestHelpers
            .CreateDraftInvoiceViaHttpAsync(_factory, baseData, client);

        invoiceId.Should().BeGreaterThan(0);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var invoice = await db.SalesInvoices
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == invoiceId);

        invoice.Should().NotBeNull();
        invoice!.Status.Should().Be(SalesInvoiceStatus.Draft);
        invoice.Items.Should().HaveCount(1);
    }

    // =========================================
    // Details
    // =========================================

    [Fact]
    public async Task Details_ExistingInvoice_Returns200()
    {
        var baseData = await SalesInvoiceTestHelpers
            .SeedInvoiceBaseDataAsync(_factory);

        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var invoiceId = await SalesInvoiceTestHelpers
            .CreateDraftInvoiceViaHttpAsync(_factory, baseData, client);

        var response = await client.GetAsync($"/SalesInvoice/Details/{invoiceId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Details_NonExisting_ReturnsNotFound()
    {
        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var response = await client.GetAsync("/SalesInvoice/Details/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // =========================================
    // Confirm — الأهم
    // =========================================

    [Fact]
    public async Task Confirm_DraftInvoice_ChangesStatusToConfirmed()
    {
        var baseData = await SalesInvoiceTestHelpers
            .SeedInvoiceBaseDataAsync(_factory);

        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var invoiceId = await SalesInvoiceTestHelpers
            .CreateDraftInvoiceViaHttpAsync(_factory, baseData, client);

        var token = await IntegrationTestHelpers
            .GetAntiForgeryTokenFromPageAsync(client, $"/SalesInvoice/Details/{invoiceId}");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = invoiceId.ToString(),
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync($"/SalesInvoice/Confirm/{invoiceId}", form);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var invoice = await db.SalesInvoices.FindAsync(invoiceId);

        invoice!.Status.Should().Be(SalesInvoiceStatus.Confirmed);
    }

    // =========================================
    // Cancel
    // =========================================

    [Fact]
    public async Task Cancel_DraftInvoice_ChangesStatusToCancelled()
    {
        var baseData = await SalesInvoiceTestHelpers
            .SeedInvoiceBaseDataAsync(_factory);

        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var invoiceId = await SalesInvoiceTestHelpers
            .CreateDraftInvoiceViaHttpAsync(_factory, baseData, client);

        var token = await IntegrationTestHelpers
            .GetAntiForgeryTokenFromPageAsync(client, $"/SalesInvoice/Details/{invoiceId}");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync($"/SalesInvoice/Cancel/{invoiceId}", form);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var invoice = await db.SalesInvoices.FindAsync(invoiceId);

        invoice!.Status.Should().Be(SalesInvoiceStatus.Cancelled);
    }

    // =========================================
    // Edit
    // =========================================

    [Fact]
    public async Task Edit_Get_DraftInvoice_Returns200()
    {
        var baseData = await SalesInvoiceTestHelpers
            .SeedInvoiceBaseDataAsync(_factory);

        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var invoiceId = await SalesInvoiceTestHelpers
            .CreateDraftInvoiceViaHttpAsync(_factory, baseData, client);

        var response = await client.GetAsync($"/SalesInvoice/Edit/{invoiceId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =========================================
    // GetAvailableLots (JSON)
    // =========================================

    [Fact]
    public async Task GetAvailableLots_WithStockLot_Returns200()
    {
        var baseData = await SalesInvoiceTestHelpers
            .SeedInvoiceBaseDataAsync(_factory);

        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var response = await client.GetAsync(
            $"/SalesInvoice/GetAvailableLots?productId={baseData.ProductId}&storeId={baseData.StoreId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =========================================
    // GetCustomerInfo (JSON)
    // =========================================

    [Fact]
    public async Task GetCustomerInfo_ExistingCustomer_Returns200()
    {
        var baseData = await SalesInvoiceTestHelpers
            .SeedInvoiceBaseDataAsync(_factory);

        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var response = await client.GetAsync(
            $"/SalesInvoice/GetCustomerInfo/{baseData.CustomerId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =========================================
    // Anonymous
    // =========================================

    [Theory]
    [InlineData("/SalesInvoice/Index")]
    [InlineData("/SalesInvoice/Create")]
    [InlineData("/SalesInvoice/Pos")]
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