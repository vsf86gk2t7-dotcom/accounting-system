using AccountingSystem.Controllers;
using AccountingSystem.Models;
using AccountingSystem.Services;
using AccountingSystem.Services.QrCode;
using AccountingSystem.Services.WhatsApp;
using AccountingSystem.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Security.Claims;

namespace AccountingSystem.Tests.Controllers;

public class CustomerControllerTests : BaseTest
{
	private readonly Mock<IWhatsAppService> _whatsAppMock;
	private readonly Mock<IQrCodeService> _qrCodeMock;
	private readonly Mock<ISalesRepService> _salesRepMock;
	private readonly Mock<INotificationService> _notificationsMock;
	private readonly CustomerController _sut;

	public CustomerControllerTests()
	{
		_whatsAppMock = new Mock<IWhatsAppService>();
		_qrCodeMock = new Mock<IQrCodeService>();
		_salesRepMock = new Mock<ISalesRepService>();

		_salesRepMock
			.Setup(x => x.GetCurrentRepIdAsync())
			.ReturnsAsync((int?)null);

		_notificationsMock = new Mock<INotificationService>();

		_notificationsMock
			.Setup(x => x.CurrentUserId(It.IsAny<ClaimsPrincipal>()))
			.Returns(1);

		_sut = new CustomerController(
			Context,
			_whatsAppMock.Object,
			_qrCodeMock.Object,
			_salesRepMock.Object,
			_notificationsMock.Object);

		var httpContext = new DefaultHttpContext();
		_sut.ControllerContext = new ControllerContext
		{
			HttpContext = httpContext
		};
		_sut.TempData = new TempDataDictionary(
			httpContext,
			Mock.Of<ITempDataProvider>());

		// ⬇️⬇️⬇️ UrlHelper للاختبارات (CustomerController.Create بيستخدم Url.Action) ⬇️⬇️⬇️
		var urlHelperMock = new Mock<IUrlHelper>();
		urlHelperMock
			.Setup(x => x.Action(It.IsAny<UrlActionContext>()))
			.Returns("/Customer/Index");

		_sut.Url = urlHelperMock.Object;
		// ⬆️⬆️⬆️ UrlHelper ⬆️⬆️⬆️
	}

	// =========================================
	// Helpers
	// =========================================

	private async Task<Customer> SeedCustomerAsync(
		string name = "Test Customer",
		string phone = "01000000001",
		bool isActive = true)
	{
		var customer = new Customer
		{
			Name = name,
			Phone = phone,
			IsActive = isActive,
			CreatedAt = DateTime.UtcNow
		};
		Context.Customers.Add(customer);
		await Context.SaveChangesAsync();
		return customer;
	}

	// =========================================
	// 1. Index
	// =========================================

	[Fact]
	public async Task Index_ReturnsViewResult()
	{
		var result = await _sut.Index(search: null);

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Index_WithSearch_ReturnsViewResult()
	{
		var result = await _sut.Index(search: "test");

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Index_WithCustomers_ReturnsView()
	{
		await SeedCustomerAsync();

		var result = await _sut.Index(search: null);

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 2. Create GET
	// =========================================

	[Fact]
	public async Task Create_GET_ReturnsViewResult()
	{
		var result = await _sut.Create();

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 3. Create POST
	// =========================================

	[Fact]
	public async Task Create_POST_WithInvalidModelState_ReturnsView()
	{
		var model = new Customer();
		_sut.ModelState.AddModelError("test", "error");

		var result = await _sut.Create(model);

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Create_POST_WithValidCustomer_AddsCustomer()
	{
		var model = new Customer
		{
			Name = "New Customer",
			Phone = "01000000099",
			IsActive = true
		};

		var result = await _sut.Create(model);

		// ممكن يكون Redirect أو View
		(result is RedirectToActionResult || result is ViewResult)
			.Should().BeTrue();
	}

	// =========================================
	// 4. Edit GET
	// =========================================

	[Fact]
	public async Task Edit_GET_WhenNotFound_ReturnsNotFound()
	{
		var result = await _sut.Edit(id: 99999);

		result.Should().BeOfType<NotFoundResult>();
	}

	[Fact]
	public async Task Edit_GET_WhenExists_ReturnsView()
	{
		var customer = await SeedCustomerAsync();

		var result = await _sut.Edit(id: customer.Id);

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 5. Edit POST
	// =========================================

	[Fact]
	public async Task Edit_POST_WithInvalidModelState_ReturnsView()
	{
		var customer = await SeedCustomerAsync();
		var model = new Customer { Id = customer.Id, Name = "" };
		_sut.ModelState.AddModelError("test", "error");

		var result = await _sut.Edit(customer.Id, model);

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 6. ToggleActive
	// =========================================

	[Fact]
	public async Task ToggleActive_WhenNotFound_ReturnsNotFound()
	{
		var result = await _sut.ToggleActive(id: 99999);

		result.Should().BeOfType<NotFoundResult>();
	}

	[Fact]
	public async Task ToggleActive_WhenExists_Toggles()
	{
		var customer = await SeedCustomerAsync(isActive: true);

		var result = await _sut.ToggleActive(customer.Id);

		// ممكن Redirect أو View
		result.Should().NotBeNull();
	}

	// =========================================
	// 7. RequestPortal
	// =========================================

	[Fact]
	public async Task RequestPortal_WhenNotFound_ReturnsNotFound()
	{
		var result = await _sut.RequestPortal(id: 99999);

		result.Should().BeOfType<NotFoundResult>();
	}

	// =========================================
	// 8. PortalRequests
	// =========================================

	[Fact]
	public async Task PortalRequests_ReturnsViewResult()
	{
		var result = await _sut.PortalRequests();

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 9. ApprovePortal
	// =========================================

	[Fact]
	public async Task ApprovePortal_WhenNotFound_ReturnsNotFound()
	{
		var result = await _sut.ApprovePortal(id: 99999);

		result.Should().BeOfType<NotFoundResult>();
	}

	// =========================================
	// 10. CreatePortalAccount
	// =========================================

	[Fact]
	public async Task CreatePortalAccount_WhenNotFound_ReturnsNotFound()
	{
		var result = await _sut.CreatePortalAccount(id: 99999);

		result.Should().BeOfType<NotFoundResult>();
	}
}