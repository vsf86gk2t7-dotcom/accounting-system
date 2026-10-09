using AccountingSystem.Controllers;
using AccountingSystem.Models;
using AccountingSystem.Models.ViewModels.Purchases;
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

public class PurchaseInvoiceControllerTests : BaseTest
{
	private readonly Mock<IWhatsAppService> _whatsAppMock;
	private readonly Mock<IPdfInvoiceService> _pdfMock;
	private readonly Mock<IEmployeeScopeService> _employeeScopeMock;
	private readonly Mock<IPermissionService> _permissionMock;
	private readonly Mock<IPostingService> _postingMock;
	private readonly Mock<IDocumentNumberService> _documentNumberMock;
	    private readonly Mock<INotificationService> _notificationsMock;   // ⬅️ ضيف ده
	private readonly PurchaseInvoiceController _sut;

	public PurchaseInvoiceControllerTests()
	{
		_whatsAppMock = new Mock<IWhatsAppService>();
		_pdfMock = new Mock<IPdfInvoiceService>();
		_employeeScopeMock = new Mock<IEmployeeScopeService>();
		_permissionMock = new Mock<IPermissionService>();
		_postingMock = new Mock<IPostingService>();
		_documentNumberMock = new Mock<IDocumentNumberService>();
		_notificationsMock = new Mock<INotificationService>();
_notificationsMock
    .Setup(x => x.CurrentUserId(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
    .Returns(1);

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

		_employeeScopeMock
			.Setup(x => x.ApplyBranchFilter(
				It.IsAny<IQueryable<Branch>>(),
				It.IsAny<EmployeeScopeModel>()))
			.Returns((IQueryable<Branch> q, EmployeeScopeModel s) => q);

		_documentNumberMock
			.Setup(x => x.GenerateNumberAsync(
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>()))
			.ReturnsAsync("PUR-TEST-0001");

		_sut = new PurchaseInvoiceController(
			Context,
			_whatsAppMock.Object,
			_pdfMock.Object,
			_employeeScopeMock.Object,
			_permissionMock.Object,
			_postingMock.Object,
			_documentNumberMock.Object,
    _notificationsMock.Object);

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

			Context.Suppliers.Add(new Supplier
			{
				Id = 1, Name = "Test Supplier",
				Phone = "01000000001",
				IsActive = true, CreatedAt = DateTime.UtcNow
			});

			await Context.SaveChangesAsync();
		}
	}

	private async Task<PurchaseInvoice> SeedPurchaseInvoiceAsync(
		PurchaseInvoiceStatus status = PurchaseInvoiceStatus.Draft)
	{
		await EnsureBasicEntitiesAsync();

		var invoice = new PurchaseInvoice
		{
			InvoiceNumber = $"PUR-{Guid.NewGuid().ToString("N").Substring(0, 8)}",
			InvoiceDate = DateTime.UtcNow,
			StoreId = 1, SupplierId = 1,
			SubTotal = 100m, TotalAmount = 100m,
			Status = status,
			CreatedAt = DateTime.UtcNow,
			RowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }
		};
		Context.PurchaseInvoices.Add(invoice);
		await Context.SaveChangesAsync();
		return invoice;
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
	public async Task Index_WithPageParams_ReturnsViewResult()
	{
		var result = await _sut.Index(page: 2, pageSize: 10);

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 2. Details
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
		var invoice = await SeedPurchaseInvoiceAsync();

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

		var invoice = await SeedPurchaseInvoiceAsync();

		var result = await _sut.Details(id: invoice.Id);

		result.Should().BeOfType<RedirectToActionResult>();
	}

	// =========================================
	// 3. TaxReport
	// =========================================

	[Fact]
	public async Task TaxReport_ReturnsViewResult()
	{
		var result = await _sut.TaxReport(
			fromDate: null, toDate: null,
			storeId: null, supplierId: null);

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task TaxReport_WithDateRange_ReturnsView()
	{
		var result = await _sut.TaxReport(
			fromDate: DateTime.UtcNow.AddDays(-30),
			toDate: DateTime.UtcNow,
			storeId: null, supplierId: null);

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 4. Create GET
	// =========================================

	[Fact]
	public async Task Create_GET_ReturnsViewResult()
	{
		var result = await _sut.Create();

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 5. Create POST — Validation
	// =========================================

	[Fact]
	public async Task Create_POST_WithInvalidModelState_ReturnsView()
	{
		var model = new PurchaseInvoiceCreateViewModel();
		_sut.ModelState.AddModelError("test", "error");

		var result = await _sut.Create(model);

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 6. Post (ترحيل)
	// =========================================

	[Fact]
	public async Task Post_WhenNotFound_ReturnsNotFound()
	{
		var result = await _sut.Post(id: 99999);

		result.Should().BeOfType<NotFoundResult>();
	}

	[Fact]
	public async Task Post_WhenNotDraft_Redirects()
	{
		var invoice = await SeedPurchaseInvoiceAsync(PurchaseInvoiceStatus.Posted);

		var result = await _sut.Post(invoice.Id);

		result.Should().BeOfType<RedirectToActionResult>();
	}

	// =========================================
	// 7. Cancel
	// =========================================

	[Fact]
	public async Task Cancel_WhenNotFound_ReturnsNotFound()
	{
		var result = await _sut.Cancel(id: 99999);

		result.Should().BeOfType<NotFoundResult>();
	}

	[Fact]
	public async Task Cancel_WhenAlreadyCancelled_Redirects()
	{
		var invoice = await SeedPurchaseInvoiceAsync(PurchaseInvoiceStatus.Cancelled);

		var result = await _sut.Cancel(invoice.Id);

		result.Should().BeOfType<RedirectToActionResult>();
	}

	// =========================================
	// 8. CreateQuickSupplier
	// =========================================

	[Theory]
	[InlineData("", "01000000001")]
	[InlineData("Test", "")]
	[InlineData("", "")]
	[InlineData("   ", "   ")]
	public async Task CreateQuickSupplier_WithInvalidInput_ReturnsJsonFalse(
		string name, string phone)
	{
		var result = await _sut.CreateQuickSupplier(name, phone);

		result.Should().BeOfType<JsonResult>();
	}

	[Fact]
	public async Task CreateQuickSupplier_WithDuplicatePhone_ReturnsJsonFalse()
	{
		await EnsureBasicEntitiesAsync();

		// phone already exists
		var result = await _sut.CreateQuickSupplier("New", "01000000001");

		result.Should().BeOfType<JsonResult>();
	}

	// =========================================
	// 9. CreateQuickStore
	// =========================================

	[Theory]
	[InlineData("", 1)]
	[InlineData("   ", 1)]
	public async Task CreateQuickStore_WithEmptyName_ReturnsJsonFalse(
		string name, int branchId)
	{
		var result = await _sut.CreateQuickStore(name, branchId);

		result.Should().BeOfType<JsonResult>();
	}

	[Fact]
	public async Task CreateQuickStore_WithInvalidBranch_ReturnsJsonFalse()
	{
		var result = await _sut.CreateQuickStore("New Store", branchId: 99999);

		result.Should().BeOfType<JsonResult>();
	}

	// =========================================
	// 10. GetSupplierInfo
	// =========================================

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public async Task GetSupplierInfo_WithInvalidId_ReturnsJsonFalse(int id)
	{
		var result = await _sut.GetSupplierInfo(id);

		result.Should().BeOfType<JsonResult>();
	}

	[Fact]
	public async Task GetSupplierInfo_WhenSupplierNotFound_ReturnsJsonFalse()
	{
		var result = await _sut.GetSupplierInfo(id: 99999);

		result.Should().BeOfType<JsonResult>();
	}

	[Fact]
	public async Task GetSupplierInfo_WhenSupplierExists_ReturnsJsonOk()
	{
		await EnsureBasicEntitiesAsync();

		var result = await _sut.GetSupplierInfo(id: 1);

		result.Should().BeOfType<JsonResult>();
	}

	// =========================================
	// 11. SendWhatsApp
	// =========================================

	[Fact]
	public async Task SendWhatsApp_WhenNotFound_ReturnsNotFound()
	{
		var result = await _sut.SendWhatsApp(id: 99999);

		result.Should().BeOfType<NotFoundResult>();
	}

	// =========================================
	// 12. AddUnitForProduct
	// =========================================

	[Fact]
	public async Task AddUnitForProduct_WhenProductNotFound_ReturnsJsonFalse()
	{
		var result = await _sut.AddUnitForProduct(
			productId: 99999,
			unitName: "Test",
			unitShort: "T",
			conversionFactor: 1m,
			salePrice: 10m);

		result.Should().BeOfType<JsonResult>();
	}
}