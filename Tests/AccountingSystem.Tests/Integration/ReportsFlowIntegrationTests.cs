using AccountingSystem.Data;
using AccountingSystem.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace AccountingSystem.Tests.Integration;

public class ReportsFlowIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    private static readonly string[] AllReportPermissions =
    {
        "report.sales",
        "report.purchase",
        "report.inventory",
        "report.treasury",
        "report.accounting",
        "report.customer.statement",
        "report.supplier.statement"
    };

    public ReportsFlowIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;

        // ✅ Seed ONCE بكل الصلاحيات — كل الاختبارات تستخدم نفس الأدمن
        IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
                _factory, AllReportPermissions)
                .GetAwaiter().GetResult();
    }

    // =========================================
    // Seed helpers
    // =========================================

    private async Task<int> SeedCustomerAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var customer = new Customer
        {
            Name = "عميل تقرير " + Guid.NewGuid().ToString("N")[..6],
            Phone = "011" + Random.Shared.Next(10000000, 99999999),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return customer.Id;
    }

    private async Task<int> SeedSupplierAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var supplier = new Supplier
        {
            Name = "مورد تقرير " + Guid.NewGuid().ToString("N")[..6],
            Phone = "012" + Random.Shared.Next(10000000, 99999999),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();
        return supplier.Id;
    }

    // =========================================
    // Index
    // =========================================

    [Fact]
    public async Task Index_Returns200()
    {
        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var response = await client.GetAsync("/Reports/Index");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =========================================
    // All reports — Theory
    // =========================================

    [Theory]
    [InlineData("/Reports/SalesReport")]
    [InlineData("/Reports/PurchaseReport")]
    [InlineData("/Reports/InventoryReport")]
    [InlineData("/Reports/TreasuryReport")]
    [InlineData("/Reports/AccountingReport")]
    [InlineData("/Reports/CustomerBalancesReport")]
    [InlineData("/Reports/CustomerStatement")]
    [InlineData("/Reports/SupplierBalancesReport")]
    [InlineData("/Reports/SupplierStatement")]
    [InlineData("/Reports/ProfitReport")]
    [InlineData("/Reports/ProductProfitReport")]
    [InlineData("/Reports/DailySalesReport")]
    public async Task Report_WithAllPermissions_Returns200(string url)
    {
        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var response = await client.GetAsync(url);

        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            $"URL={url} Location={response.Headers.Location}");
    }

    // =========================================
    // Date range variants
    // =========================================

    [Theory]
    [InlineData("/Reports/SalesReport")]
    [InlineData("/Reports/PurchaseReport")]
    [InlineData("/Reports/ProfitReport")]
    [InlineData("/Reports/DailySalesReport")]
    public async Task Report_WithDateRange_Returns200(string url)
    {
        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var from = DateTime.Today.AddDays(-30).ToString("yyyy-MM-dd");
        var to = DateTime.Today.ToString("yyyy-MM-dd");

        var response = await client.GetAsync(
            $"{url}?fromDate={from}&toDate={to}");

        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            $"URL={url} Location={response.Headers.Location}");
    }

    // =========================================
    // Filters
    // =========================================

    [Fact]
    public async Task InventoryReport_WithStoreFilter_Returns200()
    {
        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var response = await client.GetAsync(
            "/Reports/InventoryReport?storeId=1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CustomerStatement_WithCustomerId_Returns200()
    {
        var customerId = await SeedCustomerAsync();

        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var response = await client.GetAsync(
            $"/Reports/CustomerStatement?customerId={customerId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SupplierStatement_WithSupplierId_Returns200()
    {
        var supplierId = await SeedSupplierAsync();

        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var response = await client.GetAsync(
            $"/Reports/SupplierStatement?supplierId={supplierId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =========================================
    // Anonymous
    // =========================================

    [Theory]
    [InlineData("/Reports/Index")]
    [InlineData("/Reports/SalesReport")]
    [InlineData("/Reports/PurchaseReport")]
    [InlineData("/Reports/InventoryReport")]
    [InlineData("/Reports/TreasuryReport")]
    [InlineData("/Reports/AccountingReport")]
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