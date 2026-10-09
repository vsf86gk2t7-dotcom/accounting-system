using AccountingSystem.Controllers;
using AccountingSystem.Models;
using AccountingSystem.Models.ViewModels.Shipping;
using AccountingSystem.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AccountingSystem.Tests.Controllers;

public class ShippingControllerTests : BaseTest
{
	private readonly ShippingController _sut;

	public ShippingControllerTests()
	{
		_sut = new ShippingController(Context);

		var httpContext = new DefaultHttpContext();
		_sut.ControllerContext = new ControllerContext { HttpContext = httpContext };
		_sut.TempData = new TempDataDictionary(
			httpContext, Mock.Of<ITempDataProvider>());
	}

	private async Task<ShippingBill> SeedShippingBillAsync()
	{
		var invoice = new SalesInvoice
		{
			InvoiceNumber = $"SAL-{Guid.NewGuid().ToString("N").Substring(0, 8)}",
			InvoiceDate = DateTime.UtcNow,
			BranchId = 1, StoreId = 1, CustomerId = 1,
			SubTotal = 100m, TotalAmount = 100m,
			Status = SalesInvoiceStatus.Confirmed,
			ConfirmedAt = DateTime.UtcNow,
			PaymentMethod = SalesPaymentMethod.Cash,
			CreatedAt = DateTime.UtcNow,
			RowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }
		};
		Context.SalesInvoices.Add(invoice);
		await Context.SaveChangesAsync();

		var bill = new ShippingBill
		{
			BillNumber = $"SB-{Guid.NewGuid().ToString("N").Substring(0, 8)}",
			SalesInvoiceId = invoice.Id,
			BillDate = DateTime.UtcNow,
			Status = ShippingBillStatus.Pending,
			DeliveryMethod = DeliveryMethod.InternalDriver
		};
		Context.ShippingBills.Add(bill);
		await Context.SaveChangesAsync();
		return bill;
	}

	[Fact]
	public async Task Index_ReturnsViewResult()
	{
		var result = await _sut.Index(search: null, status: null);
		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Index_WithSearch_ReturnsView()
	{
		var result = await _sut.Index(search: "test", status: null);
		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Index_WithStatusFilter_ReturnsView()
	{
		var result = await _sut.Index(search: null, status: ShippingBillStatus.Pending);
		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Details_WhenNotFound_ReturnsNotFound()
	{
		var result = await _sut.Details(id: 99999);
		result.Should().BeOfType<NotFoundResult>();
	}

	[Fact]
	public async Task UpdateStatus_WhenNotFound_ReturnsNotFound()
	{
		var result = await _sut.UpdateStatus(id: 99999, status: ShippingBillStatus.Delivered);
		result.Should().BeOfType<NotFoundResult>();
	}
}
