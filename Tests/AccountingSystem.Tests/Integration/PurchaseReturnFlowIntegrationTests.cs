using AccountingSystem.Data;
using AccountingSystem.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace AccountingSystem.Tests.Integration;

public class PurchaseReturnFlowIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
	private readonly CustomWebApplicationFactory _factory;

	public PurchaseReturnFlowIntegrationTests(CustomWebApplicationFactory factory)
	{
		_factory = factory;

		IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
			_factory,
			"purchase.return.view",
			"purchase.return.create")
			.GetAwaiter().GetResult();
	}

	// 1. Index
	[Fact]
	public async Task Index_Returns200()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/PurchaseReturn/Index");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	// 2. Create GET
	[Fact]
	public async Task Create_Get_Returns200()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/PurchaseReturn/Create");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	// 3. Create POST — يحفظ المرتجع
	[Fact]
	public async Task Create_Post_PersistsPurchaseReturnInvoice()
	{
		var baseData = await PurchaseReturnTestHelpers
			.SeedPurchaseReturnBaseDataAsync(_factory, stockQuantity: 100);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var quantity = 10m;
		var invoiceId = await PurchaseReturnTestHelpers
			.CreatePurchaseReturnViaHttpAsync(
				_factory, client, baseData,
				quantity: quantity,
				reason: "منتج تالف");

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var invoice = await db.PurchaseReturnInvoices
			.Include(x => x.Items)
			.FirstOrDefaultAsync(x => x.Id == invoiceId);

		invoice.Should().NotBeNull();
		invoice!.SupplierId.Should().Be(baseData.SupplierId);
		invoice.StoreId.Should().Be(baseData.StoreId);
		invoice.Reason.Should().Be("منتج تالف");
		invoice.Items.Should().HaveCount(1);

		var item = invoice.Items.First();
		item.ProductId.Should().Be(baseData.ProductId);
		item.Quantity.Should().Be(quantity);
	}

	// 4. Create POST — يخصم من StockLot
	[Fact]
	public async Task Create_Post_DeductsStockLotQuantity()
	{
		var baseData = await PurchaseReturnTestHelpers
			.SeedPurchaseReturnBaseDataAsync(_factory, stockQuantity: 100);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		await PurchaseReturnTestHelpers
			.CreatePurchaseReturnViaHttpAsync(
				_factory, client, baseData,
				quantity: 30);

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var lot = await db.StockLots.FindAsync(baseData.StockLotId);

		lot.Should().NotBeNull();
		lot!.QuantityReceived.Should().Be(100);  // ما اتغيرش
		lot.QuantityRemaining.Should().Be(70);   // 100 - 30
	}

	// 5. Create POST — بدون Items
	[Fact]
	public async Task Create_WithoutItems_ReturnsViewWithError()
	{
		var baseData = await PurchaseReturnTestHelpers
			.SeedPurchaseReturnBaseDataAsync(_factory);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/PurchaseReturn/Create");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["SupplierId"] = baseData.SupplierId.ToString(),
			["StoreId"] = baseData.StoreId.ToString(),
			["ReturnDate"] = DateTime.Today.ToString("yyyy-MM-dd"),
			["Reason"] = "بدون بنود",
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/PurchaseReturn/Create", form);

		response.StatusCode.Should().Be(HttpStatusCode.OK);

		// تأكد إن مفيش مرتجع اتحفظ
		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var any = await db.PurchaseReturnInvoices
			.AnyAsync(x => x.Reason == "بدون بنود");

		any.Should().BeFalse();
	}

	// 6. Create POST — Supplier غير موجود
	[Fact]
	public async Task Create_WithInvalidSupplier_ReturnsViewWithError()
	{
		var baseData = await PurchaseReturnTestHelpers
			.SeedPurchaseReturnBaseDataAsync(_factory);

		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/PurchaseReturn/Create");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["SupplierId"] = "999999",
			["StoreId"] = baseData.StoreId.ToString(),
			["ReturnDate"] = DateTime.Today.ToString("yyyy-MM-dd"),
			["Reason"] = "مورد وهمي",
			["Items.Index"] = "0",
			["Items[0].ProductId"] = baseData.ProductId.ToString(),
			["Items[0].Quantity"] = "5",
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync("/PurchaseReturn/Create", form);

		response.StatusCode.Should().Be(HttpStatusCode.OK);

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var any = await db.PurchaseReturnInvoices
			.AnyAsync(x => x.Reason == "مورد وهمي");

		any.Should().BeFalse();
	}
}