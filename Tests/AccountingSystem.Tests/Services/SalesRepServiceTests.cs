using AccountingSystem.Models;
using AccountingSystem.Services;
using AccountingSystem.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Security.Claims;

namespace AccountingSystem.Tests.Services;

public class SalesRepServiceTests : BaseTest
{
	private readonly Mock<IHttpContextAccessor> _accessorMock;
	private readonly SalesRepService _sut;

	public SalesRepServiceTests()
	{
		_accessorMock = new Mock<IHttpContextAccessor>();
		_accessorMock
			.Setup(x => x.HttpContext)
			.Returns((HttpContext?)null);

		_sut = new SalesRepService(Context, _accessorMock.Object);
	}

	// =========================================
	// Helpers
	// =========================================

	private void SetHttpContext(int userId)
	{
		var context = new DefaultHttpContext();
		var claims = new List<Claim>
		{
			new(ClaimTypes.NameIdentifier, userId.ToString())
		};
		var identity = new ClaimsIdentity(claims, "TestAuth");
		context.User = new ClaimsPrincipal(identity);
		_accessorMock.Setup(x => x.HttpContext).Returns(context);
	}

	private void SetHttpContextWithoutClaim()
	{
		var context = new DefaultHttpContext();
		context.User = new ClaimsPrincipal(new ClaimsIdentity());
		_accessorMock.Setup(x => x.HttpContext).Returns(context);
	}

	private async Task<User> SeedUserAsync(int? employeeId = null)
	{
		var user = new User
		{
			Phone = $"0100000{Random.Shared.Next(1000, 9999)}",
			UserType = UserType.Employee,
			EmployeeId = employeeId,
			Status = UserStatus.Approved,
			IsActive = true,
			IsPasswordSet = true,
			CreatedAt = DateTime.UtcNow
		};
		Context.Users.Add(user);
		await Context.SaveChangesAsync();
		return user;
	}

	private async Task<Employee> SeedEmployeeAsync()
	{
		var employee = new Employee
		{
			Name = "Test Rep",
			Phone = $"0100000{Random.Shared.Next(1000, 9999)}",
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		Context.Employees.Add(employee);
		await Context.SaveChangesAsync();
		return employee;
	}

	private async Task<SalesRepProfile> SeedRepProfileAsync(
		int employeeId,
		bool isActive = true,
		int? custodyStoreId = null,
		int? cashAccountId = null,
		CommissionType commissionType = CommissionType.PercentOfSales,
		decimal commissionRate = 5m)
	{
		var profile = new SalesRepProfile
		{
			EmployeeId = employeeId,
			IsActive = isActive,
			CustodyStoreId = custodyStoreId,
			CashAccountId = cashAccountId,
			CommissionType = commissionType,
			CommissionRate = commissionRate,
			MonthlyTarget = 10000m,
			CreatedAt = DateTime.UtcNow
		};
		Context.SalesRepProfiles.Add(profile);
		await Context.SaveChangesAsync();
		return profile;
	}

	private async Task<Store> SeedStoreAsync(string name = "Custody Store")
	{
		var store = new Store
		{
			Name = name,
			BranchId = 1,
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		Context.Stores.Add(store);
		await Context.SaveChangesAsync();
		return store;
	}

	private async Task<SalesInvoice> SeedSalesInvoiceAsync(
		int? salesRepId,
		decimal totalAmount = 100m,
		DateTime? invoiceDate = null,
		SalesInvoiceStatus status = SalesInvoiceStatus.Confirmed)
	{
		var invoice = new SalesInvoice
		{
			InvoiceNumber = $"SAL-{Guid.NewGuid().ToString("N").Substring(0, 8)}",
			InvoiceDate = invoiceDate ?? DateTime.UtcNow,
			BranchId = 1,
			StoreId = 1,
			CustomerId = 1,
			SalesRepId = salesRepId,
			SubTotal = totalAmount,
			TotalAmount = totalAmount,
			Status = status,
			PaymentMethod = SalesPaymentMethod.Cash,
			CreatedAt = DateTime.UtcNow,
			RowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }
		};
		Context.SalesInvoices.Add(invoice);
		await Context.SaveChangesAsync();
		return invoice;
	}

	private async Task<Customer> SeedCustomerAsync(int? salesRepId)
	{
		var customer = new Customer
		{
			Name = $"Customer {Random.Shared.Next(1000, 9999)}",
			Phone = $"0100000{Random.Shared.Next(1000, 9999)}",
			SalesRepId = salesRepId,
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		Context.Customers.Add(customer);
		await Context.SaveChangesAsync();
		return customer;
	}

		private async Task<StockLot> SeedStockLotAsync(
		int storeId,
		int productId,
		decimal quantity = 10m,
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
			// ✅ (InMemory fix) RowVersion مش بيتولد تلقائياً
			RowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }
		};
		Context.StockLots.Add(lot);
		await Context.SaveChangesAsync();
		return lot;
	}

	private async Task<CashAccount> SeedCashAccountAsync()
	{
		var cashAccount = new CashAccount
		{
			Name = $"Cash {Random.Shared.Next(1000, 9999)}",
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		Context.CashAccounts.Add(cashAccount);
		await Context.SaveChangesAsync();
		return cashAccount;
	}

	// =========================================
	// 1. GetCurrentRepIdAsync
	// =========================================

	[Fact]
	public async Task GetCurrentRepIdAsync_WhenHttpContextIsNull_ReturnsNull()
	{
		var result = await _sut.GetCurrentRepIdAsync();

		result.Should().BeNull();
	}

	[Fact]
	public async Task GetCurrentRepIdAsync_WhenNoClaim_ReturnsNull()
	{
		SetHttpContextWithoutClaim();

		var result = await _sut.GetCurrentRepIdAsync();

		result.Should().BeNull();
	}

	[Fact]
	public async Task GetCurrentRepIdAsync_WhenUserNotFound_ReturnsNull()
	{
		SetHttpContext(userId: 99999);

		var result = await _sut.GetCurrentRepIdAsync();

		result.Should().BeNull();
	}

	[Fact]
	public async Task GetCurrentRepIdAsync_WhenUserIsNotRep_ReturnsNull()
	{
		var user = await SeedUserAsync();
		SetHttpContext(user.Id);

		var result = await _sut.GetCurrentRepIdAsync();

		result.Should().BeNull();
	}

	[Fact]
	public async Task GetCurrentRepIdAsync_WhenRepIsInactive_ReturnsNull()
	{
		var employee = await SeedEmployeeAsync();
		var user = await SeedUserAsync(employee.Id);
		await SeedRepProfileAsync(employee.Id, isActive: false);
		SetHttpContext(user.Id);

		var result = await _sut.GetCurrentRepIdAsync();

		result.Should().BeNull();
	}

	[Fact]
	public async Task GetCurrentRepIdAsync_WhenRepIsActive_ReturnsRepId()
	{
		var employee = await SeedEmployeeAsync();
		var user = await SeedUserAsync(employee.Id);
		var profile = await SeedRepProfileAsync(employee.Id, isActive: true);
		SetHttpContext(user.Id);

		var result = await _sut.GetCurrentRepIdAsync();

		result.Should().Be(profile.Id);
	}

	[Fact]
	public async Task GetCurrentRepIdAsync_CalledTwice_ReturnsSameValue()
	{
		var employee = await SeedEmployeeAsync();
		var user = await SeedUserAsync(employee.Id);
		var profile = await SeedRepProfileAsync(employee.Id);
		SetHttpContext(user.Id);

		var result1 = await _sut.GetCurrentRepIdAsync();
		var result2 = await _sut.GetCurrentRepIdAsync();

		result1.Should().Be(profile.Id);
		result2.Should().Be(profile.Id);
	}

	// =========================================
	// 2. GetCurrentRepProfileAsync
	// =========================================

	[Fact]
	public async Task GetCurrentRepProfileAsync_WhenNoRep_ReturnsNull()
	{
		SetHttpContext(userId: 1);

		var result = await _sut.GetCurrentRepProfileAsync();

		result.Should().BeNull();
	}

	[Fact]
	public async Task GetCurrentRepProfileAsync_WhenRepExists_ReturnsProfile()
	{
		var employee = await SeedEmployeeAsync();
		var user = await SeedUserAsync(employee.Id);
		var store = await SeedStoreAsync();
		var cashAccount = await SeedCashAccountAsync();
		var profile = await SeedRepProfileAsync(
			employee.Id,
			custodyStoreId: store.Id,
			cashAccountId: cashAccount.Id);
		SetHttpContext(user.Id);

		var result = await _sut.GetCurrentRepProfileAsync();

		result.Should().NotBeNull();
		result!.Id.Should().Be(profile.Id);
		result.Employee.Should().NotBeNull();
		result.CustodyStore.Should().NotBeNull();
		result.CashAccount.Should().NotBeNull();
	}

	// =========================================
	// 3. GetTodaySalesAsync
	// =========================================

	[Fact]
	public async Task GetTodaySalesAsync_WhenNoRep_ReturnsZero()
	{
		SetHttpContext(userId: 1);

		var result = await _sut.GetTodaySalesAsync();

		result.Should().Be(0m);
	}

	[Fact]
	public async Task GetTodaySalesAsync_WhenNoInvoices_ReturnsZero()
	{
		var employee = await SeedEmployeeAsync();
		var user = await SeedUserAsync(employee.Id);
		var profile = await SeedRepProfileAsync(employee.Id);
		SetHttpContext(user.Id);

		var result = await _sut.GetTodaySalesAsync();

		result.Should().Be(0m);
	}

	[Fact]
	public async Task GetTodaySalesAsync_WithTodayInvoices_SumsThem()
	{
		var employee = await SeedEmployeeAsync();
		var user = await SeedUserAsync(employee.Id);
		var profile = await SeedRepProfileAsync(employee.Id);
		SetHttpContext(user.Id);

		await SeedSalesInvoiceAsync(profile.Id, 100m, DateTime.UtcNow);
		await SeedSalesInvoiceAsync(profile.Id, 200m, DateTime.UtcNow);

		var result = await _sut.GetTodaySalesAsync();

		result.Should().Be(300m);
	}

	[Fact]
	public async Task GetTodaySalesAsync_IgnoresYesterdayInvoices()
	{
		var employee = await SeedEmployeeAsync();
		var user = await SeedUserAsync(employee.Id);
		var profile = await SeedRepProfileAsync(employee.Id);
		SetHttpContext(user.Id);

		await SeedSalesInvoiceAsync(profile.Id, 100m, DateTime.UtcNow);
		await SeedSalesInvoiceAsync(profile.Id, 500m, DateTime.UtcNow.AddDays(-1));

		var result = await _sut.GetTodaySalesAsync();

		result.Should().Be(100m);
	}

	[Fact]
	public async Task GetTodaySalesAsync_IgnoresOtherRepsInvoices()
	{
		var employee1 = await SeedEmployeeAsync();
		var employee2 = await SeedEmployeeAsync();
		var user1 = await SeedUserAsync(employee1.Id);
		var profile1 = await SeedRepProfileAsync(employee1.Id);
		var profile2 = await SeedRepProfileAsync(employee2.Id);
		SetHttpContext(user1.Id);

		await SeedSalesInvoiceAsync(profile1.Id, 100m, DateTime.UtcNow);
		await SeedSalesInvoiceAsync(profile2.Id, 999m, DateTime.UtcNow);

		var result = await _sut.GetTodaySalesAsync();

		result.Should().Be(100m);
	}

	[Fact]
	public async Task GetTodaySalesAsync_IgnoresDraftInvoices()
	{
		var employee = await SeedEmployeeAsync();
		var user = await SeedUserAsync(employee.Id);
		var profile = await SeedRepProfileAsync(employee.Id);
		SetHttpContext(user.Id);

		await SeedSalesInvoiceAsync(profile.Id, 100m, DateTime.UtcNow, SalesInvoiceStatus.Confirmed);
		await SeedSalesInvoiceAsync(profile.Id, 500m, DateTime.UtcNow, SalesInvoiceStatus.Draft);

		var result = await _sut.GetTodaySalesAsync();

		result.Should().Be(100m);
	}

	// =========================================
	// 4. GetMonthSalesAsync
	// =========================================

	[Fact]
	public async Task GetMonthSalesAsync_WhenNoRep_ReturnsZero()
	{
		SetHttpContext(userId: 1);

		var result = await _sut.GetMonthSalesAsync();

		result.Should().Be(0m);
	}

	[Fact]
	public async Task GetMonthSalesAsync_WithInvoicesThisMonth_SumsThem()
	{
		var employee = await SeedEmployeeAsync();
		var user = await SeedUserAsync(employee.Id);
		var profile = await SeedRepProfileAsync(employee.Id);
		SetHttpContext(user.Id);

		await SeedSalesInvoiceAsync(profile.Id, 1000m, DateTime.UtcNow);
		await SeedSalesInvoiceAsync(profile.Id, 2000m, DateTime.UtcNow);

		var result = await _sut.GetMonthSalesAsync();

		result.Should().Be(3000m);
	}

	[Fact]
	public async Task GetMonthSalesAsync_IgnoresPreviousMonth()
	{
		var employee = await SeedEmployeeAsync();
		var user = await SeedUserAsync(employee.Id);
		var profile = await SeedRepProfileAsync(employee.Id);
		SetHttpContext(user.Id);

		await SeedSalesInvoiceAsync(profile.Id, 100m, DateTime.UtcNow);
		await SeedSalesInvoiceAsync(profile.Id, 9999m, DateTime.UtcNow.AddMonths(-1));

		var result = await _sut.GetMonthSalesAsync();

		result.Should().Be(100m);
	}

	// =========================================
	// 5. GetCustodyBalanceAsync
	// =========================================

	[Fact]
	public async Task GetCustodyBalanceAsync_WhenNoRep_ReturnsZero()
	{
		SetHttpContext(userId: 1);

		var result = await _sut.GetCustodyBalanceAsync();

		result.Should().Be(0m);
	}

	[Fact]
	public async Task GetCustodyBalanceAsync_WhenNoCustodyStore_ReturnsZero()
	{
		var employee = await SeedEmployeeAsync();
		var user = await SeedUserAsync(employee.Id);
		await SeedRepProfileAsync(employee.Id, custodyStoreId: null);
		SetHttpContext(user.Id);

		var result = await _sut.GetCustodyBalanceAsync();

		result.Should().Be(0m);
	}

	[Fact]
	public async Task GetCustodyBalanceAsync_SumsActiveStockLots()
	{
		var employee = await SeedEmployeeAsync();
		var user = await SeedUserAsync(employee.Id);
		var store = await SeedStoreAsync();
		var profile = await SeedRepProfileAsync(employee.Id, custodyStoreId: store.Id);
		SetHttpContext(user.Id);

		await SeedStockLotAsync(store.Id, productId: 1, quantity: 10m, unitCost: 50m);
		await SeedStockLotAsync(store.Id, productId: 2, quantity: 5m, unitCost: 100m);

		var result = await _sut.GetCustodyBalanceAsync();

		result.Should().Be(1000m); // 10*50 + 5*100 = 500 + 500
	}

	// =========================================
	// 6. GetCashBalanceAsync
	// =========================================

	[Fact]
	public async Task GetCashBalanceAsync_WhenNoRep_ReturnsZero()
	{
		SetHttpContext(userId: 1);

		var result = await _sut.GetCashBalanceAsync();

		result.Should().Be(0m);
	}

	[Fact]
	public async Task GetCashBalanceAsync_WhenNoCashAccount_ReturnsZero()
	{
		var employee = await SeedEmployeeAsync();
		var user = await SeedUserAsync(employee.Id);
		await SeedRepProfileAsync(employee.Id, cashAccountId: null);
		SetHttpContext(user.Id);

		var result = await _sut.GetCashBalanceAsync();

		result.Should().Be(0m);
	}

	[Fact]
	public async Task GetCashBalanceAsync_WithReceiveTransaction_ReturnsPositive()
	{
		var employee = await SeedEmployeeAsync();
		var user = await SeedUserAsync(employee.Id);
		var cashAccount = await SeedCashAccountAsync();
		var profile = await SeedRepProfileAsync(employee.Id, cashAccountId: cashAccount.Id);
		SetHttpContext(user.Id);

		Context.TreasuryTransactions.Add(new TreasuryTransaction
		{
			TransactionNumber = "TX-001",
			Type = TreasuryTransactionType.Receive,
			CashAccountId = cashAccount.Id,
			Amount = 500m,
			CreatedAt = DateTime.UtcNow
		});
		await Context.SaveChangesAsync();

		var result = await _sut.GetCashBalanceAsync();

		result.Should().Be(500m);
	}

	// =========================================
	// 7. GetCommissionEarnedAsync — PercentOfSales
	// =========================================

	[Fact]
	public async Task GetCommissionEarnedAsync_WhenNoRep_ReturnsZero()
	{
		SetHttpContext(userId: 1);

		var result = await _sut.GetCommissionEarnedAsync();

		result.Should().Be(0m);
	}

	[Fact]
	public async Task GetCommissionEarnedAsync_PercentOfSales_CalculatesCorrectly()
	{
		var employee = await SeedEmployeeAsync();
		var user = await SeedUserAsync(employee.Id);
		var profile = await SeedRepProfileAsync(
			employee.Id,
			commissionType: CommissionType.PercentOfSales,
			commissionRate: 5m);
		SetHttpContext(user.Id);

		await SeedSalesInvoiceAsync(profile.Id, 1000m, DateTime.UtcNow);
		await SeedSalesInvoiceAsync(profile.Id, 2000m, DateTime.UtcNow);

		var result = await _sut.GetCommissionEarnedAsync();

		result.Should().Be(150m); // 3000 * 5% = 150
	}

	[Fact]
	public async Task GetCommissionEarnedAsync_FixedPerInvoice_CalculatesCorrectly()
	{
		var employee = await SeedEmployeeAsync();
		var user = await SeedUserAsync(employee.Id);
		var profile = await SeedRepProfileAsync(
			employee.Id,
			commissionType: CommissionType.FixedPerInvoice,
			commissionRate: 20m);
		SetHttpContext(user.Id);

		await SeedSalesInvoiceAsync(profile.Id, 1000m, DateTime.UtcNow);
		await SeedSalesInvoiceAsync(profile.Id, 2000m, DateTime.UtcNow);
		await SeedSalesInvoiceAsync(profile.Id, 3000m, DateTime.UtcNow);

		var result = await _sut.GetCommissionEarnedAsync();

		result.Should().Be(60m); // 3 * 20
	}

	// =========================================
	// 8. GetRecentInvoicesAsync
	// =========================================

	[Fact]
	public async Task GetRecentInvoicesAsync_WhenNoRep_ReturnsEmpty()
	{
		SetHttpContext(userId: 1);

		var result = await _sut.GetRecentInvoicesAsync(5);

		result.Should().BeEmpty();
	}

	[Fact]
	public async Task GetRecentInvoicesAsync_ReturnsOnlyRepInvoices()
	{
		var employee1 = await SeedEmployeeAsync();
		var employee2 = await SeedEmployeeAsync();
		var user1 = await SeedUserAsync(employee1.Id);
		var profile1 = await SeedRepProfileAsync(employee1.Id);
		var profile2 = await SeedRepProfileAsync(employee2.Id);
		SetHttpContext(user1.Id);

		await SeedSalesInvoiceAsync(profile1.Id);
		await SeedSalesInvoiceAsync(profile1.Id);
		await SeedSalesInvoiceAsync(profile2.Id);

		var result = await _sut.GetRecentInvoicesAsync(10);

		result.Should().HaveCount(2);
		result.All(x => x.SalesRepId == profile1.Id).Should().BeTrue();
	}

	// =========================================
	// 9. GetMyCustomersAsync
	// =========================================

	[Fact]
	public async Task GetMyCustomersAsync_WhenNoRep_ReturnsEmpty()
	{
		SetHttpContext(userId: 1);

		var result = await _sut.GetMyCustomersAsync();

		result.Should().BeEmpty();
	}

	[Fact]
	public async Task GetMyCustomersAsync_ReturnsOnlyRepCustomers()
	{
		var employee1 = await SeedEmployeeAsync();
		var employee2 = await SeedEmployeeAsync();
		var user1 = await SeedUserAsync(employee1.Id);
		var profile1 = await SeedRepProfileAsync(employee1.Id);
		var profile2 = await SeedRepProfileAsync(employee2.Id);
		SetHttpContext(user1.Id);

		await SeedCustomerAsync(profile1.Id);
		await SeedCustomerAsync(profile1.Id);
		await SeedCustomerAsync(profile2.Id);

		var result = await _sut.GetMyCustomersAsync();

		result.Should().HaveCount(2);
		result.All(x => x.SalesRepId == profile1.Id).Should().BeTrue();
	}

	// =========================================
	// 10. ValidateCashAccountOwnership
	// =========================================

	[Fact]
	public async Task ValidateCashAccountOwnership_WhenNoRep_ReturnsFalse()
	{
		SetHttpContext(userId: 1);

		var result = await _sut.ValidateCashAccountOwnership(1);

		result.Should().BeFalse();
	}

	[Fact]
	public async Task ValidateCashAccountOwnership_WithMatchingAccount_ReturnsTrue()
	{
		var employee = await SeedEmployeeAsync();
		var user = await SeedUserAsync(employee.Id);
		var cashAccount = await SeedCashAccountAsync();
		var profile = await SeedRepProfileAsync(employee.Id, cashAccountId: cashAccount.Id);
		SetHttpContext(user.Id);

		var result = await _sut.ValidateCashAccountOwnership(cashAccount.Id);

		result.Should().BeTrue();
	}

	[Fact]
	public async Task ValidateCashAccountOwnership_WithDifferentAccount_ReturnsFalse()
	{
		var employee = await SeedEmployeeAsync();
		var user = await SeedUserAsync(employee.Id);
		var cashAccount = await SeedCashAccountAsync();
		var otherAccount = await SeedCashAccountAsync();
		var profile = await SeedRepProfileAsync(employee.Id, cashAccountId: cashAccount.Id);
		SetHttpContext(user.Id);

		var result = await _sut.ValidateCashAccountOwnership(otherAccount.Id);

		result.Should().BeFalse();
	}

	// =========================================
	// 11. ValidateProductInCustody
	// =========================================

	[Fact]
	public async Task ValidateProductInCustody_WhenNoRep_ReturnsFalse()
	{
		SetHttpContext(userId: 1);

		var result = await _sut.ValidateProductInCustody(productId: 1, storeId: 1);

		result.Should().BeFalse();
	}

	[Fact]
	public async Task ValidateProductInCustody_WhenNoCustodyStore_ReturnsFalse()
	{
		var employee = await SeedEmployeeAsync();
		var user = await SeedUserAsync(employee.Id);
		await SeedRepProfileAsync(employee.Id, custodyStoreId: null);
		SetHttpContext(user.Id);

		var result = await _sut.ValidateProductInCustody(1, 1);

		result.Should().BeFalse();
	}

	[Fact]
	public async Task ValidateProductInCustody_WithDifferentStore_ReturnsFalse()
	{
		var employee = await SeedEmployeeAsync();
		var user = await SeedUserAsync(employee.Id);
		var store = await SeedStoreAsync();
		var profile = await SeedRepProfileAsync(employee.Id, custodyStoreId: store.Id);
		SetHttpContext(user.Id);

		var result = await _sut.ValidateProductInCustody(productId: 1, storeId: 999);

		result.Should().BeFalse();
	}

	[Fact]
	public async Task ValidateProductInCustody_WithNoStock_ReturnsFalse()
	{
		var employee = await SeedEmployeeAsync();
		var user = await SeedUserAsync(employee.Id);
		var store = await SeedStoreAsync();
		var profile = await SeedRepProfileAsync(employee.Id, custodyStoreId: store.Id);
		SetHttpContext(user.Id);

		var result = await _sut.ValidateProductInCustody(productId: 1, storeId: store.Id);

		result.Should().BeFalse();
	}

	[Fact]
	public async Task ValidateProductInCustody_WithStock_ReturnsTrue()
	{
		var employee = await SeedEmployeeAsync();
		var user = await SeedUserAsync(employee.Id);
		var store = await SeedStoreAsync();
		var profile = await SeedRepProfileAsync(employee.Id, custodyStoreId: store.Id);
		SetHttpContext(user.Id);

		await SeedStockLotAsync(store.Id, productId: 1, quantity: 10m);

		var result = await _sut.ValidateProductInCustody(productId: 1, storeId: store.Id);

		result.Should().BeTrue();
	}
}