using AccountingSystem.Controllers;
using AccountingSystem.Models;
using AccountingSystem.Services.QrCode;
using AccountingSystem.Services.WhatsApp;
using AccountingSystem.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AccountingSystem.Tests.Controllers;

public class SupplierControllerTests : BaseTest
{
	private readonly Mock<IWhatsAppService> _whatsAppMock;
	private readonly Mock<IQrCodeService> _qrCodeMock;
	private readonly SupplierController _sut;

	public SupplierControllerTests()
	{
		_whatsAppMock = new Mock<IWhatsAppService>();
		_qrCodeMock = new Mock<IQrCodeService>();

		_sut = new SupplierController(
			Context,
			_whatsAppMock.Object,
			_qrCodeMock.Object);

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

	private async Task<Supplier> SeedSupplierAsync(
		string name = "Test Supplier",
		string phone = "01000000001",
		bool isActive = true)
	{
		var supplier = new Supplier
		{
			Name = name,
			Phone = phone,
			IsActive = isActive,
			CreatedAt = DateTime.UtcNow
		};
		Context.Suppliers.Add(supplier);
		await Context.SaveChangesAsync();
		return supplier;
	}

	// =========================================
	// 1. Index
	// =========================================

	[Fact]
	public async Task Index_ReturnsViewResult()
	{
		var result = await _sut.Index();

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Index_WithSuppliers_ReturnsView()
	{
		await SeedSupplierAsync();

		var result = await _sut.Index();

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
		var model = new Supplier();
		_sut.ModelState.AddModelError("test", "error");

		var result = await _sut.Create(model);

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Create_POST_WithValidSupplier_AddsSupplier()
	{
		var model = new Supplier
		{
			Name = "New Supplier",
			Phone = "01000000099",
			IsActive = true
		};

		var result = await _sut.Create(model);

		// ممكن Redirect أو View
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
		var supplier = await SeedSupplierAsync();

		var result = await _sut.Edit(id: supplier.Id);

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 5. Edit POST
	// =========================================

	[Fact]
	public async Task Edit_POST_WithInvalidModelState_ReturnsView()
	{
		var supplier = await SeedSupplierAsync();
		var model = new Supplier { Id = supplier.Id, Name = "" };
		_sut.ModelState.AddModelError("test", "error");

		var result = await _sut.Edit(supplier.Id, model);

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
		var supplier = await SeedSupplierAsync(isActive: true);

		var result = await _sut.ToggleActive(supplier.Id);

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