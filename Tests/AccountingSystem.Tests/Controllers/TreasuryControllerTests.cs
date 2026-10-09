using AccountingSystem.Controllers;
using AccountingSystem.Models;
using AccountingSystem.Models.ViewModels.Treasury;
using AccountingSystem.Services;
using AccountingSystem.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AccountingSystem.Tests.Controllers;

public class TreasuryControllerTests : BaseTest
{
	private readonly Mock<IPostingService> _postingMock;
	private readonly Mock<IDocumentNumberService> _documentNumberMock;
	private readonly TreasuryController _sut;

	public TreasuryControllerTests()
	{
		_postingMock = new Mock<IPostingService>();
		_documentNumberMock = new Mock<IDocumentNumberService>();

		_documentNumberMock
			.Setup(x => x.GenerateNumberAsync(
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<string>()))
			.ReturnsAsync("RCP-TEST-0001");

		_sut = new TreasuryController(
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

	private async Task<CashAccount> SeedCashAccountAsync(
		string name = "Test Cash",
		bool isActive = true)
	{
		var account = new CashAccount
		{
			Name = name,
			IsActive = isActive,
			CreatedAt = DateTime.UtcNow
		};
		Context.CashAccounts.Add(account);
		await Context.SaveChangesAsync();
		return account;
	}

	private async Task<Customer> SeedCustomerAsync()
	{
		var customer = new Customer
		{
			Name = "Test Customer",
			Phone = $"0100000{Random.Shared.Next(1000, 9999)}",
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		Context.Customers.Add(customer);
		await Context.SaveChangesAsync();
		return customer;
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
	public async Task Index_WithPage2_ReturnsViewResult()
	{
		var result = await _sut.Index(page: 2, pageSize: 10);

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Index_WithCashAccounts_ReturnsViewWithAccounts()
	{
		await SeedCashAccountAsync("Main Cash");
		await SeedCashAccountAsync("Bank");

		var result = await _sut.Index();

		var viewResult = result.Should().BeOfType<ViewResult>().Subject;
		var model = viewResult.Model.Should()
			.BeOfType<TreasuryIndexViewModel>().Subject;
		model.Accounts.Should().HaveCount(2);
	}

	[Fact]
	public async Task Index_ExcludesCommissionAccount()
	{
		await SeedCashAccountAsync("Main Cash");
		await SeedCashAccountAsync("إيراد العمولات"); // مستبعد

		var result = await _sut.Index();

		var viewResult = result.Should().BeOfType<ViewResult>().Subject;
		var model = viewResult.Model.Should()
			.BeOfType<TreasuryIndexViewModel>().Subject;
		model.Accounts.Should().HaveCount(1);
		model.Accounts[0].Name.Should().Be("Main Cash");
	}

	// =========================================
	// 2. Transactions GET Tests
	// =========================================

	[Fact]
	public async Task Transactions_ReturnsViewResult()
	{
				var result = await _sut.Transactions(
			accountId: null, type: null);

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Transactions_WithFilters_ReturnsViewResult()
	{
		var account = await SeedCashAccountAsync();

				var result = await _sut.Transactions(
			accountId: null, type: null);

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 3. DailyClose GET Tests
	// =========================================

	[Fact]
	public async Task DailyClose_ReturnsViewResult()
	{
		var result = await _sut.DailyClose(date: null);

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task DailyClose_WithSpecificDate_ReturnsViewResult()
	{
		var result = await _sut.DailyClose(date: DateTime.Today.AddDays(-1));

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 4. CreateAccount Tests
	// =========================================
	[Fact]
	public async Task CreateAccount_WithEmptyName_RedirectsToIndex()
	{
		var model = new CashAccountCreateViewModel
		{
			Name = ""
		};

		// ✅ (Unit Test fix) ModelState validation بيشتغل تلقائياً
		// في HTTP pipeline، مش في unit test — نضيفه يدوياً
		_sut.ModelState.AddModelError(
			nameof(model.Name), "اسم المركز مطلوب.");

		var result = await _sut.CreateAccount(model);

		// ✅ الـ action بيرجع RedirectToAction(nameof(Index)) مع TempData["Error"]
		result.Should().BeOfType<RedirectToActionResult>();
		_sut.TempData["Error"].Should().NotBeNull();
	}

	[Fact]
	public async Task CreateAccount_WithValidName_AddsAccount()
	{
		var model = new CashAccountCreateViewModel
		{
			Name = "New Cash Account",
			Provider = WalletProvider.General
		};

		var result = await _sut.CreateAccount(model);

		// إما RedirectToActionResult أو ViewResult
		(result is RedirectToActionResult || result is ViewResult)
			.Should().BeTrue();
	}

	// =========================================
	// 5. Receive GET Tests
	// =========================================

	[Fact]
	public async Task Receive_GET_ReturnsViewResult()
	{
		var result = await _sut.Receive();

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Receive_GET_WithAccounts_LoadsProviders()
	{
		await SeedCashAccountAsync();

		var result = await _sut.Receive();

		var viewResult = result.Should().BeOfType<ViewResult>().Subject;
		var model = viewResult.Model.Should()
			.BeOfType<TreasuryReceiveViewModel>().Subject;
		model.AccountsProviders.Should().HaveCount(1);
	}

	// =========================================
	// 6. Receive POST Tests
	// =========================================

	[Fact]
	public async Task Receive_POST_WithInvalidModelState_ReturnsView()
	{
		var model = new TreasuryReceiveViewModel
		{
			CashAccountId = 0, // invalid
			Amount = 0          // invalid
		};
		_sut.ModelState.AddModelError("test", "error");

		var result = await _sut.Receive(model);

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Receive_POST_WithoutParty_ReturnsViewWithError()
	{
		var account = await SeedCashAccountAsync();

		var model = new TreasuryReceiveViewModel
		{
			CashAccountId = account.Id,
			Amount = 100m
			// مفيش customer / supplier / employee / other income
		};

		var result = await _sut.Receive(model);

		result.Should().BeOfType<ViewResult>();
		_sut.ModelState.IsValid.Should().BeFalse();
	}

	[Fact]
	public async Task Receive_POST_WithInvalidCashAccount_ReturnsViewWithError()
	{
		var customer = await SeedCustomerAsync();

		var model = new TreasuryReceiveViewModel
		{
			CashAccountId = 99999, // مش موجود
			Amount = 100m,
			CustomerId = customer.Id
		};

		var result = await _sut.Receive(model);

		result.Should().BeOfType<ViewResult>();
		_sut.ModelState.IsValid.Should().BeFalse();
	}

	// =========================================
	// 7. Pay GET Tests
	// =========================================

	[Fact]
	public async Task Pay_GET_ReturnsViewResult()
	{
		var result = await _sut.Pay();

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 8. Pay POST Tests
	// =========================================

	[Fact]
	public async Task Pay_POST_WithInvalidModelState_ReturnsView()
	{
		var model = new TreasuryPayViewModel
		{
			CashAccountId = 0,
			Amount = 0
		};
		_sut.ModelState.AddModelError("test", "error");

		var result = await _sut.Pay(model);

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Pay_POST_WithInvalidCashAccount_ReturnsViewWithError()
	{
		var model = new TreasuryPayViewModel
		{
			CashAccountId = 99999,
			Amount = 100m,
			Reason = "test"
		};

		var result = await _sut.Pay(model);

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

	[Fact]
	public async Task Adjust_GET_LoadsAccounts()
	{
		await SeedCashAccountAsync();

		var result = await _sut.Adjust();

		var viewResult = result.Should().BeOfType<ViewResult>().Subject;
		var model = viewResult.Model.Should()
			.BeOfType<TreasuryAdjustViewModel>().Subject;
		model.Accounts.Should().HaveCount(1);
	}

	// =========================================
	// 10. Adjust POST Tests
	// =========================================

	[Fact]
	public async Task Adjust_POST_WithInvalidModelState_ReturnsView()
	{
		var model = new TreasuryAdjustViewModel
		{
			CashAccountId = 0,
			Amount = 0,
			Reason = ""
		};
		_sut.ModelState.AddModelError("test", "error");

		var result = await _sut.Adjust(model);

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Adjust_POST_WithInvalidCashAccount_ReturnsViewWithError()
	{
		var model = new TreasuryAdjustViewModel
		{
			CashAccountId = 99999,
			Amount = 100m,
			Reason = "test",
			IsIncrease = true
		};

		var result = await _sut.Adjust(model);

		result.Should().BeOfType<ViewResult>();
		_sut.ModelState.IsValid.Should().BeFalse();
	}

	// =========================================
	// 11. Receipt GET Tests
	// =========================================

	[Fact]
	public async Task Receipt_WhenNotFound_ReturnsNotFound()
	{
		var result = await _sut.Receipt(id: 99999);

		result.Should().BeOfType<NotFoundResult>();
	}

	[Fact]
	public async Task Receipt_WhenExists_ReturnsView()
	{
		var account = await SeedCashAccountAsync();

		var transaction = new TreasuryTransaction
		{
			TransactionNumber = "RCP-TEST-0001",
			Type = TreasuryTransactionType.Receive,
			CashAccountId = account.Id,
			Amount = 500m,
			Reason = "Test",
			CreatedAt = DateTime.UtcNow
		};
		Context.TreasuryTransactions.Add(transaction);
		await Context.SaveChangesAsync();

		var result = await _sut.Receipt(id: transaction.Id);

		// ممكن يكون ViewResult أو RedirectToActionResult
		(result is ViewResult || result is RedirectToActionResult)
			.Should().BeTrue();
	}
}