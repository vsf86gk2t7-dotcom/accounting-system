using AccountingSystem.Data;
using AccountingSystem.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace AccountingSystem.Tests.Integration;

public class SalesReturnFlowIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
	private readonly CustomWebApplicationFactory _factory;

	public SalesReturnFlowIntegrationTests(CustomWebApplicationFactory factory)
	{
		_factory = factory;

		IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
			_factory,
			"sales.return.view",
			"sales.return.create",
			"sales.view",
			"sales.create",
			"sales.confirm")
			.GetAwaiter().GetResult();
	}

	// 1. Index
	[Fact]
	public async Task Index_Returns200()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/SalesReturn/Index");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	// 2. Create GET
	[Fact]
	public async Task Create_Get_Returns200()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/SalesReturn/Create");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	// 3. Create POST — يحفظ المرتجع
	[Fact]
	public async Task Create_Post_PersistsSalesReturn()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var baseData = await SalesReturnTestHelpers
			.SeedConfirmedInvoiceAsync(_factory, client, quantity: 10);

				var returnQuantity = baseData.SoldQuantity > 0
			? Math.Min(1m, baseData.SoldQuantity)
			: 1m;
		var returnId = await SalesReturnTestHelpers
			.CreateSalesReturnViaHttpAsync(
				_factory, client, baseData,
				quantity: returnQuantity,
				reason: "منتج به عيب");

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var returnInvoice = await db.SalesReturnInvoices
			.Include(x => x.Items)
			.FirstOrDefaultAsync(x => x.Id == returnId);

		returnInvoice.Should().NotBeNull();
		returnInvoice!.OriginalSalesInvoiceId.Should().Be(baseData.InvoiceId);
		returnInvoice.CustomerId.Should().Be(baseData.CustomerId);
		returnInvoice.StoreId.Should().Be(baseData.StoreId);
		returnInvoice.Reason.Should().Be("منتج به عيب");
		returnInvoice.Items.Should().HaveCount(1);

		var item = returnInvoice.Items.First();
		item.ProductId.Should().Be(baseData.ProductId);
		item.SalesInvoiceItemId.Should().Be(baseData.SalesInvoiceItemId);
		item.Quantity.Should().Be(returnQuantity);
	}

	// 4. Create POST — بدون فاتورة أصلية
	[Fact]
	public async Task Create_WithoutOriginalInvoice_ReturnsViewWithError()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/SalesReturn/Create");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["OriginalSalesInvoiceId"] = string.Empty,   // ⬅️ فاضي
			["CustomerId"] = "1",
			["StoreId"] = "1",
			["ReturnDate"] = DateTime.Today.ToString("yyyy-MM-dd"),
			["Reason"] = "بدون فاتورة",
			["RefundToCash"] = "false",
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/SalesReturn/Create", form);

		response.StatusCode.Should().Be(HttpStatusCode.OK);

		// تأكد مفيش مرتجع اتحفظ
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var any = await db.SalesReturnInvoices
			.AnyAsync(x => x.Reason == "بدون فاتورة");

		any.Should().BeFalse();
	}

	// 5. Create POST — بدون بنود
	[Fact]
	public async Task Create_WithoutItems_ReturnsViewWithError()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var baseData = await SalesReturnTestHelpers
			.SeedConfirmedInvoiceAsync(_factory, client);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/SalesReturn/Create");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["OriginalSalesInvoiceId"] = baseData.InvoiceId.ToString(),
			["CustomerId"] = baseData.CustomerId.ToString(),
			["StoreId"] = baseData.StoreId.ToString(),
			["ReturnDate"] = DateTime.Today.ToString("yyyy-MM-dd"),
			["Reason"] = "بدون بنود",
			["RefundToCash"] = "false",
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/SalesReturn/Create", form);

		response.StatusCode.Should().Be(HttpStatusCode.OK);

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var any = await db.SalesReturnInvoices
			.AnyAsync(x => x.Reason == "بدون بنود");

		any.Should().BeFalse();
	}

	// 6. Create POST — RefundToCash بدون CashAccount
	[Fact]
	public async Task Create_RefundToCashWithoutAccount_ReturnsViewWithError()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var baseData = await SalesReturnTestHelpers
			.SeedConfirmedInvoiceAsync(_factory, client);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/SalesReturn/Create");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["OriginalSalesInvoiceId"] = baseData.InvoiceId.ToString(),
			["CustomerId"] = baseData.CustomerId.ToString(),
			["StoreId"] = baseData.StoreId.ToString(),
			["ReturnDate"] = DateTime.Today.ToString("yyyy-MM-dd"),
			["Reason"] = "بدون خزنة",
			["RefundToCash"] = "true",
			["RefundCashAccountId"] = string.Empty,   // ⬅️ فاضي
			["Items.Index"] = "0",
			["Items[0].SalesInvoiceItemId"] = baseData.SalesInvoiceItemId.ToString(),
			["Items[0].ProductId"] = baseData.ProductId.ToString(),
			["Items[0].UnitId"] = baseData.UnitId.ToString(),
			["Items[0].ConversionFactor"] = "1",
			["Items[0].Quantity"] = "1",
			["Items[0].UnitPrice"] = "100",
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/SalesReturn/Create", form);

		response.StatusCode.Should().Be(HttpStatusCode.OK);

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var any = await db.SalesReturnInvoices
			.AnyAsync(x => x.Reason == "بدون خزنة");

		any.Should().BeFalse();
	}
}