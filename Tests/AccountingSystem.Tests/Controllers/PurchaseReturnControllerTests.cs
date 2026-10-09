using AccountingSystem.Controllers;
using AccountingSystem.Models;
using AccountingSystem.Models.ViewModels.Returns;
using AccountingSystem.Services;
using AccountingSystem.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AccountingSystem.Tests.Controllers;

public class PurchaseReturnControllerTests : BaseTest
{
	private readonly Mock<IPostingService> _postingMock;
	private readonly Mock<IDocumentNumberService> _documentNumberMock;
	private readonly PurchaseReturnController _sut;

	public PurchaseReturnControllerTests()
	{
		_postingMock = new Mock<IPostingService>();
		_documentNumberMock = new Mock<IDocumentNumberService>();

		_documentNumberMock
			.Setup(x => x.GenerateNumberAsync(
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>()))
			.ReturnsAsync("PRT-TEST-0001");

		_sut = new PurchaseReturnController(
			Context,
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

			Context.Units.Add(new Unit
			{
				Id = 1, Name = "قطعة", ShortName = "ق",
				IsActive = true, CreatedAt = DateTime.UtcNow
			});

			await Context.SaveChangesAsync();
		}
	}

	private async Task<Product> SeedProductAsync(int id = 1)
	{
		await EnsureBasicEntitiesAsync();

		var product = new Product
		{
			Id = id,
			Name = $"Product {id}",
			Code = $"P{id:D4}",
			Barcode = $"BC{id:D8}",
			BaseUnitId = 1,
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		Context.Products.Add(product);
		await Context.SaveChangesAsync();
		return product;
	}

	private async Task<StockLot> SeedStockLotAsync(
		int storeId = 1,
		int productId = 1,
		decimal quantity = 100m,
		decimal unitCost = 50m)
	{
		var lot = new StockLot
		{
			StoreId = storeId,
			ProductId = productId,
			QuantityReceived = quantity,
			QuantityRemaining = quantity,
			UnitCost = unitCost,
			PurchaseDate = DateTime.UtcNow,
			CreatedAt = DateTime.UtcNow,
			IsActive = true,
			RowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }
		};
		Context.StockLots.Add(lot);
		await Context.SaveChangesAsync();
		return lot;
	}

	private async Task<PurchaseReturnInvoice> SeedReturnInvoiceAsync()
	{
		await EnsureBasicEntitiesAsync();

		var invoice = new PurchaseReturnInvoice
		{
			InvoiceNumber = $"PRT-{Guid.NewGuid().ToString("N").Substring(0, 8)}",
			StoreId = 1,
			SupplierId = 1,
			ReturnDate = DateTime.UtcNow,
			TotalAmount = 100m,
			CreatedAt = DateTime.UtcNow
		};
		Context.PurchaseReturnInvoices.Add(invoice);
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
	public async Task Index_WithReturnInvoices_ReturnsViewWithRows()
	{
		await SeedReturnInvoiceAsync();

		var result = await _sut.Index();

		var viewResult = result.Should().BeOfType<ViewResult>().Subject;
		var model = viewResult.Model.Should()
			.BeOfType<ReturnListViewModel>().Subject;
		model.Type.Should().Be("purchase");
		model.Rows.Should().HaveCount(1);
	}

	[Fact]
	public async Task Index_WithNoInvoices_ReturnsEmptyRows()
	{
		var result = await _sut.Index();

		var viewResult = result.Should().BeOfType<ViewResult>().Subject;
		var model = viewResult.Model.Should()
			.BeOfType<ReturnListViewModel>().Subject;
		model.Rows.Should().BeEmpty();
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
	// 3. Create POST — Validation
	// =========================================

	[Fact]
	public async Task Create_POST_WithInvalidModelState_ReturnsView()
	{
		var model = new PurchaseReturnViewModel();
		_sut.ModelState.AddModelError("test", "error");

		var result = await _sut.Create(model);

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Create_POST_WithEmptyItems_ReturnsViewWithError()
	{
		await EnsureBasicEntitiesAsync();

		var model = new PurchaseReturnViewModel
		{
			StoreId = 1,
			SupplierId = 1,
			Items = new List<PurchaseReturnItemViewModel>()
		};

		var result = await _sut.Create(model);

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Create_POST_WithZeroQuantity_ReturnsViewWithError()
	{
		await EnsureBasicEntitiesAsync();
		var product = await SeedProductAsync();

		var model = new PurchaseReturnViewModel
		{
			StoreId = 1,
			SupplierId = 1,
			Items = new List<PurchaseReturnItemViewModel>
			{
				new() { ProductId = product.Id, Quantity = 0 }
			}
		};

		var result = await _sut.Create(model);

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Create_POST_WithInsufficientStock_ReturnsViewWithError()
	{
		await EnsureBasicEntitiesAsync();
		var product = await SeedProductAsync();
		await SeedStockLotAsync(quantity: 5m);

		// نطلب 100 من أصل 5
		var model = new PurchaseReturnViewModel
		{
			StoreId = 1,
			SupplierId = 1,
			Items = new List<PurchaseReturnItemViewModel>
			{
				new() { ProductId = product.Id, Quantity = 100 }
			}
		};

		var result = await _sut.Create(model);

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Create_POST_WithInvalidStore_ReturnsViewWithError()
	{
		await EnsureBasicEntitiesAsync();
		var product = await SeedProductAsync();

		var model = new PurchaseReturnViewModel
		{
			StoreId = 99999,
			SupplierId = 1,
			Items = new List<PurchaseReturnItemViewModel>
			{
				new() { ProductId = product.Id, Quantity = 1 }
			}
		};

		var result = await _sut.Create(model);

		result.Should().BeOfType<ViewResult>();
	}
}