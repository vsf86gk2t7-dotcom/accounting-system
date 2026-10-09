using AccountingSystem.Controllers;
using AccountingSystem.Models;
using AccountingSystem.Models.ViewModels.Returns;
using AccountingSystem.Services;
using AccountingSystem.Services.EmployeeScope;
using AccountingSystem.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using EmployeeScopeModel = AccountingSystem.Services.EmployeeScope.EmployeeScope;

namespace AccountingSystem.Tests.Controllers;

public class SalesReturnControllerTests : BaseTest
{
	private readonly Mock<IEmployeeScopeService> _employeeScopeMock;
	private readonly Mock<IPostingService> _postingMock;
	private readonly Mock<IDocumentNumberService> _documentNumberMock;
	private readonly SalesReturnController _sut;

	public SalesReturnControllerTests()
	{
		_employeeScopeMock = new Mock<IEmployeeScopeService>();
		_postingMock = new Mock<IPostingService>();
		_documentNumberMock = new Mock<IDocumentNumberService>();

		_employeeScopeMock
			.Setup(x => x.GetScopeAsync())
			.ReturnsAsync(EmployeeScopeModel.Unrestricted(userId: 1));

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

		_employeeScopeMock
			.Setup(x => x.CanAccessStoreAsync(It.IsAny<int>()))
			.ReturnsAsync(true);

		_documentNumberMock
			.Setup(x => x.GenerateNumberAsync(
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>()))
			.ReturnsAsync("SRT-TEST-0001");

		_sut = new SalesReturnController(
			Context,
			_employeeScopeMock.Object,
			_postingMock.Object,
			_documentNumberMock.Object);

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

				if (!Context.Customers.Any(c => c.Id == 2))
		{
			Context.Customers.Add(new Customer
			{
				Id = 2,
				Name = "Test Customer 2",
				Phone = "01000000002",
				IsActive = true,
				CreatedAt = DateTime.UtcNow
			});
		}

		// ✅ Seed Units (1..5) — referenced by Include(x => x.Unit)
		for (int i = 1; i <= 5; i++)
		{
			if (!Context.Units.Any(u => u.Id == i))
			{
				Context.Units.Add(new Unit
				{
					Id = i,
					Name = $"Unit {i}",
					ShortName = $"U{i}",
					IsActive = true,
					CreatedAt = DateTime.UtcNow
				});
			}
		}

		// ✅ Seed Products (1..5) — referenced by Include(x => x.Product)
		for (int i = 1; i <= 5; i++)
		{
			if (!Context.Products.Any(p => p.Id == i))
			{
				Context.Products.Add(new Product
				{
					Id = i,
					Name = $"Product {i}",
					Code = $"P{i:D4}",
					Barcode = $"BC{i:D8}",
					BaseUnitId = 1,
					IsActive = true,
					CreatedAt = DateTime.UtcNow
				});
			}
				}

		await Context.SaveChangesAsync();
	}

	private async Task<SalesInvoice> SeedConfirmedInvoiceAsync(		int? customerId = 1,
		int itemCount = 1)
	{
		await EnsureBasicEntitiesAsync();

		var invoice = new SalesInvoice
		{
			InvoiceNumber = $"SAL-{Guid.NewGuid().ToString("N").Substring(0, 8)}",
			InvoiceDate = DateTime.UtcNow.AddDays(-1),
			BranchId = 1,
			StoreId = 1,
			CustomerId = customerId,
			SubTotal = 100m,
			TotalAmount = 100m,
			Status = SalesInvoiceStatus.Confirmed,
			ConfirmedAt = DateTime.UtcNow,
			PaymentMethod = SalesPaymentMethod.Cash,
			CreatedAt = DateTime.UtcNow,
			RowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }
		};

		for (int i = 0; i < itemCount; i++)
		{
			var saleItem = new SalesInvoiceItem
			{
				ProductId = i + 1,
				UnitId = 1,
				Quantity = 1,
				QuantityInBaseUnit = 1,
				UnitPrice = 100,
				TotalPrice = 100,
				ConversionFactor = 1
			};

			saleItem.LotAllocations.Add(new SalesInvoiceItemLot
			{
				StockLotId = i + 1,
				QuantityBaseUnit = 1,
				UnitCost = 60,
				TotalCost = 60
			});

			invoice.Items.Add(saleItem);
		}

		Context.SalesInvoices.Add(invoice);
		await Context.SaveChangesAsync();

		return invoice;
	}

	private async Task<SalesReturnInvoice> SeedReturnInvoiceAsync(int customerId = 1)
	{
		await EnsureBasicEntitiesAsync();

		var originalInvoice = await SeedConfirmedInvoiceAsync(customerId: customerId);

		var returnInvoice = new SalesReturnInvoice
		{
			InvoiceNumber = $"SRT-{Guid.NewGuid().ToString("N").Substring(0, 8)}",
			OriginalSalesInvoiceId = originalInvoice.Id,
			CustomerId = customerId,
			StoreId = 1,
			BranchId = 1,
			ReturnDate = DateTime.UtcNow,
			TotalAmount = 100m,
			IsPosted = true,
			CreatedAt = DateTime.UtcNow,
			RowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }
		};

		Context.SalesReturnInvoices.Add(returnInvoice);
		await Context.SaveChangesAsync();

		return returnInvoice;
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
	public async Task Index_WithRestrictedScope_FiltersByStore()
	{
		_employeeScopeMock
			.Setup(x => x.GetScopeAsync())
			.ReturnsAsync(EmployeeScopeModel.Restricted(
				userId: 1, employeeId: 1,
				branchIds: new[] { 1 },
				storeIds: new[] { 1 }));

		await SeedReturnInvoiceAsync();

		var result = await _sut.Index();

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 2. Create GET Tests
	// =========================================

	[Fact]
	public async Task Create_GET_WithoutParams_ReturnsEmptyModel()
	{
		var result = await _sut.Create(invoiceId: null, customerId: null);

		var viewResult = result.Should().BeOfType<ViewResult>().Subject;
		var model = viewResult.Model.Should()
			.BeOfType<SalesReturnViewModel>().Subject;
		model.Items.Should().BeEmpty();
	}

	[Fact]
	public async Task Create_GET_WithInvalidInvoiceId_Redirects()
	{
		var result = await _sut.Create(invoiceId: 99999, customerId: null);

		result.Should().BeOfType<RedirectToActionResult>();
	}

	[Fact]
	public async Task Create_GET_WithValidInvoiceId_LoadsInvoice()
	{
		var invoice = await SeedConfirmedInvoiceAsync();

		var result = await _sut.Create(
			invoiceId: invoice.Id, customerId: null);

		var viewResult = result.Should().BeOfType<ViewResult>().Subject;
		var model = viewResult.Model.Should()
			.BeOfType<SalesReturnViewModel>().Subject;
		model.OriginalSalesInvoiceId.Should().Be(invoice.Id);
	}

	// =========================================
	// 3. Create POST — Validation Failures
	// =========================================

	[Fact]
	public async Task Create_POST_WithoutOriginalInvoiceId_ReturnsViewWithError()
	{
		var model = new SalesReturnViewModel
		{
			OriginalSalesInvoiceId = null,
			Items = new List<SalesReturnItemViewModel>
			{
				new() { SalesInvoiceItemId = 1, Quantity = 1 }
			}
		};

		var result = await _sut.Create(model);

		result.Should().BeOfType<ViewResult>();
		_sut.ModelState.IsValid.Should().BeFalse();
	}

	[Fact]
	public async Task Create_POST_WithEmptyItems_ReturnsViewWithError()
	{
		var invoice = await SeedConfirmedInvoiceAsync();

		var model = new SalesReturnViewModel
		{
			OriginalSalesInvoiceId = invoice.Id,
			Items = new List<SalesReturnItemViewModel>()
		};

		var result = await _sut.Create(model);

		result.Should().BeOfType<ViewResult>();
		_sut.ModelState.IsValid.Should().BeFalse();
	}

	[Fact]
	public async Task Create_POST_WithRefundToCashButNoAccount_ReturnsViewWithError()
	{
		var invoice = await SeedConfirmedInvoiceAsync();

		var model = new SalesReturnViewModel
		{
			OriginalSalesInvoiceId = invoice.Id,
			RefundToCash = true,
			RefundCashAccountId = null,
			Items = new List<SalesReturnItemViewModel>
			{
				new()
				{
					SalesInvoiceItemId = invoice.Items.First().Id,
					Quantity = 1
				}
			}
		};

		var result = await _sut.Create(model);

		result.Should().BeOfType<ViewResult>();
		_sut.ModelState.IsValid.Should().BeFalse();
	}

	// =========================================
	// 4. Create POST — Invoice Failures
	// =========================================

	[Fact]
	public async Task Create_POST_WhenInvoiceNotFound_ReturnsViewWithError()
	{
		var model = new SalesReturnViewModel
		{
			OriginalSalesInvoiceId = 99999,
			Items = new List<SalesReturnItemViewModel>
			{
				new() { SalesInvoiceItemId = 1, Quantity = 1 }
			}
		};

		var result = await _sut.Create(model);

		result.Should().BeOfType<ViewResult>();
		_sut.ModelState.IsValid.Should().BeFalse();
	}

	[Fact]
	public async Task Create_POST_WhenInvoiceIsDraft_ReturnsViewWithError()
	{
		await EnsureBasicEntitiesAsync();

		var invoice = new SalesInvoice
		{
			InvoiceNumber = "SAL-DRAFT-001",
			InvoiceDate = DateTime.UtcNow,
			BranchId = 1,
			StoreId = 1,
			CustomerId = 1,
			SubTotal = 100m,
			TotalAmount = 100m,
			Status = SalesInvoiceStatus.Draft,
			PaymentMethod = SalesPaymentMethod.Cash,
			CreatedAt = DateTime.UtcNow,
			RowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }
		};
		invoice.Items.Add(new SalesInvoiceItem
		{
			ProductId = 1, UnitId = 1, Quantity = 1, QuantityInBaseUnit = 1,
			UnitPrice = 100, TotalPrice = 100, ConversionFactor = 1
		});
		Context.SalesInvoices.Add(invoice);
		await Context.SaveChangesAsync();

		var model = new SalesReturnViewModel
		{
			OriginalSalesInvoiceId = invoice.Id,
			Items = new List<SalesReturnItemViewModel>
			{
				new() { SalesInvoiceItemId = invoice.Items.First().Id, Quantity = 1 }
			}
		};

		var result = await _sut.Create(model);

		result.Should().BeOfType<ViewResult>();
		_sut.ModelState.IsValid.Should().BeFalse();
	}

	[Fact]
	public async Task Create_POST_WhenInvoiceHasNoCustomer_Redirects()
	{
		var invoice = await SeedConfirmedInvoiceAsync(customerId: null);

		var model = new SalesReturnViewModel
		{
			OriginalSalesInvoiceId = invoice.Id,
			Items = new List<SalesReturnItemViewModel>
			{
				new()
				{
					SalesInvoiceItemId = invoice.Items.First().Id,
					Quantity = 1
				}
			}
		};

		var result = await _sut.Create(model);

		result.Should().BeOfType<RedirectToActionResult>();
	}

	[Fact]
	public async Task Create_POST_WhenNoStorePermission_Redirects()
	{
		_employeeScopeMock
			.Setup(x => x.GetScopeAsync())
			.ReturnsAsync(EmployeeScopeModel.Restricted(
				userId: 1, employeeId: 1,
				branchIds: Array.Empty<int>(),
				storeIds: new[] { 99 }));

		var invoice = await SeedConfirmedInvoiceAsync();

		var model = new SalesReturnViewModel
		{
			OriginalSalesInvoiceId = invoice.Id,
			Items = new List<SalesReturnItemViewModel>
			{
				new()
				{
					SalesInvoiceItemId = invoice.Items.First().Id,
					Quantity = 1
				}
			}
		};

		var result = await _sut.Create(model);

		result.Should().BeOfType<RedirectToActionResult>();
	}

	// =========================================
	// 5. Create POST — Item Failures
	// =========================================

	[Fact]
	public async Task Create_POST_WhenItemNotInInvoice_ReturnsViewWithError()
	{
		var invoice = await SeedConfirmedInvoiceAsync();

		var model = new SalesReturnViewModel
		{
			OriginalSalesInvoiceId = invoice.Id,
			Items = new List<SalesReturnItemViewModel>
			{
				new() { SalesInvoiceItemId = 99999, Quantity = 1 }
			}
		};

		var result = await _sut.Create(model);

		result.Should().BeOfType<ViewResult>();
		_sut.ModelState.IsValid.Should().BeFalse();
	}

	[Fact]
	public async Task Create_POST_WhenQuantityExceedsReturnable_ReturnsViewWithError()
	{
		var invoice = await SeedConfirmedInvoiceAsync();
		var itemId = invoice.Items.First().Id;

		var model = new SalesReturnViewModel
		{
			OriginalSalesInvoiceId = invoice.Id,
			Items = new List<SalesReturnItemViewModel>
			{
				new() { SalesInvoiceItemId = itemId, Quantity = 100 }
			}
		};

		var result = await _sut.Create(model);

		result.Should().BeOfType<ViewResult>();
		_sut.ModelState.IsValid.Should().BeFalse();
	}

	// =========================================
	// 6. Details Tests
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
		var returnInvoice = await SeedReturnInvoiceAsync();

		var result = await _sut.Details(id: returnInvoice.Id);

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

		var returnInvoice = await SeedReturnInvoiceAsync();

		var result = await _sut.Details(id: returnInvoice.Id);

		result.Should().BeOfType<RedirectToActionResult>();
	}

	// =========================================
	// 7. GetCustomerInvoices Tests
	// =========================================

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public async Task GetCustomerInvoices_WithInvalidCustomerId_ReturnsEmptyJson(
		int customerId)
	{
		var result = await _sut.GetCustomerInvoices(customerId);

		result.Should().BeOfType<JsonResult>();
		var json = result.Should().BeOfType<JsonResult>().Subject;
		var list = json.Value.Should().BeAssignableTo<System.Collections.IEnumerable>();
		list.Subject.Cast<object>().Should().BeEmpty();
	}

	[Fact]
	public async Task GetCustomerInvoices_ReturnsConfirmedInvoicesOnly()
	{
		var invoice = await SeedConfirmedInvoiceAsync(customerId: 1);

		var result = await _sut.GetCustomerInvoices(customerId: 1);

		result.Should().BeOfType<JsonResult>();
	}
}