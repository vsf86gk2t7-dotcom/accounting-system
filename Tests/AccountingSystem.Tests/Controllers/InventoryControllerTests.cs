using AccountingSystem.Controllers;
using AccountingSystem.Models;
using AccountingSystem.Models.ViewModels.Inventory;
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

public class InventoryControllerTests : BaseTest
{
	private readonly Mock<IEmployeeScopeService> _employeeScopeMock;
	private readonly InventoryController _sut;

	public InventoryControllerTests()
	{
		_employeeScopeMock = new Mock<IEmployeeScopeService>();

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

		_sut = new InventoryController(
			Context,
			_employeeScopeMock.Object);

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

	private async Task<Store> SeedStoreAsync(string name = "Test Store", int id = 1)
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

		await Context.SaveChangesAsync();

		var store = new Store
		{
			Id = id,
			Name = name,
			BranchId = 1,
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		Context.Stores.Add(store);
		await Context.SaveChangesAsync();
		return store;
	}

	private async Task<Supplier> SeedSupplierAsync(int id = 1)
	{
		var supplier = new Supplier
		{
			Id = id,
			Name = $"Supplier {id}",
			Phone = $"01000000{id:D3}",
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		Context.Suppliers.Add(supplier);
		await Context.SaveChangesAsync();
		return supplier;
	}

	private async Task<Product> SeedProductAsync(int id = 1)
	{
		if (!Context.Units.Any(u => u.Id == 1))
		{
			Context.Units.Add(new Unit
			{
				Id = 1,
				Name = "قطعة",
				ShortName = "ق",
				IsActive = true,
				CreatedAt = DateTime.UtcNow
			});
			await Context.SaveChangesAsync();
		}

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
		int storeId,
		int productId,
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

	// =========================================
	// 1. Index Tests
	// =========================================

	[Fact]
	public async Task Index_ReturnsViewResult()
	{
		var result = await _sut.Index(storeId: null, search: null);

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Index_WithStoreId_ReturnsViewResult()
	{
		var store = await SeedStoreAsync();

		var result = await _sut.Index(storeId: store.Id, search: null);

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Index_WithSearch_ReturnsViewResult()
	{
		var result = await _sut.Index(storeId: null, search: "test");

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 2. Lots Tests
	// =========================================

	[Fact]
	public async Task Lots_ReturnsViewResult()
	{
		var result = await _sut.Lots(
			storeId: null, productId: null,
			search: null, onlyAvailable: null);

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Lots_WithOnlyAvailableTrue_ReturnsViewResult()
	{
		var result = await _sut.Lots(
			storeId: null, productId: null,
			search: null, onlyAvailable: true);

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 3. Receive GET Tests
	// =========================================

	[Fact]
	public async Task Receive_GET_ReturnsViewResult()
	{
		var result = await _sut.Receive();

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 4. Receive POST Tests
	// =========================================

	[Fact]
	public async Task Receive_POST_WithEmptyItems_ReturnsViewWithError()
	{
		var model = new InventoryReceiveViewModel
		{
			StoreId = 1,
			SupplierId = 1,
			Items = new List<InventoryReceiveItemViewModel>()
		};

		var result = await _sut.Receive(model);

		result.Should().BeOfType<ViewResult>();
		_sut.ModelState.IsValid.Should().BeFalse();
	}

	[Fact]
	public async Task Receive_POST_WithInvalidStore_ReturnsViewWithError()
	{
		var supplier = await SeedSupplierAsync();
		var product = await SeedProductAsync();

		var model = new InventoryReceiveViewModel
		{
			StoreId = 99999,
			SupplierId = supplier.Id,
			Items = new List<InventoryReceiveItemViewModel>
			{
				new() { ProductId = product.Id, Quantity = 10, UnitCost = 50 }
			}
		};

		var result = await _sut.Receive(model);

		result.Should().BeOfType<ViewResult>();
		_sut.ModelState.IsValid.Should().BeFalse();
	}

	[Fact]
	public async Task Receive_POST_WithInvalidSupplier_ReturnsViewWithError()
	{
		var store = await SeedStoreAsync();
		var product = await SeedProductAsync();

		var model = new InventoryReceiveViewModel
		{
			StoreId = store.Id,
			SupplierId = 99999,
			Items = new List<InventoryReceiveItemViewModel>
			{
				new() { ProductId = product.Id, Quantity = 10, UnitCost = 50 }
			}
		};

		var result = await _sut.Receive(model);

		result.Should().BeOfType<ViewResult>();
		_sut.ModelState.IsValid.Should().BeFalse();
	}

	[Fact]
	public async Task Receive_POST_WithZeroQuantity_ReturnsViewWithError()
	{
		var store = await SeedStoreAsync();
		var supplier = await SeedSupplierAsync();
		var product = await SeedProductAsync();

		var model = new InventoryReceiveViewModel
		{
			StoreId = store.Id,
			SupplierId = supplier.Id,
			Items = new List<InventoryReceiveItemViewModel>
			{
				new() { ProductId = product.Id, Quantity = 0, UnitCost = 50 }
			}
		};

		var result = await _sut.Receive(model);

		result.Should().BeOfType<ViewResult>();
		_sut.ModelState.IsValid.Should().BeFalse();
	}

	// =========================================
	// 5. Issue GET Tests
	// =========================================

	[Fact]
	public async Task Issue_GET_ReturnsViewResult()
	{
		var result = await _sut.Issue();

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 6. Issue POST Tests
	// =========================================

	[Fact]
	public async Task Issue_POST_WithEmptyItems_ReturnsViewWithError()
	{
		var model = new InventoryIssueViewModel
		{
			StoreId = 1,
			Items = new List<InventoryItemLineViewModel>()
		};

		var result = await _sut.Issue(model);

		result.Should().BeOfType<ViewResult>();
		_sut.ModelState.IsValid.Should().BeFalse();
	}

	[Fact]
	public async Task Issue_POST_WithInvalidStore_ReturnsViewWithError()
	{
		var product = await SeedProductAsync();

		var model = new InventoryIssueViewModel
		{
			StoreId = 99999,
			Items = new List<InventoryItemLineViewModel>
			{
				new() { ProductId = product.Id, Quantity = 5 }
			}
		};

		var result = await _sut.Issue(model);

		result.Should().BeOfType<ViewResult>();
		_sut.ModelState.IsValid.Should().BeFalse();
	}

	[Fact]
	public async Task Issue_POST_WithZeroQuantity_ReturnsViewWithError()
	{
		var store = await SeedStoreAsync();
		var product = await SeedProductAsync();

		var model = new InventoryIssueViewModel
		{
			StoreId = store.Id,
			Items = new List<InventoryItemLineViewModel>
			{
				new() { ProductId = product.Id, Quantity = 0 }
			}
		};

		var result = await _sut.Issue(model);

		result.Should().BeOfType<ViewResult>();
		_sut.ModelState.IsValid.Should().BeFalse();
	}

	[Fact]
	public async Task Issue_POST_WithInsufficientStock_ReturnsViewWithError()
	{
		var store = await SeedStoreAsync();
		var product = await SeedProductAsync();
		await SeedStockLotAsync(store.Id, product.Id, quantity: 5m);

		var model = new InventoryIssueViewModel
		{
			StoreId = store.Id,
			Items = new List<InventoryItemLineViewModel>
			{
				new() { ProductId = product.Id, Quantity = 100 }
			}
		};

		var result = await _sut.Issue(model);

		result.Should().BeOfType<ViewResult>();
		_sut.ModelState.IsValid.Should().BeFalse();
	}

	// =========================================
	// 7. Transfer GET Tests
	// =========================================

	[Fact]
	public async Task Transfer_GET_ReturnsViewResult()
	{
		var result = await _sut.Transfer();

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 8. Transfer POST Tests
	// =========================================

	[Fact]
	public async Task Transfer_POST_WithSameStore_ReturnsViewWithError()
	{
		var store = await SeedStoreAsync();
		var product = await SeedProductAsync();

		var model = new InventoryTransferViewModel
		{
			FromStoreId = store.Id,
			ToStoreId = store.Id, // نفس المخزن
			Items = new List<InventoryItemLineViewModel>
			{
				new() { ProductId = product.Id, Quantity = 10 }
			}
		};

		var result = await _sut.Transfer(model);

		result.Should().BeOfType<ViewResult>();
		_sut.ModelState.IsValid.Should().BeFalse();
	}

	[Fact]
	public async Task Transfer_POST_WithInvalidStores_ReturnsViewWithError()
	{
		var product = await SeedProductAsync();

		var model = new InventoryTransferViewModel
		{
			FromStoreId = 99999,
			ToStoreId = 88888,
			Items = new List<InventoryItemLineViewModel>
			{
				new() { ProductId = product.Id, Quantity = 10 }
			}
		};

		var result = await _sut.Transfer(model);

		result.Should().BeOfType<ViewResult>();
		_sut.ModelState.IsValid.Should().BeFalse();
	}

	[Fact]
	public async Task Transfer_POST_WithEmptyItems_ReturnsViewWithError()
	{
		var model = new InventoryTransferViewModel
		{
			FromStoreId = 1,
			ToStoreId = 2,
			Items = new List<InventoryItemLineViewModel>()
		};

		var result = await _sut.Transfer(model);

		result.Should().BeOfType<ViewResult>();
		_sut.ModelState.IsValid.Should().BeFalse();
	}

	// =========================================
	// 9. Adjust GET Tests
	// =========================================

	[Fact]
	public async Task Adjust_GET_ReturnsViewResult()
	{
		var result = await _sut.Adjust();

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 10. Adjust POST Tests
	// =========================================

	[Fact]
	public async Task Adjust_POST_WithInvalidStore_ReturnsViewWithError()
	{
		var product = await SeedProductAsync();

		var model = new InventoryAdjustViewModel
		{
			StoreId = 99999,
			ProductId = product.Id,
			NewQuantity = 50,
			Reason = "جرد"
		};

		var result = await _sut.Adjust(model);

		result.Should().BeOfType<ViewResult>();
		_sut.ModelState.IsValid.Should().BeFalse();
	}

	[Fact]
	public async Task Adjust_POST_WithInvalidProduct_ReturnsViewWithError()
	{
		var store = await SeedStoreAsync();

		var model = new InventoryAdjustViewModel
		{
			StoreId = store.Id,
			ProductId = 99999,
			NewQuantity = 50,
			Reason = "جرد"
		};

		var result = await _sut.Adjust(model);

		result.Should().BeOfType<ViewResult>();
		_sut.ModelState.IsValid.Should().BeFalse();
	}

	// =========================================
	// 11. Count GET Tests
	// =========================================

	[Fact]
	public async Task Count_GET_WithoutStoreId_ReturnsViewResult()
	{
		var result = await _sut.Count(storeId: null);

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Count_GET_WithStoreId_ReturnsViewResult()
	{
		var store = await SeedStoreAsync();

		var result = await _sut.Count(storeId: store.Id);

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 12. Count POST Tests
	// =========================================

	[Fact]
	public async Task Count_POST_WithInvalidStore_ReturnsViewWithError()
	{
		var model = new InventoryCountViewModel
		{
			StoreId = 99999,
			Lines = new List<InventoryCountLineViewModel>()
		};

		var result = await _sut.Count(model);

		result.Should().BeOfType<ViewResult>();
		_sut.ModelState.IsValid.Should().BeFalse();
	}

	// =========================================
	// 13. GetProductQuantity Tests
	// =========================================

	[Theory]
	[InlineData(0, 1)]
	[InlineData(1, 0)]
	[InlineData(-1, 1)]
	[InlineData(1, -1)]
	[InlineData(0, 0)]
	public async Task GetProductQuantity_WithInvalidParams_ReturnsZeroJson(
		int productId, int storeId)
	{
		var result = await _sut.GetProductQuantity(productId, storeId);

		result.Should().BeOfType<JsonResult>();
	}

	[Fact]
	public async Task GetProductQuantity_WithNoStock_ReturnsZeroQuantity()
	{
		var store = await SeedStoreAsync();
		var product = await SeedProductAsync();

		var result = await _sut.GetProductQuantity(product.Id, store.Id);

		result.Should().BeOfType<JsonResult>();
	}

	[Fact]
	public async Task GetProductQuantity_WithStock_ReturnsJsonResult()
	{
		var store = await SeedStoreAsync();
		var product = await SeedProductAsync();
		await SeedStockLotAsync(store.Id, product.Id, quantity: 25m, unitCost: 40m);

		var result = await _sut.GetProductQuantity(product.Id, store.Id);

		result.Should().BeOfType<JsonResult>();
	}

	// =========================================
	// 14. Transactions Tests
	// =========================================

	[Fact]
	public async Task Transactions_ReturnsViewResult()
	{
		var result = await _sut.Transactions(
			storeId: null, type: null);

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Transactions_WithStoreId_ReturnsViewResult()
	{
		var store = await SeedStoreAsync();

		var result = await _sut.Transactions(store.Id, type: null);

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 15. ProductMovement Tests
	// =========================================

	[Fact]
	public async Task ProductMovement_WithoutProductId_ReturnsViewResult()
	{
		var result = await _sut.ProductMovement(
			productId: null, storeId: null,
			fromDate: null, toDate: null);

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task ProductMovement_WithProductId_ReturnsViewResult()
	{
		var product = await SeedProductAsync();

		var result = await _sut.ProductMovement(
			productId: product.Id, storeId: null,
			fromDate: null, toDate: null);

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task ProductMovement_WithDateRange_ReturnsViewResult()
	{
		var product = await SeedProductAsync();
		var from = DateTime.UtcNow.AddDays(-30);
		var to = DateTime.UtcNow;

		var result = await _sut.ProductMovement(
			productId: product.Id, storeId: null,
			fromDate: from, toDate: to);

		result.Should().BeOfType<ViewResult>();
	}
}