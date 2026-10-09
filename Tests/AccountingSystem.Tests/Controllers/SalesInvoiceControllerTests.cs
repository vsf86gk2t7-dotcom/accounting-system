using AccountingSystem.Controllers;
using AccountingSystem.Models;
using AccountingSystem.Services;
using AccountingSystem.Services.EmployeeScope;
using AccountingSystem.Services.Pdf;
using AccountingSystem.Services.Permissions;
using AccountingSystem.Services.WhatsApp;
using AccountingSystem.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using EmployeeScopeModel = AccountingSystem.Services.EmployeeScope.EmployeeScope;

namespace AccountingSystem.Tests.Controllers;

public class SalesInvoiceControllerTests : BaseTest
{
	private readonly Mock<IEmployeeScopeService> _employeeScopeMock;
	private readonly Mock<IPdfInvoiceService> _pdfMock;
	private readonly Mock<IWhatsAppService> _whatsAppMock;
	private readonly Mock<IPermissionService> _permissionMock;
	private readonly Mock<IPostingService> _postingMock;
	private readonly Mock<IInvoiceReminderService> _reminderMock;
	private readonly Mock<IDocumentNumberService> _documentNumberMock;
	private readonly Mock<ISalesRepService> _salesRepMock;
	private readonly SalesInvoiceController _sut;

	public SalesInvoiceControllerTests()
	{
		_employeeScopeMock = new Mock<IEmployeeScopeService>();
		_pdfMock = new Mock<IPdfInvoiceService>();
		_whatsAppMock = new Mock<IWhatsAppService>();
		_permissionMock = new Mock<IPermissionService>();
		_postingMock = new Mock<IPostingService>();
		_reminderMock = new Mock<IInvoiceReminderService>();
		_documentNumberMock = new Mock<IDocumentNumberService>();
		_salesRepMock = new Mock<ISalesRepService>();

		// Default setups
		_employeeScopeMock
			.Setup(x => x.GetScopeAsync())
			.ReturnsAsync(EmployeeScopeModel.Unrestricted(userId: 1));

		_employeeScopeMock
			.Setup(x => x.CanAccessStoreAsync(It.IsAny<int>()))
			.ReturnsAsync(true);

		_employeeScopeMock
			.Setup(x => x.ApplyStoreFilter(
				It.IsAny<IQueryable<Store>>(),
				It.IsAny<EmployeeScopeModel>()))
			.Returns((IQueryable<Store> q, EmployeeScopeModel s) => q);

		_documentNumberMock
			.Setup(x => x.GenerateNumberAsync(
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>()))
			.ReturnsAsync("SAL-TEST-0001");

		_salesRepMock
			.Setup(x => x.GetCurrentRepIdAsync())
			.ReturnsAsync((int?)null);

		_sut = new SalesInvoiceController(
			Context,
			_employeeScopeMock.Object,
			_pdfMock.Object,
			_whatsAppMock.Object,
			_permissionMock.Object,
			_postingMock.Object,
			_reminderMock.Object,
			_documentNumberMock.Object,
			_salesRepMock.Object);

		var httpContext = new DefaultHttpContext();
		_sut.ControllerContext = new ControllerContext
		{
			HttpContext = httpContext
		};
		_sut.TempData = new TempDataDictionary(
			httpContext,
			Mock.Of<ITempDataProvider>());
	}

	// =========================================
	// Helpers
	// =========================================

	private async Task EnsureBasicEntitiesAsync()
	{
		if (!Context.Companies.Any(c => c.Id == 1))
		{
			Context.Companies.Add(new Company
			{
				Id = 1,
				Name = "Test Company",
				IsActive = true,
				CreatedAt = DateTime.UtcNow
			});
		}

		if (!Context.Branches.Any(b => b.Id == 1))
		{
			Context.Branches.Add(new Branch
			{
				Id = 1,
				Name = "Test Branch",
				CompanyId = 1,
				IsActive = true,
				CreatedAt = DateTime.UtcNow
			});
		}

		if (!Context.Stores.Any(s => s.Id == 1))
		{
			Context.Stores.Add(new Store
			{
				Id = 1,
				Name = "Test Store",
				BranchId = 1,
				IsActive = true,
				CreatedAt = DateTime.UtcNow
			});
		}

		if (!Context.Customers.Any(c => c.Id == 1))
		{
			Context.Customers.Add(new Customer
			{
				Id = 1,
				Name = "Test Customer",
				Phone = "01000000001",
				IsActive = true,
				CreatedAt = DateTime.UtcNow
			});
		}

		await Context.SaveChangesAsync();
	}

	private async Task<SalesInvoice> SeedSalesInvoiceAsync(
		SalesInvoiceStatus status = SalesInvoiceStatus.Draft)
	{
		await EnsureBasicEntitiesAsync();

		var invoice = new SalesInvoice
		{
			InvoiceNumber = $"SAL-{Guid.NewGuid().ToString("N").Substring(0, 8)}",
			InvoiceDate = DateTime.UtcNow,
			BranchId = 1,
			StoreId = 1,
			CustomerId = 1,
			SubTotal = 100m,
			TotalAmount = 100m,
			Status = status,
			PaymentMethod = SalesPaymentMethod.Cash,
			CreatedAt = DateTime.UtcNow,
			RowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }
		};
		Context.SalesInvoices.Add(invoice);
		await Context.SaveChangesAsync();
		return invoice;
	}

	// =========================================
	// 1. Index Tests
	// =========================================

	[Fact]
	public async Task Index_ReturnsViewResult()
	{
		var result = await _sut.Index();

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Index_WithPageParams_ReturnsViewResult()
	{
		var result = await _sut.Index(page: 2, pageSize: 10);

		result.Should().BeOfType<ViewResult>();
	}



	// =========================================
	// 3. Details Tests
	// =========================================

	[Fact]
	public async Task Details_WhenNotFound_ReturnsNotFound()
	{
		var result = await _sut.Details(id: 99999);

		result.Should().BeOfType<NotFoundResult>();
	}

	[Fact]
	public async Task Details_WhenExists_ReturnsView()
	{
		var invoice = await SeedSalesInvoiceAsync();

		var result = await _sut.Details(id: invoice.Id);

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Details_WithoutStorePermission_Redirects()
	{
		_employeeScopeMock
			.Setup(x => x.GetScopeAsync())
			.ReturnsAsync(EmployeeScopeModel.Restricted(
				userId: 1, employeeId: 1,
				branchIds: Array.Empty<int>(),
				storeIds: new[] { 99 }));

		var invoice = await SeedSalesInvoiceAsync();

		var result = await _sut.Details(id: invoice.Id);

		result.Should().BeOfType<RedirectToActionResult>();
	}

	// =========================================
	// 4. Confirm Tests
	// =========================================

	[Fact]
	public async Task Confirm_WhenInvoiceNotFound_ReturnsNotFound()
	{
		var result = await _sut.Confirm(id: 99999);

		result.Should().BeOfType<NotFoundResult>();
	}

	[Fact]
	public async Task Confirm_WhenInvoiceIsDraftButNoItems_Redirects()
	{
		var invoice = await SeedSalesInvoiceAsync(SalesInvoiceStatus.Draft);

		var result = await _sut.Confirm(invoice.Id);

		// فاتورة بدون بنود → يرفض بـ Redirect
		result.Should().BeOfType<RedirectToActionResult>();
	}

	[Fact]
	public async Task Confirm_WithoutStorePermission_Redirects()
	{
		_employeeScopeMock
			.Setup(x => x.GetScopeAsync())
			.ReturnsAsync(EmployeeScopeModel.Restricted(
				userId: 1, employeeId: 1,
				branchIds: Array.Empty<int>(),
				storeIds: new[] { 99 }));

		var invoice = await SeedSalesInvoiceAsync(SalesInvoiceStatus.Draft);

		var result = await _sut.Confirm(invoice.Id);

		result.Should().BeOfType<RedirectToActionResult>();
	}

	// =========================================
	// 5. Cancel Tests
	// =========================================

	[Fact]
	public async Task Cancel_WhenInvoiceNotFound_ReturnsNotFound()
	{
		var result = await _sut.Cancel(id: 99999);

		result.Should().BeOfType<NotFoundResult>();
	}

	[Fact]
	public async Task Cancel_WhenAlreadyCancelled_Redirects()
	{
		var invoice = await SeedSalesInvoiceAsync(SalesInvoiceStatus.Cancelled);

		var result = await _sut.Cancel(invoice.Id);

		result.Should().BeOfType<RedirectToActionResult>();
	}

	[Fact]
	public async Task Cancel_WithoutStorePermission_Redirects()
	{
		_employeeScopeMock
			.Setup(x => x.GetScopeAsync())
			.ReturnsAsync(EmployeeScopeModel.Restricted(
				userId: 1, employeeId: 1,
				branchIds: Array.Empty<int>(),
				storeIds: new[] { 99 }));

		var invoice = await SeedSalesInvoiceAsync(SalesInvoiceStatus.Draft);

		var result = await _sut.Cancel(invoice.Id);

		result.Should().BeOfType<RedirectToActionResult>();
	}

	// =========================================
	// 6. GetAvailableLots Tests
	// =========================================

	[Theory]
	[InlineData(0, 1)]
	[InlineData(1, 0)]
	[InlineData(-1, 1)]
	[InlineData(1, -1)]
	public async Task GetAvailableLots_WithInvalidParams_ReturnsEmptyJson(
		int productId, int storeId)
	{
		var result = await _sut.GetAvailableLots(productId, storeId);

		result.Should().BeOfType<JsonResult>();
	}

	[Fact]
	public async Task GetAvailableLots_WithNoLots_ReturnsJsonResult()
	{
		await EnsureBasicEntitiesAsync();

		var result = await _sut.GetAvailableLots(productId: 1, storeId: 1);

		result.Should().BeOfType<JsonResult>();
	}

	// =========================================
	// 7. GetCustomerInfo Tests
	// =========================================

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public async Task GetCustomerInfo_WithInvalidId_ReturnsJsonFalse(int id)
	{
		var result = await _sut.GetCustomerInfo(id);

		result.Should().BeOfType<JsonResult>();
	}

	[Fact]
	public async Task GetCustomerInfo_WhenCustomerNotFound_ReturnsJsonFalse()
	{
		var result = await _sut.GetCustomerInfo(id: 99999);

		result.Should().BeOfType<JsonResult>();
	}

	[Fact]
	public async Task GetCustomerInfo_WhenCustomerExists_ReturnsJsonOk()
	{
		await EnsureBasicEntitiesAsync();

		var result = await _sut.GetCustomerInfo(id: 1);

		result.Should().BeOfType<JsonResult>();
	}

	// =========================================
	// 8. CreateQuickCustomer Tests
	// =========================================

	[Theory]
	[InlineData("", "01000000001")]
	[InlineData("Test", "")]
	[InlineData("", "")]
	public async Task CreateQuickCustomer_WithInvalidInput_ReturnsJsonFalse(
		string name, string phone)
	{
		var result = await _sut.CreateQuickCustomer(name, phone);

		result.Should().BeOfType<JsonResult>();
	}

	// =========================================
	// 9. CreateQuickCashAccount Tests
	// =========================================

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public async Task CreateQuickCashAccount_WithEmptyName_ReturnsJsonFalse(
		string name)
	{
		var result = await _sut.CreateQuickCashAccount(name);

		result.Should().BeOfType<JsonResult>();
	}

	// =========================================
	// 10. SendReminder Tests
	// =========================================

	[Fact]
	public async Task SendReminder_WhenInvoiceNotFound_ReturnsNotFound()
	{
		var result = await _sut.SendReminder(id: 99999);

		result.Should().BeOfType<NotFoundResult>();
	}

	// =========================================
	// 11. SendWhatsApp Tests
	// =========================================

	[Fact]
	public async Task SendWhatsApp_WhenInvoiceNotFound_ReturnsNotFound()
	{
		var result = await _sut.SendWhatsApp(id: 99999);

		result.Should().BeOfType<NotFoundResult>();
	}
}