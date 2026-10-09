using AccountingSystem.Models;
using AccountingSystem.Services;
using AccountingSystem.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AccountingSystem.Tests.Services;

public class SalesInvoiceServiceTests : BaseTest
{
	private readonly Mock<IPostingService> _postingMock;
	private readonly SalesInvoiceService _sut;

	public SalesInvoiceServiceTests()
	{
		_postingMock = new Mock<IPostingService>();
		_sut = new SalesInvoiceService(Context, _postingMock.Object);
	}

	// =========================================
	// Helpers
	// =========================================

	private async Task<SalesInvoice> SeedInvoiceAsync(
		SalesInvoiceStatus status = SalesInvoiceStatus.Draft,
		int itemCount = 1)
	{
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
			// ✅ (InMemory fix) RowVersion مش بيتولد تلقائياً
			RowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }
		};

		for (int i = 0; i < itemCount; i++)
		{
			invoice.Items.Add(new SalesInvoiceItem
			{
				ProductId = i + 1,
				UnitId = 1,
				Quantity = 1,
				QuantityInBaseUnit = 1,
				UnitPrice = 100,
				TotalPrice = 100
			});
		}

		Context.SalesInvoices.Add(invoice);
		await Context.SaveChangesAsync();

		return invoice;
	}

	// =========================================
	// 1. ConfirmInvoiceAsync — حالات النجاح
	// =========================================

	[Fact]
	public async Task ConfirmInvoiceAsync_WithDraftInvoice_ChangesStatusToConfirmed()
	{
		var invoice = await SeedInvoiceAsync(SalesInvoiceStatus.Draft);

		var result = await _sut.ConfirmInvoiceAsync(invoice.Id, userId: 1);

		result.Success.Should().BeTrue();
		result.Error.Should().BeNull();
		result.Invoice.Should().NotBeNull();
		result.Invoice!.Id.Should().Be(invoice.Id);
	}

	[Fact]
	public async Task ConfirmInvoiceAsync_UpdatesStatusInDatabase()
	{
		var invoice = await SeedInvoiceAsync(SalesInvoiceStatus.Draft);

		await _sut.ConfirmInvoiceAsync(invoice.Id, userId: 1);

		var fromDb = await Context.SalesInvoices
			.AsNoTracking()
			.FirstAsync(x => x.Id == invoice.Id);

		fromDb.Status.Should().Be(SalesInvoiceStatus.Confirmed);
	}

	[Fact]
	public async Task ConfirmInvoiceAsync_SetsConfirmedAtTimestamp()
	{
		var invoice = await SeedInvoiceAsync(SalesInvoiceStatus.Draft);
		var before = DateTime.UtcNow.AddSeconds(-1);

		await _sut.ConfirmInvoiceAsync(invoice.Id, userId: 1);

		var fromDb = await Context.SalesInvoices
			.AsNoTracking()
			.FirstAsync(x => x.Id == invoice.Id);

		fromDb.ConfirmedAt.Should().NotBeNull();
		fromDb.ConfirmedAt.Should().BeAfter(before);
	}

	[Fact]
	public async Task ConfirmInvoiceAsync_ReturnsInvoiceWithItems()
	{
		var invoice = await SeedInvoiceAsync(
			SalesInvoiceStatus.Draft, itemCount: 3);

		var result = await _sut.ConfirmInvoiceAsync(invoice.Id, userId: 1);

		result.Invoice!.Items.Should().HaveCount(3);
	}

	// =========================================
	// 2. ConfirmInvoiceAsync — حالات الفشل
	// =========================================

	[Fact]
	public async Task ConfirmInvoiceAsync_WhenInvoiceNotFound_ReturnsFail()
	{
		var result = await _sut.ConfirmInvoiceAsync(
			invoiceId: 99999, userId: 1);

		result.Success.Should().BeFalse();
		result.Error.Should().Contain("غير موجودة");
		result.Invoice.Should().BeNull();
	}

	[Fact]
	public async Task ConfirmInvoiceAsync_WhenAlreadyConfirmed_ReturnsFail()
	{
		var invoice = await SeedInvoiceAsync(SalesInvoiceStatus.Confirmed);

		var result = await _sut.ConfirmInvoiceAsync(invoice.Id, userId: 1);

		result.Success.Should().BeFalse();
		result.Error.Should().Contain("مسودة");
	}

	[Fact]
	public async Task ConfirmInvoiceAsync_WhenCancelled_ReturnsFail()
	{
		var invoice = await SeedInvoiceAsync(SalesInvoiceStatus.Cancelled);

		var result = await _sut.ConfirmInvoiceAsync(invoice.Id, userId: 1);

		result.Success.Should().BeFalse();
		result.Error.Should().Contain("مسودة");
	}

	[Fact]
	public async Task ConfirmInvoiceAsync_WhenNoItems_ReturnsFail()
	{
		var invoice = await SeedInvoiceAsync(
			SalesInvoiceStatus.Draft, itemCount: 0);

		var result = await _sut.ConfirmInvoiceAsync(invoice.Id, userId: 1);

		result.Success.Should().BeFalse();
		result.Error.Should().Contain("بدون بنود");
	}

	[Fact]
	public async Task ConfirmInvoiceAsync_WhenNoItems_DoesNotChangeStatus()
	{
		var invoice = await SeedInvoiceAsync(
			SalesInvoiceStatus.Draft, itemCount: 0);

		await _sut.ConfirmInvoiceAsync(invoice.Id, userId: 1);

		var fromDb = await Context.SalesInvoices
			.AsNoTracking()
			.FirstAsync(x => x.Id == invoice.Id);

		fromDb.Status.Should().Be(SalesInvoiceStatus.Draft);
		fromDb.ConfirmedAt.Should().BeNull();
	}

	[Fact]
	public async Task ConfirmInvoiceAsync_WhenAlreadyConfirmed_DoesNotModifyTimestamp()
	{
		var invoice = await SeedInvoiceAsync(SalesInvoiceStatus.Confirmed);
		var originalConfirmedAt = invoice.ConfirmedAt;

		await _sut.ConfirmInvoiceAsync(invoice.Id, userId: 1);

		var fromDb = await Context.SalesInvoices
			.AsNoTracking()
			.FirstAsync(x => x.Id == invoice.Id);

		fromDb.ConfirmedAt.Should().Be(originalConfirmedAt);
	}

	// =========================================
	// 3. CancelInvoiceAsync — حالات النجاح
	// =========================================

	[Fact]
	public async Task CancelInvoiceAsync_WithConfirmedInvoice_ReturnsSuccess()
	{
		var invoice = await SeedInvoiceAsync(SalesInvoiceStatus.Confirmed);

		var result = await _sut.CancelInvoiceAsync(invoice.Id, userId: 1);

		result.Success.Should().BeTrue();
		result.Error.Should().BeNull();
	}

	[Fact]
	public async Task CancelInvoiceAsync_ChangesStatusToCancelled()
	{
		var invoice = await SeedInvoiceAsync(SalesInvoiceStatus.Confirmed);

		await _sut.CancelInvoiceAsync(invoice.Id, userId: 1);

		var fromDb = await Context.SalesInvoices
			.AsNoTracking()
			.FirstAsync(x => x.Id == invoice.Id);

		fromDb.Status.Should().Be(SalesInvoiceStatus.Cancelled);
	}

	// =========================================
	// 4. CancelInvoiceAsync — حالات الفشل
	// =========================================

	[Fact]
	public async Task CancelInvoiceAsync_WhenInvoiceNotFound_ReturnsFail()
	{
		var result = await _sut.CancelInvoiceAsync(
			invoiceId: 99999, userId: 1);

		result.Success.Should().BeFalse();
		result.Error.Should().Contain("غير موجودة");
	}

	[Fact]
	public async Task CancelInvoiceAsync_WhenDraft_ReturnsFail()
	{
		var invoice = await SeedInvoiceAsync(SalesInvoiceStatus.Draft);

		var result = await _sut.CancelInvoiceAsync(invoice.Id, userId: 1);

		result.Success.Should().BeFalse();
		result.Error.Should().Contain("مؤكدة");
	}

	[Fact]
	public async Task CancelInvoiceAsync_WhenAlreadyCancelled_ReturnsFail()
	{
		var invoice = await SeedInvoiceAsync(SalesInvoiceStatus.Cancelled);

		var result = await _sut.CancelInvoiceAsync(invoice.Id, userId: 1);

		result.Success.Should().BeFalse();
		result.Error.Should().Contain("مؤكدة");
	}

	[Fact]
	public async Task CancelInvoiceAsync_WhenDraft_DoesNotChangeStatus()
	{
		var invoice = await SeedInvoiceAsync(SalesInvoiceStatus.Draft);

		await _sut.CancelInvoiceAsync(invoice.Id, userId: 1);

		var fromDb = await Context.SalesInvoices
			.AsNoTracking()
			.FirstAsync(x => x.Id == invoice.Id);

		fromDb.Status.Should().Be(SalesInvoiceStatus.Draft);
	}

	// =========================================
	// 5. سيناريوهات مركبة (Lifecycle)
	// =========================================

	[Fact]
	public async Task InvoiceLifecycle_DraftToConfirmedToCancelled()
	{
		var invoice = await SeedInvoiceAsync(SalesInvoiceStatus.Draft);

		// 1) Confirm
		var confirmResult = await _sut.ConfirmInvoiceAsync(invoice.Id, userId: 1);
		confirmResult.Success.Should().BeTrue();

		// 2) Cancel
		var cancelResult = await _sut.CancelInvoiceAsync(invoice.Id, userId: 1);
		cancelResult.Success.Should().BeTrue();

		// 3) Verify final state
		var fromDb = await Context.SalesInvoices
			.AsNoTracking()
			.FirstAsync(x => x.Id == invoice.Id);

		fromDb.Status.Should().Be(SalesInvoiceStatus.Cancelled);
	}

	[Fact]
	public async Task ConfirmInvoiceAsync_TwiceInARow_SecondFails()
	{
		var invoice = await SeedInvoiceAsync(SalesInvoiceStatus.Draft);

		var first = await _sut.ConfirmInvoiceAsync(invoice.Id, userId: 1);
		var second = await _sut.ConfirmInvoiceAsync(invoice.Id, userId: 1);

		first.Success.Should().BeTrue();
		second.Success.Should().BeFalse();
		second.Error.Should().Contain("مسودة");
	}

	[Fact]
	public async Task CancelInvoiceAsync_TwiceInARow_SecondFails()
	{
		var invoice = await SeedInvoiceAsync(SalesInvoiceStatus.Confirmed);

		var first = await _sut.CancelInvoiceAsync(invoice.Id, userId: 1);
		var second = await _sut.CancelInvoiceAsync(invoice.Id, userId: 1);

		first.Success.Should().BeTrue();
		second.Success.Should().BeFalse();
		second.Error.Should().Contain("مؤكدة");
	}

	// =========================================
	// 6. التحقق من عدم استدعاء PostingService
	// =========================================

	[Fact]
	public async Task ConfirmInvoiceAsync_DoesNotCallPostingService()
	{
		// ملاحظة: PostingService مش مستخدم حالياً في SalesInvoiceService
		// ده test وقائي — لو حد أضاف استدعاء مستقبلاً، هنلاحظ
		var invoice = await SeedInvoiceAsync(SalesInvoiceStatus.Draft);

		await _sut.ConfirmInvoiceAsync(invoice.Id, userId: 1);

		_postingMock.Verify(
			x => x.PostSalesInvoiceAsync(
				It.IsAny<SalesInvoice>(),
				It.IsAny<decimal>(),
				It.IsAny<int?>(),
				It.IsAny<int?>()),
			Times.Never);
	}
}