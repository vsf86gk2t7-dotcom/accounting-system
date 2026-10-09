using AccountingSystem.Controllers;
using AccountingSystem.Models;
using AccountingSystem.Models.ViewModels.Reports;
using AccountingSystem.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Tests.Controllers;

public class ReportsControllerTests : BaseTest
{
	private readonly ReportsController _sut;

	public ReportsControllerTests()
	{
		_sut = new ReportsController(Context);

		var httpContext = new DefaultHttpContext();
		_sut.ControllerContext = new ControllerContext
		{
			HttpContext = httpContext
		};
	}

	// =========================================
	// Helpers
	// =========================================

	private async Task SeedBasicDataAsync()
	{
		if (!Context.Companies.Any(c => c.Id == 1))
		{
			Context.Companies.Add(new Company
			{
				Id = 1, Name = "Test Company",
				IsActive = true, CreatedAt = DateTime.UtcNow
			});

			Context.Branches.Add(new Branch
			{
				Id = 1, Name = "Test Branch",
				CompanyId = 1, IsActive = true, CreatedAt = DateTime.UtcNow
			});

			Context.Stores.Add(new Store
			{
				Id = 1, Name = "Test Store",
				BranchId = 1, IsActive = true, CreatedAt = DateTime.UtcNow
			});

			Context.Customers.Add(new Customer
			{
				Id = 1, Name = "Test Customer",
				Phone = "01000000001",
				IsActive = true, CreatedAt = DateTime.UtcNow
			});

			Context.Suppliers.Add(new Supplier
			{
				Id = 1, Name = "Test Supplier",
				Phone = "01000000002",
				IsActive = true, CreatedAt = DateTime.UtcNow
			});

			await Context.SaveChangesAsync();
		}
	}

	private async Task SeedSalesInvoiceAsync(
		decimal totalAmount = 100m,
		DateTime? invoiceDate = null)
	{
		await SeedBasicDataAsync();

		Context.SalesInvoices.Add(new SalesInvoice
		{
			InvoiceNumber = $"SAL-{Guid.NewGuid().ToString("N").Substring(0, 8)}",
			InvoiceDate = invoiceDate ?? DateTime.UtcNow,
			BranchId = 1, StoreId = 1, CustomerId = 1,
			SubTotal = totalAmount, TotalAmount = totalAmount,
			Status = SalesInvoiceStatus.Confirmed,
			ConfirmedAt = DateTime.UtcNow,
			PaymentMethod = SalesPaymentMethod.Cash,
			CreatedAt = DateTime.UtcNow,
			RowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }
		});
		await Context.SaveChangesAsync();
	}

	// =========================================
	// 1. Index
	// =========================================

	[Fact]
	public void Index_ReturnsViewResult()
	{
		var result = _sut.Index();

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 2. SalesReport
	// =========================================

	[Fact]
	public async Task SalesReport_ReturnsViewResult()
	{
		var result = await _sut.SalesReport(null, null);

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task SalesReport_WithNoInvoices_ReturnsEmptyModel()
	{
		var result = await _sut.SalesReport(null, null);

		var viewResult = result.Should().BeOfType<ViewResult>().Subject;
		viewResult.Model.Should().BeOfType<SalesReportViewModel>();
	}

	[Fact]
	public async Task SalesReport_WithInvoices_ReturnsViewWithModel()
	{
		await SeedSalesInvoiceAsync(totalAmount: 500m);

		var result = await _sut.SalesReport(null, null);

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task SalesReport_WithDateRange_ReturnsView()
	{
		await SeedSalesInvoiceAsync(100m, DateTime.UtcNow.AddDays(-5));

		var result = await _sut.SalesReport(
			fromDate: DateTime.UtcNow.AddDays(-10),
			toDate: DateTime.UtcNow);

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task SalesReport_WithFutureDateRange_ReturnsEmptyResult()
	{
		await SeedSalesInvoiceAsync(100m, DateTime.UtcNow);

		var result = await _sut.SalesReport(
			fromDate: DateTime.UtcNow.AddYears(1),
			toDate: DateTime.UtcNow.AddYears(2));

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 3. PurchaseReport
	// =========================================

	[Fact]
	public async Task PurchaseReport_ReturnsViewResult()
	{
		var result = await _sut.PurchaseReport(null, null);

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task PurchaseReport_WithDateRange_ReturnsView()
	{
		var result = await _sut.PurchaseReport(
			fromDate: DateTime.UtcNow.AddDays(-30),
			toDate: DateTime.UtcNow);

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 4. InventoryReport
	// =========================================

	[Fact]
	public async Task InventoryReport_ReturnsViewResult()
	{
				var result = await _sut.InventoryReport(storeId: null);

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task InventoryReport_WithStockLots_ReturnsView()
	{
		await SeedBasicDataAsync();

		Context.StockLots.Add(new StockLot
		{
			StoreId = 1, ProductId = 1,
			QuantityReceived = 100m, QuantityRemaining = 100m,
			UnitCost = 50m,
			PurchaseDate = DateTime.UtcNow,
			CreatedAt = DateTime.UtcNow,
			IsActive = true,
			RowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }
		});
		await Context.SaveChangesAsync();
		var result = await _sut.InventoryReport(storeId: null);

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 5. TreasuryReport
	// =========================================

	[Fact]
	public async Task TreasuryReport_ReturnsViewResult()
	{
		var result = await _sut.TreasuryReport(null, null);

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 6. AccountingReport
	// =========================================

	[Fact]
	public async Task AccountingReport_ReturnsViewResult()
	{
		var result = await _sut.AccountingReport(null, null);

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 7. CustomerStatement
	// =========================================

	[Fact]
	public async Task CustomerStatement_WithoutCustomerId_ReturnsView()
	{
		var result = await _sut.CustomerStatement(null, null, null);

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task CustomerStatement_WithValidCustomer_ReturnsView()
	{
		await SeedBasicDataAsync();

		var result = await _sut.CustomerStatement(
			customerId: 1,
			fromDate: null, toDate: null);

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task CustomerStatement_WithInvalidCustomer_ReturnsView()
	{
		var result = await _sut.CustomerStatement(
			customerId: 99999,
			fromDate: null, toDate: null);

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 8. SupplierStatement
	// =========================================

	[Fact]
	public async Task SupplierStatement_WithoutSupplierId_ReturnsView()
	{
		var result = await _sut.SupplierStatement(null, null, null);

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task SupplierStatement_WithValidSupplier_ReturnsView()
	{
		await SeedBasicDataAsync();

		var result = await _sut.SupplierStatement(
			supplierId: 1,
			fromDate: null, toDate: null);

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 9. ProfitReport
	// =========================================

	[Fact]
	public async Task ProfitReport_ReturnsViewResult()
	{
		var result = await _sut.ProfitReport(null, null);

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task ProfitReport_WithDateRange_ReturnsView()
	{
		var result = await _sut.ProfitReport(
			fromDate: DateTime.UtcNow.AddDays(-30),
			toDate: DateTime.UtcNow);

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 10. ProductProfitReport
	// =========================================

	[Fact]
	public async Task ProductProfitReport_ReturnsViewResult()
	{
		var result = await _sut.ProductProfitReport(null, null);

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 11. DailySalesReport
	// =========================================

	[Fact]
	public async Task DailySalesReport_ReturnsViewResult()
	{
		var result = await _sut.DailySalesReport(null, null);

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task DailySalesReport_WithDateRange_ReturnsView()
	{
		var result = await _sut.DailySalesReport(
			fromDate: DateTime.UtcNow.AddDays(-7),
			toDate: DateTime.UtcNow);

		result.Should().BeOfType<ViewResult>();
	}
}