using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AccountingSystem.Controllers;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Models.ViewModels.Returns;
using AccountingSystem.Services;
using AccountingSystem.Services.EmployeeScope;
using AccountingSystem.Services.Pdf;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AccountingSystem.Tests;

public sealed class SalesReturnFifoCostTests : IAsyncLifetime
{
    private readonly string _databaseName =
        $"SalesReturnFifoTest_{Guid.NewGuid():N}";

    private readonly string _connectionString;

    private const string MasterConnectionString =
        @"Server=.\SQLEXPRESS;Database=master;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=60";

    public SalesReturnFifoCostTests()
    {
        _connectionString =
            $@"Server=.\SQLEXPRESS;Database={_databaseName};Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=60";
    }

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        try
        {
            await using var conn = new SqlConnection(MasterConnectionString);
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
IF DB_ID(N'{_databaseName}') IS NOT NULL
BEGIN
    ALTER DATABASE [{_databaseName}]
    SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [{_databaseName}];
END";
            await cmd.ExecuteNonQueryAsync();
        }
        catch
        {
            // best-effort
        }
    }

    private ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(_connectionString)
            .Options;

        return new ApplicationDbContext(options);
    }

    // =========================================
    // Seed: يجهّز مستخدم + فاتورة مبيعة + دفعتين
    // =========================================
    private sealed record SeedResult(
        int UserId,
        int SalesInvoiceItemId,
        int SaleItemQuantityInBaseUnit,
        int LotAId,
        int LotBId);

    private async Task<SeedResult> SeedSaleWithTwoLotsAsync(
        decimal lotAUnitCost = 10m,
        decimal lotBUnitCost = 20m,
        decimal lotAQty = 5m,
        decimal lotBQty = 5m)
    {
        await using var ctx = CreateContext();

        // ✅ إنشاء المستخدم أولًا (لأن SalesReturnInvoice يحتاج CreatedByUserId)
        var user = new User
        {
            Phone = "01000000001",
            FullName = "Test User",
            UserType = UserType.Admin,
            Status = UserStatus.Approved,
            IsActive = true,
            IsPasswordSet = true,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var company = new Company
        {
            Name = "Test Co",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Companies.Add(company);
        await ctx.SaveChangesAsync();

        var branch = new Branch
        {
            CompanyId = company.Id,
            Name = "Main Branch",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Branches.Add(branch);
        await ctx.SaveChangesAsync();

        var store = new Store
        {
            BranchId = branch.Id,
            Name = "Main Store",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Stores.Add(store);
        await ctx.SaveChangesAsync();

        var unit = new Unit
        {
            Name = "قطعة",
            ShortName = "ق",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Units.Add(unit);
        await ctx.SaveChangesAsync();

        var product = new Product
        {
            Name = "Test Product",
            Code = "TP-001",
            BaseUnitId = unit.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Products.Add(product);
        await ctx.SaveChangesAsync();

        ctx.ProductUnits.Add(new ProductUnit
        {
            ProductId = product.Id,
            UnitId = unit.Id,
            ConversionFactor = 1m,
            SalePrice = 100m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var customer = new Customer
        {
            Name = "Test Customer",
            Phone = "01000000001",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        ctx.Customers.Add(customer);
        await ctx.SaveChangesAsync();

        // فاتورة بيع Confirmed
        var invoice = new SalesInvoice
        {
            InvoiceNumber = "SAL-TEST-001",
            BranchId = branch.Id,
            StoreId = store.Id,
            CustomerId = customer.Id,
            InvoiceDate = DateTime.Today,
            SubTotal = 1000m,
            TotalAmount = 1000m,
            PaymentMethod = SalesPaymentMethod.Cash,
            Status = SalesInvoiceStatus.Confirmed,
            ConfirmedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        ctx.SalesInvoices.Add(invoice);
        await ctx.SaveChangesAsync();

        var saleItem = new SalesInvoiceItem
        {
            SalesInvoiceId = invoice.Id,
            ProductId = product.Id,
            UnitId = unit.Id,
            ConversionFactor = 1m,
            Quantity = lotAQty + lotBQty,
            QuantityInBaseUnit = lotAQty + lotBQty,
            UnitPrice = 100m,
            TotalPrice = (lotAQty + lotBQty) * 100m,
            IssueMethod = SalesInvoice.StockIssueMethod.FIFO
        };
        ctx.SalesInvoiceItems.Add(saleItem);
        await ctx.SaveChangesAsync();

        // دفعتان (Lot A الأقدم، Lot B الأحدث)
        var lotA = new StockLot
        {
            StoreId = store.Id,
            ProductId = product.Id,
            QuantityReceived = lotAQty,
            QuantityRemaining = 0m,
            UnitCost = lotAUnitCost,
            PurchaseDate = DateTime.Today.AddDays(-10),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var lotB = new StockLot
        {
            StoreId = store.Id,
            ProductId = product.Id,
            QuantityReceived = lotBQty,
            QuantityRemaining = 0m,
            UnitCost = lotBUnitCost,
            PurchaseDate = DateTime.Today.AddDays(-5),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        ctx.StockLots.Add(lotA);
        ctx.StockLots.Add(lotB);
        await ctx.SaveChangesAsync();

        // LotAllocations
        ctx.SalesInvoiceItemLots.Add(new SalesInvoiceItemLot
        {
            SalesInvoiceItemId = saleItem.Id,
            StockLotId = lotA.Id,
            QuantityBaseUnit = lotAQty,
            UnitCost = lotAUnitCost,
            TotalCost = lotAQty * lotAUnitCost
        });
        ctx.SalesInvoiceItemLots.Add(new SalesInvoiceItemLot
        {
            SalesInvoiceItemId = saleItem.Id,
            StockLotId = lotB.Id,
            QuantityBaseUnit = lotBQty,
            UnitCost = lotBUnitCost,
            TotalCost = lotBQty * lotBUnitCost
        });
        await ctx.SaveChangesAsync();

        return new SeedResult(
            UserId: user.Id,
            SalesInvoiceItemId: saleItem.Id,
            SaleItemQuantityInBaseUnit: (int)(lotAQty + lotBQty),
            LotAId: lotA.Id,
            LotBId: lotB.Id);
    }

    // =========================================
    // Helper: يبني SalesReturnController بـ mocks
    // =========================================
    private (SalesReturnController controller,
            ApplicationDbContext ctx,
            Mock<IPostingService> postingMock)
        BuildController(
            ApplicationDbContext ctx,
            int userId,
            Action<decimal> onCostCaptured)
    {
        var scopeService = new Mock<IEmployeeScopeService>();

        scopeService
            .Setup(x => x.GetScopeAsync())
            .ReturnsAsync(EmployeeScope.Admin(userId: userId));

        scopeService
            .Setup(x => x.ApplyStoreFilter(
                It.IsAny<IQueryable<Store>>(),
                It.IsAny<EmployeeScope>()))
            .Returns((IQueryable<Store> query, EmployeeScope s) => query);

        var postingMock = new Mock<IPostingService>();

        postingMock
            .Setup(x => x.PostSalesReturnAsync(
                It.IsAny<SalesReturnInvoice>(),
                It.IsAny<decimal>(),
                It.IsAny<int?>()))
            .Callback<SalesReturnInvoice, decimal, int?>(
                (inv, cost, uid) => onCostCaptured(cost))
            .ReturnsAsync(JournalPostingResult.Ok(1, "JRN-TEST-001"));

var docNumberMock = new Mock<IDocumentNumberService>();

docNumberMock
    .Setup(x => x.GenerateNumberAsync(
        It.IsAny<string>(),
        It.IsAny<string>(),
        It.IsAny<string>()))
    .ReturnsAsync(() => $"SRT-{Guid.NewGuid():N}");

        var controller = new SalesReturnController(
            ctx,
            scopeService.Object,
            postingMock.Object,
            docNumberMock.Object);

        // ===== HttpContext مع Claims — userId الفعلي =====
        var httpContext = new DefaultHttpContext();

        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString())
            }, "test"));

        controller.TempData = new TempDataDictionary(
            httpContext,
            new NoopTempDataProvider());

        controller.ControllerContext =
            new ControllerContext { HttpContext = httpContext };

        return (controller, ctx, postingMock);
    }

    // =========================================
    // Helper: يكشف خطأ الـ Controller
    // =========================================
    private static void ThrowIfControllerError(SalesReturnController controller)
    {
        if (controller.TempData.TryGetValue("Error", out var errObj)
            && errObj is string errStr)
        {
            throw new Xunit.Sdk.XunitException($"Controller Error: {errStr}");
        }
    }

    // =========================================
    // الاختبار 1: COGS من Lot A فقط (FIFO)
    // =========================================
    [Fact]
    public async Task Create_ReturnFiveUnits_ConsumesFromLotAOnly()
    {
        var seed = await SeedSaleWithTwoLotsAsync(
            lotAUnitCost: 10m,
            lotBUnitCost: 20m,
            lotAQty: 5m,
            lotBQty: 5m);

        decimal capturedCost = -1;

        await using var ctx = CreateContext();
        var (controller, _, _) = BuildController(ctx, seed.UserId, c => capturedCost = c);

        var model = new SalesReturnViewModel
        {
            OriginalSalesInvoiceId = 1,
            CustomerId = 1,
            StoreId = 1,
            ReturnDate = DateTime.Today,
            Items = new List<SalesReturnItemViewModel>
            {
                new()
                {
                    SalesInvoiceItemId = seed.SalesInvoiceItemId,
                    ProductId = 1,
                    UnitId = 1,
                    ConversionFactor = 1m,
                    Quantity = 5m
                }
            }
        };

        var result = await controller.Create(model);

        ThrowIfControllerError(controller);

        Assert.Equal(50m, capturedCost);

        await using var verifyCtx = CreateContext();

        var lotA = await verifyCtx.StockLots.FirstAsync(x => x.Id == seed.LotAId);
        var lotB = await verifyCtx.StockLots.FirstAsync(x => x.Id == seed.LotBId);

        Assert.Equal(5m, lotA.QuantityRemaining);
        Assert.Equal(0m, lotB.QuantityRemaining);
    }

    // =========================================
    // الاختبار 2: مرتجع ثانٍ من Lot B
    // =========================================
    [Fact]
    public async Task Create_SecondReturn_ConsumesFromLotBOnly()
    {
        var seed = await SeedSaleWithTwoLotsAsync();

        var firstCost = 0m;

        await using (var ctx1 = CreateContext())
        {
            var (ctrl1, _, _) = BuildController(ctx1, seed.UserId, c => firstCost = c);

            await ctrl1.Create(new SalesReturnViewModel
            {
                OriginalSalesInvoiceId = 1,
                CustomerId = 1,
                StoreId = 1,
                ReturnDate = DateTime.Today,
                Items = new List<SalesReturnItemViewModel>
                {
                    new()
                    {
                        SalesInvoiceItemId = seed.SalesInvoiceItemId,
                        ProductId = 1,
                        UnitId = 1,
                        ConversionFactor = 1m,
                        Quantity = 5m
                    }
                }
            });

            ThrowIfControllerError(ctrl1);
        }

        Assert.Equal(50m, firstCost);

        var secondCost = 0m;

        await using (var ctx2 = CreateContext())
        {
            var (ctrl2, _, _) = BuildController(ctx2, seed.UserId, c => secondCost = c);

            await ctrl2.Create(new SalesReturnViewModel
            {
                OriginalSalesInvoiceId = 1,
                CustomerId = 1,
                StoreId = 1,
                ReturnDate = DateTime.Today,
                Items = new List<SalesReturnItemViewModel>
                {
                    new()
                    {
                        SalesInvoiceItemId = seed.SalesInvoiceItemId,
                        ProductId = 1,
                        UnitId = 1,
                        ConversionFactor = 1m,
                        Quantity = 5m
                    }
                }
            });

            ThrowIfControllerError(ctrl2);
        }

        Assert.Equal(100m, secondCost);

        await using var verifyCtx = CreateContext();

        var lotA = await verifyCtx.StockLots.FirstAsync(x => x.Id == seed.LotAId);
        var lotB = await verifyCtx.StockLots.FirstAsync(x => x.Id == seed.LotBId);

        Assert.Equal(5m, lotA.QuantityRemaining);
        Assert.Equal(5m, lotB.QuantityRemaining);
    }

    // =========================================
    // الاختبار 3: مرتجع أكثر من المتاح → error
    // =========================================
    [Fact]
    public async Task Create_ReturnMoreThanSold_DoesNotConsume()
    {
        var seed = await SeedSaleWithTwoLotsAsync();

        decimal capturedCost = -1;

        await using var ctx = CreateContext();
        var (controller, _, _) = BuildController(ctx, seed.UserId, c => capturedCost = c);

        var model = new SalesReturnViewModel
        {
            OriginalSalesInvoiceId = 1,
            CustomerId = 1,
            StoreId = 1,
            ReturnDate = DateTime.Today,
            Items = new List<SalesReturnItemViewModel>
            {
                new()
                {
                    SalesInvoiceItemId = seed.SalesInvoiceItemId,
                    ProductId = 1,
                    UnitId = 1,
                    ConversionFactor = 1m,
                    Quantity = 20m // > 10 المتاحة
                }
            }
        };

        var result = await controller.Create(model);

        Assert.Equal(-1m, capturedCost);

        await using var verifyCtx = CreateContext();

        var lotA = await verifyCtx.StockLots.FirstAsync(x => x.Id == seed.LotAId);
        var lotB = await verifyCtx.StockLots.FirstAsync(x => x.Id == seed.LotBId);

        Assert.Equal(0m, lotA.QuantityRemaining);
        Assert.Equal(0m, lotB.QuantityRemaining);
    }
}

internal sealed class NoopTempDataProvider : ITempDataProvider
{
    public IDictionary<string, object> LoadTempData(HttpContext context)
        => new Dictionary<string, object>();

    public void SaveTempData(HttpContext context, IDictionary<string, object> values)
    {
        // لا نحفظ شيئًا في الاختبار
    }
}