using AccountingSystem.Data;
using AccountingSystem.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace AccountingSystem.Tests.Integration;

public class ShippingFlowIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
	private readonly CustomWebApplicationFactory _factory;

	public ShippingFlowIntegrationTests(CustomWebApplicationFactory factory)
	{
		_factory = factory;

		IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
			_factory,
			"shipping.view",
			"shipping.create",
			"shipping.edit",
			// صلاحيات البيع (لإنشاء الفاتورة)
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

		var response = await client.GetAsync("/Shipping/Index");

		var body = await response.Content.ReadAsStringAsync();

		response.StatusCode.Should().Be(
			HttpStatusCode.OK,
			$"Status={response.StatusCode}, " +
			$"Location={response.Headers.Location}, " +
			$"Body={body.Substring(0, Math.Min(800, body.Length))}");
	}

	// 2. Create GET
	[Fact]
	public async Task Create_Get_Returns200()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var response = await client.GetAsync("/Shipping/Create");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	// 3. Create POST — InternalDriver
	[Fact]
	public async Task Create_Post_InternalDriver_PersistsShippingBill()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var baseData = await ShippingTestHelpers
			.SeedShippingBaseDataAsync(_factory, client);

		var billId = await ShippingTestHelpers
			.CreateShippingBillViaHttpAsync(
				_factory, client, baseData,
				deliveryMethod: DeliveryMethod.InternalDriver,
				notes: "مندوب داخلي");

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var bill = await db.ShippingBills.FindAsync(billId);

		bill.Should().NotBeNull();
		bill!.SalesInvoiceId.Should().Be(baseData.InvoiceId);
		bill.DeliveryMethod.Should().Be(DeliveryMethod.InternalDriver);
		bill.DriverId.Should().Be(baseData.DriverId);
		bill.ShippingCompanyId.Should().BeNull();
		bill.Status.Should().Be(ShippingBillStatus.Pending);
		bill.Notes.Should().Be("مندوب داخلي");
		bill.BillNumber.Should().NotBeNullOrWhiteSpace();
	}

	// 4. Create POST — ExternalCompany
	[Fact]
	public async Task Create_Post_ExternalCompany_PersistsShippingBill()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var baseData = await ShippingTestHelpers
			.SeedShippingBaseDataAsync(_factory, client);

		var billId = await ShippingTestHelpers
			.CreateShippingBillViaHttpAsync(
				_factory, client, baseData,
				deliveryMethod: DeliveryMethod.ExternalCompany,
				notes: "شركة خارجية");

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var bill = await db.ShippingBills.FindAsync(billId);

		bill.Should().NotBeNull();
		bill!.DeliveryMethod.Should().Be(DeliveryMethod.ExternalCompany);
		bill.ShippingCompanyId.Should().Be(baseData.ShippingCompanyId);
		bill.DriverId.Should().BeNull();
		bill.Status.Should().Be(ShippingBillStatus.Pending);
	}

	// 5. Details
	[Fact]
	public async Task Details_Returns200()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var baseData = await ShippingTestHelpers
			.SeedShippingBaseDataAsync(_factory, client);

		var billId = await ShippingTestHelpers
			.CreateShippingBillViaHttpAsync(
				_factory, client, baseData);

		var response = await client.GetAsync($"/Shipping/Details/{billId}");

		response.StatusCode.Should().Be(HttpStatusCode.OK);
	}

	// 6. UpdateStatus — Cancelled (لتفادي StockLot deduction)
	[Fact]
	public async Task UpdateStatus_ToCancelled_ChangesStatus()
	{
		var client = await IntegrationTestHelpers
			.CreateAuthenticatedClientAsync(_factory);

		var baseData = await ShippingTestHelpers
			.SeedShippingBaseDataAsync(_factory, client);

		var billId = await ShippingTestHelpers
			.CreateShippingBillViaHttpAsync(
				_factory, client, baseData);

		// احصل على التوكن من صفحة Details
		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(
				client, $"/Shipping/Details/{billId}");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["status"] = ((int)ShippingBillStatus.Cancelled).ToString(),
			["__RequestVerificationToken"] = token
		});

		var response = await client.PostAsync(
			$"/Shipping/UpdateStatus/{billId}", form);

		response.StatusCode.Should().BeOneOf(
			new[] { HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther },
			$"Status={response.StatusCode}, Location={response.Headers.Location}");

		using var scope = _factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var bill = await db.ShippingBills.FindAsync(billId);

		bill!.Status.Should().Be(ShippingBillStatus.Cancelled);
	}
}